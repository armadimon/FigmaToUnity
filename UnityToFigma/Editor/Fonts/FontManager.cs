using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityToFigma.Editor.FigmaApi;
using UnityToFigma.Editor.Import;
using UnityToFigma.Editor.Settings;
using UnityToFigma.Editor.Utils;
using Color = UnityEngine.Color;
using MathUtils = UnityToFigma.Editor.Utils.MathUtils;

namespace UnityToFigma.Editor.Fonts
{
    
    public class FigmaFontMapEntry
    {
        public string FontFamily;
        public int FontWeight;
        public TMP_FontAsset FontAsset;
        public List<FontMaterialVariation> FontmaterialVariations = new List<FontMaterialVariation>();
    }
    
    
    /// <summary>
    /// Class to map text effects (outline and shadow) to material presets
    /// </summary>
    public class FontMaterialVariation
    {
        public bool OutlineEnabled;
        public Color OutlineColor;
        public float OutlineThickness;
        
        public bool ShadowEnabled;
        public Color ShadowColor;
        public Vector2 ShadowDistance;
        
        public Material MaterialPreset;
       
    }
    
    
    public class FigmaFontMap
    {
        public List<FigmaFontMapEntry> FontMapEntries = new List<FigmaFontMapEntry>();

        public FigmaFontMapEntry GetFontMapping(string fontFamily, int fontWeight)
        {
            return FontMapEntries.FirstOrDefault(fontMapEntry => fontMapEntry.FontFamily == fontFamily && fontMapEntry.FontWeight == fontWeight);
        }
    }
    
    /// <summary>
    /// Functionality to manage fonts, retrive and generate font assets
    /// </summary>
    public static class FontManager
    {
        /// <summary>
        /// Generates a map of fonts found int the document and font to map to
        /// </summary>
        /// <param name="figmaFile"></param>
        /// <param name="enableGoogleFontsDownload"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static async Task<FigmaFontMap> GenerateFontMapForDocument(FigmaFile figmaFile, bool enableGoogleFontsDownload, UnityToFigmaSettings settings)
        {
            var paths = new FigmaImportPathResolver(settings);
            FigmaFontMap fontMap = new FigmaFontMap();
            var textNodes = new List<Node>();
            FigmaDataUtils.FindAllNodesOfType(figmaFile.document,NodeType.TEXT, textNodes, 0);
            
            var allProjectFontAssets = AssetDatabase.FindAssets($"t:TMP_FontAsset").Select(guid => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid))).ToList();

            // Cycle through each node, to see if we have a match for each
            foreach (var textNode in textNodes)
            {
                var fontFamily = textNode.style.fontFamily;
                var fontWeight = textNode.style.fontWeight;
                var fontMapEntry = fontMap.GetFontMapping(fontFamily, fontWeight);
                if (fontMapEntry != null) continue;
                
                var newFontMapEntry = new FigmaFontMapEntry
                {
                    FontFamily = fontFamily,
                    FontWeight = fontWeight
                };
                fontMap.FontMapEntries.Add(newFontMapEntry);
                if (GoogleFontLibraryManager.CheckFontExistsLocally(fontFamily, fontWeight, paths))
                {
                    newFontMapEntry.FontAsset = GoogleFontLibraryManager.GetFontAsset(fontFamily, fontWeight, paths);
                }
                else if (enableGoogleFontsDownload && GoogleFontLibraryManager.CheckFontAvailableForDownload(fontFamily, fontWeight))
                {
                    var downloadTask = GoogleFontLibraryManager.ImportFont(fontFamily, fontWeight, paths);
                    await downloadTask;
                    if (downloadTask.Result)
                    {
                        // Success
                        newFontMapEntry.FontAsset=GoogleFontLibraryManager.GetFontAsset(fontFamily, fontWeight, paths);
                    }
                }

                if (newFontMapEntry.FontAsset == null)
                    newFontMapEntry.FontAsset = GetClosestFont(allProjectFontAssets,fontFamily,fontWeight);
                
                
                // TODO - We might want to handle generation of material variations here too
            }

