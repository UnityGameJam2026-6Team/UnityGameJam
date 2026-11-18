# B 레인 시작 문서 · 심지섭 · Unity AI

> 이 문서는 심지섭과 심지섭의 에이전트만 고친다. 규칙 원본은 `AGENTS.md`와 `docs/rules/`에 있다.

## 맡는 일
씬, 프리팹, 입력, 이동(NavMesh), 카메라, UI, 에디터 도구, 아트·사운드 적용.

- **소유 경로**: `2_Script/Runtime/{Player, View, UI}/`, `2_Script/Editor/`, `2_Script/Test/{Art, View}/`, `1_Scene/`, `4_Prefab/`, `5_Art/`, `6_Audio/`, `7_Setting/InputSystem_Actions.inputactions`, `ProjectSettings/`, `docs/rules/{prefab, art-asset}.md`
- **읽기만 하는 곳**: `Runtime/Contract/`(바꿀 때는 `collaboration.md` 2절 절차), A 레인의 규칙 코드와 `3_Data/`
- **반드시 따를 규칙**: 씬에는 프리팹만 배치한다(`docs/rules/prefab.md`). 3D 에셋은 이름 규칙에 맞춰 넣고 메뉴로 적용한다(`docs/rules/art-asset.md`).

## 작업 큐 (`Assets/GDD/Roadmap.md` B 레인 단락)
| 순서 | 작업 | Spec | 핵심 | 브랜치 |
|---|---|---|---|---|
| B0 | 프리팹·아트 기반 | `ArtPipeline.md` | 프리팹 규칙, ArtSlot, ArtBinder 메뉴 | `feat/art-pipeline` |
| B1 | 화이트박스 시설 | `FacilityLayout.md` | `FL_Floor1~3`, `ENV_`/`PR_`/`MK_` 프리팹, NavMeshSurface, `FacilityLayoutAuthoring` | `feat/facility-layout` |
| B2 | 플레이어 | `Player.md` | `CH_Player`, Input System 이동·상호작용, 카메라, `FakeMatchSession` | `feat/player` |
| B3 | 졸개 몸 | `MinionAgent.md` | `CH_MinionBase` + Variant, `MinionAgent : IMinionBody`, 계단 Warp, `AC_Character` 상태 | `feat/minion-agent` |
| B4 | ContaminationView | `ContaminationView.md` | `IT_` 프리팹, `IMatchReadModel.Items` 반영 | `feat/contamination-view` |
| M1 | 통합: 오염이 보이는 맵 | - | Bootstrap에 팩토리 SO 연결 | 함께 |
| B5 | HUD | `Hud.md` | `UI_` 프리팹 5영역 | `feat/hud` |
| B6 | 대화와 제안 UI | `AssignmentUi.md` | AssignmentPanel, ProposalStackView | `feat/assignment-ui` |
| B7 | 마감 연출과 결과 | `ResultUi.md` | 마왕 등장, ResultPanel, 재시작 | `feat/result-ui` |

- 미정 사항과 기획 오류는 **200번대** 번호를 쓴다(U201~U203은 아트 관련으로 예약됨).
- 프리팹 이름은 `docs/rules/art-asset.md`의 **ArtKey 목록**을 그대로 쓴다. 그래야 나중에 모델이 들어왔을 때 메뉴 한 번으로 적용된다.

## Unity AI 첫 프롬프트 (B1)
```
나는 심지섭이고 B 레인(월드·표현) 담당이다.
AGENTS.md, docs/team/lane-b.md, docs/rules/collaboration.md, workflow.md, unity-editor.md, prefab.md, art-asset.md를 따른다.
Roadmap의 B1 "화이트박스 시설"을 진행한다. 브랜치는 feat/facility-layout.
Assets/GDD/Spec/FacilityLayout.md를 먼저 작성해 보여 줘.
GDD §2 평면도대로 FL_Floor1~3 프리팹(X축으로 떨어뜨려 배치)을 만들고, 그 안을 ENV_/PR_/MK_ 프리팹 인스턴스로 채운다.
아트를 받을 프리팹은 Visual/ArtSlot/Placeholder 표준 구조와 ArtKey 목록 이름을 따른다.
층마다 NavMeshSurface를 굽고, FacilityLayoutAuthoring이 Contract의 FacilityLayout을 만들게 한다.
씬 1_Scene/Facility.unity에는 SYS_ 프리팹과 FL_ 프리팹만 둔다. B 레인 소유 경로 밖은 고치지 마라.
```

## 3D 에셋이 들어왔을 때 (요약, 자세한 내용은 `docs/rules/art-asset.md`)
1. 파일 이름을 `<ArtKey>.fbx`로 맞춰 `5_Art/Model/<분류>/<ArtKey>/`에 넣는다. 텍스처는 `<ArtKey>_BaseMap.png` 등으로 맞춘다.
2. `Tools > UnityGameJam > Art > 검사`로 상태를 확인한다.
3. `전체 적용`(또는 선택한 프리팹에만 적용)을 누른다. 크기와 축이 맞지 않으면 프리팹의 `Visual/ArtSlot`에서 보정한다.
4. 플레이 모드에서 확인한 뒤 `art: <ArtKey> 모델 적용`으로 커밋한다.

## 커밋 전 체크리스트
- [ ] 변경 파일이 모두 B 소유 경로 안에 있다(밖의 파일은 `git restore`)
- [ ] 씬 루트가 모두 프리팹 인스턴스이거나 빈 정리용 오브젝트다(`Tools > UnityGameJam > Art > 검사`의 씬 규칙 검사)
- [ ] 새 파일과 폴더의 `.meta`가 있다
- [ ] 컴파일 에러 0, `@UnityGameJam` 경고 0, EditMode 테스트 전부 통과
- [ ] Roadmap에서 B 레인 단락의 상태만 바꿨다
