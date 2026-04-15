using SWO1.Core;
// InputSystem.cs — 物理交互输入系统 (重构版)
// 替代旧 RTS 鼠标框选/右键移动输入模式
// 处理沙盘棋子抓取、无线电台操作、文件翻阅等物理交互
// 所有交互通过事件总线通知其他系统
using UnityEngine;
using System;

namespace SWO1.CommandPost
{
    #region 枚举与事件定义

    /// <summary>
    /// 交互模式状态机
    /// 每种模式有独立的 Update 逻辑
    /// </summary>
    public enum InteractionMode
    {
        Free,           // 自由视角，鼠标解锁可点击物件
        Grabbing,       // 正在抓取沙盘棋子
        RadioFocused,   // 聚焦无线电台操作
        DocumentReading // 阅读文件/情报
    }

    /// <summary>
    /// 交互事件类型
    /// </summary>
    public enum InteractionType
    {
        Grab,               // 抓取棋子
        Release,            // 放下棋子
        RotatePiece,        // 旋转棋子朝向
        Inspect,            // 查看物件
        RadioTransmit,      // 按下通话键
        RadioFrequencyChange, // 切换无线电频率
        DocumentRead,       // 阅读文件
        NoteWrite,          // 书写便签
        SwitchToggle        // 开关/旋钮操作
    }

    /// <summary>
    /// 交互事件数据
    /// </summary>
    [Serializable]
    public class InteractionEvent
    {
        public InteractableObject target;
        public InteractionType type;
        public float timestamp;
        public int intValue;       // 频率值等
        public string stringValue; // 描述文本
    }

    #endregion

    /// <summary>
    /// 物理交互输入系统。
    /// 
    /// 核心交互：
    /// - 左键点击抓取沙盘棋子 → 拖动 → 放下
    /// - 右键旋转棋子朝向
    /// - 点击无线电台 → 进入电台模式（切换频率/通话）
    /// - 点击文件架 → 进入阅读模式
    /// - ESC/右键退出当前模式
    /// 
    /// 设计原则：
    /// - 模式状态机，每种模式独立处理
    /// - 通过事件总线通知，不直接调用其他系统
    /// - 射线检测使用 LayerMask 隔离可交互层
    /// </summary>
    public class InputSystem : MonoBehaviour
    {
        public static InputSystem Instance { get; private set; }

        [Header("射线检测")]
        [Tooltip("最大交互距离")]
        [SerializeField] private float interactDistance = 3f;

        [Tooltip("可交互物件所在的 Layer")]
        [SerializeField] private LayerMask interactableLayer;

        [Tooltip("玩家摄像机引用")]
        [SerializeField] private Camera playerCamera;

        [Header("棋子抓取")]
        [Tooltip("棋子抓起后的离桌高度")]
        [SerializeField] private float grabHeight = 0.1f;

        [Tooltip("棋子跟随鼠标平滑度")]
        [SerializeField] private float grabSmooth = 15f;

        [Header("依赖引用")]
        [Tooltip("摄像机控制器（自动查找）")]
        [SerializeField] private CameraController cameraController;

        // === 状态 ===
        public InteractionMode CurrentMode { get; private set; } = InteractionMode.Free;

        private InteractableObject hoveredObject;
        private InteractableObject grabbedObject;
        private Vector3 grabOffset;
        private Vector3 grabOriginalPosition; // 抓取前的位置（用于取消时恢复）

        // 当前电台频率
        public int CurrentRadioFrequency { get; private set; } = 1;

        // 事件
        public event Action<InteractionEvent> OnInteraction;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (cameraController == null)
                cameraController = FindObjectOfType<CameraController>();
            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        void Update()
        {
            switch (CurrentMode)
            {
                case InteractionMode.Free:
                    UpdateFreeMode();
                    break;
                case InteractionMode.Grabbing:
                    UpdateGrabbing();
                    break;
                case InteractionMode.RadioFocused:
                    UpdateRadioMode();
                    break;
                case InteractionMode.DocumentReading:
                    UpdateDocumentMode();
                    break;
            }
        }

        #region Free Mode — 自由视角

        /// <summary>
        /// 自由模式：检测悬停物件，左键抓取，ESC 取消注视
        /// </summary>
        private void UpdateFreeMode()
        {
            UpdateHover();

            // 左键点击 → 抓取或进入模式
            if (Input.GetMouseButtonDown(0) && hoveredObject != null)
            {
                BeginInteraction(hoveredObject);
            }

            // 右键 → 如果在注视模式则退出
            if (Input.GetMouseButtonDown(1))
            {
                if (cameraController != null && cameraController.CurrentFocus != FocusPoint.Free)
                {
                    cameraController.ReleaseFocus();
                }
            }

            // ESC → 退出注视
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (cameraController != null)
                    cameraController.ReleaseFocus();
            }
        }

