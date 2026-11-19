# 화이트박스 시설 (B1) 스펙

- 상태: **확정** (미결 질문 Q1~Q4 모두 승인. 구현 중 문 좌표를 2m 격자에 맞게 조정)
- 담당: B 레인 · 심지섭
- 근거: `Assets/GDD/GDD/Demon_King_Cleaning_Gameplay_Spec.md` §2(시설 구조와 평면도), §3.2(구역별 시작 오염 성격), §7.1(화장실·물걸레), §7.2(현관 배출) / `Undecided.md` U8(구역 밖 귀속), U202(작은 물건 목록), U204(평면도 치수) / `docs/rules/prefab.md`, `docs/rules/art-asset.md`
- 범위: 이번에 하는 것은 3개 층의 화이트박스 프리팹, 저작 마커, NavMesh, 씬 마커 → `FacilityLayout` 추출, `Facility.unity` 구성이다. 하지 않는 것은 플레이어(B2), 졸개 몸과 계단 이동(B3), 오염 표시(B4)다.
- 네임스페이스: `UnityGameJam.View`(Authoring), `UnityGameJam.Editor`(저작 보조 도구가 필요하면)

## 목적
GDD §2 평면도를 실제로 걸을 수 있는 공간으로 만든다. 규칙(A 레인)이 쓸 배치 데이터(`FacilityLayout`)를 씬에서 뽑을 수 있게 한다.

## 입력 / 출력
| 구분 | 이름 | 설명 |
|---|---|---|
| 입력 | GDD §2 평면도 | 방의 위치 관계 (스케치 이미지 없음, 치수는 이 스펙에서 정함) |
| 출력 | `FL_Floor1~3` 프리팹 | ENV_/PR_/MK_ 인스턴스로만 조립 |
| 출력 | `FacilityLayout` | 구역, 층 범위, 화장실, 순찰 경로, 생성 지점, 제자리 슬롯, 시작점, 배출 구역 |
| 출력 | `Facility.unity` | SYS_ + FL_ 프리팹만 배치, 빌드 목록 첫 번째 |

## 치수 (U204, 2m 격자)
- 층마다 바깥 범위는 36x28m(X −18~18, Z −14~14)이다. 높이는 벽 3m, 벽 두께 0.2m다.
- 층은 X축으로 100m씩 떨어뜨린다: 1층 X+0, 2층 X+100, 3층 X+200. 표의 좌표는 층 기준 로컬 좌표다. 같은 화면에 다른 층이 보이지 않는다.
- 계단은 모든 층에서 **오른쪽 위 X 14~18, Z 8~14**다. 아래쪽(Z=8)으로 열린다.

| 층 | 구역·시설 | 범위 X / Z | 문 (폭 2m, 구간) | GDD 근거 |
|---|---|---|---|---|
| 1층 | 다목적실 `MultipurposeRoom` | −12~12 / 4~14 | 아래 벽 X 0~2 | 위쪽 큰 방 |
| 1층 | 휴게실 `Lounge` | −18~−8 / −14~−4 | 오른쪽 벽 Z −10~−8 | 왼쪽 아래 |
| 1층 | 화장실 (판정 제외) | −18~−12 / −4~2 | 오른쪽 벽 Z −2~0 | 왼쪽 아래 |
| 1층 | 창고 `Storage` (오른쪽 두 모서리 곡선) | 10~18 / −10~2 (위쪽 Z 2~4는 계단 쪽으로 가는 로비 통로) | 왼쪽 벽 Z −4~−2 | 오른쪽 곡선 방 |
| 1층 | 현관 (1층 로비에 포함) | −2~2 / −14~−11, 바깥 아래 벽 X −2~2에 출입구(`ENV_Entrance`) | - | 아래 가운데 현관 |
| 1층 | 배출 구역 (1층 로비 안) | 4~8 / −14~−11 | - | 별도의 폐기물 배출 구역 |
| 2층 | 회의실 `MeetingRoom` | −18~−6 / −14~−2 | 위 벽 X −10~−8 | 왼쪽 아래 |
| 2층 | 화장실 | −18~−12 / −2~4 | 오른쪽 벽 Z 0~2 | 회의실 위쪽 |
| 2층 | 작업실 `Workshop` | 6~18 / −14~−2 | 위 벽 X 10~12 | 오른쪽 아래 |
| 3층 | 간부실 `ExecutiveRoom` (위쪽 두 모서리 곡선) | −14~14 / 0~14 | 아래 벽 X 0~2 | 위쪽 큰 곡선 방 |
| 3층 | 화장실 | −18~−12 / −14~−8 | 위 벽 X −16~−14 | 아래쪽 |

