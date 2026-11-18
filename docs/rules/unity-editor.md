# Unity 에디터 작업 규칙

> 적용 대상: `Assets/**`, `ProjectSettings/**`, `Packages/**`. 이 경로의 파일을 바꾸기 전에 읽는다.

## 에셋 수정
- `.unity`, `.prefab`, `.asset`, `.mat`, `.controller` 같은 YAML 직렬화 파일은 **텍스트로 직접 고치지 않는다.** fileID와 GUID 참조가 깨지기 쉽다. 씬·프리팹·에셋은 Unity 에디터에서 바꾸고, 바꿀 내용을 사용자에게 단계별로 안내하거나 에디터 스크립트(`2_Script/Editor`)를 만들어 처리한다.
- 파일 이동·이름 변경도 에디터(Project 창 또는 `AssetDatabase.MoveAsset`)에서 해서 GUID를 유지한다. 셸에서 `mv`/`git mv`로 옮기면 `.meta`와 따로 놀아 참조가 끊길 수 있다.
- 새 파일(C#, md, asmdef 등)을 `Assets/` 아래에 만들면 Unity가 `.meta`를 만든다. 사용자가 에디터로 돌아가 임포트한 뒤 `.meta`가 생겼는지 확인하도록 안내한다.
- `Packages/manifest.json`과 `ProjectSettings/`는 요청이 있을 때만 바꾼다.

## 컴파일·콘솔 확인
- C#을 고친 뒤에는 Unity 에디터에서 컴파일이 끝나기를 기다리고, Console의 에러·경고를 확인한다.
- 경고 0 기준은 우리 코드(`Assets/@UnityGameJam`)에만 적용한다. 외부 패키지와 `@UnityGameJam` 밖 에셋의 경고는 고치지 않는다.
- 에이전트가 에디터 상태를 직접 확인할 수 없으면, 사용자에게 콘솔 내용을 붙여 달라고 요청한다. 확인하지 않은 채 "컴파일 성공"이라고 보고하지 않는다.

## 테스트 실행
- **에디터가 열려 있을 때:** `Window > General > Test Runner > EditMode`에서 `UnityGameJam.Test`를 실행한다. 에이전트가 실행할 수 없으면 사용자에게 실행과 결과 공유를 요청한다.
- **에디터가 닫혀 있을 때 (배치 모드):**
  ```
  "<Unity 설치 경로>/Unity.exe" -batchmode -projectPath . -runTests -testPlatform EditMode -assemblyNames UnityGameJam.Test -testResults TestResults/EditMode.xml -logFile -
  ```
  - 같은 프로젝트가 에디터에 열려 있으면 배치 모드는 실패한다(프로젝트 잠금).
  - 결과 XML에서 `failed` 개수와 실패한 테스트 이름을 확인해 보고한다. `TestResults/`는 커밋하지 않는다.

## 외부 패키지 로컬 패치
외부 패키지는 원칙적으로 고치지 않는다. 호환성 문제로 꼭 고쳐야 하면 `// UnityGameJam 로컬 패치: 이유` 주석을 달고 아래 목록에 기록한다. 패키지를 업데이트하면 목록을 보고 패치가 아직 필요한지 확인한다.
- (없음)
