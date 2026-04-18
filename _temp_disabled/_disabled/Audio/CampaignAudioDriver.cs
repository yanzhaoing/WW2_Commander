// CampaignAudioDriver.cs — 战役音频事件驱动器
// 将游戏设计文档中的战役时间线映射到音频行为
using UnityEngine;
using FMOD.Studio;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SWO1.Audio
{
    /// <summary>
    /// 战役音频事件定义
    /// </summary>
    [Serializable]
    public class CampaignAudioEvent
    {
        public string eventId;
        public float triggerMinutes;      // 游戏内分钟 (06:00 = 360)
        public float durationSeconds;     // 持续时间
        [TextArea] public string description;

        // 音频参数变化
        public float targetRadioClarity = -1;  // -1 = 不改变
        public float targetRadioNoise = -1;
        public float targetBattleIntensity = -1;
        public float targetAmbientTension = -1;

        // 特殊行为
        public bool radioSilence;          // 无线电完全静默
        public bool heavyInterference;     // 强干扰
        public int targetChannel = -1;     // 影响的频道 (-1 = 全部)

        // 关联的语音消息
        public string[] messages;          // 消息文本数组
        public float messageDelay;         // 首条消息延迟
    }

    /// <summary>
    /// 战役音频事件驱动器
    /// 按照游戏设计文档的时间线驱动音频系统
    /// </summary>
    public class CampaignAudioDriver : MonoBehaviour
    {
        [Header("系统引用")]
        [SerializeField] private RadioAudioSystem radioSystem;
        [SerializeField] private BattleAudioSystem battleSystem;
        [SerializeField] private InteractionAudioSystem interactionSystem;

        [Header("战役事件")]
        [SerializeField] private CampaignAudioEvent[] campaignEvents;

        // 时间追踪
        private float _gameStartTime;
        private float _gameMinutes => 360f + (Time.time - _gameStartTime) / 60f; // 06:00 开始

        // 已触发事件追踪
        private readonly HashSet<string> _triggeredEvents = new();

        // 事件回调
        public event Action<string> OnCampaignEventTriggered;

        // 预定义事件（按游戏设计文档）
        private void Awake()
        {
            if (campaignEvents == null || campaignEvents.Length == 0)
            {
                campaignEvents = GenerateDefaultCampaignEvents();
            }
        }

        private void Start()
        {
            _gameStartTime = Time.time;
        }

        private void Update()
        {
            float currentMinutes = _gameMinutes;

            foreach (var evt in campaignEvents)
            {
                if (_triggeredEvents.Contains(evt.eventId))
                    continue;

                if (currentMinutes >= evt.triggerMinutes)
                {
                    TriggerCampaignEvent(evt);
                    _triggeredEvents.Add(evt.eventId);
                }
            }

            // 持续更新时间参数
            AudioManager.Instance.UpdateTimeOfDay(currentMinutes);
        }

        private void TriggerCampaignEvent(CampaignAudioEvent evt)
        {
            Debug.Log($"[CampaignAudio] Triggering: {evt.eventId} at {_gameMinutes:F1} min — {evt.description}");

            // 应用参数变化
            if (evt.targetRadioClarity >= 0)
                AudioManager.Instance.SetParameter("RadioClarity", evt.targetRadioClarity);
            if (evt.targetRadioNoise >= 0)
                AudioManager.Instance.SetParameter("RadioNoise", evt.targetRadioNoise);
            if (evt.targetBattleIntensity >= 0)
                AudioManager.Instance.SetParameter("BattleIntensity", evt.targetBattleIntensity);
            if (evt.targetAmbientTension >= 0)
                AudioManager.Instance.SetParameter("AmbientTension", evt.targetAmbientTension);

            // 无线电静默
            if (evt.radioSilence && evt.targetChannel >= 0)
            {
                string channelId = $"freq{evt.targetChannel}";
                radioSystem.TriggerCommsLost(channelId, evt.durationSeconds);
            }
            else if (evt.radioSilence)
            {
                radioSystem.SetGlobalInterference(0.95f);
            }

            // 强干扰
            if (evt.heavyInterference)
            {
                radioSystem.SetGlobalInterference(0.7f);
            }

            // 播放关联消息
            if (evt.messages != null && evt.messages.Length > 0)
            {
                StartCoroutine(PlayMessagesDelayed(evt));
            }

            OnCampaignEventTriggered?.Invoke(evt.eventId);
        }

        private IEnumerator PlayMessagesDelayed(CampaignAudioEvent evt)
        {
            if (evt.messageDelay > 0)
                yield return new WaitForSeconds(evt.messageDelay);

            string channelId = evt.targetChannel >= 0 ? $"freq{evt.targetChannel}" : "freq1";

            var condition = evt.radioSilence ? RadioCondition.HeavyInterference
                : evt.heavyInterference ? RadioCondition.WeakSignal
                : RadioCondition.Normal;

            foreach (var msg in evt.messages)
            {
                radioSystem.PlayRadioMessage(channelId, msg, condition, VoiceEmotion.Tense);
                yield return new WaitForSeconds(EstimateMessageDuration(msg) + 1f);
            }
        }

        private float EstimateMessageDuration(string text) => text.Length * 0.15f;

        /// <summary>
        /// 生成默认战役事件（基于游戏设计文档）
        /// </summary>
        private CampaignAudioEvent[] GenerateDefaultCampaignEvents()
        {
            return new[]
            {
                new CampaignAudioEvent
                {
                    eventId = "briefing",
                    triggerMinutes = 360f,  // 06:00
                    durationSeconds = 900,
                    description = "作战室简报 — 低紧张度，无战场音",
                    targetRadioClarity = 0.8f,
                    targetRadioNoise = 0.1f,
                    targetBattleIntensity = 0.1f,
                    targetAmbientTension = 0.1f,
                },
                new CampaignAudioEvent
                {
                    eventId = "embark",
                    triggerMinutes = 375f,  // 06:15
                    durationSeconds = 900,
                    description = "部队登艇 — 远处开始有引擎声，无线电开始有底噪",
                    targetBattleIntensity = 0.2f,
                    targetAmbientTension = 0.25f,
                },
                new CampaignAudioEvent
                {
                    eventId = "landing_silence",
                    triggerMinutes = 390f,  // 06:30
                    durationSeconds = 300,
                    description = "登陆开始 — 无线电完全沉默 5 分钟",
                    radioSilence = true,
                    targetRadioClarity = 0.05f,
                    targetRadioNoise = 0.9f,
                    targetBattleIntensity = 0.6f,
                    targetAmbientTension = 0.8f,
                },
                new CampaignAudioEvent
                {
                    eventId = "first_reports",
                    triggerMinutes = 395f,  // 06:35
                    durationSeconds = 600,
                    description = "首批汇报到达 — 无线电突然激活，紧张语音，密集枪声背景",
                    targetRadioClarity = 0.65f,
                    targetRadioNoise = 0.3f,
                    targetBattleIntensity = 0.8f,
                    targetAmbientTension = 0.7f,
                    messages = new[]
                    {
                        "这里是红一...我们已登陆...海滩到处是...请求指示目标方向...完毕",
                        "[静电噪音]...遭遇重火力...重复...重火力...南侧...碉堡？不确认...完毕"
                    },
                    messageDelay = 1f,
                },
                new CampaignAudioEvent
                {
                    eventId = "contradictory_intel",
                    triggerMinutes = 405f,  // 06:45
                    durationSeconds = 600,
                    description = "矛盾情报 — 两个频率交替播报",
                    targetBattleIntensity = 0.7f,
                    messages = new[]
                    {
                        "红一呼叫指挥所...海滩安全...正在向内陆推进...完毕",
                        "红二呼叫...遭遇重火力压制...请求撤退...重复...请求撤退...完毕",
                    },
                },
                new CampaignAudioEvent
                {
                    eventId = "tanks_arrive",
                    triggerMinutes = 420f,  // 07:00
                    durationSeconds = 300,
                    description = "坦克排登陆 — 坦克引擎声加入无线电背景",
                    targetBattleIntensity = 0.6f,
                    messages = new[]
                    {
                        "蓝三呼叫...坦克排已登陆...5 辆谢尔曼...正在寻找目标...完毕",
                    },
                    targetChannel = 3,
                },
                new CampaignAudioEvent
                {
                    eventId = "comms_lost",
                    triggerMinutes = 435f,  // 07:15
                    durationSeconds = 120,
                    description = "第 1 连通讯中断 — 频率突然只剩静电",
                    radioSilence = true,
                    targetChannel = 1,
                    targetAmbientTension = 0.85f,
                },
                new CampaignAudioEvent
                {
                    eventId = "counterattack",
                    triggerMinutes = 450f,  // 07:30
                    durationSeconds = 1800,
                    description = "德军反击 — 战场音密度骤增，多频率同时呼叫",
                    heavyInterference = true,
                    targetBattleIntensity = 0.95f,
                    targetAmbientTension = 0.9f,
                    messages = new[]
                    {
                        "红二紧急呼叫...敌军反击...装甲单位...东北方向...请求炮击支援...完毕",
                        "蓝三呼叫...燃油不足...重复...燃油不足...无法继续机动...完毕",
                    },
                },
                new CampaignAudioEvent
                {
                    eventId = "decision_point",
                    triggerMinutes = 480f,  // 08:00
                    durationSeconds = 1800,
                    description = "关键决策点 — 环境音降低，无线电突出，时钟声放大",
                    targetBattleIntensity = 0.6f,
                    targetAmbientTension = 0.95f,
                },
                new CampaignAudioEvent
                {
                    eventId = "campaign_end",
                    triggerMinutes = 540f,  // 09:00
                    durationSeconds = 600,
                    description = "战役结束 — 音效渐弱，炮声稀疏，最终安静",
                    targetBattleIntensity = 0.15f,
                    targetAmbientTension = 0.2f,
                    targetRadioClarity = 0.9f,
                    targetRadioNoise = 0.1f,
                },
            };
        }

        /// <summary>
        /// 获取当前游戏时间字符串
        /// </summary>
        public string GetTimeString()
        {
            int hours = Mathf.FloorToInt(_gameMinutes / 60f);
            int minutes = Mathf.FloorToInt(_gameMinutes % 60f);
            int seconds = Mathf.FloorToInt((_gameMinutes * 60f) % 60f);
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        /// <summary>
        /// 跳转到指定时间（测试用）
        /// </summary>
        [ContextMenu("Skip to 06:35 (First Reports)")]
        public void SkipTo0635()
        {
            _gameStartTime = Time.time - (395f - 360f) * 60f;
        }

        [ContextMenu("Skip to 07:15 (Comms Lost)")]
        public void SkipTo0715()
        {
            _gameStartTime = Time.time - (435f - 360f) * 60f;
        }

        [ContextMenu("Skip to 07:30 (Counterattack)")]
        public void SkipTo0730()
        {
            _gameStartTime = Time.time - (450f - 360f) * 60f;
        }
    }
}
