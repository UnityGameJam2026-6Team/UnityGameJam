# 2인 분업, 프리팹 배치 규칙, 3D 에셋 자동 적용 계획

> 상위 계획: `Assets/Plans/demon-king-cleaning-implementation.md` (게임 전체 구현과 A/B 레인 구조)
> 이 계획은 세 가지를 정한다. 첫째, **누가 무엇을 어떤 에이전트로 돌리는지**. 둘째, **씬에는 프리팹만 배치한다는 규칙**. 셋째, **3D 에셋이 들어오면 이름만 맞춰도 메뉴 한 번으로 프리팹에 적용되는 파이프라인**.
> 결정 사항: 심지섭 = B 레인 + Unity AI, 김보경 = A 레인 + Codex, 에셋 형식은 FBX(에셋 스토어 모델 프리팹도 받음), 적용은 메뉴 버튼 한 번.

# Project Overview
- Game Title: 마왕 도착 전 시설 청소 (가제)
- High-Level Concept: 180초 안에 졸개 5종을 지휘하고 직접 청소해서 9개 구역 60%, 시설 85%를 달성하는 3D 탑다운 지휘 게임
- Players: 싱글 플레이
- Inspiration / Reference Games: 미정 (U17)
- Tone / Art Direction: 1차는 화이트박스. 나중에 FBX 모델로 교체하며, 이 계획의 파이프라인으로 적용한다.
- Target Platform: PC (Windows 64bit)
- Screen Orientation / Resolution: 가로 1920x1080 (U18)
- Render Pipeline: URP (PC_RPAsset)

# Game Mechanics
## Core Gameplay Loop
상위 계획의 "Core Gameplay Loop"와 같다(배정 → 위임·직접 청소 → 제안 응답 → 마감 판정). 이 계획은 게임 규칙을 바꾸지 않는다. 작업 분담과 에셋 제작 흐름만 다룬다.

## Controls and Input Methods
게임 조작은 상위 계획과 같다. 이 계획에서 새로 생기는 조작은 **에디터 메뉴**뿐이다.
- `Tools > UnityGameJam > Art > 검사 (Validate)`
- `Tools > UnityGameJam > Art > 전체 적용 (Apply All)`
- `Tools > UnityGameJam > Art > 선택한 프리팹에 적용 (Apply Selected)`
- `Tools > UnityGameJam > Art > 플레이스홀더로 되돌리기 (Revert Selected)`
- `Tools > UnityGameJam > Art > 아트 키 목록 내보내기 (Export Key List)`

# UI
게임 UI는 상위 계획에서 다룬다. 이 계획의 UI는 **아트 적용 결과 창**(`ArtBindingWindow`, 에디터 전용) 하나다.

```
┌ Art Binding ───────────────────────────────────────────────────────────┐
│ [검사] [전체 적용] [선택 적용] [되돌리기]        필터: [전체 ▾]          │
├────────────┬──────────────┬────────────────────────┬──────────────────┤
│ 상태        │ ArtKey        │ 프리팹                   │ 모델              │
├────────────┼──────────────┼────────────────────────┼──────────────────┤
│ ✔ 적용됨    │ CH_Goblin     │ 4_Prefab/Character/…    │ Model/Character/… │
│ ● 적용 가능 │ CH_Orc        │ 4_Prefab/Character/…    │ Model/Character/… │
│ ○ 모델 없음 │ CH_Slime      │ 4_Prefab/Character/…    │ -                 │
│ ? 주인 없음 │ IT_Trash_02   │ -                       │ Model/Item/…      │
│ ✖ 이름 오류 │ goblin_final  │ -                       │ 규칙: CH_<이름>    │
└────────────┴──────────────┴────────────────────────┴──────────────────┘
 행을 클릭하면 해당 에셋이 Project 창에서 선택된다. 같은 내용을 콘솔에도 요약해서 남긴다.
```
- 상태별 색: 적용됨(초록), 적용 가능(파랑), 모델 없음(회색), 주인 없음(노랑), 이름 오류(빨강)
- "주인 없음": 모델은 있는데 같은 ArtKey의 프리팹이 없다. 프리팹을 만들거나 파일 이름을 고쳐야 한다.

# Key Asset & Context

