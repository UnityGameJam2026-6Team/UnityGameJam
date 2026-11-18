# 마왕 도착 전 시설 청소: 구현 구상

> 근거: `Assets/GDD/GDD/Demon_King_Cleaning_Gameplay_Spec.md` (v0.2)
> 이 문서는 전체 구현의 큰 계획(마스터 플랜)이다. 프로젝트 규칙(`docs/rules/workflow.md`)에 따라 단계마다 `Assets/GDD/Spec/<기능>.md`를 따로 만들어 승인을 받은 뒤 구현한다.
> 결정 사항: **3D 탑다운 + NavMesh**, **1차는 기본 대사만 사용(LLM은 인터페이스만)**, **단계별 전체 구현**, **프로그래머 2명이 레인을 나눠 병렬로 작업(계약 먼저 확정)**

# Project Overview
- Game Title: 마왕 도착 전 시설 청소 (가제)
- High-Level Concept: 마왕이 도착하기까지 남은 3분 동안 졸개 4종에게 방을 맡기고 플레이어도 직접 청소한다. 일반 방 6개와 로비 3개가 각각 60% 이상, 시설 전체가 85% 이상이 되면 성공한다. 정보가 제한된 상황에서 분업과 배치를 판단하는 실시간 지휘 게임이다.
- Players: 싱글 플레이 (플레이어 1명이 AI 졸개 5마리를 지휘)
- Inspiration / Reference Games: GDD에 없음 (질문 Q16)
- Tone / Art Direction: GDD에 없음. 1차는 화이트박스(기본 도형 + 색 구분)로 만든다. 3D 공간이라 Spine이나 스프라이트 캐릭터는 나중에 교체할 수 있다.
- Target Platform: PC (Windows 64bit)
- Screen Orientation / Resolution: 가로 1920x1080 (임시 값, U-후보)
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
1. **배정**: 1층 로비에서 졸개에게 말을 걸어 방(이름과 층수)을 고른다. 고르는 동안 그 졸개는 멈추고, 타이머는 계속 흐른다.
2. **위임과 직접 행동**: 졸개는 이동, 현장 확인, 작업을 스스로 한다. 그동안 플레이어는 다른 방을 정찰하거나(HUD로 구역 수치 확인), 직접 청소하거나, 현관에서 헬하운드에게 배출을 명령한다.
3. **제안에 응답**: 할 일이 끝난 졸개가 우측 하단에 이동 제안을 올린다. 플레이어는 7초 안에 승인하거나 보류한다. 보류하면 졸개는 로비로 돌아가고, 다시 쓰려면 플레이어가 직접 찾아가야 한다.
4. **마감**: 180초가 되면 운반 중인 폐기물을 그 자리에 내려놓고, 9개 구역 기준과 시설 기준을 판정한다.

재미 요소(MDA 기준):
- **Mechanics**: 졸개 종마다 능력이 다르다. 졸개는 직접 본 정보만 알고, 그 정보는 갱신되지 않는다. 폐기물은 현관 배출 구역에 쌓였다가 헬하운드가 가져간다.
- **Dynamics**: 여러 방에 나눌지 한 방에 모을지 고르게 된다. 운반한 폐기물이 1층 로비 점수를 깎는다. 언제 현관에 들러 배출할지 판단하게 된다.
- **Aesthetics**: 시간에 쫓기는 긴장(Challenge), 부하를 다루는 지휘자 판타지(Fantasy), 종마다 다른 말투에서 나오는 개성(Expression).

## Controls and Input Methods
New Input System을 쓰고, 기존 `7_Setting/InputSystem_Actions.inputactions`(Project-wide)에 액션을 추가한다. 키 배치는 임시 값이며 U-후보로 기록한다.

| 액션 | 기본 키 (임시) | 용도 |
|---|---|---|
| Player/Move (기존) | WASD / 왼쪽 스틱 | 이동 |
| Player/Interact (기존) | E | 근처 대상과 상황별 상호작용: 줍기, 제자리에 놓기, 닦기, 졸개와 대화, 헬하운드 명령, 걸레 대여·반납 |
| Player/Drop (신규) | Q | 들고 있는 물건을 그 자리에 내려놓기 |
| Player/Approve (신규) | F | 가장 오래된 활성 제안 승인 |
| Player/Hold (신규) | R | 가장 오래된 활성 제안 보류 |
| Player/ToggleMinionPanel (신규) | Tab | 졸개 현황 패널 접기·펴기 |
| UI/Navigate, Submit, Cancel (기존) | 방향키, Enter, Esc | 방 배정 패널 |

# UI
UGUI와 TextMeshPro로 만든다(Runtime asmdef가 이미 참조한다). 원격 정보는 방 이름과 층수만 보여 주고, 구역 수치는 그 구역 안에 있을 때만 보여 준다.

