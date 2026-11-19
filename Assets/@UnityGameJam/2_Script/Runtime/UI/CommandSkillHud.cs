using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGameJam.Contract;

namespace UnityGameJam.UI
{
    internal sealed class CommandSkillHud
    {
        private static readonly Color _textColor = new Color(1f, .96f, .82f);
        private static readonly Color _cooldownHighlight = new Color(1f, .79f, .42f);
        private const float CooldownFlashSeconds = .25f;

        private sealed class Slot
        {
            internal Image Frame, Icon, Sweep;
            internal TMP_Text Time;
            internal Color Accent;
            internal int LastSeconds = -1;
            internal float FlashUntil;
            internal float CooldownFlashUntil;
        }

        private readonly Slot[] _slots = new Slot[2];
        private readonly Image _effectFill;
        private readonly TMP_Text _effectTime;
        private readonly Sprite _maskSprite;
        private int _lastEffect = -1;
        private int _lastCast;

        internal CommandSkillHud(RectTransform parent, TMP_FontAsset font, Sprite shout, Sprite surge)
        {
            Texture2D white = Texture2D.whiteTexture;
            _maskSprite = Sprite.Create(white, new Rect(0, 0, white.width, white.height), new Vector2(.5f, .5f));
            _slots[0] = MakeSlot(parent, font, "Q", "호통", shout, new Color(.82f, .36f, .30f), 0f);
            _slots[1] = MakeSlot(parent, font, "E", "증진", surge, new Color(.75f, .52f, 1f), 91f);
            Image track = MakeImage(parent, "증진 효과 배경", 91f, 115f, 80f, 4f, new Color(.13f, .07f, .20f, .95f));
            _effectFill = MakeImage(track.rectTransform, "증진 효과", 0f, 0f, 80f, 4f, _slots[1].Accent);
            _effectFill.sprite = _maskSprite;
            _effectFill.type = Image.Type.Filled;
            _effectFill.fillMethod = Image.FillMethod.Horizontal;
            _effectTime = MakeText(parent, font, "", 91f, 98f, 80f, 17f, 14f);
            track.gameObject.SetActive(false);
        }

        internal void Show(ICommandSkillSession skills, ECommandSkill? cooldownPressed)
        {
            float now = Time.unscaledTime;
            if (cooldownPressed is ECommandSkill pressed)
            {
                _slots[(int)pressed].CooldownFlashUntil = now + CooldownFlashSeconds;
            }
            if (skills.LastSkillCast.Sequence != _lastCast)
            {
                _lastCast = skills.LastSkillCast.Sequence;
                _slots[(int)skills.LastSkillCast.Skill].FlashUntil = now + .23f;
            }
            for (int i = 0; i < _slots.Length; i++)
            {
                var skill = (ECommandSkill)i;
                Slot slot = _slots[i];
                float left = skills.SkillCooldownRemaining(skill);
                int seconds = Mathf.CeilToInt(left);
                if (slot.LastSeconds != seconds)
                {
                    if (seconds == 0 && slot.LastSeconds > 0) { slot.FlashUntil = now + .46f; }
                    slot.LastSeconds = seconds;
                    slot.Time.text = seconds > 0 ? Seconds(seconds) : "";
                }
                slot.Sweep.fillAmount = Mathf.Clamp01(left / skills.SkillCooldown(skill));
                float cooldownPulse = Mathf.Clamp01((slot.CooldownFlashUntil - now) / CooldownFlashSeconds);
                Color iconColor = left > 0f ? new Color(.64f, .59f, .68f) : Color.white;
                slot.Icon.color = Color.Lerp(iconColor, _cooldownHighlight, cooldownPulse);
                slot.Time.color = Color.Lerp(_textColor, _cooldownHighlight, cooldownPulse);
                float pulse = Mathf.Clamp01((slot.FlashUntil - now) / .32f);
                slot.Frame.color = Color.Lerp(slot.Accent, new Color(1f, .96f, .77f), pulse);
                slot.Frame.rectTransform.localScale = Vector3.one * (1f + pulse * .055f);
            }

            float active = 0f;
            for (int worker = (int)EWorkerType.Goblin; worker <= (int)EWorkerType.Hellhound; worker++)
            {
                active = Mathf.Max(active, skills.BoostRemaining((EWorkerType)worker));
            }
            bool visible = active > 0f;
            _effectFill.transform.parent.gameObject.SetActive(visible);
            if (visible) { _effectFill.fillAmount = Mathf.Clamp01(active / skills.BoostDuration); }
            int effectSeconds = Mathf.CeilToInt(active);
            if (_lastEffect != effectSeconds)
            {
                _lastEffect = effectSeconds;
                _effectTime.text = visible ? $"효과 {Seconds(effectSeconds)}" : "";
            }
        }

        internal void Release() => Object.Destroy(_maskSprite);

        private Slot MakeSlot(RectTransform parent, TMP_FontAsset font, string key, string name, Sprite sprite, Color accent, float x)
        {
            Color dark = new Color(.12f, .07f, .13f, .98f);
            Image frame = MakeImage(parent, name, x, 0f, 80f, 80f, accent);
            RectTransform rect = frame.rectTransform;
            MakeImage(rect, "테두리", 2f, 2f, 76f, 76f, dark);
            Image icon = MakeImage(rect, "기술 아이콘", 5f, 5f, 70f, 70f, Color.white);
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            Image sweep = MakeImage(rect, "쿨다운 원형 마스크", 5f, 5f, 70f, 70f, new Color(.04f, .025f, .07f, .87f));
            sweep.sprite = _maskSprite;
            sweep.type = Image.Type.Filled;
            sweep.fillMethod = Image.FillMethod.Radial360;
            sweep.fillOrigin = (int)Image.Origin360.Top;
            sweep.fillAmount = 0f;

            MakeImage(rect, "키 배경", -3f, -4f, 24f, 23f, dark);
            MakeText(rect, font, key, -3f, -4f, 24f, 23f, 19f);
            MakeText(parent, font, name, x, 79f, 80f, 22f, 19f);
            TMP_Text timer = MakeText(rect, font, "", 3f, 30f, 74f, 38f, 25f);
            return new Slot { Frame = frame, Icon = icon, Sweep = sweep, Time = timer, Accent = accent };
        }

        private static string Seconds(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

        private static Image MakeImage(RectTransform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            SetRect(image.rectTransform, parent, x, y, width, height);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text MakeText(RectTransform parent, TMP_FontAsset font, string value,
            float x, float y, float width, float height, float size)
        {
            var text = new GameObject("텍스트", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            SetRect(text.rectTransform, parent, x, y, width, height);
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = _textColor;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform rect, RectTransform parent, float x, float y, float width, float height)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
