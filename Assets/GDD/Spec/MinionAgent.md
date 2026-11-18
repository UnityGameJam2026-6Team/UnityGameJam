# 졸개 몸 (B3) 스펙

- 상태: **확정** (사용자 요청으로 작성과 구현을 함께 진행. GDD에 없는 값은 U201·U208 결정/임시값)
- 담당: B 레인 · 심지섭
- 근거: GDD §2(계단으로 층 이동, 졸개는 1층 로비 대기, 헬하운드는 현관 대기), §4(졸개 5종), §5(이동·현장 확인·작업), §7.2(헬하운드 배출 왕복), §9(현재 층만 표시) / 계약 `IMinionBody`, `MinionStatus` / Undecided U201(애니메이터 상태), U19(계단 시간)
- 범위: 이번에 하는 것은 졸개 몸(NavMeshAgent)이 계약 `IMinionBody` 명령대로 층을 넘나들며 이동하는 것, 졸개 프리팹 6종(베이스 + Variant 5), 상태에 따른 자세(애니메이터)와 헬하운드 숨김, 플레이어 계단 이동, 가짜 판의 졸개 이동 흉내, 디버그 배정 버튼이다. 하지 않는 것은 졸개의 판단(A5 MinionBrain), 배정 패널(B6), 졸개 위 말풍선(B6)이다.
- 네임스페이스: `UnityGameJam.View`, `UnityGameJam.Player`

## 목적
규칙(A 레인)의 졸개 두뇌가 "어디로 가라"고만 명령하면 몸(B 레인)이 계단을 포함해 길을 찾아 이동하고, 상태에 맞는 자세를 보여 주게 한다.

## 입력 / 출력
| 구분 | 이름 | 설명 |
|---|---|---|
| 입력 | `IMinionBody.MoveTo(층, 위치)`, `Stop()`, `SetMoveSpeed(속도)` | 규칙이 내리는 명령 |
| 입력 | `IMatchReadModel.Minions`(MinionStatus), `Items` | 자세 결정, 헬하운드 숨김 |
| 입력 | `Player/Previous`(1), `Player/Next`(2) | 플레이어 계단 이동(아래층·위층) |
| 출력 | `IMinionBody.Position`, `Floor`, `HasArrived` | 규칙이 읽는 몸 상태 |
| 출력 | Animator `Pose`(int) | 모델이 들어오면 자세 클립 재생 |

## 규칙
1. 같은 층으로 이동하면 NavMeshAgent로 바로 간다.
2. 다른 층으로 이동하면 현재 층 계단 → (계단 시간만큼 대기 후 다음 층 계단으로 이동)을 층 수만큼 반복 → 목적지로 간다. 계단 시간은 `ViewSettingsSO.StairTransitSeconds`(임시 2초, U19)다.
3. 지나갈 층 목록은 출발 층과 도착 층 사이의 층을 순서대로 담는다(1층→3층이면 2층, 3층).
4. `HasArrived`는 계단 이동 중이 아니고, 경로를 다 갔고, 남은 거리가 멈춤 거리 이내일 때 참이다. 처음 생성됐을 때와 `Stop()` 뒤에도 참이다.
5. `Floor`는 계단을 건너는 순간 바뀐다.
6. **자세(U201 결정)**: 애니메이터 int 파라미터 `Pose` 하나로 고른다. 값은 클립 목록 순서(Idle 0, Walk 1, Carry 2, CarryHeavy 3, Mop 4, Eat 5, Talk 6, Dispatch 7)다. 우선순위는 다음과 같다.
   1. 대화 중이거나 응답 대기 중이고 멈춰 있음 → Talk
   2. 슬라임이 소화 중(운반자가 자기인 Digesting 개체가 있음) → Eat
   3. 움직이는 중: 배출 이동이면 Dispatch, 무거운 잔해를 들었으면 CarryHeavy, 다른 물건을 들었으면 Carry, 그 밖에는 Walk
   4. 멈춰 있음: 작업 중이고 걸레를 들었으면 Mop, 그 밖에는 Idle
   - "움직이는 중"은 속도가 0.1m/s를 넘을 때다.
7. 헬하운드는 배출 이동(Dispatching) 중 목적지에 도착하면 모습을 숨긴다(현관 밖으로 나간 것으로 표현). 다른 상태가 되면 다시 보인다.
8. 플레이어는 계단 출입 지점 반경(`InteractRadius`) 안에서 1을 누르면 아래층, 2를 누르면 위층으로 간다(U208 임시). 계단 시간 동안 움직이지 않고, 끝나면 도착 층 계단 출입 지점으로 옮긴다. 작업 중이면 먼저 작업을 멈춘다.

## 예외 처리
- 목적지가 NavMesh 밖이면 1m 안의 가장 가까운 NavMesh 지점으로 간다. 그마저 없으면 경고를 남기고 멈춘다(`HasArrived` 참).
- 계단 이동 중에 새 `MoveTo`가 오면 진행 중인 계단 대기를 마친 뒤 새 목적지로 경로를 다시 짠다.
- 맨 아래층에서 1, 맨 위층에서 2를 누르면 아무 일도 없다.

