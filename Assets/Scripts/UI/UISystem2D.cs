// UISystem2D.cs — 纯 2D Canvas UI 系统
// Screen Space Overlay Canvas 版本，不依赖 3D 场景内元素
// 通过 GameEventBus 订阅事件，CommandSystem API 发指令
// 命名空间: SWO1.UI
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.Command;
using SWO1.Intelligence;

namespace SWO1.UI
{
    #region 数据定义

    /// <summary>无线电消息运行时状态（用于 2D Text）</summary>
    public class RadioMessageRuntime2D
    {
        public GameObject GameObject;
        public Text TextComp;
        public string FullText;
        public ReportPriority Priority;
        public float SpawnTime;
        public bool IsInterfered;
    }

    #endregion

    #region 军事配色（2D 版）

    public static class MilColor2D
    {
        public static readonly Color Background   = new Color(0.10f, 0.11f, 0.18f, 0.92f); // #1a1a2e
        public static readonly Color PanelBG       = new Color(0.12f, 0.13f, 0.20f, 0.88f);
        public static readonly Color MilitaryGreen = new Color(0.29f, 0.40f, 0.25f);        // #4a6741
        public static readonly Color Amber         = new Color(0.83f, 0.66f, 0.29f);        // #d4a84b
        public static readonly Color AlertRed      = new Color(0.75f, 0.06f, 0.06f);        // #c01010
        public static readonly Color TextWhite     = Color.white;
        public static readonly Color TextGray      = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color HPGood        = new Color(0.20f, 0.75f, 0.25f);
        public static readonly Color HPWarn        = new Color(0.85f, 0.75f, 0.15f);
        public static readonly Color HPDanger      = new Color(0.85f, 0.25f, 0.10f);
        public static readonly Color ButtonNormal  = new Color(0.29f, 0.40f, 0.25f);        // #4a6741
        public static readonly Color ButtonHover   = new Color(0.35f, 0.48f, 0.30f);
        public static readonly Color ButtonPressed = new Color(0.22f, 0.32f, 0.20f);
        public static readonly Color FreqActive    = new Color(0.83f, 0.66f, 0.29f);        // #d4a84b
        public static readonly Color FreqInactive  = new Color(0.29f, 0.40f, 0.25f);        // #4a6741
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════
    // UISystem2D — 纯 2D Canvas 主控
    // ═══════════════════════════════════════════════════════════════

    public class UISystem2D : MonoBehaviour
    {
        #region 字段

        // --- Canvas 引用 ---
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _canvasRect;

        // --- 子面板 ---
        private RadioTextPanel _radioPanel;
        private StatusPanel _statusPanel;
        private CommandPanel _commandPanel;
        private GameResultPopup _resultPopup;
        private FrequencyPanel _freqPanel;

        // --- CommandSystem 引用 ---
        private CommandSystem _commandSystem;

        // --- 最新状态 ---
        private StatusSummary _lastStatus;

        #endregion

        #region 公开回调

        /// <summary>指令发出回调 (targetUnitId, commandType)</summary>
        public Action<string, CommandType> OnCommandIssued;

        #endregion

        #region 生命周期

        void Awake()
        {
            CreateCanvas();
            CreateRadioPanel();
            CreateStatusPanel();
            CreateCommandPanel();
            CreateFrequencyPanel();
            CreateGameResultPopup();

            // 缓存 CommandSystem
            _commandSystem = FindObjectOfType<CommandSystem>();
        }

