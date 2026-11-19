using System.Collections.Generic;
using UnityEngine;
using UnityGameJam.Contract;
using UnityGameJam.Player;

namespace UnityGameJam.View
{
    [RequireComponent(typeof(GameSfxPlayer))]
    [DefaultExecutionOrder(160)]
    public sealed class GameAudioPresenter : MonoBehaviour
    {
        private sealed class Unit
        {
            internal EWorkerType Worker;
            internal Transform Transform;
            internal CharacterAnimatorDriver Driver;
            internal MinionAgent Agent;
            internal PlayerController Player;
            internal Vector3 LastPosition;
            internal float Distance;
            internal int StepPhase = -1;
            internal AnimationClip LastClip;
            internal bool HasMop;
            internal EMinionActivity Activity;
        }

        private sealed class Contact
        {
            internal Unit Unit;
            internal ESfxCue Cue;
            internal int Variant;
            internal bool PickingUp;
            internal bool Organized;
            internal Vector3 Position;
            internal EFloor Floor;
        }

        private static GameAudioPresenter _instance;
        private MatchBootstrap _bootstrap;
        private IMatchSession _session;
        private GameSfxPlayer _player;
        private readonly Dictionary<EWorkerType, Unit> _units = new();
        private readonly Dictionary<int, ItemSnapshot> _items = new();
        private readonly List<Contact> _contacts = new();
        private readonly Dictionary<int, MessageEntry> _messages = new();
        private readonly HashSet<int> _heardMessages = new();
        private readonly HashSet<int> _handledMessages = new();
        private readonly HashSet<int> _visibleMessages = new();
        private int _previousSecond;
        private float _previousElapsed;

        private void Awake()
        {
            _instance = this;
            _player = GetComponent<GameSfxPlayer>();
            _bootstrap = FindFirstObjectByType<MatchBootstrap>();
        }

        private void OnEnable()
        {
            if (_bootstrap == null) { return; }
            _bootstrap.MatchStarted += Bind;
            if (_bootstrap.Session != null) { Bind(_bootstrap); }
        }

        private void OnDisable()
        {
            if (_bootstrap != null) { _bootstrap.MatchStarted -= Bind; }
            if (_session != null)
            {
                _session.ItemChanged -= OnItemChanged;
                _session.MatchStateChanged -= OnStateChanged;
            }
            if (_instance == this) { _instance = null; }
        }

        private void Bind(MatchBootstrap bootstrap)
        {
            if (_session == bootstrap.Session) { return; }
            _session = bootstrap.Session;
            AddUnit(EWorkerType.Player, bootstrap.Player.transform);
            foreach (MinionAgent minion in bootstrap.Minions) { AddUnit(minion.Worker, minion.transform); }
            foreach (ItemSnapshot item in _session.Items) { _items[item.ItemId] = item; }
            _previousSecond = Mathf.CeilToInt(_session.RemainingTime);
            _session.ItemChanged += OnItemChanged;
            _session.MatchStateChanged += OnStateChanged;
            _player.BeginMatch();
        }

        private void AddUnit(EWorkerType worker, Transform body)
        {
            _units[worker] = new Unit
            {
                Worker = worker, Transform = body, LastPosition = body.position,
                Driver = body.GetComponent<CharacterAnimatorDriver>(), Agent = body.GetComponent<MinionAgent>(),
                Player = body.GetComponent<PlayerController>()
            };
        }

        private void OnStateChanged(EMatchState state)
        {
            if (state != EMatchState.DemonKingArrived) { return; }
            _contacts.Clear();
            _player.EndRound();
        }

        private void LateUpdate()
        {
            if (_session == null || _session.MatchState != EMatchState.Running) { return; }
            IReadOnlyList<MinionStatus> statuses = _bootstrap.PresentedMinions;
            UpdateMop(_units[EWorkerType.Player], _session.IsPlayerHoldingMop, _session.IsPlayerWorking);
            foreach (MinionStatus status in statuses)
            {
                if (!_units.TryGetValue(status.Minion, out Unit unit)) { continue; }
                bool isWorking = unit.Driver != null && unit.Driver.CurrentPose == EPose.Mop
                    && status.Activity == EMinionActivity.Working;
                UpdateMop(unit, status.IsHoldingMop, isWorking);
                if (status.Minion == EWorkerType.Hellhound)
                {
                    if (status.Activity == EMinionActivity.Dispatching && unit.Activity != status.Activity)
                    {
                        _player.PlayWorld(ESfxCue.HoundDispatch, unit.Transform.position, Floor(unit));
                    }
                    else if (status.Activity == EMinionActivity.LobbyIdle &&
                        unit.Activity is EMinionActivity.ReturningToLobby or EMinionActivity.Dispatching)
                    {
                        _player.PlayWorld(ESfxCue.HoundReturn, unit.Transform.position, Floor(unit));
                    }
                }
                unit.Activity = status.Activity;
            }
            foreach (Unit unit in _units.Values) { UpdateStep(unit, Floor(unit)); }
            PlayContacts();
            UpdateMessages();
            UpdateWarnings();
        }

