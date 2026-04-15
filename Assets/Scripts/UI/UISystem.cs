// UISystem.cs — 游戏 UI 系统 (v3.1 优化版)
// 替代传统 HUD 叠加层，所有 UI 都是指挥所内景的一部分
// 通过 GameEventBus 订阅事件，CommandSystem API 发指令
// 命名空间: SWO1.UI
//
// 子系统:
//   RadioTextPanel    — 无线电文本浮动面板 (打字机效果 v2)
//   OrderCardPanel    — 指令卡交互 (6 种指令类型)
//   StatusNotePanel   — 桌面状态便签 (桥 HP / 倒计时 / 排状态)
//   GameResultPopup   — 胜负结算弹窗 (统计 + 动画)
//   SandTableOverlay  — 沙盘标注叠加层
//   CommandStatusPanel— 指令状态追踪
//   ScreenNoiseEffect — 屏幕噪点后处理 (CRT 效果 v2)
//
// v3.1 优化 (2026-04-15):
//   - 打字机效果: 逐字符淡入动画、可变速率、光标闪烁
//   - 新鲜度衰减: 四色渐变 (MilColor.Fresh*)、逐条目独立追踪
//   - 指令卡动画: 开/关滑动动画、发送波纹反馈、冷却脉冲
//   - 屏幕噪点: 色差偏移、VHS 追踪线、干涉条纹、胶片颗粒
//   - 1920x1080 适配: CanvasScaler 配置、锚点响应式布局
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.Command;
using SWO1.Intelligence;
using SWO1.CommandPost;

namespace SWO1.UI
{
    #region 数据定义

    /// <summary>无线电文本条目 — 显示在浮动面板上</summary>
    [Serializable]
    public class RadioTextEntry
    {
        public string Timestamp;
        public string Frequency;
        public string Source;
        public string Text;
        public float DisplayTime;
        public bool IsInterfered;
        public ReportPriority Priority;
        public float Freshness = 1f;
    }

    /// <summary>状态摘要 — 显示在桌面便签上</summary>
    [Serializable]
    public class StatusSummary
    {
        public string CurrentTime;
        public string LandedUnits;
        public string EstimatedEnemy;
        public string CurrentPhase;
        public string LatestOrder;
        public float BridgeHP;
        public float BridgeMaxHP = 100f;
        public float CountdownSeconds;
        public PlatoonStatus Platoon1;
        public PlatoonStatus Platoon2;
        public PlatoonStatus Platoon3;
    }

    /// <summary>排级单位状态</summary>
    [Serializable]
    public class PlatoonStatus
    {
        public string Name;
        public float Strength;  // 0-1
        public string Position;
        public string State;    // 进攻/防御/撤退/待命
        public bool IsActive;
    }

    /// <summary>增援信息</summary>
    [Serializable]
    public class ReinforcementInfo
    {
        public string NextWaveTime;
        public string Contents;
    }

    /// <summary>结算统计数据</summary>
    [Serializable]
    public class GameResultStats
    {
        public GameOutcome Outcome;
        public int ObjectivesCaptured;
        public int TotalObjectives = 3;
        public float CasualtyRate;
        public float TotalPlayTime;
        public int CommandsSent;
        public int CommandsDelivered;
        public int CommandsLost;
        public int ReportsReceived;
        public float AccuracyAverage;
    }

    /// <summary>无线电文本条目数据 — 用于淡入动画追踪</summary>
    public class RadioMessageRuntime
    {
        public GameObject GameObject;
        public TextMeshProUGUI TextComp;
        public string FullText;
        public ReportPriority Priority;
        public float SpawnTime;
        public float Freshness = 1f;
        public Coroutine FadeRoutine;
    }

    #endregion

    #region 军事配色

    /// <summary>统一军事暗色调色板</summary>
    public static class MilColor
    {
        public static readonly Color Critical   = new Color(0.90f, 0.15f, 0.10f);
        public static readonly Color Urgent     = new Color(0.95f, 0.55f, 0.10f);
        public static readonly Color Important  = new Color(0.85f, 0.85f, 0.20f);
        public static readonly Color Routine    = new Color(0.65f, 0.75f, 0.60f);
        public static readonly Color FreshHigh  = new Color(0.20f, 0.85f, 0.30f);  // 刚到
        public static readonly Color FreshMed   = new Color(0.85f, 0.80f, 0.20f);  // 轻微衰减
        public static readonly Color FreshLow   = new Color(0.85f, 0.45f, 0.10f);  // 过时
        public static readonly Color FreshDead  = new Color(0.40f, 0.40f, 0.40f);  // 无参考价值
        public static readonly Color HPGood     = new Color(0.20f, 0.75f, 0.25f);
        public static readonly Color HPWarn     = new Color(0.85f, 0.75f, 0.15f);
        public static readonly Color HPDanger   = new Color(0.85f, 0.25f, 0.10f);
        public static readonly Color CmdMove    = new Color(0.30f, 0.60f, 0.90f);
        public static readonly Color CmdAttack  = new Color(0.90f, 0.25f, 0.15f);
        public static readonly Color CmdDefend  = new Color(0.85f, 0.75f, 0.20f);
        public static readonly Color CmdRetreat = new Color(0.60f, 0.30f, 0.70f);
        public static readonly Color CmdScout   = new Color(0.20f, 0.80f, 0.70f);
        public static readonly Color CmdBarrage = new Color(0.95f, 0.50f, 0.10f);
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════
    // UISystem — 主控 (GameEventBus 订阅 + CommandSystem API 调用)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// UI 系统 — 指挥所内景交互
    ///
    /// 设计原则:
    /// - 无传统 HUD，所有 UI 都是场景内面板/便签
    /// - 无线电文本用打字机效果 + 逐字符淡入，增加紧迫感
    /// - 指令卡是物理隐喻（像在纸上写字），带开/关动画
    /// - 通过 GameEventBus 订阅事件，不直接引用单例
    /// - 通过 CommandSystem API 发送指令
    /// - 所有 UI 使用 1920x1080 基准分辨率，CanvasScaler 缩放
    /// </summary>
    public class UISystem : MonoBehaviour
    {
        public static UISystem Instance { get; private set; }

        #region 子系统引用

        [Header("无线电文本面板")]
        [SerializeField] private RadioTextPanel radioTextPanel;

        [Header("指令卡")]
        [SerializeField] private OrderCardPanel orderCardPanel;

        [Header("状态便签")]
        [SerializeField] private StatusNotePanel statusNotePanel;

        [Header("胜负弹窗")]
        [SerializeField] private GameResultPopup gameResultPopup;

        [Header("沙盘标注")]
        [SerializeField] private SandTableOverlay sandTableOverlay;

        [Header("指令状态")]
        [SerializeField] private CommandStatusPanel commandStatusPanel;

        [Header("屏幕噪点后处理")]
        [SerializeField] private MonoBehaviour screenNoise;

        [Header("Canvas 适配 (1920x1080)")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasScaler canvasScaler;

        #endregion

        #region 生命周期

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SetupCanvasScaler();
        }

        void OnEnable()
        {
            SubscribeEventBus();
        }

        void OnDisable()
        {
            UnsubscribeEventBus();
        }

        /// <summary>配置 CanvasScaler 适配 1920x1080 基准分辨率</summary>
        private void SetupCanvasScaler()
        {
            if (canvasScaler == null) canvasScaler = GetComponentInChildren<CanvasScaler>();
            if (canvasScaler == null) return;

            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasScaler.matchWidthOrHeight = 0.5f; // 宽高平衡
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        }

        #endregion

        // ─────────────────────────────────────────────────────────
        // GameEventBus 订阅 — 所有模块间通信通过事件总线
        // ─────────────────────────────────────────────────────────

        #region 事件订阅 (GameEventBus)

        private void SubscribeEventBus()
        {
            var bus = GameEventBus.Instance;
            if (bus == null)
            {
                Debug.LogWarning("[UI] GameEventBus 未找到，延迟订阅...");
                StartCoroutine(DelayedSubscribe());
                return;
            }

            // 无线电事件
            bus.OnRadioReportDelivered   += HandleReportDelivered;
            bus.OnRadioInterference      += HandleReportInterfered;

            // 指挥事件
            bus.OnCommandStatusChanged   += HandleCommandStatusChanged;
            bus.OnCommandDelivered       += HandleCommandDelivered;
            bus.OnCommandLost            += HandleCommandLost;
            bus.OnCommandMisinterpreted  += HandleCommandMisinterpreted;

            // 沙盘 / 情报事件
            bus.OnSandTableUpdated       += HandleSandTableUpdated;
            bus.OnContradictionDetected  += HandleContradiction;

            // 交互事件
            bus.OnInteractionPerformed   += HandleInteraction;

            // 战役事件
            bus.OnCampaignPhaseChanged   += HandlePhaseChanged;
            bus.OnGameTimeUpdated        += HandleTimeUpdate;
            bus.OnGameOutcomeChanged     += HandleCampaignEnded;

            Debug.Log("[UI] GameEventBus 事件已订阅");
        }

        private void UnsubscribeEventBus()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;

            bus.OnRadioReportDelivered   -= HandleReportDelivered;
            bus.OnRadioInterference      -= HandleReportInterfered;
            bus.OnCommandStatusChanged   -= HandleCommandStatusChanged;
            bus.OnCommandDelivered       -= HandleCommandDelivered;
            bus.OnCommandLost            -= HandleCommandLost;
            bus.OnCommandMisinterpreted  -= HandleCommandMisinterpreted;
            bus.OnSandTableUpdated       -= HandleSandTableUpdated;
            bus.OnContradictionDetected  -= HandleContradiction;
            bus.OnInteractionPerformed   -= HandleInteraction;
            bus.OnCampaignPhaseChanged   -= HandlePhaseChanged;
            bus.OnGameTimeUpdated        -= HandleTimeUpdate;
            bus.OnGameOutcomeChanged     -= HandleCampaignEnded;
        }