        void Start()
        {
            StartCoroutine(SubscribeToEventsDelayed());
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region Canvas 创建

        private void CreateCanvas()
        {
            // 如果场景中已有 Canvas，优先使用
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            _scaler = GetComponent<CanvasScaler>();
            if (_scaler == null)
                _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _canvasRect = _canvas.GetComponent<RectTransform>();
        }

        #endregion

        #region UI 元素工厂

        /// <summary>创建带 RectTransform + Image 的面板</summary>
        private GameObject CreatePanelGO(string name, Transform parent,
            Vector2 anchoredPos, Vector2 size, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        /// <summary>创建 Text 组件</summary>
        private Text CreateTextGO(string name, Transform parent,
            string content, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        /// <summary>创建按钮</summary>
        private Button CreateButtonGO(string name, Transform parent,
            Vector2 anchoredPos, Vector2 size, string label,
            Color bgColor, Color textColor, int fontSize = 18)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.color = bgColor;

            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = MilColor2D.ButtonHover;
            cb.pressedColor = MilColor2D.ButtonPressed;
            cb.selectedColor = bgColor;
            btn.colors = cb;

            // 按钮文字
            var text = CreateTextGO("Label", go.transform, label, fontSize, textColor, TextAnchor.MiddleCenter);
            var trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        }

        /// <summary>创建 ScrollRect 面板</summary>
        private (GameObject root, ScrollRect scroll, RectTransform content) CreateScrollRect(
            string name, Transform parent, Vector2 anchoredPos, Vector2 size, Color bgColor)
        {
            // 根面板
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = anchoredPos;
            rootRt.sizeDelta = size;
            root.GetComponent<Image>().color = bgColor;

            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(root.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(4, 4);
            vpRt.offsetMax = new Vector2(-4, -4);
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // Content
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.spacing = 2;
            vlg.padding = new RectOffset(6, 6, 4, 4);

            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ScrollRect
            var scroll = root.AddComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.viewport = vpRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            return (root, scroll, contentRt);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════
        // 1. RadioTextPanel — 左侧无线电
        // ═══════════════════════════════════════════════════════════

        private class RadioTextPanel
        {
            public GameObject Root;
            public ScrollRect Scroll;
            public RectTransform Content;
            public List<RadioMessageRuntime2D> Messages = new List<RadioMessageRuntime2D>();
            private const int MaxMessages = 80;

            public void AddRadioMessage(string timestamp, string source, string text,
                bool isInterfered, int priority)
            {
                // 构建显示文本
                string displayText;
                Color textColor;

                if (isInterfered)
                {
                    // 干扰：波浪号效果
                    displayText = $"[{timestamp}] ~~{source}~~ {Wavify(text)}";
                    textColor = MilColor2D.TextGray;
                }
                else
                {
                    displayText = $"[{timestamp}] {source} {text}";
                    textColor = (priority >= 2) ? MilColor2D.AlertRed : MilColor2D.TextWhite;
                }

                // 创建条目
                var entryGO = new GameObject("RadioMsg", typeof(RectTransform));
                entryGO.transform.SetParent(Content, false);
                var entryRt = entryGO.GetComponent<RectTransform>();
                entryRt.sizeDelta = new Vector2(0, 0);

                var textComp = entryGO.AddComponent<Text>();
                textComp.text = displayText;
                textComp.fontSize = 14;
                textComp.color = textColor;
                textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
                textComp.verticalOverflow = VerticalWrapMode.Overflow;
                textComp.alignment = TextAnchor.UpperLeft;
                textComp.raycastTarget = false;

                // 布局元素
                var le = entryGO.AddComponent<LayoutElement>();
                le.flexibleWidth = 1f;
                le.minHeight = 20f;

                var msgData = new RadioMessageRuntime2D
                {
                    GameObject = entryGO,
                    TextComp = textComp,
                    FullText = displayText,
                    Priority = (ReportPriority)priority,
                    SpawnTime = Time.time,
                    IsInterfered = isInterfered
                };

                Messages.Add(msgData);

                // 限制条目数
                while (Messages.Count > MaxMessages)
                {
                    Destroy(Messages[0].GameObject);
                    Messages.RemoveAt(0);
                }

                // 延迟滚动到底部
                if (Scroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    Scroll.verticalNormalizedPosition = 0f;
                }
            }

            /// <summary>将文本中字符用波浪号隔开（干扰效果）</summary>
            private string Wavify(string input)
            {
                if (string.IsNullOrEmpty(input)) return input;
                char[] chars = input.ToCharArray();
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < chars.Length; i++)
                {
                    sb.Append(chars[i]);
                    if (i < chars.Length - 1 && UnityEngine.Random.value > 0.4f)
                        sb.Append('~');
                }
                return sb.ToString();
            }
        }

        private void CreateRadioPanel()
        {
            float panelW = 400f, panelH = 600f;
            float margin = 10f;
            Vector2 pos = new Vector2(margin + panelW / 2f, 1080f - margin - panelH / 2f);

            var (root, scroll, content) = CreateScrollRect(
                "RadioTextPanel", _canvasRect, pos, new Vector2(panelW, panelH), MilColor2D.PanelBG);

            // 标题
            var title = CreateTextGO("Title", root.transform,
                "📻 无线电监听", 16, MilColor2D.Amber, TextAnchor.UpperCenter);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.offsetMin = new Vector2(8, -24);
            titleRt.offsetMax = new Vector2(-8, -4);

            // 给内容区域加额外上边距（避开标题）
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding.top = 26;

            _radioPanel = new RadioTextPanel
            {
                Root = root,
                Scroll = scroll,
                Content = content
            };
        }

        // ═══════════════════════════════════════════════════════════
        // 2. StatusPanel — 右上角状态
        // ═══════════════════════════════════════════════════════════

        private class StatusPanel
        {
            public GameObject Root;
            public Text TimeText;
            public Slider BridgeHPSlider;
            public Text BridgeHPLabel;
            public Text WaveText;
            public Text CountdownText;
            public PlatoonSlot[] PlatoonSlots = new PlatoonSlot[3];

            public class PlatoonSlot
            {
                public Text NameText;
                public Slider StrengthSlider;
                public Text StateText;
            }

            public void UpdateStatus(StatusSummary summary)
            {
                if (summary == null) return;

                // 游戏时间
                TimeText.text = summary.CurrentTime ?? "00:00";

                // 桥梁 HP
                float hpRatio = summary.BridgeMaxHP > 0
                    ? Mathf.Clamp01(summary.BridgeHP / summary.BridgeMaxHP)
                    : 0f;
                BridgeHPSlider.value = hpRatio;
                BridgeHPLabel.text = $"桥梁: {summary.BridgeHP:F0}/{summary.BridgeMaxHP:F0}";

                // HP 颜色
                var fill = BridgeHPSlider.fillRect?.GetComponent<Image>();
                if (fill != null)
                {
                    if (hpRatio > 0.6f) fill.color = MilColor2D.HPGood;
                    else if (hpRatio > 0.3f) fill.color = MilColor2D.HPWarn;
                    else fill.color = MilColor2D.HPDanger;
                }

                // 波次/阶段
                WaveText.text = $"阶段: {summary.CurrentPhase ?? "-"}";

                // 倒计时
                if (summary.CountdownSeconds > 0)
                {
                    int min = Mathf.FloorToInt(summary.CountdownSeconds / 60f);
                    int sec = Mathf.FloorToInt(summary.CountdownSeconds % 60f);
                    CountdownText.text = $"⏱ {min:D2}:{sec:D2}";
                    CountdownText.color = summary.CountdownSeconds < 120
                        ? MilColor2D.AlertRed : MilColor2D.Amber;
                }
                else
                {
                    CountdownText.text = "";
                }

                // 排状态
                UpdatePlatoon(0, summary.Platoon1);
                UpdatePlatoon(1, summary.Platoon2);
                UpdatePlatoon(2, summary.Platoon3);
            }

            private void UpdatePlatoon(int index, PlatoonStatus ps)
            {
                if (index >= PlatoonSlots.Length || PlatoonSlots[index] == null) return;
                var slot = PlatoonSlots[index];
                if (ps == null)
                {
                    slot.NameText.text = "---";
                    slot.StrengthSlider.value = 0;
                    slot.StateText.text = "";
                    return;
                }
                slot.NameText.text = ps.Name ?? "---";
                slot.StrengthSlider.value = Mathf.Clamp01(ps.Strength);
                slot.StateText.text = ps.State ?? "";

                // 兵力条颜色
                var fill = slot.StrengthSlider.fillRect?.GetComponent<Image>();
                if (fill != null)
                {
                    if (ps.Strength > 0.6f) fill.color = MilColor2D.HPGood;
                    else if (ps.Strength > 0.3f) fill.color = MilColor2D.HPWarn;
                    else fill.color = MilColor2D.HPDanger;
                }
            }
        }

        private void CreateStatusPanel()
        {
            float panelW = 300f, panelH = 400f;
            float margin = 10f;
            Vector2 pos = new Vector2(1920f - margin - panelW / 2f, 1080f - margin - panelH / 2f);

            var root = CreatePanelGO("StatusPanel", _canvasRect, pos,
                new Vector2(panelW, panelH), MilColor2D.PanelBG);

            // 垂直布局
            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 6;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            _statusPanel = new StatusPanel { Root = root };

            // 时间显示
            _statusPanel.TimeText = CreateTextChild(root.transform, "TimeText", "00:00",
                28, MilColor2D.Amber, TextAnchor.MiddleCenter, 40);

            // 桥梁 HP 标签
            _statusPanel.BridgeHPLabel = CreateTextChild(root.transform, "BridgeHPLabel",
                "桥梁: 100/100", 14, MilColor2D.TextWhite, TextAnchor.MiddleLeft, 22);

            // 桥梁 HP Slider
            _statusPanel.BridgeHPSlider = CreateSliderChild(root.transform, "BridgeHP",
                MilColor2D.HPGood);

            // 波次
            _statusPanel.WaveText = CreateTextChild(root.transform, "WaveText",
                "阶段: -", 14, MilColor2D.TextWhite, TextAnchor.MiddleLeft, 22);

            // 倒计时
            _statusPanel.CountdownText = CreateTextChild(root.transform, "CountdownText",
                "", 18, MilColor2D.Amber, TextAnchor.MiddleCenter, 30);

            // 分隔线
            CreateTextChild(root.transform, "Sep1", "───── 部队状态 ─────",
                12, MilColor2D.TextGray, TextAnchor.MiddleCenter, 20);

            // 3 个排状态槽
            for (int i = 0; i < 3; i++)
            {
                _statusPanel.PlatoonSlots[i] = CreatePlatoonSlot(root.transform, i);
            }
        }

        private Text CreateTextChild(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor alignment, float preferredHeight)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = preferredHeight;
            le.flexibleWidth = 1f;

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;
            return text;
        }

        private Slider CreateSliderChild(Transform parent, string name, Color fillColor)
        {
            // Background
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 20;
            le.flexibleWidth = 1f;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f);

            // Fill Area
            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.offsetMin = new Vector2(4, 2);
            faRt.offsetMax = new Vector2(-4, -2);

            // Fill
            var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(fillArea.transform, false);
            var fillImg = fillGO.GetComponent<Image>();
            fillImg.color = fillColor;
            var fillRt = fillGO.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.maxValue = 1f;
            slider.minValue = 0f;
            slider.value = 1f;
            slider.interactable = false;

            return slider;
        }

        private StatusPanel.PlatoonSlot CreatePlatoonSlot(Transform parent, int index)
        {
            var slot = new StatusPanel.PlatoonSlot();

            var container = new GameObject($"Platoon_{index}", typeof(RectTransform));
            container.transform.SetParent(parent, false);
            var le = container.AddComponent<LayoutElement>();
            le.preferredHeight = 60;
            le.flexibleWidth = 1f;

            var vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;

            slot.NameText = CreateTextChild(container.transform, "Name",
                $"排 {index + 1}", 14, MilColor2D.Amber, TextAnchor.MiddleLeft, 20);
            slot.StrengthSlider = CreateSliderChild(container.transform, "Strength",
                MilColor2D.HPGood);
            slot.StateText = CreateTextChild(container.transform, "State",
                "待命", 12, MilColor2D.TextGray, TextAnchor.MiddleRight, 18);

            return slot;
        }

        // ═══════════════════════════════════════════════════════════
        // 3. CommandPanel — 底部指令面板
        // ═══════════════════════════════════════════════════════════

        private class CommandPanel
        {
            public GameObject Root;
            public Dropdown TargetDropdown;
            public Button[] TypeButtons = new Button[6];
            public Button SendButton;
            public int SelectedTypeIndex = -1;

            public void RefreshInteractable(bool canSend)
            {
                SendButton.interactable = canSend && SelectedTypeIndex >= 0;
            }
        }

        private static readonly string[] UnitLabels = { "红一排", "红二排", "蓝四坦克排" };
        private static readonly string[] UnitIds = { "RED_1", "RED_2", "BLUE_4" };
        private static readonly string[] CommandTypeLabels =
            { "移动", "攻击", "防御", "撤退", "侦察", "查询" };
        private static readonly CommandType[] CommandTypeValues =
            { CommandType.Move, CommandType.Attack, CommandType.Defend,
              CommandType.Retreat, CommandType.Recon, CommandType.StatusQuery };

        private void CreateCommandPanel()
        {
            float panelW = 1920f - 50f - 310f; // 留出频率面板空间
            float panelH = 120f;
            float freqPanelWidth = 60f;
            Vector2 pos = new Vector2(panelW / 2f + freqPanelWidth / 2f + 5f, panelH / 2f + 10f);

            var root = CreatePanelGO("CommandPanel", _canvasRect, pos,
                new Vector2(panelW, panelH), MilColor2D.PanelBG);

            // 水平布局
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(10, 10, 10, 10);
            hlg.spacing = 8;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;

            _commandPanel = new CommandPanel { Root = root };

            // 目标部队 Dropdown
            var dropdownGO = new GameObject("TargetDropdown", typeof(RectTransform));
            dropdownGO.transform.SetParent(root.transform, false);
            var ddLe = dropdownGO.AddComponent<LayoutElement>();
            ddLe.preferredWidth = 180;
            ddLe.flexibleHeight = 1f;
            var ddImg = dropdownGO.AddComponent<Image>();
            ddImg.color = MilColor2D.MilitaryGreen;
            var dd = dropdownGO.AddComponent<Dropdown>();
            dd.options = new List<Dropdown.OptionData>();
            foreach (var label in UnitLabels)
                dd.options.Add(new Dropdown.OptionData(label));
            // Dropdown 标签
            var ddLabel = CreateTextGO("Label", dropdownGO.transform,
                UnitLabels[0], 16, MilColor2D.TextWhite, TextAnchor.MiddleCenter);
            dd.captionText = ddLabel;
            // Dropdown Template
            CreateDropdownTemplate(dropdownGO.transform);
            _commandPanel.TargetDropdown = dd;

            // 指令类型按钮
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var btn = CreateInlineButton(root.transform, $"Cmd_{CommandTypeLabels[i]}",
                    CommandTypeLabels[i], 100f);
                _commandPanel.TypeButtons[i] = btn;
                btn.onClick.AddListener(() => OnCommandTypeClicked(idx));
            }

            // 发送按钮
            _commandPanel.SendButton = CreateInlineButton(root.transform, "SendBtn",
                "▶ 发送指令", 140f, MilColor2D.Amber, MilColor2D.Background);
            _commandPanel.SendButton.onClick.AddListener(OnSendClicked);
        }