## 1. 분업: 담당자, 레인, 에이전트
| | **김보경** | **심지섭** |
|---|---|---|
| 레인 | **A: 규칙·시뮬레이션** | **B: 월드·표현** |
| 에이전트 | Codex (`AGENTS.md`를 읽음) | Unity AI, 에디터 안에서 Agent 모드 (`7_Setting/AI/ProjectRule/SKILL.md` → 같은 규칙 문서) |
| 소유 경로 | `Runtime/{Cleaning, Minion, Proposal, Match, Facility, Data}/`, `Test/`(아래 B 하위 폴더 제외), `3_Data/` | `Runtime/{Player, View, UI}/`, `Editor/`, `Test/Art/`, `Test/View/`, `1_Scene/`, `4_Prefab/`, `5_Art/`, `6_Audio/`, Input Actions, `ProjectSettings/` |
| 작업 큐 | A1 청결도·오염 모델 → A2 데이터와 생성 → A3 작업 선택 → A4 도구·배출·소화 → (M1) → A5 졸개 두뇌 → A6 제안 대기열 → A7 판 흐름·마감 | **B0 프리팹·아트 기반(이 계획)** → B1 화이트박스 시설 → B2 플레이어 → B3 졸개 몸 → B4 ContaminationView → (M1) → B5 HUD → B6 대화·제안 UI → B7 마감 연출·결과 |
| 에디터 작업 | 거의 없음. 테스트는 Test Runner나 배치 모드로 돌린다. | 씬, 프리팹, NavMesh, 아트 적용을 Unity AI가 직접 처리한다. |
| 브랜치 예 | `feat/cleaning-model` | `feat/art-pipeline`, `feat/facility-layout` |

- **일정 균형**: B 레인이 B0만큼 늘어난다. 김보경이 A7을 먼저 끝내면 B6과 B7의 **UI 프리팹과 스크립트**를 소유권 이전으로 넘겨받는다. 다만 프리팹 작업에는 에디터가 필요하므로, 넘겨받을 때는 김보경도 Unity AI를 쓰거나 에디터에서 직접 작업한다.
- **Test 폴더 나누기**: `2_Script/Test/`는 A 레인 소유다. B 레인의 에디터 도구 테스트는 `Test/Art/`, `Test/View/` 하위 폴더에만 둔다(collaboration.md 표에 추가).
- **각자의 시작 문서**: `docs/team/lane-a.md`(김보경), `docs/team/lane-b.md`(심지섭). 둘을 따로 둬서 서로 고칠 일이 없게 한다. 각 문서에는 첫 프롬프트, 작업 큐, 커밋 전 체크리스트를 담는다.

## 2. 프리팹 배치 규칙 (`docs/rules/prefab.md`, 새 문서)
**원칙: 씬에 놓는 모든 오브젝트는 프리팹 인스턴스다.** 예외는 컴포넌트가 Transform뿐인 정리용 빈 오브젝트(`--- Floor1 ---` 등) 하나다.

**접두사와 폴더**
| 접두사 | 뜻 | 폴더 (`4_Prefab/`) | 아트 적용 |
|---|---|---|---|
| `CH_` | 캐릭터 (플레이어, 졸개, 마왕) | `Character/` | O |
| `IT_` | 오염 개체와 들 수 있는 도구 (잔해, 쓰레기, 작은 물건, 얼룩, 걸레) | `Item/` | O |
| `PR_` | 소품과 시설물 (가구, 걸레걸이, 배출함, 제자리 받침) | `Prop/` (새로 만듦) | O |
| `ENV_` | 환경 조각 (바닥, 벽, 곡선 벽, 문, 계단, 현관) | `Environment/` (새로 만듦) | O |
| `FL_` | 층 조립품 (`FL_Floor1`~`FL_Floor3`) | `Floor/` | X (안의 프리팹이 적용됨) |
| `MK_` | 보이지 않는 저작용 마커 (구역 볼륨, 생성 지점, 순찰 지점, 제자리 슬롯, 시작점) | `Marker/` (새로 만듦) | X |
| `UI_` | UI 패널과 HUD | `UI/` | X |
| `SYS_` | 시스템 (Bootstrap, 카메라, 조명, Global Volume, EventSystem) | `System/` (새로 만듦) | X |

