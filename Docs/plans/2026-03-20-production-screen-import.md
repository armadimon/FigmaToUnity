# Production Screen Import Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Unity 6000+에서 동작하는 Screen 중심 Figma 임포트 파이프라인을 만들고, 사용자 지정 출력 경로와 Node ID 기반 재임포트를 지원한다.

**Architecture:** 기존 임포터를 유지하되, 설정 모델, 경로 계산기, 매니페스트, 리포트 계층을 분리해 Screen 임포트 경로를 안정화한다. 파괴적인 폴더 초기화와 이름 기반 중복 회피를 점진적으로 걷어내고, `Node ID -> Asset Path` 추적을 통해 같은 Screen이 같은 경로에 갱신되도록 만든다.

**Tech Stack:** Unity Editor C#, UGUI, TextMeshPro, ScriptableObject, PrefabUtility, AssetDatabase, Newtonsoft Json

---

## Preconditions
- 현재 워크스페이스는 git 저장소가 아니므로, 아래 `Commit` 단계는 저장소 초기화 후에만 실행한다.
- 작업 중 검증 대상 Unity 버전은 `Unity 6000+`다.
- 구현은 `Screen 안정화 우선`이며, Prototype Flow/Auto Layout 고도화는 이번 범위에서 제외한다.

### Task 1: Unity 6000+ 기준선 확보

**Files:**
- Modify: `package.json`
- Modify: `README.md`
- Modify: `UnityToFigma/Editor/UnityToFigmaImporter.cs`
- Test: Unity Editor import/compile in 6000+

**Step 1: 호환성 기준을 명시하는 failing checklist를 만든다**

```markdown
- package.json 의 unity 버전이 6000+ 정책과 다르다
- README 가 2021.3 중심으로 되어 있다
- Unity 6000+에서 패키지 import 시 컴파일/메뉴 진입/설정 생성이 가능해야 한다
```

**Step 2: Unity 6000+에서 현재 상태를 확인한다**

Run: Unity 6000+ 에서 패키지를 임포트하고 콘솔 오류를 수집  
Expected: 버전 경고 또는 API 호환 문제 목록이 확보됨

**Step 3: 최소 호환 수정안을 적용한다**

```json
{
  "unity": "6000.0",
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "2.0.1-preview.1",
    "com.unity.textmeshpro": "2.0.1"
  }
}
```

```csharp
// UnityToFigmaImporter.cs
// Unity 6000+에서도 동작하도록 obsolete API 사용 여부를 정리하고
// 현재 씬/이벤트 시스템/캔버스 확인 흐름을 유지한다.
```

**Step 4: 기준선 동작을 재확인한다**

Run: Unity 6000+ 에서 `UnityToFigma/Sync Document`, `Select Settings File` 메뉴 진입  
Expected: 컴파일 에러 없이 메뉴와 설정 에셋 생성이 가능함

**Step 5: Commit**

```bash
git add package.json README.md UnityToFigma/Editor/UnityToFigmaImporter.cs
git commit -m "chore: align package baseline with Unity 6000+"
```

### Task 2: 설정 모델을 경로 정책 중심으로 확장

**Files:**
- Modify: `UnityToFigma/Editor/Settings/UnityToFigmaSettings.cs`
- Modify: `UnityToFigma/Editor/Settings/UnityToFigmaSettingsEditor.cs`
- Modify: `UnityToFigma/Editor/Settings/UnityToFigmaSettingsProvider.cs`
- Create: `UnityToFigma/Editor/Settings/UnityToFigmaImportSettingsDefaults.cs`
- Test: settings asset creation/edit flow

**Step 1: 새 설정 필드에 대한 failing checklist를 적는다**

```markdown
- Import Root 를 지정할 수 있어야 한다
- Screens/Components/Textures/Fonts/Pages/ServerRender/Manifest 하위 폴더명을 지정할 수 있어야 한다
- Screen Parent Transform Name 과 Create Missing Canvas 를 지정할 수 있어야 한다
- Path Update Policy / Missing Node Policy 를 선택할 수 있어야 한다
```