        private Button CreateInlineButton(Transform parent, string name, string label,
            float width, Color? bgOverride = null, Color? textOverride = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleHeight = 1f;

            var img = go.GetComponent<Image>();
            img.color = bgOverride ?? MilColor2D.ButtonNormal;

            var btn = go.GetComponent<Button>();
            var cb = btn.colors;
            cb.normalColor = bgOverride ?? MilColor2D.ButtonNormal;
            cb.highlightedColor = MilColor2D.ButtonHover;
            cb.pressedColor = MilColor2D.ButtonPressed;
            cb.selectedColor = bgOverride ?? MilColor2D.ButtonNormal;
            btn.colors = cb;

            var text = CreateTextGO("Label", go.transform, label, 16,
                textOverride ?? MilColor2D.TextWhite, TextAnchor.MiddleCenter);
            var trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        }

        private void CreateDropdownTemplate(Transform dropdownTransform)
        {
            // Template (默认隐藏)
            var template = new GameObject("Template", typeof(RectTransform), typeof(Image));
            template.transform.SetParent(dropdownTransform, false);
            var tmplRt = template.GetComponent<RectTransform>();
            tmplRt.anchorMin = new Vector2(0, 0);
            tmplRt.anchorMax = new Vector2(1, 0);
            tmplRt.pivot = new Vector2(0.5f, 1f);
            tmplRt.anchoredPosition = Vector2.zero;
            tmplRt.sizeDelta = new Vector2(0, 150);
            template.GetComponent<Image>().color = new Color(0.15f, 0.16f, 0.22f);
            template.SetActive(false);

            // Viewport
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(template.transform, false);
            var vpRt = viewport.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.one * 2;
            vpRt.offsetMax = Vector2.one * -2;
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            // Content
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0, 1);
            cRt.anchorMax = new Vector2(1, 1);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.sizeDelta = Vector2.zero;

