# A 레인 시작 문서 · 김보경 · Codex

> 이 문서는 김보경과 김보경의 Codex만 고친다. 규칙 원본은 `AGENTS.md`와 `docs/rules/`에 있다.

## 맡는 일
순수 C# 게임 규칙(청결도, 오염, 작업 선택, 도구, 졸개 두뇌, 제안 대기열, 판 흐름), EditMode 테스트, 수치 데이터(SO).

- **소유 경로**: `2_Script/Runtime/{Cleaning, Minion, Proposal, Match, Facility, Data}/`, `2_Script/Test/`(단, `Test/Art/`와 `Test/View/`는 B 소유), `3_Data/`
- **읽기만 하는 곳**: `Runtime/Contract/`(바꿀 때는 `collaboration.md` 2절 절차), 씬, 프리팹, `5_Art/`
- **에디터 작업**: 거의 없다. SO 에셋 생성은 에디터 스크립트를 쓰거나 에디터에서 직접 한다(`unity-editor.md`).

## 작업 큐 (`Assets/GDD/Roadmap.md` A 레인 단락)
| 순서 | 작업 | Spec | 근거 | 브랜치 |
|---|---|---|---|---|
| A1 | 청결도·오염 모델 | `Cleaning.md` | GDD §3.1, §7.2, §8.1·8.2, U7·U8, D3 | `feat/cleaning-model` |
| A2 | 데이터와 생성 | `ContaminationSpawn.md` | GDD §3.1·3.2, §11 | `feat/contamination-spawn` |
| A3 | 작업 선택과 능력 | `TaskSelection.md` | GDD §4, §5.1-3, U1·U7 | `feat/task-selection` |
| A4 | 도구·배출·소화 | `ToolAndDischarge.md` | GDD §7.1·7.2, U5 | `feat/tool-discharge` |
| M1 | 통합: 오염이 보이는 맵 | - | `MatchSessionFactoryBase` 실제 구현 제공 | 함께 |
| A5 | 졸개 두뇌와 기억 | `MinionBrain.md` | GDD §5, §6, U6·U9·U13 | `feat/minion-brain` |
| A6 | 제안 대기열과 대사 | `Proposal.md` | GDD §6, U10·U15 | `feat/proposal-queue` |
| A7 | 판 흐름과 마감 | `MatchFlow.md` | GDD §8, U15 | `feat/match-flow` |

- A1 시작 전에 **D3(작은 물건을 운반할 때의 집계)**를 심지섭과 정한다.
- 미정 사항과 기획 오류는 **100번대** 번호를 쓴다.
- A7을 일찍 끝내면 B6과 B7의 UI 프리팹을 소유권 이전으로 넘겨받을 수 있다. 프리팹 작업에는 에디터가 필요하다.

## Codex 첫 프롬프트 (A1)
```
나는 김보경이고 A 레인(규칙·시뮬레이션) 담당이다.
AGENTS.md, docs/team/lane-a.md, docs/rules/collaboration.md, workflow.md, coding-convention.md를 먼저 읽어라.
Assets/GDD/Roadmap.md의 A1 "청결도·오염 모델"을 진행한다. 브랜치는 feat/cleaning-model.
Assets/GDD/Spec/_Template.md를 복사해 Assets/GDD/Spec/Cleaning.md를 작성하라.
근거: GDD §3.1, §7.2, §8.1·8.2, Undecided U7·U8, DesignIssue D3.
Runtime/Contract/의 타입을 그대로 쓰고, FacilityLayout은 테스트 코드에서 직접 만든다.
스펙과 미결 질문을 먼저 보여 주고, 승인 전에는 구현하지 마라.
A 레인 소유 경로 밖의 파일과 Contract/는 고치지 마라.
```
다음 작업부터는 첫 줄과 읽을 문서는 그대로 두고, 작업 번호, Spec 이름, 근거, 브랜치만 바꾼다.

## 커밋 전 체크리스트
- [ ] 변경 파일이 모두 A 소유 경로 안에 있다(밖의 파일은 `git restore`)
- [ ] 새 파일과 폴더의 `.meta`가 있다
- [ ] main을 받은 상태에서 컴파일 에러 0, EditMode 테스트 전부 통과 (에디터가 열려 있으면 Test Runner, 닫혀 있으면 배치 모드)
- [ ] 스펙의 규칙마다 테스트가 있다. 콘텐츠 수치는 `3_Data`에서 읽는다
- [ ] Roadmap에서 A 레인 단락의 상태만 바꿨다
