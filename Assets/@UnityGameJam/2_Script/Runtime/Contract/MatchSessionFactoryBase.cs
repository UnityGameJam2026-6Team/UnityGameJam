using UnityEngine;

namespace UnityGameJam.Contract
{
    /// <summary>
    /// Bootstrap(B 레인)이 인스펙터로 연결하는 판 생성기.
    /// A 레인은 수치 SO를 들고 있는 실제 구현을, B 레인은 가짜 구현(FakeMatchSession용)을 상속해 만든다.
    /// 내부가 바뀌어도 Bootstrap은 이 타입만 알면 되므로 수정할 필요가 없다.
    /// </summary>
    public abstract class MatchSessionFactoryBase : ScriptableObject
    {
        public abstract IMatchSession Create(MatchSetup setup);
    }
}