            // Item
            var item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
            item.transform.SetParent(content.transform, false);
            var iRt = item.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0, 0.5f);
            iRt.anchorMax = new Vector2(1, 0.5f);
            iRt.sizeDelta = new Vector2(0, 30);

            var itemLabel = CreateTextGO("Item Label", item.transform, "Option", 16,
                MilColor2D.TextWhite, TextAnchor.MiddleLeft);
            var ilRt = itemLabel.GetComponent<RectTransform>();
            ilRt.anchorMin = Vector2.zero;
            ilRt.anchorMax = Vector2.one;
            ilRt.offsetMin = new Vector2(8, 0);
            ilRt.offsetMax = Vector2.zero;

            // Dropdown 引用
            var dd = dropdownTransform.GetComponent<Dropdown>();
            if (dd != null)
            {
                dd.template = tmplRt;
                dd.itemText = itemLabel;
            }
        }

        private int _selectedCommandType = -1;
        private Color _normalBtnColor = MilColor2D.ButtonNormal;

        private void OnCommandTypeClicked(int index)
        {
            _selectedCommandType = index;
            // 高亮选中的按钮
            for (int i = 0; i < _commandPanel.TypeButtons.Length; i++)
            {
                var img = _commandPanel.TypeButtons[i].GetComponent<Image>();
                img.color = (i == index) ? MilColor2D.Amber : MilColor2D.ButtonNormal;
            }
        }

