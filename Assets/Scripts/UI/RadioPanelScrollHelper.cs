// RadioPanelScrollHelper.cs — 鼠标在汇报框上时，滚轮滚动文本而不是缩放
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SWO1.UI
{
    [RequireComponent(typeof(ScrollRect))]
    public class RadioPanelScrollHelper : MonoBehaviour, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("滚动速度")]
        public float ScrollSpeed = 50f;

        private ScrollRect _scrollRect;
        private bool _isPointerOver;

        void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerOver = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerOver = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (_scrollRect == null || _scrollRect.content == null) return;

            // 滚动内容
            float delta = eventData.scrollDelta.y * ScrollSpeed;
            Vector2 pos = _scrollRect.content.anchoredPosition;
            pos.y -= delta;
            _scrollRect.content.anchoredPosition = pos;

            // 标记事件已处理，防止传递给相机
            eventData.Use();
        }

        void Update()
        {
            // 鼠标在汇报框上时，直接读取滚轮输入（备用方案）
            if (_isPointerOver && _scrollRect != null && _scrollRect.content != null)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Vector2 pos = _scrollRect.content.anchoredPosition;
                    pos.y -= scroll * ScrollSpeed;
                    _scrollRect.content.anchoredPosition = pos;
                }
            }
        }
    }
}
