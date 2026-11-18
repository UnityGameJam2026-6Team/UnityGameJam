# B 레인 실행 계획 (심지섭): 계약 보완 → B1~B7 → 통합 M1~M3

> 상위 계획: `Assets/Plans/demon-king-cleaning-implementation.md`(전체), `Assets/Plans/team-division-prefab-art-pipeline.md`(분업·아트, B0 완료)
> 이 계획은 B 레인의 남은 일을 **명령 단위**(Unity AI에게 주는 프롬프트 하나)와 **구현 단위**(커밋 하나)로 쪼갠다. A 레인(김보경)은 의존 관계만 표시한다.
> 결정 사항:
> - 플레이어가 무거운 잔해를 들면 이동 속도는 기본의 50%(배율은 A의 SO)
> - 작업 진행도는 0~100(%)
> - 구역 표시 이름은 GDD 이름 그대로 쓰는 공용 클래스로 둔다
> - 상호작용 거리는 B의 `ViewSettingsSO`에 둔다
> - 평면도 치수는 B1 Spec에서 내가 제안하고 승인받는다

# Project Overview
- Game Title: 마왕 도착 전 시설 청소 (가제)
- High-Level Concept: 180초 안에 졸개 5종을 지휘하고 직접 청소해서 9개 구역 60%, 시설 85%를 달성하는 3D 탑다운 지휘 게임
- Players: 싱글 플레이
- Inspiration / Reference Games: 미정 (U17)
- Tone / Art Direction: 화이트박스. 모델은 아트 파이프라인(B0)으로 나중에 교체한다.
- Target Platform: PC (Windows 64bit)
- Screen Orientation / Resolution: 가로 1920x1080 (U18)
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
배정(1층 로비에서 졸개와 대화) → 위임과 직접 청소(정찰, 줍기, 닦기, 헬하운드 배출) → 이동 제안에 승인·보류(7초) → 180초 마감 판정. B 레인은 이 루프에서 **보이는 것과 만지는 것**을 맡는다: 공간, 이동, 입력, 표시, 연출.

## Controls and Input Methods
New Input System, Project-wide `InputSystem_Actions` (B 소유)
| 액션 | 키 | 비고 |
|---|---|---|
| Player/Move | WASD, 왼쪽 스틱 | 속도는 `IMatchReadModel.PlayerMoveSpeed`를 매 프레임 적용 |
| Player/Interact | E | **지금은 Hold 인터랙션이 걸려 있다.** B2에서 Press로 바꾼다 |
| Player/Drop | Q | 들고 있는 물건 내려놓기 |
| Player/Approve, Hold | F, R | 가장 오래된 활성 제안 |
| Player/ToggleMinionPanel | Tab | 졸개 현황 패널 |
| UI/Navigate, Submit, Cancel | 방향키, Enter, Esc | 배정 패널이 열리면 Player 맵을 끄고 UI 맵만 쓴다(U10 임시) |

# UI
HUD 5영역의 배치는 상위 계획의 와이어프레임을 따른다. B6과 B7에서 새로 만드는 화면은 다음과 같다.

**배정 패널 `UI_AssignmentPanel`** (졸개와 대화할 때. 그 졸개만 멈추고, 타이머는 계속 흐른다)
```
┌ 오크에게 맡길 곳 ───────────────┐
│ 현재: 창고 (작업 중)              │
│ ── 3층 ──                        │
│   간부실        3층 로비          │
│ ── 2층 ──                        │
│   회의실  작업실  2층 로비        │
│ ── 1층 ──                        │
│   다목적실 휴게실 창고 1층 로비    │
│              [Enter] 배정 [Esc] 취소 │
└──────────────────────────────────┘
```
- 버튼 문구는 `ZoneDisplayName.GetName`, 제목은 졸개 이름. 구역 수치는 보여 주지 않는다(GDD §2).

**결과 패널 `UI_ResultPanel`** (마왕 등장 연출 뒤)
```
┌ 마왕 도착 ─────────────── 실패 ┐
│ 시설 전체   82% / 85%   ✖       │
│ 1층 로비    71%  ✔  다목적실 64% ✔ │
│ 휴게실      58%  ✖  창고    90% ✔ │
│ …(9개 구역)                       │
│                  [R] 다시 하기    │
└──────────────────────────────────┘
```

