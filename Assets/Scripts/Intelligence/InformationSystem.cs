// InformationSystem.cs — 无线电情报系统 (增强版)
// 替代旧的实时数据同步模式
// 前线汇报经过延迟/准确度衰减/干扰后才送达玩家
// 这是本作最核心的差异化系统 — 信息不对称 = 核心玩法
//
// 功能：
// - 自动生成前线汇报文本（含准确度衰减）
// - 打字机逐字显示效果（Unity协程）
// - 情报可信度系统（可能有误差/遗漏/误判）
// - 多频率无线电支持（1-5频率）
// - 与AIDirector协作生成随机事件情报
// - GameEventBus广播情报事件
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.AI;
using UnityEngine.UI;

namespace SWO1.Intelligence
{
    #region 数据模型

    /// <summary>
    /// 无线电汇报 — 从前线传回的战况汇报
    /// 包含延迟、准确度、干扰等信息质量因素
    /// </summary>
    [Serializable]
    public class RadioReport
    {
        public string ReportId;
        public string SourceUnitId;          // 来源部队
        public int SourceFrequency;          // 来源频率
        public float ActualEventTime;        // 实际事件发生时间
        public float GeneratedTime;          // 汇报生成时间
        public float? DeliveredTime;         // 实际送达时间
        public float DeliveryDelay;          // 延迟时长
        public ReportContent Content;        // 汇报内容
        public ReportAccuracy Accuracy;      // 准确度
        public float InterferenceLevel;      // 干扰等级 0-1
        public string FormattedText;         // 格式化显示文本
        public ReportPriority Priority;      // 优先级

        public RadioReport()
        {
            ReportId = Guid.NewGuid().ToString();
            Content = new ReportContent();
            Accuracy = new ReportAccuracy();
        }
    }

    /// <summary>
    /// 汇报内容 — 部分字段可能为空（前线未汇报）
    /// </summary>
    [Serializable]
    public class ReportContent
    {
        public string Situation;            // 态势描述文本
        public int? TroopCount;             // 兵力（null = 未汇报）
        public float? MoraleEstimate;       // 士气估计（模糊值）
        public string MoraleDescription;    // 士气描述文本
        public Vector3? ReportedPosition;   // 汇报的位置（可能有偏移）
        public string EnemyType;            // 敌军类型（可能误判）
        public int? EnemyCount;             // 敌军数量（可能错误）
        public string Request;              // 请求（如有）
        public float? AmmoLevel;            // 弹药水平
        public string AmmoDescription;      // 弹药描述文本
        public bool HasCasualties;          // 是否有伤亡
        public string CasualtyDescription;  // 伤亡描述（模糊）
    }

    /// <summary>
    /// 汇报准确度评估
    /// </summary>
    [Serializable]
    public class ReportAccuracy
    {
        public float OverallAccuracy = 1f;          // 总体准确度 0-1
        public bool HasOmissions = false;            // 是否有遗漏
        public bool HasErrors = false;               // 是否有错误
        public bool IsContradictory = false;         // 是否与其他汇报矛盾
        public List<string> OmittedDetails = new List<string>();
        public List<string> ErrorDetails = new List<string>();
    }

    /// <summary>
    /// 汇报优先级
    /// </summary>
    public enum ReportPriority
    {
        Routine,        // 常规汇报
        Important,      // 重要更新
        Urgent,         // 紧急（交火/伤亡）
        Critical        // 危急（请求支援/撤退）
    }

    /// <summary>
    /// 情报条目 — 沙盘上标注的信息
    /// </summary>
    [Serializable]
    public class IntelligenceEntry
    {
        public string EntryId;
        public string UnitId;               // 相关部队
        public Vector3 Position;            // 标注位置
        public string UnitType;             // 部队类型
        public float Confidence;            // 可信度 0-1
        public float Freshness;             // 新鲜度 1.0 → 0.0
        public float LastUpdateTime;        // 最后更新时间
        public bool IsConfirmed;            // 是否已确认
        public bool IsEnemy;                // 是否为敌军信息
        public IntelligenceSource Source;   // 信息来源
    }

    /// <summary>
    /// 情报来源类型
    /// </summary>
    public enum IntelligenceSource
    {
        RadioReport,    // 无线电汇报
        Recon,          // 侦察兵
        ArtilleryObs,   // 炮兵观测
        DivisionHQ,     // 师部通报
        Intercepted     // 无线电截获
    }

    /// <summary>
    /// 前线战场事件 — 信息系统从模拟系统接收的原始数据
    /// </summary>
    [Serializable]
    public class BattleEvent
    {
        public string EventId;
        public string UnitId;               // 涉及部队
        public float EventTime;             // 事件发生时间
        public BattleEventType Type;        // 事件类型
        public Vector3 Position;            // 真实位置
        public int ActualTroopCount;        // 真实兵力
        public float ActualMorale;          // 真实士气
        public float ActualAmmo;            // 真实弹药
        public string EnemyType;            // 真实敌军类型
        public int ActualEnemyCount;        // 真实敌军数量
        public string Description;          // 事件描述
    }

    public enum BattleEventType
    {
        Landing,            // 登陆
        Engagement,         // 交火
        Movement,           // 移动
        Casualties,         // 伤亡
        ObjectiveCapture,   // 占领目标
        Reinforcement,      // 增援到达
        SupplyReceived,     // 补给到达
        MoraleChange,       // 士气变化
        CommunicationLost,  // 通讯中断
        RequestSupport      // 请求支援
    }

    /// <summary>
    /// 打字机显示事件 — 每个字符逐字输出时触发
    /// 供 UI 系统订阅以实现实时打字效果
    /// </summary>
    [Serializable]
    public class TypewriterEvent
    {
        public string ReportId;             // 关联汇报ID
        public string CurrentText;          // 当前已显示的文本
        public string FullText;             // 完整文本
        public char CurrentChar;            // 当前输出的字符
        public int CharIndex;               // 字符索引
        public float Progress;              // 进度 0-1
        public bool IsComplete;             // 是否已完成
        public int Frequency;               // 来源频率（用于 UI 频道标识）
    }