```
┌──────────────────────────────────────────────────────────────┐
│ [1F] 휴게실                      ⏱ 00:02:31                  │
│ 청결도 42% (기준 60%) ▼미달                                   │
│                                                              │
│                                                              │
│                     (탑다운 게임 화면)            ┌─────────┐ │
│                                                  │오크: 창고로│ │
│ ┌졸개 현황 ▾┐                                     │갈까? ███░ │ │
│ │고블린 다목적실·운반│                             │[F]승인[R]보류│
│ │오크   창고·잔해운반│                             ├─────────┤ │
│ │스켈레톤 휴게실·걸레│                             │슬라임: 복귀│ │
│ │슬라임 로비대기     │      시설 청결도 71% ▼미달    │(응답 불필요)│
│ └──────────┘                                     └─────────┘ │
└──────────────────────────────────────────────────────────────┘
```
- **중앙 상단** `MatchTimerView`: 남은 시간. 표시 형식은 D5 결정에 따른다.
- **좌측 상단** `ZoneHudView`: 현재 층, 장소, 구역 청결도, "최저치"(Q4), 60% 미만이면 미달 표시. 화장실이나 계단에서는 수치를 숨긴다.
- **중앙 하단** `FacilityHudView`: 시설 청결도. 85% 미만이면 미달 표시.
- **좌측 하단** `MinionStatusPanel`: 접을 수 있는 패널. 졸개마다 담당 방과 현재 행동을 보여 준다.
- **우측 하단** `ProposalStackView`: 새 메시지가 아래에서 올라오며 기존 메시지를 위로 민다. 제안에는 7초 게이지와 승인·보류 키를 표시하고, 입력 대상(가장 오래된 활성 제안)을 강조한다. 통보는 회색으로 표시하고 입력을 막지 않는다.
- **방 배정 패널** `AssignmentPanel`: 졸개와 대화하면 열린다. 층별로 방 목록을 보여 주고, 졸개 이름과 현재 담당 방을 표시한다. 열린 동안 해당 졸개만 멈춘다. Esc로 취소한다(Q9).
- **결과 화면** `ResultPanel`: 마왕 등장 연출 뒤 9개 구역과 시설의 점수, 통과 여부, 재시작 버튼을 보여 준다. GDD에 없는 화면이다(Q11).

# Key Asset & Context

## 아키텍처 원칙
- 게임 규칙은 **순수 C#**(`UnityGameJam.Runtime`)으로 만들고 EditMode 테스트로 검증한다. MonoBehaviour는 이동, 입력, 연출, UI만 맡는다.
- 시간은 `float now`를 주입받는다. 시뮬레이션은 `MatchSimulation.Tick(deltaTime)` 하나로 진행하므로 테스트에서 같은 결과를 다시 만들 수 있다.
- 난수는 `IRandom`, 수치는 데이터 객체로 주입받는다(코딩 규칙).
- 졸개 두뇌(`MinionBrain`)는 이동 같은 의도(`MoveTo`, `PickUp`, `Work` 등)만 내보낸다. 실제 이동은 `MinionAgent`(NavMeshAgent)가 하고 도착을 알려 준다. 두뇌와 몸을 나눠서 상태 전이를 테스트할 수 있게 한다.

## 네임스페이스 (새 도메인이라 승인 필요)
`UnityGameJam.Facility`(층·구역·계단·화장실), `UnityGameJam.Cleaning`(오염·청결도·마감), `UnityGameJam.Minion`(졸개 능력·두뇌·기억·도구), `UnityGameJam.Proposal`(제안 대기열·대사), `UnityGameJam.Match`(타이머·흐름), `UnityGameJam.Player`, `UnityGameJam.UI`, `UnityGameJam.Data`(SO)

## 구역 ID와 GDD 이름 대응 (승인 필요)
| EZoneId | GDD 이름 | 층 | 종류 |
|---|---|---|---|
| Floor1Lobby | 1층 로비 (현관 포함) | 1 | 로비 |
| MultipurposeRoom | 다목적실 | 1 | 일반 방 |
| Lounge | 휴게실 | 1 | 일반 방 |
| Storage | 창고 | 1 | 일반 방 |
| Floor2Lobby | 2층 로비 | 2 | 로비 |
| MeetingRoom | 회의실 | 2 | 일반 방 |
| Workshop | 작업실 | 2 | 일반 방 |
| Floor3Lobby | 3층 로비 | 3 | 로비 |
| ExecutiveRoom | 간부실 | 3 | 일반 방 |

`EContaminationType`: HeavyDebris(무거운 잔해), Trash(일반 쓰레기), ClutterItem(어질러진 작은 물건), FloorStain(바닥 얼룩)
`EWorkerType`: Player, Goblin, Orc, Skeleton, Slime, Hellhound

