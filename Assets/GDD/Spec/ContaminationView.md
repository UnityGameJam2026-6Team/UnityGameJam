# 오염 표시 (B4) 스펙

- 상태: **확정** (사용자 요청으로 작성과 구현을 함께 진행)
- 담당: B 레인 · 심지섭
- 근거: GDD §3.1(오염 4유형: 무거운 잔해, 일반 쓰레기, 어질러진 작은 물건, 바닥 얼룩), §7.1(물걸레), §7.2(운반·배출 구역·헬하운드 배출), §5.1-3·§4(슬라임은 먹어서 처리) / 계약 `ItemSnapshot`(`HomeSlotId` 포함), `MinionStatus.IsHoldingMop`, `IMatchReadModel.IsPlayerHoldingMop` / Undecided U3(개체마다 제자리), U202(작은 물건 4종)
- 범위: 이번에 하는 것은 오염 개체와 물걸레의 화면 표시(상태별 보이기·숨기기, 운반자 따라가기, 종류별 프리팹), `IT_` 프리팹, 기존 계약 `ItemSnapshot.HomeSlotId` 연결, B2의 디버그 개체 표시 제거다. 하지 않는 것은 처리 연출 이펙트, 사운드, HUD(B5)다.
- 네임스페이스: `UnityGameJam.View`

## 목적
규칙(A 레인)이 관리하는 오염 개체 상태를 그대로 화면에 보여 준다. 플레이어가 "무엇이 어디에 남았는지, 누가 무엇을 들고 있는지" 눈으로 알 수 있게 한다.

## 입력 / 출력
| 구분 | 이름 | 설명 |
|---|---|---|
| 입력 | `IMatchReadModel.Items` (`ItemSnapshot`) | ID, 유형, 상태, 위치, 운반자, 제자리 슬롯 |
| 입력 | `IsPlayerHoldingMop`, `MinionStatus.IsHoldingMop` | 걸레 표시 |
| 입력 | 제자리 슬롯 종류 표 | 씬의 `HomeSlotAuthoring`(SlotId → Book/Cup/Document/Chair) |
| 출력 | 화면의 `IT_` 인스턴스 | 개체마다 하나, 걸레는 걸레를 든 캐릭터마다 하나 |

## 규칙
1. 개체마다 화면 오브젝트를 하나 둔다. 처음 보이는 ID는 그때 만든다.
2. 바닥(OnFloor)과 배출 구역(InDischargeArea)의 개체는 규칙이 정한 위치에 둔다. 집을 때는 손이 닿는 순간부터, 놓을 때는 역재생한 손이 바닥에 닿을 때까지 손을 따라간다. 가벼운 물건은 플레이어·오크의 왼손이나 고블린의 오른손에, 무거운 잔해는 양손 사이에 놓는다. 작은 물건은 제자리에 놓은 순간 숨긴다. 소화(Digesting)나 배출(Dispatched)을 시작한 개체는 즉시 숨긴다.
3. 운반자를 화면에서 찾지 못하면 마지막으로 알려진 개체 위치에 둔다.
4. 프리팹은 유형별 목록에서 `ItemId % 개수`로 고른다(같은 판에서 같은 개체는 항상 같은 모양).
5. 어질러진 작은 물건은 `HomeSlotId`의 슬롯 종류(Book 등)에 맞는 프리팹을 쓴다. 슬롯이 없거나 종류를 모르면 작은 물건 목록의 첫 번째를 쓴다.
6. 걸레(`IT_Mop`)는 손잡이의 `Grip`이 캐릭터의 오른손을 따라가며, 손 높이에 맞춰 머리가 바닥 쪽을 향한다. 반납 동작에서 손을 떼면 숨긴다. 손 본이 없는 대체 모델에는 기존 운반 높이·걸레 오프셋을 쓴다.
7. 계약: 기존 `ItemSnapshot.HomeSlotId`(int?)를 읽는다. 작은 물건만 값이 있고 다른 유형은 null이다. 기존 생성자 호출은 그대로 컴파일된다(선택 인자).
8. 같은 층의 공개된 공간에서 플레이어가 손이 닿는 바닥 작업에 발광 고리를 표시한다. 빈손이면 쓰레기·잔해·작은 물건을, 걸레를 들었으면 얼룩을 표시한다. 운반 중인 물건과 이미 배출 구역에 적치한 폐기물은 제외한다. 입력으로 바로 작업할 대상은 더 밝게 표시한다.

## 예외 처리
- 프리팹 목록이 비어 있으면 그 유형은 표시하지 않고 경고를 한 번 남긴다.

