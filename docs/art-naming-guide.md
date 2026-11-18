# 아트 에셋 이름 규칙 안내

> 3D 모델, 텍스처, 애니메이션을 만들어 넘기는 사람(아티스트)과 받는 사람(심지섭)을 위한 안내서다.
> 규칙 원본은 `docs/rules/art-asset.md`다. 둘이 다르면 원본을 따르고 이 문서를 고친다.

## 1. 핵심 한 줄
모델 파일에는 **원본 ArtKey**를 쓴다. 보통 `CH_Slime.fbx`처럼 게임 프리팹 이름과 같다. 다른 이름의 원본을 쓰는 역할은 받는 쪽에서 `Visual`의 `ArtSlot.ArtKeyOverride`로 연결한다. 해당 원본을 교체할 때에는 아래 5절의 원본 이름을 유지한다.

## 2. ArtKey 만드는 법
```
CH _ Goblin
│     └─ 이름: 영어, 대문자로 시작하는 파스칼 케이스 (HeavyDebris, HomeSlot)
└─ 접두사: 무엇인지
```
```
IT _ HeavyDebris _ 01
                  └─ 변형(선택): 같은 종류를 여러 개 만들 때 두 자리 숫자 또는 단어 (01, 02, Book, Cup)
```

| 접두사 | 분류 | 넣는 폴더 | 예 |
|---|---|---|---|
| `CH_` | 캐릭터 | `5_Art/Model/Character/` | `CH_Goblin`, `CH_DemonKing` |
| `IT_` | 아이템 (들고 다니는 것, 오염) | `5_Art/Model/Item/` | `IT_Trash_01`, `IT_Mop` |
| `PR_` | 소품, 가구, 시설물 | `5_Art/Model/Prop/` | `PR_Desk`, `PR_MopRack` |
| `ENV_` | 환경 조각 (바닥, 벽, 문, 계단) | `5_Art/Model/Environment/` | `ENV_Wall_Curve`, `ENV_Stairs` |

**규칙**
- 영어와 숫자만 쓴다. 한글, 공백, 하이픈(`-`), 점(`.`)은 쓰지 않는다.
- 접두사는 대문자로 쓰고, 나머지 단어는 대문자로 시작한다.
- `_final`, `_v2`, `_new` 같은 버전 표시는 붙이지 않는다. 고친 파일은 **같은 이름으로 덮어쓴다**. 이전 버전은 git이 기억한다.

## 3. 파일 종류별 이름
| 종류 | 형식 | 예 |
|---|---|---|
| 모델 | `<ArtKey>.fbx` | `CH_Skeleton.fbx` |
| 애니메이션 (클립 하나에 파일 하나) | `<ArtKey>@<클립>.fbx` | `CH_Skeleton@Walk.fbx` |
| 색 텍스처 | `<ArtKey>_BaseMap.png` | `CH_Skeleton_BaseMap.png` |
| 노멀맵 | `<ArtKey>_Normal.png` | `CH_Skeleton_Normal.png` |
| 마스크 (R = 금속성, A = 매끈함) | `<ArtKey>_Mask.png` | `CH_Goblin_Mask.png` |
| 발광 | `<ArtKey>_Emission.png` | `CH_Goblin_Emission.png` |
| 머티리얼이 여러 개일 때의 텍스처 | `<ArtKey>_<재질>_<맵>.png` (`<재질>` = FBX 안의 머티리얼 이름) | `PR_Desk_Top_BaseMap.png`, `PR_Desk_Leg_BaseMap.png` |

- 텍스처는 `.png`를 권장한다(`.tga`, `.jpg`도 됨). PSD 원본은 저장소에 넣지 않는다.
- 머티리얼(`M_…`)과 애니메이션 컨트롤러(`AOC_…`)는 **만들지 않는다**. 도구가 자동으로 만든다.

