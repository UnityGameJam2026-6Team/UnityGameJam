using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(MatchBootstrap))]
    [DisallowMultipleComponent]
    public sealed class ContaminationView : MonoBehaviour
    {
        private sealed class HeldVisual
        {
            public readonly GameObject Instance;
            public readonly Vector3 GripOffset;
            public EWorkerType? Carrier;

            public HeldVisual(GameObject instance)
            {
                Instance = instance;
                Transform grip = instance.transform.Find("Grip");
                if (grip != null)
                {
                    GripOffset = instance.transform.InverseTransformPoint(grip.position);
                    return;
                }

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { return; }
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) { bounds.Encapsulate(renderers[i].bounds); }
                GripOffset = instance.transform.InverseTransformPoint(bounds.center);
            }

            public void Attach(Vector3 position, Quaternion rotation)
            {
                Transform root = Instance.transform;
                root.rotation = rotation;
                root.position = position - root.TransformVector(GripOffset);
            }
        }

        private readonly Dictionary<int, HeldVisual> _items = new();
        private readonly Dictionary<EWorkerType, (Transform Body, float Height, CharacterAnimatorDriver Driver)> _carriers = new();
        private readonly Dictionary<EWorkerType, HeldVisual> _mops = new();
        private readonly HashSet<EContaminationType> _missing = new();
        private MatchBootstrap _bootstrap;
        private bool _isMopMissing;

        private void Awake()
        {
            _bootstrap = GetComponent<MatchBootstrap>();
        }

        private void OnEnable()
        {
            _bootstrap.MatchStarted += Bind;
            if (_bootstrap.Session != null) { Bind(_bootstrap); }
        }

        private void Bind(MatchBootstrap bootstrap)
        {
            _carriers[EWorkerType.Player] = (bootstrap.Player.transform, bootstrap.Player.GetComponent<CharacterController>().height,
                bootstrap.Player.GetComponent<CharacterAnimatorDriver>());
            foreach (MinionAgent minion in bootstrap.Minions)
            {
                _carriers[minion.Worker] = (minion.transform, minion.GetComponent<NavMeshAgent>().height,
                    minion.GetComponent<CharacterAnimatorDriver>());
            }
        }

        private void OnDisable()
        {
            _bootstrap.MatchStarted -= Bind;
            foreach (HeldVisual item in _items.Values) { item.Instance.SetActive(false); }
            foreach (HeldVisual mop in _mops.Values) { mop.Instance.SetActive(false); }
        }

        private void LateUpdate()
        {
            IMatchSession session = _bootstrap.Session;
            if (session == null) { return; }
            foreach (ItemSnapshot item in session.Items) { UpdateItem(item); }
            UpdateMop(EWorkerType.Player, session.IsPlayerHoldingMop);
            foreach (MinionStatus minion in session.Minions) { UpdateMop(minion.Minion, minion.IsHoldingMop); }
        }

        private void UpdateItem(ItemSnapshot item)
        {
            bool visible = ContaminationViewRules.IsVisible(item.State);
            if (!_items.TryGetValue(item.ItemId, out HeldVisual visual))
            {
                if (!visible) { return; }
                string kind = ContaminationViewRules.ResolveClutterKind(item.HomeSlotId, _bootstrap.HomeSlotKinds);
                GameObject prefab = _bootstrap.ViewSettings.ResolvePrefab(item, kind);
                if (prefab == null)
                {
                    if (_missing.Add(item.Type)) { Debug.LogWarning($"[오염 표시] {item.Type} 프리팹이 없습니다.", this); }
                    return;
                }
                visual = new HeldVisual(Instantiate(prefab, transform));
                _items.Add(item.ItemId, visual);
            }

            bool held = item.State == EItemState.Carried;
            if (held) { visual.Carrier = item.Carrier; }
            bool canFinishDrop = item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea
                || (item.State == EItemState.Resolved && item.Type == EContaminationType.ClutterItem);
            bool onHand = (held || canFinishDrop) && FollowGrip(visual, item.Type, held);
            if (!held && !onHand) { visual.Carrier = null; }
            visible |= onHand;
            if (visual.Instance.activeSelf != visible) { visual.Instance.SetActive(visible); }
            if (visible && !onHand)
            {
                visual.Instance.transform.SetPositionAndRotation(item.Position, Quaternion.identity);
            }
        }

        private void UpdateMop(EWorkerType worker, bool held)
        {
            if (!_mops.TryGetValue(worker, out HeldVisual mop))
            {
                if (!held) { return; }
                if (_bootstrap.ViewSettings.MopPrefab == null)
                {
                    if (!_isMopMissing) { Debug.LogWarning("[오염 표시] 걸레 프리팹이 없습니다.", this); }
                    _isMopMissing = true;
                    return;
                }
                mop = new HeldVisual(Instantiate(_bootstrap.ViewSettings.MopPrefab, transform));
                _mops.Add(worker, mop);
            }
            if (held) { mop.Carrier = worker; }
            bool onHand = FollowGrip(mop, null, held);
            if (!held && !onHand) { mop.Carrier = null; }
            if (mop.Instance.activeSelf != onHand) { mop.Instance.SetActive(onHand); }
        }

        private bool FollowGrip(HeldVisual visual, EContaminationType? type, bool held)
        {
            if (!visual.Carrier.HasValue || !_carriers.TryGetValue(visual.Carrier.Value, out var carrier)
                || carrier.Body == null) { return false; }
            CharacterAnimatorDriver driver = carrier.Driver;
            // 규칙이 상태를 변경한 프레임과 실제로 손이 물건에 닿는 프레임 사이를 연결한다.
            if (!held && (driver == null || !driver.IsHandling || driver.IsPickingUp
                || _bootstrap.Session.MatchState != EMatchState.Running)) { return false; }
            if (driver != null && driver.IsHandling && !driver.GripHasItem) { return false; }
            if (driver != null && driver.TryGetGrip(type, out Vector3 hand, out Quaternion heading))
            {
                if (!type.HasValue && visual.GripOffset.y > 0f)
                {
                    // 손 높이가 다른 유닛도 걸레 머리가 바닥을 뚫지 않도록 손잡이를 앞으로 기울인다.
                    float reach = Mathf.Clamp01((hand.y - carrier.Body.position.y) / visual.GripOffset.y);
                    heading *= Quaternion.Euler(-Mathf.Acos(reach) * Mathf.Rad2Deg, 0f, 0f);
                }
                visual.Attach(hand, heading);
                return true;
            }
            Vector3 fallback = type.HasValue
                ? ContaminationViewRules.CarryPosition(carrier.Body.position, carrier.Height, _bootstrap.ViewSettings.CarryOffset)
                : carrier.Body.TransformPoint(_bootstrap.ViewSettings.MopOffset);
            visual.Instance.transform.SetPositionAndRotation(fallback, carrier.Body.rotation);
            return true;
        }
    }
}