## 설계
| 파일 | 역할 |
|---|---|
| `Runtime/Contract/IMatchReadModel.cs` (기존 계약) | `ItemSnapshot.HomeSlotId` (규칙 7) |
| `Runtime/View/ContaminationViewRules.cs` | 순수. 보이는 상태(규칙 2), 프리팹 고르기(규칙 4), 작은 물건 종류(규칙 5), 운반 위치 (테스트 대상) |
| `Runtime/View/ContaminationView.cs` | 매 프레임 `Items`를 화면에 반영(규칙 1~6). SYS_Bootstrap에 붙인다 |
| `Runtime/View/ViewSettingsSO.cs` (수정) | 유형별 프리팹 목록, 작은 물건 종류별 프리팹, 걸레 프리팹, 운반 높이 |
| `Runtime/View/MatchBootstrap.cs` (수정) | 제자리 슬롯 종류 표(`HomeSlotKinds`)를 씬에서 만든다 |
| `Runtime/View/Debug/FakeMatchSession.cs` (수정) | 작은 물건에 같은 구역 슬롯을 차례로 짝지어 `HomeSlotId`를 준다. 제자리 정리는 자기 슬롯 근처에서만 된다 |
| `Runtime/View/Debug/DebugItemView.cs` | **삭제** (이번 표시로 대체) |
| 프리팹 `IT_HeavyDebris_01`, `IT_Trash_01`, `IT_Clutter_{Book,Cup,Document,Chair}`, `IT_FloorStain_01`(텍스처 전용), `IT_Mop` | 표준 구조, 콜라이더 없음(선택은 규칙 데이터로 한다) |

## 연결 및 확인
- `MatchBootstrap`은 층의 `HomeSlotAuthoring`에서 슬롯 ID와 종류를 수집한다. 가짜 판은 구역별 생성 순서로 슬롯 하나씩을 배정한다. 슬롯이 부족한 개체는 ID가 null이며 제자리 정리에 성공하지 않는다.
- `SYS_Bootstrap`의 `ContaminationView`는 LateUpdate에서 Items와 걸레 소지를 읽는다. 보이는 ID만 생성하고 숨겼던 인스턴스는 다시 사용한다. 걸레의 상대 위치는 ViewSettings의 MopOffset으로 조정한다.
- Unity 메뉴 `Tools > UnityGameJam > B4 > 오염 프리팹 생성 및 연결`에서 기본 프리팹과 매핑을 준비한다. 바닥 얼룩은 ArtSlot의 텍스처 전용 모드를 사용한다.
- 실제 판은 `SYS_Bootstrap`에 연결된 `MatchFactory`에서 생성한다. `ContaminationItem.Snapshot`이 기존 계약의 `HomeSlotId`를 전달하므로 초기 목록과 운반 이벤트 모두 같은 슬롯의 외형을 사용한다. 회귀 테스트는 `MatchSimulationTest.작은_물건의_제자리_슬롯이_초기_목록과_운반_이벤트에_유지된다`다.

## 미정·보류
- (없음)

## 참조 테이블
| 테이블 | 컬럼 | 용도 |
|---|---|---|
| (B) `ViewSettingsSO` | HeavyDebrisPrefabs, TrashPrefabs, StainPrefabs, ClutterPrefabs(Kind, Prefab), MopPrefab, CarryOffset | 표시 |

## 미결 질문
- (없음)

## 테스트 시나리오 (`Test/View/ContaminationViewRulesTest.cs`, `Test/View/FakeMatchSessionTest.cs`, `Test/ContractSmokeTest.cs`)
| 규칙 | 조건 | 행동 | 기대 결과 | 테스트 메서드 이름 |
|---|---|---|---|---|
| 2 | 상태 6가지 | 보이는지 | 바닥·배출 구역·운반 중만 보임 | `바닥과_배출_구역과_운반_중일_때만_보인다` |
| 2 | 손 본이 없는 운반자의 위치와 키 | 대체 운반 위치 | 키 + 오프셋 높이 | `운반_중이면_운반자_머리_위에_있다` |
| 4 | ID 5, 프리팹 2개 | 고르기 | 1번 | `프리팹은_ID를_개수로_나눈_나머지로_고른다` |
| 5 | 슬롯 3 = Cup | 종류 | Cup | `작은_물건은_제자리_슬롯의_종류를_따른다` |
| 5 | 슬롯 없음 / 모르는 슬롯 | 종류 | null(첫 번째 사용) | `제자리_슬롯이_없으면_종류를_정하지_않는다` |
| 7 | HomeSlotId 지정 / 생략 | 생성 | 7 / null | `개체_스냅샷은_제자리_슬롯을_담을_수_있고_기본값은_없음이다` |
| 5 | 가짜 판 작은 물건 | 생성 | 같은 구역 슬롯과 짝 | `작은_물건은_같은_구역의_제자리_슬롯과_짝을_짓는다` |
| 1~6 | 실제 씬 플레이 | 줍기·이동 | 개체가 보이고, 든 물건이 플레이어를 따라감 | (플레이 모드 확인) |
