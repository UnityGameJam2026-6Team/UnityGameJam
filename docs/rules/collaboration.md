# 2인 협업 규칙 (레인 소유 · 계약 · 충돌 방지)

> 모든 작업 전에 읽는다. 프로그래머 2명이 같은 저장소에서 병합 충돌 없이 병렬로 작업하기 위한 규칙이다.
> 에이전트는 작업을 시작할 때 사용자에게 **어느 레인인지** 확인한다(예: "나는 A 레인"). 레인을 모르면 파일을 고치기 전에 묻는다.

## 1. 레인과 소유 경로
파일마다 주인은 한 명이다. **주인이 아닌 사람(과 그 사람의 에이전트)은 그 파일을 고치거나 커밋하지 않는다.**

| 레인 | 담당 | 소유 경로 (`Assets/@UnityGameJam/` 기준) |
|---|---|---|
| **A: 규칙·시뮬레이션** | 순수 C# 게임 규칙, EditMode 테스트, 수치 데이터 | `2_Script/Runtime/{Cleaning, Minion, Proposal, Match, Facility, Data}/`, `2_Script/Test/`, `3_Data/` |
| **B: 월드·표현** | 씬, 프리팹, 입력, 이동, 카메라, UI, 에디터 도구, 아트·사운드 적용 | `2_Script/Runtime/{Player, View, UI}/`, `2_Script/Editor/`, `1_Scene/`, `4_Prefab/`, `5_Art/`, `6_Audio/`, `7_Setting/InputSystem_Actions.inputactions`, 저장소 루트의 `ProjectSettings/` |
| **공동 (계약)** | 두 레인의 접점 | `2_Script/Runtime/Contract/` (2절 절차로만 바꾼다) |
| **공동 (설정)** | 한 번 정하면 거의 바뀌지 않는 것 | asmdef, `Packages/manifest.json`, `7_Setting/` 아래 URP 에셋, `AGENTS.md`, `docs/rules/` (바꿀 때는 메신저로 먼저 알리고 단독 PR로 올린다) |

- `Assets/GDD/Spec/<기능>.md`는 작성한 레인이 주인이다.
- `Assets/GDD/Roadmap.md`는 **자기 레인 단락만** 고친다. 통합(M1~M3) 단락은 둘이 함께 확인한 뒤 고친다.
- 소유권을 넘길 때(예: B의 UI 프리팹 하나를 A가 맡음)는 이 표에 경로를 먼저 적고 PR로 머지한다. 넘기는 단위는 **프리팹 하나와 그 스크립트**처럼 작게 잡는다.

## 2. 계약 (`Runtime/Contract/`)
두 레인은 계약을 통해서만 서로를 호출한다. A는 B의 MonoBehaviour를 참조하지 않고, B는 A의 내부 클래스를 직접 만들지 않는다.

| 계약 | 방향 | 용도 |
|---|---|---|
| `GameEnums` | 공용 | 구역, 층, 오염 유형, 작업자, 개체 상태, 졸개 행동, 판 상태 |
| `FacilityLayout` | B → A | 씬에서 뽑은 순수 데이터. A는 테스트에서 직접 만들어 쓴다. |
| `IWorkerBody`, `IMinionBody` | A → B | 규칙이 읽는 몸의 위치와 층, 졸개 두뇌가 몸(NavMeshAgent)에 내리는 명령. 층간 이동(계단)은 몸이 처리한다. |
| `IMatchCommands` | B → A | 플레이어 입력을 규칙으로 넘긴다 |
| `IMatchReadModel` | A → B | UI와 연출이 읽는 상태, DTO(`MinionStatus`, `MessageEntry`, `ItemSnapshot`, `MatchResult`), 이벤트 |
| `IMatchSession`, `MatchSetup`, `MatchSessionFactoryBase` | A → B | Bootstrap이 인스펙터로 팩토리 SO를 연결하고 판을 만든 뒤 매 프레임 `Tick`을 부른다. A는 실제 팩토리, B는 가짜 팩토리를 상속한다. |

