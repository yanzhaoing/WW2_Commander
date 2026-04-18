// InputManager2D.cs — 2D 鼠标输入管理
// 点击选中单位，拖拽移动，右键取消
using UnityEngine;
using SWO1.Core;

namespace SWO1.CommandPost
{
    public class InputManager2D : MonoBehaviour
    {
        [Header("检测")]
        public LayerMask UnitLayerMask = -1;
        public float ClickRadius = 0.3f;

        private ChessPiece2D _selected;
        private Camera _cam;
        private GameEventBus _bus;

        void Start()
        {
            _cam = Camera.main;
            _bus = GameEventBus.Instance;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
                HandleLeftClick();

            if (Input.GetMouseButtonDown(1))
                HandleRightClick();
        }

        void HandleLeftClick()
        {
            Vector2 worldPos = _cam.ScreenToWorldPoint(Input.mousePosition);

            // 检测是否点击了单位
            Collider2D hit = Physics2D.OverlapCircle(worldPos, ClickRadius, UnitLayerMask);
            if (hit != null)
            {
                var piece = hit.GetComponent<ChessPiece2D>();
                if (piece != null)
                {
                    SelectPiece(piece);
                    return;
                }
            }

            // 点击空白处取消选中
            // （不自动取消，让拖拽逻辑处理）
        }

        void HandleRightClick()
        {
            if (_selected != null)
            {
                _selected.Deselect();
                _selected = null;
            }
        }

        void SelectPiece(ChessPiece2D piece)
        {
            if (_selected != null && _selected != piece)
                _selected.Deselect();

            _selected = piece;
            piece.IsSelected = true;
            piece.UpdateColor();

            if (_bus != null)
                _bus.RaiseUnitSelected(piece.UnitId);

            Debug.Log($"[Input2D] 选中单位: {piece.UnitName} ({piece.UnitId})");
        }

        /// <summary>外部调用：获取当前选中单位</summary>
        public ChessPiece2D GetSelectedPiece() => _selected;

        /// <summary>外部调用：取消选中</summary>
        public void ClearSelection()
        {
            if (_selected != null)
            {
                _selected.Deselect();
                _selected = null;
            }
        }
    }
}