        private void OnSendClicked()
        {
            if (_selectedCommandType < 0 || _selectedCommandType >= CommandTypeValues.Length)
                return;

            int unitIndex = _commandPanel.TargetDropdown.value;
            if (unitIndex < 0 || unitIndex >= UnitIds.Length)
                return;

            string targetUnit = UnitIds[unitIndex];
            CommandType cmdType = CommandTypeValues[_selectedCommandType];

            // 回调
            OnCommandIssued?.Invoke(targetUnit, cmdType);

            // 调用 CommandSystem
            if (_commandSystem != null)
            {
                string content = CommandTypeLabels[_selectedCommandType];
                _commandSystem.SendCommand(targetUnit, CurrentFrequency, cmdType, content);
            }
        }

        // ═══════════════════════════════════════════════════════════
        // 4. GameResultPopup — 中央弹窗
        // ═══════════════════════════════════════════════════════════

        private class GameResultPopup
        {
            public GameObject Root;
            public Text TitleText;
            public Text StatsText;
            public Button RestartButton;

            public void Show(GameOutcome outcome, GameResultStats stats)
            {
                Root.SetActive(true);

                // 标题
                string title = outcome switch
                {
                    GameOutcome.PerfectVictory => "🏆 完美胜利",
                    GameOutcome.PyrrhicVictory => "⚔️ 惨胜",
                    GameOutcome.PartialVictory => "📋 部分完成",
                    GameOutcome.Defeat => "💀 任务失败",
                    GameOutcome.TotalDefeat => "☠️ 全军覆没",
                    _ => "战役结束"
                };
                TitleText.text = title;
                TitleText.color = outcome switch
                {
                    GameOutcome.PerfectVictory => MilColor2D.HPGood,
                    GameOutcome.PyrrhicVictory => MilColor2D.Amber,
                    GameOutcome.PartialVictory => MilColor2D.HPWarn,
                    _ => MilColor2D.AlertRed
                };

                // 统计
                if (stats != null)
                {
                    float deliveredRate = stats.CommandsSent > 0
                        ? (float)stats.CommandsDelivered / stats.CommandsSent * 100f : 0f;
                    StatsText.text =
                        $"占领目标: {stats.ObjectivesCaptured}/{stats.TotalObjectives}\n" +
                        $"伤亡率: {stats.CasualtyRate:P0}\n" +
                        $"发送指令: {stats.CommandsSent}\n" +
                        $"送达率: {deliveredRate:F0}%\n" +
                        $"收到汇报: {stats.ReportsReceived}\n" +
                        $"游戏时长: {stats.TotalPlayTime:F0}秒";
                }
            }

