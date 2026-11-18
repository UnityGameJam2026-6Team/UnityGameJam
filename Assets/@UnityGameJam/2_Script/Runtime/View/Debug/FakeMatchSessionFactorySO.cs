using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// 가짜 판 팩토리. A 레인의 실제 팩토리가 나오기 전까지 SYS_Bootstrap에 연결해 쓴다(M1에서 교체).
    /// 값은 디버그용이며 규칙 밸런스와 무관하다.
    /// </summary>
    [CreateAssetMenu(menuName = "UnityGameJam/Debug/Fake Match Session Factory", fileName = "FakeMatchSessionFactory")]
    public sealed class FakeMatchSessionFactorySO : MatchSessionFactoryBase
    {
        [SerializeField] private float _matchSeconds = 180f;
        [SerializeField] private float _baseMoveSpeed = 4f;
        [Tooltip("무거운 잔해 운반 중 속도 배율 (U20: 0.5)")]
        [SerializeField] private float _heavyCarryMultiplier = 0.5f;
        [SerializeField] private float _mopSeconds = 2f;
        [Tooltip("구역·유형마다 놓을 가짜 개체 수 (생성 지점 앞에서부터)")]
        [SerializeField] private int _itemsPerTypePerZone = 2;

        public override IMatchSession Create(MatchSetup setup)
        {
            return new FakeMatchSession(setup, new FakeMatchSettings(_matchSeconds, _baseMoveSpeed, _heavyCarryMultiplier, _mopSeconds, _itemsPerTypePerZone));
        }
    }
}