        /// <summary>等待 GameEventBus 初始化后订阅</summary>
        private IEnumerator DelayedSubscribe()
        {
            float timeout = 5f;
            while (GameEventBus.Instance == null && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            if (GameEventBus.Instance != null)
                SubscribeEventBus();
            else
                Debug.LogError("[UI] GameEventBus 初始化超时，UI 事件订阅失败!");
        }

        #endregion

        // ─────────────────────────────────────────────────────────
        // 事件处理 — 响应 GameEventBus 广播
        // ─────────────────────────────────────────────────────────

        #region 事件处理

        private void HandleReportDelivered(RadioReport report)
        {
            if (radioTextPanel == null || report == null) return;

            var entry = new RadioTextEntry
            {
                Timestamp  = FormatGameTime(report.DeliveredTime ?? Time.time),
                Frequency  = $"频率{report.SourceFrequency}",
                Source     = GetUnitName(report.SourceUnitId),
                Text       = report.FormattedText,
                DisplayTime = Time.time,
                IsInterfered = report.InterferenceLevel > 0.3f,
                Priority   = report.Priority
            };
            radioTextPanel.AddMessage(entry);
        }

        private void HandleReportInterfered(RadioReport report)
        {
            if (radioTextPanel != null)
                radioTextPanel.ShowInterference(report.InterferenceLevel);
            // 联动屏幕噪点
            var noise = screenNoise as ScreenNoiseEffect;
            if (noise != null)
                noise.SetInterference(report.InterferenceLevel);
        }

        private void HandleContradiction(List<RadioReport> reports)
        {
            if (sandTableOverlay != null && reports != null && reports.Count >= 2)
            {
                var pos = reports[0].Content.ReportedPosition;
                if (pos.HasValue)
                    sandTableOverlay.ShowContradictionWarning(pos.Value);
            }
        }

        private void HandleSandTableUpdated(IntelligenceEntry entry)
        {
            if (sandTableOverlay != null && entry != null)
                sandTableOverlay.UpdateMarker(entry);
        }

        private void HandleCommandStatusChanged(RadioCommand cmd, CommandStatus status)
        {
            if (commandStatusPanel != null && cmd != null)
                commandStatusPanel.UpdateStatus(cmd.CommandId, status);
        }

        private void HandleCommandDelivered(RadioCommand cmd)
        {
            if (commandStatusPanel != null)
                commandStatusPanel.UpdateStatus(cmd.CommandId, CommandStatus.Delivered);
        }

        private void HandleCommandLost(RadioCommand cmd)
        {
            if (commandStatusPanel != null)
                commandStatusPanel.UpdateStatus(cmd.CommandId, CommandStatus.Lost);

            if (radioTextPanel != null)
            {
                var entry = new RadioTextEntry
                {
                    Timestamp    = FormatGameTime(Time.time),
                    Frequency    = $"频率{cmd.TargetFrequency}",
                    Source       = "系统",
                    Text         = $"[通信中断] 指令未能送达 {GetUnitName(cmd.TargetUnitId)}...请重试",
                    DisplayTime  = Time.time,
                    IsInterfered = true,
                    Priority     = ReportPriority.Urgent
                };
                radioTextPanel.AddMessage(entry);
            }

            var noise = screenNoise as ScreenNoiseEffect;
            if (noise != null) noise.SetInterference(0.6f);
        }

        /// <summary>
        /// 指令确认回复 — "收到" ≠ "理解" ≠ "执行"
        /// </summary>
        private void HandleCommandAcknowledged(CommandAcknowledgment ack)
        {
            if (commandStatusPanel != null)
                commandStatusPanel.UpdateStatus(ack.CommandId, CommandStatus.Acknowledged);

            if (radioTextPanel != null)
            {
                var entry = new RadioTextEntry
                {
                    Timestamp   = FormatGameTime(Time.time),
                    Frequency   = "—",
                    Source      = GetUnitName(ack.UnitId),
                    Text        = $"\"{ack.ResponseText}\"",
                    DisplayTime = Time.time,
                    Priority    = ReportPriority.Routine
                };
                radioTextPanel.AddMessage(entry);
            }
        }

        private void HandleCommandMisinterpreted(RadioCommand cmd, string misinterpretation)
        {
            if (radioTextPanel != null)
            {
                var entry = new RadioTextEntry
                {
                    Timestamp    = FormatGameTime(Time.time),
                    Frequency    = $"频率{cmd.TargetFrequency}",
                    Source       = "系统",
                    Text         = "[注意] 指令可能存在理解偏差...",
                    DisplayTime  = Time.time,
                    IsInterfered = true,
                    Priority     = ReportPriority.Urgent
                };
                radioTextPanel.AddMessage(entry);
            }
        }

        private void HandleInteraction(InteractionEvent evt)
        {
            if (evt == null) return;

            // 通话键 → 打开指令卡
            if (evt.type == InteractionType.RadioTransmit && orderCardPanel != null)
                orderCardPanel.OpenCard();
        }

        private void HandlePhaseChanged(CampaignPhase phase)
        {
            if (statusNotePanel != null)
                statusNotePanel.UpdatePhase(phase.ToString());
        }

        private void HandleTimeUpdate(float gameTime)
        {
            if (statusNotePanel != null)
                statusNotePanel.UpdateTime(FormatGameTime(gameTime));
        }

        /// <summary>
        /// 战役结束 — 弹出胜负结算窗
        /// </summary>
        private void HandleCampaignEnded(GameOutcome outcome)
        {
            Debug.Log($"[UI] 战役结束: {outcome}");

            // 1) 无线电文本播报
            PushOutcomeRadioMessage(outcome);

            // 2) 状态便签更新
            PushOutcomeStatusNote(outcome);

            // 3) 弹出胜负结算窗 (核心)
            if (gameResultPopup != null)
            {
                var stats = BuildResultStats(outcome);
                gameResultPopup.Show(stats);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────
        // 战役结果 — 构建统计数据 & 广播
        // ─────────────────────────────────────────────────────────

        #region 战役结果

        private void PushOutcomeRadioMessage(GameOutcome outcome)
        {
            if (radioTextPanel == null) return;

            string text = outcome switch
            {
                GameOutcome.PerfectVictory  => "[师部] 完美胜利！三个目标全部占领，伤亡极低。干得漂亮！",
                GameOutcome.PyrrhicVictory  => "[师部] 惨胜。目标已占领，但伤亡惨重...",
                GameOutcome.PartialVictory  => "[师部] 部分完成。部分目标已占领。",
                GameOutcome.Defeat          => "[师部] 任务失败。未能占领目标。",
                GameOutcome.TotalDefeat     => "[师部] 全军覆没...",
                _                           => "[师部] 战役结束。"
            };

            radioTextPanel.AddMessage(new RadioTextEntry
            {
                Timestamp   = FormatGameTime(Time.time),
                Frequency   = "频率5",
                Source      = "师部",
                Text        = text,
                DisplayTime = Time.time,
                Priority    = ReportPriority.Critical
            });
        }

        private void PushOutcomeStatusNote(GameOutcome outcome)
        {
            if (statusNotePanel == null) return;

            statusNotePanel.UpdateStatus(new StatusSummary
            {
                CurrentTime   = FormatGameTime(GameDirector.Instance?.CurrentGameTime ?? 0f),
                CurrentPhase  = outcome.ToString(),
                LatestOrder   = "战役结束"
            });
        }

        /// <summary>
        /// 从各子系统收集统计数据，构建结算数据
        /// </summary>
        private GameResultStats BuildResultStats(GameOutcome outcome)
        {
            var stats = new GameResultStats { Outcome = outcome };

            if (GameDirector.Instance != null)
            {
                stats.TotalPlayTime = GameDirector.Instance.CurrentGameTime;
            }

            if (CommandSystem.Instance != null)
            {
                var pending   = CommandSystem.Instance.GetPendingCommands();
                var history   = CommandSystem.Instance.GetRecentCommands(999);
                stats.CommandsSent     = history.Count;
                stats.CommandsDelivered = history.FindAll(c =>
                    c.Status == CommandStatus.Delivered ||
                    c.Status == CommandStatus.Acknowledged ||
                    c.Status == CommandStatus.Executing ||
                    c.Status == CommandStatus.Completed).Count;
                stats.CommandsLost = history.FindAll(c => c.Status == CommandStatus.Lost).Count;
            }

            if (InformationSystem.Instance != null)
            {
                var reports = InformationSystem.Instance.GetRecentReports(999);
                stats.ReportsReceived = reports.Count;
                if (reports.Count > 0)
                {
                    float totalAcc = 0f;
                    foreach (var r in reports) totalAcc += r.Accuracy.OverallAccuracy;
                    stats.AccuracyAverage = totalAcc / reports.Count;
                }
            }

            return stats;
        }

        #endregion

        #region 辅助方法

        private string FormatGameTime(float time)
        {
            int hours   = Mathf.FloorToInt(time / 3600f) + 6;
            int minutes = Mathf.FloorToInt((time % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        private string GetUnitName(string unitId) => unitId switch
        {
            "company_1"   => "第1连",
            "company_2"   => "第2连",
            "tank_platoon" => "坦克排",
            "naval_gunfire" => "蓝四",
            "division_hq" => "师部",
            _             => unitId
        };

        #endregion
    }

    // ═══════════════════════════════════════════════════════════════
    // 子面板组件
    // ═══════════════════════════════════════════════════════════════

    #region RadioTextPanel — 无线电文本浮动面板

    /// <summary>
    /// 无线电文本浮动面板 (v2 打字机效果)
    ///
    /// 优化:
    /// - 逐字符淡入动画 (per-character alpha fade) 替代硬切 substring
    /// - 可变速率: 字母快、数字/专有名词慢、标点停顿
    /// - 光标闪烁指示器
    /// - 每条消息独立新鲜度追踪 → 四色渐变 (FreshHigh → FreshMed → FreshLow → FreshDead)
    /// - 干扰时随机 RGB 色差偏移
    /// - 自动滚动锁定 + 历史回滚
    /// </summary>
    public class RadioTextPanel : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Transform messageContainer;
        [SerializeField] private GameObject messagePrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Button scrollUpButton;
        [SerializeField] private Button scrollDownButton;
        [SerializeField] private Image interferenceOverlay;

        [Header("打字机设置")]
        [SerializeField] private int maxMessages = 25;
        [SerializeField] private float typewriterSpeed = 0.025f;
        [SerializeField] private float typewriterSpeedCritical = 0.04f;
        [SerializeField] private float punctuationPause = 0.12f;
        [SerializeField] private float numberSlowFactor = 1.5f;   // 数字打字减速
        [SerializeField] private int soundTriggerInterval = 8;
        [SerializeField] private bool usePerCharFade = true;       // 逐字符淡入
        [SerializeField] private float charFadeInDuration = 0.06f; // 单字符淡入时间
        [SerializeField] private string cursorChar = "▌";          // 光标字符
        [SerializeField] private float cursorBlinkRate = 0.5f;     // 光标闪烁频率

        [Header("新鲜度衰减")]
        [SerializeField] private float freshnessDecayPerSecond = 0.03f;
        [SerializeField] private float freshnessUpdateInterval = 0.5f;
        [SerializeField] private float freshHighThreshold = 0.7f;
        [SerializeField] private float freshMedThreshold = 0.4f;
        [SerializeField] private float freshLowThreshold = 0.15f;

        [Header("干扰设置")]
        [SerializeField] private float interferenceFlashDuration = 0.5f;

        // 运行时
        private Queue<RadioTextEntry> messageQueue = new Queue<RadioTextEntry>();
        private List<RadioMessageRuntime> runtimeMessages = new List<RadioMessageRuntime>();
        private bool isTyping;
        private bool autoScrollLocked = true;
        private Coroutine freshnessRoutine;

        void Start()
        {
            scrollUpButton?.onClick.AddListener(() =>
            {
                autoScrollLocked = false;
                if (scrollRect != null)
                    scrollRect.verticalNormalizedPosition = Mathf.Min(1f, scrollRect.verticalNormalizedPosition + 0.15f);
            });
            scrollDownButton?.onClick.AddListener(() =>
            {
                autoScrollLocked = true;
                ScrollToBottom();
            });
            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(v => { autoScrollLocked = v.y < 0.05f; });
            if (interferenceOverlay != null)
                interferenceOverlay.color = new Color(0, 0, 0, 0);
        }

        public void AddMessage(RadioTextEntry entry)
        {
            messageQueue.Enqueue(entry);
            while (runtimeMessages.Count >= maxMessages)
            {
                var oldest = runtimeMessages[0];
                if (oldest.FadeRoutine != null) StopCoroutine(oldest.FadeRoutine);
                if (oldest.GameObject != null) Destroy(oldest.GameObject);
                runtimeMessages.RemoveAt(0);
            }
            if (!isTyping) StartCoroutine(ProcessQueue());
            if (freshnessRoutine == null) freshnessRoutine = StartCoroutine(FreshnessDecayLoop());
        }

        private IEnumerator ProcessQueue()
        {
            isTyping = true;
            while (messageQueue.Count > 0)
            {
                var entry = messageQueue.Dequeue();
                if (messagePrefab == null || messageContainer == null) continue;
                var msgObj = Instantiate(messagePrefab, messageContainer);
                var rt = new RadioMessageRuntime
                {
                    GameObject = msgObj,
                    TextComp = msgObj.GetComponentInChildren<TextMeshProUGUI>(),
                    FullText = BuildFullText(entry),
                    Priority = entry.Priority,
                    SpawnTime = Time.time,
                    Freshness = 1f
                };
                runtimeMessages.Add(rt);
                yield return StartCoroutine(TypewriterEffect(rt, entry));
                if (autoScrollLocked) ScrollToBottom();
            }
            isTyping = false;
        }

        private string BuildFullText(RadioTextEntry entry)
        {
            return $"[{entry.Timestamp}] {entry.Frequency} - {entry.Source} {entry.Text}";
        }

        /// <summary>
        /// v2 打字机效果 — 逐字符淡入 + 可变速率 + 光标闪烁
        /// </summary>
        private IEnumerator TypewriterEffect(RadioMessageRuntime rt, RadioTextEntry entry)
        {
            if (rt.TextComp == null) yield break;

            string fullText = rt.FullText;
            rt.TextComp.color = PriorityToColor(entry.Priority);
            rt.TextComp.maxVisibleCharacters = 0;

            // 使用 TMP maxVisibleCharacters 实现打字效果（性能更好）
            if (usePerCharFade)
            {
                rt.TextComp.text = fullText;
                rt.TextComp.ForceMeshUpdate();

                // 逐字符淡入：每帧设置 maxVisibleCharacters 并附加光标
                float speed = entry.Priority >= ReportPriority.Urgent ? typewriterSpeedCritical : typewriterSpeed;
                float elapsed = 0f;
                int visibleCount = 0;

                while (visibleCount < fullText.Length)
                {
                    float targetTime = GetCharDelay(fullText[visibleCount], speed);
                    elapsed += Time.deltaTime;

                    if (elapsed >= targetTime)
                    {
                        elapsed -= targetTime;
                        visibleCount++;
                        rt.TextComp.maxVisibleCharacters = visibleCount;

                        // 音效触发
                        if (visibleCount % soundTriggerInterval == 0)
                        {
                            // AudioManager.Instance?.PlaySFX("radio_type");
                        }

                        // 干扰丢字
                        if (entry.IsInterfered && visibleCount < fullText.Length && UnityEngine.Random.value < 0.08f)
                        {
                            int glitchFrames = UnityEngine.Random.Range(1, 4);
                            for (int g = 0; g < glitchFrames && visibleCount < fullText.Length; g++)
                            {
                                rt.TextComp.maxVisibleCharacters = visibleCount + 1;
                                visibleCount++;
                                yield return new WaitForSeconds(speed * 0.3f);
                            }
                        }
                    }

                    // 光标闪烁
                    if (visibleCount < fullText.Length && visibleCount > 0)
                    {
                        bool showCursor = Mathf.Sin(Time.time * Mathf.PI * 2f / cursorBlinkRate) > 0f;
                        if (showCursor)
                        {
                            // 通过更新顶点颜色实现光标（不修改文本）
                            ApplyCursorBlink(rt.TextComp, visibleCount);
                        }
                    }

                    yield return null;
                }

                // 确保完全显示
                rt.TextComp.maxVisibleCharacters = fullText.Length;
                ResetCharColors(rt.TextComp);
            }
            else
            {
                // 回退: 原始 substring 方式
                yield return StartCoroutine(TypewriterLegacy(rt, entry));
            }
        }

        /// <summary>根据字符类型返回延迟时间</summary>
        private float GetCharDelay(char c, float baseSpeed)
        {
            // 标点: 长停顿
            if (c == '.' || c == '!' || c == '?' || c == '。' || c == '！')
                return punctuationPause * 2f;
            // 逗号/顿号: 中等停顿
            if (c == ',' || c == ';' || c == '，' || c == '；' || c == '—')
                return punctuationPause;
            // 数字: 稍慢 (重要信息)
            if (char.IsDigit(c))
                return baseSpeed * numberSlowFactor;
            // 中文: 稍慢
            if (c > 0x4E00)
                return baseSpeed * 1.2f;
            // 普通字母: 基准速度
            return baseSpeed;
        }

        /// <summary>TMP 顶点光标闪烁效果</summary>
        private void ApplyCursorBlink(TextMeshProUGUI textComp, int cursorPos)
        {
            var meshInfo = textComp.textInfo;
            if (meshInfo == null || meshInfo.characterCount <= cursorPos) return;

            // 将光标位置字符设为高亮
            int materialIndex = meshInfo.characterInfo[cursorPos].materialReferenceIndex;
            int vertexIndex = meshInfo.characterInfo[cursorPos].vertexIndex;

            Color32 cursorColor = new Color32(255, 255, 255, 200);
            var colors = meshInfo.meshInfo[materialIndex].colors32;
            colors[vertexIndex + 0] = cursorColor;
            colors[vertexIndex + 1] = cursorColor;
            colors[vertexIndex + 2] = cursorColor;
            colors[vertexIndex + 3] = cursorColor;
            textComp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>重置所有字符颜色为正常</summary>
        private void ResetCharColors(TextMeshProUGUI textComp)
        {
            textComp.ForceMeshUpdate();
        }

        /// <summary>回退打字效果（substring）</summary>
        private IEnumerator TypewriterLegacy(RadioMessageRuntime rt, RadioTextEntry entry)
        {
            string fullText = rt.FullText;
            float speed = entry.Priority >= ReportPriority.Urgent ? typewriterSpeedCritical : typewriterSpeed;
            int charCount = 0;

            for (int i = 0; i <= fullText.Length; i++)
            {
                if (entry.IsInterfered && i < fullText.Length && UnityEngine.Random.value < 0.08f)
                {
                    rt.TextComp.text = fullText.Substring(0, i) + "█";
                    yield return new WaitForSeconds(speed * 0.5f);
                    i++;
                    continue;
                }

                rt.TextComp.text = fullText.Substring(0, i);
                charCount++;

                if (charCount % soundTriggerInterval == 0)
                {
                    // AudioManager.Instance?.PlaySFX("radio_type");
                }

                if (i > 0 && i < fullText.Length)
                {
                    char c = fullText[i - 1];
                    if (c == '.' || c == ',' || c == '!' || c == '?' || c == '。' || c == '，')
                    {
                        yield return new WaitForSeconds(punctuationPause);
                        continue;
                    }
                }
                yield return new WaitForSeconds(GetCharDelay(i < fullText.Length ? fullText[i] : ' ', speed));
            }
        }

        /// <summary>
        /// 新鲜度衰减循环 — 每条消息独立追踪，四色渐变
        /// FreshHigh (>0.7) → FreshMed (>0.4) → FreshLow (>0.15) → FreshDead
        /// </summary>
        private IEnumerator FreshnessDecayLoop()
        {
            while (runtimeMessages.Count > 0)
            {
                yield return new WaitForSeconds(freshnessUpdateInterval);

                float dt = freshnessUpdateInterval;
                for (int i = 0; i < runtimeMessages.Count; i++)
                {
                    var rt = runtimeMessages[i];
                    if (rt == null || rt.TextComp == null) continue;

                    // 衰减新鲜度
                    rt.Freshness = Mathf.Max(0f, rt.Freshness - freshnessDecayPerSecond * dt);

                    // 四色渐变
                    Color targetColor = FreshnessToColor(rt.Freshness);
                    Color baseColor = PriorityToColor(rt.Priority);

                    // 基础优先色 × 新鲜度色调 = 最终颜色
                    // 新鲜度高时保持原始优先色，低时渐渐变为灰色
                    float tintAmount = rt.Freshness > freshHighThreshold ? 0f
                                     : rt.Freshness > freshLowThreshold ? 1f - (rt.Freshness - freshLowThreshold) / (freshHighThreshold - freshLowThreshold)
                                     : 1f;

                    rt.TextComp.color = Color.Lerp(baseColor, MilColor.FreshDead, tintAmount * 0.6f);
                }
            }
            freshnessRoutine = null;
        }

        /// <summary>新鲜度 → 颜色映射</summary>
        private Color FreshnessToColor(float freshness)
        {
            if (freshness > freshHighThreshold)
                return MilColor.FreshHigh;
            else if (freshness > freshMedThreshold)
                return Color.Lerp(MilColor.FreshMed, MilColor.FreshHigh, (freshness - freshMedThreshold) / (freshHighThreshold - freshMedThreshold));
            else if (freshness > freshLowThreshold)
                return Color.Lerp(MilColor.FreshLow, MilColor.FreshMed, (freshness - freshLowThreshold) / (freshMedThreshold - freshLowThreshold));
            else
                return Color.Lerp(MilColor.FreshDead, MilColor.FreshLow, freshness / freshLowThreshold);
        }

        public void ShowInterference(float level) => StartCoroutine(InterferenceFlash(level));

        /// <summary>
        /// 干扰闪效 — RGB 色差偏移 + 亮度随机波动
        /// </summary>
        private IEnumerator InterferenceFlash(float level)
        {
            if (interferenceOverlay != null)
            {
                float dur = interferenceFlashDuration * (1f + level);
                float t = 0f;
                while (t < dur)
                {
                    // 随机 RGB 色差偏移（模拟 CRT 干扰）
                    float r = UnityEngine.Random.Range(0f, 0.3f * level);
                    float g = UnityEngine.Random.Range(0.3f, 0.8f);
                    float b = UnityEngine.Random.Range(0f, 0.3f * level);
                    float a = UnityEngine.Random.Range(0.05f, 0.25f) * level;
                    interferenceOverlay.color = new Color(r, g, b, a);
                    t += Time.deltaTime * UnityEngine.Random.Range(0.8f, 1.5f);
                    yield return null;
                }
                interferenceOverlay.color = new Color(0, 0, 0, 0);
            }
            else
            {
                var cg = GetComponent<CanvasGroup>();
                if (cg == null) yield break;
                float dur = interferenceFlashDuration * level;
                float t = 0f;
                while (t < dur)
                {
                    cg.alpha = UnityEngine.Random.Range(0.3f, 1f);
                    t += Time.deltaTime;
                    yield return null;
                }
                cg.alpha = 1f;
            }
        }

        private void ScrollToBottom()
        {
            if (scrollRect == null) return;
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private static Color PriorityToColor(ReportPriority p) => p switch
        {
            ReportPriority.Critical  => MilColor.Critical,
            ReportPriority.Urgent    => MilColor.Urgent,
            ReportPriority.Important => MilColor.Important,
            _                        => MilColor.Routine
        };
    }

    #endregion

    #region OrderCardPanel — 指令卡面板

    /// <summary>
    /// 指令卡面板 (v2 动画版)
    ///
    /// 优化:
    /// - 开卡动画: 从底部滑入 + 弹性缩放 + 淡入
    /// - 关卡动画: 快速滑出 + 缩小
    /// - 发送反馈: 波纹扩散效果 + 指令类型颜色闪烁
    /// - 冷却可视化: 脉冲呼吸 + 颜色渐变
    /// - 目标高亮: 呼吸灯效果
    /// - 6 种指令类型视觉区分
    /// - 1920x1080 适配: 相对位置锚点
    /// </summary>
    public class OrderCardPanel : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private GameObject cardRoot;
        [SerializeField] private RectTransform cardRectTransform;
        [SerializeField] private CanvasGroup cardCanvasGroup;
        [SerializeField] private TMP_Dropdown frequencyDropdown;
        [SerializeField] private TMP_Dropdown typeDropdown;
        [SerializeField] private TMP_InputField contentInput;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button discardButton;

        [Header("类型视觉")]
        [SerializeField] private Image cardBorder;
        [SerializeField] private Image typeIcon;
        [SerializeField] private Sprite[] typeIcons;

        [Header("发送反馈")]
        [SerializeField] private CanvasGroup sendFeedbackGroup;
        [SerializeField] private TextMeshProUGUI sendFeedbackText;
        [SerializeField] private Image sendFeedbackRipple;

        [Header("冷却可视化")]
        [SerializeField] private Image cooldownRing;
        [SerializeField] private TextMeshProUGUI cooldownText;

        [Header("目标选择")]
        [SerializeField] private Image[] targetHighlights;

        [Header("设置")]
        [SerializeField] private string[] frequencyOptions =
        {
            "频率1 - 第1连", "频率2 - 第2连", "频率3 - 坦克排",
            "频率4 - 舰炮支援", "频率5 - 师部"
        };
        [SerializeField] private string[] commandTypeOptions =
        {
            "移动", "攻击", "防御", "撤退", "侦察", "炮击", "状态查询", "补给"
        };
        [SerializeField] private float commandCooldown = 5f;
        [SerializeField] private float feedbackDuration = 1.5f;

        [Header("开/关动画")]
        [SerializeField] private float openSlideDuration = 0.35f;
        [SerializeField] private float closeSlideDuration = 0.2f;
        [SerializeField] private float openSlideOffset = 200f;     // 滑入偏移 (像素)
        [SerializeField] private AnimationCurve openEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve closeEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private bool enableBounce = true;
        [SerializeField] private float bounceScale = 1.05f;

        private bool isOpen;
        private float cooldownTimer;
        private Coroutine feedbackCo;
        private Coroutine animCo;
        private Vector2 cardOriginalPos;

        private static readonly Color[] TypeColors =
        {
            MilColor.CmdMove, MilColor.CmdAttack, MilColor.CmdDefend,
            MilColor.CmdRetreat, MilColor.CmdScout, MilColor.CmdBarrage
        };

        void Start()
        {
            if (cardRoot != null) cardRoot.SetActive(false);

            // 记录原始位置
            if (cardRectTransform != null)
                cardOriginalPos = cardRectTransform.anchoredPosition;

            // 确保有 CanvasGroup 用于淡入
            if (cardCanvasGroup == null && cardRoot != null)
                cardCanvasGroup = cardRoot.GetComponent<CanvasGroup>();

            if (frequencyDropdown != null)
            {
                frequencyDropdown.ClearOptions();
                frequencyDropdown.AddOptions(new List<string>(frequencyOptions));
                frequencyDropdown.onValueChanged.AddListener(SelectTarget);
            }
            if (typeDropdown != null)
            {
                typeDropdown.ClearOptions();
                typeDropdown.AddOptions(new List<string>(commandTypeOptions));
                typeDropdown.onValueChanged.AddListener(UpdateTypeVisual);
            }

            sendButton?.onClick.AddListener(OnSendClicked);
            discardButton?.onClick.AddListener(OnDiscardClicked);

            if (cooldownRing != null) cooldownRing.gameObject.SetActive(false);
            if (sendFeedbackGroup != null) sendFeedbackGroup.alpha = 0f;

            // 波纹初始状态
            if (sendFeedbackRipple != null)
            {
                sendFeedbackRipple.gameObject.SetActive(false);
                sendFeedbackRipple.transform.localScale = Vector3.one;
            }
        }

        void Update()
        {
            if (isOpen && Input.GetKeyDown(KeyCode.Escape)) CloseCard();

            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                UpdateCooldownVisual();
                if (cooldownTimer <= 0f)
                {
                    SetInteractable(true);
                    if (cooldownRing != null) cooldownRing.gameObject.SetActive(false);
                }
            }

            if (isOpen)
            {
                for (int i = 0; i < 5; i++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                        SelectTarget(i);
            }

            // 目标高亮呼吸灯
            if (isOpen)
                AnimateTargetHighlights();
        }

        /// <summary>打开指令卡 — 滑入 + 弹性缩放 + 淡入动画</summary>
        public void OpenCard()
        {
            if (cardRoot == null || isOpen) return;
            isOpen = true;
            cardRoot.SetActive(true);

            FindObjectOfType<CameraController>()?.UnlockMouse();
            if (contentInput != null) contentInput.text = "";
            if (typeDropdown != null) UpdateTypeVisual(typeDropdown.value);

            // 开卡动画
            if (animCo != null) StopCoroutine(animCo);
            animCo = StartCoroutine(AnimateOpen());
        }

        /// <summary>关闭指令卡 — 快速滑出 + 缩小动画</summary>
        public void CloseCard()
        {
            if (!isOpen) return;

            if (animCo != null) StopCoroutine(animCo);
            animCo = StartCoroutine(AnimateClose());
        }

        public void PlaySendFeedback(RadioCommand cmd)
        {
            if (feedbackCo != null) StopCoroutine(feedbackCo);
            feedbackCo = StartCoroutine(SendFeedbackAnim(cmd));
        }

        #region 开/关动画

        private IEnumerator AnimateOpen()
        {
            if (cardRectTransform == null) { yield break; }

            Vector2 targetPos = cardOriginalPos;
            Vector2 startPos = targetPos + Vector2.down * openSlideOffset;
            cardRectTransform.anchoredPosition = startPos;

            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 0f;
                cardCanvasGroup.interactable = false;
                cardCanvasGroup.blocksRaycasts = false;
            }

            float t = 0f;
            while (t < openSlideDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / openSlideDuration);
                float eased = openEase.Evaluate(p);

                // 滑入
                cardRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, eased);

                // 弹性缩放
                if (enableBounce)
                {
                    float scale = p < 0.7f
                        ? Mathf.Lerp(0.85f, bounceScale, eased)
                        : Mathf.Lerp(bounceScale, 1f, (p - 0.7f) / 0.3f);
                    cardRectTransform.localScale = Vector3.one * scale;
                }

                // 淡入
                if (cardCanvasGroup != null)
                    cardCanvasGroup.alpha = Mathf.Clamp01(p * 1.5f);

                yield return null;
            }

            // 最终状态
            cardRectTransform.anchoredPosition = targetPos;
            cardRectTransform.localScale = Vector3.one;
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 1f;
                cardCanvasGroup.interactable = true;
                cardCanvasGroup.blocksRaycasts = true;
            }
        }

        private IEnumerator AnimateClose()
        {
            if (cardRectTransform == null)
            {
                if (cardRoot != null) cardRoot.SetActive(false);
                isOpen = false;
                FindObjectOfType<CameraController>()?.LockMouse();
                yield break;
            }

            Vector2 startPos = cardRectTransform.anchoredPosition;
            Vector2 endPos = cardOriginalPos + Vector2.down * openSlideOffset;
            float startAlpha = cardCanvasGroup != null ? cardCanvasGroup.alpha : 1f;

            float t = 0f;
            while (t < closeSlideDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / closeSlideDuration);
                float eased = closeEase.Evaluate(p);

                cardRectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, eased);
                cardRectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, eased);

                if (cardCanvasGroup != null)
                    cardCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);

