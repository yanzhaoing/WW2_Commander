// CommandMenuBinder.cs — 指令面板按钮事件绑定
// 自动查找按钮并绑定点击事件
// 兼容性: Unity 2022.3, UnityEngine.UI (非 TextMeshPro)
using UnityEngine;
using UnityEngine.UI;

namespace SWO1.UI
{
    public class CommandMenuBinder : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────
        //  配置
        // ─────────────────────────────────────────────────────────
        private static readonly string[] UnitIds = { "alpha_1", "alpha_2", "tank_1" };
        private static readonly string[] UnitNames = { "红一排", "红二排", "蓝四坦克排" };
        private static readonly string[] CommandNames = { "推进", "防御", "侦察", "攻击", "撤退", "查询", "待命" };
        private static readonly SWO1.Radio.CommandType[] CommandTypes = {
            SWO1.Radio.CommandType.Move,
            SWO1.Radio.CommandType.Defend,
            SWO1.Radio.CommandType.Scout,
            SWO1.Radio.CommandType.Attack,
            SWO1.Radio.CommandType.Retreat,
            SWO1.Radio.CommandType.Status,
            SWO1.Radio.CommandType.Wait
        };

        // ─────────────────────────────────────────────────────────
        //  UI 引用
        // ─────────────────────────────────────────────────────────
        private Button[] unitButtons = new Button[3];
        private Button[] commandButtons = new Button[7];
        private Button[] freqButtons = new Button[5];
        private Button sendButton;
        private Image[] unitButtonImages = new Image[3];
        private Image[] commandButtonImages = new Image[7];
        private Image[] freqButtonImages = new Image[5];
        private Image sendButtonImage;

        // ─────────────────────────────────────────────────────────
        //  状态
        // ─────────────────────────────────────────────────────────
        private int selectedUnitIndex = -1;
        private int selectedCommandIndex = -1;
        private int selectedFreqIndex = 1; // 默认频率 1 (②)

        // ─────────────────────────────────────────────────────────
        //  缓存颜色
        // ─────────────────────────────────────────────────────────
        private Color[] unitButtonOriginalColors;
        private Color[] commandButtonOriginalColors;
        private Color[] freqButtonOriginalColors;
        private Color sendButtonOriginalColor;

        // ─────────────────────────────────────────────────────────
        //  系统引用
        // ─────────────────────────────────────────────────────────
        private SWO1.Core.GameController2D gameController;
        private SWO1.Core.GameEventBus eventBus;
        private InputField coordInput;

        // ─────────────────────────────────────────────────────────
        void Start()
        {
            FindButtons();
            CacheOriginalColors();
            FindSystems();
            BindEvents();
            RefreshHighlight();
            UpdateSendButtonState();
        }

        // ─────────────────────────────────────────────────────────
        //  查找按钮
        // ─────────────────────────────────────────────────────────
        private void FindButtons()
        {
            Transform commandMenu = GameObject.Find("CommandMenu")?.transform;
            if (commandMenu == null)
            {
                // 静默跳过，不报错
                return;
            }

            // 查找部队按钮: Unit_0, Unit_1, Unit_2
            for (int i = 0; i < 3; i++)
            {
                Transform btn = commandMenu.Find($"Unit_{i}");
                if (btn != null)
                {
                    unitButtons[i] = btn.GetComponent<Button>();
                    unitButtonImages[i] = btn.GetComponent<Image>();
                }
                else
                {
                    Debug.LogWarning($"[CommandMenuBinder] 找不到 Unit_{i}");
                }
            }

            // 查找指令按钮: Cmd_推进, Cmd_防御, Cmd_侦察, Cmd_攻击, Cmd_撤退, Cmd_查询, Cmd_待命
            for (int i = 0; i < 7; i++)
            {
                Transform btn = commandMenu.Find($"Cmd_{CommandNames[i]}");
                if (btn != null)
                {
                    commandButtons[i] = btn.GetComponent<Button>();
                    commandButtonImages[i] = btn.GetComponent<Image>();
                }
                else
                {
                    Debug.LogWarning($"[CommandMenuBinder] 找不到 Cmd_{CommandNames[i]}");
                }
            }

            // 查找频率按钮: Freq_0 到 Freq_4
            for (int i = 0; i < 5; i++)
            {
                Transform btn = commandMenu.Find($"Freq_{i}");
                if (btn != null)
                {
                    freqButtons[i] = btn.GetComponent<Button>();
                    freqButtonImages[i] = btn.GetComponent<Image>();
                }
                else
                {
                    Debug.LogWarning($"[CommandMenuBinder] 找不到 Freq_{i}");
                }
            }

            // 查找发送按钮
            Transform sendBtn = commandMenu.Find("SendBtn");
            if (sendBtn != null)
            {
                sendButton = sendBtn.GetComponent<Button>();
                sendButtonImage = sendBtn.GetComponent<Image>();
            }
            else
            {
                Debug.LogWarning("[CommandMenuBinder] 找不到 SendBtn");
            }

            // 查找坐标输入框
            Transform inputGo = commandMenu.Find("InputField");
            if (inputGo != null)
            {
                coordInput = inputGo.GetComponent<InputField>();
            }
        }