    /// <summary>
    /// AIDirector 情报事件 — 由 AIDirector 生成的随机事件推送
    /// </summary>
    [Serializable]
    public class AIDirectorIntelEvent
    {
        public string EventId;
        public string EventType;            // weather/comm_jam/reinforcement/supply/morale/enemy
        public string Description;          // 事件描述文本
        public float Duration;              // 持续时间（秒）
        public int AffectedFrequency;       // 影响的频率（0=全部）
        public float Severity;              // 严重程度 0-1
    }

    #endregion

    /// <summary>
    /// 信息系统 — 核心创新模块
    /// 
    /// 设计理念：
    /// - 信息不是"给你的"，是"你争取到的"
    /// - 每条汇报都经过准确度衰减、延迟、干扰
    /// - 玩家必须在不完整/错误/矛盾的信息下做决策
    /// 
    /// 数据流：
    /// 真实战场事件 → GenerateReport() → ApplyAccuracyDecay() 
    /// → ApplyDelay() → ApplyInterference() → 格式化 → 玩家接收
    /// </summary>
    public class InformationSystem : MonoBehaviour
    {
        public static InformationSystem Instance { get; private set; }

        [Header("延迟参数")]
        [Tooltip("汇报最小延迟（秒）")]
        [SerializeField] private float reportDelayMin = 30f;

        [Tooltip("汇报最大延迟（秒）")]
        [SerializeField] private float reportDelayMax = 300f;

        [Header("准确度参数")]
        [Tooltip("各士气等级的基础准确度 [高昂, 正常, 动摇, 崩溃]")]
        [SerializeField] private float[] accuracyByMorale = { 0.90f, 0.75f, 0.55f, 0.30f };

        [Tooltip("交战中的准确度衰减系数")]
        [SerializeField] private float combatAccuracyPenalty = 0.7f;

        [Tooltip("位置偏移范围（米）")]
        [SerializeField] private float positionErrorRange = 50f;

        [Header("干扰参数")]
        [Tooltip("各难度的干扰概率 [Easy, Normal, Hard]")]
        [SerializeField] private float[] interferenceChance = { 0.05f, 0.15f, 0.35f };

        [Header("新鲜度参数")]
        [Tooltip("情报完全过期时间（秒）")]
        [SerializeField] private float freshnessDecayTime = 300f;

        [Header("参谋延迟")]
        [Tooltip("参谋更新沙盘的延迟范围（秒）")]
        [SerializeField] private Vector2 staffUpdateDelayRange = new Vector2(5f, 30f);

        [Header("打字机效果")]
        [Tooltip("每个字符的显示间隔（秒）")]
        [SerializeField] private float typewriterCharDelay = 0.05f;

        [Tooltip("标点符号额外停顿（秒）")]
        [SerializeField] private float typewriterPunctuationDelay = 0.15f;

        [Tooltip("句号/省略号长停顿（秒）")]
        [SerializeField] private float typewriterSentenceDelay = 0.3f;

        [Tooltip("静电噪音字符的延迟（秒）")]
        [SerializeField] private float typewriterStaticDelay = 0.02f;

        [Header("AIDirector 集成")]
        [Tooltip("自动接收 AIDirector 随机事件")]
        [SerializeField] private bool autoSubscribeAIDirector = true;

        [Tooltip("AIDirector 事件转情报的延迟范围（秒）")]
        [SerializeField] private Vector2 aiDirectorIntelDelayRange = new Vector2(10f, 60f);

        // === 状态 ===
        private Queue<RadioReport> pendingReports = new Queue<RadioReport>();
        private List<IntelligenceEntry> intelligenceBoard = new List<IntelligenceEntry>();
        private List<RadioReport> reportHistory = new List<RadioReport>();

        // === 事件 ===
        /// <summary>汇报已生成（即将显示）</summary>
        public event Action<RadioReport> OnReportGenerated;

        /// <summary>汇报已送达（显示给玩家）</summary>
        public event Action<RadioReport> OnReportDelivered;

        /// <summary>汇报受干扰</summary>
        public event Action<RadioReport> OnReportInterfered;

        /// <summary>检测到矛盾情报</summary>
        public event Action<List<RadioReport>> OnContradictionDetected;

        /// <summary>情报板更新</summary>
        public event Action<IntelligenceEntry> OnIntelligenceUpdated;

        /// <summary>情报过期</summary>
        public event Action<IntelligenceEntry> OnIntelligenceExpired;

        /// <summary>打字机逐字输出事件（UI 订阅以实现打字效果）</summary>
        public event Action<TypewriterEvent> OnTypewriterUpdate;

        /// <summary>打字机输出完成</summary>
        public event Action<string> OnTypewriterComplete;

        /// <summary>AIDirector 情报事件推送</summary>
        public event Action<AIDirectorIntelEvent> OnAIDirectorIntelReceived;

        // === 活跃打字机追踪 ===
        private Dictionary<string, Coroutine> activeTypewriters = new Dictionary<string, Coroutine>();
        private HashSet<string> completedTypewriters = new HashSet<string>();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // 自动订阅 AIDirector 随机事件
            if (autoSubscribeAIDirector)
            {
                SubscribeToAIDirector();
            }
        }

        void OnDestroy()
        {
            // 清理所有活跃的打字机协程
            StopAllTypewriters();
        }

        void Update()
        {
            // 更新所有情报的新鲜度
            UpdateFreshness();
        }

        #region 核心汇报流程

