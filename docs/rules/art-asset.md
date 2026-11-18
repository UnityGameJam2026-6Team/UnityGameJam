# 3D 아트 에셋 규칙 (이름 · 전달 규격 · 자동 적용)

> 3D 모델, 텍스처, 애니메이션을 받거나 프리팹에 적용하기 전에 읽는다. 소유: B 레인(심지섭).
> 아티스트용으로 풀어 쓴 안내서는 `docs/art-naming-guide.md`다. 이 문서를 고치면 안내서도 함께 맞춘다.
> 모델 파일 이름은 **원본 ArtKey**다. 프리팹 이름을 기본 키로 쓰며, 다른 키의 모델을 받는 프리팹은 `Visual`의 `ArtSlot.ArtKeyOverride`로 원본을 지정한다. 4절 표에 게임 프리팹과 원본의 대응을 기록한다.

## 1. 이름 규칙
| 종류 | 형식 | 예 |
|---|---|---|
| 모델 | `<ArtKey>.fbx` | `CH_Skeleton.fbx`, `IT_Trash_CrushedDrinkCan.fbx` |
| 에셋 스토어 모델 | 모델 프리팹을 복사해 `<ArtKey>.prefab`으로 이름을 바꾼다 | `PR_Desk.prefab` |
| 애니메이션 | `<ArtKey>@<Clip>.fbx` (Unity 표준 규칙) | `CH_Skeleton@Idle.fbx` |
| 텍스처 | `<ArtKey>[_<재질>]_<맵>.png` (`.tga`, `.jpg`도 됨) | `CH_Skeleton_BaseMap.png`, `PR_Desk_Top_Normal.png` |
| 머티리얼 | `M_<ArtKey>[_<재질>].mat` (**도구가 만든다**, 직접 만들지 않음) | `M_CH_Goblin.mat` |
| 애니메이션 오버라이드 | `AOC_<ArtKey>.overrideController` (**도구가 만든다**) | `AOC_CH_Skeleton.overrideController` |

- **ArtKey** 형식: `<접두사>_<이름>[_<변형>]`. 접두사는 `CH_`(캐릭터), `IT_`(아이템), `PR_`(소품), `ENV_`(환경) 중 하나다. 영어 파스칼 케이스로 쓰고, 변형은 `01`이나 단어로 쓴다.
  - 정규식: `^(CH|IT|PR|ENV)_[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$`
  - O: `CH_Goblin`, `IT_HeavyDebris_01`, `ENV_Wall_Curve` / X: `goblin_final`, `CH-Goblin`, `Ch_Goblin`, `CH_goblin`
- **텍스처 맵** (URP/Lit 기준)
  | 접미사 | 용도 | 임포트 설정 |
  |---|---|---|
  | `_BaseMap` | 색(알파 포함 가능) | sRGB |
  | `_Normal` | 노멀맵 | Normal map 타입 |
  | `_Mask` | R = Metallic, A = Smoothness (URP Lit의 Metallic 맵) | sRGB 끔 |
  | `_Emission` | 발광 | sRGB |
- 모델에 머티리얼이 1개면 텍스처는 `<ArtKey>_<맵>`으로 짓는다. 머티리얼이 여러 개면 FBX 안의 머티리얼 이름을 `<재질>` 자리에 넣는다(`PR_Desk_Top_BaseMap`, `PR_Desk_Leg_BaseMap`).
- **별도 `@<Clip>` 파일**의 클립 이름은 `Idle`, `Walk`, `Carry`, `CarryHeavy`, `Mop`, `Eat`, `Talk`, `Dispatch` 중에서 고른다. 게임 캐릭터 7종의 FBX 내장 클립은 납품된 이름을 그대로 유지한다.

## 2. 폴더
```
5_Art/Model/
├─ Character/
│  ├─ AC_Character.controller      캐릭터 공용 베이스 애니메이터 (도구가 만든다)
│  └─ CH_Skeleton/                 ArtKey마다 폴더 하나
│     ├─ CH_Skeleton.fbx
│     ├─ CH_Skeleton@Idle.fbx
│     ├─ CH_Skeleton_BaseMap.png
│     ├─ M_CH_Skeleton.mat         (도구가 만든다)
│     └─ AOC_CH_Skeleton.overrideController (도구가 만든다)
├─ Item/
├─ Prop/
└─ Environment/
```
- 새로 전달하는 파일의 분류 폴더는 접두사와 맞춘다. `CH_`는 `Character/`, `IT_`는 `Item/`, `PR_`는 `Prop/`, `ENV_`는 `Environment/`를 사용한다. 기존에 `Prop/`에 들어온 A·F·G·H·L·T·W 번호 폴더는 원본 납품 묶음이며, 각 FBX의 분류는 파일 접두사를 따른다.

