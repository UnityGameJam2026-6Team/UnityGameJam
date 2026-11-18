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
- [ ] A2 데이터와 생성 (Spec `ContaminationSpawn.md`)
- [ ] A3 작업 선택과 능력 (Spec `TaskSelection.md`)
- [ ] A4 도구·배출·소화 (Spec `ToolAndDischarge.md`)
- [ ] A5 졸개 두뇌와 기억 (Spec `MinionBrain.md`)
- [ ] A6 제안 대기열과 대사 (Spec `Proposal.md`)
- [ ] A7 판 흐름과 마감 (Spec `MatchFlow.md`)

## B 레인 (월드·표현) · 심지섭 · Unity AI · `docs/team/lane-b.md`
- [x] B0 프리팹·아트 기반(Spec `ArtPipeline.md`, 규칙 `docs/rules/prefab.md`·`art-asset.md`)
- [x] B1 화이트박스 시설(Spec `FacilityLayout.md`)
- [x] B2 플레이어 (Spec `Player.md`)
- [x] B3 졸개 몸 (Spec `MinionAgent.md`) — 졸개 층간 이동 도착은 플레이 확인 필요
- [ ] B4 ContaminationView
- [ ] B5 HUD (Spec `Hud.md`)
- [ ] B6 대화와 제안 UI (Spec `AssignmentUi.md`)
- [ ] B7 마감 연출과 결과 (Spec `ResultUi.md`)

## 통합 (A+B)
- [ ] M1 오염이 보이는 맵 (A1~A4, B1~B4)
- [ ] M2 졸개 배정 플레이 (A5·A6, B5·B6)
- [ ] M3 3분 한 판 완주 (A7, B7)
- [ ] 플레이테스트와 밸런스

## 완료
- [x] 프로젝트 폴더 구조 구성