# Key Asset & Context

## C. 계약 보완 (이번에 먼저 할 일)
**`Runtime/Contract/IMatchReadModel.cs`에 추가할 멤버**
```csharp
float PlayerMoveSpeed { get; }      // m/s. 규칙이 계산하고, 무거운 잔해를 들면 기본의 50%(배율은 WorkerStatsSO)
bool IsPlayerWorking { get; }       // 물걸레질처럼 시간이 걸리는 작업 중인지
float PlayerWorkProgress { get; }   // 0~100(%). 작업 중이 아니면 0
```
**새 파일 `Runtime/Contract/ZoneDisplayName.cs`** (공용 정적 클래스)
- `GetName(EZoneId)`: "다목적실", "1층 로비"
- `GetFloor(EZoneId)`, `IsLobby(EZoneId)`, `GetFloorLabel(EFloor)`: "1층"
- `GetFullName(EZoneId)`: 일반 방은 "1층 다목적실", 로비는 "2층 로비"

**함께 바꾸는 공동 문서**
- `Undecided.md`에 결정 두 줄: U20(플레이어 운반 감속 50%), U21(작업 진행도 0~100%)
- `collaboration.md`:
  - 계약 표에 `ZoneDisplayName`을 추가한다.
  - 소유 표에 `3_Data/ScriptableObject/View/`를 B 소유로 추가한다. `ViewSettingsSO` 같은 연출용 SO 에셋을 둘 곳이고, 지금 `3_Data/`는 A 소유라서 필요하다.
- `ContractSmokeTest.cs`(Test 루트, A 소유): 계약 PR이라 김보경 리뷰를 전제로 테스트 3개를 추가한다.

## B1 평면도 치수 초안 (B1 Spec에서 확정, U204)
1m 격자로 잡는다. 층마다 바깥 크기는 36x28m(X −18~18, Z −14~14)이다. 층끼리는 X축으로 100m씩 떨어뜨린다(1층 X=0, 2층 X=100, 3층 X=200). 계단은 모든 층에서 **오른쪽 위(X 14~18, Z 8~14)**에 둔다.
| 층 | 구역·시설 | 범위 (X, Z) | GDD §2 근거 |
|---|---|---|---|
| 1층 | 다목적실 | −12~12, 4~14 | 위쪽 큰 방 |
| 1층 | 휴게실 | −18~−8, −14~−4 | 왼쪽 아래 |
| 1층 | 화장실 | −18~−12, −4~2 | 왼쪽 아래 (휴게실 위) |
| 1층 | 창고 (오른쪽 곡선 벽) | 10~18, −8~4 | 오른쪽 곡선 방 |
| 1층 | 현관 + 배출 구역 / 헬하운드 대기점 | 현관 −3~3, −14~−11 / 배출 구역 3~6, −14~−11 / 대기점 (−4, −12) | 아래 가운데 현관 |
| 1층 | 1층 로비 (복도 포함) | 나머지 바닥. 플레이어와 졸개 시작점은 중앙 (0, −2) 근처 | 중앙 로비 |
| 2층 | 회의실 | −18~−6, −14~−2 | 왼쪽 아래 |
| 2층 | 화장실 | −18~−12, −2~4 | 회의실 위쪽 |
| 2층 | 작업실 | 6~18, −14~−2 | 오른쪽 아래 |
| 2층 | 2층 로비 | 나머지 | 중앙 공간과 복도 |
| 3층 | 간부실 (곡선 벽) | −14~14, 0~14 | 위쪽 큰 곡선 방 |
| 3층 | 3층 로비 / 화장실 | 로비 −18~18, −14~0 중 화장실 제외 / 화장실 −18~−12, −14~−8 | 아래쪽 로비와 화장실 |
- 작은 물건 목록 초안(U202): Book, Cup, Document, Chair. 각각 `IT_Clutter_<물건>`과 `PR_HomeSlot_<물건>`으로 짝을 짓는다.