        private EFloor Floor(Unit unit) => unit.Agent != null ? unit.Agent.Floor : _bootstrap.Player.Floor;

        private void UpdateMop(Unit unit, bool hasMop, bool isWorking)
        {
            if (unit.HasMop != hasMop)
            {
                QueueContact(unit, hasMop ? ESfxCue.MopBorrow : ESfxCue.MopReturn, hasMop, -1, false,
                    unit.Transform.position, Floor(unit));
                unit.HasMop = hasMop;
            }
            _player.SetScrub(unit.Worker, isWorking, unit.Transform.position, Floor(unit));
        }

        private void UpdateStep(Unit unit, EFloor floor)
        {
            Vector3 position = unit.Transform.position;
            float distance = Vector3.ProjectOnPlane(position - unit.LastPosition, Vector3.up).magnitude;
            unit.LastPosition = position;
            bool isTransit = (unit.Agent != null && unit.Agent.IsInStairTransit)
                || (unit.Player != null && unit.Player.IsInStairTransit);
            if (isTransit || distance <= 0.001f || distance > _player.Library.MaximumDistance ||
                (unit.Driver != null && unit.Driver.IsHandling))
            {
                unit.Distance = 0f;
                unit.StepPhase = -1;
                return;
            }
            unit.Distance += distance;
            ESfxCue cue = unit.Worker switch
            {
                EWorkerType.Orc => ESfxCue.HeavyStep,
                EWorkerType.Skeleton => ESfxCue.SkeletonStep,
                EWorkerType.Slime => ESfxCue.SlimeStep,
                EWorkerType.Hellhound => ESfxCue.HoundStep,
                _ => ESfxCue.LightStep
            };
            AnimationClip clip = unit.Driver != null ? unit.Driver.CurrentClip : null;
            bool hasContact = false;
            if (clip != null && clip.length > 0f && unit.Worker != EWorkerType.Slime)
            {
                int phase = Mathf.FloorToInt(unit.Driver.ClipTime * 2f / clip.length);
                hasContact = unit.StepPhase >= 0 && (unit.StepPhase != phase || unit.LastClip != clip);
                unit.StepPhase = phase;
                unit.LastClip = clip;
            }
            else
            {
                float stride = unit.Worker == EWorkerType.Slime
                    ? _player.Library.SlimeStepDistance : _player.Library.StepDistance;
                hasContact = unit.Distance >= stride;
            }
            if (!hasContact) { return; }
            unit.Distance = 0f;
            _player.PlayWorld(cue, position, floor);
        }

        private void OnItemChanged(ItemSnapshot item)
        {
            if (!_items.TryGetValue(item.ItemId, out ItemSnapshot before)) { _items[item.ItemId] = item; return; }
            _items[item.ItemId] = item;
            if (before.State == item.State) { return; }
            EFloor floor = FindFloor(item.Position);
            if (item.State == EItemState.Digesting)
            {
                _player.PlayWorld(ESfxCue.Swallow, item.Position, floor);
                return;
            }
            if (before.State == EItemState.Digesting && item.State == EItemState.Resolved)
            {
                Vector3 position = _units[EWorkerType.Slime].Transform.position;
                _player.PlayWorld(ESfxCue.DigestDone, position, Floor(_units[EWorkerType.Slime]));
                return;
            }
            if (item.Type == EContaminationType.FloorStain && item.State == EItemState.Resolved)
            {
                _player.PlayWorld(ESfxCue.StainClear, item.Position, floor);
                return;
            }
            bool pickingUp = item.State == EItemState.Carried;
            bool dropping = before.State == EItemState.Carried && (item.State == EItemState.OnFloor ||
                item.State == EItemState.InDischargeArea || item.State == EItemState.Resolved);
            if (!pickingUp && !dropping) { return; }
            bool isEnding = _session.MatchState != EMatchState.Running;
            if (isEnding && item.Type == EContaminationType.ClutterItem) { return; }
            EWorkerType worker = (pickingUp ? item.Carrier : before.Carrier) ?? EWorkerType.Player;
            if (!_units.TryGetValue(worker, out Unit unit)) { return; }
            var (cue, variant) = ResolveHandling(item, pickingUp);
            if (isEnding) { _player.PlayWorld(cue, item.Position, floor, variant); return; }
            QueueContact(unit, cue, pickingUp, variant, item.State == EItemState.Resolved,
                item.Position, floor);
        }

        private void QueueContact(Unit unit, ESfxCue cue, bool pickingUp, int variant, bool organized,
            Vector3 position, EFloor floor)
        {
            _contacts.Add(new Contact { Unit = unit, Cue = cue, PickingUp = pickingUp,
                Variant = variant, Organized = organized, Position = position, Floor = floor });
        }