## 설계
| 파일 | 역할 |
|---|---|
| `Runtime/View/StairRoutePlanner.cs` | 순수. 규칙 3 (테스트 대상) |
| `Runtime/View/MinionPoseResolver.cs` | 순수. 규칙 6. `EPose` enum 포함 (테스트 대상) |
| `Runtime/View/StairDirectory.cs` | 층 → 계단 출입 지점. 층 프리팹의 `StairAuthoring`에서 만든다 |
| `Runtime/View/MinionAgent.cs` | `IMinionBody` 구현(규칙 1~5). NavMeshAgent 사용, 계단은 대기 후 `Warp` |
| `Runtime/View/CharacterAnimatorDriver.cs` | `ArtSlot.Animator`에 `Pose`를 넣는다. 모델이 없으면 아무것도 하지 않는다 |
| `Runtime/View/MinionStatusPresenter.cs` | MinionStatus와 Items로 자세를 정해 드라이버에 넘기고(규칙 6), 헬하운드 숨김(규칙 7) |
| `Runtime/Player/PlayerController.cs` (수정) | 계단 이동(규칙 8), 플레이어 자세 |
| `Editor/Art/ArtAnimationBinder.cs` (수정) | `AC_Character`에 `Pose` 파라미터와 Any State 전이를 넣는다(멱등) |
| `Runtime/View/Debug/FakeMatchSession.cs` (수정) | `AssignRoom` → 그 구역 순찰 첫 점으로 MoveTo, 도착하면 Working. `DispatchHellhound` → 배출 구역으로 갔다가 잠시 뒤 복귀 |
| `Runtime/View/Debug/DebugMatchOverlay.cs` (수정) | 졸개별 "배정" 버튼(누를 때마다 다음 구역), 계단 안내 |
| `Runtime/View/MatchBootstrap.cs` (수정) | 졸개 생성(시작점), `MinionBodies` 전달, 플레이어 상호작용에 등록 |
| 프리팹 `CH_MinionBase` → `CH_Goblin`, `CH_Orc`, `CH_Skeleton`, `CH_Slime`, `CH_Hellhound` | 표준 구조 + NavMeshAgent(반경 0.4) + MinionAgent + Presenter + Driver. 크기와 색은 권장 크기표 |

**흐름**: 규칙 → `MoveTo` → MinionAgent(경로: 계단 → 대기 → Warp → 목적지) → 매 프레임 Presenter가 상태로 자세 결정 → Driver가 Animator에 `Pose`

## 미정·보류
- U201 캐릭터 애니메이터 상태: **결정** (규칙 6)
- U208 플레이어 계단 키: 임시 (1 아래층, 2 위층)
- U19 계단 시간: 임시 2초 (`ViewSettingsSO`)

## 참조 테이블
| 테이블 | 컬럼 | 용도 |
|---|---|---|
| (B) `ViewSettingsSO` | StairTransitSeconds, InteractRadius | 계단 시간, 플레이어 계단 반경 |
| (B) `FakeMatchSessionFactorySO` | MinionMoveSpeed, HellhoundAwaySeconds | 가짜 판 졸개 이동 |

## 미결 질문
- (없음)

## 테스트 시나리오 (`Test/View/StairRoutePlannerTest.cs`, `Test/View/MinionPoseResolverTest.cs`, 플레이 모드 확인)
| 규칙 | 조건 | 행동 | 기대 결과 | 테스트 메서드 이름 |
|---|---|---|---|---|
| 3 | 같은 층 | 계획 | 빈 목록 | `같은_층이면_계단을_거치지_않는다` |
| 3 | 1층→3층 / 3층→1층 | 계획 | [2층, 3층] / [2층, 1층] | `일층에서_삼층은_계단을_두_번_지난다`, `삼층에서_일층은_이층을_거쳐_내려간다` |
| 6-1 | 대화 중, 멈춤 | 자세 | Talk | `대화_중에_멈춰_있으면_Talk이다` |
| 6-2 | 소화 중 | 자세 | Eat | `소화_중이면_Eat이다` |
| 6-3 | 무거운 잔해를 들고 이동 / 쓰레기를 들고 이동 / 빈손 이동 / 배출 이동 | 자세 | CarryHeavy / Carry / Walk / Dispatch | `무거운_잔해를_들고_움직이면_CarryHeavy다`, `물건을_들고_움직이면_Carry다`, `빈손으로_움직이면_Walk이다`, `배출하러_움직이면_Dispatch다` |
| 6-4 | 작업 중 걸레 들고 멈춤 / 대기 | 자세 | Mop / Idle | `작업_중에_걸레를_들고_멈추면_Mop이다`, `할_일_없이_멈춰_있으면_Idle이다` |
| 1·2·5 | 가짜 판에서 고블린을 2층 회의실에 배정 | 플레이 | 계단을 거쳐 2층에 도착, Floor=2층 | (플레이 모드 확인) |
| 8 | 플레이어가 1층 계단에서 2 | 플레이 | 2층 계단으로 이동 | (플레이 모드 확인) |

### 실제 판에서 확인한 층간 배정

`Facility` 씬에서 1층 로비 고블린에게 대화 명령 후 3층 간부실을 배정했다. 플레이 중 관찰한 게임 시간 기준으로 13.6초에 1층 계단 대기를 시작했고, 15.6초에 2층, 17.6초에 3층 계단에 도착했다. 26.3초에는 간부실 첫 순찰점 (187.50, 0, 1.50)에서 수평 거리 약 0.20m인 지점에서 `MovingToRoom`이 `Surveying`으로 전환됐다.