                yield return null;
            }

            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.alpha = 0f;
                cardCanvasGroup.interactable = false;
                cardCanvasGroup.blocksRaycasts = false;
            }
            cardRoot.SetActive(false);
            cardRectTransform.localScale = Vector3.one;
            cardRectTransform.anchoredPosition = cardOriginalPos;
            isOpen = false;
            FindObjectOfType<CameraController>()?.LockMouse();
        }

        #endregion

        private void UpdateTypeVisual(int idx)
        {
            idx = Mathf.Clamp(idx, 0, TypeColors.Length - 1);
            Color c = TypeColors[idx];
            if (cardBorder != null) cardBorder.color = c;
            if (typeIcon != null && typeIcons != null && idx < typeIcons.Length && typeIcons[idx] != null)
            {
                typeIcon.sprite = typeIcons[idx];
                typeIcon.color = c;
                typeIcon.gameObject.SetActive(true);
            }
            else if (typeIcon != null) typeIcon.gameObject.SetActive(false);
        }

        private void SelectTarget(int idx)
        {
            if (frequencyDropdown != null) frequencyDropdown.SetValueWithoutNotify(idx);
            if (targetHighlights != null)
            {
                for (int i = 0; i < targetHighlights.Length; i++)
                {
                    if (targetHighlights[i] == null) continue;
                    // 使用 ping-pong 动画高亮
                    if (i == idx)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                        targetHighlights[i].color = Color.Lerp(MilColor.FreshMed, MilColor.FreshHigh, pulse);
                    }
                    else
                    {
                        targetHighlights[i].color = new Color(1, 1, 1, 0.08f);
                    }
                }
            }
        }

        /// <summary>目标高亮呼吸灯动画</summary>
        private void AnimateTargetHighlights()
        {
            if (targetHighlights == null || frequencyDropdown == null) return;
            int selectedIdx = frequencyDropdown.value;

            for (int i = 0; i < targetHighlights.Length; i++)
            {
                if (targetHighlights[i] == null) continue;
                if (i == selectedIdx)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
                    targetHighlights[i].color = Color.Lerp(MilColor.FreshMed, MilColor.FreshHigh, pulse);
                }
            }
        }

        /// <summary>冷却可视化 — 脉冲呼吸 + 颜色渐变</summary>
        private void UpdateCooldownVisual()
        {
            if (cooldownRing == null) return;
            cooldownRing.gameObject.SetActive(true);
            float ratio = cooldownTimer / commandCooldown;

            // 渐变色: 红 → 黄 → 绿
            if (ratio > 0.6f)      cooldownRing.color = MilColor.HPDanger;
            else if (ratio > 0.3f) cooldownRing.color = MilColor.HPWarn;
            else                   cooldownRing.color = MilColor.HPGood;

            // 平滑填充 + 脉冲
            cooldownRing.fillAmount = ratio;
            float pulse = 1f + 0.03f * Mathf.Sin(Time.time * 8f);
            cooldownRing.transform.localScale = Vector3.one * pulse;

            if (cooldownText != null) cooldownText.text = $"{cooldownTimer:F1}s";
        }

        /// <summary>
        /// 发送反馈动画 — 波纹扩散 + 指令类型颜色闪烁 + 缩放弹跳
        /// </summary>
        private IEnumerator SendFeedbackAnim(RadioCommand cmd)
        {
            if (sendFeedbackGroup == null) yield break;

            sendFeedbackGroup.alpha = 1f;
            if (sendFeedbackText != null)
            {
                string name = cmd.TargetUnitId switch
                {
                    "company_1" => "第1连", "company_2" => "第2连",
                    "tank_platoon" => "坦克排", "naval_gunfire" => "蓝四",
                    "division_hq" => "师部", _ => cmd.TargetUnitId
                };
                sendFeedbackText.text = $"📡 指令已发送 → {name}";
                sendFeedbackText.color = MilColor.FreshHigh;
            }

            // 波纹效果
            if (sendFeedbackRipple != null)
            {
                sendFeedbackRipple.gameObject.SetActive(true);
                int typeIdx = (int)cmd.CommandType;
                typeIdx = Mathf.Clamp(typeIdx, 0, TypeColors.Length - 1);
                sendFeedbackRipple.color = TypeColors[typeIdx];
            }

            Vector3 orig = sendFeedbackGroup.transform.localScale;
            float t = 0f;
            while (t < feedbackDuration)
            {
                float p = t / feedbackDuration;

                // 缩放弹跳: 快速弹出 → 慢慢回落
                float scale;
                if (p < 0.2f)
                    scale = 1f + 0.15f * Mathf.Sin(p / 0.2f * Mathf.PI);
                else if (p < 0.4f)
                    scale = 1f + 0.05f * Mathf.Sin((p - 0.2f) / 0.2f * Mathf.PI);
                else
                    scale = 1f;
                sendFeedbackGroup.transform.localScale = orig * scale;

                // 渐隐
                if (p > 0.6f)
                    sendFeedbackGroup.alpha = 1f - (p - 0.6f) / 0.4f;

                // 波纹扩散
                if (sendFeedbackRipple != null && sendFeedbackRipple.gameObject.activeSelf)
                {
                    float rippleP = Mathf.Clamp01(p * 2f);
                    float rippleScale = Mathf.Lerp(0.5f, 3f, rippleP);
                    float rippleAlpha = Mathf.Lerp(0.6f, 0f, rippleP);
                    sendFeedbackRipple.transform.localScale = Vector3.one * rippleScale;
                    Color rc = sendFeedbackRipple.color;
                    rc.a = rippleAlpha;
                    sendFeedbackRipple.color = rc;
                }

                t += Time.deltaTime;
                yield return null;
            }

            sendFeedbackGroup.transform.localScale = orig;
            sendFeedbackGroup.alpha = 0f;
            if (sendFeedbackRipple != null) sendFeedbackRipple.gameObject.SetActive(false);
        }

        private void OnSendClicked()
        {
            if (CommandSystem.Instance == null || cooldownTimer > 0f) return;

            int freq = frequencyDropdown != null ? frequencyDropdown.value + 1 : 1;
            int typeIdx = typeDropdown != null ? typeDropdown.value : 0;
            string content = contentInput != null ? contentInput.text : "";
            if (string.IsNullOrWhiteSpace(content)) return;

            CommandType cmdType = (CommandType)Mathf.Min(typeIdx, Enum.GetValues(typeof(CommandType)).Length - 1);
            string unitId = freq switch
            {
                1 => "company_1", 2 => "company_2", 3 => "tank_platoon",
                4 => "naval_gunfire", 5 => "division_hq", _ => "company_1"
            };

            // 通过 CommandSystem API 发送指令
            CommandSystem.Instance.SendCommand(unitId, freq, cmdType, content);
            cooldownTimer = commandCooldown;
            SetInteractable(false);
            CloseCard();
        }

        private void OnDiscardClicked() => CloseCard();
        private void SetInteractable(bool v) { if (sendButton != null) sendButton.interactable = v; }
    }

    #endregion

    #region StatusNotePanel — 状态便签面板

    /// <summary>
    /// 状态便签面板
    /// 桥 HP 进度条 + 倒计时 + 3 排状态概览 + 脏标记高性能更新
    /// </summary>
    public class StatusNotePanel : MonoBehaviour
    {
        [Header("时间 & 阶段")]
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI phaseText;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private TextMeshProUGUI reinforcementText;

        [Header("桥 HP 进度条")]
        [SerializeField] private Slider bridgeHPSlider;
        [SerializeField] private Image bridgeHPFill;
        [SerializeField] private TextMeshProUGUI bridgeHPText;

        [Header("倒计时")]
        [SerializeField] private TextMeshProUGUI countdownText;

        [Header("排状态 (3 个)")]
        [SerializeField] private PlatoonStatusView platoon1;
        [SerializeField] private PlatoonStatusView platoon2;
        [SerializeField] private PlatoonStatusView platoon3;

        // 脏标记 + 缓存
        private StatusSummary cached;
        private string cachedTime, cachedPhase;
        private bool timeDirty, statusDirty, phaseDirty;
        private float lastFlush;
        [SerializeField] private float updateInterval = 0.5f;

        public void UpdateTime(string time)
        {
            if (cachedTime == time) return;
            cachedTime = time;
            timeDirty = true;
        }

        public void UpdateStatus(StatusSummary summary)
        {
            cached = summary;
            statusDirty = true;
        }

        public void UpdatePhase(string phase)
        {
            if (cachedPhase == phase) return;
            cachedPhase = phase;
            phaseDirty = true;
        }

        public void UpdateReinforcement(ReinforcementInfo info)
        {
            if (reinforcementText == null) return;
            reinforcementText.text = $"下一波登陆：{info.NextWaveTime}\n{info.Contents}";
        }

        void Update()
        {
            // 倒计时实时递减
            if (cached != null && cached.CountdownSeconds > 0f)
            {
                cached.CountdownSeconds -= Time.deltaTime;
                RefreshCountdown(cached.CountdownSeconds);
            }
            // 定期 flush 脏标记
            if (Time.time - lastFlush >= updateInterval)
            {
                lastFlush = Time.time;
                Flush();
            }
        }

        private void Flush()
        {
            if (timeDirty) { timeDirty = false; if (timeText != null) timeText.text = $"当前时间：{cachedTime}"; }
            if (phaseDirty) { phaseDirty = false; if (phaseText != null) phaseText.text = $"当前阶段：{cachedPhase}"; }
            if (statusDirty && cached != null)
            {
                statusDirty = false;
                RefreshBridgeHP(cached.BridgeHP, cached.BridgeMaxHP);
                RefreshCountdown(cached.CountdownSeconds);
                RefreshPlatoons(cached);
                if (statusText != null)
                    statusText.text = $"已登陆部队：{cached.LandedUnits}\n预计敌军强度：{cached.EstimatedEnemy}\n师部最新指令：{cached.LatestOrder}";
            }
        }

        private void RefreshBridgeHP(float cur, float max)
        {
            float r = max > 0f ? Mathf.Clamp01(cur / max) : 0f;
            if (bridgeHPSlider != null) bridgeHPSlider.value = r;
            if (bridgeHPFill != null)
            {
                if (r > 0.5f)       bridgeHPFill.color = Color.Lerp(MilColor.HPWarn, MilColor.HPGood, (r - 0.5f) * 2f);
                else if (r > 0.25f) bridgeHPFill.color = Color.Lerp(MilColor.HPDanger, MilColor.HPWarn, (r - 0.25f) * 4f);
                else                bridgeHPFill.color = Color.Lerp(new Color(0.6f,0.05f,0.05f), MilColor.HPDanger, r * 4f);
            }
            if (bridgeHPText != null) bridgeHPText.text = $"桥梁 HP: {cur:F0}/{max:F0}";
        }

        private void RefreshCountdown(float sec)
        {
            if (countdownText == null) return;
            if (sec <= 0f) { countdownText.text = ""; return; }
            int m = Mathf.FloorToInt(sec / 60f), s = Mathf.FloorToInt(sec % 60f);
            countdownText.text = $"{m:D2}:{s:D2}";
            countdownText.color = sec < 60f
                ? (Mathf.PingPong(Time.time * 3f, 1f) > 0.5f ? MilColor.Critical : MilColor.Urgent)
                : MilColor.Routine;
        }

        private void RefreshPlatoons(StatusSummary sum)
        {
            if (platoon1 != null && sum.Platoon1 != null) platoon1.Bind(sum.Platoon1);
            if (platoon2 != null && sum.Platoon2 != null) platoon2.Bind(sum.Platoon2);
            if (platoon3 != null && sum.Platoon3 != null) platoon3.Bind(sum.Platoon3);
        }
    }

    /// <summary>排级单位状态小部件 — 兵力条 + 状态 + 位置</summary>
    [Serializable]
    public class PlatoonStatusView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI stateText;
        [SerializeField] private TextMeshProUGUI posText;
        [SerializeField] private Slider strengthBar;
        [SerializeField] private Image strengthFill;
        [SerializeField] private Image activeDot;

        public void Bind(PlatoonStatus s)
        {
            if (nameText != null) nameText.text = s.Name;
            if (stateText != null)
            {
                stateText.text = s.State;
                stateText.color = s.State switch
                {
                    "进攻" => MilColor.CmdAttack,
                    "防御" => MilColor.CmdDefend,
                    "撤退" => MilColor.CmdRetreat,
                    _      => MilColor.Routine
                };
            }
            if (posText != null) posText.text = s.Position ?? "";
            if (strengthBar != null) strengthBar.value = s.Strength;
            if (strengthFill != null)
            {
                float v = s.Strength;
                strengthFill.color = v > 0.6f ? MilColor.HPGood : v > 0.3f ? MilColor.HPWarn : MilColor.HPDanger;
            }
            if (activeDot != null) activeDot.color = s.IsActive ? MilColor.FreshHigh : MilColor.FreshDead;
        }
    }

    #endregion

    #region GameResultPopup — 胜负结算弹窗

    /// <summary>
    /// 战役结算弹窗 — 胜利 / 失败结果展示 (v2 动画增强)
    ///
    /// 功能:
    /// - 胜负标题 + 战绩评分 (S/A/B/C/D)
    /// - 详细统计数据 (目标占领率 / 伤亡率 / 指令送达率 / 情报准确度)
    /// - 入场动画 (弹性缩放 + 渐显 + 背景暗化)
    /// - 统计条目逐行延迟显示
    /// - 评级字母大号浮出动画
    /// - 点击任意位置或按 ESC 关闭
    /// - 可选重新开始按钮
    /// </summary>
    public class GameResultPopup : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform popupRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI hintText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Image backgroundDim;
        [SerializeField] private Image outcomeIcon;

        [Header("标题颜色 (按结局)")]
        [SerializeField] private Color victoryColor  = new Color(0.95f, 0.85f, 0.20f);
        [SerializeField] private Color pyrrhicColor  = new Color(0.95f, 0.55f, 0.10f);
        [SerializeField] private Color partialColor  = new Color(0.70f, 0.75f, 0.80f);
        [SerializeField] private Color defeatColor   = new Color(0.85f, 0.20f, 0.15f);
        [SerializeField] private Color totalDefeatColor = new Color(0.50f, 0.10f, 0.10f);

        [Header("动画")]
        [SerializeField] private float fadeInDuration  = 0.4f;
        [SerializeField] private float scaleInDuration = 0.35f;
        [SerializeField] private float statsRevealDelay = 0.08f;   // 统计条目逐行延迟
        [SerializeField] private float gradeRevealDelay = 0.3f;    // 评级浮出延迟
        [SerializeField] private AnimationCurve scaleInEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private float gradeScalePunch = 1.3f;     // 评级弹出缩放

        private bool isVisible;
        private Coroutine animCo;

        void Start()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }

            closeButton?.onClick.AddListener(Hide);
            restartButton?.onClick.AddListener(OnRestartClicked);

            if (popupRoot != null) popupRoot.localScale = Vector3.zero;
        }

        void Update()
        {
            if (isVisible && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        /// <summary>弹出结算窗</summary>
        public void Show(GameResultStats stats)
        {
            if (stats == null) return;
            isVisible = true;

            // 填充数据
            PopulateTitle(stats.Outcome);
            PopulateGrade(stats);
            PopulateStats(stats);
            PopulateHint(stats.Outcome);

            // 播放入场动画
            if (animCo != null) StopCoroutine(animCo);
            animCo = StartCoroutine(AnimateIn());
        }

        /// <summary>关闭结算窗</summary>
        public void Hide()
        {
            isVisible = false;
            if (animCo != null) StopCoroutine(animCo);
            animCo = StartCoroutine(AnimateOut());
        }

        #region 数据填充

        private void PopulateTitle(GameOutcome outcome)
        {
            if (titleText == null) return;

            (string text, Color color) = outcome switch
            {
                GameOutcome.PerfectVictory => ("★ 完美胜利 ★",       victoryColor),
                GameOutcome.PyrrhicVictory => ("△ 惨胜 △",           pyrrhicColor),
                GameOutcome.PartialVictory => ("— 部分完成 —",       partialColor),
                GameOutcome.Defeat         => ("✗ 任务失败",         defeatColor),
                GameOutcome.TotalDefeat    => ("☠ 全军覆没",         totalDefeatColor),
                _                          => ("战役结束",           Color.white)
            };

            titleText.text = text;
            titleText.color = color;
        }

        private void PopulateGrade(GameResultStats stats)
        {
            if (gradeText == null) return;

            float score = CalculateScore(stats);
            string grade;
            Color color;

            if (score >= 95f)      { grade = "S";  color = victoryColor; }
            else if (score >= 85f) { grade = "A";  color = MilColor.FreshHigh; }
            else if (score >= 70f) { grade = "B";  color = MilColor.Important; }
            else if (score >= 50f) { grade = "C";  color = MilColor.Urgent; }
            else                   { grade = "D";  color = MilColor.Critical; }

            gradeText.text = $"评级: {grade}  ({score:F0}分)";
            gradeText.color = color;
        }

        private void PopulateStats(GameResultStats stats)
        {
            if (statsText == null) return;

            var sb = new System.Text.StringBuilder();

            // 目标占领
            float objRate = stats.TotalObjectives > 0
                ? (float)stats.ObjectivesCaptured / stats.TotalObjectives : 0f;
            sb.AppendLine($"目标占领:  {stats.ObjectivesCaptured}/{stats.TotalObjectives}  ({objRate:P0})");

            // 伤亡率
            string casualtyLabel = stats.CasualtyRate switch
            {
                < 0.2f => "极低",
                < 0.4f => "中等",
                < 0.6f => "较高",
                _      => "惨重"
            };
            sb.AppendLine($"伤亡率:    {stats.CasualtyRate:P0} ({casualtyLabel})");

            // 指令统计
            float cmdRate = stats.CommandsSent > 0
                ? (float)stats.CommandsDelivered / stats.CommandsSent : 0f;
            sb.AppendLine($"指令送达:  {stats.CommandsDelivered}/{stats.CommandsSent}  ({cmdRate:P0})");
            sb.AppendLine($"指令丢失:  {stats.CommandsLost}");

            // 情报统计
            sb.AppendLine($"情报接收:  {stats.ReportsReceived} 条");
            sb.AppendLine($"情报准确度: {stats.AccuracyAverage:P0}");

            // 游戏时间
            float totalMin = stats.TotalPlayTime / 60f;
            int mins = Mathf.FloorToInt(totalMin);
            int secs = Mathf.FloorToInt((totalMin - mins) * 60f);
            sb.AppendLine($"作战时长:  {mins}分{secs:D2}秒");

            statsText.text = sb.ToString();
        }

        private void PopulateHint(GameOutcome outcome)
        {
            if (hintText == null) return;

            hintText.text = outcome switch
            {
                GameOutcome.PerfectVictory => "师部嘉奖：精确的指挥挽救了无数生命。",
                GameOutcome.PyrrhicVictory => "代价惨重，但任务完成了。重新评估战术或许能减少伤亡。",
                GameOutcome.PartialVictory => "未能完成全部目标。下次注意兵力分配和时机把握。",
                GameOutcome.Defeat         => "情报不足和指挥延误是主要原因。再来一次！",
                GameOutcome.TotalDefeat    => "通信完全中断，前线失去了指挥。需要更好的信息管理。",
                _                          => "按 ESC 关闭"
            };
        }

        /// <summary>
        /// 综合评分 (0-100)
        /// 权重: 目标 40% + 伤亡 30% + 指令送达 15% + 情报准确度 15%
        /// </summary>
        private float CalculateScore(GameResultStats stats)
        {
            float objScore = stats.TotalObjectives > 0
                ? (float)stats.ObjectivesCaptured / stats.TotalObjectives * 100f : 0f;
            float casualtyScore = (1f - stats.CasualtyRate) * 100f;
            float cmdScore = stats.CommandsSent > 0
                ? (float)stats.CommandsDelivered / stats.CommandsSent * 100f : 50f;
            float intelScore = stats.AccuracyAverage * 100f;

            return objScore * 0.4f + casualtyScore * 0.3f + cmdScore * 0.15f + intelScore * 0.15f;
        }

        #endregion

        #region 动画

        /// <summary>入场动画: 背景暗化 + 弹性缩放 + 渐显 + 逐行统计</summary>
        private IEnumerator AnimateIn()
        {
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            // 背景暗化
            if (backgroundDim != null)
            {
                backgroundDim.gameObject.SetActive(true);
                backgroundDim.color = new Color(0, 0, 0, 0);
            }

            float t = 0f;
            float maxDur = Mathf.Max(fadeInDuration, scaleInDuration);

            Vector3 targetScale = Vector3.one;
            if (popupRoot != null) popupRoot.localScale = Vector3.zero;

            // 隐藏子元素用于逐行显示
            if (statsText != null) statsText.alpha = 0f;
            if (gradeText != null) gradeText.alpha = 0f;
            if (hintText != null) hintText.alpha = 0f;

            while (t < maxDur)
            {
                t += Time.deltaTime;

                // Fade in
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Clamp01(t / fadeInDuration);

                // Scale in (弹性)
                if (popupRoot != null)
                {
                    float sp = Mathf.Clamp01(t / scaleInDuration);
                    float eased = scaleInEase.Evaluate(sp);
                    // 弹性过冲
                    float overshoot = 1f + 0.06f * Mathf.Sin(sp * Mathf.PI);
                    popupRoot.localScale = Vector3.Lerp(Vector3.zero, targetScale, eased) * overshoot;
                }

                // 背景暗化
                if (backgroundDim != null)
                    backgroundDim.color = new Color(0, 0, 0, Mathf.Clamp01(t / fadeInDuration) * 0.7f);

                yield return null;
            }

            // 确保最终状态
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (popupRoot != null) popupRoot.localScale = targetScale;

            // 评级浮出动画 (弹跳)
            if (gradeText != null)
            {
                gradeText.alpha = 1f;
                yield return StartCoroutine(PunchScale(gradeText.rectTransform, gradeScalePunch, 0.4f));
            }

            // 统计逐行显示
            if (statsText != null)
            {
                var lines = statsText.text.Split('\n');
                statsText.text = "";
                statsText.alpha = 1f;

                for (int i = 0; i < lines.Length; i++)
                {
                    statsText.text += (i > 0 ? "\n" : "") + lines[i];
                    yield return new WaitForSeconds(statsRevealDelay);
                }
            }

            // 提示文字
            if (hintText != null)
            {
                hintText.alpha = 1f;
                yield return StartCoroutine(FadeCanvasGroup(hintText.GetComponent<CanvasGroup>(), 0f, 1f, 0.3f));
            }
        }

        private IEnumerator AnimateOut()
        {
            float t = 0f;
            float dur = 0.25f;

            float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
            Vector3 startScale = popupRoot != null ? popupRoot.localScale : Vector3.one;

            while (t < dur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);

                if (canvasGroup != null) canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
                if (popupRoot != null) popupRoot.localScale = Vector3.Lerp(startScale, Vector3.zero, p);
                if (backgroundDim != null)
                    backgroundDim.color = new Color(0, 0, 0, Mathf.Lerp(0.7f, 0f, p));

                yield return null;
            }

            if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.interactable = false; canvasGroup.blocksRaycasts = false; }
            if (popupRoot != null) popupRoot.localScale = Vector3.zero;
            if (backgroundDim != null) backgroundDim.gameObject.SetActive(false);
        }

        /// <summary>缩放弹跳效果</summary>
        private IEnumerator PunchScale(RectTransform target, float punchScale, float duration)
        {
            if (target == null) yield break;
            Vector3 orig = target.localScale;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float scale = Mathf.Lerp(punchScale, 1f, p);
                // 轻微弹性衰减
                scale += 0.05f * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
                target.localScale = orig * scale;
                yield return null;
            }
            target.localScale = orig;
        }

        /// <summary>CanvasGroup 淡入</summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        #endregion

        private void OnRestartClicked()
        {
            Hide();
            // 重载场景 (由 GameDirector 处理)
            Debug.Log("[UI] 请求重新开始战役...");
            if (GameDirector.Instance != null)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
                );
            }
        }
    }

    #endregion

    #region SandTableOverlay — 沙盘标注叠加层

    /// <summary>
    /// 沙盘标注叠加层 — 参谋在沙盘上的标注 (有延迟和误差)
    /// </summary>
    public class SandTableOverlay : MonoBehaviour
    {
        [Header("设置")]
        [SerializeField] private float staffUpdateDelayMin = 5f;
        [SerializeField] private float staffUpdateDelayMax = 30f;

        [Header("新鲜度颜色")]
        [SerializeField] private Color highConfidenceColor = Color.green;
        [SerializeField] private Color midConfidenceColor = Color.yellow;
        [SerializeField] private Color lowConfidenceColor = new Color(1f, 0.5f, 0f);
        [SerializeField] private Color expiredColor = Color.gray;

        [Header("UI 引用")]
        [SerializeField] private Transform markerContainer;

        private Dictionary<string, GameObject> markers = new Dictionary<string, GameObject>();

        public void UpdateMarker(IntelligenceEntry entry) => StartCoroutine(StaffUpdateMarker(entry));

        private IEnumerator StaffUpdateMarker(IntelligenceEntry entry)
        {
            float delay = UnityEngine.Random.Range(staffUpdateDelayMin, staffUpdateDelayMax);
            yield return new WaitForSeconds(delay);

            // 参谋可能标错位置 (±50m)
            float error = UnityEngine.Random.Range(-50f, 50f);
            Vector3 adjustedPos = entry.Position + new Vector3(error, 0f, error);

            if (markers.TryGetValue(entry.EntryId, out var existingMarker))
            {
                existingMarker.transform.position = adjustedPos;
                UpdateMarkerColor(existingMarker, entry.Freshness);
            }
            else
            {
                string markerName = entry.IsEnemy
                    ? $"EnemyMarker_{entry.UnitId}"
                    : $"FriendlyMarker_{entry.UnitId}";
                var marker = new GameObject(markerName);
                marker.transform.SetParent(markerContainer);
                marker.transform.position = adjustedPos;

                if (entry.IsEnemy && entry.Confidence < 0.5f)
                {
                    var uncertain = new GameObject("Uncertain");
                    uncertain.transform.SetParent(marker.transform);
                    uncertain.transform.localPosition = Vector3.up * 0.2f;
                }

                markers[entry.EntryId] = marker;
                UpdateMarkerColor(marker, entry.Freshness);
            }

            Debug.Log($"[SandTable] 参谋标注: {entry.UnitId} at {adjustedPos} (置信度:{entry.Confidence:F2})");
        }

        private void UpdateMarkerColor(GameObject marker, float freshness)
        {
            var renderer = marker.GetComponent<Renderer>();
            if (renderer == null) return;

            Color color;
            if (freshness > 0.7f)      color = highConfidenceColor;
            else if (freshness > 0.4f) color = midConfidenceColor;
            else if (freshness > 0.2f) color = lowConfidenceColor;
            else                       color = expiredColor;

            renderer.material.color = color;
        }

        public void ShowContradictionWarning(Vector3 position) => StartCoroutine(FlashContradictionWarning(position));

        private IEnumerator FlashContradictionWarning(Vector3 position)
        {
            var warning = new GameObject("ContradictionWarning");
            warning.transform.SetParent(markerContainer);
            warning.transform.position = position;

            var renderer = warning.AddComponent<MeshRenderer>();
            var filter = warning.AddComponent<MeshFilter>();
            filter.mesh = CreateWarningMesh();

            float flashDuration = 3f;
            float elapsed = 0f;
            bool visible = true;

            while (elapsed < flashDuration)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                    renderer.material.color = visible ? Color.red : Color.clear;
                }
                visible = !visible;
                elapsed += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }

            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.material.color = new Color(1f, 0.2f, 0.2f, 0.8f);
            }

            Debug.Log($"[SandTable] 矛盾警告已标记: {position}");
        }

        private Mesh CreateWarningMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0f, 0.3f, 0f),
                new Vector3(-0.1f, 0f, 0f),
                new Vector3(0.1f, 0f, 0f)
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.normals = new Vector3[] { Vector3.back, Vector3.back, Vector3.back };
            return mesh;
        }

        public void RefreshAllMarkers()
        {
            if (InformationSystem.Instance == null) return;
            foreach (var entry in InformationSystem.Instance.GetIntelligenceBoard())
            {
                if (markers.TryGetValue(entry.EntryId, out var marker))
                    UpdateMarkerColor(marker, entry.Freshness);
            }
        }
    }

    #endregion

    #region CommandStatusPanel — 指令状态追踪面板

    /// <summary>
    /// 指令状态追踪面板 — 显示已发送指令的生命周期状态
    /// </summary>
    public class CommandStatusPanel : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Transform commandContainer;
        [SerializeField] private GameObject commandEntryPrefab;

        private Dictionary<string, GameObject> commandEntries = new Dictionary<string, GameObject>();

        public void AddCommand(RadioCommand cmd)
        {
            if (commandEntryPrefab == null || commandContainer == null) return;
            var entryObj = Instantiate(commandEntryPrefab, commandContainer);
            commandEntries[cmd.CommandId] = entryObj;
            UpdateEntryDisplay(cmd);
        }

        public void UpdateStatus(string commandId, CommandStatus status)
        {
            if (!commandEntries.TryGetValue(commandId, out var entryObj)) return;

            var textComp = entryObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp == null) return;

            string statusText = status switch
            {
                CommandStatus.Sending      => "📡 发送中...",
                CommandStatus.InTransit    => "⏳ 传输中...",
                CommandStatus.Delivered    => "✅ 已送达",
                CommandStatus.Acknowledged => "📋 已确认",
                CommandStatus.Executing    => "⚙️ 执行中",
                CommandStatus.Completed    => "✔️ 已完成",
                CommandStatus.Lost         => "❌ 通信中断",
                CommandStatus.Failed       => "⚠️ 失败",
                _                          => status.ToString()
            };

            Color statusColor = status switch
            {
                CommandStatus.Sending      => new Color(0.5f, 0.55f, 0.5f),
                CommandStatus.InTransit    => MilColor.Important,
                CommandStatus.Delivered    => MilColor.FreshHigh,
                CommandStatus.Acknowledged => MilColor.FreshHigh,
                CommandStatus.Lost or CommandStatus.Failed => MilColor.Critical,
                _                          => MilColor.Routine
            };
            textComp.color = statusColor;

            string currentText = textComp.text;
            int statusIdx = currentText.IndexOf('|');
            if (statusIdx > 0)
                textComp.text = currentText.Substring(0, statusIdx + 1) + " " + statusText;
        }

        private void UpdateEntryDisplay(RadioCommand cmd)
        {
            if (!commandEntries.TryGetValue(cmd.CommandId, out var entryObj)) return;

            var textComp = entryObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComp == null) return;

            string unitName = cmd.TargetUnitId switch
            {
                "company_1" => "第1连", "company_2" => "第2连",
                "tank_platoon" => "坦克排", "naval_gunfire" => "蓝四",
                "division_hq" => "师部", _ => cmd.TargetUnitId
            };

            textComp.text = $"频率{cmd.TargetFrequency} → {unitName}: {cmd.Content} | 发送中...";
            textComp.color = new Color(0.5f, 0.55f, 0.5f);
        }
    }

    #endregion

    #region ScreenNoiseEffect — 屏幕噪点后处理

    /// <summary>
    /// CRT / 无线电显示器噪点效果 (v2 增强版)
    ///
    /// 优化:
    /// - 色差偏移 (Chromatic Aberration) — 干扰时 RGB 分离
    /// - VHS 追踪线 — 周期性水平干扰线
    /// - 胶片颗粒叠加
    /// - 干扰等级联动: 噪点强度随干扰级别动态变化
    /// - 需要配套 Shader: ScreenNoise.shader
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ScreenNoiseEffect : MonoBehaviour
    {
        [Header("基础效果")]
        [SerializeField] private Material noiseMaterial;
        [SerializeField] private float baseNoiseIntensity = 0.08f;
        [SerializeField] private float scanlineIntensity = 0.04f;
        [SerializeField] private float vignetteIntensity = 0.35f;

        [Header("闪烁")]
        [SerializeField] private bool enableFlicker = true;
        [SerializeField] private float flickerSpeed = 2f;
        [SerializeField] private float flickerAmplitude = 0.02f;

        [Header("干扰联动")]
        [SerializeField] private float interferenceBoost = 0.15f;
        [SerializeField] private float interferenceChromaticShift = 0.015f;

        [Header("VHS 追踪线")]
        [SerializeField] private bool enableTrackingLines = true;
        [SerializeField] private float trackingLineSpeed = 0.5f;
        [SerializeField] private float trackingLineIntensity = 0.15f;
        [SerializeField] private float trackingLineFrequency = 0.3f; // 出现频率 (0-1)

        [Header("胶片颗粒")]
        [SerializeField] private bool enableFilmGrain = true;
        [SerializeField] private float filmGrainIntensity = 0.03f;

        private float currentInterference;
        private Material runtimeMat;

        void Start()
        {
            // 创建运行时材质实例，避免修改原始材质
            if (noiseMaterial != null)
                runtimeMat = new Material(noiseMaterial);
        }

        void OnDestroy()
        {
            if (runtimeMat != null)
                DestroyImmediate(runtimeMat);
        }

        public void SetInterference(float level)
        {
            currentInterference = Mathf.Clamp01(level);
            StartCoroutine(FadeInterference());
        }

        private IEnumerator FadeInterference()
        {
            yield return new WaitForSeconds(0.8f);
            while (currentInterference > 0f)
            {
                currentInterference = Mathf.Max(0f, currentInterference - Time.deltaTime * 0.5f);
                yield return null;
            }
        }

        void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            if (runtimeMat == null)
            {
                Graphics.Blit(src, dest);
                return;
            }

            // 基础噪点 + 干扰提升
            float effective = baseNoiseIntensity + currentInterference * interferenceBoost;

            // 闪烁
            if (enableFlicker)
                effective += Mathf.PerlinNoise(Time.time * flickerSpeed, 0f) * flickerAmplitude;

            runtimeMat.SetFloat("_NoiseIntensity", effective);
            runtimeMat.SetFloat("_ScanlineIntensity", scanlineIntensity);
            runtimeMat.SetFloat("_VignetteIntensity", vignetteIntensity);

            // 色差偏移 (干扰时 RGB 分离)
            float chromatic = currentInterference * interferenceChromaticShift;
            runtimeMat.SetFloat("_ChromaticAberration", chromatic);

            // VHS 追踪线
            if (enableTrackingLines)
            {
                runtimeMat.SetFloat("_TrackingLineActive",
                    Mathf.PerlinNoise(Time.time * trackingLineSpeed, 42f) < trackingLineFrequency ? 1f : 0f);
                runtimeMat.SetFloat("_TrackingLineIntensity", trackingLineIntensity * (0.5f + currentInterference * 0.5f));
                runtimeMat.SetFloat("_TrackingLineOffset", Mathf.Repeat(Time.time * 0.3f, 1f));
            }
            else
            {
                runtimeMat.SetFloat("_TrackingLineActive", 0f);
            }

            // 胶片颗粒
            runtimeMat.SetFloat("_FilmGrainIntensity", enableFilmGrain ? filmGrainIntensity : 0f);

            Graphics.Blit(src, dest, runtimeMat);
        }
    }

    #endregion
}
