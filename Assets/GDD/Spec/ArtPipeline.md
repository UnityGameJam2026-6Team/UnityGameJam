# 아트 파이프라인 (B0) 스펙

- 상태: 확정 (`Assets/Plans/team-division-prefab-art-pipeline.md` 승인)
- 담당: B 레인 · 심지섭
- 근거: `docs/rules/prefab.md`, `docs/rules/art-asset.md`
- 범위: 이번에 하는 것은 이름 규칙 검사, 상태 분류, 프리팹에 모델 적용과 되돌리기, 머티리얼과 애니메이션 오버라이드 생성, 임포트 설정, 결과 창, 씬 규칙 검사다. 하지 않는 것은 애니메이터 파라미터와 전이(B3, U201), 실제 게임 프리팹 제작(B1~B4)이다.
- 네임스페이스: `UnityGameJam.View`(ArtSlot), `UnityGameJam.Editor.Art`(도구)

## 목적
3D 에셋이 들어오면 파일 이름(ArtKey)만 맞춰도 메뉴 한 번으로 해당 프리팹의 화이트박스가 모델로 바뀌게 한다. 게임 로직(루트의 콜라이더와 스크립트)은 건드리지 않는다.

## 입력 / 출력
| 구분 | 이름 | 설명 |
|---|---|---|
| 입력 | 프리팹 | `4_Prefab/**`에서 `ArtSlot`을 가진 프리팹 |
| 입력 | 모델 | `5_Art/Model/**`의 `<ArtKey>.fbx` / `<ArtKey>.prefab` |
| 입력 | 텍스처, 애니메이션 | `<ArtKey>[_재질]_<맵>.*`, `<ArtKey>@<Clip>.fbx` |
| 출력 | 프리팹 변경 | `Visual/Model` 교체, `Placeholder` 끄기 |
| 출력 | 생성 에셋 | `M_<ArtKey>[_재질].mat`, `AOC_<ArtKey>.overrideController`, `AC_Character.controller` |
| 출력 | 결과 | 항목마다 상태(적용됨, 적용 가능, 모델 없음, 주인 없음, 이름 오류, 중복, 베이스) |

## 규칙
1. ArtKey는 `^(CH|IT|PR|ENV)_[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$`를 만족해야 한다.
2. 접두사로 분류를 정한다: CH → Character, IT → Item, PR → Prop, ENV → Environment.
3. `<ArtKey>@<Clip>` 파일 이름은 ArtKey와 클립 이름으로 나뉜다. 클립 이름은 `Idle, Walk, Carry, CarryHeavy, Mop, Eat, Talk, Dispatch` 중 하나여야 한다.
4. 텍스처 파일 이름은 기준 이름과 맵(`BaseMap, Normal, Mask, Emission`)으로 나뉜다.
5. 프리팹의 키는 ArtSlot의 오버라이드 키가 있으면 그것을, 없으면 프리팹 파일 이름을 쓴다.
6. 키가 `Base`로 끝나는 프리팹은 "베이스"로 분류하고 적용하지 않는다.
7. 프리팹 키와 같은 이름의 모델이 하나 있으면, 이미 그 모델이 들어 있을 때는 "적용됨", 아니면 "적용 가능"이다.
8. 같은 키의 모델이 둘 이상이면 해당 모델과 프리팹 모두 "중복"이고, 적용하지 않는다.
9. 모델이 없으면 "모델 없음"이다. 텍스처 전용 프리팹(`IsTextureOnly`)은 `<ArtKey>_BaseMap`을 모델 대신 쓴다.
10. 프리팹이 없는 모델은 "주인 없음"이다.
11. 이름 규칙을 어긴 모델이나 프리팹 키는 "이름 오류"다.
12. 적용하면 `Visual/Model`이 생기고 `Placeholder`가 꺼진다. 루트의 컴포넌트는 바뀌지 않는다.
    모델은 원본 루트 회전에 슬롯 회전 보정을 적용하고, 원본 축별 스케일에 맞춤 배율과 슬롯 배율을 곱한다. 위치는 슬롯의 `ModelOffset`을 쓴다.
13. 되돌리면 `Visual/Model`이 사라지고 `Placeholder`가 켜진다.
14. "적용됨" 항목에 다시 적용하면 프리팹을 저장하지 않는다(멱등).
15. 씬 규칙: 프리팹 인스턴스가 아니면서 Transform 말고 다른 컴포넌트가 있는 오브젝트는 위반이다.

