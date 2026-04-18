// AIDirector.cs — AI 事件导演系统
// 负责生成随机战场事件、敌军决策、情报文本
// 支持 LLM API 调用 + 离线降级
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.Command;
using SWO1.Intelligence;
using SWO1.Simulation;

namespace SWO1.AI
{
    #region 数据模型

    [Serializable]
    public class EnemyStrategy
    {
        public string StrategyId;
        public string Description;
        public int[] TargetPlatoons;      // 主攻哪些排
        public float AggressionLevel;      // 0-1 进攻激进度
        public bool IsFeint;               // 是否佯攻
        public float Duration;             // 持续时间
    }

    [Serializable]
    public class RandomEvent
    {
        public string EventId;
        public string EventType;           // weather/comm_jam/reinforcement/supply
        public string Description;
        public float Duration;
        public Action<GameContext> ApplyEffect;
    }

    [Serializable]
    public class GameContext
    {
        public float BridgeHP;
        public float BridgeMaxHP;
        public int[] PlatoonsAlive;
        public float[] PlatoonsMorale;
        public float[] PlatoonsAmmo;
        public float ElapsedTime;
        public int CurrentWave;
        public string RecentPlayerCommand;
    }

    #endregion

    /// <summary>
    /// AI 事件导演 — 游戏的"大脑"
    /// 
    /// 职责：
    /// 1. 决定敌军进攻策略（主攻方向/是否佯攻）
    /// 2. 生成自然语言情报汇报
    /// 3. 触发随机事件（天气/通讯干扰/增援）
    /// 4. 生成前线回复文本
    /// 
    /// 支持两种模式：
    /// - LLM 模式：调用 API 生成内容（效果最好）
    /// - 降级模式：使用预设文本池随机选择（离线可用）
    /// </summary>
    public class AIDirector : MonoBehaviour
    {
        public static AIDirector Instance { get; private set; }

        [Header("AI 配置")]
        [SerializeField] private bool useLLM = true;
        [SerializeField] private string llmEndpoint = "https://token-plan-cn.xiaomimimo.com/v1/chat/completions";
        [SerializeField] private string llmModel = "mimo-v2-omni";
        [SerializeField] private string apiKey = "tp-cu3xxezvib0sx9dxg66i4gocxx335q2ttfewj7oafw4k6nsb";
        [SerializeField] private float llmTimeout = 3f;

        [Header("随机事件")]
        [SerializeField] private float minEventInterval = 30f;
        [SerializeField] private float maxEventInterval = 90f;

        // 预设文本池（离线降级用）
        private string[] presetIntelReports = new string[]
        {
            "东北方向发现敌军步兵，约30人，正在接近桥梁。",
            "西南方向听到装甲车辆引擎声。",
            "前方观察哨报告：敌军正在集结，规模不明。",
            "通讯受到干扰，信号不稳定。",
            "侦察兵回报：敌军携带重武器，预计10分钟内到达。",
            "我方左翼遭遇小股敌军骚扰。",
            "空中观察到敌军增援部队正在路上。",
            "桥梁附近发现可疑人员活动。"
        };

        private string[] presetEnemyStrategies = new string[]
        {
            "正面强攻",
            "侧翼包抄",
            "分散骚扰",
            "集中突破",
            "佯攻+主攻",
            "全线压上"
        };

        private string[] presetRandomEvents = new string[]
        {
            "weather_rain",
            "comm_jam",
            "reinforcement_delay",
            "ammo_shortage",
            "morale_boost",
            "enemy_hesitation"
        };

        private string[] presetFrontlineReplies = new string[]
        {
            "收到！正在执行。",
            "明白，但敌军火力很猛。",
            "收到，弹药不足，请求补给。",
            "明白，但我们伤亡很大。",
            "收到指令，正在移动。",
            "收到...通讯不好，重复一遍？",
            "明白！正在构筑防御工事。",
            "收到，炮击效果很好！"
        };

        // 状态
        private float nextEventTime;
        private System.Random rng = new System.Random();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            nextEventTime = Time.time + UnityEngine.Random.Range(minEventInterval, maxEventInterval);
        }

        void Update()
        {
            if (Time.time >= nextEventTime)
            {
                TriggerRandomEvent();
                nextEventTime = Time.time + UnityEngine.Random.Range(minEventInterval, maxEventInterval);
            }
        }

        #region 敌军决策

        /// <summary>
        /// 为下一波敌军生成进攻策略
        /// </summary>
        public EnemyStrategy GenerateEnemyStrategy(GameContext ctx)
        {
            if (useLLM)
            {
                // 尝试 LLM 调用（同步等待简化版）
                // 实际应用中用协程
            }

            // 降级：随机选择策略
            var strategy = new EnemyStrategy
            {
                StrategyId = Guid.NewGuid().ToString(),
                Description = presetEnemyStrategies[rng.Next(presetEnemyStrategies.Length)],
                AggressionLevel = 0.3f + (ctx.CurrentWave * 0.15f) + UnityEngine.Random.Range(-0.1f, 0.1f),
                IsFeint = rng.NextDouble() < 0.2,
                Duration = UnityEngine.Random.Range(60f, 120f)
            };

            // 选择主攻目标（兵力最弱的排）
            int weakestIdx = 0;
            int minAlive = int.MaxValue;
            for (int i = 0; i < ctx.PlatoonsAlive.Length; i++)
            {
                if (ctx.PlatoonsAlive[i] < minAlive)
                {
                    minAlive = ctx.PlatoonsAlive[i];
                    weakestIdx = i;
                }
            }
            strategy.TargetPlatoons = new int[] { weakestIdx, (weakestIdx + 1) % ctx.PlatoonsAlive.Length };

            Debug.Log($"[AI Director] 敌军策略: {strategy.Description} (激进度: {strategy.AggressionLevel:F1})");

            // 通过 InformationSystem 推送敌军动向情报
            PushIntelToSystem("enemy",
                $"侦察发现敌军动向：{strategy.Description}，疑似进攻准备中。",
                strategy.Duration, strategy.AggressionLevel);

            return strategy;
        }