- 이름 형식: `<접두사>_<이름>[_<변형>]`. 영어 파스칼 케이스로 쓰고, 변형은 `01`처럼 두 자리 숫자나 단어로 쓴다. 예: `CH_Goblin`, `IT_HeavyDebris_01`, `IT_Clutter_Book`, `ENV_Wall_Curve`
- **ArtKey는 프리팹 이름과 같다.** 그래서 `CH_Goblin.prefab`에는 `CH_Goblin.fbx`가 들어간다.

**아트를 받는 프리팹의 표준 구조**
```
CH_Goblin (루트)             ← 로직: Collider, NavMeshAgent, MinionAgent, 레이어. 아트를 바꿔도 그대로 둔다.
└─ Visual                    ← ArtSlot 컴포넌트. 회전·오프셋 보정만 담당한다.
   ├─ Placeholder            ← 화이트박스 도형. 모델이 들어오면 비활성화한다(지우지 않음).
   └─ Model                  ← ArtBinder가 넣는 FBX 인스턴스. 손으로 넣지 않는다.
```
- Collider, NavMesh 관련 컴포넌트, 게임 스크립트는 **루트에만** 붙인다. `Visual` 아래에는 렌더링 관련 컴포넌트(MeshRenderer, Animator)만 둔다. 그래서 모델을 바꿔도 게임 로직이 깨지지 않는다.
- 공통 구조는 **베이스 프리팹 + Prefab Variant**로 만든다: `CH_MinionBase` → `CH_Goblin`, `CH_Orc`, …, `IT_ClutterBase` → `IT_Clutter_Book`, …
- 층은 중첩 프리팹으로 만든다. `FL_Floor1` 안에 `ENV_`, `PR_`, `MK_` 인스턴스를 넣고, 방 수정은 `FL_Floor1`을 Prefab Mode로 열어서 한다.
- **씬에서 허용하는 오버라이드**: Transform, 오브젝트 이름, 인스턴스마다 달라야 하는 저작 값(예: `MK_ZoneVolume`의 EZoneId, `MK_SpawnPoint`의 오염 유형)만 허용한다. 그 밖의 변경은 프리팹에 Apply하거나 Variant를 만든다.
- 프리팹 생성과 수정은 에디터 API(`PrefabUtility.SaveAsPrefabAsset`, `LoadPrefabContents`)로 한다. YAML을 텍스트로 고치지 않는다. Codex는 에디터 스크립트를 쓰거나 사람에게 맡긴다.
- 기존 `SampleScene`은 이 규칙에서 뺀다. 게임 씬(`1_Scene/Facility.unity`)부터 적용한다.

## 3. 3D 에셋 이름 규칙과 전달 규격 (`docs/rules/art-asset.md`, 새 문서)
**파일 이름 (아티스트가 지킬 규칙)**
| 종류 | 형식 | 예 |
|---|---|---|
| 모델 | `<ArtKey>.fbx` | `CH_Goblin.fbx`, `IT_Trash_02.fbx` |
| 애니메이션 | `<ArtKey>@<Clip>.fbx` (Unity 표준 규칙) | `CH_Goblin@Idle.fbx`, `CH_Goblin@Carry.fbx` |
| 텍스처 | `<ArtKey>[_<재질>]_<맵>.png`, 맵은 `BaseMap`, `Normal`, `Mask`, `Emission` | `CH_Goblin_BaseMap.png`, `PR_Desk_Top_Normal.png` |
| 머티리얼 (도구가 만듦) | `M_<ArtKey>[_<재질>]` | `M_CH_Goblin` |
| 에셋 스토어 모델 | 모델 프리팹 이름을 `<ArtKey>.prefab`으로 바꿔 같은 폴더에 둔다 | `PR_Desk.prefab` |

- 애니메이션 클립 이름은 고정 목록을 쓴다: `Idle`, `Walk`, `Carry`, `CarryHeavy`, `Mop`, `Eat`, `Talk`, `Dispatch`(헬하운드). 상태 머신이 이 이름을 기준으로 클립을 찾는다.
- 이름 정규식: 모델은 `^(CH|IT|PR|ENV)_[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$`, 애니메이션은 `^<ArtKey>@[A-Z][A-Za-z]+$`

**폴더**: `5_Art/Model/<분류>/<ArtKey>/` 안에 fbx, 애니메이션 fbx, 텍스처, 머티리얼을 함께 둔다. 분류는 `Character`, `Item`, `Prop`, `Environment`다.