            public void Hide()
            {
                Root.SetActive(false);
            }
        }

        private void CreateGameResultPopup()
        {
            // 半透明遮罩
            var overlay = CreatePanelGO("ResultOverlay", _canvasRect,
                Vector2.zero, new Vector2(1920f, 1080f), new Color(0, 0, 0, 0.7f));
            var overlayRt = overlay.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;

            // 弹窗
            var popup = CreatePanelGO("ResultPopup", overlay.transform,
                Vector2.zero, new Vector2(500, 400), MilColor2D.PanelBG);

            // 垂直布局
            var vlg = popup.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(30, 30, 30, 30);
            vlg.spacing = 15;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childAlignment = TextAnchor.MiddleCenter;

            _resultPopup = new GameResultPopup { Root = overlay };

            // 标题
            _resultPopup.TitleText = CreateTextChild(popup.transform, "Title",
                "战役结束", 32, MilColor2D.Amber, TextAnchor.MiddleCenter, 50);

            // 统计
            _resultPopup.StatsText = CreateTextChild(popup.transform, "Stats",
                "", 18, MilColor2D.TextWhite, TextAnchor.MiddleLeft, 180);

            // 重新开始按钮
            var btnGO = new GameObject("RestartBtn", typeof(RectTransform),
                typeof(Image), typeof(Button), typeof(LayoutElement));
            btnGO.transform.SetParent(popup.transform, false);
            var btnLe = btnGO.GetComponent<LayoutElement>();
            btnLe.preferredHeight = 50;
            btnLe.flexibleWidth = 1f;

            btnGO.GetComponent<Image>().color = MilColor2D.MilitaryGreen;
            var restartBtn = btnGO.GetComponent<Button>();
            var cb = restartBtn.colors;
            cb.normalColor = MilColor2D.MilitaryGreen;
            cb.highlightedColor = MilColor2D.ButtonHover;
            cb.pressedColor = MilColor2D.ButtonPressed;
            restartBtn.colors = cb;
            restartBtn.onClick.AddListener(OnRestartClicked);

            var btnLabel = CreateTextGO("Label", btnGO.transform,
                "🔄 重新开始", 20, MilColor2D.TextWhite, TextAnchor.MiddleCenter);
            var blRt = btnLabel.GetComponent<RectTransform>();
            blRt.anchorMin = Vector2.zero;
            blRt.anchorMax = Vector2.one;
            blRt.offsetMin = Vector2.zero;
            blRt.offsetMax = Vector2.zero;

            _resultPopup.RestartButton = restartBtn;

            // 默认隐藏
            overlay.SetActive(false);
        }