**별도 파일로 전달하는 애니메이션의 클립 이름**
| 클립 | 쓰는 곳 |
|---|---|
| `Idle` | 서 있기 |
| `Walk` | 걷기 |
| `Carry` | 가벼운 물건 들고 걷기 |
| `CarryHeavy` | 무거운 잔해 들고 걷기 |
| `Mop` | 물걸레질 |
| `Eat` | 슬라임이 먹기·소화 |
| `Talk` | 대화(배정) 중 |
| `Dispatch` | 헬하운드 배출 |

게임 캐릭터 7종은 모델 FBX 안에 든 원래 클립 이름을 그대로 쓴다. 예를 들어 플레이어는 `Idle_12`, `Walking`, `Running`으로 대기·걷기·달리기를 구분한다. 기존 모델을 갱신할 때는 클립 이름과 본 경로를 유지하고, Unity에서 Art 메뉴의 전체 적용을 다시 실행한다. 별도 `@` 파일에 새 행동 이름을 추가할 때는 먼저 심지섭과 합의한다.

## 4. 좋은 예와 나쁜 예
| O | X | 이유 |
|---|---|---|
| `CH_Skeleton.fbx` | `skeleton.fbx` | 접두사가 없고 소문자로 시작한다 |
| `CH_Skeleton.fbx` | `CH_Skeleton_final.fbx` | 버전 표시를 붙였다 |
| `IT_HeavyDebris_01.fbx` | `IT_Heavy Debris 1.fbx` | 공백이 있다 |
| `ENV_Wall_Curve.fbx` | `ENV-Wall-Curve.fbx` | 하이픈을 썼다 |
| `CH_Skeleton@Walk.fbx` | `CH_Skeleton_Walk.fbx` | 애니메이션은 `@`로 구분해야 한다 |
| `CH_Skeleton@Walk.fbx` | `CH_Skeleton@walking.fbx` | 클립 목록에 없는 이름이다 |
| `CH_Slime_BaseMap.png` | `CH_Slime_Diffuse.png` | 맵 이름은 BaseMap, Normal, Mask, Emission 중 하나다 |