## 설계
| 파일 | 역할 |
|---|---|
| `Runtime/View/ArtSlot.cs` | 보정 값, Placeholder와 Model 참조, `Animator` 접근자 |
| `Editor/Art/ArtKeyRule.cs` | 규칙 1~4 (순수 정적 함수) |
| `Editor/Art/ArtBindingPlanner.cs` | 규칙 5~11 (순수 로직) |
| `Editor/Art/ArtBinder.cs` | 에셋 수집, 규칙 12~14 적용, 메뉴 |
| `Editor/Art/ArtMaterialBuilder.cs` | `M_` 머티리얼 생성과 FBX 머티리얼 remap, 텍스처 전용 적용 |
| `Editor/Art/ArtAnimationBinder.cs` | `AC_Character` 보장, `AOC_` 생성과 연결 |
| `Editor/Art/ArtModelPostprocessor.cs` | 처음 임포트할 때의 설정 |
| `Editor/Art/ArtBindingWindow.cs` | 결과 창, 키 목록 내보내기, 씬 규칙 검사(규칙 15) |

## 미정·보류
- U201 캐릭터 애니메이터 상태 구성 (B3에서 결정)
- U202 작은 물건과 제자리 목록 (B1에서 결정)
- U203 아트 방향과 셰이더 (U17과 연결)

## 미결 질문
- (없음)

## 테스트 시나리오
| 규칙 | 조건 | 행동 | 기대 결과 | 테스트 메서드 이름 |
|---|---|---|---|---|
| 1 | `CH_Goblin`, `goblin_final` | 검사 | 앞은 통과, 뒤는 위반 | `규칙에_맞는_이름만_ArtKey로_인정한다` |
| 2 | `ENV_Wall_Curve` | 분류 | Environment | `접두사가_ENV이면_Environment로_분류한다` |
| 3 | `CH_Goblin@Idle`, `CH_Goblin@Dance` | 분리 | Idle은 허용, Dance는 목록 밖 | `골뱅이_뒤의_이름을_클립으로_분리한다`, `목록에_없는_클립_이름은_허용하지_않는다` |
| 4 | `PR_Desk_Top_Normal` | 분리 | 기준 `PR_Desk_Top`, 맵 Normal | `텍스처_이름을_기준_이름과_맵으로_나눈다` |
| 5 | 오버라이드 키가 있음 | 계획 | 오버라이드 키 사용 | `오버라이드_키가_있으면_프리팹_이름보다_우선한다` |
| 6 | `CH_MinionBase` | 계획 | 베이스 | `Base로_끝나는_프리팹은_적용하지_않는다` |
| 7 | 키가 같은 모델 1개 | 계획 | 적용 가능 / 적용됨 | `프리팹과_모델의_키가_같으면_적용_가능이다`, `이미_같은_모델이_있으면_적용됨이다` |
| 8 | 같은 키의 모델 2개 | 계획 | 중복 | `같은_키의_모델이_둘이면_중복으로_적용하지_않는다` |
| 9 | 모델 없음 / 텍스처 전용 | 계획 | 모델 없음 / BaseMap을 원본으로 사용 | `모델이_없으면_모델_없음이다`, `텍스처_전용_프리팹은_BaseMap을_원본으로_쓴다` |
| 10 | 프리팹 없는 모델 | 계획 | 주인 없음 | `프리팹이_없는_모델은_주인_없음이다` |
| 11 | 규칙을 어긴 모델 이름 | 계획 | 이름 오류 | `규칙을_어긴_모델_이름은_이름_오류다` |
| 12 | 임시 프리팹과 모델 | 적용 | Model 생성, Placeholder 꺼짐, 루트 Collider 유지 | `적용하면_Model이_생기고_Placeholder가_꺼진다`, `루트의_Collider는_적용_후에도_유지된다` |
| 13 | 적용된 프리팹 | 되돌리기 | Model 삭제, Placeholder 켜짐 | `되돌리면_Model이_사라지고_Placeholder가_켜진다` |
| 14 | 적용된 프리팹 | 다시 적용 | 파일이 바뀌지 않음 | `같은_모델을_두_번_적용해도_프리팹이_바뀌지_않는다` |