## 새로 만들 파일 (B 소유)
| 위치 | 파일 | 단계 |
|---|---|---|
| `Runtime/View/Authoring/` | `ZoneVolumeAuthoring`, `SpawnPointAuthoring`, `PatrolRouteAuthoring`, `HomeSlotAuthoring`, `StartPointAuthoring`, `ToiletAuthoring`, `DischargeAreaAuthoring`, `FacilityLayoutAuthoring` | B1 |
| `Runtime/View/` | `StairPortal`, `StairRoutePlanner`(순수), `MinionAgent`, `HellhoundAgent`, `CharacterAnimatorDriver`, `ContaminationView`, `MatchBootstrap`, `ViewSettingsSO` | B1~B4 |
| `Runtime/View/Debug/` | `FakeMatchSession`, `FakeMatchSessionFactorySO` | B2 |
| `Runtime/Player/` | `PlayerController`, `PlayerInteractor`, `PlayerBody : IWorkerBody`, `FloorCameraFollow` | B2 |
| `Runtime/UI/` | `MatchTimerView`, `ZoneHudView`, `FacilityHudView`, `MinionStatusPanel`, `AssignmentPanel`, `ProposalStackView`, `ResultPanel`, `DemonKingArrivalView` | B5~B7 |
| `Test/View/` | `FacilityLayoutAuthoringTest`, `StairRoutePlannerTest`, `FakeMatchSessionTest` | B1~B3 |
| `4_Prefab/` | `MK_*`, `ENV_*`, `PR_*`, `FL_Floor1~3`, `CH_Player`, `CH_MinionBase`+Variant 5, `CH_DemonKing`, `IT_*`, `UI_*`, `SYS_*` | B1~B7 |
| `1_Scene/` | `Facility.unity` (빌드 목록 첫 번째) | B1 |
| `3_Data/ScriptableObject/View/` | `ViewSettings.asset`, `FakeMatchSessionFactory.asset` | B2 |

# Implementation Steps
읽는 법:
- **단위**는 커밋 하나다.
- **명령**은 Unity AI에게 그대로 주면 되는 요지다. 첫 줄 "나는 심지섭, B 레인"과 규칙 문서 읽기는 `docs/team/lane-b.md`를 따른다.
- Spec 단위에서는 Spec을 작성해 승인받기 전에 구현하지 않는다.
- 단위마다 공통 확인: 컴파일 에러 0, `@UnityGameJam` 경고 0, EditMode 테스트 전부 통과, `.meta` 확인, 소유 경로 확인.

## C. 계약 보완 PR · 브랜치 `refactor/contract-player-zone-name` · *의존: 0단계와 B0가 main에 커밋돼 있음*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| C-1 | "계약에 플레이어 속도·작업 진행도를 추가하고 ZoneDisplayName을 만들어줘" | `IMatchReadModel.cs`(+3 멤버), `ZoneDisplayName.cs` | 컴파일 | - |
| C-2 | "계약 테스트를 추가해줘" | `ContractSmokeTest`에 `모든_구역에_표시_이름이_있다`, `일반_방은_층과_이름을_함께_표시한다`, `로비는_층을_중복해서_붙이지_않는다` | 테스트 통과 | - |
| C-3 | "Undecided U20·U21, collaboration 계약 표와 View SO 소유를 갱신해줘" | `Undecided.md`, `collaboration.md`, `3_Data/ScriptableObject/View/` 폴더 | 문서 확인 | `refactor: 계약에 플레이어 속도·작업 진행도와 구역 표시 이름 추가` |
| C-4 | (사람) 김보경에게 알리고 PR 리뷰 → 머지 → 둘 다 main 받기 | - | 김보경 승인 | - |