**Step 2: 기존 설정 인스펙터에서 필요한 필드가 없는지 확인한다**

Run: Unity 에서 `Assets/UnityToFigmaSettings.asset` 생성 후 인스펙터 확인  
Expected: 현재는 문서 URL, Auto Layout 등만 보이고 경로 정책 필드는 없음

**Step 3: 설정 모델과 에디터를 확장한다**

```csharp
public enum FigmaPathUpdatePolicy
{
    KeepExistingAssetPath,
    MoveToLatestResolvedPath
}

public enum FigmaMissingNodePolicy
{
    MarkAsOrphaned,
    DeleteOnImport
}

public string ImportRoot = "Assets/Figma";
public string ScreensFolderName = "Screens";
public string ComponentsFolderName = "Components";
public string TexturesFolderName = "Textures";
public string FontsFolderName = "Fonts";
public string PagesFolderName = "Pages";
public string ServerRenderedImagesFolderName = "ServerRenderedImages";
public string ManifestFolderName = "Manifest";
public string ScreenParentTransformName = "ScreenParentTransform";
public bool CreateMissingCanvas = true;
public FigmaPathUpdatePolicy PathUpdatePolicy = FigmaPathUpdatePolicy.KeepExistingAssetPath;
public FigmaMissingNodePolicy MissingNodePolicy = FigmaMissingNodePolicy.MarkAsOrphaned;
```

**Step 4: 설정 생성/수정 흐름을 검증한다**

Run: settings asset 생성 후 필드 입력, 저장, Unity 재시작 후 값 유지 확인  
Expected: 새 경로 정책 필드가 Project Settings 와 Inspector 모두에서 유지됨

**Step 5: Commit**

```bash
git add UnityToFigma/Editor/Settings/UnityToFigmaSettings.cs UnityToFigma/Editor/Settings/UnityToFigmaSettingsEditor.cs UnityToFigma/Editor/Settings/UnityToFigmaSettingsProvider.cs UnityToFigma/Editor/Settings/UnityToFigmaImportSettingsDefaults.cs
git commit -m "feat: add configurable import roots and policies"
```

### Task 3: 경로 계산기를 분리하고 하드코딩 경로를 제거

**Files:**
- Modify: `UnityToFigma/Editor/Utils/FigmaPaths.cs`
- Create: `UnityToFigma/Editor/Import/FigmaImportPathResolver.cs`
- Create: `UnityToFigma/Editor/Import/FigmaImportAssetPath.cs`
- Modify: `UnityToFigma/Editor/FigmaApi/FigmaApiUtils.cs`
- Modify: `UnityToFigma/Editor/Nodes/FigmaAssetGenerator.cs`
- Modify: `UnityToFigma/Editor/Components/ComponentManager.cs`
- Modify: `UnityToFigma/Editor/Fonts/GoogleFontLibraryManager.cs`
- Modify: `UnityToFigma/Editor/Fonts/FontManager.cs`
- Modify: `UnityToFigma/Editor/PrototypeFlow/ScreenNameCodeGenerator.cs`
- Test: path resolution for screen/page/component/image/font

**Step 1: 현재 하드코딩 경로 사용 지점을 실패 목록으로 정리한다**

```markdown
- FigmaPaths 가 Assets/Figma 를 전역 상수로 고정
- CreateRequiredDirectories 가 페이지/스크린 폴더를 비움
- Screen/Page/Component/Image/Font/ServerRender 경로가 이름 기반만 사용
```

**Step 2: 기존 경로 사용 지점을 검색한다**

Run: `FigmaPaths.` 및 `SaveAsPrefabAssetAndConnect` 호출부 확인  
Expected: `FigmaAssetGenerator`, `ComponentManager`, `FigmaApiUtils`, 폰트 생성 코드가 모두 연결되어 있음

**Step 3: 설정 기반 경로 계산기를 도입한다**

