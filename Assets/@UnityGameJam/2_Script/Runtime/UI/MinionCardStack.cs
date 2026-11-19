using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityGameJam.UI
{
    /// <summary>
    /// 겹쳐 쌓인 졸개 카드 중 마우스가 올라간 카드 하나를 골라 MinionCardHover에 알려 준다.
    /// 씬의 EventSystem은 키보드 전용(Point 액션 없음)이라 포인터 이벤트가 오지 않으므로 마우스 위치를 직접 읽는다.
    /// 이미 꺼낸 카드는 마우스가 그 카드(지금 자리 또는 제자리) 위에 있는 동안 유지해서, 움직이는 중에 깜빡이지 않게 한다.
    /// 그 밖에는 겹침 순서상 가장 앞(아래쪽) 카드를 고른다.
    /// </summary>
    public sealed class MinionCardStack : MonoBehaviour
    {
        private MinionCardHover[] _cards = System.Array.Empty<MinionCardHover>();
        private RectTransform _rect;
        private Camera _eventCamera;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _cards = GetComponentsInChildren<MinionCardHover>(true);
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                _eventCamera = canvas.rootCanvas.worldCamera;
            }
        }

        private void OnDisable()
        {
            foreach (MinionCardHover card in _cards) { card.SetHovered(false); }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            MinionCardHover picked = null;
            if (mouse != null)
            {
                Vector2 screenPoint = mouse.position.ReadValue();
                picked = Pick(screenPoint);
            }
            foreach (MinionCardHover card in _cards) { card.SetHovered(card == picked); }
        }

        private MinionCardHover Pick(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screenPoint, _eventCamera, out Vector2 local))
            {
                return null;
            }
            // 카드는 이 영역의 오른쪽 아래 모서리를 기준으로 놓여 있다.
            var anchor = new Vector2(_rect.rect.xMax, _rect.rect.yMin);

            foreach (MinionCardHover card in _cards)
            {
                if (!card.IsHovered) { continue; }
                bool isOverNow = RectTransformUtility.RectangleContainsScreenPoint(card.Rect, screenPoint, _eventCamera);
                if (isOverNow || card.ContainsAtRest(local, anchor)) { return card; }
            }

            MinionCardHover front = null;
            foreach (MinionCardHover card in _cards)
            {
                if (!card.isActiveAndEnabled || !card.ContainsAtRest(local, anchor)) { continue; }
                if (front == null || card.RestOrder > front.RestOrder) { front = card; }
            }
            return front;
        }
    }
}