## 순수 C# 핵심 클래스 (`2_Script/Runtime/<도메인>/`)
| 파일 | 역할 / 핵심 시그니처 |
|---|---|
| Facility/ZoneMap.cs | 위치를 구역으로 바꾼다. 구역마다 저작한 사각형 목록으로 판정한다. `EZoneId? FindZone(Vector3 pos)`, `EZoneId ResolveDropZone(Vector3 pos)` (판정 구역 밖 귀속 규칙, Q8/D7) |
| Facility/FloorRoute.cs | 층간 경로. `IReadOnlyList<RouteLeg> Plan(EFloor from, EFloor to)`. 계단 이동 시간은 데이터에서 읽는다. |
| Cleaning/ContaminationItem.cs | `Id, Type, Weight, State(EItemState), ZoneId, HomeSlotId?, ClaimedBy`. EItemState: OnFloor, Carried, InDischargeArea, Dispatched, Digesting, Resolved |
| Cleaning/ContaminationRegistry.cs | 모든 상태 변화가 이곳을 거친다. `TryClaim/Release`, `PickUp`, `Drop(pos)`, `PlaceAtHome`, `CompleteMop`, `StartDigest/CompleteDigest`, `DispatchDischargeBatch()`. 이벤트 `ItemChanged`를 낸다. |
| Cleaning/CleanlinessCalculator.cs | 시작 시점 스냅샷(구역별, 시설). `float GetZoneCleanliness(EZoneId)`, `float GetFacilityCleanliness()`. 계산식은 100×(1−현재/시작)이고 0~100으로 자른다. 포함 상태는 OnFloor와 InDischargeArea다. |
| Cleaning/ContaminationSpawner.cs | `IRandom`과 `ZoneSpawnTableSO`로 생성한다. 구역마다 최소 1개를 보장하고, 시설 전체 가중치가 허용 범위에 들어올 때까지 다시 뽑는다(최대 N회). |
| Cleaning/MatchResolver.cs | 마감 처리. 운반 중인 폐기물을 강제로 내려놓고 다시 귀속한 뒤, 소화 중이거나 출발한 묶음은 빼고 9개 구역과 시설을 판정한다. 결과는 `MatchResult`로 돌려준다. |
| Minion/WorkerCapability.cs | 종별로 할 수 있는 작업 표(Q1). `bool CanHandle(EWorkerType, EContaminationType)` |
| Minion/TaskSelector.cs | 우선순위는 작은 물건 → 무거운 잔해 → 일반 쓰레기 → 얼룩, 같은 유형이면 가까운 순이다. 할 수 없거나 선점된 개체는 건너뛴다. 걸레를 들고 있으면 얼룩만 고른다. |
| Minion/MopStationService.cs | 층마다 2개, 모두 6개. `TryBorrow(worker, preferFloor) → station`, `Return(worker)`, `AvailableCount` |
| Minion/DischargeService.cs | 헬하운드. 현관에 있을 때만 `Dispatch()`를 받는다. 출발 시점의 InDischargeArea 묶음을 Dispatched로 바꾸고, 왕복 시간 뒤 대기로 돌아온다. |
| Minion/MinionMemory.cs | 방마다 직접 본 스냅샷(방문 여부, 유형별 개수, 시각)과 다른 졸개의 담당 방(실시간). 물걸레 현황(D2)도 안다. |
| Minion/MinionBrain.cs | 계층형 상태 머신. LobbyIdle / Talking / MovingToRoom / Surveying(현장 확인·순찰) / Working{GoingToItem, CarryingToHome, CarryingToDischarge, FetchingMop, Mopping, ReturningMop, Eating} / AwaitingResponse / ReturningToLobby. 배정마다 `AssignmentId`가 1씩 늘어난다. |
| Minion/ProposalCandidateFinder.cs | 후보: 미확인 방(탐색), 기억상 할 일이 있는 방, 다른 졸개가 일하는 방, 근처 방. 제외: 현재 방, 기억상 깨끗한 방(Q6/D4). |
| Proposal/ProposalQueue.cs | 항목: `MinionId, AssignmentId, ShownAt, Kind(Proposal/Notice)`. 항목마다 표시 시점부터 7초를 센다. 승인·보류는 가장 오래된 활성 제안에만 적용한다. 만료되면 보류로 처리한다. 통보는 슬롯을 차지하지 않는다. 같은 졸개의 중복 제안은 막는다. |
| Proposal/IProposalDialogueProvider.cs | `void Request(ProposalContext ctx, Action<ProposalReply> onReply)`. 1차 구현은 `DefaultProposalDialogueProvider`(종별 대사 템플릿)다. 응답이 늦거나 맞지 않는 배정 ID로 오면 버린다. |
| Match/MatchSimulation.cs | 전체 진행. `Tick(dt)`, 상태는 Running → DemonKingArrived → Resolved. 한 틱 안에서 작업 완료를 먼저 처리하고, 그다음 제안 만료, 마지막으로 마감을 검사한다(Q14). |

## 데이터 (`3_Data/ScriptableObject/`, 코드에 수치를 쓰지 않음)
| SO | 내용 |
|---|---|
| GameRuleSO | 제한 시간 180, 구역 기준 60, 시설 기준 85, 제안 응답 7초, 통보 표시 시간, API 대기 한도, 상호작용 거리 |
| ContaminationTypeSO | 유형별 가중치(4/3/2/1), 플레이어 작업 시간 |
| ZoneSpawnTableSO | 구역별·유형별 최소·최대 개수와 비율(§3.2를 수치로 옮김), 시설 시작 가중치의 허용 범위 |
| WorkerStatsSO (종마다 1개) | 이동 속도, 운반 감속, 작업별 소요 시간, 소화 시간, 헬하운드 왕복 시간, 능력 표 |
| ProposalLineSO 또는 Json | 종별 기본 대사 템플릿({room}, {minion} 같은 자리표시자) |