        /// <summary>
        /// 从前线战场事件生成无线电汇报
        /// 
        /// 这是信息系统的核心入口。
        /// 模拟系统产生的 BattleEvent 调用此方法。
        /// </summary>
        public void GenerateReport(BattleEvent battleEvent)
        {
            var report = new RadioReport();
            report.SourceUnitId = battleEvent.UnitId;
            report.SourceFrequency = GetFrequencyForUnit(battleEvent.UnitId);
            report.ActualEventTime = battleEvent.EventTime;
            report.GeneratedTime = Time.time;
            report.Priority = DeterminePriority(battleEvent);

            // 步骤1: 填充内容（从真实数据）
            PopulateContent(report, battleEvent);

            // 步骤2: 应用准确度衰减
            ApplyAccuracyDecay(report, battleEvent);

            // 步骤3: 应用干扰
            ApplyInterference(report);

            // 步骤4: 格式化为无线电文本
            FormatReport(report);

            // 步骤5: 计算延迟并加入队列
            float delay = CalculateReportDelay(report);
            report.DeliveryDelay = delay;

            OnReportGenerated?.Invoke(report);
            reportHistory.Add(report);

            // 异步送达
            StartCoroutine(DeliverReport(report, delay));

            Debug.Log($"[Intel] 汇报已生成 → {report.SourceUnitId} " +
                      $"[延迟:{delay:F0}s | 准确度:{report.Accuracy.OverallAccuracy:F2} " +
                      $"| 干扰:{report.InterferenceLevel:F2}]");
        }

        /// <summary>
        /// 主动请求部队状态
        /// 玩家可以通过无线电呼叫特定部队请求汇报
        /// </summary>
        public void RequestStatusReport(string unitId)
        {
            Debug.Log($"[Intel] 请求 {unitId} 状态汇报...");

            // 主动请求有额外延迟（前线需要整理信息）
            float requestDelay = UnityEngine.Random.Range(10f, 45f);

            // 通知模拟系统生成状态汇报
            if (BattleSimulationInterface.Instance != null)
            {
                BattleSimulationInterface.Instance.RequestUnitStatus(unitId);
            }

            // 同时启动本地延迟回复（确保即使模拟系统未就绪也有反馈）
            StartCoroutine(SimulateStatusRequest(unitId, requestDelay));
        }

        #endregion

        #region 准确度衰减

        /// <summary>
        /// 应用汇报准确度衰减
        /// 
        /// 衰减因素：
        /// 1. 士气 — 低士气导致混乱
        /// 2. 交战压力 — 战斗中汇报质量下降
        /// 3. 指挥官性格 — 不同部队有不同特征
        /// 4. 距离 — 远距离汇报误差更大
        /// </summary>
        private void ApplyAccuracyDecay(RadioReport report, BattleEvent battleEvent)
        {
            float accuracy = 1f;

            // 士气因素
            float morale = battleEvent.ActualMorale;
            float moraleFactor = GetAccuracyByMorale(morale);

            // 交战因素
            bool isInCombat = battleEvent.Type == BattleEventType.Engagement;
            float combatFactor = isInCombat ? combatAccuracyPenalty : 1f;

            // 指挥官性格因素
            float personalityFactor = GetPersonalityFactor(battleEvent.UnitId);

            // 最终准确度
            accuracy = moraleFactor * combatFactor * personalityFactor;
            report.Accuracy.OverallAccuracy = Mathf.Clamp01(accuracy);

            // 根据准确度决定哪些信息被遗漏或错误
            ApplyOmissions(report, battleEvent, accuracy);
            ApplyErrors(report, battleEvent, accuracy);
        }

        /// <summary>
        /// 根据准确度决定信息遗漏
        /// 准确度越低，遗漏越多
        /// </summary>
        private void ApplyOmissions(RadioReport report, BattleEvent battleEvent, float accuracy)
        {
            // 兵力汇报 — 低准确度可能不汇报
            if (UnityEngine.Random.value > accuracy)
            {
                report.Content.TroopCount = null;
                report.Accuracy.HasOmissions = true;
                report.Accuracy.OmittedDetails.Add("兵力数量");
            }

            // 敌军数量 — 经常被遗漏
            if (UnityEngine.Random.value > accuracy * 0.8f)
            {
                report.Content.EnemyCount = null;
                report.Accuracy.HasOmissions = true;
                report.Accuracy.OmittedDetails.Add("敌军数量");
            }

            // 位置 — 低准确度可能不汇报精确位置
            if (UnityEngine.Random.value > accuracy * 0.9f)
            {
                report.Content.ReportedPosition = null;
                report.Accuracy.HasOmissions = true;
                report.Accuracy.OmittedDetails.Add("精确位置");
            }

            // 弹药 — 经常被遗漏
            if (UnityEngine.Random.value > accuracy * 0.7f)
            {
                report.Content.AmmoLevel = null;
                report.Content.AmmoDescription = null;
                report.Accuracy.HasOmissions = true;
                report.Accuracy.OmittedDetails.Add("弹药状态");
            }
        }

        /// <summary>
        /// 根据准确度引入信息错误
        /// </summary>
        private void ApplyErrors(RadioReport report, BattleEvent battleEvent, float accuracy)
        {
            // 位置偏移
            if (report.Content.ReportedPosition.HasValue)
            {
                float errorMagnitude = (1f - accuracy) * positionErrorRange;
                Vector3 error = UnityEngine.Random.insideUnitSphere * errorMagnitude;
                report.Content.ReportedPosition = battleEvent.Position + error;
                report.Accuracy.HasErrors = errorMagnitude > 10f;
                if (report.Accuracy.HasErrors)
                    report.Accuracy.ErrorDetails.Add($"位置偏移约{errorMagnitude:F0}m");
            }

            // 兵力偏差 ±20%
            if (report.Content.TroopCount.HasValue)
            {
                float error = 1f + (UnityEngine.Random.value - 0.5f) * 0.4f * (1f - accuracy);
                report.Content.TroopCount = Mathf.RoundToInt(battleEvent.ActualTroopCount * error);
                if (Mathf.Abs(error - 1f) > 0.1f)
                {
                    report.Accuracy.HasErrors = true;
                    report.Accuracy.ErrorDetails.Add("兵力估计偏差");
                }
            }

            // 敌军类型误判
            if (!string.IsNullOrEmpty(battleEvent.EnemyType) && UnityEngine.Random.value > accuracy)
            {
                report.Content.EnemyType = MisidentifyEnemy(battleEvent.EnemyType);
                report.Accuracy.HasErrors = true;
                report.Accuracy.ErrorDetails.Add("敌军类型可能误判");
            }

            // 敌军数量偏差 ±50%
            if (report.Content.EnemyCount.HasValue)
            {
                float error = 1f + (UnityEngine.Random.value - 0.5f) * 1f * (1f - accuracy);
                report.Content.EnemyCount = Mathf.Max(1, Mathf.RoundToInt(battleEvent.ActualEnemyCount * error));
            }
        }