## B1. 화이트박스 시설 · `feat/facility-layout` · *의존: C*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B1-1 | "B1 Spec을 써줘. 평면도 치수안과 작은 물건 목록을 포함해" | `Spec/FacilityLayout.md`(치수표, 마커 목록, 규칙, 테스트 시나리오), Undecided U204·U202 갱신 | **사람 승인** | `docs: B1 화이트박스 시설 스펙` |
| B1-2 | "저작용 Authoring 스크립트와 MK_ 마커 프리팹을 만들어줘" | Authoring 7종, `MK_ZoneVolume`, `MK_SpawnPoint`, `MK_PatrolPoint`, `MK_HomeSlot`, `MK_StartPoint`, `MK_Toilet`, `MK_DischargeArea`, `MK_Stair` (Gizmo 표시) | 프리팹 검사 | `feat: 시설 저작용 마커 프리팹` |
| B1-3 | "ENV_ 환경 조각 프리팹을 표준 구조로 만들어줘" | `ENV_FloorTile`, `ENV_Wall_Straight/Corner/Curve`, `ENV_Door`, `ENV_Stairs`, `ENV_Entrance`, `ENV_Toilet` (Visual/ArtSlot/Placeholder, 콜라이더는 루트) | Art 검사에서 "모델 없음" 8개 | `feat: 환경 조각 화이트박스 프리팹` |
| B1-4 | "PR_ 소품 프리팹을 만들어줘" | `PR_MopRack`, `PR_DischargeBin`, `PR_HomeSlot_{Book,Cup,Document,Chair}`, 가구 `PR_Desk`, `PR_Shelf`, `PR_Sofa`, `PR_Table` | Art 검사 | `feat: 소품 화이트박스 프리팹` |
| B1-5 | "치수표대로 FL_Floor1~3을 조립해줘" | `FL_Floor1~3`(ENV/PR/MK 인스턴스만 중첩) | 층별 스크린샷, 구역 볼륨이 겹치지 않음 | `feat: 1~3층 화이트박스 조립` |
| B1-6 | "층마다 NavMeshSurface를 굽고 연결을 확인해줘" | `FL_`마다 NavMeshSurface, 구운 데이터 `4_Prefab/Floor/NavMesh/` | 모든 방에 길이 이어지는지 경로 샘플링 | `feat: 층별 NavMesh 베이크` |
| B1-7 | "FacilityLayoutAuthoring과 테스트를 만들어줘" | `FacilityLayoutAuthoring`(씬 마커 → `FacilityLayout`), `FacilityLayoutAuthoringTest`(구역 9개, 화장실 3개, 배출 구역 1개, 시작점 6개, 순찰 경로 9개, 구역 겹침 없음) | 테스트 통과 | `feat: 시설 배치 데이터 추출` |
| B1-8 | "SYS_ 프리팹과 Facility 씬을 만들고 빌드 목록에 넣어줘" | `SYS_MainCamera`, `SYS_DirectionalLight`, `SYS_GlobalVolume`, `Facility.unity`(SYS_ + FL_만), 빌드 씬 첫 번째 | 씬 규칙 검사 통과, 플레이 시 에러 0 | `feat: Facility 씬 구성` |

## B2. 플레이어 · `feat/player` · *의존: B1*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B2-1 | "B2 Spec을 써줘" | `Spec/Player.md`(이동, 대상 고르기 우선순위, 상호작용 표, 카메라) | **사람 승인** | `docs: B2 플레이어 스펙` |
| B2-2 | "ViewSettingsSO와 에셋을 만들어줘" | `ViewSettingsSO`(상호작용 거리, 카메라 높이·각도·추적 속도, 계단 이동 시간 임시값), `3_Data/ScriptableObject/View/ViewSettings.asset` | 에셋 생성 | - |
| B2-3 | "Interact를 Press로 바꿔줘" | Input Actions의 Interact에서 Hold 제거 | 탭으로 반응 | `setting: Interact 입력을 Press로 변경` |
| B2-4 | "FakeMatchSession을 만들어줘" | `FakeMatchSession : IMatchSession`(아이템 몇 개, 줍기·내려놓기·닦기 진행도 0→100, 운반 시 속도 50%), 팩토리 SO와 에셋, `FakeMatchSessionTest` | 테스트 통과 | `feat: 가짜 판 세션` |
| B2-5 | "CH_Player와 PlayerController, PlayerBody를 만들어줘" | `CH_Player`(CharacterController, 표준 구조 1.7m), Move 입력 → `PlayerMoveSpeed` 적용, 층 판별 | 이동, 운반 시 절반 속도 | - |
| B2-6 | "PlayerInteractor를 만들어줘" | 반경 안에서 가장 가까운 대상 강조, E → TryPickUp / TryPlaceAtHome / TryStartMop / BeginTalk / DispatchHellhound / TryBorrowMop·TryReturnMop, Q → TryDrop, 손 위치(CarryAnchor) | 가짜 세션으로 줍기·놓기·닦기 | - |
| B2-7 | "카메라와 Bootstrap을 붙여줘" | `FloorCameraFollow`(현재 층만 보이게), `SYS_Bootstrap`(`MatchBootstrap`: 팩토리 SO → Create → 매 프레임 Tick) | 플레이 확인 | `feat: 플레이어 이동·상호작용과 카메라` |