```csharp
public sealed class FigmaImportPathResolver
{
    public string GetScreensRoot(UnityToFigmaSettings settings) { ... }
    public string GetTexturesRoot(UnityToFigmaSettings settings) { ... }
    public string ResolveScreenPrefabPath(Node node, FigmaImportProcessData data) { ... }
    public string ResolvePagePrefabPath(Node node, FigmaImportProcessData data) { ... }
    public string ResolveComponentPrefabPath(Node node, Node parentNode, FigmaImportProcessData data) { ... }
    public string ResolveImageFillPath(string imageId, Node owningNode, FigmaImportProcessData data) { ... }
}
```

```csharp
// FigmaPaths.cs
// 문자열 상수 모음에서 설정 기반 경로 헬퍼로 축소하고,
// 폴더 전체 삭제 로직은 제거한다.
```

**Step 4: 경로 계산 결과를 검증한다**

Run: 샘플 설정에 `ImportRoot = Assets/UI/Figma` 입력 후 screen/component/image 경로 로그 출력  
Expected: `Assets/UI/Figma/Screens/...`, `Assets/UI/Figma/Textures/...` 등 설정 기반 경로가 일관되게 계산됨

**Step 5: Commit**

```bash
git add UnityToFigma/Editor/Utils/FigmaPaths.cs UnityToFigma/Editor/Import/FigmaImportPathResolver.cs UnityToFigma/Editor/Import/FigmaImportAssetPath.cs UnityToFigma/Editor/FigmaApi/FigmaApiUtils.cs UnityToFigma/Editor/Nodes/FigmaAssetGenerator.cs UnityToFigma/Editor/Components/ComponentManager.cs UnityToFigma/Editor/Fonts/GoogleFontLibraryManager.cs UnityToFigma/Editor/Fonts/FontManager.cs UnityToFigma/Editor/PrototypeFlow/ScreenNameCodeGenerator.cs
git commit -m "refactor: resolve generated asset paths from settings"
```

### Task 4: Node ID 기반 매니페스트와 리포트 계층 추가

**Files:**
- Create: `UnityToFigma/Editor/Import/FigmaImportManifest.cs`
- Create: `UnityToFigma/Editor/Import/FigmaImportManifestEntry.cs`
- Create: `UnityToFigma/Editor/Import/FigmaImportManifestStore.cs`
- Create: `UnityToFigma/Editor/Import/FigmaImportReport.cs`
- Modify: `UnityToFigma/Editor/FigmaImportProcessData.cs`
- Modify: `UnityToFigma/Editor/UnityToFigmaImporter.cs`
- Test: manifest create/load/update

**Step 1: 추적해야 할 필드를 실패 목록으로 적는다**

```markdown
- File ID
- Page ID
- Node ID
- Node Name
- Node Type
- Generated Asset Path
- Hierarchy Snapshot
- Status(active/missing/moved)
- created/updated/skipped/failed/orphaned 카운트
```

**Step 2: 현재 임포트가 어떤 상태도 저장하지 않는지 확인한다**

Run: 두 번 연속 import 후 같은 노드가 어떻게 저장되는지 수동 확인  
Expected: 현재는 이름 기반 저장과 폴더 비우기에 의존하며 Node ID 매핑 파일이 없음

**Step 3: 매니페스트 및 리포트 모델을 추가한다**

```csharp
[Serializable]
public sealed class FigmaImportManifestEntry
{
    public string FileId;
    public string PageId;
    public string NodeId;
    public string NodeName;
    public string NodeType;
    public string AssetPath;
    public string HierarchyPath;
    public string Status;
}
```

```csharp
public sealed class FigmaImportReport
{
    public int Created;
    public int Updated;
    public int Skipped;
    public int Failed;
    public int Orphaned;
    public List<string> Messages = new();
}
```

**Step 4: 매니페스트 저장/로드를 검증한다**

