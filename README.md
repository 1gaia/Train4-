# 서울 지하철 4호선 운전 시뮬레이터

Unity `6000.6.8f1`에서 확인한 프로젝트입니다. Unity Hub에서 프로젝트 폴더를 열고, Project 창에서 `Assets/Scene/Oido_Run.unity`를 더블 클릭한 뒤 **Play**를 누릅니다. Game 탭에서 **운행 시작**을 누르거나 `Enter`를 누르면 오이도역에서 시작합니다.

오이도역에서 `E`로 문을 열고 3초 뒤 `E`로 닫으면 `W`로 출발할 수 있습니다. `S`는 제동, `Space`는 비상 제동, `C`는 시점 변경, `R`은 구간 재시도입니다.

## 1. 코드

### 오이도–진접 51개 역 운행

| 파일 | 담당 기능 |
| --- | --- |
| [Line4Drive.cs](Assets/Script/OidoLine4/Line4Drive.cs) | 가속·제동, 정차 판정, 출입문, 점수, 카메라, 운행 화면과 사운드 |
| [RouteSchedule.cs](Assets/Script/OidoLine4/RouteSchedule.cs) | 역 번호·이름, 정차 위치, 터널 구간 |
| [PassengerFlow.cs](Assets/Script/OidoLine4/PassengerFlow.cs) | 승객 모델 생성, 대기·승차·하차 움직임 |
| [PassengerFabric.shader](Assets/Script/OidoLine4/PassengerFabric.shader) | 승객 의상 셰이더 |

### Unity 에디터용 코드

| 파일 | 담당 기능 |
| --- | --- |
| [Line4Setup.cs](Assets/Script/OidoLine4/Editor/Line4Setup.cs) | 초기 빈 씬에서 운행 씬 열기, 열차와 운행 씬 구성 |
| [Line4Environment.cs](Assets/Script/OidoLine4/Editor/Line4Environment.cs) | 선로, 터널, 승강장, 주변 배경 메시 생성 |
| [StationDetails.cs](Assets/Script/OidoLine4/Editor/StationDetails.cs) | 역별 지붕과 안내 표지 구성 |
| [JinjeopBuild.cs](Assets/Script/OidoLine4/Editor/JinjeopBuild.cs) | 금정 이후 진접까지의 노선 확장, 정차 점수와 승하차 동작 검증 |
| [RailwayModule.cs](Assets/Script/Line4Drive/Editor/RailwayModule.cs) | 두 운행 씬을 생성할 때 40m 복선 모듈을 배치 |

혜화–동대문역사문화공원 3개 역 운행에는 별도 [운행 코드](Assets/Script/Line4Drive/Line4Drive.cs), [씬 구성 코드](Assets/Script/Line4Drive/Editor/Line4Setup.cs), [환경 생성 코드](Assets/Script/Line4Drive/Editor/Line4Environment.cs)가 있습니다.

## 2. Unity 씬

| 씬 | 용도 |
| --- | --- |
| [Oido_Run.unity](Assets/Scene/Oido_Run.unity) | 기본 실행 씬. 오이도에서 진접까지 51개 역 운행 |
| [Line4_Run.unity](Assets/Scene/Line4_Run.unity) | 혜화 → 동대문 → 동대문역사문화공원 3개 역 운행 |
| [Previous_Untitled.unity](Assets/Scene/Backup/Previous_Untitled.unity) | 초기 카메라와 조명이 있는 이전 씬 백업 |
| [0.unity](Assets/Scene/Backup/_Recovery/0.unity) | Unity 복구본 |

빌드에는 위 표의 첫 두 씬이 등록돼 있습니다. [씬 목록 설정](ProjectSettings/EditorBuildSettings.asset)

`Oido_Run`의 Hierarchy에서 `Line 4 | Route operation`은 운행 컨트롤러, `4054 | Ten-car consist`는 열차, `Driving camera`는 게임 카메라입니다. 노선 오브젝트는 `Oido - Geumjeong`과 `Geumjeong - Jinjeop`으로 나뉩니다.

선로·역·터널의 메시와 재질은 `Assets/OidoLine4/` 아래 [RouteAssets](Assets/OidoLine4/RouteAssets), [GeumjeongStations](Assets/OidoLine4/GeumjeongStations), [JinjeopStations](Assets/OidoLine4/JinjeopStations)에 있습니다. [Signs](Assets/OidoLine4/Signs)에는 역명과 안내 표지 이미지가 있습니다. 3개 역 씬의 환경 에셋은 [별도 RouteAssets 폴더](Assets/Line4Drive/RouteAssets)에 있습니다.

## 3. Blender 관련 파일

| 파일 | 용도 |
| --- | --- |
| [Line4_Sliding.fbx](Assets/Blender/Line4_Sliding.fbx) | 오이도–진접 씬의 10량 열차 모델. 출입문 개폐에 사용하는 블렌드 셰이프 포함 |
| [Line4_Unity.fbx](Assets/Blender/Line4_Unity.fbx) | 3개 역 씬의 열차 모델 |
| [Line4_Palette.png](Assets/Blender/Line4_Palette.png) | 두 열차 모델의 Unity 재질에 쓰는 색상 텍스처 |
| [Untitled.blend](Assets/Blender/Untitled.blend) | 선로 원본. Blender의 Scripting 작업 공간에서 `Text` 블록에 생성 코드가 있음 |
| [RailwayModule.fbx](Assets/Blender/RailwayModule.fbx) | 원본 코드로 만든 40m 직선 복선 모듈. 두 운행 씬의 레일·침목·체결구·자갈에 사용 |

프로젝트를 공유할 때는 `Assets`, `Packages`, `ProjectSettings`를 함께 사용하세요. 씬은 `.meta` 파일의 GUID로 에셋을 참조하고, 씬 생성 코드에도 일부 에셋 경로가 지정되어 있습니다.