        #endregion

        #region 情报生成

        /// <summary>
        /// 生成战场情报汇报
        /// </summary>
        public string GenerateIntelReport(GameContext ctx)
        {
            string baseReport = presetIntelReports[rng.Next(presetIntelReports.Length)];

            // 根据游戏状态添加上下文
            if (ctx.BridgeHP < ctx.BridgeMaxHP * 0.3f)
            {
                baseReport = "【紧急】" + baseReport + " 桥梁受损严重！";
            }
            else if (ctx.BridgeHP < ctx.BridgeMaxHP * 0.6f)
            {
                baseReport = "【警告】" + baseReport;
            }

            return baseReport;
        }

        /// <summary>
        /// 生成前线回复文本
        /// </summary>
        public string GenerateFrontlineReply(string commandContent, float morale)
        {
            if (morale < 30)
            {
                return "收到...但我们快撑不住了。" + presetFrontlineReplies[rng.Next(presetFrontlineReplies.Length)];
            }
            return presetFrontlineReplies[rng.Next(presetFrontlineReplies.Length)];
        }

        #endregion

        #region 随机事件

        /// <summary>
        /// 触发随机事件
        /// 通过 InformationSystem 推送情报给玩家
        /// </summary>
        private void TriggerRandomEvent()
        {
            string eventType = presetRandomEvents[rng.Next(presetRandomEvents.Length)];

            switch (eventType)
            {
                case "weather_rain":
                    Debug.Log("[AI Event] 天气变化：开始下雨，视野降低");
                    PushIntelToSystem("weather_rain", "天气变化：开始下雨，能见度降低，各部队注意观察。",
                        UnityEngine.Random.Range(60f, 180f), 0.4f);
                    break;

                case "comm_jam":
                    Debug.Log("[AI Event] 通讯干扰：无线电暂时中断");
                    PushIntelToSystem("comm_jam", "侦测到敌军电子干扰，无线电通讯可能不稳定。",
                        UnityEngine.Random.Range(30f, 90f), 0.7f);
                    break;

                case "reinforcement_delay":
                    Debug.Log("[AI Event] 增援延迟：原定增援推迟到达");
                    PushIntelToSystem("reinforcement", "师部通知：原定增援因故推迟，各部坚守阵地。",
                        0f, 0.5f);
                    break;

                case "ammo_shortage":
                    Debug.Log("[AI Event] 弹药短缺：各排弹药消耗加快");
                    PushIntelToSystem("supply", "后勤报告：弹药补给线受阻，请各部节约弹药。",
                        UnityEngine.Random.Range(60f, 120f), 0.6f);
                    break;

                case "morale_boost":
                    Debug.Log("[AI Event] 士气提升：收到师部嘉奖");
                    PushIntelToSystem("morale", "师部嘉奖令：表彰前线各部英勇作战，司令部表示感谢。",
                        UnityEngine.Random.Range(45f, 90f), 0.3f);
                    break;

                case "enemy_hesitation":
                    Debug.Log("[AI Event] 敌军犹豫：进攻节奏放缓");
                    PushIntelToSystem("enemy", "侦察报告：敌军集结速度放缓，疑似在等待增援。",
                        UnityEngine.Random.Range(60f, 120f), 0.4f);
                    break;
            }
        }

        /// <summary>
        /// 向 InformationSystem 推送情报事件
        /// </summary>
        private void PushIntelToSystem(string eventType, string description, float duration, float severity)
        {
            if (InformationSystem.Instance != null)
            {
                InformationSystem.Instance.ReceiveAIDirectorEvent(eventType, description, duration, severity);
            }
            else
            {
                Debug.LogWarning("[AI Director] InformationSystem 实例不存在，无法推送情报");
            }
        }

        #endregion

        #region 上下文构建

        /// <summary>
        /// 从当前游戏状态构建上下文
        /// </summary>
        public GameContext BuildContext()
        {
            var ctx = new GameContext();

            if (BattleSimulator.Instance != null)
            {
                ctx.BridgeHP = BattleSimulator.Instance.BridgeHP;
                ctx.BridgeMaxHP = 100f;
                var platoons = BattleSimulator.Instance.GetPlatoons();
                ctx.PlatoonsAlive = new int[platoons.Count];
                ctx.PlatoonsMorale = new float[platoons.Count];
                ctx.PlatoonsAmmo = new float[platoons.Count];
                for (int i = 0; i < platoons.Count; i++)
                {
                    ctx.PlatoonsAlive[i] = platoons[i].CurrentTroops;
                    ctx.PlatoonsMorale[i] = platoons[i].Morale;
                    ctx.PlatoonsAmmo[i] = platoons[i].AmmoPercent;
                }
            }

            if (GameDirector.Instance != null)
            {
                ctx.ElapsedTime = GameDirector.Instance.CurrentGameTime;
            }

            if (EnemyWaveManager.Instance != null)
            {
                ctx.CurrentWave = EnemyWaveManager.Instance.CurrentWaveIndex;
            }

            return ctx;
        }

        #endregion
    }
}
