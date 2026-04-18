// GameEventBus.cs — 全局事件总线
// 所有模块间通信通过此事件总线，实现松耦合
using UnityEngine;
using System;
using SWO1.Command;
using SWO1.Intelligence;
using SWO1.CommandPost;

namespace SWO1.Core
{
    /// <summary>
    /// 全局事件总线（单例）
    /// 
    /// 设计原则：
    /// - 所有模块间通信通过事件总线，不直接引用
    /// - 事件定义在此处，各模块订阅/发布
    /// - 使用 C# event 关键字确保类型安全
    /// 
    /// 使用方式：
    ///   // 订阅
    ///   GameEventBus.Instance.OnReportDelivered += HandleReport;
    ///   // 发布
    ///   GameEventBus.Instance.OnReportDelivered?.Invoke(report);
    /// </summary>
    public class GameEventBus : MonoBehaviour
    {
        public static GameEventBus Instance { get; private set; }

        #region 无线电事件

        /// <summary>无线电汇报已送达</summary>
        public event Action<RadioReport> OnRadioReportDelivered;

        /// <summary>无线电汇报生成</summary>
        public event Action<RadioReport> OnRadioReportGenerated;

        /// <summary>无线电指令已发送</summary>
        public event Action<RadioCommand> OnRadioCommandSent;

        /// <summary>无线电干扰发生</summary>
        public event Action<RadioReport> OnRadioInterference;

        /// <summary>AIDirector 情报事件推送</summary>
        public event Action<SWO1.Intelligence.AIDirectorIntelEvent> OnAIDirectorIntelReceived;

        #endregion

        #region 指挥事件

        /// <summary>指令状态变更</summary>
        public event Action<RadioCommand, CommandStatus> OnCommandStatusChanged;

        /// <summary>指令已送达前线</summary>
        public event Action<RadioCommand> OnCommandDelivered;

        /// <summary>指令丢失</summary>
        public event Action<RadioCommand> OnCommandLost;

        /// <summary>指令被误解</summary>
        public event Action<RadioCommand, string> OnCommandMisinterpreted;

        #endregion

        #region 沙盘事件

        /// <summary>沙盘标记更新</summary>
        public event Action<IntelligenceEntry> OnSandTableUpdated;

        /// <summary>矛盾情报检测</summary>
        public event Action<System.Collections.Generic.List<RadioReport>> OnContradictionDetected;

        #endregion

        #region 可视化事件

        /// <summary>战场数据更新（地形/桥梁HP/波次等整体状态变更时触发）</summary>
        public event Action<BattlefieldData> OnBattlefieldUpdated;

        /// <summary>单位位置变更（部队移动时触发，带动画插值信息）</summary>
        public event Action<UnitPositionData> OnUnitPositionChanged;

        #endregion

        #region 交互事件

        /// <summary>物理交互发生</summary>
        public event Action<InteractionEvent> OnInteractionPerformed;

        /// <summary>摄像机注视点变更</summary>
        public event Action<FocusPoint> OnCameraFocusChanged;

        #endregion

        #region 棋子事件

        /// <summary>棋子被抓取</summary>
        public event Action<ChessPiece> OnChessPieceGrabbed;

        /// <summary>棋子被释放</summary>
        public event Action<ChessPiece> OnChessPieceReleased;

        /// <summary>棋子被移动</summary>
        public event Action<ChessPiece, Vector3> OnChessPieceMoved;

        #endregion

        #region 2D 沙盘交互事件

        /// <summary>单位被选中（2D 模式）</summary>
        public event Action<ChessPiece> OnUnitSelected;

        /// <summary>单位被命令（2D 模式）</summary>
        public event Action<ChessPiece, string> OnUnitCommanded;

        /// <summary>单位被选中（2D 纯ID版）</summary>
        public event Action<string> OnUnitSelectedById;

        /// <summary>单位被移动（2D 纯ID版）</summary>
        public event Action<string, Vector2> OnUnitMovedById;

        /// <summary>指令由UI发出</summary>
        public event Action<string, SWO1.Command.CommandType> OnUICommandIssued;

        /// <summary>频率切换</summary>
        public event Action<int> OnFrequencyChanged;

        #endregion

        #region 战役事件

        /// <summary>战役阶段变更</summary>
        public event Action<CampaignPhase> OnCampaignPhaseChanged;

        /// <summary>战役结束</summary>
        public event Action<GameOutcome> OnGameOutcomeChanged;

