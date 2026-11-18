# 폴더 구조 구성 계획

# Project Overview
- Game Title: UnityGameJam (가제, 미정)
- High-Level Concept: 미정. 이번 작업은 게임 내용과 상관없이 프로젝트 폴더·어셈블리 뼈대만 만든다.
- Players: 미정
- Inspiration / Reference Games: 미정
- Tone / Art Direction: 미정 (Art 폴더는 Font / Spine / Sprite)
- Target Platform: PC (StandaloneWindows64)
- Screen Orientation / Resolution: 가로 1920×1080 (U18)
- Render Pipeline: URP (PC_RPAsset / Mobile_RPAsset)

# Game Mechanics
## Core Gameplay Loop
이번 작업에는 해당 없음. 게임 기획이 정해지면 `Assets/GDD/`에 문서를 쓰고, 기능마다 `Assets/GDD/Spec/`에 스펙을 작성한다.

## Controls and Input Methods
New Input System만 쓴다. `InputSystem_Actions.inputactions`는 `7_Setting/`으로 옮긴다. 옮겨도 GUID가 그대로라서 Project-wide Actions 연결은 끊기지 않는다.

# UI
이번 작업에는 해당 없음. UI 프리팹은 `4_Prefab/`, 폰트는 `5_Art/Font/`에 둔다.

# Key Asset & Context

## 최종 폴더 구조
```
Assets/
├─ @UnityGameJam/            직접 만드는 게임 에셋 전부
│  ├─ 1_Scene/               SampleScene.unity (Assets/Scenes에서 옮김)
│  ├─ 2_Script/
│  │  ├─ Editor/             UnityGameJam.Editor.asmdef, AssemblyInfo.cs
│  │  ├─ Runtime/            UnityGameJam.Runtime.asmdef, AssemblyInfo.cs
│  │  └─ Test/               UnityGameJam.Test.asmdef, AssemblySmokeTest.cs
│  ├─ 3_Data/
│  │  ├─ Json/               .gitkeep
│  │  ├─ ScriptableObject/   .gitkeep
│  │  └─ Sheet/              .gitkeep  (원본 xlsx)
│  ├─ 4_Prefab/              .gitkeep
│  ├─ 5_Art/{Font, Spine, Sprite}/   각각 .gitkeep
│  ├─ 6_Audio/{BGM, SFX}/    각각 .gitkeep
│  └─ 7_Setting/             URP 에셋 7개 + InputSystem_Actions (Assets/Settings 등에서 옮김)
├─ GDD/
│  ├─ Roadmap.md / Undecided.md / DesignIssue.md   (빈 양식)
│  └─ Spec/_Template.md      (기능 스펙 템플릿)
└─ Plans/                    (이 계획 문서 – 그대로 둠)
```

## 폴더 규칙
- 폴더 이름은 **단수형**으로 쓰고, 표시 순서는 번호 접두사(`N_`)로 정한다.
- Unity 특수 폴더(`Resources`, `Plugins`, `StreamingAssets`, `Gizmos`, `Editor Default Resources`)는 정해진 이름 그대로 쓴다.
- 외부 에셋·플러그인(예: Spine 런타임)은 `@UnityGameJam` 밖에 둔다.
- 빈 폴더에는 `.gitkeep`을 둔다. 점으로 시작하는 파일이라 Unity가 무시하므로 meta가 생기지 않는다.

