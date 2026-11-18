# 프리팹 배치 규칙

> 씬에 오브젝트를 놓거나 프리팹을 만들고 고치기 전에 읽는다. 소유: B 레인(심지섭).
> 목적: 씬 파일을 거의 고치지 않게 해서 병합 충돌을 줄이고, 아트가 들어왔을 때 프리팹만 바꾸면 모든 씬에 반영되게 한다.

## 1. 원칙
- **씬에 놓는 모든 오브젝트는 프리팹 인스턴스다.** 카메라, 조명, Global Volume, EventSystem, Bootstrap도 프리팹(`SYS_`)으로 만든다.
- 예외는 **컴포넌트가 Transform뿐인 정리용 빈 오브젝트**(이름 예: `--- Floor1 ---`) 하나다.
- 씬에서 새 오브젝트가 필요하면 먼저 프리팹을 만들고, 그 프리팹을 배치한다. 씬에서 만든 오브젝트를 나중에 프리팹으로 바꾸지 않는다.
- 기존 `1_Scene/SampleScene.unity`는 이 규칙에서 뺀다. 게임 씬(`1_Scene/Facility.unity`)부터 적용한다.

## 2. 이름과 폴더
이름 형식: `<접두사>_<이름>[_<변형>]`. 영어 파스칼 케이스로 쓰고, 변형은 두 자리 숫자(`01`)나 단어(`Book`)로 쓴다.

| 접두사 | 뜻 | 폴더 (`4_Prefab/`) | 아트 적용 대상 |
|---|---|---|---|
| `CH_` | 캐릭터: 플레이어, 졸개, 마왕 | `Character/` | O |
| `IT_` | 오염 개체와 들 수 있는 도구: 잔해, 쓰레기, 작은 물건, 얼룩, 걸레 | `Item/` | O |
| `PR_` | 소품과 시설물: 가구, 걸레걸이, 배출함, 제자리 받침 | `Prop/` | O |
| `ENV_` | 환경 조각: 바닥, 벽, 곡선 벽, 문, 계단, 현관, 화장실 | `Environment/` | O |
| `FL_` | 층 조립품: `FL_Floor1`~`FL_Floor3` | `Floor/` | X (안의 프리팹에 적용됨) |
| `MK_` | 보이지 않는 저작용 마커: 구역 볼륨, 생성 지점, 순찰 지점, 제자리 슬롯, 시작점 | `Marker/` | X |
| `UI_` | UI 패널과 HUD | `UI/` | X |
| `SYS_` | 시스템: Bootstrap, 카메라, 조명, Global Volume, EventSystem | `System/` | X |

- 예: `CH_Goblin`, `IT_HeavyDebris_01`, `IT_Clutter_Book`, `PR_HomeSlot_Book`, `ENV_Wall_Curve`, `MK_SpawnPoint`, `SYS_MainCamera`
- 베이스 프리팹은 이름 끝에 `Base`를 붙인다: `CH_MinionBase`, `IT_ClutterBase`. 베이스는 직접 배치하지 않는다.
- **아트 적용 대상(`CH_`, `IT_`, `PR_`, `ENV_`)은 프리팹 이름이 곧 ArtKey다.** 이름은 `docs/rules/art-asset.md`의 ArtKey 목록을 그대로 쓴다.

## 3. 아트를 받는 프리팹의 표준 구조
```
CH_Goblin (루트)          ← 로직: Collider, NavMeshAgent, 게임 스크립트, 레이어
└─ Visual                 ← ArtSlot 컴포넌트 (회전·오프셋·크기 보정)
   ├─ Placeholder         ← 화이트박스 도형. 모델이 들어오면 꺼진다(지우지 않음)
   └─ Model               ← ArtBinder 메뉴가 넣는 모델 인스턴스. 손으로 넣거나 고치지 않는다
```
- Collider, NavMesh 컴포넌트(Agent, Obstacle, Surface), 게임 스크립트는 **루트에만** 붙인다. `Visual` 아래에는 렌더링 관련 컴포넌트(MeshRenderer, Animator 등)만 둔다. 그래서 모델을 바꿔도 게임 로직이 깨지지 않는다.
- 게임 스크립트가 `Visual` 아래 오브젝트를 이름이나 경로로 찾지 않는다. 애니메이션이 필요하면 `ArtSlot.Animator`를 쓴다.
- `Placeholder`의 크기는 `art-asset.md`의 권장 크기에 맞춘다. 콜라이더도 이 크기에 맞춘다.
- `MK_`, `UI_`, `SYS_`, `FL_`에는 이 구조가 필요 없다.

## 4. Variant와 중첩
- 공통 구조는 **베이스 프리팹 + Prefab Variant**로 만든다.
  - `CH_MinionBase` → `CH_Goblin`, `CH_Orc`, `CH_Skeleton`, `CH_Slime`, `CH_Hellhound`
  - `IT_ClutterBase` → `IT_Clutter_<물건>`, `IT_DebrisBase` → `IT_HeavyDebris_01~`
- 층은 중첩 프리팹으로 만든다. `FL_Floor1` 안에 `ENV_`, `PR_`, `MK_` 인스턴스를 넣는다. 방을 고칠 때는 `FL_Floor1`을 Prefab Mode로 열어서 고친다.
- UI는 `UI_HudRoot` 안에 패널마다 별도의 프리팹(`UI_ZoneHud`, `UI_ProposalStack` 등)을 중첩한다. 패널마다 주인을 따로 둘 수 있다.

## 5. 씬에서 허용하는 오버라이드
| 허용 | 금지 (프리팹에 Apply하거나 Variant를 만든다) |
|---|---|
| Transform(위치, 회전, 크기), 오브젝트 이름 | 컴포넌트 추가·삭제, 자식 추가·삭제 |
| 인스턴스마다 달라야 하는 저작 값(예: `MK_ZoneVolume`의 EZoneId, `MK_SpawnPoint`의 오염 유형, `MK_HomeSlot`의 SlotId) | 머티리얼 교체, 콜라이더 크기, 스크립트의 일반 설정 값 |

## 6. 만드는 방법 (에이전트)
- 프리팹 생성과 수정은 에디터 API(`PrefabUtility.SaveAsPrefabAsset`, `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`/`UnloadPrefabContents`, `PrefabUtility.InstantiatePrefab`)로 한다. `.prefab`과 `.unity` YAML을 텍스트로 고치지 않는다.
- 씬에 배치할 때는 `PrefabUtility.InstantiatePrefab`을 쓴다. `Object.Instantiate`를 쓰면 프리팹 연결이 끊긴다.
- Codex처럼 에디터를 다룰 수 없는 에이전트는 `2_Script/Editor/`에 메뉴 도구를 만들고, 사람에게 실행을 맡긴다.

## 7. 체크리스트
- [ ] 씬 루트가 모두 프리팹 인스턴스이거나 빈 정리용 오브젝트다(`Tools > UnityGameJam > Art > 검사`의 "씬 규칙" 항목)
- [ ] 새 프리팹 이름이 접두사 규칙과 ArtKey 목록을 따른다
- [ ] 아트 대상 프리팹은 `Visual`(ArtSlot)과 `Placeholder`가 있고, 콜라이더는 루트에 있다
- [ ] 씬 오버라이드가 5절 허용 범위 안에 있다
- [ ] 새 프리팹과 폴더의 `.meta`가 있다
