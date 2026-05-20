using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityToFigma.Editor.Postprocess
{
    // AI(Claude) 후처리를 요청하기 위한 컨텍스트를 모아 마크다운으로 저장하고
    // 사용자에게 줄 프롬프트 문구를 클립보드에 복사한다.
    //
    // Unity Editor 안에서 Claude 를 직접 호출할 수 없으므로 이 메뉴는 "사람이 Claude 에게
    // 붙여 넣을 입력"을 한 번에 만들어 주는 역할만 한다.
    internal static class FigmaPostprocessMenu
    {
        private static string ContextOutputPath => FigmaPostprocessPaths.Debug + "/PostprocessContext.md";

        [MenuItem("UnityToFigma/Postprocess/Run AI Post-Process (Prepare Context)")]
        public static void PrepareAiContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Figma 임포트 후처리 컨텍스트");
            sb.AppendLine();
            sb.AppendLine($"생성 시각: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"ImportRoot: `{FigmaPostprocessPaths.ImportRoot}`");
            sb.AppendLine();

            sb.AppendLine("## 1. 매니페스트");
            foreach (var path in EnumerateAssets(FigmaPostprocessPaths.Manifest, ".asset"))
                sb.AppendLine($"- `{path}`");

            sb.AppendLine();
            sb.AppendLine("## 2. Figma 원본 JSON");
            sb.AppendLine($"- `{FigmaPostprocessPaths.FigmaOutputJson}`");
            if (File.Exists(FigmaPostprocessPaths.FigmaOutputJson))
            {
                var info = new FileInfo(FigmaPostprocessPaths.FigmaOutputJson);
                sb.AppendLine($"  - size: {info.Length / 1024} KB");
            }
            else
            {
                sb.AppendLine("  - **(파일 없음 - Sync 를 먼저 실행하세요)**");
            }

            sb.AppendLine();
            sb.AppendLine("## 3. 임포트된 프리팹");
            foreach (var root in FigmaPostprocessPaths.PrefabSearchRoots)
            {
                sb.AppendLine($"### {root}");
                if (!AssetDatabase.IsValidFolder(root))
                {
                    sb.AppendLine("- (폴더 없음)");
                    continue;
                }
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { root });
                if (guids.Length == 0)
                {
                    sb.AppendLine("- (없음)");
                    continue;
                }
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    sb.AppendLine($"- `{path}`");
                }
            }

            sb.AppendLine();
            sb.AppendLine("## 4. AI 컨벤션 후처리 체크리스트 (이 도구가 담당)");
            sb.AppendLine("- [ ] Screen 프리팹 명명을 프로젝트 규칙(예: `{Name}Screen`)으로 정리 (필요시)");
            sb.AppendLine("- [ ] 깊이 2 이내 자식 노드명에 프로젝트 접두(예: `Btn_/Txt_/Img_/Scroll_`) 적용");
            sb.AppendLine("- [ ] Figma `reactions`/구조 분석 기반으로 Button 컴포넌트 부착");
            sb.AppendLine("- [ ] `cornerRadius`/사이즈 변화 패턴 기반으로 9-slice 후보 스프라이트 `spriteBorder` 설정");
            sb.AppendLine("- [ ] `clipsContent: true` + 반복 자식 구조에 LayoutGroup 부착 (오탐 없도록 케이스별 판단)");
            sb.AppendLine("- [ ] 동일 이름 MonoBehaviour 가 존재하는 Screen 프리팹에 AddComponent + `[SerializeField]` 자동 매핑");
            sb.AppendLine();
            sb.AppendLine("## 5. ugui-from-screenshot 스킬로 위임된 보정 (Claude 에서 별도 실행)");
            sb.AppendLine("아래 항목은 본 컨벤션 후처리 이후 또는 병행해서 unity-mcp `Unity_RunCommand` 로 처리하세요:");
            sb.AppendLine("- [ ] RectTransform anchor 디자인 의도 추론: 메뉴 `Tools/UnityToFigma Bootstrap/Auto Anchor`");
            sb.AppendLine("- [ ] 좌측 쏠림 진단: `Tools/UnityToFigma Bootstrap/Diagnose Screen Layout`");
            sb.AppendLine("- [ ] 다해상도 반응형 보정: `Tools/UnityToFigma Bootstrap/Apply Responsive Layout`");
            sb.AppendLine("- [ ] 한글 TMP fallback Dynamic SDF: `Tools/UnityToFigma Bootstrap/Setup TMP Korean Fallback`");
            sb.AppendLine("- [ ] 디자인 사이즈 정확 캡처: `Tools/UnityToFigma Bootstrap/Capture Default Screen`");
            sb.AppendLine("- [ ] TMP overflow/wrap 일괄 보정: `Tools/Fix TMP Overflow Settings`");
            sb.AppendLine();
            sb.AppendLine("자세한 휴리스틱: ugui-from-screenshot 스킬의 `references/figma-to-unity-convention.md` 참조.");
            sb.AppendLine("프로젝트별 명명/MonoBehaviour 규칙은 해당 프로젝트의 CLAUDE.md 또는 agent_docs 를 따른다.");

            Directory.CreateDirectory(Path.GetDirectoryName(ContextOutputPath));
            File.WriteAllText(ContextOutputPath, sb.ToString());
            AssetDatabase.ImportAsset(ContextOutputPath);

            var prompt =
                "ugui-from-screenshot 스킬을 invoke 해서 Figma 임포트 후처리를 진행해 주세요. " +
                $"`{ContextOutputPath}` 의 4번 (AI 컨벤션 후처리 체크리스트) 와 5번 (ugui-from-screenshot 위임 항목) 을 모두 수행합니다. " +
                "프로젝트별 명명/MonoBehaviour 규칙은 현재 프로젝트의 CLAUDE.md / agent_docs 를 따르세요.";
            EditorGUIUtility.systemCopyBuffer = prompt;

            Debug.Log($"[FigmaPostprocess] 컨텍스트 저장: {ContextOutputPath}\nClaude 프롬프트가 클립보드에 복사되었습니다.");
            EditorUtility.RevealInFinder(ContextOutputPath);
        }

        private static IEnumerable<string> EnumerateAssets(string folder, string extension)
        {
            if (!AssetDatabase.IsValidFolder(folder)) yield break;
            var full = Path.GetFullPath(folder);
            foreach (var f in Directory.EnumerateFiles(full, "*" + extension, SearchOption.TopDirectoryOnly))
            {
                yield return "Assets" + f.Substring(Application.dataPath.Length).Replace('\\', '/');
            }
        }
    }
}
