# Figma → Unity 임포트 AI 컨벤션 후처리

> **도구 표기 주의**: 본 스킬은 unity-mcp 채널만 사용한다. 본 문서에 등장하는 Editor 호출은 모두 SKILL.md Phase 0 매핑 표에 따라 `mcp__unity-mcp__Unity_RunCommand` 안의 C# 스니펫으로 변환해서 실행한다.

`com.armadimon.figmatounity` 패키지로 임포트한 결과물을 **AI(Claude)가 후처리해서** 프로젝트 코딩 컨벤션에 맞추는 파이프라인. **Figma 디자이너는 명명 규칙을 따르지 않는다고 가정**한다. 따라서 노드명 토큰(`Btn_`, `_9slice` 등)에 의존하지 않고, 노드 구조 + 이미지 + Figma 메타데이터로 역할을 추론한다.

> 패키지 측 컨벤션 후처리 메뉴는 `UnityToFigma/Postprocess/*` (FigmaToUnity/UnityToFigma/Editor/Postprocess/) 에 위치한다.

## 전체 파이프라인

```
[1] UnityToFigma > Sync Document  → {ImportRoot}/Screens/, Components/, Textures/, Fonts/
[2] AssetPostprocessor (자동)      → 스프라이트 maxSize/FullRect 강제, 9-slice border 는 AI 패스에서 갱신
[3] Postprocess Sync Options 창 (자동 표시)
    └ "AI 컨벤션 후처리 컨텍스트 생성 + 클립보드 복사" Apply
[4] AI 컨벤션 후처리 (Claude 호출)
    ├ Figma file JSON + 임포트된 프리팹 구조 + (필요시) Figma MCP 스크린샷 비교
    ├ 노드 역할 추론 (Button/Text/Image/ScrollArea/ListItem/Background)
    ├ 프로젝트 컨벤션명 재명명
    ├ MonoBehaviour 부착 + [SerializeField] 자동 매핑
    ├ 9-slice border 확정 (스프라이트 측에 반영)
    └ LayoutGroup 부착 여부 결정
[5] ugui-from-screenshot 보정 (Claude 호출, unity-mcp)
    ├ Apply Responsive Layout / Auto Anchor (anchor 디자인 의도 추론)
    ├ Setup TMP Korean Fallback (Dynamic SDF)
    ├ Capture Default Screen (디자인 사이즈 정확 캡처)
    └ Multi-resolution 검증
```

## 1. AI 가 도달해야 할 Unity 측 목표 상태

### 1-1. 프리팹·MonoBehaviour 명명 (프로젝트별)

명명 규칙은 **프로젝트마다 다를 수 있다**. 대표적인 컨벤션 예시:

| 종류 | 예시 컨벤션 | 예 |
|------|------------|----|
| Screen 프리팹 | `{Name}Screen.prefab` + 동명 MonoBehaviour | `LobbyScreen.prefab` ↔ `LobbyScreen.cs` |
| Component 프리팹 | `{Role}Component.prefab` | `RewardItemComponent.prefab` |
| 자식 텍스트 | `Txt_{Role}` | `Txt_Title` |
| 자식 버튼 | `Btn_{Role}` | `Btn_Play` |
| 자식 이미지 | `Img_{Role}` | `Img_Background` |
| 자식 스크롤 | `Scroll_{Role}` + 안쪽 `{Role}_Content` | `Scroll_StageList`, `StageList_Content` |
| Safe Area | `SafeArea` | (자동 컴포넌트 부착) |

**프로젝트별 컨벤션이 다른 경우** 해당 프로젝트의 `CLAUDE.md` / `agent_docs` 를 우선 참조한다. 위 표는 디폴트 예시.

### 1-2. MonoBehaviour 자동 매핑 (패키지가 자동 처리)

- Screen 프리팹: 동명 클래스가 프로젝트에 있으면 패키지가 Sync 시 자동 `AddComponent`
- 필드: `[SerializeField] private TextMeshProUGUI _txt_Title;` 같은 필드가 있으면 깊이 2 이내의 동명 노드를 찾아 자동 할당
- onClick: `[BindFigmaButtonPress("Btn_Play")]` 어트리뷰트 매핑

