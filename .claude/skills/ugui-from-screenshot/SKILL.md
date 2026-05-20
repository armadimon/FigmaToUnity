---
name: ugui-from-screenshot
description: FigmaToUnity 패키지로 Figma 디자인을 일괄 포팅한 뒤 부족한 부분만 unity-mcp 로 보정하는 스킬. 주 용도는 FigmaToUnity 임포트 결과의 후처리이며 모든 Editor 제어는 unity-mcp 도구로 수행한다. 라운드 코너는 자동 보정하지 않고 사용자에게 보고한다. 스크린샷 단독 입력(Path B)은 비권장 폴백으로만 남겨둔다. 'UGUI from Figma', 'FigmaToUnity 후처리', 'Figma to UGUI', 'Figma 화면 가져온 뒤 보정', 'UGUI from screenshot', '스크린샷으로 UI' 등 요청 시 트리거된다.
metadata:
  mcp-server: figma
  tooling: unity-mcp
  scope: figmatounity-post-import
---

# UGUI from FigmaToUnity (with unity-mcp)

이 스킬의 **주 시나리오는 `FigmaToUnity` (구 UnityToFigma) 패키지로 Figma 문서를 일괄 포팅한 결과를 unity-mcp 로 후처리**하는 것이다. Figma URL → FigmaToUnity Sync → 자동 임포트로 만들어진 화면/컴포넌트/이미지/폰트/벡터에 대해, 자동 임포트가 처리하지 못한 항목만 unity-mcp 의 `Unity_RunCommand` 로 C# 스니펫을 실행해 보정한다.

스크린샷 단독 입력 (Path B) 은 **비권장 폴백**으로 남겨둔다. FigmaToUnity 임포트가 불가능한 환경에서만 사용하며, 그 경우에도 Editor 제어는 동일하게 unity-mcp 로 수행한다.

> **이 스킬은 본인 깃 리포 패키지 `armadimon/FigmaToUnity` 와 함께 배포**되므로, 패키지 사용자는 별도 설치 없이 동일 스킬 동작을 보장받는다. 본 스킬은 unity-cli 가 PATH 에 없어도 동작하도록 unity-mcp 채널만 사용한다.

## 핵심 원칙 (전역)

1. **자동 임포트 우선, 보정 최소화**. FigmaToUnity 가 처리한 RectTransform/앵커/색상은 가급적 건드리지 않는다. 비율을 망치는 가장 흔한 원인이다.
2. **라운드 코너는 추가 보정/대체를 하지 않는다**. Path A (FigmaToUnity) 에서는 `FigmaImage.cornerRadius` 가 SDF 로 정확히 처리되므로 그대로 둔다. Path B (스크린샷) 에서는 라운드를 사용자에게 위임한다. **어떤 경우에도 sprite/mask 로 라운드를 흉내내지 말 것.** 자세한 규칙: `references/round-corner-policy.md`