        private void OnRestartClicked()
        {
            // 重新加载当前场景
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        // ═══════════════════════════════════════════════════════════
        // 5. FrequencyPanel — 频率切换
        // ═══════════════════════════════════════════════════════════

        private class FrequencyPanel
        {
            public GameObject Root;
            public Button[] FreqButtons = new Button[5];
            public Text[] FreqLabels = new Text[5];
            private int _currentFrequency = 1;

            public int CurrentFrequency
            {
                get => _currentFrequency;
                set
                {
                    _currentFrequency = Mathf.Clamp(value, 1, 5);
                    UpdateHighlight();
                }
            }

            public void UpdateHighlight()
            {
                for (int i = 0; i < 5; i++)
                {
                    bool active = (i + 1) == _currentFrequency;
                    FreqButtons[i].GetComponent<Image>().color =
                        active ? MilColor2D.FreqActive : MilColor2D.FreqInactive;
                    FreqLabels[i].color = active ? MilColor2D.Background : MilColor2D.TextWhite;
                }
            }
        }

        private void CreateFrequencyPanel()
        {
            float panelW = 60f, panelH = 260f;
            Vector2 pos = new Vector2(panelW / 2f + 10f, panelH / 2f + 10f);

            var root = CreatePanelGO("FrequencyPanel", _canvasRect, pos,
                new Vector2(panelW, panelH), MilColor2D.PanelBG);

            var vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 8, 8);
            vlg.spacing = 5;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            _freqPanel = new FrequencyPanel { Root = root };

            for (int i = 0; i < 5; i++)
            {
                int freq = i + 1;
                var btnGO = new GameObject($"Freq_{freq}", typeof(RectTransform),
                    typeof(Image), typeof(Button), typeof(LayoutElement));
                btnGO.transform.SetParent(root.transform, false);

                var btnImg = btnGO.GetComponent<Image>();
                btnImg.color = (freq == 1) ? MilColor2D.FreqActive : MilColor2D.FreqInactive;

                var btn = btnGO.GetComponent<Button>();
                int capturedFreq = freq;
                btn.onClick.AddListener(() => OnFreqClicked(capturedFreq));

                var cb = btn.colors;
                cb.normalColor = MilColor2D.FreqInactive;
                cb.highlightedColor = MilColor2D.ButtonHover;
                cb.pressedColor = MilColor2D.Amber;
                btn.colors = cb;

                var label = CreateTextGO("Label", btnGO.transform,
                    freq.ToString(), 22, Color.white, TextAnchor.MiddleCenter);
                var lRt = label.GetComponent<RectTransform>();
                lRt.anchorMin = Vector2.zero;
                lRt.anchorMax = Vector2.one;
                lRt.offsetMin = Vector2.zero;
                lRt.offsetMax = Vector2.zero;

                _freqPanel.FreqButtons[i] = btn;
                _freqPanel.FreqLabels[i] = label;
            }

            _freqPanel.UpdateHighlight();
        }

        private void OnFreqClicked(int freq)
        {
            _freqPanel.CurrentFrequency = freq;
        }

        // ═══════════════════════════════════════════════════════════
        // 事件订阅
        // ═══════════════════════════════════════════════════════════