> AI 가 추가로 보완할 일: 필드 누락 시 [SerializeField] 추가, 누락된 클래스가 있으면 생성 안내.

## 2. AI 역할 추론 휴리스틱 (이름에 의존하지 않음)

### 2-1. Button 판별

다음 중 **둘 이상** 해당 시 Button 으로 분류 → `Btn_{Role}` 이름 + Button 컴포넌트:

- Figma `reactions` 배열에 클릭 트리거 존재 (가장 강한 신호)
- 프레임 안에 텍스트 자식 1~2개 + 배경 이미지/벡터 1개의 단순 구조
- `pointerLeave`/`pointerEnter` 상태 변형(variant) 이 있는 컴포넌트 인스턴스
- 비주얼: 직사각형 + 둥근 모서리 + 안쪽 중앙 텍스트 (LLM 이미지 분석 보조)

`{Role}` 결정 휴리스틱: 안쪽 텍스트 내용을 영문 카멜케이스화 (한글이면 의미 번역). 텍스트가 없으면 인접한 아이콘/스프라이트 파일명 참조.

### 2-2. 9-slice 판별 (스프라이트별)

- Figma 노드의 `cornerRadius` > 0 이면서 동시에 사이즈 가변 컨테이너로 사용됨 → 9-slice 후보
- `strokeAlign: INSIDE/OUTSIDE` 로 둘러싸인 단색/그라데이션 배경 → 9-slice 후보
- 같은 스프라이트가 서로 다른 사이즈로 여러 프레임에 사용됨 → 9-slice 후보
- 이미지 분석: 가장자리에서 일정 픽셀이 균질한 그래픽 (LLM 이미지 보조)

확정 시 `TextureImporterSettings.spriteBorder` 를 픽셀로 계산해 적용.

### 2-3. RectTransform Stretch / Anchor

> **이 항목은 ugui-from-screenshot 의 `Auto Anchor` (Tools/UnityToFigma Bootstrap/Auto Anchor) 메뉴가 자동 처리**한다. AI 컨벤션 후처리에서는 추가 작업 불필요. 자세한 규칙: `references/anchoring-strategy.md`.

### 2-4. ScrollArea / List 판별

- 부모 프레임이 `clipsContent: true` + 자식이 같은 컴포넌트 인스턴스로 반복 → ScrollArea
- 자식들이 일정 간격으로 세로 정렬 → `VerticalLayoutGroup`
- 자식들이 일정 간격으로 가로 정렬 → `HorizontalLayoutGroup`
- 그리드 패턴 (행·열) → `GridLayoutGroup`
- LayoutGroup 후처리는 **자동 일괄 부착하지 않고** Claude 가 케이스별로 판단 후 적용 (오탐 위험)

### 2-5. Background vs Foreground 판별

- 동일 부모 안에서 가장 큰 사이즈 + 가장 깊은 Z(맨 뒤) → Background
- 명칭은 `Img_Background` 또는 의미를 반영해 `Img_PanelBg` 등

## 3. AI 후처리에 필요한 입력

`UnityToFigma/Postprocess/Run AI Post-Process (Prepare Context)` 메뉴가 다음을 한 번에 모아 `{ImportRoot}/Debug/PostprocessContext.md` 로 출력한다:

1. **Figma file JSON 캐시**: `{ImportRoot}/Debug/FigmaOutput.json` (constraints/reactions/cornerRadius 등)
2. **임포트된 프리팹 구조**: `{ImportRoot}/Screens/*.prefab`, `Components/*.prefab`, `Pages/*.prefab`
3. **임포트 매니페스트**: `{ImportRoot}/Manifest/` (Figma nodeId ↔ Unity asset path 매핑)
4. **발견된 폰트 패밀리** 목록
5. **(선택) Figma MCP 스크린샷**: 시각 검증·역할 추론 보조에만 사용 (비용 큼)

