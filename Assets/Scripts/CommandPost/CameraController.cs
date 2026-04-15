using SWO1.Core;
// CameraController.cs — 第一人称固定座位摄像机 + 输入控制 (SWO-158)
// 替代旧 RTS 自由飞行摄像机
// 玩家坐在指挥所固定座位上，只能转头/低头/抬头，不能移动位置
// 支持注视点切换（沙盘/无线电/地图/窗户）和平滑过渡
// 集成: ESC 暂停、数字键 1-5 切换无线电频率、鼠标点击交互
using UnityEngine;
using System;

namespace SWO1.CommandPost
{
    /// <summary>
    /// 注视点枚举 — 定义玩家可以看向的区域
    /// 每个注视点对应场景中一个预设的空物体 Transform
    /// </summary>
    public enum FocusPoint
    {
        Free,           // 自由视角（默认）
        SandTable,      // 沙盘地图桌（核心交互区）
        RadioStation,   // 无线电台
        MapWall,        // 墙上作战地图
        Window,         // 舷窗（可见海面/天空）
        Documents       // 文件/情报区
    }

    /// <summary>
    /// 固定座位第一人称摄像机 + 全局输入控制。
    /// 
    /// 设计原则：
    /// - 玩家不能移动，只能转头（模拟坐在椅子上）
    /// - 呼吸晃动在位置上（增加沉浸感，避免眩晕）
    /// - 注视点是场景中预设 Transform 引用
    /// - 通过事件总线通知其他系统视角变化
    /// 
    /// 输入职责 (SWO-158):
    /// - ESC: 暂停/恢复游戏
    /// - 数字键 1-5: 切换无线电频率（自由视角下也可用）
    /// - 鼠标左键: 交互（通过 InputSystem 射线检测）
    /// - 鼠标右键: 退出当前注视
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("视角限制")]
        [Tooltip("左右最大旋转角度（度）")]
        [SerializeField] private float yawLimit = 120f;

        [Tooltip("上方最大俯角（度）")]
        [SerializeField] private float pitchUpLimit = 30f;

        [Tooltip("下方最大俯角（度）")]
        [SerializeField] private float pitchDownLimit = 45f;

        [Tooltip("鼠标灵敏度")]
        [SerializeField] private float mouseSensitivity = 2f;

        [Header("呼吸晃动")]
        [Tooltip("呼吸位移幅度")]
        [SerializeField] private float breathAmplitude = 0.002f;

        [Tooltip("呼吸频率 (Hz)")]
        [SerializeField] private float breathFrequency = 0.4f;

        [Header("注视点目标 (场景中预设的空物体)")]
        [SerializeField] private Transform sandTableLookTarget;
        [SerializeField] private Transform radioStationLookTarget;
        [SerializeField] private Transform mapWallLookTarget;
        [SerializeField] private Transform windowLookTarget;
        [SerializeField] private Transform documentsLookTarget;

        [Header("过渡动画")]
        [Tooltip("注视点切换的插值速度")]
        [SerializeField] private float focusTransitionSpeed = 3f;

        [Tooltip("自由视角恢复速度")]
        [SerializeField] private float returnToFreeSpeed = 4f;

        // === 内部状态 ===
        private float currentYaw = 0f;
        private float currentPitch = 0f;
        private Vector3 basePosition;
        private Quaternion baseRotation;

        // 注视状态
        private bool isTransitioning = false;
        private bool isReturningToFree = false;
        private Quaternion transitionStartRotation;
        private Quaternion transitionTargetRotation;
        private float transitionProgress = 0f;

        // 鼠标锁定
        private bool mouseLocked = true;

        // 暂停状态
        private bool isPaused = false;

        // 当前无线电频率
        private int currentRadioFrequency = 1;

        // 当前注视点
        public FocusPoint CurrentFocus { get; private set; } = FocusPoint.Free;
        public bool IsMouseLocked => mouseLocked;
        public bool IsPaused => isPaused;
        public int CurrentRadioFrequency => currentRadioFrequency;

        // === 事件 ===
        /// <summary>暂停状态变更 (true=暂停, false=恢复)</summary>
        public event Action<bool> OnPauseStateChanged;

        /// <summary>无线电频率变更 (频率值 1-5)</summary>
        public event Action<int> OnRadioFrequencyChanged;

        void Start()
        {
            basePosition = transform.position;
            baseRotation = transform.rotation;
            LockMouseInternal();
        }

        void Update()
        {
            // 全局输入 — 始终响应
            HandleGlobalInput();

            // 暂停时不处理视角
            if (isPaused) return;

            if (isTransitioning)
            {
                UpdateFocusTransition();
                return;
            }

            if (isReturningToFree)
            {
                UpdateReturnToFree();
                return;
            }

            HandleMouseLook();
            ApplyBreathing();
        }

        #region 全局输入 (SWO-158)

        /// <summary>
        /// 全局输入处理 — ESC 暂停、数字键切换频率
        /// 在任何模式下都可响应
        /// </summary>
        private void HandleGlobalInput()
        {
            // ESC → 暂停/恢复
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 如果正在抓取或特殊模式，让 InputSystem 先处理
                if (InputSystem.Instance != null &&
                    InputSystem.Instance.CurrentMode != InteractionMode.Free)
                {
                    // InputSystem 会处理 ESC 退出模式
                    return;
                }

                // 如果有注视点，先退出注视
                if (!isPaused && CurrentFocus != FocusPoint.Free)
                {
                    ReleaseFocus();
                    return;
                }

                // 切换暂停
                TogglePause();
            }

            // 数字键 1-5 → 切换无线电频率（自由视角下也可用）
            if (!isPaused)
            {
                for (int i = 1; i <= 5; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                    {
                        SetRadioFrequency(i);
                    }
                }
            }
        }

        /// <summary>
        /// 切换暂停状态
        /// </summary>
        public void TogglePause()
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void Pause()
        {
            if (isPaused) return;
            isPaused = true;
            Time.timeScale = 0f;
            UnlockMouse();

            // 同步 GameDirector 暂停状态
            if (GameDirector.Instance != null)
                GameDirector.Instance.Pause();

            OnPauseStateChanged?.Invoke(true);
            Debug.Log("[Camera] 游戏暂停");
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void Resume()
        {
            if (!isPaused) return;
            isPaused = false;
            Time.timeScale = 1f;
            LockMouseInternal();

            // 同步 GameDirector 恢复状态
            if (GameDirector.Instance != null)
                GameDirector.Instance.Resume();

            OnPauseStateChanged?.Invoke(false);
            Debug.Log("[Camera] 游戏恢复");
        }

        /// <summary>
        /// 设置无线电频率（全局可用，无需进入电台模式）
        /// </summary>
        public void SetRadioFrequency(int frequency)
        {
            frequency = Mathf.Clamp(frequency, 1, 5);
            if (frequency == currentRadioFrequency) return;

            currentRadioFrequency = frequency;

            string unitName = frequency switch
            {
                1 => "第1步兵连",
                2 => "第2步兵连",
                3 => "坦克排",
                4 => "舰炮支援",
                5 => "师部",
                _ => "未知"
            };

            // 同步 InputSystem 的频率状态
            if (InputSystem.Instance != null)
                InputSystem.Instance.SetRadioFrequency(frequency);

            // 同步 RadioStation（如果有引用）
            var radioStation = FindObjectOfType<RadioStation>();
            if (radioStation != null)
                radioStation.SetFrequency(frequency);

            OnRadioFrequencyChanged?.Invoke(frequency);
            Debug.Log($"[Camera] 无线电频率切换 → {frequency} ({unitName})");
        }

        #endregion

        #region 核心视角控制

        /// <summary>
        /// 处理鼠标自由视角旋转
        /// </summary>
        private void HandleMouseLook()
        {
            if (!mouseLocked) return;

            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            currentYaw += mouseX;
            currentPitch -= mouseY;

            currentYaw = Mathf.Clamp(currentYaw, -yawLimit, yawLimit);
            currentPitch = Mathf.Clamp(currentPitch, -pitchDownLimit, pitchUpLimit);

            transform.rotation = baseRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
        }

        /// <summary>
        /// 呼吸晃动 — 在 Y 轴上微小位移，模拟人体呼吸
        /// </summary>
        private void ApplyBreathing()
        {
            float breathOffset = Mathf.Sin(Time.time * breathFrequency * Mathf.PI * 2f) * breathAmplitude;
            transform.position = basePosition + transform.up * breathOffset;
        }

        #endregion

        #region 注视点切换

        /// <summary>
        /// 平滑切换到指定注视点
        /// 输入系统和 UI 系统都可以调用此方法
        /// </summary>
        public void FocusOn(FocusPoint point)
        {
            if (point == FocusPoint.Free)
            {
                ReleaseFocus();
                return;
            }

            Transform target = GetFocusTarget(point);
            if (target == null)
            {
                Debug.LogWarning($"[Camera] 注视点 {point} 没有对应的目标 Transform");
                return;
            }

            // 保存当前旋转作为过渡起点
            transitionStartRotation = transform.rotation;
            transitionTargetRotation = Quaternion.LookRotation(target.position - transform.position);
            transitionProgress = 0f;
            isTransitioning = true;
            isReturningToFree = false;
            CurrentFocus = point;

            // 通知事件总线
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishCameraFocusChanged(point);
            }

            // 解锁鼠标（注视时通常需要操作UI）
            UnlockMouse();

            Debug.Log($"[Camera] 切换注视点: {point}");
        }

        /// <summary>
        /// 取消聚焦，回到自由视角
        /// </summary>
        public void ReleaseFocus()
        {
            if (CurrentFocus == FocusPoint.Free) return;

            // 保存当前旋转
            transitionStartRotation = transform.rotation;
            // 目标旋转是基础旋转 + 当前偏移（回到自由视角状态）
            transitionTargetRotation = baseRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
            transitionProgress = 0f;
            isTransitioning = false;
            isReturningToFree = true;

            CurrentFocus = FocusPoint.Free;
            LockMouseInternal();

            // 通知事件总线
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishCameraFocusChanged(FocusPoint.Free);
            }

            Debug.Log("[Camera] 回到自由视角");
        }

        /// <summary>
        /// 获取注视点对应的目标 Transform
        /// </summary>
        private Transform GetFocusTarget(FocusPoint point)
        {
            return point switch
            {
                FocusPoint.SandTable => sandTableLookTarget,
                FocusPoint.RadioStation => radioStationLookTarget,
                FocusPoint.MapWall => mapWallLookTarget,
                FocusPoint.Window => windowLookTarget,
                FocusPoint.Documents => documentsLookTarget,
                _ => null
            };
        }

        /// <summary>
        /// 更新注视过渡动画
        /// </summary>
        private void UpdateFocusTransition()
        {
            transitionProgress += Time.deltaTime * focusTransitionSpeed;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transitionProgress));

            transform.rotation = Quaternion.Slerp(transitionStartRotation, transitionTargetRotation, t);

            if (transitionProgress >= 1f)
            {
                isTransitioning = false;
                transform.rotation = transitionTargetRotation;
            }
        }

        /// <summary>
        /// 更新返回自由视角的过渡动画
        /// </summary>
        private void UpdateReturnToFree()
        {
            transitionProgress += Time.deltaTime * returnToFreeSpeed;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(transitionProgress));

            transform.rotation = Quaternion.Slerp(transitionStartRotation, transitionTargetRotation, t);

            if (transitionProgress >= 1f)
            {
                isReturningToFree = false;
                transform.rotation = transitionTargetRotation;
            }
        }

        #endregion

        #region 鼠标控制

        /// <summary>
        /// 解锁鼠标（用于 UI 交互时）
        /// </summary>
        public void UnlockMouse()
        {
            mouseLocked = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// 重新锁定鼠标
        /// </summary>
        public void LockMouse()
        {
            LockMouseInternal();
            // 如果正在注视，回到自由视角
            if (CurrentFocus != FocusPoint.Free)
            {
                ReleaseFocus();
            }
        }

        private void LockMouseInternal()
        {
            mouseLocked = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        #endregion

        #region 运行时配置

        /// <summary>
        /// 运行时调节鼠标灵敏度
        /// </summary>
        public void SetSensitivity(float sensitivity)
        {
            mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 10f);
        }

        /// <summary>
        /// 设置注视点目标 Transform（运行时动态设置）
        /// </summary>
        public void SetFocusTarget(FocusPoint point, Transform target)
        {
            switch (point)
            {
                case FocusPoint.SandTable: sandTableLookTarget = target; break;
                case FocusPoint.RadioStation: radioStationLookTarget = target; break;
                case FocusPoint.MapWall: mapWallLookTarget = target; break;
                case FocusPoint.Window: windowLookTarget = target; break;
                case FocusPoint.Documents: documentsLookTarget = target; break;
            }
        }

        #endregion
    }
}