## B3. 졸개 몸 · `feat/minion-agent` · *의존: B1, B2-4*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B3-1 | "B3 Spec을 써줘. 애니메이터 상태도 정해" | `Spec/MinionAgent.md`, U201 결정(파라미터 Speed, Activity, IsCarrying 등) | **사람 승인** | `docs: B3 졸개 몸 스펙` |
| B3-2 | "StairRoutePlanner와 테스트를 먼저 만들어줘" | 순수 클래스: (현재 층, 목표 층) → 구간 목록. `StairRoutePlannerTest` | 테스트 통과 | - |
| B3-3 | "MinionAgent와 StairPortal을 만들어줘" | `MinionAgent : IMinionBody`(NavMeshAgent, 다른 층이면 계단 → 대기 → Warp), `StairPortal` | 디버그 명령으로 1층→3층 이동 | - |
| B3-4 | "CH_MinionBase와 Variant 5종을 만들어줘" | `CH_MinionBase`, `CH_Goblin/Orc/Skeleton/Slime/Hellhound`(권장 크기, 색 구분) | Art 검사 "베이스 1, 모델 없음 5" | - |
| B3-5 | "HellhoundAgent와 애니메이터 드라이버를 만들어줘" | `HellhoundAgent`(Dispatching이면 현관 밖 왕복), `CharacterAnimatorDriver`(MinionStatus → AC_Character), `AC_Character` 전이 | 가짜 세션으로 배출 왕복 | `feat: 졸개 이동과 계단 이동` |

## B4. ContaminationView · `feat/contamination-view` · *의존: B2-4, B3*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B4-1 | "B4 Spec을 써줘" | `Spec/ContaminationView.md`(상태별 표시: 바닥, 운반, 배출 구역, 소화, 사라짐) | **사람 승인** | `docs: B4 오염 표시 스펙` |
| B4-2 | "IT_ 프리팹을 만들어줘" | `IT_HeavyDebris_01`, `IT_Trash_01`, `IT_Clutter_{Book,Cup,Document,Chair}`, `IT_FloorStain_01`(텍스처 전용 쿼드), `IT_Mop` | Art 검사 | - |
| B4-3 | "ContaminationView를 만들어줘" | `ItemChanged` 구독 → ItemId별 생성, 이동, 숨김(풀링), 운반 중이면 운반자 손에 붙이기, 유형→프리팹 표는 ViewSettingsSO | 가짜 세션으로 상태 전환 확인 | `feat: 오염 개체 표시` |

## M1. 통합: 오염이 보이는 맵 [A+B] · *의존: A1~A4, B1~B4*
| 단위 | 명령 | 산출물 | 검증 |
|---|---|---|---|
| M1-1 | (김보경) 실제 `MatchSessionFactorySO` 제공 | A의 팩토리 에셋 | - |
| M1-2 | "SYS_Bootstrap의 팩토리를 실제 것으로 바꾸고 통합 점검해줘" | Bootstrap 연결 변경 | 생성된 오염이 보임, 줍기·내려놓기·닦기로 구역 청결도가 바뀜(디버그 로그), 운반 감속 50% |

## B5. HUD · `feat/hud` · *의존: B2 (가짜 세션), D5 결정*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B5-1 | "B5 Spec을 써줘" | `Spec/Hud.md`, D5(분:초 표시) 결정 반영 | **사람 승인** | `docs: B5 HUD 스펙` |
| B5-2 | "TMP 기본 리소스와 한글 폰트를 준비해줘" | TMP Essential Resources, 한글 SDF 폰트 에셋(`5_Art/Font/`) | 한글 표시 | `setting: TMP와 한글 폰트` |
| B5-3 | "HUD 프리팹을 만들어줘" | `UI_HudRoot` + `UI_MatchTimer`, `UI_ZoneHud`(현재 구역만, 미달 표시, 기준 60%), `UI_FacilityHud`, `UI_MinionStatusPanel`(Tab으로 접기), 작업 진행 게이지(`PlayerWorkProgress`) | 1920x1080 스크린샷, 원격 구역 수치가 보이지 않음 | `feat: HUD` |