**내보내기 규격 (Blender/Maya 공통)**
- 단위는 미터, 스케일 1, 위쪽 Y(Unity에서 변환), 정면은 +Z
- 피벗: 바닥 중앙. 벽과 문 같은 환경 조각은 1m 격자에 맞춘 모서리
- 오브젝트 하나에 메시 하나를 권장하고, 머티리얼은 2개 이하
- 캐릭터 리그: Generic. 애니메이션은 클립마다 별도 fbx로 내보낸다.
- 권장 크기(화이트박스 기준): 플레이어 1.7m, 고블린 1.0m, 오크 2.0m, 스켈레톤 1.7m, 슬라임 0.8m, 헬하운드 1.2m(어깨), 마왕 3.0m. 화이트박스 단계에서 이 크기로 Placeholder를 만든다.
- 얼룩(`IT_FloorStain_*`)은 메시 없이 텍스처(`_BaseMap`, 알파 포함)만 받는다. 도구가 바닥 쿼드 머티리얼에 넣는다.

**초기 ArtKey 목록** (B1~B4 단계에서 프리팹을 만들 때 이 이름을 쓴다. 문서에 표로 둔다.)
| 분류 | ArtKey | GDD 이름 |
|---|---|---|
| Character | `CH_Player`, `CH_Goblin`, `CH_Orc`, `CH_Skeleton`, `CH_Slime`, `CH_Hellhound`, `CH_DemonKing` | 플레이어, 고블린, 오크, 스켈레톤, 슬라임, 헬하운드, 마왕 |
| Item | `IT_HeavyDebris_01~`, `IT_Trash_01~`, `IT_Clutter_<물건>`, `IT_FloorStain_01~`, `IT_Mop` | 무거운 잔해, 일반 쓰레기, 어질러진 작은 물건, 바닥 얼룩, 물걸레 |
| Prop | `PR_MopRack`, `PR_DischargeBin`, `PR_HomeSlot_<물건>`, `PR_<가구>` | 화장실 걸레걸이, 현관 배출 구역, 작은 물건 제자리, 가구 |
| Environment | `ENV_FloorTile`, `ENV_Wall_Straight`, `ENV_Wall_Corner`, `ENV_Wall_Curve`, `ENV_Door`, `ENV_Stairs`, `ENV_Entrance`, `ENV_Toilet` | 바닥, 벽, 곡선 방(창고, 간부실) 벽, 문, 계단, 현관, 화장실 |
- `IT_Clutter_<물건>`과 `PR_HomeSlot_<물건>`은 같은 `<물건>` 이름으로 짝을 짓는다(U3: 개체마다 제자리). 어떤 물건들을 쓸지는 B1 Spec에서 정한다.

