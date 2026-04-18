// RadioStation.cs — 无线电台交互对象
// 继承 InteractableObject，管理电台的物理交互和状态
using UnityEngine;
using System;

namespace SWO1.CommandPost
{
    /// <summary>
    /// 无线电台 — 指挥所核心交互物件
    /// 
    /// 交互方式：
    /// - 点击进入电台操作模式
    /// - 数字键 1-5 切换频率
    /// - 点击通话键发送指令
    /// - 收到汇报时面板亮起
    /// 
    /// 继承体系：
    /// InteractableObject → RadioStation
    /// </summary>
    public class RadioStation : InteractableObject
    {
        [Header("电台配置")]
        [Tooltip("可用频率名称")]
        [SerializeField] private string[] frequencyNames = {
            "频率1 - 第1连",
            "频率2 - 第2连",
            "频率3 - 坦克排",
            "频率4 - 舰炮支援",
            "频率5 - 师部"
        };

        [Header("视觉反馈")]
        [Tooltip("通话键指示灯")]
        [SerializeField] private Renderer transmitIndicator;

        [Tooltip("接收指示灯")]
        [SerializeField] private Renderer receiveIndicator;

        [Tooltip("频率显示")]
        [SerializeField] private TMPro.TextMeshPro frequencyDisplay;

        [Header("旋钮引用")]
        [Tooltip("频率旋钮 Transform")]
        [SerializeField] private Transform frequencyKnob;

        [SerializeField] private float knobRotationPerStep = 60f;

        // === 状态 ===
        public int CurrentFrequency { get; private set; } = 1;
        public bool IsTransmitting { get; private set; }
        public bool IsReceiving { get; private set; }

        // === 事件 ===
        public event Action<int> OnFrequencyChanged;
        public event Action OnTransmitStarted;
        public event Action OnTransmitEnded;

        protected override void Awake()
        {
            base.Awake();
            Type = InteractableType.RadioStation;
            DisplayName = "无线电台";
        }

        void Start()
        {
            UpdateFrequencyDisplay();
            SetIndicatorState(transmitIndicator, false);
            SetIndicatorState(receiveIndicator, false);
        }

        /// <summary>
        /// 切换到指定频率
        /// </summary>
        public void SetFrequency(int frequency)
        {
            frequency = Mathf.Clamp(frequency, 1, frequencyNames.Length);
            if (frequency == CurrentFrequency) return;

            CurrentFrequency = frequency;
            UpdateFrequencyDisplay();
            UpdateKnobRotation();

            OnFrequencyChanged?.Invoke(frequency);

            // 发布到输入系统
            if (InputSystem.Instance != null)
            {
                // 通过事件总线通知其他系统
            }

            Debug.Log($"[Radio] 频率切换 → {frequencyNames[frequency - 1]}");
        }

        /// <summary>
        /// 开始发送
        /// </summary>
        public void StartTransmit()
        {
            if (IsTransmitting) return;
            IsTransmitting = true;
            SetIndicatorState(transmitIndicator, true);
            OnTransmitStarted?.Invoke();
        }

        /// <summary>
        /// 结束发送
        /// </summary>
        public void EndTransmit()
        {
            if (!IsTransmitting) return;
            IsTransmitting = false;
            SetIndicatorState(transmitIndicator, false);
            OnTransmitEnded?.Invoke();
        }

        /// <summary>
        /// 接收指示（汇报到达时调用）
        /// </summary>
        public void IndicateReceiving()
        {
            if (IsReceiving) return;
            IsReceiving = true;
            SetIndicatorState(receiveIndicator, true);
            StartCoroutine(EndReceiveIndicator());
        }

        private System.Collections.IEnumerator EndReceiveIndicator()
        {
            yield return new WaitForSeconds(2f);
            IsReceiving = false;
            SetIndicatorState(receiveIndicator, false);
        }

        public override void OnGrab()
        {
            base.OnGrab();
            // 电台不可抓取，进入操作模式
        }

        public override void OnRelease()
        {
            base.OnRelease();
        }

        #region 视觉更新

        private void UpdateFrequencyDisplay()
        {
            if (frequencyDisplay != null)
            {
                frequencyDisplay.text = frequencyNames[CurrentFrequency - 1];
            }
        }

        private void UpdateKnobRotation()
        {
            if (frequencyKnob != null)
            {
                float targetAngle = (CurrentFrequency - 1) * knobRotationPerStep;
                frequencyKnob.localRotation = Quaternion.Euler(0f, targetAngle, 0f);
            }
        }

        private void SetIndicatorState(Renderer indicator, bool active)
        {
            if (indicator == null) return;
            indicator.material.color = active ? Color.green : Color.red;
            indicator.material.SetColor("_EmissionColor", active ? Color.green * 2f : Color.black);
        }

        #endregion
    }
}