## 3. 내보내기 규격 (아티스트 전달용)
- 단위는 미터, 스케일 1, 정면은 +Z, 위쪽은 Y(Blender의 Z-up은 FBX 내보내기에서 변환)
- 피벗: 캐릭터, 아이템, 소품은 **바닥 중앙**. 벽, 바닥, 문 같은 환경 조각은 **1m 격자에 맞춘 모서리**
- 오브젝트 하나에 메시 하나를 권장하고, 머티리얼은 2개 이하
- 캐릭터 리그는 Generic으로 한다. 게임 캐릭터 7종은 모델 FBX에 행동 클립을 포함한다. 개별 오버라이드 클립은 `<ArtKey>@<Clip>.fbx`로 내보낸다.
- 얼룩(`IT_FloorStain_*`)은 메시 없이 텍스처만 받는다: `IT_FloorStain_01_BaseMap.png`(알파 포함). 도구가 바닥 쿼드에 붙인다.
- PSD 같은 작업 원본은 저장소에 넣지 않는다(Git LFS 미사용).

**권장 크기** (화이트박스 Placeholder도 이 크기로 만든다)
| ArtKey | 높이 | ArtKey | 높이 |
|---|---|---|---|
| `CH_Player` | 1.7m | `CH_Slime` | 0.8m |
| `CH_Goblin` | 1.0m | `CH_Hellhound` | 1.2m (어깨) |
| `CH_Orc` | 2.0m | `CH_DemonKing` | 3.0m |
| `CH_Skeleton` | 1.7m | 문, 벽 | 3.0m (벽 두께 0.2m) |

## 4. ArtKey 목록
프리팹을 만들 때 이 이름을 그대로 쓴다. 새 ArtKey가 필요하면 이 표에 먼저 추가한다. `Tools > UnityGameJam > Art > 아트 키 목록 내보내기`로 현재 프로젝트의 목록을 뽑아 비교할 수 있다.

| 분류 | ArtKey | GDD 이름 / 용도 | 비고 |
|---|---|---|---|
| Character | `CH_Player` | 플레이어 | 원본 `CH_RamHornMaidPrincess` |
| Character | `CH_Goblin` | 고블린 | `CH_MinionBase` Variant, 원본 `CH_GoblinSparkplug` |
| Character | `CH_Orc` | 오크 | `CH_MinionBase` Variant, 원본 `CH_GoblinTrailblazer` |
| Character | `CH_Skeleton` | 스켈레톤 | `CH_MinionBase` Variant |
| Character | `CH_Slime` | 슬라임 | `CH_MinionBase` Variant |
| Character | `CH_Hellhound` | 헬하운드 | `CH_MinionBase` Variant |
| Character | `CH_DemonKing` | 마왕 (마감 연출) | 원본 `CH_CrimsonHoodedMage` |
| Item | `IT_HeavyDebris_01` | 무거운 잔해 | 대표 원본 H01 `IT_HeavyDebris_BrokenPlankBundle` |
| Item | `IT_Trash_01` | 일반 쓰레기 | 대표 원본 G01 `IT_Trash_CrumpledPaperCup` |
| Item | `IT_Clutter_<물건>` | 어질러진 작은 물건 | U202의 `Book`, `Cup`, `Document`, `Chair`는 같은 이름의 `PR_HomeSlot_<물건>`과 짝 |
| Item | `IT_FloorStain_01`~ | 바닥 얼룩 | 텍스처만 받음 |
| Item | `IT_Mop` | 물걸레 | |
| Prop | `PR_MopRack` | 화장실 걸레걸이 (2개 보관) | |
| Prop | `PR_DischargeBin` | 1층 현관 배출 구역 표시 | 대표 원본 T01 `PR_WasteDropOffPlatform` |
| Prop | `PR_HomeSlot_<물건>` | 작은 물건 제자리 | U202 |
| Prop | `PR_Desk`, `PR_ExecutiveDesk` | 일반 책상, 간부실 책상 | 일반 책상 원본은 `Prop/PR_Desk.fbx`, F26 간부실 책상 원본은 `F26_Executive_Desk/PR_ExecutiveDesk.fbx` |
| Prop | `PR_<가구>` | 방 가구 (선반, 탁자, 소파 등) | F 계열 납품 모델 |
| Environment | `ENV_FloorTile` | 바닥 2x2m (피벗: 한 모서리) | 원본 `ENV_FloorTile_Tiled` (평면, 타일링 색상·노멀 맵) |
| Environment | `ENV_Wall_Straight`, `ENV_Wall_Corner`, `ENV_Wall_Curve` | 벽 2m(피벗: 한쪽 끝), 모서리 기둥, 곡선 벽(반지름 4m 1/4 원호, 피벗: 원호 중심) | 창고, 간부실 |
| Environment | `ENV_Door` | 방 출입구의 통행용 문틀 | 원본 A05 `ENV_ArchedDoorFrame` |
| Environment | `ENV_WoodenDoorLeaf` | 나무 문짝 | A06 독립 프리팹 |
| Environment | `ENV_Stairs` | 계단 | |
| Environment | `ENV_Entrance` | 1층 현관 | |
| Environment | `ENV_Toilet` | 화장실 방 바닥 표식 | |
| Environment | `ENV_ToiletFixture` | 변기 | W02 변기 모델 |

