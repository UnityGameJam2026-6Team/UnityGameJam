# Project Overview
- Game Title: UnityGameJam (프로젝트명 기준, 정식 제목은 이번 변경 범위 밖).
- High-Level Concept: 시설을 청소하면서 졸개에게 작업을 배정하는 싱글 플레이 게임. 기존 좌측 졸개 현황과 우측 메시지 패널을 하단 관리 HUD로 통합하여 상태 확인 → 메시지 읽기 → 지시 흐름을 한곳에서 제공한다.
- Players: 기존 단일 플레이어 구조 유지.
- Inspiration / Reference Games: 사용자 설명의 13번 현황 탭, 11번 팝업 패널 및 첨부 참조를 배치 지침으로 사용. 이번 조사에서 첨부 이미지의 실제 픽셀은 확인되지 않았으므로 세부 그림 형태는 재현했다고 주장하지 않는다.
- Tone / Art Direction: 기존 시설 HUD의 색상·서체·패널 스타일 재사용. 새 AI 그림 생성은 범위 밖이며 초상은 기존 자산을 조사해 활용하고 없으면 명확한 이름/색상 식별용 임시 표시를 사용한다.
- Target Platform: StandaloneWindows64, Unity 6000.3.28f1.
- Screen Orientation / Resolution: 기존 CanvasScaler 기준 가로 1920×1080; 1280×720 및 초광폭에서도 검증.
- Render Pipeline: PC_RPAsset 기반 기존 파이프라인 유지. 렌더링 설정은 변경하지 않는다.

# Game Mechanics
## Core Gameplay Loop
- 하단의 고블린·오크·스켈레톤·슬라임 4개 고정 슬롯에서 이름, 담당 방, 현재 행동을 실시간 확인한다.
- 각 졸개 메시지는 발신 졸개의 슬롯 위에 표시한다. 응답 대상 제안에는 승인/보류와 남은 시간을 표시한다.
- 사용자 확인 사항: 헬하운드는 하단 4개 슬롯에 포함하지 않고 기존 별도 배출 기능을 유지한다. 헬하운드 안내 메시지도 누락하지 않고 하단 HUD 인근 공용 안내 영역으로 보낸다.
- 사용자 선택: 기존 규칙 유지. 이동 제안은 기존 최오래된 활성 제안에만 승인/보류 가능하며, 방 배정은 기존 대면 대화가 성립했을 때만 가능하다. HUD 클릭으로 원격 방 배정을 새로 허용하지 않는다.
- 상태와 피드백의 공간적 연결로 시선 이동을 줄이고 졸개 관리의 명확성을 높인다. 경제·작업 속도·AI·청결도·제안 제한 시간은 변경하지 않는다.

## Controls and Input Methods
- 기존 New Input System 및 InputSystemUIInputModule을 유지한다. 구현 직전에 PlayerSettings의 실제 활성 입력 설정과 Action 연결을 재확인한다.
- 기존 승인/보류 InputActionReference를 그대로 사용한다. 키 이름은 실제 바인딩에서 확인하고 화면 힌트도 이를 따른다.
- 팝업의 승인/보류 버튼은 클릭과 기존 UI 내비게이션을 지원한다. 현재 MessageEntry.IsInputTarget이 true이고 대면 대화 중이 아닐 때만 활성화한다.
- 오래된 메시지 버튼 이벤트가 새 제안에 잘못 적용되지 않도록 클릭 시 MessageId와 현재 입력 대상, 매치 상태를 재검증한다.
- 대면 대화에서는 기존 방 선택 버튼·취소·플레이어 입력 중단/복구·FocusConversation을 유지한다. 하단으로 이동한 패널의 포커스가 화면 밖으로 나가지 않게 한다.
- 4명의 실시간 현황은 항상 보인다. 기존 현황 탭 토글만으로 하단 전체가 숨겨지지 않게 하며 공유 Action을 임의 Disable하지 않는다.

# UI
## Wireframe
```text
┌──────────────────── 게임 화면 / 기존 상단 HUD ────────────────────┐
│                                                                  │
│                 (대면 중) 방 배정 / 대화 패널                       │
│  [고블린 메시지]    [오크 메시지]    [스켈레톤 메시지] [슬라임 메시지] │
│  [승인][보류]       [알림]           [응답 대기]       [알림]        │
├────────────────────── 하단 졸개 HUD ──────────────────────────────┤
│ [초상/이름]         [초상/이름]      [초상/이름]       [초상/이름]   │
│ 고블린             오크             스켈레톤          슬라임       │
│ 담당 방 / 행동      담당 방 / 행동   담당 방 / 행동    담당 방 / 행동│
└──────────────────────────────────────────────────────────────────┘
```
- 슬롯 순서는 고블린 → 오크 → 스켈레톤 → 슬라임으로 명시적으로 매핑한다. session.Minions 인덱스로 매핑하지 않는다(헬하운드가 포함될 수 있음).
- 기존 가로 Canvas에 하단 고정 앵커로 배치한다. 초기 바 높이는 기준 해상도에서 약 140–170px로 시작하고 실제 플레이 화면에서 검증한다.
- 각 슬롯 위에 메시지 앵커를 둔다. 동일 졸개의 복수 메시지는 높이 기준으로 쌓고, 화면 경계를 넘는 경우 제한된 스크롤 영역으로 접근 가능하게 한다. 최신 메시지·현재 응답 대상을 묻어버리거나 임의로 제거하지 않는다.
- 메시지 없을 때 상태 슬롯만 표시한다. 구분은 이름/상태 텍스트와 강조를 함께 사용하며 색상만으로 전달하지 않는다.
- 대면 방 배정 UI는 하단 HUD 위 공용 영역으로 이동하고 발신 졸개를 명확히 표시한다. 메시지와 배정 UI의 겹침 방지, 시야 점유와 긴 한글 문장 가독성을 확인한다.
- 장식 이미지/텍스트의 raycast를 제거하고 클릭 가능한 버튼에만 입력을 받게 한다. 기존 작업 게이지, 결과창, 타이머 등은 기능을 보존한다.

