# 로드맵

다음에 할 작업의 기준 문서. 위에서부터 순서대로 진행한다.
전체 계획은 `Assets/Plans/demon-king-cleaning-implementation.md`, 레인과 소유 규칙은 `docs/rules/collaboration.md`를 따른다.
**각자 자기 레인 단락만 고친다.** 통합 단락은 둘이 함께 확인한 뒤 고친다.

## 공통 (0단계: 기반과 계약)
- [x] 문서 정리 (Roadmap, DesignIssue D1~D8, Undecided, AGENTS.md 요약)
- [x] 협업 규칙 `docs/rules/collaboration.md`
- [x] 폴더 미리 만들기, 공유 설정(asmdef, Input 액션, 태그·레이어)
- [x] 계약 코드 `Runtime/Contract/`
- [x] 미결 질문 Q1~Q8 답변 반영 (`Undecided.md` U1~U8 결정)

## A 레인 (규칙·시뮬레이션) · 김보경 · Codex · `docs/team/lane-a.md`
- [x] A1 청결도·오염 모델 (Spec `Cleaning.md`)
- [x] A2 데이터와 생성 (Spec `ContaminationSpawn.md`)
- [x] A3 작업 선택과 능력 (Spec `TaskSelection.md`)
- [x] A4 도구·배출·소화 (Spec `ToolAndDischarge.md`)
- [x] A5 졸개 두뇌와 기억 (Spec `MinionBrain.md`)
- [x] A6 제안 대기열과 대사 (Spec `Proposal.md`)
- [x] A7 판 흐름과 마감 (Spec `MatchFlow.md`)

## B 레인 (월드·표현) · 심지섭 · Unity AI · `docs/team/lane-b.md`
- [x] B0 프리팹·아트 기반(Spec `ArtPipeline.md`, 규칙 `docs/rules/prefab.md`·`art-asset.md`)
- [x] B1 화이트박스 시설(Spec `FacilityLayout.md`)
- [x] B2 플레이어 (Spec `Player.md`)
- [x] B3 졸개 몸 (Spec `MinionAgent.md`) — 실제 판에서 고블린이 1층 → 2층 → 3층 간부실에 도착해 탐색을 시작하는 것까지 확인
- [x] B4 ContaminationView (Spec `ContaminationView.md`) — 가짜 판과 실제 판에서 오염 표시 및 슬롯별 외형 확인.
- [ ] B5 HUD (Spec `Hud.md`) — 실제 판에서 Tab 현황 패널과 직접 청소 게이지 확인. 타이머 형식 D5 검토 대기.
- [ ] B6 대화와 제안 UI (Spec `AssignmentUi.md`) — 우측 하단 제안 스택, 방 선택 중 제안 입력 차단과 만료, M2에서 대면 배정 및 자연 발생 제안 응답 확인. 방 선택 중 이동 입력과 방향키·Enter·Esc 조작은 별도 확인 대상.
- [ ] B7 마감 연출과 결과 (Spec `ResultUi.md`)

## 통합 (A+B)
- [x] M1 오염이 보이는 맵 (A1~A4, B1~B4) — Facility 실제 판에서 슬롯별 외형, 제자리 정리, 운반 감속·내려놓기, 헬하운드 배출, 걸레 대여·청소·반납과 청결도 반영 확인.
- [x] M2 졸개 배정 플레이 (A5·A6, B5·B6) — Facility 실제 판에서 고블린·오크·스켈레톤·슬라임을 방 선택 패널로 1층 창고에 배정했다. 네 마리의 작업과 창고 청결도 100%를 확인했다. 자연 발생 제안에 F로 응답한 슬라임은 1층 로비에 도착해 탐색했고, R로 응답한 스켈레톤은 로비 시작점으로 돌아와 대기했다. 졸개 현황 패널에도 두 상태가 반영됐다.
- [ ] M3 3분 한 판 완주 (A7, B7)
- [ ] 플레이테스트와 밸런스

## 완료
- [x] 프로젝트 폴더 구조 구성