**바꾸는 절차**
1. 메신저로 무엇을 왜 바꾸는지 먼저 알린다.
2. `Contract/`만 고치는 PR을 만든다(구현 코드를 섞지 않는다). 커밋 메시지 예: `refactor: 계약 IMatchCommands에 걸레 내려놓기 추가`
3. 상대가 리뷰한 뒤 바로 머지하고, 두 사람 모두 main을 받는다.
4. 계약을 깨는 변경(이름 변경, 삭제)은 피한다. 필요하면 새 멤버를 먼저 추가하고, 양쪽이 옮긴 뒤 지운다.

## 3. 상대를 기다리지 않는 방법
- B는 A가 구현을 끝내기 전에도 `Runtime/View/Debug/`의 가짜 구현(`FakeMatchSession` 등, B 소유)으로 HUD와 조작을 먼저 만든다.
- A는 씬 없이 `FacilityLayout`을 테스트 코드에서 직접 만들고, `IMinionBody`는 테스트용 가짜로 대신한다.
- 통합은 정해진 지점(M1~M3, `Roadmap.md`)에서 함께 확인한다.

## 4. 번호 대역
`Assets/GDD/Undecided.md`와 `DesignIssue.md`는 둘이 동시에 행을 추가하므로 번호 대역을 나눈다. 번호는 바꾸지 않는다.

| 대역 | 쓰는 사람 |
|---|---|
| 1~99 | 공통 (0단계 기록, 둘이 함께 정한 항목) |
| 100~199 | A 레인 |
| 200~299 | B 레인 |

## 5. 씬·프리팹
- 씬(`1_Scene/Facility.unity`)에는 Bootstrap, 카메라, 조명, 층 프리팹, UI 루트 프리팹만 둔다. 내용은 프리팹(`4_Prefab/`) 안에서 고친다.
- 층(`Floor1/2/3.prefab`)과 UI 패널은 각각 별도의 (중첩) 프리팹으로 만든다.
- 남의 씬이나 프리팹을 예외적으로 고쳐야 하면 메신저로 "잠금"을 알리고, 머지한 뒤 "해제"를 알린다.
- 3_Data의 SO 에셋은 A 소유다. B가 연출 값이 필요하면 B 소유의 별도 SO(`ViewSettingsSO` 등)를 만든다.

## 6. Git 운영
- 브랜치는 Spec 하나에 하나씩 만든다: `feat/cleaning-model`, `feat/facility-layout` 등. PR은 1~2일 분량으로 작게 나누고, **상대가 리뷰한 뒤** Squash로 머지한다.
- 하루에 한 번 이상, 그리고 계약 PR이 머지되면 바로 main을 자기 브랜치로 받는다.
- **커밋 전 확인**
  - 변경 목록에 자기 소유가 아닌 파일이 있으면 되돌린다(`git restore <파일>`). Unity는 씬을 열기만 해도 씬이나 `ProjectSettings/`를 다시 저장하는 경우가 있다.
  - 새 파일과 새 폴더의 `.meta`가 함께 있는지 확인한다.
  - main을 받은 상태에서 컴파일 에러 0, EditMode 테스트가 모두 통과하는지 확인한다.
- 새 폴더는 가능하면 자기 소유 경로 안에서만 만든다. 공동 경로에 폴더가 필요하면 단독 PR로 먼저 머지한다(둘이 같은 폴더를 따로 만들면 폴더 `.meta`의 GUID가 갈려 충돌한다).
- YAML 충돌이 생기면 수동으로 고치지 말고, 주인의 버전을 받은 뒤 다른 사람이 다시 작업한다. 최후 수단으로 각자 로컬 git 설정에 UnityYAMLMerge를 mergetool로 등록해 쓸 수 있다.
  ```
  [merge]
      tool = unityyamlmerge
  [mergetool "unityyamlmerge"]
      trustExitCode = false
      cmd = '<Unity 설치 경로>/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
  ```