- **로비 구역** = 층 범위 − 방 − 화장실 − 계단. 이 영역을 겹치지 않는 사각형 여러 개로 나눠 `ZoneArea.Areas`에 넣는다. 1층 로비는 현관과 배출 구역을 포함한다.
- **곡선 벽**은 `ENV_Wall_Curve`(반지름 4m 1/4 원호)를 모서리마다 하나씩 둔다. 구역 사각형은 곡선 바깥 모서리까지 포함하지만, 그 부분은 걸을 수 없다.
- 화장실과 계단은 어느 판정 구역에도 넣지 않는다. 여기에 떨어진 물건은 그 층 로비로 귀속한다(U8, A 레인 규칙).

## 시작점 (1층 로컬)
| 작업자 | 위치 (X, Z) |
|---|---|
| Player | (0, −2) |
| Goblin / Orc | (−2, −4) / (2, −4) |
| Skeleton / Slime | (−2, −6) / (2, −6) |
| Hellhound | (−4, −12) (현관 옆 대기점) |

## 생성 지점 용량 (구역별 최대 자리 수)
GDD §3.2의 경향을 자리 수로 옮겼다. 환산 기준: 적게(나올 수 있음) 2, 조금·적음 3, 보통 5, 많음 6, 매우 많음·주로 8, 가장 많음 10.
**실제 생성 개수는 A 레인의 `ZoneSpawnTableSO`가 정하고, 이 용량을 넘지 않아야 한다.** 표는 씬에서 층 인스턴스에 추가한 마커를 포함한 `Facility.unity`의 용량이다.
| 구역 | 무거운 잔해 | 일반 쓰레기 | 작은 물건 (= 제자리 슬롯 수) | 얼룩 |
|---|---:|---:|---:|---:|
| 1층 로비 | 2 | 10 | 4 | 10 |
| 창고 | 6 | 6 | 2 | 5 |
| 다목적실 | 2 | 10 | 2 | 7 |
| 휴게실 | 2 | 8 | 6 | 9 |
| 2층 로비 | 2 | 10 | 2 | 9 |
| 작업실 | 3 | 6 | 8 | 5 |
| 회의실 | 5 | 6 | 5 | 4 |
| 3층 로비 | 2 | 10 | 2 | 10 |
| 간부실 | 3 | 11 | 8 | 8 |

- **작은 물건 목록 (U202)**: `Book`, `Cup`, `Document`, `Chair`. 방마다 네 종류를 차례로 돌려 슬롯에 배정한다. `PR_HomeSlot_<물건>`(받침)과 `MK_HomeSlot`(SlotId)을 한 자리에 둔다.

## 규칙
1. `FacilityLayout.Zones`에는 `EZoneId` 9개가 하나씩 있고, 각 구역의 `Floor`는 `ZoneDisplayName.GetFloor`와 같다.
2. 구역 사각형끼리 겹치지 않는다(같은 구역 안의 사각형끼리도 겹치지 않는다).
3. 화장실은 층마다 1개씩 모두 3개다. 화장실 범위는 어떤 구역 사각형과도 겹치지 않는다. `MopPickupPosition`은 해당 화장실 안의 걸레걸이(`PR_MopRack`) 위치다. 같은 위치에서 빌리고 반납한다.
4. 배출 구역은 1층 로비 구역 안에 있다.
5. `StartPositions`에 작업자 6종이 모두 있다.
6. 구역마다 순찰 경로가 1개 있고 점이 3개 이상이며, 모든 점이 그 구역 안에 있다.
7. 생성 지점은 모두 자기 구역 안에 있다. 유형별 개수는 위 용량표와 같다.
8. 제자리 슬롯의 SlotId는 겹치지 않고, 자기 구역 안에 있다. 구역별 개수는 작은 물건 용량과 같다.
9. `Floors`에 층 범위 3개가 있다.
10. 시작점, 순찰점, 생성 지점, 화장실 걸레 위치는 모두 NavMesh 위에 있다(1m 이내에서 샘플링).
11. 한 판에서 플레이어가 아직 들어가지 않은 방은 내부가 보이지 않는다(U13). 플레이어가 그 방의 구역에 들어가면 방 전체가 드러나고, 다른 방이나 층으로 이동해도 방문 기록은 유지된다. 로비·화장실·계단은 처음부터 보인다. 졸개는 가려진 방에서도 작업할 수 있고, 졸개의 이동은 방을 공개하지 않는다. 다시 하기로 새 판을 시작하면 방문 기록을 초기화한다.