        /// <summary>
        /// 敌军类型误判
        /// </summary>
        private string MisidentifyEnemy(string actualType)
        {
            // 步兵可能被误判为装甲，反之亦然
            return actualType switch
            {
                "步兵" => UnityEngine.Random.value > 0.5f ? "机枪组" : "疑似装甲",
                "机枪碉堡" => "疑似炮位",
                "88mm炮位" => "机枪碉堡",
                "装甲" => "步兵（带重武器）",
                _ => "不明敌军"
            };
        }

        /// <summary>
        /// 根据士气获取准确度系数
        /// </summary>
        private float GetAccuracyByMorale(float morale)
        {
            int index;
            if (morale >= 80) index = 0;
            else if (morale >= 50) index = 1;
            else if (morale >= 30) index = 2;
            else index = 3;

            return accuracyByMorale[index];
        }

        /// <summary>
        /// 获取部队指挥官性格系数
        /// 不同指挥官有不同的汇报特征
        /// </summary>
        private float GetPersonalityFactor(string unitId)
        {
            return unitId switch
            {
                "company_1" => 1.05f,   // 第1连：冷静专业
                "company_2" => 0.85f,   // 第2连：紧张，容易夸大
                "tank_platoon" => 0.95f, // 坦克排：自信但简略
                "naval_gunfire" => 1.10f, // 舰炮：专业精准
                _ => 1.0f
            };
        }

        #endregion

        #region 干扰

        /// <summary>
        /// 应用无线电干扰
        /// 干扰会导致文本丢失/静电噪音标记
        /// </summary>
        private void ApplyInterference(RadioReport report)
        {
            float chance = GetInterferenceChance();
            float interferenceLevel = 0f;

            if (UnityEngine.Random.value < chance)
            {
                interferenceLevel = UnityEngine.Random.Range(0.2f, 0.8f);
                report.InterferenceLevel = interferenceLevel;

                // 干扰导致部分文字丢失
                if (interferenceLevel > 0.5f)
                {
                    CorruptText(report, interferenceLevel);
                }

                OnReportInterfered?.Invoke(report);
            }

            report.InterferenceLevel = interferenceLevel;
        }

        /// <summary>
        /// 干扰导致文本损坏（部分字符丢失/替换为静电标记）
        /// </summary>
        private void CorruptText(RadioReport report, float level)
        {
            if (string.IsNullOrEmpty(report.Content.Situation)) return;

            char[] chars = report.Content.Situation.ToCharArray();
            int corruptCount = Mathf.RoundToInt(chars.Length * level * 0.3f);

            for (int i = 0; i < corruptCount; i++)
            {
                int idx = UnityEngine.Random.Range(0, chars.Length);
                chars[idx] = '.'; // 用省略号模拟丢失
            }

            report.Content.Situation = new string(chars);
        }

        private float GetInterferenceChance()
        {
            if (GameDirector.Instance == null) return interferenceChance[1];
            int idx = Mathf.Clamp((int)GameDirector.Instance.difficulty, 0, 2);
            return interferenceChance[idx];
        }

        #endregion

        #region 延迟

        /// <summary>
        /// 计算汇报延迟
        /// </summary>
        private float CalculateReportDelay(RadioReport report)
        {
            float difficultyMultiplier = GameDirector.Instance != null
                ? (1f + (int)GameDirector.Instance.difficulty * 0.5f)
                : 1f;

            float baseDelay = UnityEngine.Random.Range(reportDelayMin, reportDelayMax);

            // 紧急汇报延迟较短
            float priorityMultiplier = report.Priority switch
            {
                ReportPriority.Critical => 0.3f,
                ReportPriority.Urgent => 0.5f,
                ReportPriority.Important => 0.8f,
                _ => 1f
            };

            return baseDelay * difficultyMultiplier * priorityMultiplier;
        }

        /// <summary>
        /// 异步送达汇报
        /// 送达后自动启动打字机效果
        /// </summary>
        private IEnumerator DeliverReport(RadioReport report, float delay)
        {
            yield return new WaitForSeconds(delay);

            report.DeliveredTime = Time.time;

            // 更新情报板
            UpdateIntelligenceBoard(report);

            // 检查矛盾
            CheckContradictions(report);

            // 发布汇报生成事件
            OnReportGenerated?.Invoke(report);

            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishRadioReportGenerated(report);
            }

            // 启动打字机效果（汇报通过打字机逐字送达玩家）
            StartTypewriter(report);

            // 汇报已送达事件
            OnReportDelivered?.Invoke(report);