2-1. `**FigmaImage` 컴포넌트는 표준 `UI.Image` 로 변환하지 않는다**. FigmaToUnity 가 라운드 / 스트로크 / 그라디언트 / 멀티 채널 컬러를 SDF 로 한 번에 처리하는 전용 컴포넌트다. `Image` 로 갈아끼우면 라운드/스트로크가 즉시 깨진다. 일반적인 UGUI 워크플로우와 달라 보여도 그대로 둔다. 진짜로 `Image` 가 필요한 케이스 (마스크, 9-slice 등) 는 사용자가 명시 요청 시에만 변경하고, 라운드 정보가 손실됨을 안내한다.
3. **에이전트는 차이만 처리**. 자동 임포트가 만든 결과 vs 레퍼런스 스크린샷을 비교해서 실제로 빠진/잘못된 항목만 수정한다. 전체를 다시 만들지 않는다.
4. **다이얼로그가 뜨는 작업은 자동화하지 말 것**. 다이얼로그를 띄우는 환경(설정 누락, PAT 미입력, TMP Essentials 미설치 등)은 사전 부트스트랩으로 박아두거나 사용자에게 한 번 요청한다.
5. **Sync 전 Figma 1차 정리 필수**. 페이지에 화면 루트 Frame 이 없거나 한국어/자동생성 레이어명이 섞여 있으면, 에이전트가 `use_figma` 로 먼저 정리한 뒤에 sync 한다. 자세한 정책: `references/figma-prep-policy.md`
6. **Canvas 는 화면 단위로 referenceResolution 을 맞춘다**. 부트스트랩의 `Instantiate Default Screen` 메뉴가 Screen prefab 의 `RectTransform.sizeDelta` 를 읽어 Canvas `CanvasScaler.referenceResolution` 으로 자동 설정한다. 하나의 Canvas 에 여러 화면을 욱여넣지 않는다 (필요 시 화면별로 새 Canvas 사용 또는 기본 옵션 `clearCanvasOnInstantiate=true` 로 한 번에 한 화면만 표시).
7. **한글 텍스트는 자동 폰트 변경하지 않는다**. TMP fallback 에 한글이 없으면 `□` 로 표시되는데, 에이전트는 임의로 폰트를 깔지 않고 사용자에게 안내만 한다. 자세한 정책: `references/font-fallback-policy.md`
8. **Editor 제어는 unity-mcp 만 사용한다**. 모든 Unity Editor 호출은 `mcp__unity-mcp__Unity_RunCommand` (임의 C# 실행) · `mcp__unity-mcp__Unity_GetConsoleLogs` (콘솔 조회) · `mcp__unity-mcp__Unity_Camera_Capture` / `Unity_SceneView_Capture2DScene` (캡처) 로 처리한다. 본 스킬 문서에 등장하는 unity-cli 명령 예시는 모두 Phase 0 의 매핑 표대로 C# 스니펫 (`IRunCommand`) 호출로 옮긴 뒤 실행한다. unity-cli 가 PATH 에 있어도 이 스킬에서는 호출하지 않는다.
9. **프리팹 편집 중에는 컴파일/리프레시 트리거 금지**. `AssetDatabase.Refresh()` / `AssetDatabase.SaveAssets()` 를 호출하는 C# 스니펫을 unity-mcp 로 실행하면 편집 상태가 손실된다. 부득이한 경우 사용자에게 프리팹 저장 후 진행 요청.

## Prerequisites

이 스킬은 **unity-mcp 채널만 사용**한다. 사용자 PC 에서 다음이 갖춰져 있어야 한다.

- Unity Editor 가 실행 중일 것
- `com.unity.ai.assistant` 패키지가 프로젝트 manifest 에 포함 (FigmaToUnity 와 별개)
- `Edit > Project Settings > AI > Unity MCP Server` 패널에서 **Bridge Start** 클릭
- 첫 연결 시 같은 패널에서 **Accept** 클릭하여 외부 호출 승인
- `.mcp.json` 에 `unity-mcp` relay 등록 (Claude Code/Claude Desktop)

Path A (FigmaToUnity) 추가 요구사항:

- `armadimon/FigmaToUnity` (구 `com.simonoliver.unitytofigma`) 패키지 설치 — Phase 0 에서 자동 보장
- Figma Personal Access Token (PAT) — 사용자에게 1회 입력 요청
- TextMeshPro Essential Resources 임포트
- (선택) Figma MCP 서버 — 검증 단계용 스크린샷 비교에만 사용

unity-mcp Bridge 가 5xx 로 실패하면 우선 Bridge 재시작 + Accept 상태부터 확인한다 (스킬에서 자동 복구 불가능, 사용자 조치 필요).

## Repository & Auto-Update

이 스킬은 [geuneda/ugui-from-screenshot](https://github.com/geuneda/ugui-from-screenshot) 레포의 `skill/` 디렉토리에서 가져와 `armadimon/FigmaToUnity` 패키지의 `.claude/skills/ugui-from-screenshot/` 에 동봉 배포된다. 다른 PC에서 개선한 뒤 두 리포 모두에 반영하는 것이 정상 경로다.

- 원본 레포: `https://github.com/geuneda/ugui-from-screenshot` (public, `main` 브랜치)
- 배포 위치: `armadimon/FigmaToUnity` 리포 안 `.claude/skills/ugui-from-screenshot/`
- 로컬 버전 파일: `.commit-hash` (현재 반영된 원격 커밋 SHA)

### Phase −1: 업데이트 체크 (스킬 실행 시 최우선 수행)

Phase 0 이전에 **반드시** 업데이트 여부를 확인한다. 원격 main HEAD가 로컬 `.commit-hash`와 다르면 최신 변경을 반영한 뒤 워크플로우를 시작해야 한다. 이 단계는 unity-mcp 와 무관한 git/curl 동작이므로 기존 bash 스크립트를 그대로 사용한다.

**자동 체크 & 적용**:

```bash
# 스킬 루트에서
bash scripts/check_update.sh --auto
```

- `--auto`: 변경이 있으면 확인 없이 자동 업데이트 (권장, 비파괴적 — 기존 파일은 `.backup-<timestamp>/` 로 보관)
- 옵션 없이 실행하면 인터랙티브(y/N 확인)
- `--check`: exit code로만 상태 반환 (0=최신, 1=업데이트 있음, 2=오류)

동기화 대상은 `SKILL.md`, `references/`, `scripts/`, `assets/` 만이며, 그 외 로컬 파일(예: 작업 중인 임시 데이터)은 유지된다. 완료 후 `.commit-hash`가 새 SHA로 갱신된다.

**체크 실패 시**: 네트워크 오류 등으로 실패해도 워크플로우는 이전 버전으로 계속 진행한다. 단, 로그에 "스킬 업데이트 확인 실패"를 기록하고 사용자에게 공지한다.

## Phase −2: unity-mcp Bridge 상태 확인 (스킬 실행 시 Phase −1 직후 수행)

이 스킬의 모든 Editor 호출은 unity-mcp 채널로 진행된다. **Phase 0 진입 전에 반드시** Bridge 가 살아있는지 확인하고, 죽어 있으면 사용자에게 1회 안내한다 (자동 복구 불가, Unity Editor UI 조작 필요).

체크 절차:

1. `Unity_GetConsoleLogs (logTypes="Log", maxEntries=1, includeStackTrace=false)` 를 호출해 응답이 오는지 확인.
2. 실패 (5xx / no response) 시 사용자에게 다음 안내:

   ```
   unity-mcp Bridge 가 응답하지 않습니다. Unity Editor 에서
   Edit > Project Settings > AI > Unity MCP Server 패널을 열고
   Bridge Start 를 누른 뒤 첫 연결 다이얼로그에서 Accept 를 클릭해주세요.
   ```

3. 사용자가 Bridge 를 켰다고 응답하면 다시 한 번 `Unity_GetConsoleLogs` 호출로 검증.

**자동 복구 시도 금지**: Editor 자체를 재시작하거나 `.mcp.json` 을 수정하지 않는다. Bridge UI 조작은 사용자의 책임 영역.

## Phase 0: Pre-flight 체크 (필수 - 실전에서 반드시 먼저 수행)

unity-mcp 는 `Unity_RunCommand` 로 임의 C# 코드를 실행할 수 있는 단일 강력 채널이다. **본 스킬은 모든 unity-cli 명령을 동등한 C# 스니펫으로 옮겨서 호출한다.** 아래 매핑 표를 기준으로 본문 곳곳의 명령 예시를 변환한다.

### unity-cli → unity-mcp 매핑 표 (2026-05-20)

| 의도                     | unity-cli (사용 금지)                                       | unity-mcp 호출                                                                                                                                                                                                  |
| ---------------------- | ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Editor/Bridge 상태       | `unity-cli status`                                      | `Unity_GetConsoleLogs(maxEntries=1)` 로 응답 여부만 확인                                                                                                                                                              |
| 콘솔 조회                  | `unity-cli --json console get`                          | `Unity_GetConsoleLogs(logTypes="Log,Warning,Error", maxEntries=200, includeStackTrace=true)`                                                                                                                |
| 콘솔 비우기                 | `unity-cli console clear`                               | `Unity_RunCommand` 로 `UnityEditorInternal.LogEntries.Clear()` (reflection) 또는 `Debug.ClearDeveloperConsole()`                                                                                                |
| 메뉴 실행                  | `unity-cli menu execute path="Tools/..."`               | `Unity_RunCommand` 안에서 `EditorApplication.ExecuteMenuItem("Tools/...")`                                                                                                                                     |
| 에셋 컴파일 트리거             | `unity-cli menu execute path="Assets/Refresh"`          | `Unity_RunCommand` 안에서 `AssetDatabase.Refresh()` (단, 프리팹 편집 모드면 호출 금지)                                                                                                                                       |
| 패키지 목록/추가              | `unity-cli --json package list` / `package add ...`     | `Unity_RunCommand` 안에서 `UnityEditor.PackageManager.Client.List(true)` / `Client.Add("git+https://...")` 후 `EditorApplication.update` 폴링                                                                       |
| 씬 정보                   | `unity-cli scene info`                                  | `Unity_RunCommand` 안에서 `EditorSceneManager.GetActiveScene()` 의 path/isLoaded/isDirty 출력 → `result.Log(...)` 로 회수                                                                                              |
| GameObject 조회          | `unity-cli --json gameobject get name="X"`              | `Unity_RunCommand` 안에서 `GameObject.Find("X")` + Transform 트리/RectTransform 정보 직렬화 후 `result.Log(JsonUtility.ToJson(...))`                                                                                     |
| GameObject reparent    | `unity-cli gameobject reparent name=X newParentName=Y`  | `Unity_RunCommand` 안에서 `Undo.SetTransformParent(child.transform, newParent.transform, "Reparent")` (RectTransform 보존 시 `worldPositionStays=false` 동등 처리)                                                       |
| 에셋 → 씬 인스턴스화           | `unity-cli asset add-to-scene assetPath=...`            | `Unity_RunCommand` 안에서 `AssetDatabase.LoadAssetAtPath<GameObject>(path)` + `PrefabUtility.InstantiatePrefab(prefab, parent)` (부모 지정 가능) → `result.RegisterObjectCreation(obj)`                                |
| GameView 스크린샷 (W×H 강제) | `unity-cli ui screenshot.capture outputPath=... width=W height=H` | `Unity_RunCommand` 로 임시 카메라+RT 렌더링 후 `File.WriteAllBytes` (FigmaToUnity 부트스트랩의 `Capture Default Screen` 메뉴 호출로 위임 권장) 또는 `Unity_Camera_Capture` (Camera GameObject 가 따로 있을 때)                                |
| Scene View 캡처          | `unity-cli` 미지원                                         | `Unity_SceneView_Capture2DScene` (2D 영역) / `Unity_SceneView_CaptureMultiAngleSceneView` (3D, 본 스킬에선 거의 안 씀)                                                                                                  |
| 컴포넌트 값 갱신              | `unity-cli component update name=X type=... values=...` | `Unity_RunCommand` 안에서 `GameObject.Find` → 컴포넌트 SerializedObject 또는 직접 프로퍼티 설정 → `result.RegisterObjectModification(component)`                                                                                |
| TMP 텍스트 일괄 변경          | `unity-cli component update ... values='{"text":"..."}'` | `Unity_RunCommand` 안에서 `TextMeshProUGUI.text = "..."` 직접 설정 (쉘 이스케이프 문제 자체가 사라짐 — `scripts/batch_set_texts.py` 불필요)                                                                                            |

### Unity_RunCommand 골든 템플릿 (모든 보정/임포트 호출에 공통)

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 1) 대상 조회
        var go = GameObject.Find("Header");
        if (go == null) { result.LogError("Header not found"); return; }

        // 2) 변경 전 등록
        var image = go.GetComponent<Image>();
        result.RegisterObjectModification(image);

        // 3) 변경 수행
        if (ColorUtility.TryParseHtmlString("#1A1A2EFF", out var c))
            image.color = c;

        // 4) 결과 로깅
        result.Log("Header color updated to {0}", image.color);
    }
}
```

**필수 규칙** (unity-mcp 스킬 사양):

| 항목 | 요구사항 |
|------|----------|
| 클래스명 | **반드시 `CommandScript`** (다른 이름은 NullReferenceException) |
| 접근 제어자 | **`internal`** (public 은 "Inconsistent Accessibility" 컴파일 에러) |
| 생성 등록 | `result.RegisterObjectCreation(obj)` |
| 수정 등록 | `result.RegisterObjectModification(obj)` (변경 직전에 호출) |
| 삭제 | `result.DestroyObject(obj)` (직접 `Object.DestroyImmediate` 금지) |
| 로깅 | `result.Log/LogWarning/LogError("{0}", ...)` (Debug.Log 도 허용되지만 회수 어려움) |
| 코드 구조 | Top-level statements 금지, 반드시 클래스 안에 작성 |

**환경변수 → Editor 전달은 불가능**:

unity-mcp 도 unity-cli 와 동일하게 외부 채널이라 셸의 `export FIGMA_PAT=...` 가 Unity 프로세스에 전달되지 **않는다**. Path A 의 ContextFile 기반 동작은 그대로 유지하되, 파일을 만드는 주체가 bash 스크립트가 아니라 **`Unity_RunCommand` 로 실행하는 C# 스니펫이 직접 `File.WriteAllText({PROJECT}/Library/UguiFigmaContext.json, json)`** 으로 작성한다. 자세한 흐름은 Phase 1A Step 1A.3 참조.

**빠지면 안 되는 사전 준비** (모두 `Unity_RunCommand` 1회 실행으로 처리 권장):

1. 현재 씬 확인 — `EditorSceneManager.GetActiveScene()` 의 path/isLoaded/isDirty 를 result.Log 로 출력 → **BootScene 등 중요 씬 건드리지 말 것**. FigmaToUnity 임포트 시 새 씬 생성 권장.
2. 기존 UI 계층 조회 — `GameObject.Find("Canvas")` / `Transform` 트리 spot-check.
3. Figma URL 입력 시: **FigmaToUnity 패키지 설치 보장** (`UnityEditor.PackageManager.Client.List(true)` 결과에 `com.armadimon.figmatounity` 또는 `com.simonoliver.unitytofigma` 존재 여부 검사 → 없으면 `Client.Add("https://github.com/armadimon/FigmaToUnity.git")` 호출 후 폴링).
4. TMP Essentials 확인 — `TMP_Settings.instance` 가 null 이거나 fallback 이 비어있으면 `TMPro.TMP_PackageResourceImporter.ImportResources()` 시도 (다이얼로그 회피). 실패 시 사용자에게 `Window > TextMeshPro > Import TMP Essential Resources` 요청.
5. Editor 가 Play Mode 가 아닌지 확인 — `EditorApplication.isPlayingOrWillChangePlaymode == false`.

**상세 내용**: `references/unity-cli-gotchas.md` 참조 (unity-cli 시행착오 + unity-mcp 호출 시 동등 변환 정리).

## 입력 소스 판별

사용자가 제공하는 입력에 따라 두 가지 경로로 분기한다. **Path A 가 사실상 표준 경로**이고, Path B 는 FigmaToUnity 임포트가 불가능한 환경에서만 사용하는 비권장 폴백이다.


| 입력                                 | 경로                                       | 우선도          |
| ---------------------------------- | ---------------------------------------- | ------------ |
| Figma URL (`figma.com/design/...`) | **Path A: FigmaToUnity 일괄 임포트 → unity-mcp 보정** | **표준 (필수)**  |
| 스크린샷 파일 경로 (`*.png`, `*.jpg`)      | **Path B: 비전 분석 → unity-mcp 구성**         | 폴백 (비권장)     |


두 경로 모두 Phase 3 (검증 루프) 이후는 동일한 워크플로우를 따른다. Path B 진입 시 사용자에게 "FigmaToUnity 임포트 경로(Path A) 사용 가능 여부"를 한 번 확인한다.

---

## Phase 1: Setup & Design Acquisition

### Path A: Figma URL 입력 (FigmaToUnity 일괄 임포트 + unity-mcp 보정) — 표준

이 경로의 핵심은 **에이전트가 노드를 하나씩 분석하지 않고 FigmaToUnity 패키지에 한 번에 맡기는 것**이다.
이전 버전처럼 MCP `get_design_context` 결과를 보고 `ui.`* 명령을 노드별로 호출하지 말 것 (비율 깨짐의 주범).

자세한 흐름·한계·트러블슈팅: `references/unity-to-figma-workflow.md` (unity-cli 잔존 표기는 Phase 0 매핑 표 기준으로 변환).

#### Step 1A.1: 사용자 입력 수집

다음 정보를 사용자로부터 받는다 (없으면 1회 질의):


| 항목                          | 필수  | 비고                                                                                                                                    |
| --------------------------- | --- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Figma 문서 URL                | O   | 페이지/노드가 아닌 **문서 URL** (FigmaToUnity 는 fileId 기준으로 전체 문서를 가져옴)                                                                         |
| Figma Personal Access Token | O   | [https://www.figma.com/developers/api#authentication](https://www.figma.com/developers/api#authentication). PlayerPrefs 에 1회 저장 후 재사용 |
| 프로젝트 절대 경로                  | O   | unity-mcp Bridge 가 붙어있는 Unity 프로젝트 루트                                                                                                  |
| 프로토타입 플로우 빌드 여부             | X   | 기본 false. 화면 전환 자동 생성이 필요할 때만 true                                                                                                    |
| 가져올 페이지                     | X   | 기본 전체. 일부만 원하면 사용자에게 별도 확인 후 처리                                                                                                       |


#### Step 1A.2: 환경 보장 (unity-mcp 단일 호출)

다음 한 번의 `Unity_RunCommand` 로 (1) FigmaToUnity 패키지 존재 확인 → 없으면 자동 설치, (2) TMP Essentials 존재 확인 까지 일괄 처리한다.

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 1) FigmaToUnity 패키지 (com.armadimon.figmatounity / com.simonoliver.unitytofigma)
        var listReq = Client.List(true);
        while (!listReq.IsCompleted) { System.Threading.Thread.Sleep(50); }
        bool hasPkg = false;
        foreach (var p in listReq.Result)
        {
            if (p.name == "com.armadimon.figmatounity" || p.name == "com.simonoliver.unitytofigma")
            { hasPkg = true; break; }
        }
        if (!hasPkg)
        {
            result.Log("FigmaToUnity 미설치 → git URL 추가");
            var addReq = Client.Add("https://github.com/armadimon/FigmaToUnity.git");
            while (!addReq.IsCompleted) { System.Threading.Thread.Sleep(100); }
            if (addReq.Status != StatusCode.Success)
            { result.LogError("패키지 추가 실패: {0}", addReq.Error?.message ?? "unknown"); return; }
            AssetDatabase.Refresh();
        }

        // 2) TMP Essentials (TMP_Settings 가 null 이거나 fallback 리스트 미초기화 시 안내)
        var tmp = TMPro.TMP_Settings.instance;
        if (tmp == null || tmp.fallbackFontAssets == null)
        {
            result.LogWarning("TMP Essentials 미설치 → Window > TextMeshPro > Import TMP Essential Resources 안내 필요");
        }

        result.Log("env ok: figmaToUnityInstalled={0}, tmp={1}", hasPkg, tmp != null);
    }
}
```

호출 후 `Unity_GetConsoleLogs(logTypes="Log,Warning,Error", maxEntries=30)` 로 결과를 회수해 다음 Step 진행 여부를 판단한다.

> **프리팹 편집 모드 주의**: 위 스니펫은 `AssetDatabase.Refresh()` 를 부르므로 프리팹 편집 중이면 호출 금지. 사용자에게 프리팹 저장 요청 후 재시도.

#### Step 1A.2.5: Figma 1차 정리 (Sync 전 위생 점검)

`get_metadata` 로 대상 노드를 한 번 훑은 뒤 다음 4가지를 점검한다. 자세한 절차·코드: `references/figma-prep-policy.md`.


| 점검 항목                                            | 처리                                                   |
| ------------------------------------------------ | ---------------------------------------------------- |
| 화면 루트 Frame 부재 (Page 직접 자식으로 평면 나열)              | `use_figma` 로 `MainScreen` Frame 생성 + 모든 노드 reparent |
| 한국어/특수문자/공백 들어간 레이어 이름                           | ID → 영문 PascalCase 매핑 후 `use_figma` 로 일괄 변경          |
| `레이어 1 복사 4` / `사각형 19 복사 3` / `그룹 5` 같은 자동생성 이름 | 의미 영문명으로 변경 (예: `LobbyTabBg`, `EnergyResourceBg`)    |
| 같은 시각적 컴포넌트가 형제 평면화                              | 같은 Frame 으로 묶기 (Auto Layout 강제는 안 함)                 |


