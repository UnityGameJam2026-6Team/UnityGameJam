using UnityEngine;

namespace UnityGameJam.UI
{
    /// <summary>
    /// 화면 오른쪽에 겹쳐 쌓인 졸개 카드 한 장의 움직임. 평소엔 초상화만 보이고 위치·상태 글자는 화면 밖에 있다.
    /// MinionCardStack이 마우스가 올라간 카드를 정해 주면 맨 앞으로 꺼내 왼쪽으로 밀어 글자가 보이게 하고,
    /// 마우스가 나가면 제자리로 돌아간 다음에 원래 겹침 순서로 되돌린다(돌아오는 도중에 뒤로 숨지 않도록).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class MinionCardHover : MonoBehaviour
    {
        [Tooltip("마우스를 올렸을 때 카드 왼쪽 위 모서리의 x (오른쪽 화면 끝 기준)")]
        [SerializeField] private float _hoverX = -415f;
        [Tooltip("마우스를 올렸을 때 위로 살짝 들어 올리는 양")]
        [SerializeField] private float _hoverLift = 12f;
        [SerializeField] private float _hoverRotation = 3f;
        [SerializeField] private float _hoverScale = 0.95f;
        [Tooltip("클수록 빨리 움직인다")]
        [SerializeField] private float _sharpness = 14f;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private float _restRotation;
        private float _restScale;
        private int _restSiblingIndex;
        private bool _isHovered;
        private bool _isOrderRestorePending;
        private float _blend;

        internal RectTransform Rect => _rect;
        internal int RestOrder => _restSiblingIndex;
        internal bool IsHovered => _isHovered;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _restPosition = _rect.anchoredPosition;
            _restRotation = _rect.localEulerAngles.z;
            _restScale = _rect.localScale.x;
            _restSiblingIndex = _rect.GetSiblingIndex();
        }

        private void OnDisable()
        {
            // Tab으로 패널을 접으면 꺼지므로, 다시 펼쳤을 때 제자리에서 시작하게 한다.
            // 부모가 켜지고 꺼지는 도중에는 겹침 순서를 바꿀 수 없어서, 다시 켜진 뒤 첫 Update에서 되돌린다.
            _isHovered = false;
            _blend = 0f;
            ApplyPose();
            _isOrderRestorePending = true;
        }

        internal void SetHovered(bool isHovered)
        {
            if (_isHovered == isHovered) { return; }
            _isHovered = isHovered;
            if (isHovered) { _rect.SetAsLastSibling(); }
        }

        /// <summary>제자리 자세의 카드가 부모 기준 좌표의 점을 덮는지. 움직이는 중에도 판정이 흔들리지 않게 제자리 값으로 계산한다.</summary>
        internal bool ContainsAtRest(Vector2 pointInParent, Vector2 anchorInParent)
        {
            Vector2 offset = pointInParent - (anchorInParent + _restPosition);
            offset = Quaternion.Euler(0f, 0f, -_restRotation) * offset;
            offset /= _restScale;
            Vector2 size = _rect.rect.size;
            // 피벗이 왼쪽 위(0, 1)이므로 카드는 x 0~폭, y -높이~0 범위다.
            return offset.x >= 0f && offset.x <= size.x && offset.y <= 0f && offset.y >= -size.y;
        }

        private void Update()
        {
            if (_isOrderRestorePending)
            {
                _isOrderRestorePending = false;
                if (!_isHovered) { _rect.SetSiblingIndex(_restSiblingIndex); }
            }

            float target = _isHovered ? 1f : 0f;
            if (_blend == target) { return; }

            _blend = Mathf.Lerp(_blend, target, 1f - Mathf.Exp(-_sharpness * Time.unscaledDeltaTime));
            if (Mathf.Abs(_blend - target) < 0.002f) { _blend = target; }
            ApplyPose();

            if (!_isHovered && _blend == 0f) { _rect.SetSiblingIndex(_restSiblingIndex); }
        }

        private void ApplyPose()
        {
            float t = Mathf.SmoothStep(0f, 1f, _blend);
            var hoverPosition = new Vector2(_hoverX, _restPosition.y + _hoverLift);
            _rect.anchoredPosition = Vector2.LerpUnclamped(_restPosition, hoverPosition, t);
            _rect.localEulerAngles = new Vector3(0f, 0f, Mathf.LerpAngle(_restRotation, _hoverRotation, t));
            float scale = Mathf.LerpUnclamped(_restScale, _hoverScale, t);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
