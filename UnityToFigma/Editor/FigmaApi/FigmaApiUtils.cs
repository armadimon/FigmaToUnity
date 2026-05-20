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
        // ------------------------------------------------------------------------------------------------
        // Response cache (Library/UnityToFigma/cache/{fileId}.json)
        // Saves the raw figma file API response so we don't burn the per-month GET file quota
        // (Starter PAT = 6 / file / month) on every Sync.
        // ------------------------------------------------------------------------------------------------
        const string ResponseCacheRoot = "Library/UnityToFigma/cache";

        static string ResponseCachePath(string fileId) => Path.Combine(ResponseCacheRoot, fileId + ".json");

        public static bool TryReadCachedFigmaDocument(string fileId, int ttlHours, out string json, out DateTime writtenAt)
        {
            json = null;
            writtenAt = default;
            var path = ResponseCachePath(fileId);
            if (!File.Exists(path)) return false;
            writtenAt = File.GetLastWriteTimeUtc(path);
            if (ttlHours > 0 && (DateTime.UtcNow - writtenAt).TotalHours > ttlHours) return false;
            try
            {
                json = File.ReadAllText(path);
                return !string.IsNullOrEmpty(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FigmaToUnity] Failed to read cache {path}: {e.Message}");
                return false;
            }
        }

        public static void WriteCachedFigmaDocument(string fileId, string json)
        {
            if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(json)) return;
            try
            {
                if (!Directory.Exists(ResponseCacheRoot)) Directory.CreateDirectory(ResponseCacheRoot);
                File.WriteAllText(ResponseCachePath(fileId), json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FigmaToUnity] Failed to write cache for {fileId}: {e.Message}");
            }
        }

        public static void InvalidateCachedFigmaDocument(string fileId)
        {
            var path = ResponseCachePath(fileId);
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch (Exception e) { Debug.LogWarning($"[FigmaToUnity] Failed to delete cache {path}: {e.Message}"); }
            }
        }

        public static FigmaFile DeserializeFigmaFile(string json)
        {
            return JsonConvert.DeserializeObject<FigmaFile>(json, CreateFigmaJsonSettings());
        }

        /// <summary>
        /// Shared JsonSerializerSettings used for every figma response. Tolerates null primitives
        /// inside numeric arrays (figma occasionally emits e.g. relativeTransform = [[null,null,null],...])
        /// by swallowing the conversion error and leaving the slot at default(0).
        /// </summary>
        public static JsonSerializerSettings CreateFigmaJsonSettings()
        {
            return new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Error = (sender, args) =>
                {
                    var ex = args.ErrorContext.Error;
                    var msg = ex?.Message ?? string.Empty;
                    if (ex is InvalidCastException ||
                        msg.IndexOf("Null object cannot be converted", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        msg.IndexOf("Error converting value {null}", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        args.ErrorContext.Handled = true;
                    }
                },
            };
        }

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
            "Unknown Error", // Unity 6 macOS surfaces HTTP/2 stream INTERNAL_ERROR as result=ConnectionError + error="Unknown Error" + code=200 (partial body).
        };

        static bool IsTransientFailure(UnityWebRequest req)
        {
            // ConnectionError covers HTTP/2 stream aborts that leave responseCode=200 but a truncated body.
            // DataProcessingError covers gzip/chunked decode failures mid-stream.
            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError) return true;
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
        // Aligns with figma's official guidance: honor Retry-After exactly, cap retries low, and surface the
        // diagnostic headers (Plan-Tier / Rate-Limit-Type / Upgrade-Link) so the user can tell apart a
        // transient HTTP/2 abort vs a per-month plan quota hit. Self-invented exponential backoff is useless
        // against the leaky-bucket budget (Starter PAT = 6 GET file / file / month).
        public static async Task<UnityWebRequest> SendGetWithRetryAsync(string url, string figmaAccessToken,
            int timeoutSeconds = 180, int maxAttempts = 3)
        {
            const int FallbackRetryDelayMs = 5_000;
            const int MaxRetryAfterCapMs = 60_000; // do not block the editor longer than 60s; surface the error and let user decide

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
                if (!IsTransientFailure(webRequest) || attempt == maxAttempts)
                {
                    LogRateLimitDiagnostics(webRequest, url);
                    return webRequest;
                }

                int delayMs = FallbackRetryDelayMs;
                var retryAfter = webRequest.GetResponseHeader("Retry-After");
                if (!string.IsNullOrEmpty(retryAfter))
                {
                    if (int.TryParse(retryAfter, out var secs))
                        delayMs = Math.Max(1_000, secs * 1_000);
                    else if (DateTime.TryParse(retryAfter, out var when))
                    {
                        var delta = (when.ToUniversalTime() - DateTime.UtcNow).TotalMilliseconds;
                        if (delta > 0) delayMs = (int)delta;
                    }
                }

                if (delayMs > MaxRetryAfterCapMs)
                {
                    Debug.LogError(
                        $"[FigmaToUnity] Server asked to wait {delayMs / 1000}s (> {MaxRetryAfterCapMs / 1000}s cap). " +
                        $"Aborting retry. Likely Starter plan monthly quota — try again next month or upgrade. URL={url}");
                    LogRateLimitDiagnostics(webRequest, url);
                    return webRequest;
                }

                Debug.LogWarning(
                    $"[FigmaToUnity] Transient failure (attempt {attempt}/{maxAttempts}) for {url}. " +
                    $"Retrying in {delayMs}ms. HTTP {webRequest.responseCode} {webRequest.error}");
                LogRateLimitDiagnostics(webRequest, url);
                await Task.Delay(delayMs);
            }
            return webRequest;
        }

        static void LogRateLimitDiagnostics(UnityWebRequest req, string url)
        {
            if (req == null) return;
            var retryAfter = req.GetResponseHeader("Retry-After");
            var planTier = req.GetResponseHeader("X-Figma-Plan-Tier");
            var rlType = req.GetResponseHeader("X-Figma-Rate-Limit-Type");
            var upgrade = req.GetResponseHeader("X-Figma-Upgrade-Link");
            if (string.IsNullOrEmpty(retryAfter) && string.IsNullOrEmpty(planTier) &&
                string.IsNullOrEmpty(rlType) && string.IsNullOrEmpty(upgrade)) return;
            Debug.Log(
                $"[FigmaToUnity] figma headers — Retry-After={retryAfter ?? "-"}s, " +
                $"X-Figma-Plan-Tier={planTier ?? "-"}, X-Figma-Rate-Limit-Type={rlType ?? "-"}, " +
                $"X-Figma-Upgrade-Link={upgrade ?? "-"} (url={url})");
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
        /// Lightweight fetch that only returns the document + canvas (page) nodes without frame children.
        /// Use this for UI flows that just need the page list (e.g. page-selection picker) so we avoid
        /// pulling the whole file with ?geometry=paths, which triggers HTTP/2 stream aborts and 429s on
        /// large documents.
        /// </summary>
        public static async Task<FigmaFile> GetFigmaDocumentPagesLite(string fileId, string accessToken)
        {
            var url = $"https://api.figma.com/v1/files/{fileId}?depth=1";
            var webRequest = await SendGetWithRetryAsync(url, accessToken);
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"{FormatWebRequestFailure(webRequest, "Figma file (pages-lite) API")} url={url}.");
            }
            try
            {
                return JsonConvert.DeserializeObject<FigmaFile>(webRequest.downloadHandler.text, CreateFigmaJsonSettings());
            }
            catch (Exception e)
            {
                throw new Exception($"Problem decoding Figma pages-lite JSON {e.ToString()}");
            }
        }

        /// <summary>
        /// Download a Figma document by issuing a single GET /v1/files/{key}/nodes?ids=PAGE_IDS&amp;geometry=paths
        /// instead of the full GET file endpoint. Used when OnlyImportSelectedPages is on so the response payload
        /// stays small (one entry per selected page rather than the whole document), which both reduces HTTP/2
        /// stream abort risk and lets users limit the data they're pulling on tight rate-limit budgets.
        /// </summary>
        /// <param name="fileId">Figma File Id</param>
        /// <param name="accessToken">Figma Access Token</param>
        /// <param name="selectedPageIds">Canvas (page) node ids to import</param>
        /// <returns>A synthesized FigmaFile whose document.children is restricted to the selected pages.</returns>
        public static async Task<FigmaFile> GetFigmaDocumentBySelectedPages(string fileId, string accessToken,
            IList<string> selectedPageIds)
        {
            if (selectedPageIds == null || selectedPageIds.Count == 0)
                throw new ArgumentException("selectedPageIds is empty", nameof(selectedPageIds));

            // Get the lite document so we have the canonical document root + metadata.
            var litePromise = GetFigmaDocumentPagesLite(fileId, accessToken);
            var liteFile = await litePromise;
            if (liteFile?.document == null)
                throw new Exception("Pages-lite fetch returned no document");

            var joined = string.Join(",", selectedPageIds);
            var url = $"https://api.figma.com/v1/files/{fileId}/nodes?ids={Uri.EscapeDataString(joined)}&geometry=paths";
            var webRequest = await SendGetWithRetryAsync(url, accessToken);
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"{FormatWebRequestFailure(webRequest, "Figma nodes (selected pages) API")} url={url}.");
            }

            FigmaFileNodes fileNodes;
            try
            {
                fileNodes = JsonConvert.DeserializeObject<FigmaFileNodes>(webRequest.downloadHandler.text, CreateFigmaJsonSettings());
            }
            catch (Exception e)
            {
                throw new Exception($"Problem decoding Figma nodes JSON: {e}");
            }

            if (fileNodes?.nodes == null)
                throw new Exception("Figma nodes response missing 'nodes' map");

            // Replace document.children with only the selected page subtrees (preserving order from user selection).
            var pageChildren = new List<Node>();
            foreach (var pageId in selectedPageIds)
            {
                if (fileNodes.nodes.TryGetValue(pageId, out var miniFile) && miniFile?.document != null)
                    pageChildren.Add(miniFile.document);
                else
                    Debug.LogWarning($"[FigmaToUnity] Selected page id '{pageId}' missing in nodes response.");
            }
            liteFile.document.children = pageChildren.ToArray();

            // Merge components/styles from each page's mini-file (figma returns them per-node entry).
            var mergedComponents = new Dictionary<string, Component>();
            var mergedStyles = new Dictionary<string, Style>();
            foreach (var kv in fileNodes.nodes)
            {
                if (kv.Value?.components != null)
                    foreach (var c in kv.Value.components) mergedComponents[c.Key] = c.Value;
                if (kv.Value?.styles != null)
                    foreach (var s in kv.Value.styles) mergedStyles[s.Key] = s.Value;
            }
            liteFile.components = mergedComponents;
            liteFile.styles = mergedStyles;
            return liteFile;
        }

        /// <summary>
        /// Download a Figma doc from server and deserialize
        /// </summary>
        /// <param name="fileId">Figma File Id</param>
        /// <param name="accessToken">Figma Access Token</param>
        /// <param name="debugOutputPath">Optional Unity asset-relative path for writing downloaded JSON</param>
        /// <returns>The deserialized Figma file</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<FigmaFile> GetFigmaDocument(string fileId, string accessToken, string debugOutputPath = null,
            bool useCache = false, int cacheTtlHours = 0)
        {
            if (useCache && TryReadCachedFigmaDocument(fileId, cacheTtlHours, out var cachedJson, out var writtenAt))
            {
                try
                {
                    Debug.Log($"[FigmaToUnity] Using cached figma response for {fileId} (written {writtenAt:u})");
                    return DeserializeFigmaFile(cachedJson);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[FigmaToUnity] Cache deserialize failed, refetching: {e.Message}");
                    InvalidateCachedFigmaDocument(fileId);
                }
            }

            var url =
                $"https://api.figma.com/v1/files/{fileId}?geometry=paths"; // We need geometry=paths to get rotation and full transform

            FigmaFile figmaFile = null;
            var webRequest = await SendGetWithRetryAsync(url, accessToken);

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                throw new Exception(
                    $"{FormatWebRequestFailure(webRequest, "Figma file API")} url={url}. Check Personal Access Token (401/403) and file id.");
            }

            WriteCachedFigmaDocument(fileId, webRequest.downloadHandler.text);

            try
            {
                figmaFile = JsonConvert.DeserializeObject<FigmaFile>(webRequest.downloadHandler.text, CreateFigmaJsonSettings());
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
                    JsonConvert.DeserializeObject<FigmaServerRenderData>(webRequest.downloadHandler.text, CreateFigmaJsonSettings());
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
                imageFillData = JsonConvert.DeserializeObject<FigmaImageFillData>(webRequest.downloadHandler.text, CreateFigmaJsonSettings());
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
                fileNodes = JsonConvert.DeserializeObject<FigmaFileNodes>(webRequest.downloadHandler.text, CreateFigmaJsonSettings());
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