# UnityToFigma

`UnityToFigma`는 Figma 문서, 컴포넌트, 에셋, 프로토타입을 Unity로 손쉽게 가져오기 위한 패키지입니다.  
Figma 문서를 Unity 프로젝트에 연결하고, 에셋을 네이티브 Unity UI 형태로 임포트하는 과정을 최대한 단순하게 만들어 줍니다.

게임 잼, 빠른 프로토타이핑, 그리고 Figma 디자인을 Unity에 빠르게 반영하는 작업에 특히 적합합니다.  
패키지 매니페스트 기준 최소 버전은 Unity **6000.0**(Unity 6)입니다.

**주의**: 아직 초기 릴리스 단계이므로 미구현 기능이 많고 버그가 존재할 수 있습니다.

현재는 **Unity 6000.0 이상**(Unity 6)을 대상으로 합니다.

프로덕션 화면 파이프라인에서는 **Unity 6000.x 에디터**에서 TextMeshPro·Newtonsoft JSON 의존성을 맞춘 뒤 `Sync Document`로 반복 임포트하는 흐름을 권장합니다. 동일 Figma 파일을 다시 가져올 때는 설정의 **Import Paths & Policies**가 경로·삭제 노드 처리에 영향을 줍니다.

### 임포트 요약 · 매니페스트 · 재임포트 정책

- 동기화가 정상적으로 끝나면 **Console**에 `UnityToFigma import: created=…, updated=…, skipped=…, failed=…, orphaned=…, manifestRemoved=…` 형태의 **한 줄 요약**이 출력됩니다. 다운로드 실패·고아 매니페스트 항목 등이 있으면 같은 내용으로 **대화 상자**가 한 번 뜰 수 있습니다.
- **`PathUpdatePolicy`**: Figma 트리/폴더 레이아웃이 바뀌었을 때 화면 프리팹을 **기존 에셋 경로에 유지**할지(`KeepExistingAssetPath`, 기본값), **최신 규칙으로 잡힌 경로로 맞출지**(`MoveToLatestResolvedPath`)를 결정합니다. 매니페스트에 기록된 경로와 `FigmaImportPathResolver`의 해석이 함께 사용됩니다.
- **`MissingNodePolicy`**: 이전에 임포트했던 노드가 문서에서 사라진 경우(삭제·구조 변경 등), 매니페스트 행을 **고아로 표시**할지(`MarkAsOrphaned`, 기본값), **매니페스트에서만 제거**할지(`DeleteOnImport`)를 결정합니다. **디스크에 있는 프리팹·텍스처 파일은 자동으로 삭제하지 않습니다** — 필요하면 프로젝트에서 수동으로 정리하세요.
- 상세 메시지는 Console의 `[UnityToFigma]` 로그에 추가로 쌓입니다.

## 주요 기능

- 핵심 Figma 요소를 네이티브 Unity 오브젝트로 재구성
- Section을 포함한 Figma 프로토타입 플로우를 Unity 프로토타입으로 재현
- 주요 Figma 도형(Ellipse, Rectangle, Star)을 위한 SDF 렌더러 제공
- Figma 컴포넌트를 프리팹으로 생성하고 참조 관계까지 연결(중첩 컴포넌트 포함)
- 반응형 레이아웃 및 디바이스 Safe Area 지원
- 필요한 폰트 에셋 자동 생성(부족한 폰트는 Google Fonts에서 다운로드 가능)
- Unity MonoBehaviour와 필드를 Figma 오브젝트에 바인딩
- 벡터 도형 서버 사이드 렌더링 지원
- Auto Layout 지원(실험적 기능, 복잡한 레이아웃에서는 문제가 생길 수 있음)
- 스크롤 프레임 지원(overflow scrolling 설정 사용)

## 설치 방법