## 4. 자동 적용 도구 (B 레인 코드)
| 파일 | 역할 |
|---|---|
| `Runtime/View/ArtSlot.cs` | `Visual`에 붙는 컴포넌트. `_artKeyOverride`(비우면 프리팹 이름 사용), `EArtFitMode _fitMode`(None / MatchPlaceholderHeight / MatchPlaceholderBounds), `_modelOffset`, `_modelEuler`, `_modelScale`. 런타임 로직은 없고 데이터만 들고 있다. |
| `Editor/Art/ArtKeyRule.cs` | 순수 정적 함수. 이름 검사, 접두사로 분류 찾기, `@Clip` 분리, 텍스처 맵 접미사 분리. |
| `Editor/Art/ArtBindingPlanner.cs` | 순수 로직. 프리팹 키 목록과 모델 경로 목록을 받아 항목마다 상태(적용됨 / 적용 가능 / 모델 없음 / 주인 없음 / 이름 오류)를 만든다. 테스트 대상이다. |
| `Editor/Art/ArtBinder.cs` | 메뉴 명령. 계획에 따라 `PrefabUtility.LoadPrefabContents`로 프리팹을 연다. `Visual/Model`을 교체(`InstantiatePrefab`로 FBX 링크 유지)하고, ArtSlot 보정을 적용하고, Placeholder를 끈 뒤 저장한다. 되돌리기는 Model을 지우고 Placeholder를 켠다. 실행 전체를 `AssetDatabase.StartAssetEditing`으로 묶는다. |
| `Editor/Art/ArtMaterialBuilder.cs` | `<ArtKey>_BaseMap` 등의 텍스처가 있으면 `M_<ArtKey>`(URP/Lit)를 만들거나 갱신하고, FBX 머티리얼을 그 머티리얼로 remap한다(`ModelImporter.AddRemap`). 얼룩은 쿼드 머티리얼에 BaseMap만 넣는다. |
| `Editor/Art/ArtAnimationBinder.cs` | `<ArtKey>@<Clip>.fbx`를 찾아 `AOC_<ArtKey>.overrideController`(베이스 `AC_Character`)의 같은 이름 클립에 넣고, Model의 Animator에 연결한다. 베이스 컨트롤러의 파라미터와 상태 연결은 B3에서 정한다. |
| `Editor/Art/ArtModelPostprocessor.cs` | `5_Art/Model/**`에 임포트되는 파일의 **임포트 설정만** 자동으로 맞춘다. 모델: 스케일 1, 콜라이더 생성 끔, Read/Write 끔, Character는 Generic 리그, 나머지는 리그 없음. 텍스처: `_Normal`은 노멀맵, `_Mask`는 sRGB 끔. 프리팹은 건드리지 않는다. |
| `Editor/Art/ArtBindingWindow.cs` | 위 UI 와이어프레임의 결과 창 |
| `Test/Art/ArtKeyRuleTest.cs`, `Test/Art/ArtBindingPlannerTest.cs` | 이름 규칙과 상태 분류 테스트 |
| `Test/Art/ArtBinderTest.cs` | 임시 폴더에 프리팹과 모델용 프리팹을 만들어 적용과 되돌리기를 확인한 뒤 정리한다. |

- 네임스페이스: `UnityGameJam.View`(ArtSlot), `UnityGameJam.Editor.Art`(도구)
- 모델 검색 대상: `5_Art/Model/**`의 `.fbx`와 `.prefab`. 같은 ArtKey가 두 개 이상이면 "이름 오류(중복)"로 표시하고 적용하지 않는다.
- 이미 적용된 프리팹에 같은 모델을 다시 적용하면 아무것도 바꾸지 않는다(멱등). 그래서 다시 실행해도 git diff가 생기지 않는다.

# Implementation Steps
승인되면 심지섭(B 레인)의 작업으로 진행한다. 문서 작업(1~2단계)은 공동 설정에 해당하므로 단독 PR로 올린다.

1. **분업 문서** (공동 설정 PR `docs: 2인 분업과 담당자 지정`)
   - `docs/rules/collaboration.md`: 레인 표에 담당자(A 김보경 / B 심지섭)와 에이전트(Codex / Unity AI) 열을 추가한다. `Test/Art/`, `Test/View/`는 B 소유로 적는다. "프리팹·아트 규칙은 B 소유" 한 줄을 넣는다.
   - `docs/team/lane-a.md`(김보경), `docs/team/lane-b.md`(심지섭): 첫 프롬프트, 작업 큐, 커밋 전 체크리스트
   - `Assets/GDD/Roadmap.md`: 레인 제목에 담당자 이름을 붙이고, B 레인 맨 앞에 "B0 프리팹·아트 기반"을 추가한다.
   - `AGENTS.md`: 문서 표에 `prefab.md`, `art-asset.md`, `docs/team/`을 추가한다. 폴더 트리에 `5_Art/Model/`과 `4_Prefab/` 하위 폴더를 반영한다.
   - `7_Setting/AI/ProjectRule/SKILL.md`: 문서 표에 같은 문서들을 추가한다.
   - 상위 계획의 "두 사람 모두 Codex를 쓴다" 문장을 "김보경 Codex, 심지섭 Unity AI"로 고친다.
2. **규칙 문서** *(의존: 1)*
   - `docs/rules/prefab.md`: 위 2절(원칙, 접두사·폴더, 표준 구조, Variant·중첩, 허용 오버라이드, 체크리스트)
   - `docs/rules/art-asset.md`: 위 3절(파일 이름, 폴더, 내보내기 규격, 클립 목록, ArtKey 목록, 적용 방법, 문제 해결)
   - `Assets/GDD/Spec/ArtPipeline.md`: workflow 규칙에 맞춘 도구 스펙(규칙 번호와 테스트 시나리오). 이 계획의 4절을 옮긴다.
