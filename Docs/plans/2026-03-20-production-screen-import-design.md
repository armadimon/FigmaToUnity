# UnityToFigma Production Screen Import Design

## Goal
Unity 6000+ 환경에서 동작하는 `Screen` 중심의 Figma -> Unity 임포트 파이프라인을 만들고, 출력 경로 지정, 관리 가능한 자산 구조, Node ID 기반 재임포트, 안전한 덮어쓰기를 지원한다.

## Current State
- 패키지는 `Unity 2021.3` 기준으로 작성되어 있다.
- 출력 경로는 `FigmaPaths`에 `Assets/Figma/...` 형태로 하드코딩되어 있다.
- `FigmaPaths.CreateRequiredDirectories()`가 페이지와 스크린 프리팹 폴더의 기존 파일을 삭제한다.
- 스크린, 페이지, 컴포넌트, 이미지, 폰트 생성은 이름 기반 경로 생성에 의존한다.
- 재임포트 시 동일 Node를 안정적으로 추적하는 매니페스트가 없다.
- 런타임 UI 생성 위치는 현재 씬과 캔버스 존재 여부에 크게 의존한다.

## Product Direction
- 첫 라이브 목표는 `Screen 단위 임포트 안정화`다.
- 프로토타입 플로우, 고급 Auto Layout, 자동 바인딩 고도화보다 먼저 임포트 안정성, 경로 관리, 재임포트 일관성을 확보한다.
- 패키지는 데모 수준이 아니라 실서비스 프로젝트에서 반복 실행 가능한 제작 파이프라인을 목표로 한다.

## Output Path Design
출력 경로는 `혼합형` 구조를 사용한다.

- 사용자는 하나의 `Import Root`를 지정한다.
- 그 아래에 자산 타입별 하위 루트를 둔다.
- 실제 파일 경로는 사람이 읽기 쉬운 `Page / 상위 노드 경로 / 자산명` 규칙을 반영한다.
- 내부 동일성은 파일명이나 폴더명이 아니라 `Figma Node ID`로 판단한다.

### Recommended Settings
- `Import Root`
- `Screens Folder Name`
- `Components Folder Name`
- `Textures Folder Name`
- `Fonts Folder Name`
- `Pages Folder Name`
- `Server Render Folder Name`
- `Manifest Folder Name`
- `Runtime Assets Scene Path`
- `Screen Parent Transform Name`
- `Create Missing Canvas`
- `Path Update Policy`
- `Missing Node Policy`

### Example Layout
- `Assets/UI/Figma/Screens/<Page>/<Screen>.prefab`
- `Assets/UI/Figma/Components/<Page>/<ComponentPath>/<ComponentName>.prefab`
- `Assets/UI/Figma/Textures/<Page>/<NodePath>/<AssetName>.png`
- `Assets/UI/Figma/Fonts/<FontName>.ttf`
- `Assets/UI/Figma/Manifest/<FileId>.asset`

## Asset Identity And Reimport
재임포트의 핵심 원칙은 `경로는 사람이 읽기 좋게, 동일성 판단은 Node ID로`다.

### Required Manifest Data
- `Figma File ID`
- `Page ID`
- `Node ID`
- `Node Name`
- `Node Type`
- `Generated Asset Path`
- `Node Hierarchy Snapshot`
- `Last Imported At`
- `Last Imported Hash` 또는 변경 감지용 필드
- `Status` (`active`, `missing`, `moved`)

### Reimport Rules
1. 동일 `Node ID`는 항상 같은 Unity 자산으로 간주한다.
2. 동일 `Node ID`의 이름이 바뀌어도 기본값은 기존 경로를 유지한다.
3. 동일 `Node ID`의 상위 경로가 바뀌어도 기본값은 기존 경로를 유지한다.
4. 사용자가 원하면 별도 정리 액션으로 최신 이름/구조에 맞춰 이동할 수 있다.
5. Figma에서 삭제된 노드는 자동 삭제하지 않고 `orphaned` 또는 `missing` 상태로 표시한다.
6. 동일 이름이지만 다른 `Node ID`는 새로운 자산으로 취급한다.

