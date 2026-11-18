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
- [x] B2 플레이어 (Spec `Player.md`) — U11 키 배치 반영. U13에 따라 플레이어가 들어간 일반 방만 공개하며, 새 판에서는 여섯 방을 다시 가린다.
- [x] B3 졸개 몸 (Spec `MinionAgent.md`) — 실제 판에서 고블린이 1층 → 2층 → 3층 간부실에 도착해 탐색을 시작하는 것까지 확인
- [x] B4 ContaminationView (Spec `ContaminationView.md`) — 가짜 판과 실제 판에서 오염 표시 및 슬롯별 외형 확인.
- [x] B5 HUD (Spec `Hud.md`) — 실제 판에서 Tab 현황 패널과 직접 청소 게이지 확인. D5 결정에 따라 상단 중앙 타이머를 분:초(`03:00`)로 표시한다.
- [x] B6 대화와 제안 UI (Spec `AssignmentUi.md`) — M2에서 대면 배정과 제안 응답 확인. Facility에서 방향키 탐색, Enter 배정·Esc 취소 후 이동 재개를 확인했다. 오크에게 2층 회의실을 배정한 뒤 다시 말을 걸면 담당 방이 표시되고, 미배정 고블린에게 말을 걸면 이름과 담당 방이 함께 갱신된다.
- [x] B7 마감 연출과 결과 (Spec `ResultUi.md`) — 마왕 등장 후 시설·9개 구역의 최종 성적을 표시한다. 가짜 판의 성공·실패, 배정 대화 중 마감과 R을 누른 채 결과 화면 진입을 확인했다. 전체 청소 완료 후 남은 시간을 빠르게 넘기고, 이 PC 순위표에 완주 시간순·미완주 최종 진행도순으로 기록한다. 운반 중 100%의 조기 종료 방지와 실제 완료 시각 표시를 확인했다.

## 통합 (A+B)
- [x] M1 오염이 보이는 맵 (A1~A4, B1~B4) — Facility 실제 판에서 슬롯별 외형, 제자리 정리, 운반 감속·내려놓기, 헬하운드 배출, 걸레 대여·청소·반납과 청결도 반영 확인.
- [x] M2 졸개 배정 플레이 (A5·A6, B5·B6) — Facility 실제 판에서 고블린·오크·스켈레톤·슬라임을 방 선택 패널로 1층 창고에 배정했다. 네 마리의 작업과 창고 청결도 100%를 확인했다. 자연 발생 제안에 F로 응답한 슬라임은 1층 로비에 도착해 탐색했고, R로 응답한 스켈레톤은 로비 시작점으로 돌아와 대기했다. 졸개 현황 패널에도 두 상태가 반영됐다.
- [x] M3 3분 한 판 완주 (A7, B7) — Facility 실제 판의 180초를 배속으로 진행했다. 2층에서 운반 중인 쓰레기가 바닥으로 돌아오고 현재 층에 마왕이 등장한 뒤 결과가 열렸다. R과 다시 하기 버튼으로 각각 새 180초 판이 시작되는 것을 확인했다.
- [ ] 플레이테스트와 밸런스

## 완료
- [x] 프로젝트 폴더 구조 구성
