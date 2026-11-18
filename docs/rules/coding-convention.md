# C# 코딩 규칙

> 적용 대상: `**/*.cs`. C# 파일을 만들거나 고치기 전에 읽는다.

## 코드 구조
- 네임스페이스는 `UnityGameJam`으로 시작하고 도메인별로 나눈다(예: `UnityGameJam.Player`). 여러 도메인이 함께 쓰는 코드만 `UnityGameJam`에 둔다. 테이블 데이터(행·테이블 클래스, 로더)는 `UnityGameJam.Data`에, 에디터 도구는 `UnityGameJam.Editor`에 둔다. 새 도메인이 필요하면 먼저 묻는다.
- 게임 규칙(판정, 점수 계산, 상태 전이)은 MonoBehaviour와 분리된 순수 C# 클래스로 만들어 테스트할 수 있게 한다. MonoBehaviour는 입력·연출·UI 연결만 맡는다.
- MonoBehaviour는 파일 하나에 클래스 하나만 두고, 파일 이름을 클래스 이름과 같게 한다.
- 입력은 Input System(신규)만 쓴다. `UnityEngine.Input`(구 Input Manager)은 쓰지 않는다. 액션은 `7_Setting/InputSystem_Actions.inputactions`에 정의한다.
- 연출 중 중복 입력 막기, 일시정지 같은 기획서의 예외 처리를 빠뜨리지 않는다.

## 어셈블리·테스트
| 폴더 | 어셈블리 | 루트 네임스페이스 | 비고 |
|---|---|---|---|
| `2_Script/Runtime` | `UnityGameJam.Runtime` | `UnityGameJam` | 게임 코드 |
| `2_Script/Editor` | `UnityGameJam.Editor` | `UnityGameJam.Editor` | 에디터 전용 |
| `2_Script/Test` | `UnityGameJam.Test` | `UnityGameJam.Test` | EditMode 테스트 (NUnit, Newtonsoft.Json) |

- 새 패키지를 쓰려면 해당 asmdef의 `references`에 추가한다. Runtime은 지금 `Unity.InputSystem`, `UnityEngine.UI`, `Unity.TextMeshPro`를 참조한다.
- 테스트에서 internal 멤버에 접근할 수 있다(`Runtime/AssemblyInfo.cs`, `Editor/AssemblyInfo.cs`의 `InternalsVisibleTo`).
- 게임 규칙을 추가하거나 바꾸면 `Test`에 EditMode 테스트도 함께 작성한다. 실행 방법은 [unity-editor.md](unity-editor.md)의 "테스트 실행"을 따른다.
- 테스트 메서드 이름은 조건과 기대 결과가 모두 드러나는 한국어 문장으로 짓고, 띄어쓰기 대신 `_`를 쓴다(예: `체력이_0이면_사망_상태가_된다`). 기호(`?`, `%`, `-`, `.`, `≥` 등)는 쓸 수 없으니 말로 풀어 쓴다(`10_이상이면`, `1점5배`).
- 한글은 **테스트 메서드 이름에만** 쓴다. 테스트 클래스와 파일 이름은 영어로 `<대상클래스>Test` 형식으로 짓는다(OS에 따라 한글 파일 이름이 git에서 깨지는 것을 막기 위해서다).
- 테스트 결과가 매번 같도록, 규칙 클래스는 난수와 테이블 데이터를 직접 만들지 않고 생성자로 주입받는다(난수는 인터페이스로, 테이블은 데이터 객체로 받는다).

## 네이밍
기준: [Unity C# 네이밍·코드 스타일 가이드](https://unity.com/kr/how-to/naming-and-code-style-tips-c-scripting-unity)

| 대상 | 규칙 | 예 |
|---|---|---|
| private·protected 필드 | `_` + 카멜 케이스 | `private int _number;` |
| public 필드 | 파스칼 케이스. 가능하면 프로퍼티로 바꾼다(const·static 제외) | `public int Number;` |
| 프로퍼티 | 파스칼 케이스 | `public float Horizontal { get; private set; }` |
| 지역 변수·매개변수 | 카멜 케이스 | `int number;` |
| bool | `is`·`has`·`can`·`should`로 시작하는 질문형 | `_isTalk`, `hasWeapon` |
| 클래스 | 파스칼 케이스, 명사 | `PlayerMovement` |
| ScriptableObject | 파스칼 케이스 + `SO` 접미사. 파일 이름도 같게 | `StageDataSO` |
| 인터페이스 | `I` + 파스칼 케이스 | `IInteractable`, `IMovable` |
| 메서드 | 파스칼 케이스, 동사(또는 전치사)로 시작. bool을 반환하면 `Is`·`Has` 같은 질문형 | `Jump()`, `FindPlayer()`, `HasTarget()` |
| enum | `E` + 파스칼 케이스, **단수형**. 멤버에는 `E`를 붙이지 않고 파스칼 케이스. 프로퍼티 이름은 `E` 없이 그대로 | `EAttackType` (O) / `AttackType`·`EAttackTypes` (X), `public EAttackType AttackType` |
| 네임스페이스 | 파스칼 케이스 | `UnityGameJam.Player` |

- 변수 이름은 명사로 짓는다. 지나치게 줄이지 말고, 누구나 뜻을 알 수 있게 짓는다.
- 접근 제한자는 항상 쓴다: `private int _hp;` (O) / `int _hp;` (X)
- `[SerializeField]`는 인스펙터에서 연결하거나 확인해야 하는 필드에만 붙인다.

## 서식
- 중괄호는 Allman 스타일로 쓴다(여는 중괄호를 다음 줄에). IDE 설정은 루트의 `.editorconfig`에 있다.
- 짧은 한 문장(`return` 등)은 한 줄로 써도 되지만, 중괄호는 생략하지 않는다: `if (isDead) { return; }`
- `switch`에서는 `case`를 한 단계, 그 본문을 한 단계 더 들여쓴다.
- `var`는 오른쪽에 타입이 분명히 보일 때만 쓴다: `var dice = new Dice();` (O) / `var result = Calculate();` (X)
- 파일은 UTF-8로 저장한다(한국어 Windows 기본값 CP949로 저장되면 한글 주석과 테스트 이름이 깨진다).

## 구조
- 클래스 앞부분만 봐도 의도를 알 수 있게 쓴다. 필드를 앞에, 메서드를 뒤에 두고, 중요한 메서드를 먼저 둔다.
- 매개변수는 적게 유지하고, 필요 없는 오버로딩은 피한다.
- 동작을 바꾸는 bool 플래그 매개변수 대신 메서드를 나눈다: `Mute(bool isAll)` (X) → `Mute()`, `MuteAll()` (O)
- 컴포넌트 참조는 `Awake()`에서 캐시하고, `Update()` 안에서 `GetComponent`·`Find`를 부르지 않는다.
- SOLID를 염두에 두되, 지나치게 복잡하게 설계하지 않는다.

## 주석
- 지나치지 않다면 자유롭게 단다. 다만 **무엇을 하는지보다, 이 코드가 왜 필요한지**를 쓴다.