**납품 모델의 변형 키**

아래 이름에 표의 접두사를 붙이면 원본 FBX의 ArtKey가 된다. 게임의 잔해·쓰레기 대표 프리팹에는 위 표의 H01·G01을 연결한다.

| 원본 번호 | 접두사 | 번호 순서의 이름 |
|---|---|---|
| A01~A07 | `ENV_` | `FloorTile`, `Wall_Straight`, `Wall_Corner`, `Wall_Curve`, `ArchedDoorFrame`, `WoodenDoorLeaf`, `Entrance` |
| A08~A14 | `ENV_` | `DoorThreshold`, `Stairs`, `StairLanding`, `StraightSafetyRailing`, `MasonryPillar`, `WallSconce`, `BlankRoomSign` |
| F01~F08 | `PR_` | `ReceptionCounter`, `LobbyBench`, `PlainWasteBin`, `StonePlanter`, `Shelf`, `IntactWoodenChest`, `LoadingPallet`, `Table` |
| F10~F18 | `PR_` | `WallNoticeBoard`, `LowPlatform`, `StackOfFoldedChairs`, `Sofa`, `LowCoffeeTable`, `KitchenetteSink`, `WallShelf`, `Microwave`, `MiniFridge` |
| F19~F28 | `PR_` | `BuiltInCabinet`, `WorkshopBench`, `WorkshopPegboard`, `RoundWorkStool`, `CastleHeraldicBanner`, `DeskLamp`, `WallClock`, `ExecutiveDesk`, `ExecutiveHighBackChair`, `OfficeRug` |
| G01~G06 | `IT_Trash_` | `CrumpledPaperCup`, `CrushedDrinkCan`, `CrumpledPaperBall`, `TornFoodWrapper`, `TiedTrashBag`, `CollapsedCardboardBox` |
| H01~H04 | `IT_HeavyDebris_` | `BrokenPlankBundle`, `BrokenMasonryPile`, `BrokenWoodenChest`, `BrokenFurnitureDebris` |
| L01~L06 | `IT_Clutter_` | `Book`, `Cup`, `Document`, `SmallToolCase`, `RemoteControl`, `SmallStorageBox`. 의자 `IT_Clutter_Chair`는 F09다. |
| T01~T04 | `PR_` | `WasteDropOffPlatform`, `MopRack`, `OrganizingTray`, `HoundWaitingMat`. T05 걸레는 `IT_Mop`이다. |
| W01~W06 | `ENV_` | `PedestalSink`, `ToiletFixture`, `WallMirror`, `BathroomStall`, `WallDispenser`, `FloorDrain`. W07 양동이는 `IT_CleaningBucket`이다. |