## B6. 대화와 제안 UI · `feat/assignment-ui` · *의존: B5*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B6-1 | "B6 Spec을 써줘" | `Spec/AssignmentUi.md`(입력 맵 전환, U9·U10 임시 처리) | **사람 승인** | `docs: B6 배정·제안 UI 스펙` |
| B6-2 | "배정 패널을 만들어줘" | `UI_AssignmentPanel`(BeginTalk → 9개 구역 버튼 → AssignRoom / Esc → CancelTalk) | 가짜 세션으로 배정 | - |
| B6-3 | "제안 스택을 만들어줘" | `UI_ProposalStack`(아래에서 올라옴, 7초 게이지, 입력 대상 강조, 통보는 회색, F/R → Approve/Hold) | 여러 제안이 동시에 올 때 | `feat: 배정 패널과 제안 스택` |

## B7. 마감 연출과 결과 · `feat/result-ui` · *의존: B6*
| 단위 | 명령 | 산출물 | 검증 | 커밋 |
|---|---|---|---|---|
| B7-1 | "B7 Spec을 써줘" | `Spec/ResultUi.md`(U12: 결과와 재시작만) | **사람 승인** | `docs: B7 마감·결과 스펙` |
| B7-2 | "마왕 등장 연출과 결과 패널을 만들어줘" | `CH_DemonKing`, `DemonKingArrivalView`(DemonKingArrived이면 입력 잠금, 등장 연출), `UI_ResultPanel`(9개 구역과 시설, 성공·실패), R → 씬 다시 불러오기 | 가짜 세션으로 성공·실패 두 경우 | `feat: 마왕 등장과 결과 화면` |

## M2·M3. 통합 [A+B]
- **M2 졸개 배정 플레이** (A5·A6, B5·B6): 졸개 4마리를 배정하면 자동으로 작업한다. 제안이 오고 승인·보류가 동작하며, 졸개 현황 패널이 상태를 보여 준다.
- **M3 3분 한 판 완주** (A7, B7): 180초 → 강제로 내려놓기 → 판정 → 결과 화면 → 재시작
- 둘 다 이 순서로 진행한다: 각자 main을 받는다 → 같은 씬을 플레이한다 → 체크리스트를 확인한다 → Roadmap 통합 단락을 갱신한다.

## 일정 메모
- C는 김보경이 A1 구현을 시작하기 **전에** 머지해야 한다.
- B1~B4는 A를 기다리지 않는다. 가짜 세션으로 진행한다. M1만 A1~A4 완료가 필요하다.
- 진행 상황은 `Roadmap.md`의 B 레인 단락 체크박스로 갱신한다.

# Verification & Testing
- **EditMode** (`Test/View/`):
  - `FacilityLayoutAuthoringTest`: `씬_마커에서_판정_구역_9개를_추출한다`, `구역_볼륨끼리_겹치지_않는다`, `작업자_6종의_시작점이_모두_있다`
  - `StairRoutePlannerTest`: `같은_층이면_계단을_거치지_않는다`, `1층에서_3층은_계단을_두_번_지난다`
  - `FakeMatchSessionTest`: `무거운_잔해를_들면_이동_속도가_절반이_된다`, `닦기를_시작하면_진행도가_0에서_100으로_오른다`
  - 계약: `ContractSmokeTest`에 추가하는 세 테스트
- **씬 규칙**: `Tools > UnityGameJam > Art > 검사`에서 씬 규칙 위반 0, 프리팹 이름 오류 0
- **수동 플레이** (단계마다): 콘솔 에러 0, 층을 옮길 때 다른 층이 보이지 않음, 원격 구역 수치가 노출되지 않음, 1920x1080 스크린샷 확인
- **통합 M1~M3**: 두 사람이 main을 새로 받아 같은 결과가 나오는지 확인
