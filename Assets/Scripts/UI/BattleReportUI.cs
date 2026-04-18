// BattleReportUI.cs — 战斗报告与将军回复面板
// 显示将军回复和战报的滚动文本面板

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SWO1.UI
{
    /// <summary>
    /// 战斗报告UI - 显示将军回复和战报的滚动文本面板
    /// </summary>
    public class BattleReportUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform rootPanel;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentTransform;
        [SerializeField] private Text titleText;

        [Header("Colors")]
        [SerializeField] private Color rommelColor = new Color(0.753f, 0.251f, 0.251f);      // #c04040
        [SerializeField] private Color mansteinColor = new Color(0.251f, 0.376f, 0.753f);    // #4060c0
        [SerializeField] private Color guderianColor = new Color(0.376f, 0.376f, 0.376f);    // #606060
        [SerializeField] private Color systemColor = new Color(0.831f, 0.659f, 0.294f);      // #d4a84b
        [SerializeField] private Color playerCommandColor = new Color(0.290f, 0.404f, 0.255f); // #4a6741
        [SerializeField] private Color defaultTextColor = Color.white;

        [Header("Settings")]
        [SerializeField] private int maxMessages = 100;
        [SerializeField] private float messageSpacing = 5f;
        [SerializeField] private int messageFontSize = 14;

        // 消息历史
        private List<Text> messageTexts = new List<Text>();
        private Font legacyFont;
        private int currentTurn = 1;

        // 单例实例
        public static BattleReportUI Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 获取内置字体
            legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyFont == null)
            {
                // 如果内置字体不可用，使用默认字体
                legacyFont = Font.CreateDynamicFontFromOSFont("Arial", messageFontSize);
            }

            InitializeUI();
        }

        void Start()
        {
            // 订阅游戏事件
            if (Core.GameEventBus.Instance != null)
            {
                Core.GameEventBus.Instance.OnRadioReportDelivered += HandleRadioReport;
                Core.GameEventBus.Instance.OnCommandStatusChanged += HandleCommandStatusChanged;
            }
        }

        void OnDestroy()
        {
            if (Core.GameEventBus.Instance != null)
            {
                Core.GameEventBus.Instance.OnRadioReportDelivered -= HandleRadioReport;
                Core.GameEventBus.Instance.OnCommandStatusChanged -= HandleCommandStatusChanged;
            }
        }

        /// <summary>
        /// 初始化UI布局
        /// </summary>
        private void InitializeUI()
        {
            // 设置根面板
            if (rootPanel == null)
            {
                rootPanel = GetComponent<RectTransform>();
            }

            // 设置标题
            if (titleText != null)
            {
                titleText.text = "📻 无线电";
                titleText.color = systemColor;
                titleText.font = legacyFont;
                titleText.fontSize = 16;
                titleText.fontStyle = FontStyle.Bold;
            }

            // 确保ScrollRect配置正确
            if (scrollRect != null)
            {
                scrollRect.vertical = true;
                scrollRect.horizontal = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            // 确保Content有VerticalLayoutGroup
            if (contentTransform != null)
            {
                VerticalLayoutGroup vlg = contentTransform.GetComponent<VerticalLayoutGroup>();
                if (vlg == null)
                {
                    vlg = contentTransform.gameObject.AddComponent<VerticalLayoutGroup>();
                }
                vlg.spacing = messageSpacing;
                vlg.padding = new RectOffset(10, 10, 10, 10);
                vlg.childAlignment = TextAnchor.UpperLeft;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                // 添加ContentSizeFitter
                ContentSizeFitter csf = contentTransform.GetComponent<ContentSizeFitter>();
                if (csf == null)
                {
                    csf = contentTransform.gameObject.AddComponent<ContentSizeFitter>();
                }
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }

            // 添加欢迎消息
            AddSystemMessage("=== 无线电通信已建立 ===");
            AddSystemMessage("等待作战指令...");
        }

        /// <summary>
        /// 设置当前回合数
        /// </summary>
        public void SetCurrentTurn(int turn)
        {
            currentTurn = turn;
        }

        /// <summary>
        /// 添加将军回复消息
        /// </summary>
        /// <param name="generalName">将军名称</param>
        /// <param name="replyText">回复内容</param>
        /// <param name="nameColor">名称颜色</param>
        public void AddGeneralReply(string generalName, string replyText, Color nameColor)
        {
            string formattedMessage = $"<b>[回合{currentTurn}] {generalName}:</b> {replyText}";
            AddMessage(formattedMessage, nameColor, false);
        }

        /// <summary>
        /// 添加系统消息（战报、遭遇战结果等）
        /// </summary>
        public void AddSystemMessage(string text)
        {
            string formattedMessage = $"<color=#{ColorUtility.ToHtmlStringRGB(systemColor)}>⚠️ {text}</color>";
            AddMessage(formattedMessage, systemColor, true);
        }

        /// <summary>
        /// 添加玩家指令回显
        /// </summary>
        public void AddPlayerCommand(string targetGeneral, string command)
        {
            string formattedMessage = $"<b>[你 → {targetGeneral}]</b> {command}";
            AddMessage(formattedMessage, playerCommandColor, false);
        }

        /// <summary>
        /// 通用添加消息方法
        /// </summary>
        private void AddMessage(string text, Color color, bool isRichTextOnly)
        {
            if (contentTransform == null) return;

            // 创建消息文本对象
            GameObject msgObj = new GameObject($"Message_{messageTexts.Count}", typeof(RectTransform));
            msgObj.transform.SetParent(contentTransform, false);

            Text msgText = msgObj.AddComponent<Text>();
            msgText.font = legacyFont;
            msgText.fontSize = messageFontSize;
            msgText.color = isRichTextOnly ? defaultTextColor : color;
            msgText.text = text;
            msgText.alignment = TextAnchor.UpperLeft;
            msgText.horizontalOverflow = HorizontalWrapMode.Wrap;
            msgText.verticalOverflow = VerticalWrapMode.Overflow;
            msgText.supportRichText = true;

            // 设置布局
            RectTransform msgRect = msgObj.GetComponent<RectTransform>();
            msgRect.sizeDelta = new Vector2(0, 20);
            msgRect.pivot = new Vector2(0.5f, 1f);
            msgRect.anchorMin = new Vector2(0, 0);
            msgRect.anchorMax = new Vector2(1, 0);

            // 添加到列表
            messageTexts.Add(msgText);

            // 限制消息数量
            TrimMessages();

            // 滚动到底部
            ScrollToBottom();
        }

        /// <summary>
        /// 限制消息数量，超过时删除最早的
        /// </summary>
        private void TrimMessages()
        {
            while (messageTexts.Count > maxMessages)
            {
                Text oldestMsg = messageTexts[0];
                messageTexts.RemoveAt(0);
                if (oldestMsg != null && oldestMsg.gameObject != null)
                {
                    Destroy(oldestMsg.gameObject);
                }
            }

            // 重命名消息对象以保持顺序
            for (int i = 0; i < messageTexts.Count; i++)
            {
                if (messageTexts[i] != null && messageTexts[i].gameObject != null)
                {
                    messageTexts[i].gameObject.name = $"Message_{i}";
                }
            }
        }

        /// <summary>
        /// 滚动到底部显示最新消息
        /// </summary>
        private void ScrollToBottom()
        {
            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 清除所有消息
        /// </summary>
        public void ClearAllMessages()
        {
            foreach (var msg in messageTexts)
            {
                if (msg != null && msg.gameObject != null)
                {
                    Destroy(msg.gameObject);
                }
            }
            messageTexts.Clear();
        }

        #region Event Handlers

        private void HandleRadioReport(SWO1.Intelligence.RadioReport report)
        {
            if (report == null) return;

            // 根据报告内容显示不同消息
            string generalName = GetGeneralNameFromReport(report);
            Color generalColor = GetGeneralColor(generalName);

            AddGeneralReply(generalName, report.FormattedText, generalColor);
        }

        private void HandleCommandStatusChanged(SWO1.Command.RadioCommand cmd, SWO1.Command.CommandStatus status)
        {
            if (cmd == null) return;

            string statusText = "";
            switch (status)
            {
                case SWO1.Command.CommandStatus.Delivered:
                    statusText = "指令已送达前线";
                    break;
                case SWO1.Command.CommandStatus.Lost:
                    statusText = "指令丢失（信号干扰）";
                    break;
                default:
                    statusText = "指令状态: " + status.ToString();
                    break;
            }

            if (!string.IsNullOrEmpty(statusText))
            {
                AddSystemMessage($"{GetGeneralNameFromCommand(cmd)}: {statusText}");
            }
        }

        #endregion

        #region Helper Methods

        private string GetGeneralNameFromReport(SWO1.Intelligence.RadioReport report)
        {
            // 从报告中提取将军名称
            if (report.SourceUnitId != null)
            {
                return GetGeneralDisplayName(report.SourceUnitId);
            }
            return "未知";
        }

        private string GetGeneralNameFromCommand(SWO1.Command.RadioCommand cmd)
        {
            if (cmd.TargetUnitId != null)
            {
                return GetGeneralDisplayName(cmd.TargetUnitId);
            }
            return "未知";
        }

        private string GetGeneralDisplayName(string generalId)
        {
            switch (generalId?.ToLower())
            {
                case "rommel":
                case "隆美尔":
                    return "隆美尔";
                case "manstein":
                case "曼施坦因":
                    return "曼施坦因";
                case "guderian":
                case "古德里安":
                    return "古德里安";
                default:
                    return generalId ?? "未知";
            }
        }

        private Color GetGeneralColor(string generalName)
        {
            switch (generalName)
            {
                case "隆美尔":
                    return rommelColor;
                case "曼施坦因":
                    return mansteinColor;
                case "古德里安":
                    return guderianColor;
                default:
                    return defaultTextColor;
            }
        }

        #endregion

        #region Public Color Accessors

        public Color GetRommelColor() => rommelColor;
        public Color GetMansteinColor() => mansteinColor;
        public Color GetGuderianColor() => guderianColor;
        public Color GetSystemColor() => systemColor;
        public Color GetPlayerCommandColor() => playerCommandColor;

        #endregion
    }
}