레이어 *이름* 만 영문화하고 텍스트 *내용* (`characters`) 은 절대 건드리지 않는다 (디자인 의도 보존). 정리 후 `use_figma` 응답의 `missing` 가 비어있어야 하고, 새 MainScreen 의 ID 를 다음 단계 URL 의 `node-id` 에 반영한다.

**한 번의 use_figma 호출 예시** (wrap + rename 동시):

```javascript
const page = figma.currentPage;
let main = page.children.find(n => n.name === 'MainScreen' && n.type === 'FRAME');
if (!main) {
  main = figma.createFrame();
  main.name = 'MainScreen';
  main.x = 0; main.y = 0;
  main.resize(1440, 3040); // 좌표 영역 추정 (Page bounds)
  main.fills = []; main.clipsContent = true;
  page.appendChild(main);
  for (const node of page.children.filter(c => c !== main).slice()) main.appendChild(node);
}
const renameMap = { '3:3': 'Background', '3:7': 'BottomTabBar', /* ... */ };
let renamed = 0;
for (const id of Object.keys(renameMap)) {
  const node = await figma.getNodeByIdAsync(id);
  if (node) { node.name = renameMap[id]; renamed++; }
}
return { mainScreenId: main.id, mainChildCount: main.children.length, renamed };
```

#### Step 1A.3: 일괄 임포트 실행 (unity-mcp 단일 호출)

`UnityToFigmaBootstrap.cs` 는 Editor 측 메뉴(`Tools/UnityToFigma Bootstrap/*`)를 제공한다 (assets/ 폴더에 복사본 포함). 이 단계에서는 ContextFile 작성 + PAT EditorPrefs 저장 + Sync 메뉴 실행을 **한 번의 `Unity_RunCommand`** 로 처리한다 (구버전의 `run_unity_to_figma_sync.sh` 를 C# 으로 옮긴 형태).

전체 흐름:

- 부트스트랩 스크립트가 프로젝트 `Assets/Editor/UnityToFigmaBootstrap.cs` 에 없으면 사용자에게 1회 복사 안내 (또는 패키지에 동봉된 정식 메뉴 사용)
- 설정 에셋 (`Assets/UnityToFigmaSettings.asset`) 자동 생성 + URL 주입
- PAT 을 `PlayerPrefs("FIGMA_PERSONAL_ACCESS_TOKEN")` 에 저장
- `BuildPrototypeFlow=false` 강제 (다이얼로그 회피)
- `UnityToFigma/Sync Document` 메뉴 실행

`Unity_RunCommand` 본문 예시:

```csharp
using System.IO;
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // --- 입력값 (호출 시마다 치환) ---
        string figmaUrl = "https://www.figma.com/design/.../...";
        string figmaPat = "figd_xxx";
        bool buildPrototypeFlow = false;
        string defaultScreen = "MainScreen";
        string[] selectedPages = null; // 예: new[] {"Page 3 - Settings Test"}
        string koreanFontPath = "Assets/Fonts/NotoSansKR-Regular.ttf";

        // 1) ContextFile (Library/UguiFigmaContext.json)
        var ctx = new {
            documentUrl = figmaUrl,
            personalAccessToken = figmaPat,
            buildPrototypeFlow = buildPrototypeFlow,
            defaultScreenName = defaultScreen,
            selectedPages = selectedPages,
            koreanFontPath = koreanFontPath,
            cleanOtherScreens = true,
            clearCanvasOnInstantiate = true,
            syncGameViewAspect = true,
        };
        var ctxPath = Path.Combine(Application.dataPath, "../Library/UguiFigmaContext.json");
        File.WriteAllText(ctxPath, JsonUtility.ToJson(ctx, true));

        // 2) PAT 영속화
        EditorPrefs.SetString("ugui.figma.pat", figmaPat);
        PlayerPrefs.SetString("FIGMA_PERSONAL_ACCESS_TOKEN", figmaPat);

        // 3) 페이지 선택이 있으면 PreparePageSelection 먼저
        if (selectedPages != null && selectedPages.Length > 0)
            EditorApplication.ExecuteMenuItem("Tools/UnityToFigma Bootstrap/Prepare Page Selection");

        // 4) Sync
        var ok = EditorApplication.ExecuteMenuItem("UnityToFigma/Sync Document");
        if (!ok) { result.LogError("Sync Document 메뉴 실행 실패"); return; }

        result.Log("Sync 시작. 완료 폴링은 별도 호출로 진행");
    }
}
```

호출 직후 `Unity_GetConsoleLogs(logTypes="Log,Warning,Error", maxEntries=300)` 를 폴링해 `[UnityToFigma]` / `UnityToFigma import:` 로그가 나올 때까지 대기. 컴파일 모드 (`EditorApplication.isCompiling`) 가 끝나지 않은 상태에서는 결과 로그가 늦게 나올 수 있다.

옵션 (ContextFile 키로 모두 표현 가능):

- `buildPrototypeFlow=true` : PrototypeFlow 빌드 활성화 (씬 다이얼로그 발생 가능 → 권장하지 않음)
- `reportPath=...` : 리포트 출력 경로 변경 (기본 `Assets/_Temp/UnityToFigmaReport.json`)
- `defaultScreenName="MainScreen"` : 후속 `Instantiate Default Screen` 메뉴가 띄울 Screen prefab 이름. EditorPrefs(`ugui.figma.defaultScreenName`) 로 영속화하면 Sync 이후에도 유지.
- `selectedPages=["Page 3 - Settings Test"]` : 특정 페이지만 임포트. 매칭은 정확 이름 (case-insensitive) 또는 `'Prefix*'` 와일드카드. **다른 페이지의 prefab 자동 생성을 막고 싶을 때 필수**. 위 스니펫이 `PreparePageSelection` 메뉴를 sync 직전에 자동 호출. (2026-04-21 검증)
- `koreanFontPath="Assets/Fonts/NotoSansKR-Regular.ttf"` : 한글 폰트 SDF 자동 생성 + TMP fallback 등록 시 사용할 폰트 경로. 별도로 `Setup TMP Korean Fallback` 메뉴를 호출해야 적용됨 (Step 1A.5c 참고).

ContextFile (`Library/UguiFigmaContext.json`) 에 추가로 둘 수 있는 키:

- `"cleanOtherScreens"` (bool, 기본 true) : 씬 내 다른 Screen prefab 인스턴스 자동 정리
- `"clearCanvasOnInstantiate"` (bool, 기본 true) : 대상 Canvas 자식 모두 비움 (한 화면만 보이게)
- `"syncGameViewAspect"` (bool, 기본 true) : GameView 종횡비를 prefab 사이즈에 맞춤
- `"selectedPages"` (string[]) : 특정 페이지만 임포트할 때 사용. `UGUI_FIGMA_SELECTED_PAGES` 와 동등.
- `"koreanFontPath"` (string) : `Setup TMP Korean Fallback` 메뉴가 사용할 폰트 경로.
- `"koreanFontSdfOutputPath"` (string) : 자동 생성된 SDF asset 저장 경로 (기본: 폰트 파일과 같은 폴더 + `_SDF.asset`).

**PAT 재사용 (검증됨, 2026-04-21)**: 첫 실행 시 부트스트랩이 PAT 을 PlayerPrefs(`FIGMA_PERSONAL_ACCESS_TOKEN`) 에 저장한다. 두 번째 실행부터는 `FIGMA_PAT` 환경변수를 **비워두거나 아예 안 줘도** `run_unity_to_figma_sync.sh` 가 폴백을 안내만 하고 진행한다 (부트스트랩이 ContextFile → Env → EditorPrefs → PlayerPrefs → 기존 settings 순으로 자동 채움). 다른 디자인 파일을 임포트할 때는 `FIGMA_DOCUMENT_URL` 만 바꿔서 재실행하면 된다.

#### Step 1A.4: 임포트 결과 검증

Step 1A.3 의 스니펫이 Sync 메뉴 실행까지 완료하면, `Unity_GetConsoleLogs` 로 결과 로그를 회수해 판정한다. 자동 폴링은 다음 패턴:

1. `Unity_GetConsoleLogs(logTypes="Log,Warning,Error", maxEntries=500, includeStackTrace=false)` 호출
2. `logs[].message` 에서 `UnityToFigma import:` 로 시작하는 줄 추출
3. 정규식 `created=(\d+), updated=(\d+), skipped=(\d+), failed=(\d+), orphaned=(\d+)` 으로 수치 파싱
4. 시한 (예: 60초) 이내에 못 받으면 다시 호출

기대 결과: `UnityToFigma import: created=N, updated=M, skipped=K, failed=0, orphaned=0, manifestRemoved=0`

판정 기준:


| 결과                       | 판정                                                                                |
| ------------------------ | --------------------------------------------------------------------------------- |
| `failed > 0`             | 임포트 부분 실패. `[UnityToFigma]` 메시지로 원인 파악 후 사용자에게 PAT/네트워크/문서 권한 확인 요청. 보정 단계 진입 금지. |
| `created + updated == 0` | 임포트 결과 없음. 페이지 선택 / URL 정확성 재확인.                                                  |
| 정상                       | 다음 단계 진행                                                                          |


리포트 dump 결과 (`Assets/_Temp/UnityToFigmaReport.json`) 에서 다음을 확인:

- `importRoot` : 실제 임포트 루트 (settings.ImportRoot 기준, 기본 `Assets/Figma`)
- `screens` : `{importRoot}/Screens/*.prefab` 목록
- `components` : `{importRoot}/Components/*.prefab`
- `textures`, `serverImages`, `fonts`
- `roundedHandled` : `FigmaImage.cornerRadius != 0` 인 정상 라운드 노드 (UnityToFigma SDF 처리, 추가 작업 불필요)
- `roundedExtreme` : `max(cornerRadius) >= 500` (pill / circle 후보, 시각 검토 권장)
- `roundedSkipped` : 검출 실패 (대개 0; FigmaImage 타입 미참조 등)

#### Step 1A.5: 기본 화면 인스턴스화 (Canvas referenceResolution 자동 설정)

`buildPrototypeFlow=false` 인 경우 씬에 자동 인스턴스화되지 않는다. 본 스킬은 다음 메뉴로 첫 Screen 을 현재 씬에 띄운다 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UnityToFigma Bootstrap/Instantiate Default Screen");
        result.Log("Instantiate Default Screen: {0}", ok);
    }
}
```

이 메뉴는 다음을 한 번에 수행한다 (검증됨, 2026-04-21):

1. `defaultScreenName` 매칭 (ContextFile/EditorPrefs/환경변수 → 폴백: 알파벳 첫 prefab) 으로 대상 Screen prefab 선택
  - **Suffix 자동 폴백**: 다른 페이지에 동일 Frame 이름이 선점돼 있어 UnityToFigma 가 `MainScreen_1` 처럼 저장한 경우, `defaultScreenName=MainScreen` 만 줘도 가장 작은 인덱스의 `MainScreen_N` 을 자동 매칭한다 (검증됨, 2026-04-21).
2. 같은 이름의 기존 인스턴스 제거 → 반복 호출 안전
3. **다른 Screen prefab 인스턴스 정리** (`cleanOtherScreens=true` 기본): 씬 루트 + 모든 Canvas 자식에서 동일 폴더 내 다른 Screen prefab 이름과 매칭되는 GameObject 제거
4. **Canvas 청소** (`clearCanvasOnInstantiate=true` 기본): 대상 Canvas 의 모든 자식을 비움. unpack 된 prefab 잔재처럼 이름으로 추적 불가능한 객체까지 깨끗이 정리하여 한 화면만 보이게 한다
5. 없으면 `UICanvas` 새로 생성 (ScreenSpaceOverlay)
6. **CanvasScaler 화면 대응 정책 (검증됨, 2026-04-21)**: `ScaleWithScreenSize`, `referenceResolution = prefab.RectTransform.sizeDelta` (예: 1080x1920), `screenMatchMode` = `canvasMatchMode` 옵션 (**기본 expand** — 사용자 워크플로우에서 가장 일반적). `"auto"` 로 변경 시 portrait→Width / landscape→Height 자동 매칭.
7. `PrefabUtility.InstantiatePrefab(prefab, canvasTransform)` 로 인스턴스화 + **Screen root 의 RectTransform 을 center-anchor + sizeDelta=디자인 사이즈 로 고정** (검증됨, 2026-04-21).
  - **구 정책 (stretch + sizeDelta=0) 의 문제**: UnityToFigma 자식들이 픽셀 단위 절대 좌표라 root 가 Canvas 폭으로 stretch 되면 자식들이 좌상단에 몰린다 (사용자 보고: "한쪽에 붙어버림"). 이번 변경으로 해소.
  - **새 정책**: root 가 항상 디자인 사이즈를 유지하고 화면 가운데에 정렬됨. CanvasScaler 가 화면 비율 차이를 흡수.
8. **자식 RT anchor 자동 보정 (`autoAnchor=true` 기본, 검증됨 2026-04-21)**: UnityToFigma 가 만든 자식 RT 들의 anchor=(0,1) TopLeft 고정을 디자인 의도(좌/우/가운데/stretch · 상/하/가운데/stretch)대로 재설정. SafeArea 활용 + 표준 UGUI 베스트 프랙티스 충족. 자세한 알고리즘은 Step 1A.5a-2 참조.
9. (best-effort) GameView 종횡비를 디자인 사이즈에 맞춰 자동 변경 (Unity 6.x 일부 버전에서 reflection 실패 가능 — 실패해도 다른 동작에 영향 없음)

**옵션 (ContextFile 또는 EditorPrefs 로 제어)**:


| 키                           | 기본값        | 효과                                                                                                                                                                       |
| --------------------------- | ---------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `defaultScreenName`         | (없음)       | 인스턴스화할 prefab 이름. 미지정 시 알파벳 첫 prefab                                                                                                                                     |
| `cleanOtherScreens`         | `true`     | 씬에서 다른 Screen prefab 인스턴스 자동 제거                                                                                                                                          |
| `clearCanvasOnInstantiate`  | `true`     | 대상 Canvas 의 모든 자식 비움 (가장 강력한 청소)                                                                                                                                         |
| `syncGameViewAspect`        | `true`     | GameView 종횡비를 prefab 사이즈에 맞춰 자동 변경 (실패 시 경고만)                                                                                                                            |
| `canvasMatchMode`           | `"expand"` | CanvasScaler.ScreenMatchMode 강제 (실제 사용자 워크플로우에서 가장 빈번). `"expand"` (디자인 박스를 화면 안에 fit + 비율 유지), `"auto"` (포트레이트=Width, 랜드스케이프=Height), `"width"`, `"height"`, `"shrink"` |
| `screenRootStretch`         | `false`    | `true` 면 root 를 풀스트레치(0,0~1,1). expand 모드에선 비권장 (스케일이 디자인 박스 기준이라 좌측 쏠림 재현). auto/width/height 모드에서 화면 가득 활용 원할 때만 true                                                  |
| `autoAnchor`                | `true`     | 자식 RectTransform 의 anchor 를 디자인 의도(좌/우/가운데/stretch · 상/하/가운데/stretch)에 맞춰 자동 보정. UnityToFigma 가 모든 자식을 (0,1) TopLeft 로 만드는 한계를 해결                                        |
| `autoAnchorEdgeRatio`       | `0.10`     | 부모 폭/높이 대비 가장자리 판정 비율. 자식 여백이 이 값 이내면 해당 가장자리에 정렬된 것으로 간주                                                                                                                |
| `autoAnchorCenterTolerance` | `0.30`     | 가운데 정렬 판정 허용 오차. |좌-우 여백| <= min(좌,우) × 이 값 이면 center 로 분류                                                                                                               |
| `autoAnchorStretchCoverage` | `0.85`     | stretch 분류 임계. 자식 사이즈가 부모의 이 비율 이상 + 양 가장자리 여백이 모두 edge 이내면 stretch                                                                                                      |
| `autoAnchorEnableStretch`   | `true`     | false 면 stretch 분류를 끄고 좌/우/가운데 정렬만 사용                                                                                                                                    |
| `autoAnchorDryRun`          | `false`    | true 면 anchor 변경은 하지 않고 분류 통계만 콘솔에 출력 (사전 시뮬레이션)                                                                                                                         |


여러 화면을 띄워야 하는 시나리오:

- **권장**: 화면별로 별도 Canvas 사용 (Unity 의 ScreenSpaceOverlay Canvas 는 여러 개 동시 사용 가능, sortingOrder 로 정렬). 각 Canvas 의 referenceResolution 을 해당 화면 prefab 사이즈에 맞춤.
- **비권장**: 한 Canvas 에 여러 Screen 동시 배치 (referenceResolution 이 한 화면 기준이라 다른 화면이 깨짐).

**asset add-to-scene 으로 직접 인스턴스화하지 말 것**:

- 인자 이름은 `assetPath=` 만 받고 `parent=`/`parentName=` 은 **무시되어 항상 씬 루트로 들어간다**.
- `gameobject reparent` 는 `success=true` 응답이 와도 **실제로 부모가 안 잡히는 경우가 많다** (`parentId=0` 그대로).
- → 반드시 부트스트랩 메뉴(`Tools/UnityToFigma Bootstrap/Instantiate Default Screen`) 를 사용한다. C# 측에서 `PrefabUtility.InstantiatePrefab(prefab, canvasTransform)` 으로 안전하게 처리한다.

> **참고**: 기본 Canvas 이름은 `Canvas` 가 아니라 `UICanvas` 가 자동 생성된다 (UnityToFigma + 부트스트랩 동작).

**무시해도 되는 콘솔 노이즈** (검증됨, 2026-04-21):

`Instantiate Default Screen` 직후 콘솔에 다음 류 메시지가 5~10건 섞여 나올 수 있다 — 모두 **정상이며 임포트/렌더링에는 영향 없음**:


| 메시지 패턴                                                                                                                                                     | 원인                                                                                                 | 후속 영향                                        |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- | -------------------------------------------- |
| `MissingReferenceException: The object of type 'Material' has been destroyed but you are still trying to access it.` (스택에 `FigmaImage`, `MaskableGraphic`) | `clearCanvasOnInstantiate=true` 가 기존 Canvas 자식을 destroy 하면서 그 프레임의 Material 참조가 cleanup 도중 한 번 호출됨 | 인스턴스화 끝나면 새 prefab 의 Material 이 정상 바인딩되어 사라짐 |
| `Some objects were not cleaned up...`                                                                                                                      | 위와 동일 cleanup 타이밍                                                                                  | 위와 동일                                        |


판정: 인스턴스화 직후 `Diagnose Screen Layout` 메뉴가 `→ CENTER ✓` 를 출력하고 캡처에서 디자인이 정상 렌더링되면 무시 가능. 만약 화면이 비어 있거나 텍스트/이미지가 깨지면 그때 임포트 실패를 의심한다.

#### Step 1A.5a: 다해상도 검증 + 사후 보정 (Apply Responsive Layout)

**중요한 검증 절차** (검증됨, 2026-04-21): 인스턴스화 직후 부트스트랩의 `Capture Default Screen` 메뉴(Step 1A.5b 의 `Unity_RunCommand` 호출 패턴) 또는 unity-mcp `Unity_RunCommand` 안에서 임시 카메라+RT 로 강제 W×H 캡처를 수행해, 디자인과 다른 비율의 GameView 시뮬레이션을 통해 한쪽 쏠림 / 잘림 / 빈 영역이 없는지 확인한다. 흔한 문제 패턴:


| 증상                                                         | 원인                                                                                                                        | 처리                                                                                                                                                                                                                                                                                                        |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **자식 RT 의 anchor 가 모두 (0,1) TopLeft 고정** (Inspector 시각 확인) | FigmaToUnity 의 출력 그대로. 화면 비율 변하면 우측/하단 정렬되어야 할 요소가 어긋남 + SafeArea 활용 불가                                                   | `**autoAnchor=true` (기본)** 가 인스턴스화 직후 자동 보정. 사후 단독 호출은 `Tools/UnityToFigma Bootstrap/Auto Anchor` 메뉴 (Step 1A.5a-2 의 `Unity_RunCommand` 패턴)                                                                                                                                                                  |
| 디자인이 좌측에 1080 폭으로 붙고 우측 840 영역이 비어 있음 (가로 GameView)        | 구 정책 (root stretch + sizeDelta=0). 자식들이 절대 좌표라 좌상단 쏠림                                                                     | 새 정책 (root center-anchor) 으로 자동 해소. 이미 인스턴스화된 화면은 `Apply Responsive Layout` 메뉴 호출 (Step 1A.5a 의 `Unity_RunCommand`)                                                                                                                                                                                       |
| **expand 모드에서 디자인 박스 외부 좌우/상하에 Skybox 빈 영역**               | expand 의 본질적 동작 (디자인 비율 유지). 자식이 root 디자인 박스 밖으로 못 나감                                                                     | 의도된 동작. 빈 영역에 다른 UI 가 필요하면 별도 SafeAreaBackground 컨테이너 추가 또는 `canvasMatchMode="auto"` 로 변경                                                                                                                                                                                                                 |
| 위/아래 일부 잘림 (portrait 디자인 + landscape GameView, auto 매칭)    | match=Width 자동 선택 → 가로폭은 맞지만 디자인 세로가 화면 밖으로 늘어남                                                                           | 정책상 정상 trade-off. `canvasMatchMode="height"` 로 override 시 좌우 빈 영역으로 바뀜 (선호에 따라 선택)                                                                                                                                                                                                                        |
| 좌측 쏠림 + 우측 Skybox (expand 모드 + screenRootStretch=true)     | expand 의 스케일 기준이 디자인 박스라 root stretch 와 상충 → root rect 가 (anchor 0~1) + sizeDelta=0 형태가 되어 디자인이 실제로는 stretch 박스 좌상단에만 그려짐 | `**Tools/UnityToFigma Bootstrap/Diagnose Screen Layout`** 으로 root anchor 상태 확인 → STRETCH ⚠ 진단 시 ContextFile 의 `screenRootStretch` 키 제거(또는 false) + `Instantiate Default Screen` 또는 `Apply Responsive Layout` 재호출. SafeArea 활용은 `canvasMatchMode="auto"` + `screenRootStretch=true` + `autoAnchor=true` 조합 |


이미 인스턴스화된 화면을 다시 만들지 않고 부분 보정만 하고 싶을 때 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UnityToFigma Bootstrap/Apply Responsive Layout");
        result.Log("Apply Responsive Layout: {0}", ok);
    }
}
```