        /// <summary>
        /// 射线检测悬停物件并高亮
        /// </summary>
        private void UpdateHover()
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
            {
                var interactable = hit.collider.GetComponent<InteractableObject>();
                if (interactable != null && interactable.IsInteractable)
                {
                    if (hoveredObject != interactable)
                    {
                        if (hoveredObject != null) hoveredObject.OnHoverExit();
                        hoveredObject = interactable;
                        hoveredObject.OnHoverEnter();
                    }
                    return;
                }
            }

            // 没有命中可交互物件
            if (hoveredObject != null)
            {
                hoveredObject.OnHoverExit();
                hoveredObject = null;
            }
        }

        #endregion

        #region Grabbing Mode — 棋子抓取

        /// <summary>
        /// 根据物件类型开始不同的交互
        /// </summary>
        private void BeginInteraction(InteractableObject target)
        {
            switch (target.Type)
            {
                case InteractableType.ChessPiece:
                    BeginGrab(target);
                    break;

                case InteractableType.RadioStation:
                    EnterRadioMode(target);
                    break;

                case InteractableType.Document:
                    EnterDocumentMode(target);
                    break;

                case InteractableType.Switch:
                    ToggleSwitch(target);
                    break;

                case InteractableType.Notepad:
                    OpenNotepad(target);
                    break;

                default:
                    // 其他物件触发 Inspect 事件
                    FireInteractionEvent(target, InteractionType.Inspect);
                    break;
            }
        }

        /// <summary>
        /// 开始抓取棋子
        /// </summary>
        private void BeginGrab(InteractableObject target)
        {
            grabbedObject = target;
            grabOriginalPosition = target.transform.position; // 保存原始位置
            CurrentMode = InteractionMode.Grabbing;
            grabbedObject.OnGrab();

            // 计算抓取偏移
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
            {
                grabOffset = grabbedObject.transform.position - hit.point;
            }

            FireInteractionEvent(target, InteractionType.Grab);
        }

        /// <summary>
        /// 更新抓取状态 — 棋子跟随鼠标在沙盘表面移动
        /// </summary>
        private void UpdateGrabbing()
        {
            if (grabbedObject == null)
            {
                SetMode(InteractionMode.Free);
                return;
            }

            // 射线投射到沙盘表面，棋子跟随移动
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
            {
                Vector3 targetPos = hit.point + Vector3.up * grabHeight;
                grabbedObject.transform.position = Vector3.Lerp(
                    grabbedObject.transform.position,
                    targetPos,
                    Time.deltaTime * grabSmooth
                );
            }

            // 右键旋转棋子朝向
            if (Input.GetMouseButtonDown(1))
            {
                RotateGrabbedPiece();
            }

            // 左键释放 → 放下棋子
            if (Input.GetMouseButtonUp(0))
            {
                EndGrab();
            }

            // ESC → 取消抓取，恢复原位
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelGrab();
            }
        }

        /// <summary>
        /// 旋转被抓取的棋子（90度增量）
        /// </summary>
        private void RotateGrabbedPiece()
        {
            if (grabbedObject == null) return;
            grabbedObject.transform.Rotate(0f, 90f, 0f);
            FireInteractionEvent(grabbedObject, InteractionType.RotatePiece);
        }

        /// <summary>
        /// 放下棋子
        /// </summary>
        private void EndGrab()
        {
            if (grabbedObject != null)
            {
                grabbedObject.OnRelease();
                FireInteractionEvent(grabbedObject, InteractionType.Release);
            }

            grabbedObject = null;
            SetMode(InteractionMode.Free);
        }

        /// <summary>
        /// 取消抓取（棋子回到原始位置）
        /// </summary>
        private void CancelGrab()
        {
            if (grabbedObject != null)
            {
                // 恢复棋子到抓取前的位置
                grabbedObject.transform.position = grabOriginalPosition;
                grabbedObject.OnRelease();
            }
            grabbedObject = null;
            SetMode(InteractionMode.Free);
        }

        #endregion

        #region Radio Mode — 无线电台操作

        /// <summary>
        /// 进入电台操作模式
        /// </summary>
        private void EnterRadioMode(InteractableObject radioStation)
        {
            CurrentMode = InteractionMode.RadioFocused;

            // 聚焦摄像机到电台
            if (cameraController != null)
                cameraController.FocusOn(FocusPoint.RadioStation);

            FireInteractionEvent(radioStation, InteractionType.Inspect);
        }

        /// <summary>
        /// 电台模式更新
        /// - 数字键 1-5 切换频率
        /// - 鼠标左键按下通话键 → 发送指令
        /// - ESC/右键退出
        /// </summary>
        private void UpdateRadioMode()
        {
            // ESC 或右键退出电台模式
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                ExitRadioMode();
                return;
            }

            // 数字键切换频率
            for (int i = 1; i <= 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    ChangeRadioFrequency(i);
                }
            }

            // 鼠标左键 → 通话键（发送指令）
            if (Input.GetMouseButtonDown(0))
            {
                // 射线检测是否点击了电台上的通话键
                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayer))
                {
                    var target = hit.collider.GetComponent<InteractableObject>();
                    if (target != null && target.Type == InteractableType.Switch)
                    {
                        // 按下通话键
                        FireInteractionEvent(target, InteractionType.RadioTransmit, CurrentRadioFrequency);
                    }
                }
            }
        }

        /// <summary>
        /// 切换无线电频率
        /// </summary>
        private void ChangeRadioFrequency(int frequency)
        {
            CurrentRadioFrequency = Mathf.Clamp(frequency, 1, 5);

            string unitName = frequency switch
            {
                1 => "第1步兵连",
                2 => "第2步兵连",
                3 => "坦克排",
                4 => "舰炮支援",
                5 => "师部",
                _ => "未知"
            };

            Debug.Log($"[Radio] 切换到频率 {frequency} ({unitName})");
            FireInteractionEvent(null, InteractionType.RadioFrequencyChange, frequency);
        }

        /// <summary>
        /// 退出电台模式
        /// </summary>
        private void ExitRadioMode()
        {
            if (cameraController != null)
                cameraController.ReleaseFocus();

            SetMode(InteractionMode.Free);
        }

        #endregion

        #region Document Mode — 文件阅读

        /// <summary>
        /// 进入文件阅读模式
        /// </summary>
        private void EnterDocumentMode(InteractableObject document)
        {
            CurrentMode = InteractionMode.DocumentReading;
            FireInteractionEvent(document, InteractionType.DocumentRead);
        }

        /// <summary>
        /// 文件模式更新
        /// - ESC/右键退出
        /// </summary>
        private void UpdateDocumentMode()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                SetMode(InteractionMode.Free);
            }
        }

        #endregion

        #region 辅助交互

        /// <summary>
        /// 切换开关/旋钮
        /// </summary>
        private void ToggleSwitch(InteractableObject switchObj)
        {
            FireInteractionEvent(switchObj, InteractionType.SwitchToggle);
            // 具体行为由 UISystem 或其他系统处理
        }

        /// <summary>
        /// 打开便签
        /// </summary>
        private void OpenNotepad(InteractableObject notepad)
        {
            FireInteractionEvent(notepad, InteractionType.NoteWrite);
        }

        #endregion

        #region 模式管理

        /// <summary>
        /// 切换交互模式，并同步鼠标/摄像机状态
        /// </summary>
        private void SetMode(InteractionMode mode)
        {
            CurrentMode = mode;

            if (cameraController == null) return;

            switch (mode)
            {
                case InteractionMode.Free:
                    cameraController.LockMouse();
                    break;
                case InteractionMode.Grabbing:
                case InteractionMode.RadioFocused:
                case InteractionMode.DocumentReading:
                    cameraController.UnlockMouse();
                    break;
            }
        }

        #endregion

        #region 事件发布

        /// <summary>
        /// 发布交互事件
        /// </summary>
        private void FireInteractionEvent(InteractableObject target, InteractionType type, int intValue = 0)
        {
            var evt = new InteractionEvent
            {
                target = target,
                type = type,
                timestamp = Time.time,
                intValue = intValue
            };

            OnInteraction?.Invoke(evt);

            // 发布到全局事件总线
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishInteractionPerformed(evt);
            }
        }

        #endregion

        #region 外部 API

        /// <summary>
        /// 由 CameraController 调用，同步无线电频率状态
        /// </summary>
        public void SetRadioFrequency(int frequency)
        {
            CurrentRadioFrequency = Mathf.Clamp(frequency, 1, 5);
            Debug.Log($"[Input] 无线电频率同步 → {CurrentRadioFrequency}");
        }

        #endregion
    }
}
