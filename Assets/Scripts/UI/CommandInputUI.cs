// CommandInputUI.cs — 指令输入与回合控制面板
// 玩家输入文字指令 + 选择目标将军 + 下达/下一回合按钮

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SWO1.UI
{
    /// <summary>
    /// 指令输入UI - 处理玩家指令输入和回合控制
    /// </summary>
    public class CommandInputUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform rootPanel;
        [SerializeField] private Dropdown generalDropdown;
        [SerializeField] private InputField commandInput;
        [SerializeField] private Button sendCommandButton;
        [SerializeField] private Button nextTurnButton;
        [SerializeField] private Text statusText;

        [Header("Dropdown Options")]
        [SerializeField] private List<string> generalOptions = new List<string>
        {
            "隆美尔",
            "曼施坦因",
            "古德里安",
            "全体"
        };

        [Header("Colors")]
        [SerializeField] private Color activeButtonColor = new Color(0.2f, 0.4f, 0.2f);
        [SerializeField] private Color inactiveButtonColor = new Color(0.4f, 0.4f, 0.4f);
        [SerializeField] private Color disabledButtonColor = new Color(0.3f, 0.3f, 0.3f);

        // 引用
        private Core.TurnManager turnManager;
        private BattleReportUI battleReportUI;
        private Font legacyFont;

        // 状态
        private bool isAIThinking = false;
        private bool isInitialized = false;

        // 将军ID映射
        private Dictionary<string, string> generalIdMap = new Dictionary<string, string>
        {
            { "隆美尔", "rommel" },
            { "曼施坦因", "manstein" },
            { "古德里安", "guderian" },
            { "全体", "all" }
        };

        void Awake()
        {
            // 获取内置字体
            legacyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (legacyFont == null)
            {
                legacyFont = Font.CreateDynamicFontFromOSFont("Arial", 14);
            }
        }

        void Start()
        {
            InitializeUI();
            FindReferences();
            SubscribeEvents();
            isInitialized = true;
            UpdateUIState();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        /// <summary>
        /// 初始化UI组件
        /// </summary>
        private void InitializeUI()
        {
            // 设置根面板
            if (rootPanel == null)
            {
                rootPanel = GetComponent<RectTransform>();
            }

            // 初始化Dropdown
            if (generalDropdown != null)
            {
                generalDropdown.ClearOptions();
                generalDropdown.AddOptions(generalOptions);
                generalDropdown.value = 0;
                generalDropdown.captionText.font = legacyFont;
                generalDropdown.captionText.fontSize = 14;

                // 设置Dropdown项字体
                Transform template = generalDropdown.transform.Find("Template");
                if (template != null)
                {
                    Transform item = template.Find("Viewport/Content/Item");
                    if (item != null)
                    {
                        Text itemText = item.GetComponentInChildren<Text>();
                        if (itemText != null)
                        {
                            itemText.font = legacyFont;
                            itemText.fontSize = 14;
                        }
                    }
                }
            }

            // 初始化InputField
            if (commandInput != null)
            {
                commandInput.text = "";
                commandInput.placeholder.GetComponent<Text>().text = "输入指令...";
                commandInput.placeholder.GetComponent<Text>().font = legacyFont;
                commandInput.textComponent.font = legacyFont;
                commandInput.textComponent.fontSize = 14;

                // 添加回车提交事件
                commandInput.onEndEdit.AddListener(OnInputEndEdit);
            }

            // 初始化按钮
            if (sendCommandButton != null)
            {
                Text btnText = sendCommandButton.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = "下达指令";
                    btnText.font = legacyFont;
                    btnText.fontSize = 14;
                }
                sendCommandButton.onClick.AddListener(OnSendCommand);
            }

            if (nextTurnButton != null)
            {
                Text btnText = nextTurnButton.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = "下一回合 ▶";
                    btnText.font = legacyFont;
                    btnText.fontSize = 14;
                }
                nextTurnButton.onClick.AddListener(OnNextTurn);
            }

            // 初始化状态文本
            if (statusText != null)
            {
                statusText.text = "等待指令...";
                statusText.font = legacyFont;
                statusText.fontSize = 12;
                statusText.color = Color.gray;
            }
        }

        /// <summary>
        /// 查找必要的引用
        /// </summary>
        private void FindReferences()
        {
            // 查找TurnManager
            turnManager = FindFirstObjectByType<Core.TurnManager>();
            if (turnManager == null)
            {
                Debug.LogWarning("[CommandInputUI] TurnManager not found in scene");
            }

            // 查找BattleReportUI
            battleReportUI = BattleReportUI.Instance;
            if (battleReportUI == null)
            {
                battleReportUI = FindFirstObjectByType<BattleReportUI>();
            }
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeEvents()
        {
            if (turnManager != null)
            {
                turnManager.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        /// <summary>
        /// 取消订阅事件
        /// </summary>
        private void UnsubscribeEvents()
        {
            if (turnManager != null)
            {
                turnManager.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (commandInput != null)
            {
                commandInput.onEndEdit.RemoveListener(OnInputEndEdit);
            }

            if (sendCommandButton != null)
            {
                sendCommandButton.onClick.RemoveListener(OnSendCommand);
            }

            if (nextTurnButton != null)
            {
                nextTurnButton.onClick.RemoveListener(OnNextTurn);
            }
        }

        /// <summary>
        /// 输入框回车提交
        /// </summary>
        private void OnInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                OnSendCommand();
            }
        }

        /// <summary>
        /// "下达指令" 按钮回调
        /// </summary>
        private void OnSendCommand()
        {
            if (isAIThinking) return;

            // 获取选中的将军
            string selectedGeneral = GetSelectedGeneral();
            if (string.IsNullOrEmpty(selectedGeneral))
            {
                Debug.LogWarning("[CommandInputUI] 未选择将军");
                return;
            }

            // 获取输入的指令
            string command = commandInput != null ? commandInput.text : "";
            if (string.IsNullOrWhiteSpace(command))
            {
                Debug.LogWarning("[CommandInputUI] 指令为空");
                return;
            }

            // 发送指令
            if (selectedGeneral == "全体")
            {
                // 给三个将军都发送同样的指令
                SendCommandToGeneral("隆美尔", command);
                SendCommandToGeneral("曼施坦因", command);
                SendCommandToGeneral("古德里安", command);
            }
            else
            {
                SendCommandToGeneral(selectedGeneral, command);
            }

            // 在战报面板显示玩家指令
            if (battleReportUI != null)
            {
                battleReportUI.AddPlayerCommand(selectedGeneral, command);
            }

            // 清空输入框
            if (commandInput != null)
            {
                commandInput.text = "";
                commandInput.ActivateInputField();
            }
        }

        /// <summary>
        /// 发送指令给指定将军
        /// </summary>
        private void SendCommandToGeneral(string generalName, string command)
        {
            if (turnManager != null)
            {
                string generalId = GetGeneralId(generalName);
                turnManager.SubmitCommand(generalId, command);
                Debug.Log($"[CommandInputUI] 指令已发送给 {generalName}: {command}");
            }
            else
            {
                Debug.LogWarning("[CommandInputUI] TurnManager is null, cannot send command");
            }
        }

        /// <summary>
        /// "下一回合" 按钮回调
        /// </summary>
        private void OnNextTurn()
        {
            if (isAIThinking) return;

            if (turnManager != null)
            {
                turnManager.NextTurn();
                Debug.Log("[CommandInputUI] 下一回合");

                // 禁用按钮直到本回合结束
                SetButtonsInteractable(false);
                UpdateStatusText("回合进行中...");
            }
            else
            {
                Debug.LogWarning("[CommandInputUI] TurnManager is null, cannot advance turn");
            }
        }

        /// <summary>
        /// 处理回合阶段变化
        /// </summary>
        private void HandlePhaseChanged(Core.TurnPhase phase)
        {
            switch (phase)
            {
                case Core.TurnPhase.PlayerCommand:
                    isAIThinking = false;
                    SetButtonsInteractable(true);
                    UpdateStatusText("等待指令...");
                    break;

                case Core.TurnPhase.AIThinking:
                    isAIThinking = true;
                    SetButtonsInteractable(false);
                    UpdateStatusText("将军正在思考...");
                    break;

                case Core.TurnPhase.AIActions:
                    isAIThinking = true;
                    SetButtonsInteractable(false);
                    UpdateStatusText("将军行动中...");
                    break;

                case Core.TurnPhase.EnemyTurn:
                    isAIThinking = true;
                    SetButtonsInteractable(false);
                    UpdateStatusText("敌军回合...");
                    break;

                case Core.TurnPhase.Settlement:
                    isAIThinking = true;
                    SetButtonsInteractable(false);
                    UpdateStatusText("回合结算中...");
                    break;

                case Core.TurnPhase.BattleReport:
                    // 结算阶段结束后，准备下一回合
                    isAIThinking = false;
                    SetButtonsInteractable(true);
                    UpdateStatusText("等待指令...");
                    break;
            }
        }

        /// <summary>
        /// 获取选中的将军名称
        /// </summary>
        private string GetSelectedGeneral()
        {
            if (generalDropdown != null && generalDropdown.options.Count > 0)
            {
                int index = generalDropdown.value;
                if (index >= 0 && index < generalDropdown.options.Count)
                {
                    return generalDropdown.options[index].text;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取将军ID
        /// </summary>
        private string GetGeneralId(string displayName)
        {
            if (generalIdMap.TryGetValue(displayName, out string id))
            {
                return id;
            }
            return displayName.ToLower();
        }

        /// <summary>
        /// 设置按钮交互状态
        /// </summary>
        private void SetButtonsInteractable(bool interactable)
        {
            if (sendCommandButton != null)
            {
                sendCommandButton.interactable = interactable;
                UpdateButtonColor(sendCommandButton, interactable ? activeButtonColor : disabledButtonColor);
            }

            if (nextTurnButton != null)
            {
                nextTurnButton.interactable = interactable;
                UpdateButtonColor(nextTurnButton, interactable ? activeButtonColor : disabledButtonColor);
            }

            if (generalDropdown != null)
            {
                generalDropdown.interactable = interactable;
            }

            if (commandInput != null)
            {
                commandInput.interactable = interactable;
            }
        }

        /// <summary>
        /// 更新按钮颜色
        /// </summary>
        private void UpdateButtonColor(Button button, Color color)
        {
            ColorBlock cb = button.colors;
            cb.normalColor = color;
            cb.highlightedColor = color * 1.1f;
            cb.pressedColor = color * 0.9f;
            cb.disabledColor = disabledButtonColor;
            button.colors = cb;
        }

        /// <summary>
        /// 更新状态文本
        /// </summary>
        private void UpdateStatusText(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
                statusText.color = isAIThinking ? new Color(0.8f, 0.6f, 0.2f) : Color.gray;
            }
        }

        /// <summary>
        /// 更新UI状态
        /// </summary>
        private void UpdateUIState()
        {
            if (turnManager != null)
            {
                HandlePhaseChanged(turnManager.CurrentPhase);
            }
            else
            {
                SetButtonsInteractable(true);
            }
        }

        /// <summary>
        /// 设置TurnManager引用（供外部调用）
        /// </summary>
        public void SetTurnManager(Core.TurnManager manager)
        {
            if (turnManager != null)
            {
                turnManager.OnPhaseChanged -= HandlePhaseChanged;
            }

            turnManager = manager;

            if (turnManager != null)
            {
                turnManager.OnPhaseChanged += HandlePhaseChanged;
                if (isInitialized)
                {
                    UpdateUIState();
                }
            }
        }

        /// <summary>
        /// 设置BattleReportUI引用（供外部调用）
        /// </summary>
        public void SetBattleReportUI(BattleReportUI ui)
        {
            battleReportUI = ui;
        }

        /// <summary>
        /// 获取当前输入的指令文本
        /// </summary>
        public string GetCurrentCommandText()
        {
            return commandInput != null ? commandInput.text : "";
        }

        /// <summary>
        /// 设置输入框文本
        /// </summary>
        public void SetCommandText(string text)
        {
            if (commandInput != null)
            {
                commandInput.text = text;
            }
        }

        /// <summary>
        /// 检查当前是否AI思考中
        /// </summary>
        public bool IsAIThinking()
        {
            return isAIThinking;
        }
    }
}