Run: import 전/후 manifest asset 생성 확인, 두 번째 import 후 동일 Node ID 엔트리 재사용 확인  
Expected: 같은 Node ID 가 같은 manifest entry 를 갱신함

**Step 5: Commit**

```bash
git add UnityToFigma/Editor/Import/FigmaImportManifest.cs UnityToFigma/Editor/Import/FigmaImportManifestEntry.cs UnityToFigma/Editor/Import/FigmaImportManifestStore.cs UnityToFigma/Editor/Import/FigmaImportReport.cs UnityToFigma/Editor/FigmaImportProcessData.cs UnityToFigma/Editor/UnityToFigmaImporter.cs
git commit -m "feat: add import manifest and report models"
```

### Task 5: Screen 저장을 매니페스트 기반 재임포트로 전환

**Files:**
- Modify: `UnityToFigma/Editor/Nodes/FigmaAssetGenerator.cs`
- Modify: `UnityToFigma/Editor/UnityToFigmaImporter.cs`
- Modify: `UnityToFigma/Editor/FigmaImportProcessData.cs`
- Modify: `UnityToFigma/Editor/Utils/FigmaPaths.cs`
- Test: repeated screen import without duplicate asset creation

**Step 1: 현재 스크린 저장 실패 조건을 명시한다**

```markdown
- SaveFigmaScreenAsPrefab 가 이름 중복 카운터에 의존한다
- 재임포트 전에 Screens 폴더가 삭제된다
- 이름 변경 시 기존 자산과 연계할 방법이 없다
```

**Step 2: 동일 문서를 두 번 임포트했을 때 중복/삭제 동작을 확인한다**

Run: 같은 Figma 문서를 연속 두 번 import  
Expected: 현재는 폴더 정리와 이름 카운터 때문에 안정적 재사용이 되지 않음

**Step 3: Screen 저장 경로를 매니페스트 우선으로 바꾼다**

```csharp
private static string ResolveScreenPrefabPath(Node node, FigmaImportProcessData data)
{
    var existing = data.Manifest.GetEntry(node.id);
    if (existing != null && data.Settings.PathUpdatePolicy == FigmaPathUpdatePolicy.KeepExistingAssetPath)
        return existing.AssetPath;

    return data.PathResolver.ResolveScreenPrefabPath(node, data);
}
```

```csharp
// SaveFigmaScreenAsPrefab
// - 기존 entry 조회
// - 경로 결정
// - SaveAsPrefabAsset
// - manifest 갱신
// - report 에 created/updated 반영
```

**Step 4: 재임포트 동작을 검증한다**

Run: 동일 문서 2회 import 후 `.prefab` 경로 비교  
Expected: 동일 Screen Node ID 는 같은 경로에 갱신되고 새 duplicate prefab 이 생기지 않음

**Step 5: Commit**

```bash
git add UnityToFigma/Editor/Nodes/FigmaAssetGenerator.cs UnityToFigma/Editor/UnityToFigmaImporter.cs UnityToFigma/Editor/FigmaImportProcessData.cs UnityToFigma/Editor/Utils/FigmaPaths.cs
git commit -m "feat: preserve screen prefab paths across reimport"
```

### Task 6: 런타임 씬/Canvas 생성 위치를 명시적으로 제어

**Files:**
- Modify: `UnityToFigma/Editor/UnityToFigmaImporter.cs`
- Create: `UnityToFigma/Editor/Import/FigmaRuntimePlacementResolver.cs`
- Modify: `UnityToFigma/Runtime/UI/PrototypeFlowController.cs`
- Test: missing canvas / existing canvas / named parent transform cases

**Step 1: 현재 씬 생성 위치 문제를 실패 목록으로 적는다**

```markdown
- 현재는 활성 씬과 첫 Canvas 에 강하게 의존한다
- ScreenParentTransform 이름을 설정할 수 없다
- 재임포트 시 어느 부모 아래 생성될지 명시성이 약하다
```

**Step 2: 현재 씬/캔버스 확인 흐름을 재현한다**