# Key Asset & Context
## 확인된 구조
- `Assets/@UnityGameJam/4_Prefab/UI/UI_HudRoot.prefab`: uGUI Canvas, 기준 1920×1080, FacilityHud 및 DemonKingArrivalView 연결.
- `UI_MinionStatusPanel.prefab`: MinionStatusPanel이 단일 텍스트 현황 및 토글을 처리.
- `UI_ProposalStack.prefab`: ProposalStackView 및 Card/ProposalCard. 메시지 카드는 이미 재사용되는 풀 구조.
- `UI_AssignmentPanel.prefab`: 9개 방 선택 버튼 및 Cancel. 기존 대면 규칙을 그대로 활용.
- `Assets/@UnityGameJam/2_Script/Runtime/UI/FacilityHud.cs`: LateUpdate에서 session.Minions와 메시지를 표시, 대화 UI 및 입력 잠금/복구 담당. EndMatch에서 구 HUD를 비활성화.
- `MinionStatusPanel.cs`: Show(IReadOnlyList<MinionStatus>), WorkerName/RoomName 기존 표시 함수.
- `ProposalStackView.cs`: Show(IMatchSession, float, RectTransform, bool); 현재 전체 메시지 수직 스택과 최오래된 제안 응답.
- `ProposalCard.cs`: MessageId별 본문 갱신, 시간·게이지·배경·힌트 표시.
- `Contract/IMatchReadModel.cs`: MinionStatus.Minion/AssignedZone/Activity, MessageEntry.Minion/MessageId/Kind/IsInputTarget, ActiveMessages와 MessagesChanged.
- `Contract/IMatchCommands.cs`: BeginTalk, AssignRoom, ApproveOldestProposal, HoldOldestProposal. 인터페이스 변경 없음.
- `Match/MatchSimulation.cs`: Minions getter는 호출마다 List를 생성하고 헬하운드를 추가할 수 있다. HUD는 프레임당 한 번만 상태를 받아 공유한다. 시뮬레이션 최적화는 이번 범위 밖.

## 변경/신규 자산
- 수정: 위 UI_HudRoot, UI_MinionStatusPanel, UI_ProposalStack, 필요한 UI_AssignmentPanel 레이아웃.
- 수정: FacilityHud.cs, MinionStatusPanel.cs, ProposalStackView.cs, ProposalCard.cs.
- 신규(필요 시): `Runtime/UI/MinionHudSlot.cs`, `4_Prefab/UI/UI_MinionHudSlot.prefab` — 식별 타입, 이름/방/행동/초상/메시지 앵커만 담당.
- 필요 시 `DemonKingArrivalView.cs` 또는 루트의 _gameplayPanels 직렬화 목록을 업데이트하여 종료 연출 시 새 하단 HUD가 정상 숨겨지게 한다.
- `Facility.unity`는 프리팹 변경이 반영되는지 확인하고 기존 인스턴스 override가 실제로 방해할 때만 최소 수정한다.
- 입력 에셋, 규칙/AI, 패키지 소스, 렌더 파이프라인은 수정하지 않는다.
- 현재 도구에서 전용 의존성 조회가 노출되지 않아 AssetDatabase.GetDependencies로 루트/스크립트 의존 관계를 확인했다. 구현 전 각 추가 변경 자산과 씬의 의존/참조를 다시 확인한다.

# Implementation Steps
1. **사전 영향/연결 조사**
   - 의존성 조회와 프리팹/씬 실인스턴스의 serialized reference, nested prefab 및 override를 확인한다.
   - 기존 초상 Sprite, UI 서체/색상/버튼 스타일, 입력 바인딩, 실제 MatchBootstrap Session 생성·졸개 4명 구성을 확인한다.
   - 기존 테스트 위치와 종료/재시작 흐름을 조사한다.
   - Dependencies: 계획 승인. 게임 자산 변경 전 완료.
