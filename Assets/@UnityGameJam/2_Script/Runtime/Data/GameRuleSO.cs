using UnityEngine;

namespace UnityGameJam.Data
{
    [CreateAssetMenu(menuName = "UnityGameJam/Data/Game Rule", fileName = "GameRule")]
    public sealed class GameRuleSO : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _matchSeconds;
        [SerializeField, Range(0f, 100f)] private float _zoneThreshold;
        [SerializeField, Range(0f, 100f)] private float _facilityThreshold;
        [SerializeField, Min(0f)] private float _proposalSeconds;
        [SerializeField, Min(0f)] private float _noticeSeconds;
        [SerializeField, Min(0f)] private float _apiTimeoutSeconds;
        [SerializeField, Min(0f)] private float _interactionDistance;
        [SerializeField, Min(1)] private int _mopsPerToilet;

        public float MatchSeconds => _matchSeconds;
        public float ZoneThreshold => _zoneThreshold;
        public float FacilityThreshold => _facilityThreshold;
        public float ProposalSeconds => _proposalSeconds;
        public float NoticeSeconds => _noticeSeconds;
        public float ApiTimeoutSeconds => _apiTimeoutSeconds;
        public float InteractionDistance => _interactionDistance;
        public int MopsPerToilet => _mopsPerToilet;
    }
}