Run: 빈 씬, Canvas 있는 씬, 다른 이름 부모 Transform 있는 씬 각각에서 import  
Expected: 현재는 일관성이 부족하거나 자동 생성 위치가 고정되어 있지 않음

**Step 3: 배치 해석기를 추가한다**

```csharp
public sealed class FigmaRuntimePlacementResolver
{
    public Canvas ResolveCanvas(UnityToFigmaSettings settings) { ... }
    public RectTransform ResolveScreenParent(UnityToFigmaSettings settings, PrototypeFlowController controller) { ... }
}
```

```csharp
// UnityToFigmaImporter.CheckRunTimeRequirements
// - 설정된 씬 열기
// - Canvas 확인 또는 생성
// - 설정된 이름의 ScreenParentTransform 확인 또는 생성
// - controller 에 명시적으로 연결
```

**Step 4: 씬 배치 일관성을 검증한다**

Run: 서로 다른 씬 상태에서 import 3회 반복  
Expected: 결과가 항상 지정된 Canvas 와 지정된 ScreenParentTransform 아래에 생성됨

**Step 5: Commit**

```bash
git add UnityToFigma/Editor/UnityToFigmaImporter.cs UnityToFigma/Editor/Import/FigmaRuntimePlacementResolver.cs UnityToFigma/Runtime/UI/PrototypeFlowController.cs
git commit -m "feat: make runtime screen placement deterministic"
```

### Task 7: 실패 리포트, orphan 처리, 검증 문서 업데이트

**Files:**
- Modify: `UnityToFigma/Editor/UnityToFigmaImporter.cs`
- Modify: `UnityToFigma/Editor/FigmaApi/FigmaApiUtils.cs`
- Modify: `README.md`
- Create: `docs/plans/2026-03-20-production-screen-import-validation.md`
- Test: auth failure / image download failure / renamed node / deleted node scenarios

**Step 1: 검증해야 할 실패 시나리오를 체크리스트로 만든다**

```markdown
- 잘못된 token
- 잘못된 document URL
- 일부 이미지 다운로드 실패
- Screen 이름 변경
- Screen 삭제 후 orphan 표시
```

**Step 2: 현재 실패 로그가 무엇을 남기는지 확인한다**

Run: 잘못된 토큰/URL 로 import 시도  
Expected: 일부 대화상자/경고는 있지만 summary 와 orphan 추적은 없음

**Step 3: 리포트 출력과 문서를 보강한다**

```csharp
// Import 종료 시
Debug.Log($"Import complete: created={report.Created}, updated={report.Updated}, skipped={report.Skipped}, failed={report.Failed}, orphaned={report.Orphaned}");
```

```markdown
## Unity 6000+ Usage
- Import Root 설정
- 재임포트 정책 설명
- orphaned 자산 정리 방법
- 알려진 제한 사항
```

**Step 4: 대표 시나리오를 검증한다**

Run: 샘플 문서로 정상 import 2회, 이름 변경, 삭제, 다운로드 실패를 각각 재현  
Expected: summary 출력, orphan 표시, 동일 Screen 경로 유지, 실패 메시지에 대상 경로/Node ID 포함

**Step 5: Commit**

```bash
git add UnityToFigma/Editor/UnityToFigmaImporter.cs UnityToFigma/Editor/FigmaApi/FigmaApiUtils.cs README.md docs/plans/2026-03-20-production-screen-import-validation.md
git commit -m "docs: add import reporting and validation guidance"
```

## Execution Notes
- 구현 순서는 반드시 `Task 1 -> Task 7` 순서를 유지한다.
- 각 Task 완료 후 Unity 6000+에서 실제 import 를 한 번씩 돌려 눈으로 결과를 확인한다.
- `PrototypeFlow`, `Binding`, `Complex Auto Layout` 변경은 이번 계획에 섞지 않는다.
- `docs/plans/2026-03-20-production-screen-import-design.md`를 항상 함께 참고한다.
