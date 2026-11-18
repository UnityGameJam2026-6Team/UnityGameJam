using UnityEngine;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Game Rule", fileName = "GameRule")]
    public sealed class GameRuleSO : ScriptableObject
    {
        [Header("판 규칙")]
        [Tooltip("마왕이 도착하기까지 주어지는 시간(초)")]
        [SerializeField, Min(0f)] private float _matchSeconds;
        [Tooltip("각 구역이 달성해야 하는 최소 청결도(%)")]
        [SerializeField, Range(0f, 100f)] private float _zoneThreshold;
        [Tooltip("시설 전체가 달성해야 하는 최소 청결도(%)")]
        [SerializeField, Range(0f, 100f)] private float _facilityThreshold;

        [Header("알림과 상호작용")]
        [SerializeField, Min(0f)] private float _proposalSeconds;
        [SerializeField, Min(0f)] private float _noticeSeconds;
        [SerializeField, Min(0f)] private float _apiTimeoutSeconds;
        [Tooltip("대상 탐색과 실제 행동 판정에 적용하는 거리(m)")]
        [SerializeField, Min(0.01f)] private float _interactionDistance;
        [SerializeField, Min(1)] private int _mopsPerToilet;
        [SerializeField, Min(0f)] private float _observationRadius;

        public float MatchSeconds => _matchSeconds;
        public float ZoneThreshold => _zoneThreshold;
        public float FacilityThreshold => _facilityThreshold;
        public float ProposalSeconds => _proposalSeconds;
        public float NoticeSeconds => _noticeSeconds;
        public float ApiTimeoutSeconds => _apiTimeoutSeconds;
        public float InteractionDistance => _interactionDistance;
        public int MopsPerToilet => _mopsPerToilet;
        public float ObservationRadius => _observationRadius;
    }
}
