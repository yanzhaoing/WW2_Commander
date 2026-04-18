// ChessPiece2D.cs — 2D 沙盘棋子（Sprite版）
// 可拖拽的单位标记，挂在 SpriteRenderer 的 GameObject 上
using UnityEngine;
using SWO1.Core;

namespace SWO1.CommandPost
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class ChessPiece2D : MonoBehaviour
    {
        [Header("棋子信息")]
        public string UnitId;
        public string UnitName;
        public bool IsEnemy;
        public bool IsSelected;

        [Header("视觉")]
        public Color FriendlyColor = new Color(0.2f, 0.4f, 0.9f);
        public Color EnemyColor = new Color(0.9f, 0.2f, 0.2f);
        public Color SelectedColor = new Color(1f, 0.9f, 0.2f);
        public Color LostContactColor = new Color(0.4f, 0.4f, 0.4f);

        private SpriteRenderer _sr;
        private Vector2 _dragOffset;
        private bool _isDragging;
        private Camera _cam;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _cam = Camera.main;
        }

        void Start()
        {
            UpdateColor();
        }

        void Update()
        {
            if (_isDragging && Input.GetMouseButton(0))
            {
                Vector2 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
                transform.position = mouseWorld + _dragOffset;
            }

            if (_isDragging && Input.GetMouseButtonUp(0))
            {
                _isDragging = false;
                // 发送移动事件
                var bus = FindObjectOfType<GameEventBus>();
                if (bus != null)
                    bus.RaiseUnitMoved(UnitId, (Vector2)transform.position);
            }
        }

        void OnMouseDown()
        {
            IsSelected = true;
            _isDragging = true;
            Vector2 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);
            _dragOffset = (Vector2)transform.position - mouseWorld;
            UpdateColor();

            var bus = FindObjectOfType<GameEventBus>();
            if (bus != null)
                bus.RaiseUnitSelected(UnitId);
        }

        public void SetLostContact(bool lost)
        {
            _sr.color = lost ? LostContactColor : (IsEnemy ? EnemyColor : FriendlyColor);
        }

        public void UpdateColor()
        {
            if (IsSelected)
                _sr.color = SelectedColor;
            else if (IsEnemy)
                _sr.color = EnemyColor;
            else
                _sr.color = FriendlyColor;
        }

        public void Deselect()
        {
            IsSelected = false;
            UpdateColor();
        }
    }
}