## 5. 적용 방법
1. 이름 규칙에 맞춰 파일을 `5_Art/Model/<분류>/<ArtKey>/`에 넣는다. 처음 임포트할 때 임포트 설정이 자동으로 맞춰진다(6절).
2. `Tools > UnityGameJam > Art > 검사` → 결과 창에서 상태를 확인한다.
   | 상태 | 뜻 | 할 일 |
   |---|---|---|
   | 적용됨 | 프리팹에 이 모델이 이미 들어 있다 | 없음 |
   | 적용 가능 | 모델이 있고 아직 적용되지 않았다 | 적용 |
   | 모델 없음 | 프리팹은 있는데 모델이 없다 | 화이트박스 유지 |
   | 주인 없음 | 모델은 있는데 같은 이름의 프리팹이 없다 | 프리팹을 만들거나 파일 이름을 고친다 |
   | 이름 오류 | 이름 규칙 위반이거나 같은 ArtKey가 둘 이상이다 | 파일 이름을 고친다 |
   | 베이스 | `...Base` 프리팹이다. 적용하지 않는다 | 없음 |
3. `전체 적용` 또는 `선택한 프리팹에 적용`을 누른다. 도구가 하는 일:
   - 텍스처가 있으면 `M_<ArtKey>`를 만들고 모델 머티리얼을 그것으로 바꾼다
   - 프리팹의 `Visual/Model`을 새 모델로 교체하고 `Placeholder`를 끈다
   - `<ArtKey>@<Clip>.fbx`가 있으면 `AOC_<ArtKey>`를 만들어 Animator에 연결한다
   - 게임 캐릭터 7종은 해당 모델의 내장 클립을 `Visual/CharacterAnimationSet`에 연결한다. 프리팹 크기로 발의 보행 속도와 집기 클립의 손 접촉·들어 올리기 시각을 측정해 함께 저장한다. 모델이나 클립을 교체했다면 같은 메뉴로 갱신한다.
4. 크기나 축이 맞지 않으면 프리팹의 `Visual`에 있는 **ArtSlot**에서 보정하고 다시 적용한다.
   - `Fit Mode`: None(모델 크기 그대로), MatchPlaceholderHeight(Placeholder 높이에 맞춤), MatchPlaceholderBounds(Placeholder 상자 안에 맞춤)
   - `Model Offset`, `Model Euler`, `Model Scale`: 위치, 회전, 배율 보정
   - 보정 값은 프리팹에 저장되므로 모델 파일을 교체해도 유지된다.
5. 되돌리려면 `플레이스홀더로 되돌리기`를 누른다.
6. 같은 모델로 다시 적용하면 아무것도 바뀌지 않는다. 그래서 다시 실행해도 git diff가 생기지 않는다.
7. 커밋 메시지는 `art: <ArtKey> 모델 적용` 형식으로 쓰고, 새 `.meta`를 함께 넣는다.

## 6. 자동 임포트 설정 (처음 임포트할 때만)
`5_Art/Model/` 아래 파일을 **처음** 임포트할 때만 적용한다. 그 뒤에 Inspector에서 바꾼 값은 유지된다.
- 모델: Scale Factor 1, Convert Units 켬, 콜라이더 생성 끔, Read/Write 끔
- `CH_` 모델과 `@` 애니메이션 파일: Rig Generic, 애니메이션 임포트 켬
- 내장 클립을 쓰는 7개 캐릭터 모델은 아트 적용 시에도 Generic과 애니메이션 임포트를 확인한다.
- 그 밖의 모델: Rig 없음, 애니메이션 임포트 끔
- 텍스처: `_Normal`은 Normal map 타입, `_Mask`는 sRGB 끔

## 7. 문제 해결
- **모델이 누워 있음**: 내보낼 때 축을 확인한다. 급하면 ArtSlot `Model Euler`에서 (-90, 0, 0) 등으로 보정한다.
- **크기가 100배**: 내보낼 때 단위를 확인한다(cm로 내보냈을 수 있음). 급하면 Fit Mode를 MatchPlaceholderHeight로 한다.
- **분홍색 머티리얼**: URP가 아닌 셰이더다. 텍스처 이름을 규칙에 맞추면 도구가 URP/Lit 머티리얼로 바꾼다.
- **"주인 없음"**: 파일 이름의 오타이거나 프리팹이 아직 없는 것이다. ArtKey 목록과 비교한다.