## 설계
| 파일 | 역할 |
|---|---|
| `Runtime/View/Authoring/ZoneVolumeAuthoring.cs` | `EZoneId` + BoxCollider(isTrigger) 하나. 한 구역에 여러 개 둘 수 있다 |
| `Runtime/View/Authoring/SpawnPointAuthoring.cs` | `EContaminationType`. 자기 구역은 위치로 판정한다 |
| `Runtime/View/Authoring/PatrolRouteAuthoring.cs` | `EZoneId` + 자식 Transform 순서가 순찰 순서 |
| `Runtime/View/Authoring/HomeSlotAuthoring.cs` | SlotId, 물건 종류(문자열 Book 등) |
| `Runtime/View/Authoring/StartPointAuthoring.cs` | `EWorkerType` |
| `Runtime/View/Authoring/ToiletAuthoring.cs` | `EFloor` + BoxCollider(범위) + 걸레 위치 Transform |
| `Runtime/View/Authoring/DischargeAreaAuthoring.cs` | BoxCollider(범위) |
| `Runtime/View/Authoring/FloorAuthoring.cs` | `FL_` 루트. `EFloor` + 층 범위 |
| `Runtime/View/Authoring/FacilityLayoutBuilder.cs` | 정적 클래스. `FloorAuthoring` 3개(와 그 자식 마커)를 받아 `FacilityLayout`을 만든다. Bootstrap(B2)은 씬의 층 인스턴스 3개를 인스펙터로 연결해 호출한다(Find 사용 안 함) |
| `Runtime/View/RoomVisibility.cs`, `SYS_RoomCover` | 판 시작 시 방 구역의 각 사각형에 가림 프리팹을 생성한다. 가림에는 렌더러만 두고, 플레이어가 들어가면 같은 방의 가림을 모두 끈다. |
| `UnvisitedRoom.shader`, `SYS_MainCamera` | 카메라 깊이 텍스처에서 화면에 보이는 표면의 월드 좌표를 읽는다. 미방문 방 안의 표면만 가려 문 밖 로비의 플레이어를 볼 수 있다. |
| `Runtime/View/Authoring/StairAuthoring.cs` | 계단 마커(층, 출입 지점). B3의 StairPortal이 쓴다 |
| `Runtime/View/Authoring/FacilityLayoutValidator.cs` | 규칙 1~9 검사 (순수, 테스트 대상) |
| 프리팹 `MK_*` | 위 Authoring 하나씩 + Gizmo(구역별 색). 보이지 않는다 |
| 프리팹 `ENV_*` | `ENV_FloorTile`(2x2m), `ENV_Wall_Straight`(2m), `ENV_Wall_Corner`, `ENV_Wall_Curve`(r4m 1/4호), `ENV_Door`(2m 문틀), `ENV_Stairs`(4x6m), `ENV_Entrance`, `ENV_Toilet`(바닥 표시). Visual/ArtSlot/Placeholder 표준 구조 |
| 프리팹 `PR_*` | `PR_MopRack`, `PR_DischargeBin`, `PR_HomeSlot_{Book,Cup,Document,Chair}`, `PR_Desk`, `PR_Shelf`, `PR_Sofa`, `PR_Table` |
| 프리팹 `FL_Floor1~3` | FloorAuthoring + NavMeshSurface + 위 인스턴스 |
| 프리팹 `SYS_*` | `SYS_MainCamera`(임시 고정 탑뷰, 추적은 B2), `SYS_DirectionalLight`, `SYS_GlobalVolume` |
| `Editor/Facility/WhiteboxFloorBuilder.cs` | 메뉴 `Tools > UnityGameJam > Facility`. 치수표대로 `FL_Floor1~3`을 다시 조립(덮어쓰기)하고 NavMesh를 굽는다. 손으로 고친 뒤에는 "층 NavMesh 다시 굽기"만 쓴다 |
| `Facility.unity` | `--- System ---`(SYS_), `--- Floor ---`(FL_ 3개). 빌드 목록 첫 번째 |

**흐름**: 치수표 → 프리팹 조립(B1-3~5) → NavMesh 베이크(B1-6, 층 프리팹 안에서 굽고 데이터는 `4_Prefab/Floor/NavMesh/`) → `FacilityLayoutBuilder.Build()` → `FacilityLayoutValidator.Validate()`로 규칙 검사(B1-7) → 씬 구성(B1-8)