## 씬·프리팹 (화이트박스)
- `1_Scene/Facility.unity`(새 씬, SampleScene은 그대로 둠): 3개 층을 X축으로 떨어뜨려 배치한다. 같은 화면에 다른 층이 보이지 않게 하고, 층마다 NavMeshSurface를 따로 굽는다. 계단은 `StairPortal`이다. 경로상 계단에 도착하면 이동 시간만큼 기다린 뒤 다른 층 계단으로 `Warp`한다.
- 구역 저작: `ZoneAuthoring`(구역마다 BoxCollider 여러 개) → 런타임에 `ZoneMap`을 만든다. 같은 방식으로 `PatrolRoute`(방마다 순찰 지점), `SpawnPoint`(유형 태그), `HomeSlot`(작은 물건 제자리), `ToiletStation`, `DischargeArea`, `HellhoundPost`, `PlayerStart`를 둔다.
- 프리팹(`4_Prefab/`): Player, Minion_Goblin/Orc/Skeleton/Slime, Hellhound, Item_HeavyDebris/Trash/Clutter/Stain, Mop, UI 프리팹들
- MonoBehaviour: `PlayerController`(CharacterController + Input System), `PlayerInteractor`, `MinionAgent`, `HellhoundAgent`, `ContaminationView`, `FloorCameraFollow`, `MatchBootstrap`(조립과 Tick 호출)
- Runtime asmdef의 references에 `Unity.AI.Navigation`을 추가한다.

## 2인 협업 구조 (충돌 방지)

### 원칙
Unity에서 병합 충돌은 대부분 **씬, 프리팹, SO, 설정 파일 같은 YAML 에셋**과 **두 사람이 함께 고치는 C# 파일**에서 생긴다. 그래서 아래 세 가지를 지킨다.
1. **레인 분리**: 폴더와 에셋마다 주인을 한 명으로 정한다. 주인이 아닌 사람은 그 파일을 커밋하지 않는다.
2. **계약 먼저**: 두 레인이 만나는 지점(인터페이스, enum, 데이터 구조)은 0단계에서 한 번에 확정해 main에 넣는다. 그 뒤로는 각자 계약 뒤에서 구현만 한다.
3. **공유 파일 사전 생성**: 둘이 동시에 만들면 GUID가 갈리는 것들(폴더 `.meta`, asmdef, Input Actions, 태그·레이어, 패키지)은 0단계에서 미리 만든다.

> 확인 결과: EditorSettings의 직렬화 모드가 Force Text(2)이고, `.gitattributes`에 YAML이 LF로 지정돼 있다. 텍스트 병합이 가능한 상태다.

### 레인 분담 (레이어 기준으로 나눔, 추천)
| 레인 | 담당 | 소유 경로 |
|---|---|---|
| **A: 규칙·시뮬레이션** | 순수 C# 규칙, 테스트, 수치 데이터 | `2_Script/Runtime/{Cleaning, Minion, Proposal, Match, Facility, Data}/`, `2_Script/Test/`, `3_Data/` 전체 |
| **B: 월드·표현** | 씬, 프리팹, 입력, 이동, 카메라, UI, 에디터 도구 | `2_Script/Runtime/{Player, View, UI}/`, `2_Script/Editor/`, `1_Scene/`, `4_Prefab/`, `5_Art/`, `6_Audio/`, `7_Setting/InputSystem_Actions.inputactions`, `ProjectSettings/` |
| **공동(계약)** | 두 레인의 접점 | `2_Script/Runtime/Contract/`. 바꿀 때는 따로 작은 PR을 만들고, 상대가 리뷰한 뒤 바로 머지한다. |
| **문서** | 기획과 진행 기록 | `GDD/Spec/<기능>.md`는 작성자가 주인이다. `Roadmap.md`는 레인별 단락만 고친다. `Undecided.md`와 `DesignIssue.md`는 번호 대역을 나눈다(A: 100번대, B: 200번대. 0단계 기록은 1번대). |

- 이렇게 나누는 이유: 레인 A는 씬과 에셋을 거의 건드리지 않으므로, YAML 충돌이 B 혼자 다루는 파일에서만 생길 수 있다.
- 다른 방안은 기능(세로) 기준 분담이다. 예를 들어 A는 졸개, B는 플레이어와 청결도를 맡는다. 각자 기능을 끝까지 책임질 수 있지만, 같은 씬, 프리팹, Bootstrap을 함께 고치게 돼 충돌 위험이 크다. 그래서 추천하지 않는다.
- 일정 균형: A는 1~7단계(로직 분량이 큼), B는 8~10단계(에셋 분량이 큼)를 맡는다. 한쪽이 먼저 끝나면 **프리팹 단위로 소유권을 넘겨** 돕는다. 예: A가 `ProposalStackView.prefab`과 해당 스크립트를 넘겨받는다. 넘길 때는 `collaboration.md`의 소유 표를 먼저 고친다.

