using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityGameJam.Contract;
using UnityGameJam.Player;

namespace UnityGameJam.View
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(MatchBootstrap))]
    [DisallowMultipleComponent]
    public sealed class ContaminationView : MonoBehaviour
    {
        private const int OpeningItemsPerFrame = 8;

        private sealed class HeldVisual
        {
            public readonly GameObject Instance;
            public readonly Vector3 GripOffset;
            public EWorkerType? Carrier;
            public SpriteRenderer Glow;

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
        private readonly Dictionary<int, ItemSnapshot> _changedItems = new();
        private readonly List<ItemSnapshot> _movingItems = new();
        private readonly Dictionary<EWorkerType, (Transform Body, float Height, CharacterAnimatorDriver Driver)> _carriers = new();
        private readonly Dictionary<EWorkerType, HeldVisual> _mops = new();
        private readonly HashSet<EContaminationType> _missing = new();
        private MatchBootstrap _bootstrap;
        private IMatchSession _session;
        private PlayerInteractor _playerInteractor;
        private Sprite _glowSprite;
        private SpriteRenderer _homeGlow;
        private readonly SpriteRenderer[] _skillWaves = new SpriteRenderer[2];
        private readonly Dictionary<EWorkerType, SpriteRenderer> _boostGlows = new();
        private CommandSkillCast _skillCast;
        private float _skillEffectStarted;
        private bool _isMopMissing;
        private int _preparationVersion;

        private void Awake()
        {
            _bootstrap = GetComponent<MatchBootstrap>();
        }

        private void OnEnable()
        {
            if (_bootstrap.Session != null) { StartCoroutine(Prepare(int.MaxValue)); }
        }

        internal IEnumerator PrepareOpening() => Prepare(OpeningItemsPerFrame);

        private IEnumerator Prepare(int itemsPerFrame)
        {
            if (_session != null) { yield break; }
            int version = ++_preparationVersion;
            _session = _bootstrap.Session;
            _playerInteractor = _bootstrap.Player.GetComponent<PlayerInteractor>();
            _carriers[EWorkerType.Player] = (_bootstrap.Player.transform, _bootstrap.Player.GetComponent<CharacterController>().height,
                _bootstrap.Player.GetComponent<CharacterAnimatorDriver>());
            foreach (MinionAgent minion in _bootstrap.Minions)
            {
                _carriers[minion.Worker] = (minion.transform, minion.GetComponent<NavMeshAgent>().height,
                    minion.GetComponent<CharacterAnimatorDriver>());
            }

            _session.ItemChanged += OnItemChanged;
            IReadOnlyList<ItemSnapshot> items = _session.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ItemSnapshot item = items[i];
                if (UpdateItem(item)) { _movingItems.Add(item); }
                if ((i + 1) % itemsPerFrame == 0 && i + 1 < items.Count)
                {
                    yield return null;
                    if (version != _preparationVersion) { yield break; }
                }
            }
        }

        private void OnDisable()
        {
            _preparationVersion++;
            if (_session != null) { _session.ItemChanged -= OnItemChanged; }
            _session = null;
            _changedItems.Clear();
            _movingItems.Clear();
            if (_homeGlow != null) { _homeGlow.enabled = false; }
            HideSkillEffects();
            foreach (HeldVisual item in _items.Values)
            {
                if (item.Glow != null) { item.Glow.enabled = false; }
                item.Instance.SetActive(false);
            }
            foreach (HeldVisual mop in _mops.Values) { mop.Instance.SetActive(false); }
        }

        private void OnDestroy()
        {
            if (_glowSprite == null) { return; }
            Destroy(_glowSprite.texture);
            Destroy(_glowSprite);
        }

        private void OnItemChanged(ItemSnapshot item)
        {
            _changedItems[item.ItemId] = item;
        }

        private void LateUpdate()
        {
            if (_session == null || _session.MatchState == EMatchState.Ready) { return; }

            for (int i = _movingItems.Count - 1; i >= 0; i--)
            {
                ItemSnapshot item = _movingItems[i];
                if (_changedItems.ContainsKey(item.ItemId) || !UpdateItem(item)) { _movingItems.RemoveAt(i); }
            }
            foreach (ItemSnapshot item in _changedItems.Values)
            {
                if (UpdateItem(item)) { _movingItems.Add(item); }
            }
            _changedItems.Clear();
            UpdateWorkGlows();
            UpdateSkillEffects();

            UpdateMop(EWorkerType.Player, _session.IsPlayerHoldingMop);
            IReadOnlyList<MinionStatus> minions = _bootstrap.PresentedMinions;
            for (int i = 0; i < minions.Count; i++)
            {
                MinionStatus minion = minions[i];
                UpdateMop(minion.Minion, minion.IsHoldingMop);
            }
        }

        private void UpdateWorkGlows()
        {
            FloorArea visibleFloor = null;
            PlayerInteractionContext context = default;
            if (_playerInteractor != null && _playerInteractor.TryGetGroundWorkContext(out context))
            {
                foreach (FloorArea floor in _bootstrap.Layout.Floors)
                {
                    if (floor.Floor == context.PlayerFloor) { visibleFloor = floor; break; }
                }
            }

            float pulse = .84f + .16f * Mathf.Sin(Time.time * 4f);
            int selected = _playerInteractor != null ? _playerInteractor.CurrentChoice.ItemId : -1;
            IReadOnlyList<ItemSnapshot> items = _session.Items;
            for (int i = 0; i < items.Count; i++)
            {
                ItemSnapshot item = items[i];
                if (!_items.TryGetValue(item.ItemId, out HeldVisual visual)) { continue; }
                bool visible = visibleFloor != null && !visual.Carrier.HasValue && PlayerInteractionSelector.CanHighlightItem(context, item)
                    && FacilityLayoutValidator.ContainsXZ(visibleFloor.Area, item.Position)
                    && _bootstrap.CanSeeAt(item.Position, visibleFloor.Floor);
                if (visible && visual.Glow == null) { visual.Glow = CreateGlow(visual, item.Type); }
                if (visual.Glow == null) { continue; }
                visual.Glow.enabled = visible;
                if (visible)
                {
                    float intensity = selected == item.ItemId ? 1f : .7f;
                    visual.Glow.color = new Color(1f, .79f, .38f, pulse * intensity);
                }
            }
            UpdateHomeGlow(_playerInteractor != null ? _playerInteractor.HeldHomeSlot : null, pulse);
        }

        private void UpdateHomeGlow(HomeSlot slot, float pulse)
        {
            bool visible = slot != null && _session.MatchState == EMatchState.Running
                && _bootstrap.CanSeeAt(slot.Position, ZoneDisplayName.GetFloor(slot.Zone));
            if (visible)
            {
                if (_homeGlow == null) { _homeGlow = CreateGlow("작은 물건 제자리 발광", transform, 2f); }
                // 슬롯 위치는 가구 아래 바닥에 있으므로 표시만 가구 위로 올린다.
                _homeGlow.transform.position = slot.Position + Vector3.up * 1.18f;
                _homeGlow.transform.localScale = Vector3.one * Mathf.Lerp(1.8f, 2.4f, Mathf.InverseLerp(.68f, 1f, pulse));
                _homeGlow.color = new Color(.40f, 1f, .72f, pulse);
            }
            if (_homeGlow != null) { _homeGlow.enabled = visible; }
        }

        private void UpdateSkillEffects()
        {
            if (_session.MatchState != EMatchState.Running || _session is not ICommandSkillSession skills)
            {
                HideSkillEffects();
                return;
            }

            CommandSkillCast cast = skills.LastSkillCast;
            if (cast.Sequence != _skillCast.Sequence)
            {
                _skillCast = cast;
                _skillEffectStarted = Time.time;
                GameSfxPlayer.PlayCommandSkill(cast.Skill);
            }
            for (int i = 0; i < _skillWaves.Length; i++)
            {
                float time = Time.time - _skillEffectStarted - i * .15f;
                bool visible = cast.Sequence > 0 && cast.Floor == _bootstrap.Player.Floor && time >= 0f && time < .66f;
                if (visible)
                {
                    SpriteRenderer wave = _skillWaves[i] ??= CreateGlow("지휘 기술 충격파", transform, 1f);
                    float t = time / .66f;
                    float spread = cast.Skill == ECommandSkill.Shout ? 16f : 11f;
                    wave.transform.position = cast.Position + Vector3.up * (.11f + i * .05f);
                    wave.transform.localScale = Vector3.one * Mathf.Lerp(1f, spread, 1f - (1f - t) * (1f - t));
                    wave.color = cast.Skill == ECommandSkill.Shout
                        ? new Color(1f, .25f + i * .25f, .16f, (1f - t) * .95f)
                        : new Color(.78f + i * .18f, .43f + i * .27f, 1f - i * .62f, (1f - t) * .92f);
                }
                if (_skillWaves[i] != null) { _skillWaves[i].enabled = visible; }
            }

            foreach (MinionAgent minion in _bootstrap.Minions)
            {
                float left = skills.BoostRemaining(minion.Worker);
                bool visible = left > 0f && _bootstrap.CanSeeMinion(minion) && !minion.IsInStairTransit;
                _boostGlows.TryGetValue(minion.Worker, out SpriteRenderer glow);
                if (visible)
                {
                    if (glow == null)
                    {
                        glow = CreateGlow("증진 발광", transform, 1.4f);
                        _boostGlows[minion.Worker] = glow;
                    }
                    float rise = Mathf.Clamp01((skills.BoostDuration - left) / .48f);
                    float wave = .5f + .5f * Mathf.Sin(Time.time * 7f);
                    glow.transform.position = minion.Position + Vector3.up * (.09f + 1.05f * Mathf.Sin(rise * Mathf.PI));
                    glow.transform.localScale = Vector3.one * Mathf.Lerp(1.9f, 1.3f + wave * .2f, rise);
                    glow.color = new Color(.86f, .55f + wave * .20f, 1f, Mathf.Min(1f, left * 4f) * Mathf.Lerp(1f, .53f, rise));
                }
                if (glow != null) { glow.enabled = visible; }
            }
        }

        private void HideSkillEffects()
        {
            foreach (SpriteRenderer wave in _skillWaves) { if (wave != null) { wave.enabled = false; } }
            foreach (SpriteRenderer glow in _boostGlows.Values) { if (glow != null) { glow.enabled = false; } }
        }

        private SpriteRenderer CreateGlow(HeldVisual visual, EContaminationType type)
        {
            float diameter = type switch
            {
                EContaminationType.HeavyDebris => 1.65f,
                EContaminationType.FloorStain => 1.35f,
                _ => 1f
            };
            return CreateGlow("작업 대상 발광", visual.Instance.transform, diameter);
        }

        private SpriteRenderer CreateGlow(string label, Transform parent, float diameter)
        {
            if (_glowSprite == null) { _glowSprite = CreateGlowSprite(); }
            var glow = new GameObject(label).AddComponent<SpriteRenderer>();
            glow.sprite = _glowSprite;
            glow.transform.SetParent(parent, false);
            glow.transform.SetLocalPositionAndRotation(Vector3.up * .035f, Quaternion.Euler(-90f, 0f, 0f));
            glow.transform.localScale = Vector3.one * diameter;
            return glow;
        }

        private static Sprite CreateGlowSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "작업 대상 발광", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(2f * (x + .5f) / size - 1f, 2f * (y + .5f) / size - 1f).magnitude;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(radius - .68f) / .27f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(235f * ring * ring));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect);
        }

        private bool UpdateItem(ItemSnapshot item)
        {
            bool visible = ContaminationViewRules.IsVisible(item.State);
            if (!_items.TryGetValue(item.ItemId, out HeldVisual visual))
            {
                if (!visible) { return false; }
                string kind = ContaminationViewRules.ResolveClutterKind(item.HomeSlotId, _bootstrap.HomeSlotKinds);
                GameObject prefab = _bootstrap.ViewSettings.ResolvePrefab(item, kind);
                if (prefab == null)
                {
                    if (_missing.Add(item.Type)) { Debug.LogWarning($"[오염 표시] {item.Type} 프리팹이 없습니다.", this); }
                    return false;
                }
                visual = new HeldVisual(Instantiate(prefab, transform));
                _items.Add(item.ItemId, visual);
            }

            bool held = item.State == EItemState.Carried;
            if (held) { visual.Carrier = item.Carrier; }
            bool canFinishDrop = item.State == EItemState.OnFloor || item.State == EItemState.InDischargeArea
                || (item.State == EItemState.Resolved && item.Type is (EContaminationType.Trash or EContaminationType.ClutterItem));
            bool onHand = (held || canFinishDrop) && FollowGrip(visual, item.Type, held);
            if (!held && !onHand) { visual.Carrier = null; }
            visible |= onHand;
            if (visual.Instance.activeSelf != visible) { visual.Instance.SetActive(visible); }
            if (visible && !onHand)
            {
                visual.Instance.transform.SetPositionAndRotation(item.Position, Quaternion.identity);
            }
            return held || onHand;
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
                || _session.MatchState != EMatchState.Running)) { return false; }
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