## 4. 스프라이트 후처리 (자동, AssetPostprocessor)

`{ImportRoot}/Textures/` 에 들어오는 신규/갱신 스프라이트에 자동 적용. **이름 토큰에 의존하지 않는다.**

| 항목 | 규칙 |
|------|------|
| `spriteImportMode` | `Single` |
| `spriteMeshType` | `FullRect` |
| `maxTextureSize` | 원본 픽셀 크기 이상 가장 작은 2의 거듭제곱 (32~2048) |
| `spriteBorder` | **초기값은 0**. AI 분석 패스에서 9-slice 로 판정되면 그때 갱신 |

> 9-slice 자동 감지를 AssetPostprocessor 에서 시도하지 않음 (오탐 위험). AI 후처리 시점에 Figma 메타 + 이미지로 판단해 적용.

## 5. 폰트 처리 (자동 다운로드 + 실패 시 GUI 직접 선택)

- **자동 흐름**: FigmaToUnity 가 (1) 로컬 SDF 가 있으면 사용, (2) `EnableGoogleFontsDownloads=true` 일 때 Google Fonts 에서 다운로드 시도, (3) 다운로드 실패 / 미시도 시 `GetClosestFont` (Levenshtein 거리 최소) 로 임시 폴백.
- **GUI 직접 선택**: 다운로드 실패 / 미보유 폰트는 `FigmaMissingFontTracker` 가 자동 누적한다. Sync 종료 시점에 패키지의 `FigmaMissingFontWindow` 가 자동으로 떠서 family 별 `TMP_FontAsset` 을 사용자가 ObjectField 로 직접 선택할 수 있다. Apply 시 `{ImportRoot}/Screens/Components/Pages` 의 모든 프리팹에서 매칭되는 TMP_Text 폰트를 일괄 교체.
- **수동 호출**: `UnityToFigma/Postprocess/Open Missing Font Window` 메뉴로 언제든 다시 열 수 있다.
- **AI 후처리에서는 폰트를 직접 교체하지 않는다.** 사용자 GUI 선택을 우선시한다.
- **한글/CJK 폰트 누락 케이스**: `Tools/UnityToFigma Bootstrap/Setup TMP Korean Fallback` 메뉴로 **TMP Dynamic SDF Fallback** 만 등록한다 (`font` 프로퍼티는 건드리지 않음). 자세한 정책: `references/font-fallback-policy.md`.

## 6. LayoutGroup 후처리

**자동 부착하지 않는다.** Vertical/Horizontal/Grid 판별은 오탐 위험이 크고, 케이스마다 부착 위치(부모 vs Content)도 다르므로 Claude 가 분석 후 적용한다.

### 6-1. 사용자 호출 절차

1. `UnityToFigma > Sync Document` 실행 → Sync Options 창 자동 표시
2. "AI 컨벤션 후처리 컨텍스트 생성 + 클립보드 복사" 토글 + `Apply` → 컨텍스트 마크다운이 `{ImportRoot}/Debug/PostprocessContext.md` 에 저장되고 Claude 프롬프트가 클립보드에 복사됨
3. Claude 에게 그 프롬프트를 붙여 넣고 ugui-from-screenshot 스킬 invoke
4. Claude 가 unity-mcp `Unity_RunCommand` 로 프리팹을 열어 케이스별로 부착

### 6-2. Claude 의 판별 기준 (요약)

| 패턴 | 부착 위치 | 컴포넌트 | 설정 힌트 |
|------|----------|----------|---------|
| 스크롤 가능 + 자식이 위→아래 일정 간격 | `*_Content` | `VerticalLayoutGroup` + `ContentSizeFitter` | `controlChildSize`/`childForceExpand` 검토 |
| 스크롤 가능 + 자식이 좌→우 일정 간격 | `*_Content` | `HorizontalLayoutGroup` + `ContentSizeFitter` | 동일 |
| 행·열 격자 + 동일 사이즈 자식 반복 | 부모 프레임 | `GridLayoutGroup` | `cellSize`, `spacing`, `constraint` |
| 단일 자식만 가운데 정렬 | 부착하지 않음 | `RectTransform.anchor` 로 처리 | LayoutGroup 추가 금지 |