### 계약 (0단계에서 확정, `Runtime/Contract/`)
| 파일 | 방향 | 내용 |
|---|---|---|
| `GameEnums.cs` | 공용 | EZoneId, EFloor, EContaminationType, EWorkerType, EItemState, EMinionActivity, EMatchState |
| `FacilityLayout.cs` | B → A | 씬에서 뽑은 **순수 데이터**: 구역 사각형, 순찰 지점, 생성 지점, 제자리 슬롯, 화장실, 배출 구역, 계단, 시작점. A는 테스트에서 이 데이터를 직접 만들어 쓰므로 씬이 없어도 개발할 수 있다. B는 `FacilityLayoutAuthoring`(View)에서 이 데이터를 만든다. |
| `IMinionBody.cs` | A → B | 두뇌가 몸에 내리는 명령: `MoveTo(Vector3)`, `Stop()`, `bool HasArrived`, `Vector3 Position`, `EFloor Floor`. B의 `MinionAgent`가 구현하고, A는 테스트용 `FakeMinionBody`를 쓴다. |
| `IMatchCommands.cs` | B → A | 플레이어 입력을 규칙으로 넘긴다: `AssignRoom(minionId, zone)`, `ApproveOldestProposal()`, `HoldOldestProposal()`, `DispatchHellhound()`, `TryPickUp(itemId)`, `Drop(pos)`, `TryPlaceAtHome(itemId)`, `TryStartMop(itemId)`, `TryBorrowMop(floor)`, `ReturnMop()`, `BeginTalk(minionId)`, `CancelTalk(minionId)` |
| `IMatchReadModel.cs` | A → B | UI와 연출이 읽는 상태: `RemainingTime`, `GetZoneCleanliness(zone)`, `FacilityCleanliness`, `Minions`(상태 DTO), `ActiveMessages`(제안·통보 DTO), `Items`(위치·상태), 이벤트 `ItemChanged`, `MessageAdded`, `MatchEnded` |
| `IMatchSession.cs`, `MatchSessionFactoryBase.cs` | A → B | `MatchSessionFactoryBase : ScriptableObject`의 `IMatchSession Create(MatchSetup setup)`. `MatchSetup`은 Layout, Seed, PlayerBody, MinionBodies를 담는다. B의 `MatchBootstrap`은 팩토리 SO를 인스펙터로 연결하고 이 함수 하나만 호출한다. 수치 SO는 A의 팩토리가 들고 있으므로, A가 내부를 바꿔도 Bootstrap은 고칠 필요가 없다. |

> 구현 메모(0단계): `IMinionBody.MoveTo(EFloor, Vector3)`는 층이 다르면 **몸(B)이 계단을 거쳐 이동**한다. 그래서 `Facility/FloorRoute.cs`(A)는 만들지 않고, 계단 경로와 계단 이동 시간은 B의 `StairPortal`이 맡는다.

- B는 A의 구현을 기다리지 않는다. `View/Debug/FakeMatchSession`(B 소유)으로 HUD와 플레이어 조작을 먼저 만든다.
- 계약을 바꿔야 하면: 메신저로 먼저 알린다 → `Contract/`만 고치는 PR(`refactor: 계약 …`)을 만든다 → 상대가 리뷰한다 → 머지한다 → 둘 다 main을 받는다. 계약 PR에는 구현 코드를 섞지 않는다.

### 씬과 프리팹 쪼개기 (B 레인 안에서도 충돌을 줄임)
- `1_Scene/Facility.unity`에는 Bootstrap, 카메라, 조명, 층 프리팹 3개, UI 루트 프리팹만 둔다. 씬 자체는 거의 고치지 않는다.
- 내용은 모두 프리팹으로 만든다: `Floor1/2/3.prefab`(각 층의 방, 구역, 순찰, 생성 지점), `HudRoot.prefab` 아래에 패널마다 중첩 프리팹. 프리팹마다 주인을 한 명으로 둘 수 있다.
- 3_Data의 SO 에셋은 A가 주인이다. B가 연출 값(카메라 거리 등)이 필요하면 B 소유의 별도 SO(`ViewSettingsSO`)를 만든다.

### Git 운영
- 브랜치는 Spec 하나에 하나씩 만든다. 예: `feat/cleaning-model`(A), `feat/facility-layout`(B). PR은 1~2일 분량으로 작게 나누고, 상대가 리뷰한 뒤 Squash로 머지한다.
- 하루 한 번 이상 main을 자기 브랜치로 받는다. 계약 PR이 머지되면 바로 받는다.
- **커밋 전 확인**: 자기 소유가 아닌 파일이 변경 목록에 있으면 되돌린다(`git restore <파일>`). Unity는 씬을 열기만 해도 ProjectSettings나 씬을 다시 저장하는 경우가 있어서, 의도하지 않은 변경이 섞이기 쉽다.
- 공유 씬이나 프리팹을 예외적으로 고쳐야 하면 메신저로 "잠금"을 알리고, 끝나면 해제를 알린다.
- 최후 수단으로 각자 로컬 git 설정에 UnityYAMLMerge(Smart Merge)를 mergetool로 등록한다. 경로 예: `<Unity 설치 경로>/Editor/Data/Tools/UnityYAMLMerge.exe merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"`
- 두 사람 모두 Codex를 쓴다. 에이전트가 레인을 넘지 않도록 규칙을 `docs/rules/collaboration.md`에 적고, `AGENTS.md`와 `SKILL.md`의 문서 표에 연결한다. 작업을 시작할 때 "나는 A 레인"처럼 레인을 알려 주면 에이전트가 소유 경로 밖의 파일을 고치지 않는다.

# Implementation Steps
각 단계에서 먼저 `Assets/GDD/Spec/<기능>.md`를 쓰고 승인을 받는다. 그다음 테스트 작성(실패 확인) → 구현 → 테스트 통과 순서로 진행한다. 아래 표기에서 **[A]**는 규칙·시뮬레이션 레인, **[B]**는 월드·표현 레인, **[A+B]**는 함께 하는 작업이다.

