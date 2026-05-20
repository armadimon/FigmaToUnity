using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Postprocess
{
    // {ImportRoot}/Textures/ 에 들어오는 신규/갱신 스프라이트에 대해
    // spriteImportMode=Single, spriteMeshType=FullRect, maxTextureSize=원본 픽셀 이상 가장 작은 2의 거듭제곱(32~2048) 강제.
    // spriteBorder(9-slice)는 AI 후처리 패스에서 채우므로 여기서는 건드리지 않는다.
    internal sealed class FigmaSpritePostprocessor : AssetPostprocessor
    {
        private const int MinMaxSize = 32;
        private const int MaxMaxSize = 2048;

        private void OnPreprocessTexture()
        {
            var texturesRoot = FigmaPostprocessPaths.Textures;
            if (string.IsNullOrEmpty(texturesRoot)) return;
            if (!assetPath.StartsWith(texturesRoot, System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            if (importer == null)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            var longSide = Mathf.Max(width, height);
            if (longSide > 0)
            {
                var target = NextPowerOfTwoClamped(longSide);
                if (importer.maxTextureSize != target)
                    importer.maxTextureSize = target;
            }
        }

        private static int NextPowerOfTwoClamped(int value)
        {
            var pot = Mathf.NextPowerOfTwo(value);
            return Mathf.Clamp(pot, MinMaxSize, MaxMaxSize);
        }
    }
}
