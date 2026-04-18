// CameraController2D.cs — 2D 正交相机控制
// WASD/拖拽平移，滚轮缩放，空格回位
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SWO1.CommandPost
{
    [RequireComponent(typeof(Camera))]
    public class CameraController2D : MonoBehaviour
    {
        [Header("移动")]
        public float PanSpeed = 8f;
        public float DragSpeed = 1.5f;

        [Header("缩放（已锁定）")]
        public float ZoomSpeed = 0f;
        public float MinSize = 8f;
        public float MaxSize = 14f;
        public float DefaultSize = 14f;

        [Header("边界（世界坐标）")]
        public Vector2 MinBounds = new Vector2(4f, -2f);
        public Vector2 MaxBounds = new Vector2(40f, 26f);

        private Camera _cam;
        private Vector3 _dragStart;
        private bool _isDragging;
        private Vector3 _defaultPos;
        private float _defaultSize;

        void Start()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = DefaultSize;
            _cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            _defaultPos = transform.position;
            _defaultSize = DefaultSize;
        }

        void Update()
        {
            HandleKeyboard();
            HandleDrag();
            HandleZoom();
            ClampPosition();
        }

        void HandleKeyboard()
        {
            // 输入框有焦点时，不响应 WASD
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null &&
                UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() != null)
                return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                transform.position += new Vector3(h, v, 0) * PanSpeed * _cam.orthographicSize * Time.deltaTime;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                transform.position = _defaultPos;
                _cam.orthographicSize = _defaultSize;
            }
        }

        void HandleDrag()
        {
            if (Input.GetMouseButtonDown(1))
            {
                _dragStart = _cam.ScreenToWorldPoint(Input.mousePosition);
                _isDragging = true;
            }
            if (Input.GetMouseButton(1) && _isDragging)
            {
                Vector3 current = _cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = _dragStart - current;
                transform.position += delta * DragSpeed;
            }
            if (Input.GetMouseButtonUp(1))
            {
                _isDragging = false;
            }
        }

        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float newSize = _cam.orthographicSize - scroll * ZoomSpeed * _cam.orthographicSize;
                _cam.orthographicSize = Mathf.Clamp(newSize, MinSize, MaxSize);
            }
        }

        void ClampPosition()
        {
            float vertExtent = _cam.orthographicSize;
            float horizExtent = vertExtent * _cam.aspect;
            Vector3 pos = transform.position;

            // 如果边界无效（相机太大看不到整个区域），不要强行夹紧
            float minX = MinBounds.x + horizExtent;
            float maxX = MaxBounds.x - horizExtent;
            float minY = MinBounds.y + vertExtent;
            float maxY = MaxBounds.y - vertExtent;

            if (minX < maxX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (minY < maxY) pos.y = Mathf.Clamp(pos.y, minY, maxY);

            transform.position = Vector3.Lerp(transform.position, pos, 0.2f);
        }
    }
}
