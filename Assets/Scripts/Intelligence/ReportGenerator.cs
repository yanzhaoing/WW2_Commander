// ReportGenerator.cs — 汇报生成器
// 从战场事件生成无线电汇报文本，提供模板化的汇报语言
using UnityEngine;
using System;
using System.Collections.Generic;

namespace SWO1.Intelligence
{
    /// <summary>
    /// 汇报模板
    /// </summary>
    [Serializable]
    public class ReportTemplate
    {
        public BattleEventType EventType;
        public string[] SituationTemplates;
        public string[] RequestTemplates;
        public string[] MoraleDescriptions;
    }

    /// <summary>
    /// 汇报生成器 — 将战场事件转化为无线电汇报文本
    /// 
    /// 设计原则：
    /// - 汇报语言自然，带有人格特征
    /// - 不同部队有不同的汇报风格
    /// - 模板化 + 随机化 = 每次汇报不重复
    /// 
    /// 使用方式：
    ///   var generator = ReportGenerator.Instance;
    ///   string text = generator.GenerateSituationText(battleEvent);
    /// </summary>
    public class ReportGenerator : MonoBehaviour
    {
        public static ReportGenerator Instance { get; private set; }

        [Header("模板配置")]
        [SerializeField] private List<ReportTemplate> templates = new List<ReportTemplate>();

        // 部队汇报风格
        private Dictionary<string, string[]> unitGreetings = new Dictionary<string, string[]>
        {
            { "company_1", new[] { "这里是红一...", "第1连报告...", "红一向指挥所汇报..." } },
            { "company_2", new[] { "红二报告...", "这里是第2连...", "指挥所，第2连..." } },
            { "tank_platoon", new[] { "铁骑报告...", "坦克排汇报..." } },
            { "naval_gunfire", new[] { "蓝四确认...", "舰炮支援就位..." } },
            { "division_hq", new[] { "师部通知..." } }
        };

        private Dictionary<string, string[]> unitSignoffs = new Dictionary<string, string[]>
        {
            { "company_1", new[] { "完毕", "等待指示", "红一完毕" } },
            { "company_2", new[] { "完毕", "红二等待回复", "请指示" } },
            { "tank_platoon", new[] { "铁骑完毕", "等待命令" } },
            { "naval_gunfire", new[] { "蓝四待命", "随时可以开火" } },
            { "division_hq", new[] { "收到回复", "按计划执行" } }
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // 初始化默认模板
            if (templates.Count == 0) InitializeDefaultTemplates();
        }

        /// <summary>
        /// 生成完整的汇报文本
        /// </summary>
        public string GenerateFullReport(BattleEvent evt, float accuracy)
        {
            string greeting = GetGreeting(evt.UnitId);
            string situation = GenerateSituationText(evt, accuracy);
            string request = GenerateRequestText(evt);
            string signoff = GetSignoff(evt.UnitId);

            return $"{greeting}{situation}...{request}{signoff}";
        }

        /// <summary>
        /// 生成态势描述文本
        /// </summary>
        public string GenerateSituationText(BattleEvent evt, float accuracy = 1f)
        {
            // 基础态势文本
            string baseText = evt.Type switch
            {
                BattleEventType.Landing => GenerateLandingText(evt),
                BattleEventType.Engagement => GenerateEngagementText(evt, accuracy),
                BattleEventType.Movement => GenerateMovementText(evt),
                BattleEventType.Casualties => GenerateCasualtyText(evt, accuracy),
                BattleEventType.ObjectiveCapture => GenerateCaptureText(evt),
                BattleEventType.RequestSupport => GenerateSupportRequestText(evt),
                BattleEventType.CommunicationLost => "...[通讯中断，静电噪音]...",
                BattleEventType.Reinforcement => "增援已到达，正在重新部署...",
                BattleEventType.SupplyReceived => "补给已收到，感谢...",
                BattleEventType.MoraleChange => GenerateMoraleChangeText(evt),
                _ => evt.Description ?? "情况不明..."
            };

            return baseText;
        }

        /// <summary>
        /// 生成请求文本
        /// </summary>
        public string GenerateRequestText(BattleEvent evt)
        {
            if (evt.Type != BattleEventType.RequestSupport) return "";

            return evt.ActualMorale < 30
                ? "紧急请求撤退许可！"
                : "请求火力掩护和增援！";
        }

        #region 各类事件文本生成

        private string GenerateLandingText(BattleEvent evt)
        {
            string[] templates = {
                "我们已登陆...海滩到处是...烟雾很重",
                "登陆完成...正在建立滩头阵地",
                "已上岸...部队正在集结...等待进一步指示",
                "登陆成功...但损失了一些弟兄"
            };
            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        private string GenerateEngagementText(BattleEvent evt, float accuracy)
        {
            string[] templates = {
                "遭遇敌军...正在交火...火力很猛",
                "与敌军接触...重武器在前方",
                "正在交战...敌军抵抗顽强",
                "敌军阵地在我们前方...机枪压制中"
            };

            string text = templates[UnityEngine.Random.Range(0, templates.Length)];

            // 低准确度时信息模糊
            if (accuracy < 0.5f)
            {
                text += "...具体情况不太清楚...";
            }

            return text;
        }

        private string GenerateMovementText(BattleEvent evt)
        {
            string[] templates = {
                "正在向目标方向移动...",
                "部队正在推进...遇到少量抵抗",
                "按计划向指定位置移动中",
                "正在机动...准备接敌"
            };
            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        private string GenerateCasualtyText(BattleEvent evt, float accuracy)
        {
            string[] templates = {
                "有弟兄倒下了...需要医疗兵...",
                "伤亡报告...损失了一些弟兄",
                "有人受伤...急需医疗支援",
                "我们有人受伤了...情况不太好..."
            };
            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        private string GenerateCaptureText(BattleEvent evt)
        {
            string[] templates = {
                "目标区域已占领！正在巩固防线",
                "据点已拿下...敌军撤退中",
                "目标已控制...请求下一步指示"
            };
            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        private string GenerateSupportRequestText(BattleEvent evt)
        {
            string[] templates = {
                "请求支援！我们需要火力掩护！",
                "紧急呼叫...我们需要帮助！",
                "火力支援！坐标前方！快！"
            };
            return templates[UnityEngine.Random.Range(0, templates.Length)];
        }

        private string GenerateMoraleChangeText(BattleEvent evt)
        {
            if (evt.ActualMorale > 70)
                return "弟兄们精神不错...准备好了";
            if (evt.ActualMorale > 40)
                return "有些人开始紧张了...";
            return "弟兄们很疲惫...士气不太好...";
        }

        #endregion

        #region 辅助方法

        private string GetGreeting(string unitId)
        {
            if (unitGreetings.TryGetValue(unitId, out var greetings))
                return greetings[UnityEngine.Random.Range(0, greetings.Length)];
            return "...";
        }

        private string GetSignoff(string unitId)
        {
            if (unitSignoffs.TryGetValue(unitId, out var signoffs))
                return signoffs[UnityEngine.Random.Range(0, signoffs.Length)];
            return "完毕";
        }

        private void InitializeDefaultTemplates()
        {
            // 可在 Inspector 中配置，或使用默认值
        }

        #endregion
    }
}