### 6-3. 자동화 시도 금지 사유

- 디자이너가 의도하지 않은 LayoutGroup 이 붙으면 기존 anchor/sizeDelta 가 강제로 덮어써져 시각 결과가 망가짐
- 한 번 잘못 붙은 LayoutGroup 은 자식 위치가 모두 변경되어 되돌리기 비용이 큼
- → "사람이 Claude 에게 요청 → Claude 가 한 화면씩 검증" 절차가 가장 안전

## 7. 자동 진입점 — Sync 직후 GUI

`UnityToFigma > Sync Document` 를 실행하면 임포트 종료 시점에 **`Figma Sync Options` 창이 자동으로 뜬다.** (`AssetPostprocessor` 가 `{ImportRoot}/{Screens,Components,Pages}/*.prefab` 변경을 감지)

창에서 선택 가능한 항목:

| 옵션 | 기본값 | 동작 |
|------|--------|------|
| AI 컨벤션 후처리 컨텍스트 생성 + 클립보드 복사 | ✓ | `{ImportRoot}/Debug/PostprocessContext.md` 생성 + 프롬프트 복사 |
| 다음부터 Sync 후 자동으로 띄우기 | ✓ | OFF 로 두면 자동 GUI 비활성, 메뉴로만 호출 |

> **위임된 항목** (RT anchor 보정, 한글 fallback, 다해상도 검증) 은 이 창에서 직접 실행하지 않고, AI 컨텍스트 안내를 보고 Claude 가 ugui-from-screenshot 스킬을 통해 처리한다.

**스프라이트 후처리** (Single + FullRect + maxSize=NextPOT) 는 `AssetPostprocessor` 로 항상 자동 적용 — 옵션 토글에 없음.

### 메뉴 일람

| 메뉴 | 설명 |
|------|------|
| `UnityToFigma/Postprocess/Open Sync Options Window` | Sync Options 창 수동 열기 (자동 띄우기 OFF 인 경우용) |
| `UnityToFigma/Postprocess/Run AI Post-Process (Prepare Context)` | AI 컨텍스트만 단독 생성 |

코드 위치: `FigmaToUnity/UnityToFigma/Editor/Postprocess/`
- `FigmaPostprocessPaths.cs` — 활성 Settings 의 ImportRoot 동적 참조 헬퍼
- `FigmaSpritePostprocessor.cs` — 스프라이트 자동 후처리 (`AssetPostprocessor`)
- `FigmaSyncWatcher.cs` — Sync 종료 감지 + Sync Options 창 자동 호출
- `FigmaPostSyncWindow.cs` — Sync Options 통합 창 (AI 컨텍스트 + 위임 안내)
- `FigmaSyncOptions.cs` — EditorPrefs 기반 옵션 저장소
- `FigmaPostprocessMenu.cs` — AI 컨텍스트 생성 + 메뉴 모음

## 8. 검증 체크리스트 (AI 후처리 종료 후)

- [ ] 임포트된 모든 Screen 프리팹이 프로젝트 명명 규칙 (`{Name}Screen` 등) 인지
- [ ] 동명 MonoBehaviour 가 부착되었는지 (있으면)
- [ ] 자동 매핑 가능한 모든 `[SerializeField]` 필드가 채워졌는지
- [ ] 9-slice 후보 이미지의 `spriteBorder` 가 합리적인지
- [ ] LayoutGroup 이 필요한 곳에만 부착되었는지 (오탐 없음)
- [ ] ugui-from-screenshot 의 Apply Responsive Layout / Setup TMP Korean Fallback 실행 완료
- [ ] Capture Default Screen 으로 디자인 사이즈 캡처 → Figma 와 시각 비교