이 메뉴는 다음을 수행한다 (검증됨, 2026-04-21):

1. `defaultScreenName` (suffix 폴백 포함) 또는 씬에서 `GameObject.Find` 로 대상 Screen 결정
2. **Screen root 의 RectTransform 정상화**: 기본은 center-anchor (0.5, 0.5) + sizeDelta = 디자인 사이즈. `screenRootStretch=true` 옵션 시 풀스트레치
3. **CanvasScaler 정상화**: `referenceResolution = 디자인 W x H`, `screenMatchMode` = `canvasMatchMode` 옵션 (기본 expand)
4. **LayoutElement 검수 리포트** (수정 안 함): `LayoutElement` 가 있는데 부모에 `LayoutGroup` 이 없는 자식 개수를 콘솔에 출력. UnityToFigma 가 모든 노드에 `LayoutElement` 를 붙이지만 실제 레이아웃은 절대 좌표 RectTransform 으로 결정되므로 이 컴포넌트는 사실상 무용지물. 디스크 / inspector 노이즈가 신경 쓰이면 사용자 또는 에이전트가 수동 정리.
5. **자식 anchor 자동 보정** (`autoAnchor=true` 기본): 아래 `Auto Anchor` 메뉴와 동일한 로직으로 자식 RT 의 anchor 를 디자인 의도대로 재설정
6. (옵션) GameView 종횡비도 디자인 사이즈로 재동기화 (`syncGameViewAspect=true` 기본)

**LayoutElement-only 정리 정책 (사용자 위임)**: 본 스킬은 LayoutElement 를 자동 제거하지 않는다 — UnityToFigma 가 향후 LayoutGroup 으로 묶을 가능성을 남겨두기 위해서. 사용자가 명시적으로 정리 원하면 검수 리포트 ("LayoutElement 가 있지만 부모에 LayoutGroup 이 없는 자식 N/M 개") 를 보고 수동 결정.

#### Step 1A.5a-1: Screen Layout 진단 (Diagnose)

