// GameController.cs — 2D 游戏主控制器（胶水层）
// 串联所有核心系统，处理游戏循环和数据同步
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SWO1.Command;
using SWO1.Intelligence;
using SWO1.Simulation;
using SWO1.AI;
using SWO1.Visualization;
using SWO1.UI;
using SWO1.Audio;

namespace SWO1.Core
{
    public class GameController : MonoBehaviour
    {
        [Header("系统引用（自动查找）")]
        public GameDirector Director;
        public GameEventBus EventBus;
        public BattleSimulator BattleSim;
        public EnemyWaveManager WaveMgr;
        public CommandSystem CmdSystem;
        public InformationSystem InfoSystem;
        public AIDirector AIDir;
        public SandTableRenderer SandTable;
        public SimpleAudioManager AudioManager;

        [Header("UI（可选，自动查找）")]
        public UISystem2D UI2D;

        [Header("配置")]
        public float StateSyncInterval = 0.5f;

        private float _lastSyncTime;
        private bool _gameRunning;

        void Start()
        {
            // 自动查找系统
            Director = Director ?? FindObjectOfType<GameDirector>();
            EventBus = EventBus ?? GameEventBus.Instance;
            BattleSim = BattleSim ?? FindObjectOfType<BattleSimulator>();
            WaveMgr = WaveMgr ?? FindObjectOfType<EnemyWaveManager>();
            CmdSystem = CmdSystem ?? FindObjectOfType<CommandSystem>();
            InfoSystem = InfoSystem ?? FindObjectOfType<InformationSystem>();
            AIDir = AIDir ?? FindObjectOfType<AIDirector>();
            SandTable = SandTable ?? FindObjectOfType<SandTableRenderer>();
            AudioManager = AudioManager ?? FindObjectOfType<SimpleAudioManager>();
            UI2D = UI2D ?? FindObjectOfType<UISystem2D>();

            // 订阅事件
            if (EventBus != null)
            {
                EventBus.OnCampaignPhaseChanged += OnPhaseChanged;
                EventBus.OnRadioReportDelivered += OnReportDelivered;
                EventBus.OnCommandStatusChanged += OnCommandStatusChanged;
                EventBus.OnUICommandIssued += OnUICommand;
                EventBus.OnBattlefieldUpdated += OnBattlefieldUpdated;
                EventBus.OnGameOutcomeChanged += OnGameOutcome;
            }

            Debug.Log("[GameController] 初始化完成，所有系统已连接");
        }

        void OnDestroy()
        {
            if (EventBus != null)
            {
                EventBus.OnCampaignPhaseChanged -= OnPhaseChanged;
                EventBus.OnRadioReportDelivered -= OnReportDelivered;
                EventBus.OnCommandStatusChanged -= OnCommandStatusChanged;
                EventBus.OnUICommandIssued -= OnUICommand;
                EventBus.OnBattlefieldUpdated -= OnBattlefieldUpdated;
                EventBus.OnGameOutcomeChanged -= OnGameOutcome;
            }
        }

        void Update()
        {
            if (!_gameRunning) return;

            // 定期同步战场状态到沙盘
            if (Time.time - _lastSyncTime >= StateSyncInterval)
            {
                SyncBattlefieldState();
                _lastSyncTime = Time.time;
            }
        }

        // === 事件处理 ===

        void OnPhaseChanged(CampaignPhase phase)
        {
            Debug.Log($"[GameController] 阶段变更: {phase}");

            switch (phase)
            {
                case CampaignPhase.FirstWaveLanding:
                    _gameRunning = true;
                    if (BattleSim != null) BattleSim.enabled = true;
                    if (WaveMgr != null) WaveMgr.enabled = true;
                    break;

                case CampaignPhase.CounterAttack:
                    // 增加敌军强度
                    break;

                case CampaignPhase.Resolution:
                    _gameRunning = false;
                    break;
            }
        }

        void OnReportDelivered(RadioReport report)
        {
            // 推送无线电汇报到 UI
            if (UI2D != null && report != null)
            {
                string text = report.FormattedText ?? report.Content?.Situation ?? "收到汇报";
                bool interfered = report.InterferenceLevel > 0.5f;
                UI2D.AddRadioMessage(
                    FormatTime(report.GeneratedTime),
                    report.SourceUnitId ?? "未知",
                    text,
                    interfered,
                    (int)report.Priority
                );
            }
        }

        void OnCommandStatusChanged(RadioCommand cmd, CommandStatus status)
        {
            Debug.Log($"[GameController] 指令 {cmd?.CommandId} 状态: {status}");
            // UI 状态更新由 UISystem2D 自己订阅处理
        }

        void OnUICommand(string unitId, CommandType cmdType)
        {
            if (CmdSystem == null) return;

            int freq = 1; // 默认频率
            string content = GetCommandContent(unitId, cmdType);

            CmdSystem.SendCommand(unitId, freq, cmdType, content);
            Debug.Log($"[GameController] 发送指令: {cmdType} -> {unitId}");
        }

        void OnBattlefieldUpdated(BattlefieldData data)
        {
            if (UI2D != null && data != null)
            {
                // 构造 StatusSummary
                var summary = new StatusSummary
                {
                    CurrentTime = FormatGameTime(data.GameTime),
                    BridgeHP = data.BridgeHP,
                    BridgeMaxHP = data.BridgeMaxHP,
                    CurrentPhase = $"第 {data.CurrentWave} 波",
                    CountdownSeconds = Mathf.Max(0, 540f - data.GameTime) // 9分钟
                };
                UI2D.UpdateStatus(summary);
            }
        }

        void OnGameOutcome(GameOutcome outcome)
        {
            _gameRunning = false;
            Debug.Log($"[GameController] 游戏结束: {outcome}");

            if (UI2D != null)
            {
                var stats = new GameResultStats
                {
                    Outcome = outcome,
                    TotalPlayTime = Director != null ? Director.CurrentGameTime : 0f
                };
                UI2D.ShowResult(outcome, stats);
            }
        }

        // === 辅助方法 ===

        void SyncBattlefieldState()
        {
            if (BattleSim == null || EventBus == null) return;

            // 从 BattleSim 读取战场数据，发布更新事件
            var data = new BattlefieldData
            {
                BridgeHP = BattleSim.BridgeHP > 0 ? (int)BattleSim.BridgeHP : 0,
                BridgeMaxHP = 100,
                GameTime = Director != null ? Director.CurrentGameTime : 0f,
                CurrentWave = WaveMgr != null ? WaveMgr.CurrentWaveIndex : 0,
                Outcome = Director != null ? Director.Outcome : GameOutcome.InProgress
            };
            EventBus.PublishBattlefieldUpdated(data);
        }

        string FormatTime(float timestamp)
        {
            int minutes = (int)(timestamp / 60f);
            int seconds = (int)(timestamp % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }

        string FormatGameTime(float gameMinutes)
        {
            int hour = 6 + (int)(gameMinutes / 60f);
            int min = (int)(gameMinutes % 60f);
            return $"{hour:D2}:{min:D2}";
        }

        string GetCommandContent(string unitId, CommandType type)
        {
            return type switch
            {
                CommandType.Move => $"{unitId}，向目标区域移动",
                CommandType.Attack => $"{unitId}，攻击前方敌军",
                CommandType.Defend => $"{unitId}，就地防御",
                CommandType.Retreat => $"{unitId}，撤退至后方",
                CommandType.Recon => $"{unitId}，侦察前方区域",
                CommandType.StatusQuery => $"{unitId}，报告当前状态",
                _ => $"{unitId}，收到请回复"
            };
        }
    }
}
