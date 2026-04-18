// CellHoverInfo.cs — 鼠标悬停在地图格子上时，显示坐标标注
// 挂在 Main Camera 或任意场景对象上即可
using UnityEngine;

namespace SWO1.UI
{
    public class CellHoverInfo : MonoBehaviour
    {
        private TextMesh _label;
        private Camera _cam;
        private Vector2Int _lastCell = new Vector2Int(-1, -1);

        void Start()
        {
            _cam = Camera.main;

            // 创建悬浮文字
            var go = new GameObject("HoverCoord");
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMesh>();
            _label.fontSize = 36;
            _label.characterSize = 0.12f;
            _label.anchor = TextAnchor.MiddleCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = new Color(1f, 1f, 0.6f, 0.95f);
            _label.fontStyle = FontStyle.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 100;
            go.SetActive(false);
        }

        void Update()
        {
            if (_cam == null) return;

            Vector3 mouseScreen = Input.mousePosition;
            Vector3 mouseWorld = _cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -_cam.transform.position.z));

            // 减去沙盘偏移量（SandTable2D transform.position）
            SandTable2D sandTable = SandTable2D.Instance;
            float offsetX = sandTable != null ? sandTable.transform.position.x : 0f;
            float offsetY = sandTable != null ? sandTable.transform.position.y : 0f;
            int gx = Mathf.FloorToInt((mouseWorld.x - offsetX) / SandTable2D.CellSize);
            int gy = Mathf.FloorToInt((mouseWorld.y - offsetY) / SandTable2D.CellSize);

            // 判断鼠标是否在地图范围内
            bool inBounds = gx >= 0 && gx < SandTable2D.GridWidth && gy >= 0 && gy < SandTable2D.GridHeight;

            if (!inBounds)
            {
                _label.gameObject.SetActive(false);
                _lastCell = new Vector2Int(-1, -1);
                return;
            }

            Vector2Int cell = new Vector2Int(gx, gy);
            if (cell != _lastCell)
            {
                _lastCell = cell;
                char col = (char)('A' + gx);
                _label.text = $"{col}{gy + 1}";
                _label.gameObject.SetActive(true);
            }

            // 跟随鼠标，偏移到格子右上方
            SandTable2D sandTable2 = SandTable2D.Instance;
            float offX = sandTable2 != null ? sandTable2.transform.position.x : 0f;
            float offY = sandTable2 != null ? sandTable2.transform.position.y : 0f;
            float cx = (gx + 0.5f) * SandTable2D.CellSize + offX;
            float cy = (gy + 0.5f) * SandTable2D.CellSize + offY;
            _label.transform.position = new Vector3(cx + 0.4f, cy + 0.4f, 0f);
        }
    }
}