**증상이 보일 때 가장 먼저 호출**: 디자인이 화면 한쪽에 붙거나, 자식 anchor 가 모두 TopLeft 같이 보일 때 root RT/Canvas/CanvasScaler 의 실제 값을 콘솔에 출력해 원인을 식별한다 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UnityToFigma Bootstrap/Diagnose Screen Layout");
        result.Log("Diagnose Screen Layout: {0}", ok);
    }
}
```

> 출력은 `Unity_GetConsoleLogs(logTypes="Log,Warning")` 로 회수. `[Diagnose]` 로 시작하는 라인만 보면 된다.

출력 예시 (정상):

```
[Diagnose] Screen root: parent=UICanvas anchorMin=(0.50, 0.50) anchorMax=(0.50, 0.50) pivot=(0.50, 0.50) sizeDelta=(1080.00, 1920.00) → CENTER ✓
[Diagnose] Canvas=UICanvas renderMode=ScreenSpaceOverlay | scaler.mode=ScaleWithScreenSize ref=(1080.00, 1920.00) match=Expand matchVal=0.00
[Diagnose] ContextFile options: screenRootStretch=(미설정 → false) canvasMatchMode=(미설정 → expand) autoAnchor=(미설정 → true)
```

출력 예시 (사용자 보고 시나리오 = 좌측 쏠림):

```
[Diagnose] Screen root: ... anchorMin=(0.00, 0.00) anchorMax=(1.00, 1.00) sizeDelta=(0.00, 0.00) → STRETCH ⚠ (좌측 쏠림 원인 가능)
[Diagnose] ContextFile options: screenRootStretch=True ...
[Diagnose] root 가 STRETCH 입니다. ContextFile 에서 'screenRootStretch' 키를 제거(또는 false 로 변경)하고 'Instantiate Default Screen' 또는 'Apply Responsive Layout' 을 다시 호출하세요.
```

원인 카탈로그:


| 진단 출력                                             | 원인                                                             | 처리                                                         |
| ------------------------------------------------- | -------------------------------------------------------------- | ---------------------------------------------------------- |
| `→ STRETCH ⚠` + `screenRootStretch=True`          | 이전 검증/실수로 ContextFile 에 옵션 잔존                                  | ContextFile 에서 키 제거 + 재인스턴스화                               |
| `→ STRETCH ⚠` + `screenRootStretch=(미설정 → false)` | 부트스트랩이 아닌 외부 코드/수동 조작이 root 를 stretch 로 변경                     | `Apply Responsive Layout` 호출 (root 정상화)                    |
| `→ CENTER ✓` 인데 여전히 좌측 쏠림                         | Canvas 가 ScreenSpaceOverlay 가 아닌 경우, 또는 다른 Canvas 인스턴스가 우선 렌더링 | `parent=...` 출력 확인. 의도한 UICanvas 가 아닌 경우 잘못된 부모 → 인스턴스 재생성 |
| `Canvas 부모가 없음` 경고                                | ScreenSpace 렌더링 안 됨 → 화면에 안 보이거나 World Space 로 그려짐             | UICanvas 부모로 이동 또는 `Instantiate Default Screen` 재호출        |


#### Step 1A.5a-2: 자식 RT anchor 자동 보정 (Auto Anchor)

**필수 검증** (검증됨, 2026-04-21): UnityToFigma 는 모든 자식 RectTransform 을 `anchor=(0,1) TopLeft` + 픽셀 절대 좌표로 만든다. 사용자가 Inspector 에서 자식 RT 들을 보면 **앵커가 모두 좌상단에 박혀있는 비표준 모양** 이다. 이 상태로는:

- 우측 정렬되어야 할 X 버튼 / 토글 핸들이 화면 비율 변하면 어긋남
- 하단 고정되어야 할 저장 버튼이 화면 길이에 따라 어중간한 위치
- SafeArea / Notch 영역 활용 불가
- 일반 UGUI 워크플로우 (anchor 기반 반응형) 와 단절

이를 해결하기 위해 인스턴스화 + Apply Responsive Layout 시 `**autoAnchor=true` (기본)** 가 자동으로 자식 RT 의 anchor 를 디자인 의도대로 재설정한다. 단독 호출 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UnityToFigma Bootstrap/Auto Anchor");
        result.Log("Auto Anchor: {0}", ok);
    }
}
```

**추론 규칙** (모든 임계값은 ContextFile 옵션으로 override):


| 결과 anchor                                     | 조건                                                                                                                     |
| --------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| **가로 stretch** (anchorMin.x=0, anchorMax.x=1) | 자식 width ≥ parent.width × `autoAnchorStretchCoverage`(0.85) AND 좌/우 여백 모두 ≤ parent.width × `autoAnchorEdgeRatio`(0.10) |
| **left** (0, 0)                               | 좌측 여백 ≤ edge AND 우측 여백 > edge × 2                                                                                      |
| **right** (1, 1)                              | 우측 여백 ≤ edge AND 좌측 여백 > edge × 2                                                                                      |
| **center-X** (0.5, 0.5)                       | |좌-우 여백| ≤ min(좌,우) × `autoAnchorCenterTolerance`(0.30)                                                                |
| 그 외                                           | 가까운 가장자리로 폴백                                                                                                           |


세로(top/bottom/center-Y/stretch-Y) 도 동일 로직.

**위치 보존 변환**: anchor 변경 시 Unity Inspector 의 'Toggle Anchors' 와 동일한 worldRect 보존 변환을 적용한다 (parent 좌표계 corners → 새 anchor 박스 기준 offsetMin/Max 재계산). 즉 anchor 만 표준 UGUI 로 바뀌고 시각적 위치/사이즈는 동일.

**Idempotent**: 두 번째 호출 시 `changed=0/N` (안전하게 반복 호출 가능).

**검증 결과 (Settings 화면 예시)**:

- changed=14/27 (변경된 자식 수 / 전체 자식 수)
- H: left=6, right=8 (토글 ON/OFF + 우측 텍스트), center=0, stretch=13 (카드 BG, 저장 버튼 BG 등)
- V: top=17 (헤더, 카드 본문), bottom=2 (저장 버튼 라벨/BG), stretch=8

#### Step 1A.5b: 디자인 사이즈 그대로 캡처 (검증용)

> **권장 경로**: 다해상도 검증/캡처는 **부트스트랩의 `Capture Default Screen` 메뉴를 unity-mcp 로 호출**해 prefab 의 sizeDelta 그대로 PNG 를 받는 방식을 표준으로 한다. unity-cli 의 `ui screenshot.capture width=W height=H` 같은 강제 W×H 캡처는 unity-mcp 환경에서는 부트스트랩 메뉴 또는 임시 카메라/RT 스니펫으로 대체한다.

부트스트랩 메뉴 호출 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UnityToFigma Bootstrap/Capture Default Screen");
        result.Log("Capture Default Screen: {0}", ok);
    }
}
```

이 메뉴는 부트스트랩이 다음을 수행한다 (검증됨, 2026-04-21):

1. `defaultScreenName` (suffix 폴백 포함) 으로 대상 prefab 결정
2. prefab 의 `RectTransform.sizeDelta` 를 그대로 W x H 로 사용 (예: 1080x1920, 1440x3040)
3. 씬의 모든 Canvas 를 일시적으로 `ScreenSpaceCamera + ConstantPixelSize(scale=1)` 로 전환
4. 임시 카메라 + W x H RenderTexture 로 정확히 픽셀 단위 렌더링
5. `Assets/_Temp/<ScreenName>_Capture_<W>x<H>.png` 로 저장 (`captureOutputPath` / `UGUI_FIGMA_CAPTURE_PATH` 로 변경 가능)
6. Canvas/CanvasScaler 상태 원복 + 임시 카메라 destroy

GameView 종횡비 동기화가 잘 동작하는 환경에서는 굳이 안 써도 되지만, 멀티 해상도 검증용 PNG 를 받고 싶을 때 유용하다.

#### Step 1A.5c: 한글 폰트 fallback 자동 등록 (선택, 한글 텍스트가 있는 디자인 전용)

UnityToFigma 가 다운로드하는 Inter / Roboto 등의 SDF 에는 한글 글리프가 없어서 한국어 텍스트가 `□□□` (tofu) 로 표시된다. **자동 다운로드는 하지 않지만(라이선스)**, 사용자가 폰트 파일만 프로젝트에 두면 부트스트랩이 SDF 생성 + TMP fallback 등록까지 한 번에 처리한다 (검증됨, 2026-04-21).

준비:

1. 한글 폰트 (.ttf 또는 .otf) 를 프로젝트에 배치. 라이선스가 자유로운 옵션:
  - **NotoSansKR** (Google Fonts, SIL OFL 1.1): `https://github.com/google/fonts/raw/main/ofl/notosanskr/NotoSansKR%5Bwght%5D.ttf`
  - **Pretendard** (SIL OFL 1.1): `https://github.com/orioncactus/pretendard`
2. 권장 경로: `Assets/Fonts/NotoSansKR-Regular.ttf` (자동 탐색 패턴에 일치)
3. Unity 임포트 완료 대기

