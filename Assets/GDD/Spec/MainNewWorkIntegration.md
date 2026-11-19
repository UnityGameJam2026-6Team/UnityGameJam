# main / NewWork 기능 통합

- 상태: 확정 · 2026-11-19 사용자 지정 기준
- 작업 브랜치: main
- main 기준: f9ad322 (ESC 설정 메뉴와 조작 모드 전환)
- NewWork 기준: d2ad2dd

## 기능별 기준

| 기능 | 기준 | 연결 방식 |
|---|---|---|
| 로딩 만화 컷씬·타이틀 연출 | NewWork | TitleScene, TitleMenu, ComicPanelImage와 이미지·음원 설정 유지 |
| 시작 5초 카운트다운 | NewWork | MatchBootstrap, FloorCameraFollow의 시작 연출과 상단 숫자 연결 |
| 셰이더·화면 효과 | NewWork | Screen_* 셰이더와 PC 렌더러·관련 머티리얼 유지 |
| 상단 시계·시설 진행도 | NewWork | 시계 프리팹과 상단 진행 바 연결 유지 |
| 에이전트 API·알고리즘 개선 | NewWork | 제안 대기열·대사·작업 선택·세션/계약 변경 유지 |
| 마지막 마왕 연출 | NewWork | 현관 무대·문·카메라·성공/실패 연출과 결과 공개 유지 |
| ESC 설정·도움말·조작 전환 | main | UI_GameMenu와 저장된 양손/한손 설정 유지 |
| 하단 졸개 현황·제안·방 배정·작업 게이지 | main | 기존 네 패널의 프리팹과 배치·도장 연출 유지 |

## 공통 연결

- FacilityHud는 main의 하단 대화·선택 배치를 사용하며 NewWork의 시작 숫자와 상단 시계를 갱신한다.
- 카메라는 NewWork의 시작/마지막 컷을 재생하고 main 대화 패널 사이의 화면 범위를 받아 캐릭터를 배치한다.
- 입력에는 NewWork 액션과 main의 GameMenu/Pause, 양손 확인·보류 바인딩을 함께 유지한다.
- 시작 카운트다운과 마지막 연출 중 ESC 메뉴를 새로 열지 않는다. 정상 플레이에서 열면 판 시간과 시뮬레이션을 멈춘다.
- 결과 화면은 마왕 연출이 끝난 뒤 표시한다. 이때 ESC와 재시작 입력이 충돌하지 않도록 main의 차단 조건을 유지한다.

## 확인

- 컴파일 오류 0.
- ProposalQueueTest 5개, AssignmentPanelTest 3개, MinionBottomHudTest 6개 통과.
- 실제 판에서 main 네 졸개 하단 HUD·ESC 표시와 시간 일시정지 확인.
- NewWork 현관 엔딩 시작, 실패 투척 적중과 결과 공개 확인.
- 엔딩 도중 ESC 차단 확인. 테스트 플레이 기록은 저장하지 않음.
- 커밋·푸시는 하지 않으며 해결된 병합 상태를 사용자가 검토한다.