## 0단계 [A+B]: 기반과 계약 (PR 1개, 둘이 함께 리뷰하고 머지할 때까지 다른 작업을 시작하지 않음)
0-1. **문서**: `Roadmap.md`를 레인별 단락(A / B / 통합)으로 나누고 1~11단계를 넣는다. 아래 "논리 오류"는 `DesignIssue.md` D1~D8로, "미정"은 `Undecided.md` U1~로 기록한다. `AGENTS.md`의 게임 요약 단락을 채운다.
0-2. **협업 규칙**: `docs/rules/collaboration.md`를 만든다(레인 소유 표, 계약 변경 절차, 번호 대역, 커밋 전 확인). `AGENTS.md`와 `7_Setting/AI/ProjectRule/SKILL.md`의 문서 표에 한 줄씩 추가한다.
0-3. **폴더 미리 만들기**: `Runtime/{Contract, Cleaning, Minion, Proposal, Match, Facility, Data, Player, View, UI}`, `4_Prefab/{Floor, Character, Item, UI}`. 비어 있는 폴더에는 `.gitkeep`을 둔다. 두 사람이 같은 폴더를 따로 만들어 폴더 `.meta` GUID가 갈리는 일을 막는다.
0-4. **공유 설정**: Runtime asmdef에 `Unity.AI.Navigation`을 추가한다. Input Actions에 Drop, Approve, Hold, ToggleMinionPanel을 추가한다. 필요한 태그·레이어(Player, Minion, Item, Interactable)를 등록한다.
0-5. **계약 코드**: `Runtime/Contract/`의 파일 5개(위 "계약" 표). 시그니처와 DTO만 만들고 구현은 넣지 않는다. 컴파일 확인과 계약 스모크 테스트 1개를 넣는다.
0-6. **미결 질문**: Q1~Q8에 답을 받는다. B 레인의 8단계는 Q8 말고는 막히지 않으므로 먼저 시작할 수 있다.

## 1차 병렬 구간 (0단계 이후)
| 순서 | [A] 규칙·시뮬레이션 | [B] 월드·표현 |
|---|---|---|
| 1 | **청결도·오염 모델** (Spec `Cleaning.md`): ContaminationItem, ContaminationRegistry, CleanlinessCalculator, ZoneMap 귀속. 테스트: 0%로 잘림, 운반 중 제외, 다른 구역에 내려놓으면 그 구역으로 귀속, 시설은 총량으로 계산 | **화이트박스 시설** (Spec `FacilityLayout.md`): `Floor1/2/3.prefab`, 구역 BoxCollider, 화장실, 계단, 현관 배출 구역, 순찰 경로, 생성 지점, 제자리 슬롯. 층마다 NavMeshSurface를 굽는다. `FacilityLayoutAuthoring`이 `FacilityLayout`을 만든다. |
| 2 | **데이터와 생성** (Spec `ContaminationSpawn.md`): SO 4종(에셋 포함), ContaminationSpawner, 고정 시드 `IRandom`. 테스트: 구역마다 최소 1개, 예산 범위 안, 같은 시드면 같은 결과 | **플레이어** (Spec `Player.md`): PlayerController(Input System), PlayerInteractor(대상 고르기), 카메라 추적, 운반 모습. `FakeMatchSession`으로 줍기와 내려놓기를 확인한다. |
| 3 | **작업 선택과 능력** (Spec `TaskSelection.md`): WorkerCapability, TaskSelector, 선점(Q7). 테스트: 고블린은 잔해를 건너뜀, 스켈레톤은 운반하지 않음, 선점된 개체는 건너뜀, 우선순위와 거리 | **졸개 몸** (Spec `MinionAgent.md`): `MinionAgent : IMinionBody`(NavMeshAgent), StairPortal Warp, HellhoundAgent. 테스트 씬에서 명령으로 층간 이동을 확인한다. |
| 4 | **도구·배출·소화** (Spec `ToolAndDischarge.md`): MopStationService, DischargeService, 슬라임 소화. 테스트: 그 층에 걸레가 없으면 다른 층, 6개 모두 사용 중이면 실패, 출발 전 묶음만 제거, 소화 중이면 새 작업 없음 | **ContaminationView**: 오염 개체 프리팹 4종, `IMatchReadModel.Items`를 화면에 반영(생성, 운반, 사라짐) |

**통합 M1 [A+B]: "오염이 보이는 맵"**. `MatchFactory`의 1차 구현(A)과 `MatchBootstrap`(B)을 연결한다. 실제 생성된 오염을 플레이어가 줍고, 내려놓고, 닦을 수 있고, 디버그로 구역 청결도가 바뀌는지 확인한다. *의존: A1~A4, B1~B4*

## 2차 병렬 구간
| 순서 | [A] 규칙·시뮬레이션 | [B] 월드·표현 |
|---|---|---|
| 5 | **졸개 두뇌와 기억** (Spec `MinionBrain.md`): MinionBrain, MinionMemory, ProposalCandidateFinder. §5.2 전이마다 테스트(`FakeMinionBody` 사용) | **HUD** (Spec `Hud.md`): MatchTimerView, ZoneHudView, FacilityHudView, MinionStatusPanel. `FakeMatchSession`으로 먼저 만든다. |
| 6 | **제안 대기열과 대사** (Spec `Proposal.md`): ProposalQueue, IProposalDialogueProvider, DefaultProposalDialogueProvider. 테스트: 항목마다 독립된 7초, 가장 오래된 제안부터, 통보는 입력을 막지 않음, 맞지 않는 배정 ID는 버림, 제안은 한 건만 | **대화와 제안 UI** (Spec `AssignmentUi.md`): AssignmentPanel(배정, 취소), ProposalStackView(올라오기, 게이지, 강조) |
| 7 | **판 흐름과 마감** (Spec `MatchFlow.md`): MatchSimulation, MatchResolver. 테스트: §8.2 순서, 판정 예시, 같은 틱 처리 순서 | **마감 연출과 결과** (Spec `ResultUi.md`): 마왕 등장 연출, 강제로 내려놓는 모습, ResultPanel, 재시작 |