**레이어**: ENV/PR은 Default. MK는 Ignore Raycast 레이어이고, 콜라이더가 있으면 isTrigger다. NavMeshSurface(Physics Colliders, 자식만 수집)는 Ignore Raycast 레이어를 빼고 굽는다. 졸개와 플레이어 레이어는 B2·B3에서 정한다.

**조각 피벗**: 바닥 타일은 한 모서리(타일이 +X, +Z로 2m 뻗음), 직선 벽은 한쪽 끝(+X로 2m), 문틀도 한쪽 끝, 곡선 벽은 원호의 중심(0~90° 구간, +X에서 +Z 방향)이다. 콜라이더는 루트나 `Colliders` 자식에 두고 `Visual` 아래에는 두지 않는다.

## 예외 처리
- 판정 구역 밖(화장실, 계단)은 레이아웃에서 따로 표시하지 않는다. A 레인이 `Floors`와 `Zones`로 판정한다.
- 마커가 중복되거나 빠지면 `Validate`가 위반 목록을 돌려주고, B1-7 테스트가 실패한다.

## 미정·보류
- U204 평면도 치수: **결정** (이 스펙의 치수표)
- U202 작은 물건 목록: **결정** (Book, Cup, Document, Chair)
- **후속 계약 (B4 전)**: `ItemSnapshot`에 `HomeSlotId`가 없다. 그래서 작은 물건이 어떤 종류(Book 등)로 보여야 하는지 B가 알 수 없다. B4 전에 계약 PR로 `int? HomeSlotId`를 추가한다(김보경과 협의).

## 참조 테이블
| 테이블 | 컬럼 | 용도 |
|---|---|---|
| (A) `ZoneSpawnTableSO` | 구역별·유형별 최대 개수 | 위 용량표를 넘지 않게 맞춘다 |
| (B) `ViewSettingsSO` | - | B1에서는 쓰지 않음 (B2부터) |

## 미결 질문
- (없음) Q1 치수표, Q2 작은 물건 4종, Q3 조각 규격(2x2m 타일, 2m 벽, r4m 곡선), Q4 용량표 공유: 모두 승인됨

## 테스트 시나리오 (`Test/View/FacilityLayoutValidatorTest.cs`, `Test/View/FacilitySceneTest.cs`)
| 규칙 | 조건 | 행동 | 기대 결과 | 테스트 메서드 이름 |
|---|---|---|---|---|
| 1 | 구역 하나가 빠진 레이아웃 | 검사 | 위반 | `판정_구역이_하나라도_빠지면_위반이다` |
| 2 | 구역 사각형 두 개가 겹침 | 검사 | 위반 | `구역_사각형이_겹치면_위반이다` |
| 3 | 화장실이 구역과 겹침 | 검사 | 위반 | `화장실이_판정_구역과_겹치면_위반이다` |
| 4 | 배출 구역이 1층 로비 밖 | 검사 | 위반 | `배출_구역이_1층_로비_밖이면_위반이다` |
| 5 | 시작점 5개 | 검사 | 위반 | `작업자_시작점이_빠지면_위반이다` |
| 6 | 순찰점이 구역 밖 | 검사 | 위반 | `순찰점이_구역_밖이면_위반이다` |
| 7·8 | 생성 지점·슬롯이 구역 밖, SlotId 중복 | 검사 | 위반 | `생성_지점이_구역_밖이면_위반이다`, `제자리_슬롯_ID가_겹치면_위반이다` |
| 1~9 | 실제 `Facility.unity` | 추출 → 검사 | 위반 0, 추출 경고 0 | `Facility_씬의_배치_데이터는_규칙을_모두_만족한다` |
| 7·8 | 실제 `Facility.unity` | 구역별 개수 세기 | 용량표와 같음 | `구역별_생성_지점과_제자리_슬롯_수가_용량표와_같다` |
| 10 | 실제 씬 + NavMesh | 샘플링 | 모든 점이 NavMesh 위, 같은 층 계단에서 경로가 끝까지 이어짐 | `모든_저작_지점이_NavMesh_위에_있고_계단에서_갈_수_있다` |
| 11 | 한 방에 구역 사각형 두 개, 다른 층에 방 하나 | 한 구역 진입 후 로비 복귀, 새 판 시작 | 방문한 방 전체만 공개되며 새 판에서는 다시 가려짐 | `RoomVisibilityTest` |
