using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;

namespace UnityToFigma.Editor.FigmaApi
{
    
    /// <summary>
    /// Reason for server rendering
    /// </summary>
    public enum ServerRenderType
    {
        Substitution, // We want to replace a complex node with an image
        Export // We want to export this image
    }
        
    /// <summary>
    /// Encapsulates server render node data
    /// </summary>
    public class ServerRenderNodeData
    {
        public ServerRenderType RenderType = ServerRenderType.Substitution;
        public Node SourceNode;
    }
    
    public static class FigmaApiUtils
    {
        // Figma's edge sometimes terminates large HTTP/2 responses with INTERNAL_ERROR (Curl error 92). The plugin
        // requests entire files with ?geometry=paths which can be very large, so wrap every UnityWebRequest in a
        // retry loop that rebuilds the request on each attempt (UnityWebRequest cannot be reused after Send).
        static readonly string[] TransientErrorHints =
        {
            "INTERNAL_ERROR",
            "HTTP/2",
            "stream ",
            "Curl error 92",
            "Curl error 56",
            "Curl error 18",
            "Curl error 28",
            "Curl error 6",
            "Curl error 7",
            "Connection refused",
            "Connection reset",
            "timed out",
            "timeout",
        };

        static bool IsTransientFailure(UnityWebRequest req)
        {
            var code = req.responseCode;
            if (code == 0 || code == 408 || code == 429 || (code >= 500 && code < 600)) return true;
            var err = req.error ?? string.Empty;
            for (int i = 0; i < TransientErrorHints.Length; i++)
            {
                if (err.IndexOf(TransientErrorHints[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Send a UnityWebRequest GET with retry/backoff for transient HTTP/2 / network failures.
        /// Returns the final UnityWebRequest (success or terminal failure). Caller owns disposal.
        /// </summary>
        public static async Task<UnityWebRequest> SendGetWithRetryAsync(string url, string figmaAccessToken,
            int timeoutSeconds = 180, int maxAttempts = 4)
        {
            UnityWebRequest webRequest = null;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                webRequest?.Dispose();
                webRequest = UnityWebRequest.Get(url);
                if (!string.IsNullOrEmpty(figmaAccessToken))
                    webRequest.SetRequestHeader("X-Figma-Token", figmaAccessToken);
                webRequest.timeout = timeoutSeconds;

                try { await webRequest.SendWebRequest(); }
                catch { /* inspect result below */ }

                if (webRequest.result == UnityWebRequest.Result.Success) return webRequest;
                if (!IsTransientFailure(webRequest) || attempt == maxAttempts) return webRequest;

                var delayMs = (int)Math.Min(8000, 500 * Math.Pow(2, attempt - 1));
                Debug.LogWarning(
                    $"[FigmaToUnity] Transient failure (attempt {attempt}/{maxAttempts}) for {url}. " +
                    $"Retrying in {delayMs}ms. HTTP {webRequest.responseCode} {webRequest.error}");
                await Task.Delay(delayMs);
            }
            return webRequest;
        }

        static string FormatWebRequestFailure(UnityWebRequest webRequest, string requestLabel)
        {
            var code = webRequest.responseCode;
            var err = webRequest.error ?? "";
            var body = webRequest.downloadHandler?.text;
            var snippet = string.IsNullOrEmpty(body)
                ? ""
                : (body.Length > 280 ? body.Substring(0, 280) + "…" : body);
            return
                $"{requestLabel}: HTTP {code} {err}.{(string.IsNullOrEmpty(snippet) ? "" : " Body: " + snippet)}";
        }

        /// <summary>
        /// Encapsulate download data
        /// </summary>
        public class FigmaDownloadQueueItem
        {
            public enum FigmaFileType
            {
                ImageFill,
                ServerRenderedImage
            }

            public FigmaFileType FileType;
            public string Url;
            public string FilePath;
        }
        
        


        /// <summary>
        /// Get Figma File Id from document Url
        /// </summary>
        /// <param name="url"Document Url</param>
        /// <returns>File Id</returns>
        public static (bool, string) GetFigmaDocumentIdFromUrl(string url)
        {
            // Legacy Format is https://www.figma.com/file/{DOC_ID}/{NAME}?node-id={NODE}
            // New format is https://www.figma.com/design/{DOC_ID}/{NAME}?node-id={NODE}
            if (string.IsNullOrWhiteSpace(url))
                return (false, "");
            
            var legacyInitialSection = "https://www.figma.com/file/";
            var modernInitialSection = "https://www.figma.com/design/";

            var legacyInitialSectionIndex = url.IndexOf(legacyInitialSection, StringComparison.Ordinal);
            var modernInitialSectionIndex = url.IndexOf(modernInitialSection, StringComparison.Ordinal);
            
            // If neither found, it's invalid
            if ( legacyInitialSectionIndex!= 0 && modernInitialSectionIndex!=0) return (false, "");
            // Select best fit
            var targetSectionToUse = legacyInitialSectionIndex == 0 ? legacyInitialSection : modernInitialSection;
            
            var remainder = url.Substring(targetSectionToUse.Length);
            var nextSeperatorIndex = remainder.IndexOf('/');
            if (nextSeperatorIndex == -1) return (false, "");
            return (true, remainder.Substring(0, nextSeperatorIndex));
        }

        /// <summary>
        /// Download a Figma doc from server and deserialize
        /// </summary>
        /// <param name="fileId">Figma File Id</param>
        /// <param name="accessToken">Figma Access Token</param>
        /// <param name="debugOutputPath">Optional Unity asset-relative path for writing downloaded JSON</param>
        /// <returns>The deserialized Figma file</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<FigmaFile> GetFigmaDocument(string fileId, string accessToken, string debugOutputPath = null)
        {
            var url =
                $"https://api.figma.com/v1/files/{fileId}?geometry=paths"; // We need geometry=paths to get rotation and full transform

            FigmaFile figmaFile = null;
            var webRequest = await SendGetWithRetryAsync(url, accessToken);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"{FormatWebRequestFailure(webRequest, "Figma file API")} url={url}. Check Personal Access Token (401/403) and file id.");
            }

            try
            {
                // Create a settings object to ignore missing members and null fields that sometimes come from Figma
                JsonSerializerSettings settings = new JsonSerializerSettings()
                {
                    DefaultValueHandling = DefaultValueHandling.Include,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                };
                
                // Deserialize the document
                figmaFile = JsonConvert.DeserializeObject<FigmaFile>(webRequest.downloadHandler.text, settings);

                Debug.Log($"Figma file downloaded, name {figmaFile.name}");
            }
            catch (Exception e)
            {
                throw new Exception($"Problem decoding Figma document JSON {e.ToString()}");
            }

            if (!string.IsNullOrEmpty(debugOutputPath))
            {
                var outputDirectory = Path.GetDirectoryName(debugOutputPath);
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                    Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(debugOutputPath, webRequest.downloadHandler.text);
            }
            return figmaFile;
        }

        /// <summary>
        /// Requests a server-side rendering of nodes from a document, returning list of urls to download
        /// </summary>
        /// <param name="fileId">Figma File Id</param>
        /// <param name="accessToken">Figma Access Token</param>
        /// <param name="serverNodeCsvList">Csv List of nodes to render</param>
        /// <param name="serverRenderImageScale">Scale to render images at</param>
        /// <returns>List of urls to access the rendered images</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<FigmaServerRenderData> GetFigmaServerRenderData(string fileId, string accessToken,
            string serverNodeCsvList, int serverRenderImageScale)
        {
            FigmaServerRenderData figmaServerRenderData = null;
            // Execute server-side rendering. Sending this webRequest will return a list of all images to download
            var serverRenderUrl =
                $"https://api.figma.com/v1/images/{fileId}?ids={serverNodeCsvList}&scale={serverRenderImageScale}&use_absolute_bounds=true";
            var webRequest = await SendGetWithRetryAsync(serverRenderUrl, accessToken);
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"{FormatWebRequestFailure(webRequest, "Figma server render API")} url={serverRenderUrl}");
            }