**통합 M2 [A+B]: "졸개 배정 플레이"**. 졸개 4마리를 배정하면 자동으로 작업하고, 제안에 승인·보류할 수 있다. *의존: A5, A6, B5, B6*
**통합 M3 [A+B]: "3분 한 판 완주"**. 마감 판정과 결과 화면까지 이어진다. *의존: A7, B7*

## 3차 구간 [A+B]
8. **플레이테스트와 밸런스**: A는 §11의 수치를 SO에 채우고, B는 조작감과 연출을 조정한다. §11의 세 가지 질문(3분 안에 달성할 수 있는지, 분업과 집중 중 어느 쪽이 유리한지, 현관 타이밍이 실제 선택을 만드는지)을 함께 확인한다. *의존: M3*

> 일정 메모: B의 1~2단계는 Q1~Q7과 상관없으므로 0단계 직후 바로 시작할 수 있다. A의 3단계는 Q1, A의 5단계는 Q5·Q6 답이 있어야 시작할 수 있다.

# Verification & Testing
- **EditMode 테스트** (`2_Script/Test/<클래스>Test.cs`, 메서드 이름은 한국어): §10 시나리오 10개를 각각 하나 이상의 테스트로 옮긴다. 예: `작업실에서_든_잔해를_2층_로비에_내려놓으면_2층_로비_오염이_된다`, `현관_밖_1층_로비_바닥의_폐기물은_배출되지_않는다`, `시설이_90이어도_3층_로비가_59이면_실패한다`, `뒤에_있는_제안도_표시_시점부터_7초가_지나면_만료된다`, `소화_중인_슬라임은_새_방에_도착해도_작업을_시작하지_않는다`
- **경계 사례**: 시작 오염 1개짜리 구역, 남은 시간 0과 작업 완료가 같은 틱에 일어남, 응답 대기 중 재배정, 헬하운드가 출발한 직후 적치, 6개 걸레가 모두 쓰이는 중에 반납
- **수동 확인**: 플레이 모드에서 졸개 4마리를 배정하고 3분을 끝까지 진행한다. 콘솔 에러 0, `@UnityGameJam` 경고 0. 층을 옮길 때 다른 층이 보이지 않는지, 원격으로 구역 수치가 노출되지 않는지 확인한다.
- 테스트는 TestRunnerApi로 `UnityGameJam.Test` EditMode를 실행해서 확인한다.
- **협업 확인 (PR마다)**: 변경 파일이 작성자 레인의 소유 경로 안에만 있는지 본다. `Contract/`가 바뀌었으면 계약 전용 PR인지 본다. 새 파일의 `.meta`가 들어 있는지 본다. main을 받은 뒤에도 컴파일 에러가 없고 EditMode 테스트가 모두 통과하는지 확인한다.
- **통합 시점(M1~M3) 확인**: 두 사람이 각자 main을 새로 받아 같은 씬을 플레이했을 때 Missing 참조가 없고, 결과가 같은지 확인한다.

---

# 기획 검토 결과