        /// <summary>游戏时间更新</summary>
        public event Action<float> OnGameTimeUpdated;

        #endregion

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 安全发布事件（可选的辅助方法）
        /// </summary>
        public void SafeInvoke(Action evt)
        {
            try { evt?.Invoke(); }
            catch (Exception e) { Debug.LogError($"[EventBus] 事件处理异常: {e}"); }
        }

        // === 发布方法（供外部类调用） ===
        public void PublishRadioReportDelivered(RadioReport report) => OnRadioReportDelivered?.Invoke(report);
        public void PublishRadioReportGenerated(RadioReport report) => OnRadioReportGenerated?.Invoke(report);
        public void PublishRadioCommandSent(RadioCommand cmd) => OnRadioCommandSent?.Invoke(cmd);
        public void PublishRadioInterference(RadioReport report) => OnRadioInterference?.Invoke(report);
        public void PublishAIDirectorIntelReceived(SWO1.Intelligence.AIDirectorIntelEvent evt) => OnAIDirectorIntelReceived?.Invoke(evt);
        public void PublishCommandStatusChanged(RadioCommand cmd, CommandStatus status) => OnCommandStatusChanged?.Invoke(cmd, status);
        public void PublishCommandDelivered(RadioCommand cmd) => OnCommandDelivered?.Invoke(cmd);
        public void PublishCommandLost(RadioCommand cmd) => OnCommandLost?.Invoke(cmd);
        public void PublishCommandMisinterpreted(RadioCommand cmd, string text) => OnCommandMisinterpreted?.Invoke(cmd, text);
        public void PublishSandTableUpdated(IntelligenceEntry entry) => OnSandTableUpdated?.Invoke(entry);
        public void PublishInteractionPerformed(InteractionEvent evt) => OnInteractionPerformed?.Invoke(evt);
        public void PublishCameraFocusChanged(FocusPoint fp) => OnCameraFocusChanged?.Invoke(fp);
        public void PublishChessPieceGrabbed(ChessPiece piece) => OnChessPieceGrabbed?.Invoke(piece);
        public void PublishChessPieceReleased(ChessPiece piece) => OnChessPieceReleased?.Invoke(piece);
        public void PublishChessPieceMoved(ChessPiece piece, Vector3 pos) => OnChessPieceMoved?.Invoke(piece, pos);
        public void PublishCampaignPhaseChanged(CampaignPhase phase) => OnCampaignPhaseChanged?.Invoke(phase);
        public void PublishGameOutcomeChanged(GameOutcome outcome) => OnGameOutcomeChanged?.Invoke(outcome);
        public void PublishGameTimeUpdated(float time) => OnGameTimeUpdated?.Invoke(time);
        public void PublishBattlefieldUpdated(BattlefieldData data) => OnBattlefieldUpdated?.Invoke(data);
        public void PublishUnitPositionChanged(UnitPositionData data) => OnUnitPositionChanged?.Invoke(data);
        public void PublishUnitSelected(ChessPiece piece) => OnUnitSelected?.Invoke(piece);
        public void PublishUnitCommanded(ChessPiece piece, string command) => OnUnitCommanded?.Invoke(piece, command);
        public void RaiseUnitSelected(string unitId) => OnUnitSelectedById?.Invoke(unitId);
        public void RaiseUnitMoved(string unitId, Vector2 pos) => OnUnitMovedById?.Invoke(unitId, pos);
        public void RaiseUICommandIssued(string unitId, SWO1.Command.CommandType cmdType) => OnUICommandIssued?.Invoke(unitId, cmdType);
        public void RaiseFrequencyChanged(int freq) => OnFrequencyChanged?.Invoke(freq);
    }

    #region 可视化数据模型

    /// <summary>
    /// 战场整体状态数据 — OnBattlefieldUpdated 事件载荷
    /// </summary>
    [Serializable]
    public class BattlefieldData
    {
        public int BridgeHP;
        public int BridgeMaxHP;
        public float GameTime;
        public int CurrentWave;
        public GameOutcome Outcome;
    }

    /// <summary>
    /// 单位位置数据 — OnUnitPositionChanged 事件载荷
    /// </summary>
    [Serializable]
    public class UnitPositionData
    {
        public string UnitId;
        public Vector3 WorldPosition;
        public Vector3 PreviousPosition;
        public bool IsEnemy;
        public float MoveSpeed;
        public int TroopCount;
        public float Morale;
    }

    #endregion
}