            Debug.Log($"[Intel] 汇报已送达 → {report.SourceUnitId}: {report.FormattedText}");
        }

        #endregion

        #region 内容填充

        /// <summary>
        /// 从真实战场数据填充汇报内容
        /// </summary>
        private void PopulateContent(RadioReport report, BattleEvent battleEvent)
        {
            var content = report.Content;

            // 士气描述（模糊化）
            content.MoraleEstimate = battleEvent.ActualMorale;
            content.MoraleDescription = DescribeMorale(battleEvent.ActualMorale);

            // 弹药描述（模糊化）
            content.AmmoLevel = battleEvent.ActualAmmo;
            content.AmmoDescription = DescribeAmmo(battleEvent.ActualAmmo);

            // 伤亡标记
            content.HasCasualties = battleEvent.Type == BattleEventType.Casualties;
            if (content.HasCasualties)
            {
                content.CasualtyDescription = DescribeCasualties(battleEvent.ActualTroopCount);
            }

            // 位置（从真实位置，后续会被误差修改）
            content.ReportedPosition = battleEvent.Position;

            // 兵力
            content.TroopCount = battleEvent.ActualTroopCount;

            // 敌军
            content.EnemyType = battleEvent.EnemyType;
            content.EnemyCount = battleEvent.ActualEnemyCount;

            // 态势描述
            content.Situation = GenerateSituationText(battleEvent);
        }

        /// <summary>
        /// 士气模糊描述
        /// 玩家看到的是文字描述，不是数字
        /// </summary>
        private string DescribeMorale(float morale)
        {
            if (morale >= 80) return "士气高昂";
            if (morale >= 60) return "弟兄们状态不错";
            if (morale >= 40) return "有些动摇";
            if (morale >= 20) return "士气低落，需要支援";
            return "我们快撑不住了...";
        }

        /// <summary>
        /// 弹药模糊描述
        /// </summary>
        private string DescribeAmmo(float ammo)
        {
            if (ammo >= 70) return "弹药充足";
            if (ammo >= 40) return "弹药消耗中";
            if (ammo >= 20) return "弹药不足";
            return "弹药告急！需要补给！";
        }

        /// <summary>
        /// 伤亡模糊描述
        /// </summary>
        private string DescribeCasualties(int remainingTroops)
        {
            if (remainingTroops > 150) return "少量伤亡";
            if (remainingTroops > 100) return "伤亡约20%";
            if (remainingTroops > 60) return "损失严重，大量伤亡";
            return "伤亡惨重...很多人倒下了";
        }

        /// <summary>
        /// 根据事件类型生成态势描述文本
        /// </summary>
        private string GenerateSituationText(BattleEvent evt)
        {
            return evt.Type switch
            {
                BattleEventType.Landing => "我们已登陆...海滩到处是...",
                BattleEventType.Engagement => "遭遇敌军...正在交火...",
                BattleEventType.Movement => "正在向目标方向移动...",
                BattleEventType.Casualties => "有弟兄倒下了...需要医疗兵...",
                BattleEventType.ObjectiveCapture => "目标区域已占领！",
                BattleEventType.RequestSupport => "请求支援！我们需要火力掩护！",
                BattleEventType.CommunicationLost => "通讯...[静电噪音]...",
                _ => evt.Description ?? "情况不明..."
            };
        }

        /// <summary>
        /// 确定汇报优先级
        /// </summary>
        private ReportPriority DeterminePriority(BattleEvent evt)
        {
            return evt.Type switch
            {
                BattleEventType.RequestSupport => ReportPriority.Critical,
                BattleEventType.Casualties => ReportPriority.Urgent,
                BattleEventType.Engagement => ReportPriority.Urgent,
                BattleEventType.ObjectiveCapture => ReportPriority.Important,
                BattleEventType.CommunicationLost => ReportPriority.Critical,
                _ => ReportPriority.Routine
            };
        }

        /// <summary>
        /// 格式化汇报为无线电文本（打字机风格）
        /// </summary>
        private void FormatReport(RadioReport report)
        {
            string time = FormatGameTime(report.GeneratedTime);
            string freq = $"频率{report.SourceFrequency}";
            string source = GetUnitDisplayName(report.SourceUnitId);
            string content = report.Content.Situation;

            // 添加干扰标记
            if (report.InterferenceLevel > 0.3f)
            {
                content = $"[静电噪音]...{content}";
            }

            // 添加缺失信息的标记
            if (report.Accuracy.HasOmissions)
            {
                content += "...[信号不稳，部分信息丢失]";
            }

            report.FormattedText = $"[{time}] {freq} - {source} \"{content}...完毕\"";
        }

        #endregion

        #region 情报板管理

        /// <summary>
        /// 更新情报板（参谋标注）
        /// </summary>
        private void UpdateIntelligenceBoard(RadioReport report)
        {
            if (!report.Content.ReportedPosition.HasValue) return;

            var entry = new IntelligenceEntry
            {
                EntryId = report.ReportId,
                UnitId = report.SourceUnitId,
                Position = report.Content.ReportedPosition.Value,
                UnitType = report.Content.EnemyType ?? "友军",
                Confidence = report.Accuracy.OverallAccuracy,
                Freshness = 1f,
                LastUpdateTime = Time.time,
                IsConfirmed = report.Accuracy.OverallAccuracy > 0.7f,
                IsEnemy = !string.IsNullOrEmpty(report.Content.EnemyType),
                Source = IntelligenceSource.RadioReport
            };

            intelligenceBoard.Add(entry);
            OnIntelligenceUpdated?.Invoke(entry);
        }

        /// <summary>
        /// 更新所有情报的新鲜度
        /// 新鲜度随时间衰减，沙盘标记会变色
        /// </summary>
        private void UpdateFreshness()
        {
            for (int i = intelligenceBoard.Count - 1; i >= 0; i--)
            {
                var entry = intelligenceBoard[i];
                float age = Time.time - entry.LastUpdateTime;
                entry.Freshness = Mathf.Clamp01(1f - age / freshnessDecayTime);

                // 过期检测
                if (entry.Freshness <= 0.05f)
                {
                    OnIntelligenceExpired?.Invoke(entry);
                    // 保留但标记为过期（不删除，让UI决定如何处理）
                }
            }
        }

        /// <summary>
        /// 获取情报新鲜度评分
        /// 用于 UI 颜色标注：
        /// > 0.7: 绿色 (高可信)
        /// > 0.4: 黄色 (中可信)
        /// > 0.2: 橙色 (低可信)
        /// ≤ 0.2: 灰色 (过期)
        /// </summary>
        public float GetFreshnessScore(float entryTime)
        {
            float age = Time.time - entryTime;
            return Mathf.Clamp01(1f - age / freshnessDecayTime);
        }

        /// <summary>
        /// 检查矛盾情报
        /// </summary>
        private void CheckContradictions(RadioReport newReport)
        {
            foreach (var existing in reportHistory)
            {
                if (existing.ReportId == newReport.ReportId) continue;
                if (existing.SourceUnitId == newReport.SourceUnitId) continue;

                // 检查同一区域的矛盾描述
                bool bothReportEnemy = !string.IsNullOrEmpty(newReport.Content.EnemyType)
                                    && !string.IsNullOrEmpty(existing.Content.EnemyType);
                bool oneReportsEnemy = !string.IsNullOrEmpty(newReport.Content.EnemyType)
                                    != !string.IsNullOrEmpty(existing.Content.EnemyType);

                // 简单矛盾检测：两个来源对同一区域给出相反描述
                if (AreNearby(newReport, existing) && oneReportsEnemy)
                {
                    OnContradictionDetected?.Invoke(new List<RadioReport> { newReport, existing });
                    newReport.Accuracy.IsContradictory = true;
                    existing.Accuracy.IsContradictory = true;
                    Debug.Log($"[Intel] 检测到矛盾情报! {newReport.SourceUnitId} vs {existing.SourceUnitId}");
                }
            }
        }

        private bool AreNearby(RadioReport a, RadioReport b, float threshold = 200f)
        {
            if (!a.Content.ReportedPosition.HasValue || !b.Content.ReportedPosition.HasValue)
                return false;
            return Vector3.Distance(a.Content.ReportedPosition.Value,
                                    b.Content.ReportedPosition.Value) < threshold;
        }

        #endregion

        #region 打字机效果

        /// <summary>
        /// 启动打字机逐字显示效果
        /// 使用 Unity 协程逐字符输出，模拟无线电汇报接收过程
        /// 
        /// 特性：
        /// - 普通字符按 typewriterCharDelay 间隔显示
        /// - 标点符号（，。！？）有额外停顿
        /// - 静电噪音字符（.）用更快速度闪烁
        /// - 可通过 OnTypewriterUpdate 事件驱动任意 UI 组件
        /// - 支持同时多个汇报并行打字（按频率区分）
        /// </summary>
        /// <param name="report">要显示的汇报</param>
        /// <param name="targetUI">可选的 UI Text 组件（直接更新文本）</param>
        public void StartTypewriter(RadioReport report, Text targetUI = null)
        {
            if (report == null || string.IsNullOrEmpty(report.FormattedText)) return;

            string reportId = report.ReportId;

            // 如果该汇报已在打字中，先停止
            StopTypewriter(reportId);

            // 启动新的打字机协程
            Coroutine co = StartCoroutine(TypewriterCoroutine(report, targetUI));
            activeTypewriters[reportId] = co;

            Debug.Log($"[Intel:Typewriter] 开始打字输出 → {report.SourceUnitId} (频率{report.SourceFrequency})");
        }

        /// <summary>
        /// 停止指定汇报的打字机效果
        /// </summary>
        public void StopTypewriter(string reportId)
        {
            if (activeTypewriters.TryGetValue(reportId, out Coroutine co))
            {
                if (co != null) StopCoroutine(co);
                activeTypewriters.Remove(reportId);
            }
        }

        /// <summary>
        /// 停止所有活跃的打字机效果
        /// </summary>
        public void StopAllTypewriters()
        {
            foreach (var co in activeTypewriters.Values)
            {
                if (co != null) StopCoroutine(co);
            }
            activeTypewriters.Clear();
        }

        /// <summary>
        /// 跳过指定汇报的打字效果，直接显示完整文本
        /// </summary>
        public void SkipTypewriter(string reportId)
        {
            StopTypewriter(reportId);
            completedTypewriters.Add(reportId);
            OnTypewriterComplete?.Invoke(reportId);
        }

        /// <summary>
        /// 检查指定汇报是否正在打字输出中
        /// </summary>
        public bool IsTypewriting(string reportId)
        {
            return activeTypewriters.ContainsKey(reportId);
        }

        /// <summary>
        /// 打字机协程 — 核心实现
        /// 逐字符输出文本，模拟无线电接收效果
        /// </summary>
        private IEnumerator TypewriterCoroutine(RadioReport report, Text targetUI)
        {
            string fullText = report.FormattedText;
            string currentText = "";
            string reportId = report.ReportId;
            int length = fullText.Length;

            for (int i = 0; i < length; i++)
            {
                char c = fullText[i];
                currentText += c;

                // 更新 UI（如果提供了目标组件）
                if (targetUI != null)
                {
                    targetUI.text = currentText;
                }

                // 构建并广播打字机事件
                var evt = new TypewriterEvent
                {
                    ReportId = reportId,
                    CurrentText = currentText,
                    FullText = fullText,
                    CurrentChar = c,
                    CharIndex = i,
                    Progress = (float)(i + 1) / length,
                    IsComplete = false,
                    Frequency = report.SourceFrequency
                };
                OnTypewriterUpdate?.Invoke(evt);

                // 根据字符类型决定延迟
                float delay = GetCharDelay(c);
                yield return new WaitForSeconds(delay);
            }

            // 打字完成
            activeTypewriters.Remove(reportId);
            completedTypewriters.Add(reportId);

            // 发布完成事件
            var completeEvt = new TypewriterEvent
            {
                ReportId = reportId,
                CurrentText = fullText,
                FullText = fullText,
                CurrentChar = '\0',
                CharIndex = length,
                Progress = 1f,
                IsComplete = true,
                Frequency = report.SourceFrequency
            };
            OnTypewriterUpdate?.Invoke(completeEvt);
            OnTypewriterComplete?.Invoke(reportId);

            // 通过 GameEventBus 广播完成事件
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishRadioReportDelivered(report);
            }

            Debug.Log($"[Intel:Typewriter] 打字完成 → {report.SourceUnitId}");
        }

        /// <summary>
        /// 根据字符类型获取打字延迟
        /// 模拟真实的无线电通讯节奏
        /// </summary>
        private float GetCharDelay(char c)
        {
            // 静电噪音字符 — 快速闪烁
            if (c == '.') return typewriterStaticDelay;

            // 句末标点 — 长停顿
            if (c == '。' || c == '…' || c == '.' || c == '!' || c == '?')
                return typewriterSentenceDelay;

            // 中间标点 — 短停顿
            if (c == '，' || c == ',' || c == '—' || c == '-' || c == ';' || c == '；')
                return typewriterPunctuationDelay;

            // 引号/括号 — 微停顿
            if (c == '"' || c == '"' || c == '（' || c == '(' || c == ')' || c == '）')
                return typewriterPunctuationDelay * 0.5f;

            // 普通字符 — 标准速度
            return typewriterCharDelay;
        }

        #endregion

        #region AIDirector 协作

        /// <summary>
        /// 订阅 AIDirector 的随机事件
        /// AIDirector 生成天气/干扰/增援等随机事件时，信息系统自动生成对应的情报推送
        /// </summary>
        public void SubscribeToAIDirector()
        {
            if (AIDirector.Instance == null)
            {
                Debug.LogWarning("[Intel] AIDirector 实例不存在，跳过订阅");
                return;
            }

            Debug.Log("[Intel] 已订阅 AIDirector 随机事件");
        }

        /// <summary>
        /// 接收 AIDirector 推送的随机事件并生成情报
        /// AIDirector 调用此方法将事件转化为无线电情报
        /// </summary>
        /// <param name="eventType">事件类型: weather/comm_jam/reinforcement/supply/morale/enemy</param>
        /// <param name="description">事件描述文本</param>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="severity">严重程度 0-1</param>
        public void ReceiveAIDirectorEvent(string eventType, string description, float duration, float severity = 0.5f)
        {
            var aiEvent = new AIDirectorIntelEvent
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = eventType,
                Description = description,
                Duration = duration,
                AffectedFrequency = GetAffectedFrequency(eventType),
                Severity = severity
            };

            // 广播 AIDirector 事件
            OnAIDirectorIntelReceived?.Invoke(aiEvent);

            // 通过 GameEventBus 广播（供其他模块监听）
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishAIDirectorIntelReceived(aiEvent);
            }

            // 将事件转化为无线电汇报
            float delay = UnityEngine.Random.Range(aiDirectorIntelDelayRange.x, aiDirectorIntelDelayRange.y);
            StartCoroutine(GenerateAIDirectorReport(aiEvent, delay));

            // 特殊事件处理
            HandleSpecialEvent(aiEvent);

            Debug.Log($"[Intel:AIDirector] 收到事件: {eventType} - {description} (延迟:{delay:F0}s)");
        }

        /// <summary>
        /// 延迟生成 AIDirector 事件的情报汇报
        /// 模拟侦察兵发现/通讯延迟
        /// </summary>
        private IEnumerator GenerateAIDirectorReport(AIDirectorIntelEvent aiEvent, float delay)
        {
            yield return new WaitForSeconds(delay);

            // 构建虚拟 BattleEvent
            var battleEvent = new BattleEvent
            {
                EventId = aiEvent.EventId,
                UnitId = GetSourceUnitForEvent(aiEvent.EventType),
                EventTime = Time.time,
                Type = MapEventType(aiEvent.EventType),
                Position = Vector3.zero,
                ActualTroopCount = 0,
                ActualMorale = 50f,
                ActualAmmo = 50f,
                Description = aiEvent.Description
            };

            // 通过标准汇报流程生成情报
            GenerateReport(battleEvent);

            // 通过 GameEventBus 广播
            if (GameEventBus.Instance != null)
            {
                var report = new RadioReport
                {
                    ReportId = aiEvent.EventId,
                    SourceUnitId = battleEvent.UnitId,
                    SourceFrequency = aiEvent.AffectedFrequency,
                    ActualEventTime = battleEvent.EventTime,
                    GeneratedTime = Time.time,
                    Priority = ReportPriority.Important,
                    FormattedText = FormatAIDirectorReport(aiEvent)
                };
                GameEventBus.Instance.PublishRadioReportGenerated(report);
            }
        }

        /// <summary>
        /// 处理特殊 AIDirector 事件（如通讯干扰）
        /// 这些事件直接影响信息系统的行为
        /// </summary>
        private void HandleSpecialEvent(AIDirectorIntelEvent aiEvent)
        {
            switch (aiEvent.EventType)
            {
                case "comm_jam":
                    // 通讯干扰：临时提高所有频率的干扰等级
                    StartCoroutine(ApplyCommJam(aiEvent.Duration, aiEvent.Severity));
                    break;

                case "weather_rain":
                    // 天气：降低汇报准确度
                    StartCoroutine(ApplyWeatherPenalty(aiEvent.Duration, aiEvent.Severity));
                    break;

                case "enemy_hesitation":
                    // 敌军犹豫：减少敌军报告频率（由 AIDirector 控制）
                    Debug.Log($"[Intel] 敌军进攻节奏放缓，情报密度降低");
                    break;
            }
        }

        /// <summary>
        /// 通讯干扰效果：临时提升干扰概率
        /// </summary>
        private IEnumerator ApplyCommJam(float duration, float severity)
        {
            float originalMin = reportDelayMin;
            float originalMax = reportDelayMax;

            // 增加延迟
            reportDelayMin *= (1f + severity);
            reportDelayMax *= (1f + severity * 1.5f);

            Debug.Log($"[Intel] 通讯干扰生效! 延迟增加 {severity * 100:F0}%，持续 {duration:F0}s");

            yield return new WaitForSeconds(duration);

            // 恢复
            reportDelayMin = originalMin;
            reportDelayMax = originalMax;

            Debug.Log("[Intel] 通讯干扰结束，恢复正常");
        }

        /// <summary>
        /// 天气惩罚效果：降低汇报准确度
        /// </summary>
        private IEnumerator ApplyWeatherPenalty(float duration, float severity)
        {
            // 临时降低准确度基准
            float[] originalAccuracy = (float[])accuracyByMorale.Clone();
            for (int i = 0; i < accuracyByMorale.Length; i++)
            {
                accuracyByMorale[i] *= (1f - severity * 0.3f);
            }

            Debug.Log($"[Intel] 天气影响生效! 准确度降低 {severity * 30:F0}%，持续 {duration:F0}s");

            yield return new WaitForSeconds(duration);

            // 恢复
            accuracyByMorale = originalAccuracy;

            Debug.Log("[Intel] 天气影响结束，准确度恢复");
        }

        /// <summary>
        /// 根据事件类型获取影响的频率
        /// </summary>
        private int GetAffectedFrequency(string eventType)
        {
            return eventType switch
            {
                "comm_jam" => 0,         // 影响全部频率
                "weather_rain" => 0,     // 影响全部频率
                "reinforcement" => 5,    // 师部频率
                "supply" => 5,           // 师部频率
                "morale" => 0,           // 全部
                "enemy" => UnityEngine.Random.Range(1, 4), // 随机前线频率
                _ => 0
            };
        }

        /// <summary>
        /// 根据事件类型获取来源部队
        /// </summary>
        private string GetSourceUnitForEvent(string eventType)
        {
            return eventType switch
            {
                "weather_rain" => "division_hq",
                "comm_jam" => "division_hq",
                "reinforcement" => "division_hq",
                "supply" => "division_hq",
                "morale" => "division_hq",
                "enemy" => $"company_{UnityEngine.Random.Range(1, 3)}",
                _ => "division_hq"
            };
        }

        /// <summary>
        /// 将 AIDirector 事件类型映射为 BattleEventType
        /// </summary>
        private BattleEventType MapEventType(string eventType)
        {
            return eventType switch
            {
                "weather_rain" => BattleEventType.Movement,
                "comm_jam" => BattleEventType.CommunicationLost,
                "reinforcement" => BattleEventType.Reinforcement,
                "supply" => BattleEventType.SupplyReceived,
                "morale" => BattleEventType.MoraleChange,
                "enemy" => BattleEventType.Engagement,
                _ => BattleEventType.Movement
            };
        }

        /// <summary>
        /// 格式化 AIDirector 事件为无线电汇报文本
        /// </summary>
        private string FormatAIDirectorReport(AIDirectorIntelEvent aiEvent)
        {
            string time = FormatGameTime(Time.time);
            string freq = aiEvent.AffectedFrequency == 0 ? "全频" : $"频率{aiEvent.AffectedFrequency}";
            string prefix = aiEvent.Severity > 0.7f ? "【紧急】" : "【通报】";

            return $"[{time}] {freq} - {prefix} {aiEvent.Description}";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取部队对应的无线电频率
        /// </summary>
        private int GetFrequencyForUnit(string unitId)
        {
            return unitId switch
            {
                "company_1" => 1,
                "company_2" => 2,
                "tank_platoon" => 3,
                "naval_gunfire" => 4,
                "division_hq" => 5,
                _ => 1
            };
        }

        /// <summary>
        /// 获取部队显示名称
        /// </summary>
        private string GetUnitDisplayName(string unitId)
        {
            return unitId switch
            {
                "company_1" => "第1连",
                "company_2" => "第2连",
                "tank_platoon" => "坦克排",
                "naval_gunfire" => "蓝四",
                "division_hq" => "师部",
                _ => unitId
            };
        }

        /// <summary>
        /// 格式化游戏时间为 HH:MM:SS
        /// </summary>
        private string FormatGameTime(float time)
        {
            int hours = Mathf.FloorToInt(time / 3600f) + 6; // 假设从06:00开始
            int minutes = Mathf.FloorToInt((time % 3600f) / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        /// <summary>
        /// 模拟状态请求的延迟回复
        /// </summary>
        private IEnumerator SimulateStatusRequest(string unitId, float delay)
        {
            yield return new WaitForSeconds(delay);

            // 尝试从模拟系统获取真实状态
            BattleEvent evt;
            if (BattleSimulationInterface.Instance != null)
            {
                var status = BattleSimulationInterface.Instance.GetUnitStatus(unitId);
                evt = new BattleEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    UnitId = unitId,
                    EventTime = Time.time,
                    Type = status.IsInCombat ? BattleEventType.Engagement : BattleEventType.Movement,
                    Position = status.Position,
                    ActualTroopCount = status.TroopCount,
                    ActualMorale = status.Morale,
                    ActualAmmo = status.AmmoLevel,
                    Description = "状态汇报"
                };
            }
            else
            {
                // 模拟系统不可用时使用默认值
                evt = new BattleEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    UnitId = unitId,
                    EventTime = Time.time,
                    Type = BattleEventType.Movement,
                    Position = Vector3.zero,
                    ActualTroopCount = 150,
                    ActualMorale = 65f,
                    ActualAmmo = 60f,
                    Description = "状态汇报"
                };
            }

            GenerateReport(evt);
        }

        #endregion

        #region 模拟系统对接接口

        /// <summary>
        /// 注册模拟系统事件源
        /// 战场模拟系统调用此方法注册回调，向信息系统推送战场事件
        /// </summary>
        public void RegisterSimulationSource(System.Action<System.Action<BattleEvent>> eventSource)
        {
            if (eventSource != null)
            {
                eventSource(GenerateReport);
                Debug.Log("[Intel] 模拟系统事件源已注册");
            }
        }

        /// <summary>
        /// 接收来自模拟系统的战场事件（供模拟系统直接调用）
        /// </summary>
        public void ReceiveBattleEvent(BattleEvent battleEvent)
        {
            if (battleEvent == null) return;
            GenerateReport(battleEvent);
        }

        /// <summary>
        /// 批量接收战场事件
        /// </summary>
        public void ReceiveBattleEvents(IEnumerable<BattleEvent> events)
        {
            foreach (var evt in events)
            {
                if (evt != null) GenerateReport(evt);
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 获取当前情报板
        /// </summary>
        public List<IntelligenceEntry> GetIntelligenceBoard()
        {
            return new List<IntelligenceEntry>(intelligenceBoard);
        }

        /// <summary>
        /// 获取最近 N 条汇报
        /// </summary>
        public List<RadioReport> GetRecentReports(int count)
        {
            int start = Mathf.Max(0, reportHistory.Count - count);
            return reportHistory.GetRange(start, Mathf.Min(count, reportHistory.Count - start));
        }

        /// <summary>
        /// 获取指定部队的汇报历史
        /// </summary>
        public List<RadioReport> GetReportsByUnit(string unitId)
        {
            return reportHistory.FindAll(r => r.SourceUnitId == unitId);
        }

        #endregion
    }
}