## A. 논리 오류와 모순 (→ DesignIssue.md)
| 번호 | 위치 | 문제 | 제안 |
|---|---|---|---|
| D1 | §4, §5.1-3, §7.1 | 고블린과 오크가 "일반 작업도 수행"한다고 했지만 물걸레질이 일반 작업에 들어가는지 알 수 없다. §7.1은 졸개 모두가 걸레를 가져오는 것처럼 읽히고, §4는 스켈레톤만 걸레질에 능숙하다고 한다. | 능력 표를 명시한다(Q1). |
| D2 | §6 인지 범위 vs §6 대사 예시, §7.1-1·2 | 졸개가 아는 정보는 세 가지뿐인데, "걸레가 전부 사용 중"이라고 말하거나 다른 층 화장실을 고르려면 **전체 물걸레 현황**을 알아야 한다. | 인지 범위에 물걸레 현황을 추가한다. |
| D3 | §7.2, §8.2-3 | 작은 물건을 운반하는 동안 집계에 포함하는지, 재배정 때 내려놓는지, 마감 때 어느 구역의 오염으로 남는지 정해져 있지 않다. 폐기물 표만 있다. | 작은 물건도 폐기물처럼 처리한다(운반 중 제외, 마감 때 현재 위치에 귀속). 다른 방안: 운반 중에도 원래 구역에 포함. |
| D4 | §6 후보 vs §5.2·§7.1-2 "이동 후보 없음" | "근처 방"과 "다른 졸개가 일하는 방"은 거의 항상 있으므로, "후보 없음 → 복귀 통보" 분기가 사실상 일어나지 않는다. | 후보에서 뺄 조건(기억상 깨끗한 방, 자기 능력으로 할 일이 없다고 본 방 등)과 "근처"의 정의를 정한다. |
| D5 | §9 중앙 상단 | 180초 게임인데 시·분·초를 두 자리씩 표시하면 시간 칸이 항상 00이다. | 분:초(03:00) 형식을 권장한다. |
| D6 | §4 고블린 "가벼운 물건" vs §3.1 "어질러진 작은 물건" | 용어가 다르다. 가벼운 물건에 일반 쓰레기도 들어가는지 분명하지 않다. GDD 이름을 코드에 그대로 써야 하므로 통일이 필요하다. | "어질러진 작은 물건"으로 통일한다. |
| D7 | §2 "화장실은 판정 제외" + §3.1 "내려놓은 구역의 오염" | 화장실이나 계단에 폐기물을 버리면 어느 구역에도 속하지 않아 **시설 오염에서 사라진다**(악용 가능). 마감 때 계단 위에서 강제로 내려놓는 경우도 같은 문제다. | 판정 구역 밖에 떨어진 물건은 그 층 로비에 귀속한다(§11 상태 경계 항목을 확정). |
| D8 | §5.2, §6 "응답 대기 중 멈춤" + §7.1-5 | 걸레를 든 졸개가 제안 대기 7초 동안이나 로비로 복귀하는 동안 걸레를 계속 들고 있으면 6개 걸레가 묶인다. 반납 시점이 정해져 있지 않다. | 할 일이 없어지면 **먼저 반납한 뒤 제안**하는 방식을 권장한다(Q5). |

## B. 구현 전에 꼭 필요한 정보 (→ **모두 결정됨**, 결과는 `Undecided.md` U1~U8)
> Q1: 고블린과 오크도 물걸레질을 한다(오크는 느림). Q2: 9개 구역 모두 배정할 수 있다. Q3: 개체마다 제자리가 있다. Q4: 기준값 60%를 표시한다. Q5: 반납한 뒤 제안·이동한다(플레이어는 화장실에서만 반납). Q6: 할 일 기억 → 미확인 → 동료 위치. Q7: 플레이어가 가로챌 수 있다. Q8: 그 층 로비에 귀속한다.

- **Q1** 종별 능력 표: 고블린과 오크도 물걸레질을 하는가? 플레이어는 모두 가능하다.
- **Q2** 배정 목록에 **로비 3곳**이 들어가는가? 빠지면 로비 3곳은 플레이어 혼자 60%를 맞춰야 한다.
- **Q3** 작은 물건의 "지정된 자리"란? (A) 개체마다 정해진 제자리, (B) 방마다 정리함 하나. 운반 중 집계는 D3에서 정한다.
- **Q4** 좌측 상단 HUD의 "**최저치**"는 무엇인가? 기준값(60%)인가, 이번 판에서 가장 낮았던 값인가?
- **Q5** 걸레: 플레이어는 걸레를 어떻게 반납하는가? 바닥에 내려놓을 수 있는가? 걸레를 든 졸개를 재배정하면 반납부터 하는가, 들고 가는가?
- **Q6** 제안 후보 규칙(D4)과 우선순위. 권장 순서: 기억상 할 일이 있는 방 → 미확인 방 탐색 → 다른 졸개가 일하는 방
- **Q7** 선점: 졸개가 잡아 둔(이동 중인) 개체를 플레이어가 가로챌 수 있는가? 권장: 잡아 두기는 졸개끼리만 적용하고, 실제로 작업 중인 개체만 잠근다.
- **Q8** 판정 구역 밖(화장실, 계단)에 떨어진 물건의 귀속(D7).

## C. 진행하면서 정해도 되는 것 (→ Undecided.md, 임시 값으로 진행)
- **Q9** 배정 대화를 취소하면 하던 일을 이어 하는가, 처음부터 다시 판단하는가?
- **Q10** 배정 패널이 열려 있을 때 다른 졸개의 제안에 승인·보류를 입력할 수 있는가? 키 배치를 확정한다.
- **Q11** 타이틀, 결과 화면, 재시작, 일시정지가 있는가? (GDD에 없음)
- **Q12** 같은 층의 들어가지 않은 방도 화면에 보이는가(오염이 눈으로 보임), 아니면 시야를 가리는가?
- **Q13** 졸개가 지나가며 본 방(로비 등)도 기억하는가? 임시: 배정받아 들어간 방만 기억한다.
- **Q14** 같은 프레임에 일이 겹칠 때 처리 순서. 임시: 작업 완료 → 제안 만료 → 마감
- **Q15** 헬하운드가 배출하러 나가 있을 때 명령하면 무시하는가, 예약하는가? 배출 구역이 비어 있을 때 명령하면 어떻게 되는가?
- **Q16** 참고 게임과 아트 방향, 마왕 등장 연출
- **밸런스(§11)**: 생성 예산, 속도와 시간 값, 순찰 경로, 계단 시간. 모두 SO 기본값으로 시작하고 11단계에서 조정한다.
- **밸런스 메모**: 시작 오염이 적은 구역(예: 잔해 1개, 가중치 4)은 0% 아니면 100%만 나온다. 60% 기준이 사실상 100%가 되므로, 구역마다 최소 가중치를 두는 것을 권장한다.