        // ─────────────────────────────────────────────────────────
        //  缓存原始颜色
        // ─────────────────────────────────────────────────────────
        private void CacheOriginalColors()
        {
            unitButtonOriginalColors = new Color[3];
            commandButtonOriginalColors = new Color[7];
            freqButtonOriginalColors = new Color[5];

            for (int i = 0; i < 3; i++)
            {
                if (unitButtonImages[i] != null)
                    unitButtonOriginalColors[i] = unitButtonImages[i].color;
            }

            for (int i = 0; i < 7; i++)
            {
                if (commandButtonImages[i] != null)
                    commandButtonOriginalColors[i] = commandButtonImages[i].color;
            }

            for (int i = 0; i < 5; i++)
            {
                if (freqButtonImages[i] != null)
                    freqButtonOriginalColors[i] = freqButtonImages[i].color;
            }

            if (sendButtonImage != null)
                sendButtonOriginalColor = sendButtonImage.color;
        }

        // ─────────────────────────────────────────────────────────
        //  查找系统引用
        // ─────────────────────────────────────────────────────────
        private void FindSystems()
        {
            gameController = FindFirstObjectByType<SWO1.Core.GameController2D>();
            eventBus = SWO1.Core.GameEventBus.Instance;

            if (gameController == null)
                Debug.LogWarning("[CommandMenuBinder] 找不到 GameController2D");
        }

        // ─────────────────────────────────────────────────────────
        //  绑定事件
        // ─────────────────────────────────────────────────────────
        private void BindEvents()
        {
            // 绑定部队按钮
            for (int i = 0; i < 3; i++)
            {
                int idx = i; // 捕获索引
                if (unitButtons[i] != null)
                {
                    unitButtons[i].onClick.AddListener(() => OnUnitSelected(idx));
                }
            }

            // 绑定指令按钮
            for (int i = 0; i < 7; i++)
            {
                int idx = i;
                if (commandButtons[i] != null)
                {
                    commandButtons[i].onClick.AddListener(() => OnCommandSelected(idx));
                }
            }

            // 绑定频率按钮
            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                if (freqButtons[i] != null)
                {
                    freqButtons[i].onClick.AddListener(() => OnFreqSelected(idx));
                }
            }