        private IEnumerator SubscribeToEventsDelayed()
        {
            float timeout = 5f;
            while (GameEventBus.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }

            if (GameEventBus.Instance != null)
            {
                var bus = GameEventBus.Instance;
                bus.OnRadioReportDelivered += HandleRadioReportDelivered;
                bus.OnRadioInterference += HandleRadioInterference;
                bus.OnGameTimeUpdated += HandleGameTimeUpdated;
                bus.OnBattlefieldUpdated += HandleBattlefieldUpdated;
                bus.OnGameOutcomeChanged += HandleGameOutcomeChanged;
                Debug.Log("[UISystem2D] GameEventBus 事件已订阅");
            }
            else
            {
                Debug.LogError("[UISystem2D] GameEventBus 初始化超时，事件订阅失败!");
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameEventBus.Instance != null)
            {
                var bus = GameEventBus.Instance;
                bus.OnRadioReportDelivered -= HandleRadioReportDelivered;
                bus.OnRadioInterference -= HandleRadioInterference;
                bus.OnGameTimeUpdated -= HandleGameTimeUpdated;
                bus.OnBattlefieldUpdated -= HandleBattlefieldUpdated;
                bus.OnGameOutcomeChanged -= HandleGameOutcomeChanged;
            }
        }

        #region 事件处理

        private void HandleRadioReportDelivered(RadioReport report)
        {
            if (report == null) return;
            string time = FormatGameTime(report.ActualEventTime);
            string source = report.SourceUnitId ?? "未知";
            string text = report.FormattedText
                ?? report.Content?.Situation
                ?? "（无内容）";
            int priority = (int)report.Priority;

            _radioPanel?.AddRadioMessage(time, source, text,
                report.InterferenceLevel > 0.5f, priority);
        }

        private void HandleRadioInterference(RadioReport report)
        {
            if (report == null) return;
            string time = FormatGameTime(Time.time);
            _radioPanel?.AddRadioMessage(time, "系统", "⚠ 频道干扰", true, 2);
        }

        private void HandleGameTimeUpdated(float gameTime)
        {
            // 更新倒计时显示
            if (_lastStatus != null && _statusPanel != null)
            {
                _lastStatus.CurrentTime = FormatGameTime(gameTime);
                _statusPanel.UpdateStatus(_lastStatus);
            }
        }

        private void HandleBattlefieldUpdated(BattlefieldData data)
        {
            if (data != null && _statusPanel != null)
            {
                if (_lastStatus == null)
                    _lastStatus = new StatusSummary();

                _lastStatus.BridgeHP = data.BridgeHP;
                _lastStatus.BridgeMaxHP = data.BridgeMaxHP;
                _lastStatus.CurrentPhase = $"第{data.CurrentWave}波";
                _lastStatus.CurrentTime = FormatGameTime(data.GameTime);
                _statusPanel.UpdateStatus(_lastStatus);
            }
        }

        private void HandleGameOutcomeChanged(GameOutcome outcome)
        {
            // 构建统计并显示弹窗
            var stats = BuildGameResultStats(outcome);
            _resultPopup?.Show(outcome, stats);
        }

        #endregion

        // ═══════════════════════════════════════════════════════════
        // 公开 API
        // ═══════════════════════════════════════════════════════════

        /// <summary>添加无线电消息</summary>
        public void AddRadioMessage(string timestamp, string source, string text,
            bool isInterfered, int priority)
        {
            _radioPanel?.AddRadioMessage(timestamp, source, text, isInterfered, priority);
        }

        /// <summary>更新状态面板</summary>
        public void UpdateStatus(StatusSummary summary)
        {
            _lastStatus = summary;
            _statusPanel?.UpdateStatus(summary);
        }

        /// <summary>显示游戏结果弹窗</summary>
        public void ShowResult(GameOutcome outcome, GameResultStats stats)
        {
            _resultPopup?.Show(outcome, stats);
        }

        /// <summary>获取当前频率</summary>
        public int CurrentFrequency => _freqPanel?.CurrentFrequency ?? 1;

        /// <summary>设置频率</summary>
        public void SetFrequency(int freq)
        {
            if (_freqPanel != null)
                _freqPanel.CurrentFrequency = freq;
        }

        // ═══════════════════════════════════════════════════════════
        // 工具方法
        // ═══════════════════════════════════════════════════════════

        private string FormatGameTime(float seconds)
        {
            int totalMin = Mathf.FloorToInt(seconds / 60f);
            int h = totalMin / 60;
            int m = totalMin % 60;
            return $"{h:D2}:{m:D2}";
        }

        private GameResultStats BuildGameResultStats(GameOutcome outcome)
        {
            // 如果有 CommandSystem 和其他引用，可以构建更详细的统计
            return new GameResultStats
            {
                Outcome = outcome,
                ObjectivesCaptured = 0,
                TotalObjectives = 3,
                CasualtyRate = 0f,
                TotalPlayTime = Time.timeSinceLevelLoad,
                CommandsSent = 0,
                CommandsDelivered = 0,
                ReportsReceived = _radioPanel?.Messages.Count ?? 0
            };
        }
    }
}