            return fontMap;
        }
        
        
        
        static string StripFontDetailsFromName(TMP_FontAsset fontAsset)
        {
            // By default fonts are added with a hyphen to denote weight variations, so strip everything from hyphen
            var fontName = fontAsset.name.ToLower();
            var hyphenPoint = fontName.IndexOf('-');
            if (hyphenPoint > -1) fontName = fontName.Substring(0, hyphenPoint);
            // Remove any extra keywords
            var stripWords = new string[]
            {
                "sdf",
                "regular",
                "bold",
                "italic",
                " "
            };
            foreach (var stripWord in stripWords)
            {
                fontName= fontName.Replace(stripWord, "");
            }
            return fontName;
        }

        private static TMP_FontAsset GetClosestFont(List<TMP_FontAsset> projectFonts,string fontFamily,int fontWeight)
        {
            var lowestMatchScore = 10000000;
            TMP_FontAsset closestMatch = null;
            
            // Make lower case and strip spaces
            var inputNameLower = fontFamily.ToLower().Replace(" ", "");;
            
            // Use Levenshtein distance to calculate best match from available strings
            foreach (var font in projectFonts)
            {
                var strippedFontName = StripFontDetailsFromName(font);
               
                var newScore = MathUtils.LeventshteinStringDistance(inputNameLower, strippedFontName);
                //Debug.Log($"Checking font name {strippedFontName} vs {inputNameLower} score {newScore}");
                if (newScore < lowestMatchScore)
                {
                    closestMatch = font;
                    lowestMatchScore = newScore;
                }
            }
            return closestMatch;
        }
        
        // Quantization steps for variant dedup. Set conservatively so visually-identical inputs collapse to
        // one material asset: cosmetic float drift (figma JSON float roundtrip, fontSize-derived outline
        // width, alpha epsilon) is below these thresholds, but real design differences exceed them.
        const float ShadowDistanceQuantStep = 0.05f; // ~0.05px
        const float OutlineThicknessQuantStep = 0.01f; // ~1% of normalized 0..0.5 outline width

        public static Material GetEffectMaterialPreset(FigmaFontMapEntry fontMapEntry, bool shadow, Color shadowColor,
            Vector2 shadowDistance, bool outline,
            Color outlineColor, float outlineThickness, UnityToFigmaSettings settings)
        {
            var paths = new FigmaImportPathResolver(settings);

            // Quantize float-y inputs so cosmetically identical variants share one material asset.
            var qShadowColor = QuantizeColor(shadowColor);
            var qShadowDist = QuantizeVec2(shadowDistance, ShadowDistanceQuantStep);
            var qOutlineColor = QuantizeColor(outlineColor);
            var qOutlineThickness = QuantizeFloat(outlineThickness, OutlineThicknessQuantStep);

            // Memory lookup against already-built variants using the quantized signature.
            foreach (var preset in fontMapEntry.FontmaterialVariations)
            {
                if (preset.ShadowEnabled != shadow) continue;
                if (preset.OutlineEnabled != outline) continue;
                if (shadow)
                {
                    if (QuantizeColor(preset.ShadowColor) != qShadowColor) continue;
                    if (QuantizeVec2(preset.ShadowDistance, ShadowDistanceQuantStep) != qShadowDist) continue;
                }
                if (outline)
                {
                    if (QuantizeColor(preset.OutlineColor) != qOutlineColor) continue;
                    if (QuantizeFloat(preset.OutlineThickness, OutlineThicknessQuantStep) != qOutlineThickness) continue;
                }
                return preset.MaterialPreset;
            }

            // Deterministic asset name derived from the quantized key — same signature => same file path,
            // so re-importing or a fresh editor run reuses the existing .mat on disk instead of stacking
            // _variant_N copies.
            string shadowKey = shadow
                ? $"s{qShadowColor:x8}_{(qShadowDist >> 32) & 0xFFFFFFFFL:X8}{qShadowDist & 0xFFFFFFFFL:X8}"
                : "s0";
            string outlineKey = outline
                ? $"o{qOutlineColor:x8}_{(uint)qOutlineThickness:X4}"
                : "o0";
            var materialName = $"{fontMapEntry.FontAsset.name}_{shadowKey}_{outlineKey}";
            var assetPath = $"{paths.FontMaterialPresetsDirectory}/{materialName}.mat";

            // Disk hit: same path already exists — reuse it.
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null)
            {
                fontMapEntry.FontmaterialVariations.Add(new FontMaterialVariation
                {
                    ShadowEnabled = shadow,
                    ShadowColor = shadowColor,
                    ShadowDistance = shadowDistance,
                    OutlineEnabled = outline,
                    OutlineColor = outlineColor,
                    OutlineThickness = outlineThickness,
                    MaterialPreset = existing,
                });
                return existing;
            }

            var newMaterialPreset = new Material(fontMapEntry.FontAsset.material);
            // We use a modified shader that handles distance from edge better
            newMaterialPreset.shader = Shader.Find("Figma/TextMeshPro");
            newMaterialPreset.name = materialName;

            newMaterialPreset.SetKeyword(new LocalKeyword(newMaterialPreset.shader, "UNDERLAY_ON"), shadow);
            if (shadow)
            {
                newMaterialPreset.SetFloat("_UnderlayOffsetX", 0);
                newMaterialPreset.SetFloat("_UnderlayOffsetY", -0.6f);
                newMaterialPreset.SetColor("_UnderlayColor", shadowColor);
            }

            newMaterialPreset.SetKeyword(new LocalKeyword(newMaterialPreset.shader, "OUTLINE_ON"), outline);
            if (outline)
            {
                newMaterialPreset.SetFloat("_OutlineWidth", outlineThickness);
                newMaterialPreset.SetColor("_OutlineColor", outlineColor);
            }

            AssetDatabase.CreateAsset(newMaterialPreset, assetPath);

            fontMapEntry.FontmaterialVariations.Add(new FontMaterialVariation
            {
                ShadowEnabled = shadow,
                ShadowColor = shadowColor,
                ShadowDistance = shadowDistance,
                OutlineEnabled = outline,
                OutlineColor = outlineColor,
                OutlineThickness = outlineThickness,
                MaterialPreset = newMaterialPreset,
            });
            return newMaterialPreset;
        }

        static uint QuantizeColor(Color c)
        {
            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
            return ((uint)r << 24) | ((uint)g << 16) | ((uint)b << 8) | a;
        }

        static int QuantizeFloat(float v, float step)
        {
            return Mathf.RoundToInt(v / step);
        }

        static long QuantizeVec2(Vector2 v, float step)
        {
            int xi = Mathf.RoundToInt(v.x / step);
            int yi = Mathf.RoundToInt(v.y / step);
            return ((long)xi << 32) | ((uint)yi & 0xFFFFFFFFL);
        }
        
    }
}
