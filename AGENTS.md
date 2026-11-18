# UnityGameJam (가제)

게임 기획은 아직 정해지지 않았다. 정해지면 이 단락에 한두 문장으로 요약한다.

- Unity 6000.3.28f1, URP (PC/Mobile 렌더러), Input System(신규)만 사용
- 대상 플랫폼: PC (Windows 64bit)
- 답변·주석·커밋 메시지는 한국어로 쓴다

## 먼저 읽을 문서
Codex는 아래 문서를 자동으로 불러오지 않는다. **해당 작업을 시작하기 전에 직접 열어서 읽는다.**

| 작업 | 문서 |
|---|---|
| 게임 기능 개발 (스펙 → 테스트 → 구현 → 완료 조건) | [docs/rules/workflow.md](docs/rules/workflow.md) |
| C# 코드 작성·수정 | [docs/rules/coding-convention.md](docs/rules/coding-convention.md) |
| 씬·프리팹·에셋·ProjectSettings·Packages 수정 | [docs/rules/unity-editor.md](docs/rules/unity-editor.md) |

## 기준 문서
- 기획의 기준은 `Assets/GDD/`의 문서다. 기능을 만들거나 바꾸기 전에 관련 내용을 먼저 읽는다.
- 기획서에 나온 이름(데이터 클래스, UI 오브젝트 이름 등)은 코드에서 **그대로** 쓴다.
- 기획서와 구현이 다르거나, 기획서에 없는 규칙이 필요하면 혼자 정하지 말고 먼저 물어본다.
- 콘텐츠 수치(스테이지 구성, 밸런스 값 등)는 코드에 직접 쓰지 않고 `3_Data`(ScriptableObject/Json)에서 읽는다.
- 진행 기준은 `Assets/GDD/Roadmap.md`다. 정해지지 않은 기획 내용은 `Assets/GDD/Undecided.md`에, 기획끼리 맞지 않는 부분은 `Assets/GDD/DesignIssue.md`에 기록한다.

## 폴더 규칙
```
Assets/
├─ @UnityGameJam/        직접 만드는 게임 에셋 전부
│  ├─ 1_Scene/
│  ├─ 2_Script/{Editor, Runtime, Test}
│  ├─ 3_Data/{Json, ScriptableObject, Sheet}   Sheet = 원본 xlsx
│  ├─ 4_Prefab/
│  ├─ 5_Art/{Font, Spine, Sprite}
│  ├─ 6_Audio/{BGM, SFX}
│  └─ 7_Setting/         URP 에셋, InputSystem_Actions
│     └─ AI/ProjectRule/SKILL.md   Unity AI용 연결 파일 (규칙 원본은 이 문서와 docs/rules)
└─ GDD/                  기획 문서 (Project 창에서 보이도록 Assets 안에 둔다)
   └─ Spec/              기능별 구현 스펙 (_Template.md를 복사해서 쓴다)
docs/rules/              에이전트 작업 규칙 (Assets 밖이라 .meta가 생기지 않는다)
```
- 폴더 이름은 **단수형**으로 쓰고, 표시 순서는 번호 접두사(`N_`)로 정한다. 새 최상위 폴더는 사용자와 번호를 정한 뒤 만든다.
- 예외: Unity 특수 폴더(`Resources`, `Plugins`, `StreamingAssets`, `Gizmos`, `Editor Default Resources`)는 정해진 이름 그대로 쓴다.
- 외부 에셋·플러그인은 `@UnityGameJam` 밖에 둔다.
- 빈 폴더에는 `.gitkeep`을 둔다. 점으로 시작하는 파일이라 Unity가 무시하므로 meta가 생기지 않는다.

## Git
- 파일을 추가하면 **`.meta`도 꼭 함께 커밋**한다. 빠뜨리면 다른 PC에서 GUID가 새로 만들어져 참조가 끊긴다.
- Git LFS는 쓰지 않는다. 바이너리는 `.gitattributes`에서 `binary`로만 지정한다. 용량이 큰 작업 원본(PSD, 녹화 영상 등)은 저장소에 넣지 않는다.
- 커밋 메시지는 `태그: 내용` 형식으로 쓴다(예: `feat: 플레이어 이동 구현`). 태그: `feat`, `fix`, `refactor`, `docs`, `setting`, `art`.
- 브랜치 이름은 `태그/짧은-영어-설명` 형식으로 쓴다(예: `feat/player-move`). main에서 따고, PR은 Squash로 머지한다.
- 사용자가 미리 요청하지 않았으면 커밋·푸시하지 않는다.
- 작업을 마치면 커밋 메시지와 설명을 추천한다. PR 단계가 되면 PR 제목(커밋 메시지 형식)과 본문(`.github/pull_request_template.md` 형식)을 함께 추천한다.