## 5. 지금 필요한 ArtKey (요약)
납품 번호 A01~W07과 원본 파일명의 전체 대응은 [3D 아트 에셋 규칙 4절](rules/art-asset.md#4-artkey-목록)에 있다.
- **캐릭터**: `CH_Player`, `CH_Goblin`, `CH_Orc`, `CH_Skeleton`, `CH_Slime`, `CH_Hellhound`, `CH_DemonKing`
- **아이템**: `IT_HeavyDebris_01`~, `IT_Trash_01`~, `IT_Clutter_<물건>`, `IT_FloorStain_01`~(텍스처만), `IT_Mop`
- **소품**: `PR_MopRack`, `PR_DischargeBin`, `PR_HomeSlot_<물건>`, `PR_<가구>`
- **환경**: `ENV_FloorTile`, `ENV_Wall_Straight`, `ENV_Wall_Corner`, `ENV_Wall_Curve`, `ENV_Door`, `ENV_Stairs`, `ENV_Entrance`, `ENV_Toilet`(화장실 방 바닥 표식), `ENV_ToiletFixture`(W02 변기)
- 현재 제자리 정리에 쓰는 물건은 `Book`, `Cup`, `Document`, `Chair`다. 예를 들어 `IT_Clutter_Book`(책)은 `PR_HomeSlot_Book`(책꽂이)에 돌려놓는다.
- 목록에 없는 새 ArtKey가 필요하면 먼저 목록에 추가한다. 그래야 게임 프리팹도 같은 이름으로 만들어진다.

**역할별로 교체할 원본 FBX**

| 게임 프리팹 | 전달할 원본 |
|---|---|
| 플레이어 `CH_Player` | `CH_RamHornMaidPrincess.fbx` |
| 마왕 `CH_DemonKing` | `CH_CrimsonHoodedMage.fbx` |
| 고블린 `CH_Goblin` | `CH_GoblinSparkplug.fbx` |
| 오크 `CH_Orc` | `CH_GoblinTrailblazer.fbx` |
| 무거운 잔해 `IT_HeavyDebris_01` | H01 `IT_HeavyDebris_BrokenPlankBundle.fbx` |
| 일반 쓰레기 `IT_Trash_01` | G01 `IT_Trash_CrumpledPaperCup.fbx` |
| 현관 배출 구역 `PR_DischargeBin` | T01 `PR_WasteDropOffPlatform.fbx` |
| 방 출입구의 통행용 문틀 `ENV_Door` | A05 `ENV_ArchedDoorFrame.fbx` |

A06 나무 문짝은 `ENV_WoodenDoorLeaf.fbx`와 같은 이름의 독립 프리팹으로 전달한다. 일반 책상 원본은 `PR_Desk.fbx`, F26 간부실 책상 원본은 `PR_ExecutiveDesk.fbx`로 구분한다.

## 6. 내보내기 체크리스트
- [ ] 단위는 미터, 스케일은 1
- [ ] 정면은 +Z, 위쪽은 Y (Blender는 FBX 내보내기에서 축을 맞춘다)
- [ ] 피벗: 캐릭터, 아이템, 소품은 **바닥 중앙**. 벽, 바닥, 문은 **1m 격자에 맞춘 모서리**
- [ ] 메시는 가능하면 하나, 머티리얼은 2개 이하
- [ ] 캐릭터는 Generic 리그이며, 게임 캐릭터용 FBX에는 해당 행동의 원본 클립이 들어 있다
- [ ] 크기는 아래 권장 높이에 가깝게

| 대상 | 높이 | 대상 | 높이 |
|---|---|---|---|
| 플레이어 | 1.7m | 슬라임 | 0.8m |
| 고블린 | 1.0m | 헬하운드 | 1.2m (어깨) |
| 오크 | 2.0m | 마왕 | 3.0m |
| 스켈레톤 | 1.7m | 벽, 문 | 3.0m (벽 두께 0.2m) |

- [ ] 바닥 얼룩은 메시 없이 알파가 있는 `IT_FloorStain_01_BaseMap.png`만 준다

## 7. 넘기는 방법
1. ArtKey마다 폴더를 하나 만들고, 그 안에 모델, 애니메이션, 텍스처를 함께 담는다.
   ```
   CH_Skeleton/
   ├─ CH_Skeleton.fbx
   ├─ CH_Skeleton@Idle.fbx
   ├─ CH_Skeleton@Walk.fbx
   ├─ CH_Skeleton_BaseMap.png
   └─ CH_Skeleton_Normal.png
   ```
2. 저장소에 직접 올리면 PR 본문의 "적용 명세" 표에 파일 이름과 들어갈 프리팹(ArtKey)을 적는다. 파일만 전달할 때도 같은 내용을 메시지로 남긴다.
3. 받는 쪽(심지섭)은 폴더를 `5_Art/Model/<분류>/`에 넣고 `Tools > UnityGameJam > Art > 검사` → `전체 적용`을 누른다.

## 8. 적용 결과 읽는 법
| 결과 창 표시 | 뜻 | 해결 |
|---|---|---|
| ✔ 적용됨 | 게임에 들어갔다 | - |
| ● 적용 가능 | 들어갈 준비가 됐다 | `전체 적용`을 누른다 |
| ○ 모델 없음 | 게임에는 자리가 있는데 파일이 아직 없다 | 파일을 만든다 |
| ? 주인 없음 | 파일은 있는데 같은 이름의 게임 프리팹이 없다 | 이름 오타를 확인하거나 ArtKey 목록에 추가한다 |
| ✖ 이름 오류 | 이름 규칙 위반 | 4절 표를 보고 이름을 고친다 |
| ✖ 중복 | 같은 이름의 파일이 두 개 있다 | 하나만 남긴다 |

크기나 방향이 어긋나면 받는 쪽이 프리팹의 ArtSlot에서 보정할 수 있다. 다만 다음 파일부터는 6절 체크리스트로 맞춰 내보내는 것이 좋다.
