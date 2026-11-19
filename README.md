# 서울 지하철 4호선 운전 시뮬레이터

Unity `6000.6.8f1`에서 확인한 프로젝트입니다. Unity Hub에서 프로젝트 폴더를 열고, Project 창에서 `Assets/Scene/Oido_Run.unity`를 더블 클릭한 뒤 **Play**를 누릅니다. Game 탭에서 **운행 시작**을 누르거나 `Enter`를 누르면 오이도역에서 시작합니다.

오이도역에서 `E`로 문을 열고 3초 뒤 `E`로 닫으면 `W`로 출발할 수 있습니다. `S`는 제동, `Space`는 비상 제동, `C`는 시점 변경, `R`은 구간 재시도입니다.

## 1. 코드

### 오이도–진접 51개 역 운행

| 파일 | 담당 기능 |
| --- | --- |
| [Line4Drive.cs](Assets/Script/OidoLine4/Line4Drive.cs) | 가속·제동, 정차 판정, 출입문, 점수, 카메라, 운행 화면과 사운드 |
| [RouteSchedule.cs](Assets/Script/OidoLine4/RouteSchedule.cs) | 역 번호·이름, 정차 위치, 터널 구간 |
| [TrackAlignment.cs](Assets/Script/OidoLine4/TrackAlignment.cs) | 오이도–정왕 스플라인과 노선 거리 계산 |
| [MappedTrack.cs](Assets/Script/OidoLine4/MappedTrack.cs) | 정왕–진접 선형, 역 사이의 실제 이동거리와 곡선 경고 구간 |
| [TrackStructures.cs](Assets/Script/OidoLine4/TrackStructures.cs) | 위치에 따른 지상·터널·교량 구분 |
| [RailwayCorridor.cs](Assets/Script/OidoLine4/RailwayCorridor.cs) | 선로 주변 터널·교량·전차선의 청크 메시 생성 |
| [RailwayTrack.cs](Assets/Script/OidoLine4/RailwayTrack.cs) | 표준궤 레일·침목·도상 메시 생성과 주변 청크 관리 |
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

### 오이도–진접 선형

Project 창에서 [OidoAlignment](Assets/Resources/OidoAlignment.asset)를 선택하세요. Inspector에서 **Edit route in Scene view**를 켜고 **Section**에서 원하는 역 사이를 골라 **Frame selected section**을 누르면 레일과 침목이 표시됩니다. 오이도–정왕은 흰 제어점을 직접 옮길 수 있습니다. 정왕 이후 좌표는 [Line4MappedTrack.csv](Assets/Resources/Line4MappedTrack.csv)에 있으며, 첫 열은 `RouteSchedule`의 출발역 인덱스이고 나머지 두 열은 정왕을 원점으로 한 X, Z(미터)입니다. `Oido_Run`에서 Play를 누르면 새 경로로 주행할 수 있습니다.

평면 선형은 OpenStreetMap의 정차점과 연결된 선로에서 추출했습니다. 오이도–정왕의 기존 지도 기반 경로와 두 노선 관계에서 가져온 구간을 이었습니다. 기존 승강장, 10량 열차 및 스크린도어에 맞춰 정차점 앞 약 225m와 출발부 약 60m를 직선으로 맞추고 바깥쪽에서 지도 선형에 연결합니다. 게임에서 표시하는 거리는 이 경로를 따라 계산하므로 영업거리표와 차이가 있을 수 있습니다. CSV 좌표 데이터는 OpenStreetMap 기여자의 ODbL 1.0 자료를 바탕으로 합니다. [오이도–불암산 관계](https://www.openstreetmap.org/relation/4744311) · [사당–진접 관계](https://www.openstreetmap.org/relation/13675921) · [OpenStreetMap 기여자 및 이용 조건](https://www.openstreetmap.org/copyright) · [한국철도공사 노선 자료](https://info.korail.com/info/contents.do?key=1446)

복선 중심 간격은 기존 승강장 규격인 5.3m입니다. [Line4Structures.json](Assets/Resources/Line4Structures.json)에 공개 선로의 터널·교량 시작과 끝을 역간거리 비율로 저장합니다. `kind`의 값 1은 터널, 2는 교량입니다. 상록수–반월과 대야미–수리산의 짧은 터널, 동작대교, 쌍문–창동의 지상 전환, 별내별가람–오남의 지상 교량을 반영했습니다. 선로, 터널, 교량, 전차선은 동일한 경로와 청크 범위에서 생성됩니다.

2026-11-18에 네이버 지도의 공개 역 안내에서 51개 역 이름을 확인했습니다. 네이버의 세부 지도 화면은 접근 제한으로 곡률을 직접 대조하지 못했습니다. 구조 경계 좌표는 OpenStreetMap에서 가져왔고, 동작대교와 쌍문–불암산 지상 구간 및 진접선 지상 교량은 지자체 공개 자료로 추가 확인했습니다. [네이버 동작역](https://pts.map.naver.com/end-subway/ends/web/431/home) · [네이버 이촌역](https://pts.map.naver.com/end-subway/ends/web/430/home) · [네이버 창동역](https://pts.map.naver.com/end-subway/ends/web/412/home) · [서울시 지상철도 자료](https://mas.smc.seoul.kr/attach/record/SEOUL/appendix/a11/A0060751.pdf) · [서울시 진접선 소개](https://mediahub.seoul.go.kr/news/article/newsArticlePrintPopup.do?articleNo=2004060)

고저차, 캔트, 실제 분기기와 교량의 개별 형식은 현장 자료가 더 필요합니다. 교량 상판 폭과 교각 높이는 현재 게임 모델에 맞춘 값이고, 터널 진입점은 지도 선형과 직선화된 승강장 진입부 사이에서 오차가 생길 수 있습니다.

## 3. Blender 관련 파일

| 파일 | 용도 |
| --- | --- |
| [Line4_Sliding.fbx](Assets/Blender/Line4_Sliding.fbx) | 오이도–진접 씬의 10량 열차 모델. 출입문 개폐에 사용하는 블렌드 셰이프 포함 |
| [Line4_Unity.fbx](Assets/Blender/Line4_Unity.fbx) | 3개 역 씬의 열차 모델 |
| [Line4_Palette.png](Assets/Blender/Line4_Palette.png) | 두 열차 모델의 Unity 재질에 쓰는 색상 텍스처 |
| [Untitled.blend](Assets/Blender/Untitled.blend) | 선로 원본. Blender의 Scripting 작업 공간에서 `Text` 블록에 생성 코드가 있음 |
| [RailwayModule.fbx](Assets/Blender/RailwayModule.fbx) | 원본 코드로 만든 40m 직선 복선 모듈. 3개 역 씬과 에디터의 기본 환경에 사용 |

프로젝트를 공유할 때는 `Assets`, `Packages`, `ProjectSettings`를 함께 사용하세요. 씬은 `.meta` 파일의 GUID로 에셋을 참조하고, 씬 생성 코드에도 일부 에셋 경로가 지정되어 있습니다.
