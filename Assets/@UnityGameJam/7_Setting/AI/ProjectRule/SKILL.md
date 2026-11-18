---
name: unitygamejam-project-rules
description: UnityGameJam 프로젝트 규칙. C# 스크립트 작성·수정, 씬·프리팹·에셋 생성·수정, 폴더·파일 배치, 게임 기능 개발, 기획 문서(GDD·Spec), 패키지·프로젝트 설정 변경, 커밋 메시지 추천 등 이 프로젝트의 거의 모든 작업을 시작하기 전에 반드시 적용한다.
---

# UnityGameJam 프로젝트 규칙

규칙의 **원본은 저장소 루트의 문서**다. 이 스킬은 원본을 가리키기만 한다. 같은 규칙을 Codex(`AGENTS.md`)와 Unity AI가 함께 쓰므로, 규칙을 고칠 때는 원본만 고치고 이 파일에 내용을 복사하지 않는다.

## 1. 작업 전에 읽을 문서
`Unity.GetFileContent`로 아래 문서를 열어 읽는다. 경로는 프로젝트 루트 기준이다.

| 언제 | 문서 |
|---|---|
| 모든 작업 (항상 먼저) | `AGENTS.md` |
| C# 코드 작성·수정 | `docs/rules/coding-convention.md` |
| 게임 기능 개발, 규칙 변경 | `docs/rules/workflow.md` |
| 씬·프리팹·에셋·ProjectSettings·Packages 수정 | `docs/rules/unity-editor.md` |

이미 이 대화에서 읽은 문서는 다시 읽지 않는다.

## 2. Unity AI에서 바꿔 적용하는 부분
원본 문서는 에디터를 직접 다루지 못하는 외부 에이전트(Codex)를 기준으로 쓰여 있다. Unity AI는 에디터 도구를 쓸 수 있으므로 아래처럼 바꿔 적용한다.

- **에셋 수정:** `.unity`/`.prefab`/`.asset` YAML을 텍스트로 고치지 않는 원칙은 그대로다. 사용자에게 수동 작업을 안내하는 대신 `Unity.RunCommand`로 에디터 API(`AssetDatabase.MoveAsset`, `PrefabUtility`, `EditorSceneManager` 등)를 써서 직접 처리하고, 씬을 바꿨으면 같은 명령 안에서 저장한다.
- **컴파일·콘솔 확인:** 사용자에게 콘솔 내용을 묻지 않고, 코드 편집 결과와 `Unity.GetConsoleLogs`로 직접 확인한다. 경고 0 기준은 `Assets/@UnityGameJam` 코드에만 적용한다.
- **테스트 실행:** 배치 모드 명령은 쓰지 않는다(에디터가 열려 있어 실패한다). `Unity.RunCommand`에서 `TestRunnerApi`로 `UnityGameJam.Test` 어셈블리의 EditMode 테스트를 실행하고 결과를 확인한다.
- **`.meta` 확인:** `Assets/` 아래에 새 파일을 만들면 `.meta`가 생겼는지 직접 확인한다. `.gitkeep`에는 `.meta`가 없어야 정상이다.

## 3. 자주 놓치는 핵심 규칙 (자세한 내용은 원본 문서)
- 게임 에셋은 `Assets/@UnityGameJam/` 안에 번호 폴더(`1_Scene` ~ `7_Setting`)로 둔다. 폴더 이름은 단수형으로 쓴다. 새 최상위 폴더는 사용자와 번호를 정한 뒤 만든다.
- 네임스페이스는 `UnityGameJam.<도메인>`으로 쓴다. 새 도메인은 먼저 묻는다.
- 게임 규칙은 순수 C# 클래스(테스트 대상)로, MonoBehaviour는 입력·연출·UI만 맡는다.
- 입력은 Input System(신규)만 쓴다. 콘텐츠 수치는 코드에 넣지 않고 `3_Data`에서 읽는다.
- 기획에 없는 규칙은 혼자 정하지 말고 묻는다. 정하지 못한 내용은 `Assets/GDD/Undecided.md`, 기획끼리 맞지 않는 부분은 `Assets/GDD/DesignIssue.md`에 기록한다.
- 답변·주석·커밋 메시지는 한국어로 쓴다. 커밋 메시지 형식은 `태그: 내용`이다(`feat`, `fix`, `refactor`, `docs`, `setting`, `art`).
- Git LFS는 쓰지 않는다. 사용자가 요청하지 않으면 커밋·푸시하지 않고, 작업이 끝나면 커밋 메시지를 추천한다.