3. **폴더** *(의존: 1)*: `4_Prefab/{Prop, Environment, Marker, System}`, `5_Art/Model/{Character, Item, Prop, Environment}`. 빈 폴더에는 `.gitkeep`을 둔다.
4. **이름 규칙과 계획 로직 (TDD)** *(의존: 2)*: `ArtKeyRuleTest`, `ArtBindingPlannerTest`를 먼저 작성해 실패를 확인한 뒤 `ArtKeyRule`, `ArtBindingPlanner`를 구현한다.
5. **ArtSlot과 Binder** *(의존: 4)*: `ArtSlot.cs`, `ArtBinder.cs`(메뉴 명령), `ArtBinderTest`(임시 폴더에서 적용, 되돌리기, 멱등 확인)
6. **임포트 설정, 머티리얼, 애니메이션** *(의존: 5)*: `ArtModelPostprocessor`, `ArtMaterialBuilder`, `ArtAnimationBinder`. 베이스 `AC_Character.controller`는 빈 상태(클립 자리만 있음)로 `5_Art/Model/Character/`에 만든다.
7. **결과 창** *(의존: 5)*: `ArtBindingWindow`. Export Key List는 `docs/rules/art-asset.md`의 표와 비교할 수 있게 Markdown 표로 출력한다.
8. **B1 이후 연결** *(의존: 5)*: B1~B4에서 프리팹을 만들 때 표준 구조(Visual/ArtSlot/Placeholder)와 ArtKey 목록을 따른다. 이 내용은 해당 Spec의 체크 항목으로 넣는다.

# Verification & Testing
- **EditMode 테스트** (`Test/Art/`)
  - `ArtKeyRuleTest`: `접두사가_CH이면_Character로_분류한다`, `소문자로_시작하는_이름은_규칙_위반이다`, `골뱅이_뒤의_이름을_클립으로_분리한다`, `Normal_접미사는_노멀맵으로_인식한다`
  - `ArtBindingPlannerTest`: `프리팹과_모델의_키가_같으면_적용_가능이다`, `모델이_없으면_모델_없음이다`, `프리팹이_없는_모델은_주인_없음이다`, `같은_키의_모델이_둘이면_중복_오류로_적용하지_않는다`, `오버라이드_키가_있으면_프리팹_이름보다_우선한다`
  - `ArtBinderTest`: `적용하면_Model이_생기고_Placeholder가_꺼진다`, `되돌리면_Model이_사라지고_Placeholder가_켜진다`, `같은_모델을_두_번_적용해도_프리팹이_바뀌지_않는다`, `루트의_Collider는_적용_후에도_유지된다`
- **수동 확인**: 임시 FBX(또는 모델 프리팹) 하나를 `5_Art/Model/Character/CH_Goblin/`에 넣는다 → 검사 → 전체 적용 → 결과 창에서 "적용됨" 확인 → 플레이 모드에서 졸개가 그대로 움직이는지(로직 유지) 확인 → 되돌리기
- **규칙 준수 확인**: `Facility.unity`의 루트 오브젝트가 모두 프리팹 인스턴스이거나 빈 정리용 오브젝트인지 검사한다. 이 검사는 `Validate` 메뉴에 "씬 규칙 검사" 항목으로 넣는다.
- 콘솔 에러 0, `@UnityGameJam` 경고 0, 새 파일의 `.meta` 확인. 도구를 다시 실행했을 때 git diff가 없는지 확인한다(멱등).

# 위험과 미정
- **U201 (B 대역) 캐릭터 애니메이션 상태 구성**: `AC_Character`의 파라미터와 전이는 B3(졸개 몸)에서 정한다. 그전까지 애니메이션 바인더는 클립을 연결하는 데까지만 한다.
- **U202 작은 물건 목록**: `IT_Clutter_<물건>`과 `PR_HomeSlot_<물건>`의 실제 물건 목록은 B1 Spec에서 정한다.
- **U203 아트 방향**: U17과 연결된다. 아트 방향이 정해지면 권장 크기와 머티리얼 셰이더(URP/Lit 또는 툰)를 다시 본다.
- 에셋 스토어 모델은 피벗과 축이 제각각이다. 그래서 ArtSlot의 FitMode와 오프셋으로 보정한다. 보정 값은 프리팹(ArtSlot)에 저장되므로 모델을 교체해도 유지된다.
