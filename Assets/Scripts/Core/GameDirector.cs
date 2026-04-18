// GameDirector.cs - 游戏总控 (替代旧 GameManager)
// 职责：全局状态、战役时间线、阶段管理
using UnityEngine;
using System;
using System.Collections.Generic;

namespace SWO1.Core
{
    public enum CampaignPhase
    {
        Briefing,           // 06:00 作战室简报
        Embarkation,        // 06:15 部队登艇
        FirstWaveLanding,   // 06:30 第一波登陆
        FirstReports,       // 06:35 第一波汇报
        SecondWaveLanding,  // 06:45 第二波登陆
        ThirdWaveLanding,   // 07:00 第三波登陆
        CounterAttack,      // 07:30 德军反击
        CriticalDecision,   // 08:00 关键决策点
        Resolution          // 09:00 战役结束
    }

    public enum Difficulty
    {
        Easy,       // 无线电可靠，延迟短
        Normal,     // 偶尔干扰，中等延迟
        Hard        // 频繁中断，严重延迟
    }

    public enum GameOutcome
    {
        InProgress,
        PerfectVictory,  // 3目标全占，伤亡<30%
        PyrrhicVictory,  // 3目标全占，伤亡>50%
        PartialVictory,  // 1-2目标占领
        Defeat,          // 0目标占领
        TotalDefeat      // 全军覆没
    }

    [Serializable]
    public class CampaignEvent
    {
        public string eventId;
        public float triggerGameTime; // 游戏内分钟数
        public string description;
        public bool triggered;
        public Action onTrigger;
    }

    public class GameDirector : MonoBehaviour
    {
        public static GameDirector Instance { get; private set; }

        [Header("战役配置")]
        public Difficulty difficulty = Difficulty.Normal;
        public float timeScale = 1f; // 1 游戏分钟 = 1 真实分钟

        [Header("战役时间")]
        [SerializeField] private float campaignStartGameTime = 360f; // 06:00 in minutes
        [SerializeField] private float campaignEndGameTime = 540f;    // 09:00 in minutes
        private float currentGameTime; // 当前游戏时间(分钟)

        // 事件
        public event Action<CampaignPhase> OnPhaseChanged;
        public event Action<float> OnGameTimeUpdated;
        public event Action<GameOutcome> OnCampaignEnded;

        // 状态
        public CampaignPhase CurrentPhase { get; private set; }
        public float CurrentGameTime => currentGameTime;
        public bool IsPaused { get; private set; }
        public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;

        // 战役事件队列
        private List<CampaignEvent> campaignEvents = new List<CampaignEvent>();

        // 目标占领状态
        private bool[] objectivesCaptured = new bool[3];

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            currentGameTime = campaignStartGameTime;
            CurrentPhase = CampaignPhase.Briefing;
            InitializeCampaignEvents();
        }

        void Update()
        {
            if (IsPaused || Outcome != GameOutcome.InProgress) return;

            // 推进游戏时间
            currentGameTime += (Time.deltaTime * timeScale) / 60f;
            OnGameTimeUpdated?.Invoke(currentGameTime);

            // 检查阶段转换
            UpdatePhase();

            // 检查战役事件
            CheckCampaignEvents();

            // 检查胜负
            CheckOutcome();
        }

        private void UpdatePhase()
        {
            CampaignPhase newPhase = CurrentPhase;

            if (currentGameTime >= 540f) newPhase = CampaignPhase.Resolution;
            else if (currentGameTime >= 480f) newPhase = CampaignPhase.CriticalDecision;
            else if (currentGameTime >= 450f) newPhase = CampaignPhase.CounterAttack;
            else if (currentGameTime >= 420f) newPhase = CampaignPhase.ThirdWaveLanding;
            else if (currentGameTime >= 405f) newPhase = CampaignPhase.SecondWaveLanding;
            else if (currentGameTime >= 395f) newPhase = CampaignPhase.FirstReports;
            else if (currentGameTime >= 390f) newPhase = CampaignPhase.FirstWaveLanding;
            else if (currentGameTime >= 375f) newPhase = CampaignPhase.Embarkation;
            else newPhase = CampaignPhase.Briefing;

            if (newPhase != CurrentPhase)
            {
                CurrentPhase = newPhase;
                OnPhaseChanged?.Invoke(CurrentPhase);
            }
        }

        private void InitializeCampaignEvents()
        {
            campaignEvents.Add(new CampaignEvent
            {
                eventId = "radio_silence",
                triggerGameTime = 390f, // 06:30
                description = "无线电沉默 - 登陆开始后5分钟无通讯"
            });

            campaignEvents.Add(new CampaignEvent
            {
                eventId = "conflicting_intel",
                triggerGameTime = 405f, // 06:45
                description = "矛盾情报 - 两连报告不一致"
            });

            campaignEvents.Add(new CampaignEvent
            {
                eventId = "comm_blackout",
                triggerGameTime = 435f, // 07:15
                description = "通讯中断 - 第1连无线电突然中断"
            });

            campaignEvents.Add(new CampaignEvent
            {
                eventId = "reinforcement_choice",
                triggerGameTime = 450f, // 07:30
                description = "增援抉择 - 三方同时请求支援"
            });
        }

        private void CheckCampaignEvents()
        {
            foreach (var evt in campaignEvents)
            {
                if (!evt.triggered && currentGameTime >= evt.triggerGameTime)
                {
                    evt.triggered = true;
                    evt.onTrigger?.Invoke();
                    Debug.Log($"[战役事件] {evt.description}");
                }
            }
        }

        public void ReportObjectiveCaptured(int index)
        {
            if (index >= 0 && index < objectivesCaptured.Length)
            {
                objectivesCaptured[index] = true;
            }
        }

        private void CheckOutcome()
        {
            if (currentGameTime < campaignEndGameTime) return;

            int captured = 0;
            foreach (var o in objectivesCaptured) if (o) captured++;

            if (captured == 3)
            {
                // 需要伤亡数据来区分完美胜利和惨胜
                Outcome = GameOutcome.PartialVictory; // 由 SimulationManager 更新
            }
            else if (captured >= 1)
                Outcome = GameOutcome.PartialVictory;
            else
                Outcome = GameOutcome.Defeat;

            OnCampaignEnded?.Invoke(Outcome);
        }

        public void SetFinalOutcome(float casualtyRate)
        {
            int captured = 0;
            foreach (var o in objectivesCaptured) if (o) captured++;

            if (captured == 3 && casualtyRate < 0.3f)
                Outcome = GameOutcome.PerfectVictory;
            else if (captured == 3 && casualtyRate >= 0.5f)
                Outcome = GameOutcome.PyrrhicVictory;
            else if (captured == 3)
                Outcome = GameOutcome.PartialVictory;
            else if (captured >= 1)
                Outcome = GameOutcome.PartialVictory;
            else if (captured == 0)
                Outcome = GameOutcome.Defeat;

            OnCampaignEnded?.Invoke(Outcome);
        }

        public void SetTotalDefeat()
        {
            Outcome = GameOutcome.TotalDefeat;
            OnCampaignEnded?.Invoke(Outcome);
        }

        // 暂停 / 恢复
        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        // 时间加速
        public void SetTimeScale(float scale) => timeScale = Mathf.Clamp(scale, 0.1f, 10f);

        /// <summary>
        /// 格式化游戏时间 HH:MM
        /// </summary>
        public string GetFormattedTime()
        {
            int hours = Mathf.FloorToInt(currentGameTime / 60f);
            int minutes = Mathf.FloorToInt(currentGameTime % 60f);
            return $"{hours:D2}:{minutes:D2}";
        }
    }
}