## 옮길 파일 (GUID 유지를 위해 `AssetDatabase.MoveAsset` 사용)
| 현재 경로 | 옮길 경로 |
|---|---|
| Assets/Scenes/SampleScene.unity | Assets/@UnityGameJam/1_Scene/SampleScene.unity |
| Assets/Settings/*.asset (7개) | Assets/@UnityGameJam/7_Setting/ |
| Assets/InputSystem_Actions.inputactions | Assets/@UnityGameJam/7_Setting/ |

옮긴 뒤 빈 폴더 `Assets/Scenes`, `Assets/Settings`는 지운다.

## 지울 파일
- `Assets/TutorialInfo/` (Readme.cs, ReadmeEditor.cs, Layout.wlt, URP.png)
- `Assets/Readme.asset` (TutorialInfo 말고는 이 파일을 참조하는 곳이 없다)

## asmdef 내용
**UnityGameJam.Runtime.asmdef**
```json
{ "name": "UnityGameJam.Runtime", "rootNamespace": "UnityGameJam",
  "references": ["Unity.InputSystem", "UnityEngine.UI", "Unity.TextMeshPro"],
  "autoReferenced": true }
```
**UnityGameJam.Editor.asmdef**
```json
{ "name": "UnityGameJam.Editor", "rootNamespace": "UnityGameJam.Editor",
  "references": ["UnityGameJam.Runtime"], "includePlatforms": ["Editor"], "autoReferenced": true }
```
**UnityGameJam.Test.asmdef**
```json
{ "name": "UnityGameJam.Test", "rootNamespace": "UnityGameJam.Test",
  "references": ["UnityGameJam.Runtime", "UnityGameJam.Editor", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"], "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll", "Newtonsoft.Json.dll"],
  "autoReferenced": false, "defineConstraints": ["UNITY_INCLUDE_TESTS"] }
```
- `Newtonsoft.Json.dll`: `com.unity.nuget.newtonsoft-json` 3.2.1이 다른 패키지의 의존성으로 이미 설치돼 있다(packages-lock 확인). `manifest.json`에도 직접 추가해서, 의존성이 바뀌어도 빠지지 않게 한다.
- `com.unity.test-framework` 1.6.0이 이미 설치돼 있다.

**AssemblyInfo.cs** (Runtime과 Editor에 하나씩)
```csharp
using System.Runtime.CompilerServices;

// 테스트에서 internal 멤버를 검증할 수 있게 한다.
[assembly: InternalsVisibleTo("UnityGameJam.Test")]
```
**AssemblySmokeTest.cs** (Test) – 어셈블리 참조가 제대로 연결됐는지 확인하는 테스트 1개.

## 루트 파일
- `.gitattributes`: Unity YAML은 LF로 저장하고, png/wav/ttf/fbx/xlsx/pdf/skel 등은 `binary`로 지정한다. **Git LFS는 쓰지 않는다**(사용자 결정).
- `.gitignore`: 기존 파일에 `/TestResults/`(배치 모드 테스트 결과)만 추가한다.
- 에이전트 규칙(Codex): `AGENTS.md`, `docs/rules/{workflow, coding-convention, unity-editor}.md`, `.editorconfig`, `.github/pull_request_template.md`

# Implementation Steps

## 진행 상황 (연결 끊김 이후 확인)
- [x] 1~3단계: 폴더 만들기, 에셋 옮기기, 기존 폴더 정리 끝. RP 참조(Default/Quality PC·Mobile)와 빌드 씬 목록이 새 경로를 가리키는 것까지 확인함
- [x] 4단계: asmdef 3개, AssemblyInfo 2개, AssemblySmokeTest, manifest에 newtonsoft 추가
- [x] 5단계: `.gitkeep` 9개
- [x] 6단계: GDD 문서 3개 + `Spec/_Template.md`
- [x] 7단계: `.gitattributes`
- [x] 검증: 컴파일 에러 0, EditMode 테스트 2/2 통과, RP·빌드 씬·Input Actions·Volume 참조 정상

1. **폴더 만들기**: `AssetDatabase.CreateFolder`로 위 구조의 폴더를 모두 만든다(폴더 meta 생성).
2. **파일 옮기기**: 씬, URP 에셋, InputSystem_Actions를 `AssetDatabase.MoveAsset`으로 옮긴다. *의존: 1단계*
   - 옮긴 뒤 확인할 것: GraphicsSettings/QualitySettings의 RP 에셋 참조, EditorBuildSettings의 씬 목록, Input System Project-wide Actions 연결
3. **정리**: `Assets/Scenes`, `Assets/Settings`, `Assets/TutorialInfo`, `Assets/Readme.asset`을 지운다. *의존: 2단계*
4. **스크립트 어셈블리 만들기**: asmdef 3개, AssemblyInfo.cs 2개, AssemblySmokeTest.cs를 만들고, `manifest.json`에 `com.unity.nuget.newtonsoft-json`을 추가한다. *의존: 1단계*
5. **.gitkeep 넣기**: 빈 폴더 9곳(3_Data/Json, 3_Data/ScriptableObject, 3_Data/Sheet, 4_Prefab, 5_Art/Font, 5_Art/Spine, 5_Art/Sprite, 6_Audio/BGM, 6_Audio/SFX)에 넣는다. *의존: 1단계*
6. **GDD 문서**: `Assets/GDD/Roadmap.md`, `Undecided.md`, `DesignIssue.md`(빈 양식)와 `Spec/_Template.md`를 만든다. 템플릿의 네임스페이스는 `UnityGameJam.<기능>`으로 쓴다. *의존: 1단계*
7. **루트 파일**: 프로젝트 루트에 `.gitattributes`를 만든다(줄바꿈 규칙 + 바이너리 지정).
8. **에이전트 규칙 문서**: 폴더 규칙과 Git 규칙을 정리한 `AGENTS.md`를 루트에 둔다.

# Verification & Testing
- 콘솔에 컴파일 에러가 없는지 확인한다(asmdef 참조 해결 여부).
- Test Runner(EditMode)에서 `AssemblySmokeTest`가 통과하는지 확인한다.
- `GraphicsSettings.defaultRenderPipeline`과 Quality 레벨별 RP가 계속 `PC_RPAsset`/`Mobile_RPAsset`을 가리키는지 확인한다(None이면 실패).
- Build Profiles 씬 목록에 `@UnityGameJam/1_Scene/SampleScene.unity`가 들어 있는지 확인한다.
- SampleScene을 열었을 때 Missing 참조가 없고, Volume Profile이 연결돼 있는지 확인한다.
- Project Settings > Input System Package에서 Project-wide Actions가 유지되는지 확인한다.
- `git status`로 새 파일마다 `.meta`가 함께 생겼는지, `.gitkeep`에는 meta가 없는지 확인한다.

## 참고 / 주의
- 5_Art/Spine은 폴더만 만든다. Spine 런타임(spine-unity)은 넣지 않으며, 필요하면 `Assets/Spine/`(@UnityGameJam 밖)에 따로 넣는다.
- xlsx→Json 변환 도구는 만들지 않는다. 3_Data에는 폴더 구조만 만든다.