            try
            {
                figmaServerRenderData =
                    JsonConvert.DeserializeObject<FigmaServerRenderData>(webRequest.downloadHandler.text);
            }
            catch (Exception e)
            {
                throw new Exception($"Problem decoding server render JSON {e.ToString()}");
            }

            return figmaServerRenderData;
        }

        /// <summary>
        /// Downloads image fill data for a Figma document
        /// </summary>
        /// <param name="fileId">Figma File Id</param>
        /// <param name="accessToken">Figma Access Token</param>
        /// <returns>List of image fills for the document</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<FigmaImageFillData> GetDocumentImageFillData(string fileId, string accessToken)
        {
            FigmaImageFillData imageFillData;
            // Download a list all the image fills container in the Figma document
            var imageFillUrl = $"https://api.figma.com/v1/files/{fileId}/images";

            var webRequest = await SendGetWithRetryAsync(imageFillUrl, accessToken);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"{FormatWebRequestFailure(webRequest, "Figma image meta API")} url={imageFillUrl}");
            }
            try
            {
                imageFillData = JsonConvert.DeserializeObject<FigmaImageFillData>(webRequest.downloadHandler.text);
            }
            catch (Exception e)
            {
                throw new Exception($"Problem decoding image fill JSON {e.ToString()}");
            }

            return imageFillData;
        }


        /// <summary>
        /// Retrieves specific nodes from specific files
        /// </summary>
        /// <param name="fileId">Figma File Id</param>
        /// <param name="accessToken">Figma Access Token</param>
        /// <param name="nodeIds">List of Node Ids to process</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task<FigmaFileNodes> GetFigmaFileNodes(string fileId, string accessToken,List<string> nodeIds, string debugOutputPath = null)
        {
            FigmaFileNodes fileNodes;
            var externalComponentsJoined = string.Join(",",nodeIds);
            var componentsUrl = $"https://api.figma.com/v1/files/{fileId}/nodes/?ids={externalComponentsJoined}";

            var webRequest = await SendGetWithRetryAsync(componentsUrl, accessToken);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception($"{FormatWebRequestFailure(webRequest, "Figma nodes API")} url={componentsUrl}");
            }
            try
            {
                fileNodes = JsonConvert.DeserializeObject<FigmaFileNodes>(webRequest.downloadHandler.text);
                if (!string.IsNullOrEmpty(debugOutputPath))
                {
                    var outputDirectory = Path.GetDirectoryName(debugOutputPath);
                    if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                        Directory.CreateDirectory(outputDirectory);
                    File.WriteAllText(debugOutputPath, webRequest.downloadHandler.text);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Problem decoding Figma components JSON {e.ToString()}");
            }

            return fileNodes;
        }


        /// <summary>
        /// Generates a standardised list of files to download 
        /// </summary>
        /// <param name="imageFillData"></param>
        /// <param name="foundImageFills"></param>
        /// <param name="serverRenderData"></param>
        /// <param name="serverRenderNodes"></param>
        /// <returns></returns>
        public static List<FigmaDownloadQueueItem> GenerateDownloadQueue(FigmaImageFillData imageFillData,List<string> foundImageFills,List<FigmaServerRenderData> serverRenderData,List<ServerRenderNodeData> serverRenderNodes, UnityToFigmaSettings settings, FigmaImportReport importReport = null)
        {
            var paths = new FigmaImportPathResolver(settings);
            // Check if each image fill file has already been downloaded. If not, add to download list
            //Dictionary<string, string> filteredImageFillList = new Dictionary<string, string>();
            List<FigmaDownloadQueueItem> downloadList = new List<FigmaDownloadQueueItem>();
            foreach (var keyPair in imageFillData.meta.images)
            {
                // Only download if it is used in the document and not already downloaded
                if (foundImageFills.Contains(keyPair.Key) && !File.Exists(paths.GetPathForImageFill(keyPair.Key)))
                {
                    downloadList.Add(new FigmaDownloadQueueItem
                    {
                        Url=keyPair.Value,
                        FilePath = paths.GetPathForImageFill(keyPair.Key),
                        FileType = FigmaDownloadQueueItem.FigmaFileType.ImageFill
                    });
                }
            }

            // If required, process server render images
           foreach (var serverRenderDataEntry in serverRenderData)
            {
                foreach (var keyPair in serverRenderDataEntry.images)
                {
                    if (string.IsNullOrEmpty(keyPair.Value))
                    {
                        importReport?.RecordFailed();
                        importReport?.AddMessage(
                            $"Server render image missing URL: node {keyPair.Key} could not be downloaded.");
                        Debug.LogWarning($"[UnityToFigma] Can't download image for Server Node {keyPair.Key} because the URL is empty.");
                    }
                    else
                    {
                        // Always overwrite as may have changed
                        downloadList.Add(new FigmaDownloadQueueItem
                        {
                            Url = keyPair.Value,
                            FilePath = paths.GetPathForServerRenderedImage(keyPair.Key, serverRenderNodes),
                            FileType = FigmaDownloadQueueItem.FigmaFileType.ServerRenderedImage
                        });
                    }
                }
            }

            return downloadList;
        }
        

        /// <summary>
        /// Download required files and process
        /// </summary>
        /// <param name="downloadItems"></param>
        public static async Task DownloadFiles(List<FigmaDownloadQueueItem> downloadItems, UnityToFigmaSettings settings,
            FigmaImportReport importReport = null)
        {
            var downloadCount = downloadItems.Count;
            var downloadIndex = 0;
            
            // Cycle through each required image and download
            foreach (var downloadItem in downloadItems)
            {
                EditorUtility.DisplayProgressBar("Importing Figma Document", $"Downloading Server Image {downloadIndex}/{downloadCount}", (float)downloadIndex/(float) downloadCount);
                try
                {
                    // Download and write the image data (retry on transient HTTP/2 failures)
                    var imageDownloadWebRequest = await SendGetWithRetryAsync(downloadItem.Url, null);

                    if (imageDownloadWebRequest.result != UnityWebRequest.Result.Success)
                    {
                        var detail = FormatWebRequestFailure(imageDownloadWebRequest, "Image GET");
                        importReport?.RecordFailed();
                        importReport?.AddMessage(
                            $"Image download failed ({downloadItem.FileType}): '{downloadItem.FilePath}'. {detail}");
                        Debug.LogWarning(
                            $"[UnityToFigma] {detail} url={downloadItem.Url} path={downloadItem.FilePath}");
                        downloadIndex++;
                        continue;
                    }

                    byte[] imageBytes = imageDownloadWebRequest.downloadHandler.data;
                    
                    // Create the directory if needed
                    var directoryPath= Path.GetDirectoryName(downloadItem.FilePath);
                    if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                    
                    File.WriteAllBytes(downloadItem.FilePath,imageBytes);
                    
                    // Refresh the asset database to ensure the asset has been created
                    AssetDatabase.ImportAsset(downloadItem.FilePath);
                    AssetDatabase.Refresh();
                    
                    // Set the properties for the texture, to mark as a sprite and with alpha transparency and no compression
                    TextureImporter textureImporter = (TextureImporter) AssetImporter.GetAtPath(downloadItem.FilePath);
                    textureImporter.textureType = TextureImporterType.Sprite;
                    textureImporter.spriteImportMode = SpriteImportMode.Single;
                    textureImporter.alphaIsTransparency = true;
                    textureImporter.mipmapEnabled = true; // We'll enable mip maps to stop issues at lower resolutions
                    textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
                    textureImporter.sRGBTexture = true;


                    switch (downloadItem.FileType)
                    {
                        case FigmaDownloadQueueItem.FigmaFileType.ImageFill:
                            // We'll want to allow repeating textures to support "tile" mode
                            textureImporter.wrapMode = TextureWrapMode.Repeat;
                            break;
                        case FigmaDownloadQueueItem.FigmaFileType.ServerRenderedImage:
                            // For server rendered images we want to clamp the texture
                            textureImporter.wrapMode = TextureWrapMode.Clamp;
                            break;
                            
                    }
                    
                    textureImporter.SaveAndReimport();

                }
                catch (Exception e)
                {
                    importReport?.RecordFailed();
                    importReport?.AddMessage(
                        $"Image download exception ({downloadItem.FileType}): '{downloadItem.FilePath}'. {e.Message}");
                    Debug.LogWarning(
                        $"Error downloading image file '{downloadItem.Url}' of type {downloadItem.FileType} for path {downloadItem.FilePath}: {e}");
                }
                downloadIndex++;
            }
        }

    
        /// <summary>
        /// Checks that existing assets are in the correct format
        /// </summary>
        public static void CheckExistingAssetProperties(UnityToFigmaSettings settings)
        {
            CheckImageFillTextureProperties(settings);
        }

        /// <summary>
        /// Checks downloaded image fills
        /// </summary>
        private static void CheckImageFillTextureProperties(UnityToFigmaSettings settings)
        {
            var texturesDir = new FigmaImportPathResolver(settings).TexturesDirectory;
            if (!Directory.Exists(texturesDir))
                return;

            foreach (var filePath in Directory.GetFiles(texturesDir))
            {
                var textureImporter = AssetImporter.GetAtPath(filePath) as TextureImporter;
                if (textureImporter == null) continue;
                // Previous versions may not have sRGB set
                if (textureImporter.sRGBTexture) continue;
                textureImporter.sRGBTexture = true;
                textureImporter.SaveAndReimport();
            }
        }
    }
}