            // 绑定发送按钮
            if (sendButton != null)
            {
                sendButton.onClick.AddListener(OnSendClicked);
            }
        }

        // ─────────────────────────────────────────────────────────
        //  按钮回调
        // ─────────────────────────────────────────────────────────
        private void OnUnitSelected(int index)
        {
            selectedUnitIndex = index;
            RefreshHighlight();
            UpdateSendButtonState();

            string unitId = UnitIds[index];
            Debug.Log($"[CommandMenuBinder] 选中部队: {UnitNames[index]} ({unitId})");

            // 通知 EventBus
            eventBus?.RaiseUnitSelected(unitId);
        }

        private void OnCommandSelected(int index)
        {
            selectedCommandIndex = index;
            RefreshHighlight();
            UpdateSendButtonState();

            Debug.Log($"[CommandMenuBinder] 选中指令: {CommandNames[index]}");
        }

        private void OnFreqSelected(int index)
        {
            selectedFreqIndex = index + 1; // 频率 1-5
            RefreshHighlight();

            Debug.Log($"[CommandMenuBinder] 切换频率: {selectedFreqIndex}");

            // 通知 EventBus
            eventBus?.RaiseFrequencyChanged(selectedFreqIndex);
        }

        private void OnSendClicked()
        {
            if (selectedUnitIndex < 0 || selectedCommandIndex < 0)
                return;

            if (gameController == null)
            {
                Debug.LogError("[CommandMenuBinder] GameController2D 未找到，无法发送指令");
                return;
            }

            string unitId = UnitIds[selectedUnitIndex];
            SWO1.Radio.CommandType cmdType = CommandTypes[selectedCommandIndex];

            // 解析坐标输入 (如果是指令需要坐标)
            int targetX = -1, targetY = -1;
            if (NeedsCoordinate(cmdType))
            {
                if (!TryParseCoordinate(coordInput?.text, out targetX, out targetY))
                {
                    Debug.LogWarning("[CommandMenuBinder] 坐标格式错误，使用默认值");
                    // 使用默认目标位置
                    targetX = 8;
                    targetY = 6;
                }
            }
            else if (cmdType == SWO1.Radio.CommandType.Scout)
            {
                // 侦察指令: 默认向前侦察2格
                targetX = 1;
                targetY = 1;
            }

            // 调用 GameController2D 执行指令
            gameController.ExecuteCommand(unitId, cmdType, targetX, targetY);

            // 通知 EventBus (映射 Radio.CommandType → Command.CommandType)
            if (eventBus != null)
            {
                var mappedCmdType = MapToCommandType(cmdType);
                eventBus.RaiseUICommandIssued(unitId, mappedCmdType);
            }

            Debug.Log($"[CommandMenuBinder] 发送指令: {UnitNames[selectedUnitIndex]} - {CommandNames[selectedCommandIndex]}");

            // 发送后重置选择 (可选)
            // selectedCommandIndex = -1;
            // RefreshHighlight();
            // UpdateSendButtonState();
        }

        // ─────────────────────────────────────────────────────────
        //  映射 Radio.CommandType → Command.CommandType
        // ─────────────────────────────────────────────────────────
        private SWO1.Command.CommandType MapToCommandType(SWO1.Radio.CommandType radioType)
        {
            return radioType switch
            {
                SWO1.Radio.CommandType.Move => SWO1.Command.CommandType.Move,
                SWO1.Radio.CommandType.Attack => SWO1.Command.CommandType.Attack,
                SWO1.Radio.CommandType.Defend => SWO1.Command.CommandType.Defend,
                SWO1.Radio.CommandType.Retreat => SWO1.Command.CommandType.Retreat,
                SWO1.Radio.CommandType.Scout => SWO1.Command.CommandType.Recon,
                SWO1.Radio.CommandType.Status => SWO1.Command.CommandType.StatusQuery,
                _ => SWO1.Command.CommandType.Custom
            };
        }

        // ─────────────────────────────────────────────────────────
        //  判断指令是否需要坐标
        // ─────────────────────────────────────────────────────────
        private bool NeedsCoordinate(SWO1.Radio.CommandType type)
        {
            return type == SWO1.Radio.CommandType.Move ||
                   type == SWO1.Radio.CommandType.Attack ||
                   type == SWO1.Radio.CommandType.Retreat;
        }

        // ─────────────────────────────────────────────────────────
        //  解析坐标 "C5" -> (2, 4)
        // ─────────────────────────────────────────────────────────
        private bool TryParseCoordinate(string input, out int x, out int y)
        {
            x = -1;
            y = -1;

            if (string.IsNullOrEmpty(input))
                return false;

            input = input.Trim().ToUpper();

            if (input.Length < 2)
                return false;

            char colChar = input[0];
            if (colChar < 'A' || colChar > 'Z')
                return false;

            x = colChar - 'A';

            string numPart = input.Substring(1);
            if (!int.TryParse(numPart, out y))
                return false;

            y = y - 1; // 转换为 0-based

            return x >= 0 && x < 16 && y >= 0 && y < 12;
        }

        // ─────────────────────────────────────────────────────────
        //  更新高亮显示
        // ─────────────────────────────────────────────────────────
        private void RefreshHighlight()
        {
            // 高亮部队按钮
            for (int i = 0; i < 3; i++)
            {
                if (unitButtonImages[i] != null)
                {
                    if (i == selectedUnitIndex)
                        unitButtonImages[i].color = unitButtonOriginalColors[i] * 1.5f;
                    else
                        unitButtonImages[i].color = unitButtonOriginalColors[i];
                }
            }

            // 高亮指令按钮
            for (int i = 0; i < 7; i++)
            {
                if (commandButtonImages[i] != null)
                {
                    if (i == selectedCommandIndex)
                        commandButtonImages[i].color = commandButtonOriginalColors[i] * 1.5f;
                    else
                        commandButtonImages[i].color = commandButtonOriginalColors[i];
                }
            }

            // 高亮频率按钮
            for (int i = 0; i < 5; i++)
            {
                if (freqButtonImages[i] != null)
                {
                    if (i == selectedFreqIndex - 1) // 频率是 1-5，索引是 0-4
                        freqButtonImages[i].color = freqButtonOriginalColors[i] * 1.5f;
                    else
                        freqButtonImages[i].color = freqButtonOriginalColors[i];
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        //  更新发送按钮状态
        // ─────────────────────────────────────────────────────────
        private void UpdateSendButtonState()
        {
            if (sendButton != null)
            {
                bool canSend = selectedUnitIndex >= 0 && selectedCommandIndex >= 0;
                sendButton.interactable = canSend;

                if (sendButtonImage != null)
                {
                    if (canSend)
                        sendButtonImage.color = sendButtonOriginalColor;
                    else
                        sendButtonImage.color = sendButtonOriginalColor * 0.5f;
                }
            }
        }

        // ─────────────────────────────────────────────────────────
        //  清理
        // ─────────────────────────────────────────────────────────
        private void OnDestroy()
        {
            // 移除事件监听
            for (int i = 0; i < 3; i++)
            {
                if (unitButtons[i] != null)
                    unitButtons[i].onClick.RemoveAllListeners();
            }

            for (int i = 0; i < 7; i++)
            {
                if (commandButtons[i] != null)
                    commandButtons[i].onClick.RemoveAllListeners();
            }

            for (int i = 0; i < 5; i++)
            {
                if (freqButtons[i] != null)
                    freqButtons[i].onClick.RemoveAllListeners();
            }

            if (sendButton != null)
                sendButton.onClick.RemoveAllListeners();
        }
    }
}