- `Window -> Package Manager`를 열고, 좌측 상단의 `Add Package` 아이콘을 눌러 Git 패키지를 추가합니다.
- 이 저장소의 Git URL을 입력합니다.
- `Edit -> Project Settings`를 열고 `Create`를 눌러 새 설정 에셋을 생성합니다.
- Figma 문서 URL을 입력합니다. 예: `https://www.figma.com/file/..../...`
- [Figma Personal Access Token](https://www.figma.com/developers/api#authentication)을 발급받아 입력합니다. 위치: `Settings -> Account -> Personal Access Tokens`
- 프로젝트에 TextMeshPro가 없다면 `Window -> Text Mesh Pro -> Import TMP Essential Resources`로 TMP Essentials를 먼저 임포트합니다.

## 프로토타이핑

기본적으로 임포터는 Figma 파일의 프로토타입 탭에 정의된 플로우를 그대로 재현합니다.  
이 동작이 필요 없으면 설정에서 `Build Prototype Flow`를 끄면 되고, 그러면 Figma 에셋만 생성됩니다.

또한 Figma Section도 지원하며, 동작 방식은 [Figma의 Sections 문서](https://help.figma.com/hc/en-us/articles/9771500257687-Organize-your-canvas-with-sections)를 따릅니다.  
각 Section에서 활성 상태였던 화면은 프로토타입 실행 중에도 기억됩니다.

`Build Prototype Flow`가 켜져 있으면 필요한 에셋(`Canvas`, `PrototypeFlowController`, `EventSystem`)을 생성하고, 현재 활성 씬에 기본 화면을 인스턴스화합니다.  
플레이를 시작하면 버튼이 Figma 문서에 정의된 전이 규칙에 맞춰 지정된 화면으로 이동합니다.

## Figma 문서 동기화

- `UnityToFigma -> Sync Document`를 클릭합니다.
- Personal Access Token을 입력합니다. 입력한 값은 이후 재사용을 위해 `PlayerPrefs`에 저장됩니다.
- 현재 씬을 프로토타입 플로우 생성용으로 사용할지 묻는 창이 나오면 `Yes`를 선택합니다.

## Figma 페이지 선택

Figma 문서의 특정 페이지들만 가져오고 싶다면(예: 메모용 페이지나 브레인스토밍 페이지 제외), 설정 파일에서 `Select Pages to import`를 활성화하면 됩니다.  
그러면 문서를 다운로드한 뒤 선택 가능한 페이지 목록을 표시합니다. 이 기능은 임포트 시간을 줄이고 생성되는 에셋 수를 줄이는 데 도움이 됩니다.

선택하지 않은 페이지에는 다음 규칙이 적용됩니다.

- 컴포넌트는 다른 페이지에서 사용할 수 있으므로 계속 생성됩니다.
- 화면(Screen)은 생성되지 않습니다.
- 이미지 Fill은 다운로드되지 않습니다. 단, 컴포넌트 내부에 포함된 경우는 예외입니다.
- 서버 사이드 이미지 렌더링도 수행되지 않습니다. 단, 컴포넌트 내부에 포함된 경우는 예외입니다.

## Figma 오브젝트가 Unity에 매핑되는 방식

| Figma 노드 타입         | Unity 변환 방식                                                                                                                       |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| **Frames**              | 페이지 루트에 있는 프레임은 하나의 "화면(Screen)"으로 간주되어 프리팹으로 생성되고 `Screens` 폴더에 저장됩니다.                       |
| **Image fills**         | PNG로 다운로드되어 스프라이트로 임포트되며, Figma ID 이름으로 설정된 `Textures` 폴더(기본 `Assets/Figma/Textures`) 아래에 저장됩니다. |
| **Components**          | 프리팹으로 생성되어 `Components` 폴더에 저장됩니다.                                                                                   |
| **Component Instances** | 대응되는 컴포넌트 프리팹을 인스턴스화하고 수정된 속성을 적용합니다.                                                                   |
| **Pages**               | 각 페이지 전체를 프리팹으로 만들어 `Pages` 폴더에 저장합니다.                                                                         |
| **Vectors**             | PNG로 서버 렌더링됩니다. 자세한 내용은 아래 `Server Rendering` 섹션을 참고하세요.                                                     |

## 폰트

원클릭 동기화에 가깝게 만들기 위해, 프로젝트에 필요한 폰트가 없으면 Google Fonts에서 일치하는 TTF를 다운로드하고, 그에 맞는 TextMesh Pro 폰트를 자동 생성하려고 시도합니다.  
폰트 목록 데이터는 [Jonathan Neal의 google-fonts-complete 프로젝트](https://github.com/jonathantneal/google-fonts-complete)를 사용합니다.

정확히 일치하는 폰트를 찾지 못하면 다음 순서로 대체를 시도합니다.

- 프로젝트 안에 동일한 Google Font가 이미 다운로드되어 있는지 확인
- 없다면 Google Fonts에서 다운로드 가능한지 확인(그리고 해당 기능이 활성화되어 있는지 확인)
- 그래도 없으면 프로젝트 내부에서 가장 유사한 폰트를 탐색(이름 우선, 그다음 굵기 기준)

그림자와 스트로크 설정을 적용하기 위해 필요한 머티리얼 프리셋도 자동으로 생성됩니다.

또한 TextMeshPro 아웃라인 처리 방식 차이 때문에 커스텀 셰이더(`Figma/TextMeshPro`)를 적용합니다.  
이 셰이더는 TMPro 기본값인 `center` 대신 Figma 기본값인 `outside` 기준으로 스트로크를 처리합니다. 앞으로는 세 가지 모드 모두 지원할 예정입니다.

## Color Space

이 패키지는 `Gamma`와 `Linear` 렌더링을 모두 지원하지만, 다음 사항을 유의해야 합니다.

- 사용되는 모든 텍스처는 `sRGB`가 체크되어 있어야 합니다. 임포터가 자동으로 처리하지만(1.0.8 이후), 이전 버전에서 업그레이드했다면 재임포트하거나 수동으로 설정해야 할 수 있습니다.
- 현재 TextMeshPro 셰이더는 Linear Color Space에서 완전히 정확하게 렌더링되지 않습니다. 추후 업데이트에서 개선할 예정입니다.

## 에셋 내보내기

- 오브젝트가 export 대상으로 표시되어 있으면 이미지로 렌더링한 뒤 PNG로 다운로드하고, 현재는 설정된 `ServerRenderedImages` 폴더(기본 `Assets/Figma/ServerRenderedImages`) 아래에 저장합니다.
- export 이름은 경로 세그먼트로 해석되지 않으며, 안전한 파일명으로 정규화된 뒤 node id가 붙어 충돌을 피합니다. 예를 들어 `Textures/Icon/GameIcon` 같은 이름도 중첩 폴더로 풀리지 않고 하나의 파일명으로 저장됩니다.

## Server Rendering

문서를 로드하면 서버에서 렌더링해야 하는 노드를 찾습니다. 렌더링 배율은 설정 파일의 `Server Render Image Scale` 값을 사용하며, 기본값은 `3`입니다.

현재는 아래 조건 중 하나라도 만족하면 서버 렌더링 대상이 됩니다.

- 오브젝트가 벡터 도형인 경우
- 오브젝트가 벡터 도형 자식만 포함한 프레임인 경우
- 오브젝트 이름에 `render`가 포함된 경우

서버 렌더링이 너무 많아진다면 Figma 문서 구조를 조정해 최적화할 수 있습니다. 예를 들어, 벡터를 컴포넌트 안으로 모아 두면 한 번만 렌더링되도록 만들 수 있습니다.

## 반응형 레이아웃

화면은 Figma의 constraints 패널 설정을 기반으로 동작합니다. 다만 `Scale` constraint는 예외입니다.  
또한 `SafeArea` 컴포넌트를 사용하면 iPhone 같은 기기에서 안전 영역에 맞게 자동으로 레이아웃을 조정할 수 있습니다.

## Auto Layout

Figma 문서의 Auto Layout 설정에 맞춰 `Vertical Layout Group` 또는 `Horizontal Layout Group`이 자동 추가됩니다.  
다만 복잡한 레이아웃에서는 문제가 생길 수 있으므로 기본값은 비활성화되어 있으며, 설정에서 직접 켜야 합니다.

## 바인딩 동작

Figma와 동기화하면 기존 컴포넌트와 화면이 교체되기 때문에, 매번 수동으로 스크립트를 다시 붙이지 않도록 동기화 시점에 자동 바인딩을 수행할 수 있습니다.

MonoBehaviour는 컴포넌트나 화면에 자동으로 연결될 수 있고, 메서드는 버튼 클릭 이벤트에 자동 바인딩될 수 있습니다. 임포터는 리플렉션을 사용해 다음 작업을 수행합니다.

- 오브젝트 이름과 동일한 MonoBehaviour가 존재하면(대소문자 무시) 자동으로 붙입니다. 예를 들어 프레임 이름이 `PlayScreen`이고 같은 이름의 MonoBehaviour가 있으면 동기화 시 자동 추가됩니다.
- 연결된 MonoBehaviour에 직렬화 가능한 필드(`public` 또는 `[SerializeField]`)가 있으면, 최대 깊이 2 안에서 동일한 이름의 오브젝트를 찾아 대응되는 컴포넌트를 할당합니다. 예를 들어 `public TextMeshPro_UGUI Title` 필드가 있고 `Title`이라는 텍스트 오브젝트가 있으면 자동 연결됩니다.

- 메서드에 `[[BindFigmaButtonPress("PlayButton")]]` 특성을 추가하면, 해당 이름의 버튼을 찾아(`depth = 2`) `onClick` 리스너를 연결합니다.

다음과 같은 특수 케이스 컴포넌트도 자동 추가됩니다.

- 오브젝트 이름에 `Button`이 포함되거나 활성화용 프로토타입 링크가 있으면 `Button` 컴포넌트를 추가합니다.
- 오브젝트 이름이 `SafeArea`이면 안전 영역 컴포넌트를 추가합니다. 노치가 있거나 직사각형이 아닌 화면을 가진 기기에서 유용합니다.

버튼이 추가된 경우에는 `selected`라는 이름의 자식 노드를 찾고, 존재하면 롤오버 상태용으로 사용합니다.

## 전환(Transitions)

`PrototypeFlowController`는 화면 전환 애니메이션을 위한 `TransitionEffect`를 가질 수 있습니다.  
기본으로는 검은색 페이드 템플릿이 생성되며, 필요하면 다른 효과로 교체해 사용할 수 있습니다.

## 의존성

패키지를 추가하면 아래 의존성도 자동으로 함께 임포트되어야 합니다.

- `TextMeshPro 2.0.1`
- `com.unity.nuget.newtonsoft-json 2.0.1-preview.1`

## 기타

Personal Access Token을 변경해야 한다면 `UnityToFigma -> Set Personal Access Token` 메뉴를 사용하면 됩니다.

## 크레딧

사용한 리소스:

- [Inigo Quilez의 2D SDF Functions](https://iquilezles.org/articles/distfunctions2d/)
- [krzy-s의 UnityWebRequestAwaiter](https://gist.github.com/krzys-h/9062552e33dd7bd7fe4a6c12db109a1a)
- [Jonathan Neal의 google-fonts-complete 목록](https://github.com/jonathantneal/google-fonts-complete)

공개해 주신 모든 분들께 감사드립니다.

## 현재 미지원 항목

- 이미지 보정(Exposure, Contrast)
- 그림자 Blur
- Ellipse sweep angle 및 fill ratio
- 대부분의 효과(Inner Shadow, Layer Blur, Background Blur)
- 단일 오브젝트의 다중 Fill
- 단색 이외의 Stroke 스타일
- 도형용 Stroke 위치(`outside/center` 외 세부 지원)
- 텍스트용 Stroke 위치(`inside/center`)
- 디바이스 폰트 동적 생성
- 비디오 Fill
- Star 도형의 5개 초과 포인트 및 비기본 반경(API 데이터 한계)
- 균일 스케일이 아닌 Star 렌더링
- Polygon 도형
- Boolean 연산
- Line/Arrow
- 일관된 UUID
- `Scale` constraint 지원