실행 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UnityToFigma Bootstrap/Setup TMP Korean Fallback");
        result.Log("Setup TMP Korean Fallback: {0}", ok);
    }
}
```

이 메뉴는 다음을 멱등하게 처리한다:

1. `koreanFontPath` (ContextFile/UGUI_FIGMA_KOREAN_FONT_PATH) 또는 자동 탐색 (`Assets/Fonts/Noto*KR*.ttf`, `Pretendard*.ttf`, `*Korean*.ttf`, `*KR*.ttf`, `Nanum*.ttf` 순) 으로 폰트 결정
2. 같은 폴더에 `<FontName>_SDF.asset` 가 있으면 재사용, 없으면 `TMP_FontAsset.CreateFontAsset(font, 90, 9, SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true)` 로 **Dynamic SDF** 생성. Dynamic 이라 한글 글리프가 런타임에 자동 생성된다 (사전 글리프 등록 불필요).
3. `TMP_Settings.fallbackFontAssets` (글로벌 fallback) 에 SDF 추가
4. `Assets/Figma/Fonts/*.asset` (UnityToFigma 가 만든 Inter SDF 등) 의 `m_FallbackFontAssetTable` 에도 SDF 추가 (per-asset fallback)

기대 로그:

```
[UnityToFigmaBootstrap] Korean SDF 생성: Assets/Fonts/NotoSansKR-Regular_SDF.asset (Dynamic, source=NotoSansKR-Regular.ttf)
[UnityToFigmaBootstrap] TMP Korean Fallback 적용 완료. globalFallbackAdded=1, perAssetFallbackAdded=3
```

**멱등 판정 기준** (검증됨, 2026-04-21):


| 출력                                                 | 의미                                                                                           | 추가 작업                |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------- | -------------------- |
| `globalFallbackAdded=0, perAssetFallbackAdded=0`   | 모든 fallback 이 이미 등록되어 있음 → 완료                                                                | 없음                   |
| `globalFallbackAdded=0, perAssetFallbackAdded=N>0` | TMP_Settings 글로벌은 이미 됐지만 일부 per-asset(예: `Inter_SDF.asset`) 에 추가 등록됨 → **정상** (재실행 후엔 0/0 됨) | 없음                   |
| `globalFallbackAdded=1, perAssetFallbackAdded=N`   | 처음 등록되었음                                                                                     | 한 번 더 호출해서 0/0 확인 권장 |


per-asset 만 변경되는 케이스가 가장 흔하다 — UnityToFigma 가 새 텍스트 추가 시 SDF를 재생성하면 fallback table 이 비워진 채로 돌아오기 때문. 호출 후 항상 한글 텍스트가 보이면 OK.

이후 `Capture Default Screen` 으로 캡처하면 한글이 정상 렌더링된다 (검증: "환경 설정", "사용자닉네임", "레벨 99 · 마법사", "소리/진동/알림", "저장하기" 모두 정상).

폰트가 없으면 Debug.LogWarning 으로 안내만 하고 종료 (다이얼로그 없음 → 자동화 안전).

#### Step 1A.6: 레퍼런스 스크린샷 (선택, 검증용)

Figma MCP 가 연결돼 있으면 보정/검증용 스크린샷만 별도로 가져온다 (디자인 컨텍스트는 가져오지 않음 — 이 경로에선 불필요):

```
get_screenshot(fileKey=":fileKey", nodeId=":nodeId")
```

여러 화면이라면 Screen 프리팹 이름과 Figma Frame 이름을 매칭해서 1:1 비교 가능하도록 정리한다.

> 이 경로에선 `get_design_context` / `get_metadata` / 에셋 다운로드를 **수행하지 않는다**. UnityToFigma 가 모두 처리했다.

### Path B: 스크린샷 파일 입력 (비권장 폴백)

> **주의**: 본 스킬의 주 시나리오는 Path A (FigmaToUnity 임포트 후 unity-mcp 보정) 이다. Path B 는 FigmaToUnity 임포트가 불가능한 환경에서만 사용한다. 진입 전 사용자에게 "Path A 사용 가능 여부"를 한 번 확인할 것.

#### Step 1B.1: 스크린샷 수신

사용자로부터 레퍼런스 스크린샷 파일 경로를 받는다.
Read 도구로 이미지를 로드하여 비전 분석을 수행한다.

#### Step 1B.2: 정보 수집

다음 정보를 확인한다. 사용자가 제공하지 않은 항목은 질문한다:


| 항목                 | 필수  | 기본값    | 예시                                 |
| ------------------ | --- | ------ | ---------------------------------- |
| 레퍼런스 스크린샷          | O   | -      | /path/to/screenshot.png            |
| 기준 해상도             | O   | -      | 1440x3040                          |
| ScreenMatchMode    | X   | Expand | Expand, Shrink, MatchWidthOrHeight |
| matchWidthOrHeight | X   | 0.5    | 0~1 사이 값                           |


#### Step 1B.3: Canvas 생성 (unity-mcp `Unity_RunCommand`)

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 입력값 (호출 시 치환)
        int refW = 1440, refH = 3040;
        var matchMode = CanvasScaler.ScreenMatchMode.Expand;
        float matchVal = 0.5f;

        var go = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(refW, refH);
        scaler.screenMatchMode = matchMode;
        scaler.matchWidthOrHeight = matchVal;

        result.RegisterObjectCreation(go);
        result.Log("Canvas created: {0} ref=({1},{2}) match={3}", go.name, refW, refH, matchMode);
    }
}
```

---

## Phase 2A: Patch (Path A 전용 — FigmaToUnity 결과 보정)

이 단계는 **FigmaToUnity 가 만들어 둔 결과 위에 차이만 덧칠하는 단계**다. 전체 재구성 금지.

### Phase 2A.0: AI 컨벤션 후처리 (Patch 진입 전 1회)

FigmaToUnity 패키지의 `UnityToFigma/Postprocess/*` 메뉴가 Sync 직후 자동으로 띄우는 Sync Options 창의 "AI 컨벤션 후처리 컨텍스트 생성 + 클립보드 복사" 를 Apply 하면 `{ImportRoot}/Debug/PostprocessContext.md` 가 만들어진다. 이 컨텍스트를 받아 본 스킬이 다음을 수행:

- Screen 프리팹 명명 (프로젝트 컨벤션 — 해당 프로젝트의 CLAUDE.md/agent_docs 우선)
- 깊이 2 자식 노드명 접두 정리 (`Btn_/Txt_/Img_/Scroll_` 등)
- Figma `reactions`/구조 분석 기반 Button 컴포넌트 부착
- 9-slice 후보 스프라이트의 `spriteBorder` 갱신
- 동명 MonoBehaviour 부착 + `[SerializeField]` 자동 매핑 보완
- LayoutGroup 케이스별 부착

자세한 휴리스틱·체크리스트: `references/figma-to-unity-convention.md`.

이후 본 Phase 2A.1~2A.4 의 unity-mcp 보정 (color/font/RT/SafeArea 등) 으로 이어진다.


### Step 2A.1: 비교 캡처

레퍼런스(Figma 스크린샷)와 Game View 캡처를 동시에 로드해 비교한다. 캡처 도구가 없으면 `assets/GameViewCapture.cs` 사용 (Phase 3 와 동일 절차).

### Step 2A.2: 차이 분류

발견된 차이를 다음 카테고리로 분류한다:


| 카테고리                      | 보정 행동 (모두 unity-mcp `Unity_RunCommand` 내부에서 처리)                                                                                                       |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 폰트가 디자인과 다르게 렌더링                | **이 스킬은 직접 교체하지 않는다.** 패키지의 `FigmaMissingFontWindow` 가 다운로드 실패한 family 를 모아 보여주고, 사용자가 ObjectField 로 TMP_FontAsset 을 직접 선택해 일괄 교체한다. 한글이 `□` 로 나오는 경우에만 `Setup TMP Korean Fallback` 메뉴로 Dynamic SDF fallback 등록 (Step 1A.5c). |
| 색상 mismatch               | `image.color = ColorUtility.TryParseHtmlString("#..", out var c) ? c : image.color` + `RegisterObjectModification`                                  |
| 텍스트 누락/오타                 | `tmp.text = "..."` 직접 대입 (쉘 이스케이프 문제 자체가 없음 → 구버전의 `batch_set_texts.py` 불필요)                                                                          |
| Image Sprite 누락 (다운로드 실패) | 사용자에게 보고. 임의 placeholder 금지.                                                                                                                          |
| SafeArea 누락               | 화면 루트에 SafeArea 컴포넌트 수동 추가 (`AddComponent<SafeAreaFitter>()` 등 프로젝트 구현체에 맞춰서)                                                                       |
| 단순 텍스트 정렬 차이              | TMP `alignment` 속성만 수정                                                                                                                                |
| **라운드 코너 차이**             | **수정하지 않음.** Path A 라면 FigmaToUnity SDF 가 정확하므로 캡처 차이는 노이즈일 가능성 높음. `roundedHandled`/`roundedExtreme` 으로 이미 분류됨 (`references/round-corner-policy.md`) |
| RectTransform 위치/크기 차이    | **건드리지 않음 (기본).** FigmaToUnity 가 constraints 로 잡은 값을 임의로 옮기면 다해상도에서 깨진다. 정말로 누락된 경우에만 보정.                                                            |


### Step 2A.3: 보정 명령 실행 (unity-mcp `Unity_RunCommand` 단위)

색상 한 줄 패치 예:

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var go = GameObject.Find("PrimaryButton");
        if (go == null) { result.LogError("PrimaryButton 없음"); return; }
        var image = go.GetComponent<Image>();
        if (image == null) { result.LogError("Image 컴포넌트 없음"); return; }
        result.RegisterObjectModification(image);
        if (ColorUtility.TryParseHtmlString("#1A1A2EFF", out var c)) image.color = c;
        result.Log("color={0}", image.color);
    }
}
```

폰트 일괄 교체 — 부트스트랩 또는 프로젝트 `Tools/Fix TMP Overflow Settings` 메뉴 호출:

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 프로젝트 Editor 폴더에 FixTMPSettings.cs 가 있다고 가정 (assets/FixTMPSettings.cs 참조).
        // 메뉴를 호출하기 전 컴파일이 완료된 상태여야 한다. 프리팹 편집 중이면 호출 금지.
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Fix TMP Overflow Settings");
        result.Log("Fix TMP Overflow Settings: {0}", ok);
    }
}
```

> **컴파일 트리거 주의**: 원래 워크플로우는 `bash + Assets/Refresh + sleep 5` 였지만, unity-mcp 환경에서는 Editor 가 이미 켜져 있어 스크립트 추가 시 자동 import 가 도는 경우가 많다. 강제 필요 시 별도 `Unity_RunCommand` 로 `AssetDatabase.Refresh()` 만 호출 (프리팹 편집 중이면 금지).

### Step 2A.4: 보정 종료 조건

- 분류 결과 모든 항목이 "수동 처리" / "보정 완료" 상태가 되면 Phase 3 (검증 루프) 진입
- 보정 시도 횟수 누적 5회 초과 시 강제 중단하고 사용자 보고. UnityToFigma 가 처리하지 못한 영역을 무리하게 시도하지 말 것.

### 금지 사항 (재확인)

- 라운드 코너를 sprite/mask 로 흉내내지 않는다. `references/round-corner-policy.md` 참조.
- UnityToFigma 가 잡은 RectTransform 의 anchor/offset 을 일괄 갈아엎지 않는다.
- 자동 임포트된 SDF 도형(Rectangle/Ellipse)의 형상을 unity-cli 로 강제 변경하지 않는다 (인스펙터에서 사용자가 직접 조정).

---

## Phase 2: Analysis & Build (Path B 전용)

### Step 2.1: UI 구조 분석

**Figma 경로**: 메타데이터의 노드 트리와 디자인 컨텍스트에서 정확한 값을 추출.
**스크린샷 경로**: Claude 비전으로 스크린샷을 상세 분석.

분석 항목:

1. **UI 요소 식별**: 각 요소의 타입, 위치, 크기, 색상, 텍스트
2. **계층 구조 파악**: 헤더/콘텐츠/하단바 등 영역 구분
3. **반응형 앵커 결정**: Step 2.2의 알고리즘으로 결정 (절대좌표 금지)
4. **레이아웃 패턴**: 반복 요소 → Layout Group, 고정 바 → 스트레치 앵커

### Step 2.2: 반응형 앵커 결정 알고리즘 (핵심)

**다해상도 대응의 핵심은 모든 요소에 올바른 앵커를 할당하는 것이다.**
모든 요소를 center anchor + 절대 좌표로 배치하면 기준 해상도에서만 동작하므로 반드시 아래 알고리즘을 따른다.

#### 1단계: 요소의 역할 판별


| 디자인 역할            | 앵커 프리셋                                    | 좌표 방식                                          |
| ----------------- | ----------------------------------------- | ---------------------------------------------- |
| 전체 배경/오버레이        | `anchorMin=0,0 anchorMax=1,1` (풀 스트레치)    | `offsetMin/offsetMax=0,0`                      |
| 상단 바/헤더           | `anchorMin=0,1 anchorMax=1,1 pivot=0.5,1` | `size=0,{height}` (가로 스트레치)                    |
| 하단 바/풋터           | `anchorMin=0,0 anchorMax=1,0 pivot=0.5,0` | `size=0,{height}` (가로 스트레치)                    |
| 콘텐츠 영역 (헤더/풋터 사이) | `anchorMin=0,0 anchorMax=1,1`             | `offsetMin=0,{footerH} offsetMax=0,-{headerH}` |
| 중앙 카드/다이얼로그       | `anchorMin=0.5,0.5 anchorMax=0.5,0.5`     | 고정 `size={w},{h}`                              |
| 좌측 고정 사이드바        | `anchorMin=0,0 anchorMax=0,1 pivot=0,0.5` | `size={w},0`                                   |
| 우측 고정 사이드바        | `anchorMin=1,0 anchorMax=1,1 pivot=1,0.5` | `size={w},0`                                   |


#### 2단계: Figma 레이아웃 속성에서 앵커 추론

`get_design_context` 반환값(Tailwind CSS 클래스)에서 레이아웃 의도를 분석:


| Tailwind/CSS 패턴                    | UGUI 앵커 결정                               |
| ---------------------------------- | ---------------------------------------- |
| `w-full` 또는 `justify-self-stretch` | 가로 스트레치 (`anchorMin.x=0, anchorMax.x=1`) |
| `h-full` 또는 `self-stretch`         | 세로 스트레치 (`anchorMin.y=0, anchorMax.y=1`) |
| `flex-[1_0_0]` (flex-grow)         | 부모 내 비율 분배 → Layout Group + 스트레치         |
| `absolute top-0 left-0`            | 부모 좌상단 앵커                                |
| `flex flex-col` 또는 `flex-col`      | Vertical Layout Group                    |
| `flex` (기본 수평)                     | Horizontal Layout Group                  |
| `grid grid-cols-[...]`             | 비율 기반 앵커 분할 또는 Grid Layout               |
| `size-full`                        | 풀 스트레치                                   |
| `shrink-0` + 고정 w/h                | 고정 크기, center 또는 가장자리 앵커                 |


#### 3단계: 부모-자식 관계에 따른 좌표 계산

앵커 타입별 좌표 변환 공식 (`references/figma-to-ugui-mapping.md` 상세 참조):

**스트레치 앵커 (가로)**: `anchorMin.x=0, anchorMax.x=1`

```
offsetMin.x = figma.x                        (왼쪽 여백)
offsetMax.x = -(parentW - figma.x - figma.w)  (오른쪽 여백, 음수)
sizeDelta.x = 0                               (앵커가 결정)
```

**스트레치 앵커 (세로)**: `anchorMin.y=0, anchorMax.y=1`

```
offsetMin.y = parentH - figma.y - figma.h     (하단 여백)
offsetMax.y = -figma.y                         (상단 여백, 음수)
sizeDelta.y = 0
```

**고정 앵커 (center)**: `anchorMin=0.5,0.5 anchorMax=0.5,0.5`

```
anchoredPosition.x = figma.x + figma.w/2 - parentW/2
anchoredPosition.y = -(figma.y + figma.h/2 - parentH/2)
sizeDelta = figma.w, figma.h
```

**상단 고정 (top stretch)**: `anchorMin=0,1 anchorMax=1,1 pivot=0.5,1`

```
anchoredPosition = 0, 0
sizeDelta = 0, figma.h      (가로는 스트레치, 세로는 고정)
offsetMin.x = figma.x       (왼쪽 마진이 있으면)
offsetMax.x = -(parentW - figma.x - figma.w) (오른쪽 마진이 있으면)
```

#### 4단계: 2-column 이상 레이아웃 처리

Figma에서 수평으로 나란한 프레임은 비율 앵커 또는 Layout Group으로 변환:

**방법 A: 비율 앵커 (반응형)**

```
// 2-column: 왼쪽 56.8%, 오른쪽 43.2% (예: 521.5/918 vs 372.5/918)
LeftCol:  anchorMin=0,0 anchorMax=0.568,1
RightCol: anchorMin=0.594,0 anchorMax=1,1   (gap 비율 반영)
```

**방법 B: Horizontal Layout Group (균등 또는 자동) — unity-mcp `Unity_RunCommand`**

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var go = GameObject.Find("ContentSection");
        if (go == null) { result.LogError("ContentSection 없음"); return; }
        var layout = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
        result.RegisterObjectModification(layout);
        layout.spacing = 24;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        result.Log("HorizontalLayoutGroup applied to {0}", go.name);
    }
}
```

### Step 2.3: Figma 값 → UGUI 매핑


| Figma 속성                   | UGUI 매핑                               |
| -------------------------- | ------------------------------------- |
| Frame position (x, y)      | 앵커 타입에 따라 다름 (Step 2.2 참조)            |
| Frame size (w, h)          | 스트레치면 `offsetMin/Max`, 고정이면 `size`    |
| Fill color                 | `color` (hex)                         |
| Text content               | `text`                                |
| Font size                  | `fontSize`                            |
| Font weight (Bold/Regular) | `fontStyle`                           |
| Text alignment             | `alignment`                           |
| Auto Layout (horizontal)   | `ui layout.add layoutType=Horizontal` |
| Auto Layout (vertical)     | `ui layout.add layoutType=Vertical`   |
| Auto Layout spacing        | `spacing`                             |
| Auto Layout padding        | `paddingLeft/Right/Top/Bottom`        |
| `w-full` / flex stretch    | 가로 스트레치 앵커                            |
| `h-full` / self-stretch    | 세로 스트레치 앵커                            |
| `absolute` + top/left      | 해당 가장자리 앵커                            |
| `grid grid-cols-[비율]`      | 비율 앵커 분할                              |


### Step 2.4: UI 빌드 (Path B 전용, unity-mcp `Unity_RunCommand`)

부모 → 자식 순서로 생성. **모든 요소에 적절한 앵커를 사용한다.** unity-mcp 채널에서는 unity-cli 의 `ui.panel.create` / `ui.image.create` / `gameobject reparent` / `component update` 단계가 모두 하나의 C# 스니펫 안에서 한 번에 처리되므로 구버전 브릿지 헬퍼(`ui_helper.sh`)나 명령 분리는 불필요하다.

다음 한 번의 `Unity_RunCommand` 가 Canvas + MainCard + Header 까지 만든다:

```csharp
using UnityEngine;
using UnityEngine.UI;

internal class CommandScript : IRunCommand
{
    static GameObject MakeImage(string name, RectTransform parent,
        Vector2 aMin, Vector2 aMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, string colorHex)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
        if (ColorUtility.TryParseHtmlString(colorHex, out var c))
            go.GetComponent<Image>().color = c;
        return go;
    }

    public void Execute(ExecutionResult result)
    {
        // Canvas
        var canvasGo = new GameObject("UICanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440, 3040);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        result.RegisterObjectCreation(canvasGo);

        var canvasRT = canvasGo.GetComponent<RectTransform>();

        // MainCard: 중앙 984x666
        var mainCard = MakeImage("MainCard", canvasRT,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 0), new Vector2(984, 666), "#FFFFFFEB");
        result.RegisterObjectCreation(mainCard);

        // Header: MainCard 좌상단 기준 (32, -32)
        var header = MakeImage("Header", mainCard.GetComponent<RectTransform>(),
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(32, -32), new Vector2(918, 77), "#00000000");
        result.RegisterObjectCreation(header);

        result.Log("Canvas/MainCard/Header 생성 완료");
    }
}
```

**반응형 앵커 공식**은 Step 2.2에 정리되어 있으나, 실전 경험상 **카드형 고정 UI**는 단순 매핑이 훨씬 빠르다:

- 자식 요소: `anchor=(0,1)`, `pivot=(0,1)` (부모 좌상단)
- `anchoredPosition = (figma.x, -figma.y)` (Y 부호 반전)
- `sizeDelta = (figma.w, figma.h)`

반응형이 진짜 필요할 때만 Step 2.2/2.3의 스트레치 앵커 전략을 개별 적용.

> unity-cli 시대의 부모 지정 3단계 (`ui.*.create` → `gameobject reparent` → `component update`) 와 헬퍼 스크립트 `scripts/ui_helper.sh` 는 unity-mcp 환경에서는 **사용하지 않는다**. C# 스니펫이 부모 transform 을 직접 받아 처리하므로 reparent 호환성 이슈 자체가 발생하지 않는다.

#### 2.4.c: 텍스트 내용 설정

unity-cli 시절의 쉘 이스케이프 우회용 `scripts/batch_set_texts.py` 는 **불필요**하다. unity-mcp 채널에서는 C# 안에서 문자열을 직접 대입하면 끝이다:

```csharp
GameObject.Find("HeaderTitle").GetComponent<TMPro.TextMeshProUGUI>().text = "MCP Import Smoke";
```

여러 텍스트를 한 번에 갱신하려면 한 스니펫 안에서 `Dictionary<string,string>` 으로 묶어 루프 처리.

#### 2.4.d: TMP 오버플로우 처리

TMP 기본 `overflowMode`는 `Ellipsis`(1)이라 긴 텍스트가 "..."로 잘린다. 반드시:

- `textWrappingMode = Normal` (1)
- `overflowMode = Overflow` (0)

Editor 메뉴 호출 (unity-mcp `Unity_RunCommand`):

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Fix TMP Overflow Settings");
        result.Log("Fix TMP Overflow Settings: {0}", ok);
    }
}
```

(템플릿: `assets/FixTMPSettings.cs` → 프로젝트 `Assets/_Project/Scripts/Editor/` 에 복사)

#### 2.4.e: 기본 UISprite를 평면 스프라이트로 교체

각진 사각형이 필요한데 sprite=null 이라 Unity 기본 둥근 UISprite 로 렌더될 때, 프로젝트의 1x1 화이트 스프라이트로 교체:

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Replace UI Images With Flat Sprite");
        result.Log("Replace UI Images With Flat Sprite: {0}", ok);
    }
}
```

(템플릿: `assets/ReplaceUISprites.cs` → `TargetSpritePath` 프로젝트에 맞게 수정 후 복사)

### Step 2.5: 리소스 매칭

**Figma 경로**: MCP가 반환한 에셋 URL에서 다운로드하여 spritePath로 할당.
**스크린샷 경로**: 프로젝트 Assets에서 유사 스프라이트 검색 (Glob 도구). 없으면 placeholder.

---

## Phase 3: Verification Loop

최대 5회 반복. 90%+ 일치 시 PASS.

### Step 3.1: 캡처

**Path A 진입 시 권장**: 부트스트랩의 `Tools/UnityToFigma Bootstrap/Capture Default Screen` 메뉴를 쓰면 현재 씬에 인스턴스화된 Screen prefab 의 사이즈 그대로 캡처된다 (Step 1A.5b 참고). 별도 스크립트 복사 불필요.

**보조 경로** (`Capture Default Screen` 이외에 명시적 W×H 캡처가 필요할 때): 스킬의 `assets/GameViewCapture.cs` 를 프로젝트 `Assets/Editor/` 에 복사한 뒤 unity-mcp `Unity_RunCommand` 로 메뉴 호출:

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 사전: Assets/Editor/GameViewCapture.cs 가 복사되어 컴파일 완료된 상태여야 함
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UguiFromScreenshot/Capture Game View 1080x1920");
        result.Log("Capture 1080x1920: {0}", ok);
        // → Assets/_Temp/GameCapture_1080x1920.png
    }
}
```

또는 `Capture Game View 1440x3040` 메뉴를 같은 패턴으로 호출.

> 임의 W x H 가 필요하면 `GameViewCapture.Capture(w, h, "Assets/_Temp/Custom.png")` 를 직접 호출하는 새 메뉴를 프로젝트별로 추가한다 (`namespace UguiFromScreenshot.Editor`). 또는 `Unity_RunCommand` 안에서 직접 임시 카메라+RT 를 만들어 `File.WriteAllBytes` 로 PNG 저장.

**캡처 주의**:

- Overlay Canvas는 카메라로 렌더 안 됨 → 스크립트 내부에서 ScreenSpaceCamera로 **임시 전환 후 원복**
- CanvasScaler가 ScaleWithScreenSize면 Screen.width/height와 RT 불일치로 축소됨 → 스크립트 내부에서 ConstantPixelSize(1.0)로 **임시 전환 후 원복**
- GameView RT 직접 캡처 시엔 **상하 반전** 발생 → 스크립트에서 플립 필요
- 캡처 실패 시 씬이 임시 상태로 저장되면 안 됨 → 스크립트에서 반드시 원복 보장

### Step 3.2: 비교

Read 도구로 레퍼런스(Figma 스크린샷 또는 원본 파일)와 캡처 스크린샷을 동시에 로드하여 비교.

평가 기준 (가중치):


| Category  | Weight | Pass Criteria      |
| --------- | ------ | ------------------ |
| STRUCTURE | 30%    | 모든 요소 존재 및 올바른 계층  |
| POSITION  | 25%    | 기준 해상도 대비 5% 이내 오차 |
| SIZE      | 20%    | 기준 크기 대비 10% 이내 오차 |
| COLOR     | 15%    | 색상 hex 값 근사 일치     |
| TEXT      | 10%    | 텍스트 내용 완전 일치       |


### Step 3.3: 수정

비교 결과를 기반으로 수정 명령 실행. **수정 행동은 입력 경로에 따라 다르다**:

**Path A (UnityToFigma) 의 경우**:

- Phase 2A 의 보정 행동 표를 따른다.
- RectTransform 일괄 변경 금지. 색상/텍스트/폰트 위주.
- 라운드 차이는 무시하고 보고 항목으로 누적.

**Path B (스크린샷) 의 경우**:

- 자유롭게 RectTransform/Layout 조정 가능.

Path B 예시 (Path A 에선 RectTransform 일괄 변경 자제) — unity-mcp `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var header = GameObject.Find("Header");
        if (header != null)
        {
            var rt = header.GetComponent<RectTransform>();
            result.RegisterObjectModification(rt);
            rt.anchoredPosition = new Vector2(0, -10);
            rt.sizeDelta = new Vector2(0, 200);

            var img = header.GetComponent<Image>();
            if (img != null && ColorUtility.TryParseHtmlString("#1A1A2EFF", out var c))
            { result.RegisterObjectModification(img); img.color = c; }
        }

        var title = GameObject.Find("HeaderTitle")?.GetComponent<TextMeshProUGUI>();
        if (title != null)
        {
            result.RegisterObjectModification(title);
            title.fontSize = 52;
            title.text = "Corrected Title";
        }

        var content = GameObject.Find("Content");
        if (content != null)
        {
            var vlg = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
            result.RegisterObjectModification(vlg);
            vlg.spacing = 20;
        }

        result.Log("Path B 수정 완료");
    }
}
```

### Step 3.4: 반복 판정

- 90%+ → PASS, Phase 4로 진행
- 5회 초과 → 현재 상태 보고, 남은 이슈 목록화, 사용자 판단 요청
- **라운드 코너로 인한 mismatch 는 점수에서 제외**한다 (자동 처리 정책상 보정 불가). `roundedSkipped` 항목에 기록만 한다.

## Phase 4: Multi-Resolution Validation

`references/resolution-profiles.md` 참조.

최대 3회/해상도 반복.

### Step 4.1: 해상도별 캡처 (unity-mcp `Unity_RunCommand`)

```csharp
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 사전 등록된 사이즈별 메뉴 호출 (assets/GameViewCapture.cs)
        var ok = UnityEditor.EditorApplication.ExecuteMenuItem(
            "Tools/UguiFromScreenshot/Capture Game View 1080x1920");
        result.Log("resolution capture: {0}", ok);
    }
}
```

임의 W×H 가 필요하면 `Capture Default Screen` 메뉴(부트스트랩) 또는 `Unity_RunCommand` 안에서 임시 카메라+RT 로 렌더 후 `File.WriteAllBytes` 로 PNG 저장. 출력 경로는 `Assets/Screenshots/resolution_{device}_{n}.png` 등 일관 명명을 사용.

### Step 4.2: 검증 기준


| Check       | Pass Criteria       |
| ----------- | ------------------- |
| Clipping    | 요소가 화면 밖으로 잘리지 않음   |
| Overlap     | 요소 간 비정상적 겹침 없음     |
| Readability | 텍스트 가독성 유지          |
| Proportion  | 비율 15% 이내 유지        |
| Spacing     | 여백/간격 비정상적 확대/축소 없음 |


### Step 4.3: 반응형 수정

문제 패턴별 해결:


| 문제       | 해결                                  |
| -------- | ----------------------------------- |
| 가로 잘림    | anchorMin.x=0, anchorMax.x=1 로 스트레치 |
| 세로 비율 깨짐 | Layout + ContentSizeFitter 조합       |
| 텍스트 잘림   | overflow=Ellipsis 또는 fontSize 축소    |
| 과도한 여백   | maxWidth 제한 또는 비율 앵커                |
| 요소 겹침    | Layout spacing 조정                   |


수정 후 모든 해상도에서 재검증.

### Step 4.4: 반복 판정

- 모든 해상도 통과 → Phase 5
- 3회 초과 미통과 → 해당 해상도 이슈 보고, 사용자 판단 요청

## Phase 5: Complete

1. Game View 기준 해상도 복원: unity-mcp 환경에서 GameView 종횡비를 강제 변경하는 안정적 공개 API 가 없으므로, `assets/GameViewCapture.cs` 의 RT 기반 캡처를 그대로 활용하거나 사용자에게 Game View 종횡비 변경 요청.
2. 결과 보고 (입력 경로에 따라 항목 다름):

**Path A (UnityToFigma) 보고서 양식**:

```
## UGUI 구성 완료 (UnityToFigma 경로)

### 입력: Figma 문서 ({URL})
### 임포트 요약: created=N, updated=N, skipped=N, failed=N, orphaned=N
### 최종 일치율: {score}%  (라운드 차이는 점수에서 제외)

### 생성 산출물 (importRoot={importRoot})
Screens/    : N 프리팹
Components/ : N 프리팹
Pages/      : N 프리팹
Textures/   : N 이미지
ServerRenderedImages/ : N 벡터 PNG
Fonts/      : N 폰트 (Google Fonts 다운로드 포함)

### 보정 내역 (Phase 2A)
- Iteration 1: PrimaryButton 색상 #1A1A2EFF 로 보정
- ...

### 라운드 코너 처리 결과 (UnityToFigma SDF 자동 처리)
- 정상 처리(roundedHandled): N 개 → 추가 작업 불필요
- pill/circle 후보(roundedExtreme): M 개 → 디자인 의도 일치 여부만 시각 확인 권장
  - {prefab path / cornerRadius} ...
- 검출 실패(roundedSkipped): K 개 (대개 0)

### 사용자 후속 작업 안내
- 폰트 교체가 필요한 family 는 패키지의 `UnityToFigma/Postprocess/Open Missing Font Window` 메뉴에서 사용자가 직접 ObjectField 로 선택. 한글 누락 시 Setup TMP Korean Fallback 만 추가 호출.
- 임포트되지 않은 효과(Inner Shadow/Blur 등): 미지원이므로 별도 구현 필요
- (선택) PrototypeFlow 가 필요하면 UGUI_FIGMA_BUILD_PROTOTYPE_FLOW=true 로 재동기화
- (선택) ImportRoot 변경: Assets/UnityToFigmaSettings.asset 의 ImportRoot 필드
```

**Path B (스크린샷) 보고서 양식**:

```
## UGUI 구성 완료 (스크린샷 경로)

### 입력: {스크린샷 경로}
### 최종 일치율: {score}%  (라운드 차이는 제외)

### 해상도별 결과
| Device | Resolution | Result |
|--------|-----------|--------|
| ...    | ...       | PASS   |

### 생성된 UI 계층 구조
UICanvas
  Header
    ...

### 수정 내역
- Iteration 1: Header 높이 180->200, 색상 수정
- Iteration 2: Content spacing 16->20

### 라운드 처리 미수행 항목 (사용자 수동 작업 필요)
{각진 placeholder 로 둔 라운드 후보들}

### 임시 파일
Assets/Screenshots/verify_*.png -- 삭제 가능
Assets/Screenshots/resolution_*.png -- 삭제 가능
```

1. 추가 작업 안내:
  - 라운드 처리 (`references/round-corner-policy.md` 의 사용자 안내 문구 그대로 출력)
  - 커스텀 폰트 적용 필요 시 안내
  - SafeArea 대응 필요 시 안내

## Error Handling


| 상황                                  | 대응                                                                                     |
| ----------------------------------- | -------------------------------------------------------------------------------------- |
| unity-mcp Bridge 연결 실패              | `Edit > Project Settings > AI > Unity MCP Server` 패널에서 Bridge Start + Accept 확인 안내. 5xx 시 Editor 가 켜져 있는지 + 프로젝트 경로 일치 여부 확인. |
| **Path A** FigmaToUnity 패키지 설치 실패   | `Client.List(true)` 결과 / `Unity_GetConsoleLogs` 확인. Unity 6 (6000.0+) 인지 확인.            |
| **Path A** Sync 후 `failed > 0`      | `Unity_GetConsoleLogs` 로 `[UnityToFigma]` 메시지 확인. 보정 단계 진입 금지. PAT/문서 권한 사용자 확인 요청.    |
| **Path A** PAT 입력 다이얼로그 발생          | 부트스트랩이 PAT 을 PlayerPrefs 에 저장 못 함. ContextFile `personalAccessToken` 또는 EditorPrefs `ugui.figma.pat` 재설정. |
| **Path A** TMP Essentials 미설치       | 사용자에게 `Window > TextMeshPro > Import TMP Essential Resources` 안내 후 중단.                 |
| **Path A** 라운드 mismatch 발견          | **수정하지 말 것.** `roundedSkipped` 항목으로 누적, Phase 5 보고.                                    |
| **Path B** Figma MCP 미연결            | MCP 서버 활성화 안내, 스크린샷 경로로 폴백 제안                                                          |
| **Path B** Figma URL 파싱 실패          | URL 형식 안내, 수동 fileKey/nodeId 입력 요청                                                     |
| **Path B** get_design_context 응답 과대 | get_metadata 로 구조 파악 후 자식 노드별 개별 조회                                                    |
| 에셋 다운로드 실패                          | placeholder Image로 생성, 수동 교체 안내. **임의 sprite 강제 할당 금지.**                               |
| GameObject 이름 중복                    | 고유 이름 사용 (기능적 명칭). `GameObject.Find` 가 중복 시 첫 번째만 반환하는 한계 인지.                          |
| 캡처 실패 (Edit Mode)                   | Play Mode 폴백 시도                                                                        |
| 해상도 변경 실패                           | 수동 Game View 설정 안내                                                                     |
| 반복 횟수 초과                            | 현재 상태 + 이슈 보고, 사용자 결정                                                                  |
| `RunCommand` 컴파일 에러 ("Inconsistent Accessibility") | `CommandScript` 를 **`internal`** 로 선언. public 금지.                            |
| `RunCommand` NullReferenceException | 클래스명을 **반드시 `CommandScript`** 로 작성. 다른 이름은 unity-mcp 측에서 못 찾음.                        |


## References

- `./references/figma-to-unity-convention.md` -- **AI 컨벤션 후처리 필독**. 명명 규칙(`Btn_/Txt_/Img_/Scroll_`), MonoBehaviour 매핑, 9-slice 판정, LayoutGroup 규칙. 패키지 측 `UnityToFigma/Postprocess/*` 메뉴와 짝을 이룬다.
- `./references/unity-to-figma-workflow.md` -- **Path A 필독**. FigmaToUnity 일괄 임포트 흐름, 다이얼로그 우회, 한계, 보정 패턴 (unity-cli 표기는 Phase 0 매핑 표 기준으로 변환)
- `./references/round-corner-policy.md` -- **전역 필독**. 라운드 코너 자동 처리 금지 정책
- `./references/unity-cli-gotchas.md` -- 구버전 unity-cli 브릿지 시행착오 정리. **unity-mcp 환경에서도 동일한 등가 변환이 필요한 함정들** (예: GameObject 중복 이름, asset add-to-scene 의 부모 누락 등) 을 보존한다.
- `./references/new-commands.md` -- (참고용) 구버전 unity-cli 명령어 레퍼런스. 본 스킬에서는 모두 unity-mcp 호출로 치환.
- `./references/anchoring-strategy.md` -- 반응형 앵커링 패턴 가이드 (주로 Path B 용)
- `./references/resolution-profiles.md` -- 다해상도 검증 프로파일
- `./references/figma-to-ugui-mapping.md` -- Figma 속성 → UGUI 매핑 상세 (Path B 보정 시 참조)
- `./references/font-fallback-policy.md` -- 한글/CJK 폰트 Dynamic SDF Fallback 정책
- `./references/figma-prep-policy.md` -- Sync 전 Figma 1차 정리 정책

## Scripts

> **본 스킬은 unity-mcp 채널만 사용하므로 bash·python 호출이 필요한 스크립트는 모두 제거되었다. 남아있는 스크립트는 `check_update.sh` (Phase −1, 스킬 자체 자동 업데이트) 단 하나로, 이는 unity-mcp 와 무관한 git+curl 동기화용이다.**

- `./scripts/check_update.sh` -- **현역**. 스킬 자체 자동 업데이트 (Phase −1). git+curl 만 사용.

구 unity-cli 시대 스크립트 (삭제됨) 의 대체:

| 삭제된 스크립트 (구 unity-cli) | 현재 처리 (unity-mcp) |
| -------------------------- | ------------------- |
| `ensure_unity_cli.sh`             | 호출 불필요 (unity-mcp 채널 사용)                                                          |
| `ensure_unity_to_figma_package.sh` | Step 1A.2 의 `Unity_RunCommand` 스니펫 (`Client.List(true)` → 없으면 `Client.Add(...)`)  |
| `run_unity_to_figma_sync.sh`       | Step 1A.3 의 `Unity_RunCommand` 스니펫 (ContextFile JSON 작성 + EditorPrefs + Sync 메뉴) |
| `ui_helper.sh`                     | unity-mcp 의 C# 스니펫 안에서 부모 transform 을 직접 받아 reparent 불필요                           |
| `batch_set_texts.py`               | unity-mcp 의 C# 안에서 문자열 직접 대입 (쉘 이스케이프 자체가 없음)                                       |

## Assets

프로젝트 `Assets/_Project/Scripts/Editor/` (또는 `Assets/Editor/`) 에 복사해 사용. 메뉴 호출은 unity-mcp `Unity_RunCommand` → `EditorApplication.ExecuteMenuItem(...)` 로 일관 처리한다.

- `./assets/UnityToFigmaBootstrap.cs` -- **Path A 핵심**. FigmaToUnity 메뉴를 다이얼로그 없이 호출하기 위한 부트스트랩 + 결과 리포트 dump + Screen 인스턴스화 메뉴. `Tools/UnityToFigma Bootstrap/*` 메뉴 추가.
- `./assets/GameViewCapture.cs` -- `Tools/UguiFromScreenshot/Capture Game View 1080x1920`, `Tools/UguiFromScreenshot/Capture Game View 1440x3040` 메뉴 제공 (namespace `UguiFromScreenshot.Editor`). 임의 사이즈는 부트스트랩의 `Capture Default Screen` 메뉴 사용 권장.
- `./assets/FixTMPSettings.cs` -- 모든 TMP의 overflow/wrap 일괄 수정 (Ellipsis 방지).
- `./assets/ReplaceUISprites.cs` -- (Path B 전용) 모든 UI Image의 sprite를 평면 화이트로 일괄 교체. **Path A 결과에는 사용하지 말 것** (FigmaToUnity 의 SDF/Sprite 결과를 망친다).