2. **4슬롯 현황 구현**
   - MinionStatusPanel.cs를 항상 표시되는 고정 4슬롯 표시자로 변경한다. 필요 시 MinionHudSlot.cs와 슬롯 프리팹을 추가한다.
   - EWorkerType으로 상태를 찾아 표시하고 이름·방·행동이 바뀔 때만 텍스트를 갱신한다. 빈 세션/없는 졸개는 안전한 대기/정보 없음 표시.
   - 토글 구 연결을 정리하며 기존 WorkerName/RoomName 호출 호환성을 유지한다.
   - Dependencies: Step 1.
3. **졸개별 메시지 배치와 응답 연결**
   - ProposalStackView.cs에서 기존 메시지 풀을 재사용하면서 발신 타입별 슬롯 앵커로 배치한다. 헬하운드는 공용 안내 영역에 배치한다.
   - ProposalCard.cs에 승인/보류 버튼 및 필요 이벤트 바인딩을 추가한다. 풀 재사용 때 중복 리스너 및 이전 MessageId가 남지 않도록 한다.
   - 타이머/강조/메시지 수명은 기존 세션 값을 따른다. 대면 중 제안 응답 금지 및 기존 최오래된 대상 규칙을 보존한다.
   - 복수 메시지와 긴 문장의 화면 경계/스크롤 정책을 구현한다.
   - Dependencies: Step 2.
4. **하단 통합 프리팹 및 대면 UI 레이아웃**
   - UI_MinionStatusPanel, UI_ProposalStack, UI_HudRoot에 하단 바·4슬롯·발신별 팝업/공용 안내 앵커를 직렬화 연결한다.
   - UI_AssignmentPanel의 대화/방 선택을 하단 인접 영역으로 이동한다. 기존 오른쪽 위치 계산과 충돌하는 FacilityHud의 배치 로직을 교체한다.
   - 기존 좌측 현황/우측 팝업 중복 UI가 남지 않게 한다.
   - Dependencies: Steps 2–3.
5. **세션/입력/종료 통합**
   - FacilityHud.LateUpdate에서 상태 리스트를 한 번 받아 현황과 대면 판정에 공유한다. 새 팝업 배치에 맞게 Show 호출을 연결한다.
   - 방 배정 성공/취소/대화 대상 변경/매치 종료/비활성화 시 포커스와 입력 잠금 복구를 회귀 검증한다.
   - EndMatch와 DemonKingArrivalView의 gameplayPanels에서 하단 바 및 팝업을 포함한다. 헬하운드 배출 조작은 그대로 둔다.
   - Dependencies: Step 4.
6. **검증·조정·완료 보고**
   - 아래 테스트를 실행하고 플레이 화면에서 배치, 긴 메시지, 입력 및 재시작을 확인한다.
   - 화면 캡처로 최종 모습과 남은 임시 초상 여부를 명확히 보고한다. 측정 전 성능 개선을 주장하지 않는다.
   - Dependencies: Step 5.

# Verification & Testing
- Unity 컴파일 오류 및 신규 Console 오류 없음. 루트/중첩 프리팹에 Missing reference 없음.
- 4슬롯이 지정 타입/순서와 일치하고 헬하운드가 5번째 카드로 나타나지 않는다.
- 현황 변경(미배정, 이동, 작업, 운반, 응답 대기, 대화)이 해당 슬롯에 반영된다. 초기 세션 없음/빈 상태도 예외 없이 표시.
- 서로 다른 졸개 동시 메시지 및 동일 졸개 복수 메시지가 올바른 발신 위치에 표시되고 본문/타이머가 혼동되지 않는다.
- 승인/보류 버튼과 기존 키가 동일한 최오래된 대상에만 작동한다. 만료/삭제 직후 클릭, 입력 대상 교체, 반복 클릭은 다른 메시지에 잘못 적용되지 않는다.
- 알림에 승인/보류가 표시되지 않으며 헬하운드 안내가 누락되지 않는다.
- 원격 HUD 조작만으로 방 배정이 되지 않는다. 기존 대면 시작 → 방 선택 → 성공 또는 취소 흐름이 유지되고 플레이어 입력이 정상 복구된다.
- 대화 중 제안 응답이 차단된다. UI 버튼에만 Raycast가 걸리고 장식/팝업 비활성 영역이 플레이 입력을 가로막지 않는다.
- 1920×1080, 1280×720, 2560×1080에서 슬롯 잘림/팝업 화면 이탈/배정 UI 겹침 없음. 긴 한글 문장과 다수 메시지를 읽을 수 있다.
- 종료 연출/결과/재시작 시 HUD와 풀 카드가 올바르게 숨김/복구되고 이벤트 중복이 없다.
- 기존 테스트 패턴을 따라 타입별 매핑, 입력 대상 검증, 메시지 그룹 배치에 대한 EditMode 테스트를 추가한다(테스트 asmdef 위치는 Step 1에서 확정).
- 짧은 Profiler 기록으로 매 프레임 슬롯 생성/메시지 Instantiate/중복 전체 상태 조회가 없는지 확인한다. 기존 시뮬레이션의 Minions getter 할당을 UI 변경의 신규 비용으로 오인하지 않는다.