        private void PlayContacts()
        {
            for (int i = _contacts.Count - 1; i >= 0; i--)
            {
                Contact contact = _contacts[i];
                CharacterAnimatorDriver driver = contact.Unit.Driver;
                if (driver != null && driver.IsHandling && driver.GripHasItem != contact.PickingUp) { continue; }
                Vector3 position = contact.PickingUp ? contact.Unit.Transform.position : contact.Position;
                _player.PlayWorld(contact.Cue, position, contact.Floor, contact.Variant);
                if (contact.Organized) { _player.PlayWorld(ESfxCue.Organized, position, contact.Floor); }
                _contacts.RemoveAt(i);
            }
        }

        private (ESfxCue Cue, int Variant) ResolveHandling(ItemSnapshot item, bool pickingUp)
        {
            string kind = ContaminationViewRules.ResolveClutterKind(item.HomeSlotId, _bootstrap.HomeSlotKinds);
            GameObject prefab = _bootstrap.ViewSettings.ResolvePrefab(item, kind);
            string name = prefab != null ? prefab.name : "";
            bool heavy = item.Type == EContaminationType.HeavyDebris || kind == "Chair";
            int variant = -1;
            if (heavy)
            {
                variant = name.Contains("Masonry") ? 1 : name.Contains("Furniture") ? 2 : 0;
                return (pickingUp ? ESfxCue.HeavyPickup : ESfxCue.HeavyPlace, variant);
            }
            bool isSoft = item.Type == EContaminationType.Trash && !name.Contains("DrinkCan")
                || kind == "Document";
            if (isSoft)
            {
                variant = name.Contains("Wrapper") || name.Contains("TrashBag") ? 1 : 0;
                return (pickingUp ? ESfxCue.SoftPickup : ESfxCue.SoftPlace, variant);
            }
            variant = kind == "Cup" || name.Contains("DrinkCan") ? 1 : 0;
            return (pickingUp ? ESfxCue.HardPickup : ESfxCue.HardPlace, variant);
        }

        private EFloor FindFloor(Vector3 position)
        {
            foreach (FloorArea floor in _bootstrap.Layout.Floors)
            {
                if (FacilityLayoutValidator.ContainsXZ(floor.Area, position)) { return floor.Floor; }
            }
            return _bootstrap.Player.Floor;
        }

        internal static void MarkHandled(int messageId)
        {
            _instance?._handledMessages.Add(messageId);
        }

        internal static void MarkWorkerHandled(EWorkerType worker)
        {
            if (_instance == null) { return; }
            foreach (MessageEntry message in _instance._messages.Values)
            {
                if (message.Minion == worker) { _instance._handledMessages.Add(message.MessageId); }
            }
        }

        internal static void QuietNotice(int messageId)
        {
            _instance?._heardMessages.Add(messageId);
        }

        private void UpdateMessages()
        {
            float elapsed = _bootstrap.MatchDuration - _session.RemainingTime;
            IReadOnlyList<MessageEntry> messages = _session.ActiveMessages;
            _visibleMessages.Clear();
            bool newProposal = false;
            bool newNotice = false;
            foreach (MessageEntry message in messages)
            {
                _visibleMessages.Add(message.MessageId);
                if (!_heardMessages.Add(message.MessageId)) { continue; }
                if (message.Kind == EMessageKind.Proposal) { newProposal = true; }
                else { newNotice = true; }
            }
            foreach (MessageEntry previous in _messages.Values)
            {
                if (!_visibleMessages.Contains(previous.MessageId) && previous.IsInputTarget
                    && previous.Kind == EMessageKind.Proposal && !_handledMessages.Contains(previous.MessageId)
                    && elapsed >= _previousElapsed + previous.RemainingTime - 0.001f)
                {
                    GameSfxPlayer.Play(ESfxCue.ProposalExpire);
                }
            }
            if (newProposal) { GameSfxPlayer.Play(ESfxCue.Proposal); }
            else if (newNotice) { GameSfxPlayer.Play(ESfxCue.Notice); }
            _messages.Clear();
            foreach (MessageEntry message in messages) { _messages[message.MessageId] = message; }
            _previousElapsed = elapsed;
        }

        private void UpdateWarnings()
        {
            int second = Mathf.Max(0, Mathf.CeilToInt(_session.RemainingTime));
            if (!_bootstrap.IsFastForwarding)
            {
                ESfxCue cue = ESfxCue.None;
                for (int crossed = _previousSecond - 1; crossed >= second; crossed--)
                {
                    if (crossed == 60) { cue = ESfxCue.OneMinute; }
                    else if (crossed > 0 && crossed <= 3) { cue = ESfxCue.Heartbeat; }
                    else if (crossed >= 10 && crossed <= 50 && crossed % 10 == 0) { cue = ESfxCue.TimerNudge; }
                }
                GameSfxPlayer.Play(cue);
            }
            _previousSecond = second;
        }
    }
}