## Runtime Placement Design
씬 생성 위치도 설정 가능한 자산 구조의 일부로 본다.

- 대상 씬은 설정 에셋에서 명시한다.
- 캔버스 생성은 자동/수동 여부를 선택할 수 있게 한다.
- 스크린 인스턴스는 설정된 부모 Transform 아래에만 생성한다.
- 재임포트마다 다른 씬/다른 캔버스에 결과가 흩어지지 않게 한다.

## Recommended Delivery Strategy
추천 전략은 기존 파이프라인 전체를 갈아엎는 대신, `Screen 파이프라인 중심 재정비`다.

### Option A: 빠른 현대화 후 기능 추가
- 장점: 초기 진입 속도가 빠르다.
- 단점: 경로와 재임포트 문제를 나중에 다시 뜯게 될 가능성이 높다.

### Option B: Screen 파이프라인 중심 재정비
- 장점: 관리 가능한 출력 구조, 재임포트 일관성, 확장 가능성을 동시에 확보하기 쉽다.
- 단점: 처음 몇 단계는 겉으로 보이는 기능 증가가 적다.

### Option C: 신규 v2 임포터 병행 구축
- 장점: 가장 깔끔한 구조를 만들 수 있다.
- 단점: 지금 저장소 규모 대비 투자 비용이 크고 이행 기간이 길다.

### Recommendation
`Option B`를 채택한다.

## Feature Priority
### Must Have For V1
1. Unity 6000+ 컴파일 및 기본 실행 가능
2. 새 설정 구조와 경로 정책
3. `PathResolver` 계층 분리
4. `Node ID -> Asset Path` 매니페스트 저장
5. Screen 프리팹 동일 위치 덮어쓰기
6. 씬 내 생성 위치 고정
7. 임포트 결과 요약 리포트
8. 대표 샘플 문서 기준 반복 검증

### Explicitly Deferred
- Prototype Flow 완성도 향상
- 자동 바인딩 고도화
- 복잡한 Auto Layout 완전 대응
- 폰트/벡터/이펙트 품질 개선
- 자동 삭제 및 자동 이동 고도화
- 컴포넌트 시스템 전면 안정화

## Error Handling
`전체 중단보다 단위 실패 기록`을 기본 원칙으로 한다.

- 인증/문서 다운로드 실패는 즉시 중단하고 원인을 명확히 표시한다.
- 페이지/노드/자산 단위 실패는 가능한 범위에서 계속 진행하고 결과에 기록한다.
- 요약에는 `created`, `updated`, `skipped`, `failed`, `orphaned`를 포함한다.
- 실패 로그에는 대상 경로, Figma 경로, Node ID를 함께 남긴다.

## Testing Strategy
### Core Test Tracks
1. Unity 6000+ 호환성 테스트
2. 동일 문서 2회 이상 재임포트 테스트
3. 이름 변경/경로 변경 대응 테스트
4. 실패 복구 및 리포트 테스트

### Sample Documents
- 단순 Screen 문서
- 중간 복잡도 문서
- 실서비스에 가까운 복합 문서

## Definition Of Done For V1
- Unity 6000+에서 패키지가 설치 및 컴파일된다.
- 설정 에셋에서 출력 루트와 하위 폴더 정책을 지정할 수 있다.
- Screen 프리팹이 관리 가능한 경로에 생성된다.
- 같은 Figma Screen은 재임포트 시 같은 위치에서 갱신된다.
- 이름 변경/구조 변경이 있어도 불필요한 참조 깨짐을 최소화한다.
- 삭제된 노드는 `orphaned`로 표시된다.
- 임포트 결과 summary가 제공된다.
- 대표 샘플 문서 기준 반복 검증이 가능하다.

## Next Step
이 설계를 바탕으로 구현 계획 문서를 작성하고, 구현은 `호환성 -> 설정 -> 경로 결정기 -> 매니페스트 -> Screen 안정화 -> 리포트/검증` 순서로 진행한다.
