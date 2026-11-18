using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityGameJam.Contract;

namespace UnityGameJam.View
{
    [RequireComponent(typeof(MatchBootstrap))]
    [DisallowMultipleComponent]
    public sealed class ContaminationView : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _items = new Dictionary<int, GameObject>();
        private readonly Dictionary<EWorkerType, (Transform Body, float Height)> _carriers = new();
        private readonly Dictionary<EWorkerType, GameObject> _mops = new();
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
            _carriers[EWorkerType.Player] = (bootstrap.Player.transform, bootstrap.Player.GetComponent<CharacterController>().height);
            foreach (MinionAgent minion in bootstrap.Minions)
            {
                _carriers[minion.Worker] = (minion.transform, minion.GetComponent<NavMeshAgent>().height);
            }
        }

        private void OnDisable()
        {
            _bootstrap.MatchStarted -= Bind;
            foreach (GameObject item in _items.Values) { item.SetActive(false); }
            foreach (GameObject mop in _mops.Values) { mop.SetActive(false); }
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
            if (!_items.TryGetValue(item.ItemId, out GameObject instance))
            {
                if (!visible) { return; }
                string kind = ContaminationViewRules.ResolveClutterKind(item.HomeSlotId, _bootstrap.HomeSlotKinds);
                GameObject prefab = _bootstrap.ViewSettings.ResolvePrefab(item, kind);
                if (prefab == null)
                {
                    if (_missing.Add(item.Type)) { Debug.LogWarning($"[오염 표시] {item.Type} 프리팹이 없습니다.", this); }
                    return;
                }
                instance = Instantiate(prefab, transform);
                _items.Add(item.ItemId, instance);
            }
            if (instance.activeSelf != visible) { instance.SetActive(visible); }
            if (!visible) { return; }
            Vector3 position = item.Position;
            if (item.State == EItemState.Carried && item.Carrier.HasValue &&
                _carriers.TryGetValue(item.Carrier.Value, out var carrier) && carrier.Body != null)
            {
                position = ContaminationViewRules.CarryPosition(carrier.Body.position, carrier.Height, _bootstrap.ViewSettings.CarryOffset);
            }
            instance.transform.position = position;
        }

        private void UpdateMop(EWorkerType worker, bool held)
        {
            if (!_carriers.TryGetValue(worker, out var carrier) || carrier.Body == null) { held = false; }
            if (!_mops.TryGetValue(worker, out GameObject mop))
            {
                if (!held) { return; }
                if (_bootstrap.ViewSettings.MopPrefab == null)
                {
                    if (!_isMopMissing) { Debug.LogWarning("[오염 표시] 걸레 프리팹이 없습니다.", this); }
                    _isMopMissing = true;
                    return;
                }
                mop = Instantiate(_bootstrap.ViewSettings.MopPrefab, transform);
                _mops.Add(worker, mop);
            }
            if (mop.activeSelf != held) { mop.SetActive(held); }
            if (held)
            {
                mop.transform.SetPositionAndRotation(carrier.Body.TransformPoint(_bootstrap.ViewSettings.MopOffset), carrier.Body.rotation);
            }
        }
    }
}
