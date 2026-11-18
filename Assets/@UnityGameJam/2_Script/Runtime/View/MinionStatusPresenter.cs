using UnityEngine;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    /// <summary>
    /// 규칙이 알려 준 졸개 상태(MinionStatus)를 화면에 반영한다(Spec MinionAgent 규칙 6·7).
    /// 자세는 MinionPoseResolver로 고르고, 헬하운드는 배출하러 나가 도착하면 숨긴다.
    /// </summary>
    [RequireComponent(typeof(MinionAgent))]
    public sealed class MinionStatusPresenter : MonoBehaviour
    {
        [SerializeField] private CharacterAnimatorDriver _animatorDriver;
        [Tooltip("숨길 때 끄는 보이는 부분(Visual)")]
        [SerializeField] private GameObject _visual;

        private MinionAgent _agent;
        private IMatchReadModel _model;

        public EPose CurrentPose => _animatorDriver != null ? _animatorDriver.CurrentPose : EPose.Idle;
        public bool IsHidden => _visual != null && !_visual.activeSelf;

        private void Awake()
        {
            _agent = GetComponent<MinionAgent>();
        }

        public void Initialize(IMatchReadModel model)
        {
            _model = model;
        }

        private void Update()
        {
            if (_model == null || !TryGetStatus(out MinionStatus status)) { return; }

            EContaminationType? carried = null;
            bool isDigesting = false;
            foreach (ItemSnapshot item in _model.Items)
            {
                if (item.Carrier != _agent.Worker) { continue; }
                if (status.CarriedItemId == item.ItemId) { carried = item.Type; }
                if (item.State == EItemState.Digesting) { isDigesting = true; }
            }

            EPose pose = MinionPoseResolver.Resolve(_agent.CurrentSpeed, status.Activity, carried, status.IsHoldingMop, isDigesting);
            if (_animatorDriver != null) { _animatorDriver.SetPose(pose); }

            if (_agent.Worker == EWorkerType.Hellhound && _visual != null)
            {
                bool isAway = status.Activity == EMinionActivity.Dispatching && _agent.HasArrived;
                if (_visual.activeSelf == isAway) { _visual.SetActive(!isAway); }
            }
        }

        private bool TryGetStatus(out MinionStatus status)
        {
            foreach (MinionStatus candidate in _model.Minions)
            {
                if (candidate.Minion == _agent.Worker)
                {
                    status = candidate;
                    return true;
                }
            }
            status = default;
            return false;
        }
    }
}
