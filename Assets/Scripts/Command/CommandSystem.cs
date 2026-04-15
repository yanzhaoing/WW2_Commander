// CommandSystem.cs — 指挥系统 (重构版)
// 替代旧的直接操控单位模式
// 指令通过无线电传达，有延迟/丢失/误解
// 每条指令有完整的生命周期状态追踪
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.CommandPost;
using SWO1.Intelligence;
using SWO1.Simulation;

namespace SWO1.Command
{
    #region 数据模型

    /// <summary>
    /// 指令类型
    /// </summary>
    public enum CommandType
    {
        Move,               // 移动 — "红一，向北移动至目标Alpha"
        Attack,             // 攻击 — "红一，攻击碉堡"
        Defend,             // 防御 — "红一，就地防御"
        Retreat,            // 撤退 — "红一，撤退至海堤"
        Recon,              // 侦察 — "红一，侦察东北方向"
        ArtilleryStrike,    // 炮击 — "蓝四，炮击坐标Grid-1234"
        StatusQuery,        // 状态查询 — "红一，报告状态"
        Supply,             // 补给 — "安排伤员后送"
        Custom              // 自定义指令
    }

    /// <summary>
    /// 指令状态 — 完整生命周期
    /// </summary>
    public enum CommandStatus
    {
        Draft,          // 草稿中（玩家正在编写）
        Sending,        // 正在发送（无线电传输中）
        InTransit,      // 传输中（已发出，未送达）
        Delivered,      // 已送达（前线收到）
        Acknowledged,   // 已确认（前线回复"收到"）
        Executing,      // 执行中
        Completed,      // 已完成
        Lost,           // 丢失（无线电干扰未送达）
        Failed          // 失败（执行失败）
    }

    /// <summary>
    /// 无线电指令数据模型
    /// 包含指令的完整生命周期信息
    /// </summary>
    [Serializable]
    public class RadioCommand
    {
        public string CommandId;            // 唯一ID (GUID)
        public string TargetUnitId;         // 目标部队ID
        public int TargetFrequency;         // 目标频率 (1-5)
        public CommandType Type;            // 指令类型
        public string Content;              // 指令内容文本
        public float SentTime;              // 发送时间 (游戏时间)
        public float? DeliveryTime;         // 实际送达时间
        public CommandStatus Status;        // 当前状态
        public float EstimatedDelay;        // 预计延迟 (秒)
        public bool IsLost;                 // 是否丢失
        public string Misinterpretation;    // 误解后的指令文本
        public string AcknowledgmentText;   // 前线确认回复文本

        public RadioCommand()
        {
            CommandId = Guid.NewGuid().ToString();
            Status = CommandStatus.Draft;
        }

        public RadioCommand(string unitId, int frequency, CommandType type, string content)
        {
            CommandId = Guid.NewGuid().ToString();
            TargetUnitId = unitId;
            TargetFrequency = frequency;
            Type = type;
            Content = content;
            Status = CommandStatus.Draft;
        }
    }

    /// <summary>
    /// 指令确认回复
    /// </summary>
    public class CommandAcknowledgment
    {
        public string CommandId;
        public string UnitId;
        public string ResponseText;         // "收到，执行中" 之类
        public float ResponseTime;
        public bool IsPartial;              // 是否部分理解
    }

    #endregion

    /// <summary>
    /// 指挥系统 — 无线电指令传达
    /// 
    /// 核心机制：
    /// 1. 指令不是即时送达的，有传输延迟
    /// 2. 指令可能被干扰丢失
    /// 3. 指令可能被前线误解（特别是士气低时）
    /// 4. "收到" ≠ "理解" ≠ "执行"
    /// 
    /// 设计原则：
    /// - 异步：使用协程模拟延迟
    /// - 事件驱动：通过事件总线通知状态变化
    /// - 可追踪：每条指令有完整状态历史
    /// </summary>
    public class CommandSystem : MonoBehaviour
    {
        public static CommandSystem Instance { get; private set; }

        [Header("传输参数")]
        [Tooltip("基础传输延迟（秒）")]
        [SerializeField] private float baseDeliveryTime = 30f;

        [Tooltip("简单指令额外延迟范围")]
        [SerializeField] private Vector2 simpleDelayRange = new Vector2(30f, 60f);

        [Tooltip("复杂指令额外延迟范围")]
        [SerializeField] private Vector2 complexDelayRange = new Vector2(60f, 180f);

        [Header("难度参数")]
        [Tooltip("各难度的指令丢失率 [Easy, Normal, Hard]")]
        [SerializeField] private float[] lossChanceByDifficulty = { 0.05f, 0.15f, 0.30f };

        [Tooltip("各难度的延迟乘数 [Easy, Normal, Hard]")]
        [SerializeField] private float[] delayMultiplierByDifficulty = { 0.7f, 1.0f, 1.8f };

        [Header("误解参数")]
        [Tooltip("各士气等级的误解概率 [高昂, 正常, 动摇, 崩溃]")]
        [SerializeField] private float[] misinterpretChanceByMorale = { 0.05f, 0.12f, 0.25f, 0.45f };

        [Header("确认参数")]
        [Tooltip("前线不回复的概率")]
        [SerializeField] private float noReplyChance = 0.15f;

        // === 状态 ===
        private Dictionary<string, RadioCommand> pendingCommands = new Dictionary<string, RadioCommand>();
        private List<RadioCommand> commandHistory = new List<RadioCommand>();

        // === 事件 ===
        /// <summary>指令已发出（正在传输）</summary>
        public event Action<RadioCommand> OnCommandDispatched;

        /// <summary>指令已送达前线</summary>
        public event Action<RadioCommand> OnCommandDelivered;

        /// <summary>指令丢失</summary>
        public event Action<RadioCommand> OnCommandLost;

        /// <summary>指令被误解（附带误解后的指令）</summary>
        public event Action<RadioCommand, string> OnCommandMisinterpreted;

        /// <summary>前线确认收到</summary>
        public event Action<CommandAcknowledgment> OnCommandAcknowledged;

        /// <summary>指令执行完成</summary>
        public event Action<RadioCommand> OnCommandCompleted;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            // 订阅输入系统的交互事件
            if (InputSystem.Instance != null)
            {
                InputSystem.Instance.OnInteraction += HandleInteraction;
            }
        }

        void OnDisable()
        {
            if (InputSystem.Instance != null)
            {
                InputSystem.Instance.OnInteraction -= HandleInteraction;
            }
        }

        #region 核心指令流程

        /// <summary>
        /// 发送指令 — 核心入口
        /// 
        /// 流程：
        /// 1. 创建指令
        /// 2. 计算传输延迟
        /// 3. 启动协程模拟异步传输
        /// 4. 在传输过程中可能丢失或被误解
        /// </summary>
        public RadioCommand SendCommand(string unitId, int frequency, CommandType type, string content)
        {
            var cmd = new RadioCommand(unitId, frequency, type, content);
            cmd.SentTime = GameDirector.Instance != null
                ? GameDirector.Instance.CurrentGameTime
                : Time.time;

            // 计算预计延迟
            cmd.EstimatedDelay = CalculateDelay(type);

            // 状态变为 Sending
            cmd.Status = CommandStatus.Sending;
            pendingCommands[cmd.CommandId] = cmd;
            commandHistory.Add(cmd);

            // 发布事件
            OnCommandDispatched?.Invoke(cmd);
            PublishToEventBus(cmd, CommandStatus.Sending);

            Debug.Log($"[Command] 发送指令 → {unitId} (频率{frequency}): {content} [预计延迟: {cmd.EstimatedDelay:F0}s]");

            // 启动异步传输
            StartCoroutine(SimulateDelivery(cmd));

            return cmd;
        }

        /// <summary>
        /// 重发丢失的指令
        /// </summary>
        public void RetryCommand(string commandId)
        {
            if (!pendingCommands.TryGetValue(commandId, out var originalCmd))
            {
                Debug.LogWarning($"[Command] 找不到指令: {commandId}");
                return;
            }

            if (originalCmd.Status != CommandStatus.Lost)
            {
                Debug.LogWarning($"[Command] 指令未丢失，无需重发: {commandId}");
                return;
            }

            // 创建新指令（内容相同，新ID）
            SendCommand(
                originalCmd.TargetUnitId,
                originalCmd.TargetFrequency,
                originalCmd.Type,
                originalCmd.Content
            );

            Debug.Log($"[Command] 重发指令: {originalCmd.Content}");
        }

        /// <summary>
        /// 取消待发指令
        /// </summary>
        public void CancelCommand(string commandId)
        {
            if (pendingCommands.TryGetValue(commandId, out var cmd))
            {
                if (cmd.Status == CommandStatus.Sending || cmd.Status == CommandStatus.InTransit)
                {
                    cmd.Status = CommandStatus.Failed;
                    pendingCommands.Remove(commandId);
                    Debug.Log($"[Command] 已取消指令: {commandId}");
                }
            }
        }

        #endregion

        #region 异步传输模拟

        /// <summary>
        /// 模拟指令的无线电传输过程
        /// 
        /// 流程：
        /// 1. 进入 InTransit 状态
        /// 2. 等待延迟时间
        /// 3. Roll 丢失概率
        /// 4. Roll 误解概率
        /// 5. 最终送达或丢失
        /// </summary>
        private IEnumerator SimulateDelivery(RadioCommand cmd)
        {
            // 进入传输中
            cmd.Status = CommandStatus.InTransit;
            PublishToEventBus(cmd, CommandStatus.InTransit);

            // 等待传输延迟
            yield return new WaitForSeconds(cmd.EstimatedDelay);

            // === 丢失检查 ===
            float lossChance = GetLossChance();
            if (UnityEngine.Random.value < lossChance)
            {
                // 指令丢失
                cmd.Status = CommandStatus.Lost;
                cmd.IsLost = true;
                pendingCommands.Remove(cmd.CommandId);

                OnCommandLost?.Invoke(cmd);
                PublishToEventBus(cmd, CommandStatus.Lost);

                Debug.Log($"[Command] 指令丢失! → {cmd.TargetUnitId}: {cmd.Content}");
                yield break;
            }

            // === 误解检查 ===
            float morale = GetUnitMorale(cmd.TargetUnitId);
            float misinterpretChance = GetMisinterpretChance(morale);

            if (UnityEngine.Random.value < misinterpretChance)
            {
                // 指令被误解
                string misinterpreted = GenerateMisinterpretation(cmd);
                cmd.Misinterpretation = misinterpreted;
                cmd.Status = CommandStatus.Delivered; // 误解的也算送达了

                OnCommandMisinterpreted?.Invoke(cmd, misinterpreted);
                PublishMisinterpretedToEventBus(cmd, misinterpreted);

                Debug.Log($"[Command] 指令被误解! 原始: {cmd.Content} → 理解为: {misinterpreted}");

                // 前线仍然可能回复（但回复的是误解后的理解）
                yield return StartCoroutine(SimulateAcknowledgment(cmd, true));
                yield break;
            }

            // === 正常送达 ===
            cmd.Status = CommandStatus.Delivered;
            cmd.DeliveryTime = GameDirector.Instance != null
                ? GameDirector.Instance.CurrentGameTime
                : Time.time;

            OnCommandDelivered?.Invoke(cmd);
            PublishToEventBus(cmd, CommandStatus.Delivered);

            Debug.Log($"[Command] 指令已送达 → {cmd.TargetUnitId}: {cmd.Content}");

            // 模拟前线确认回复
            yield return StartCoroutine(SimulateAcknowledgment(cmd, false));
        }

        /// <summary>
        /// 模拟前线确认回复
        /// "收到" 不代表 "理解" 更不代表 "执行"
        /// </summary>
        private IEnumerator SimulateAcknowledgment(RadioCommand cmd, bool wasMisinterpreted)
        {
            // 确认回复也有延迟
            float replyDelay = UnityEngine.Random.Range(5f, 30f);
            yield return new WaitForSeconds(replyDelay);

            // 前线可能不回复
            if (UnityEngine.Random.value < noReplyChance)
            {
                Debug.Log($"[Command] 前线未回复 → {cmd.TargetUnitId}（可能正忙于战斗）");
                yield break;
            }

            // 生成确认回复
            var ack = new CommandAcknowledgment
            {
                CommandId = cmd.CommandId,
                UnitId = cmd.TargetUnitId,
                ResponseTime = GameDirector.Instance != null
                    ? GameDirector.Instance.CurrentGameTime
                    : Time.time,
                IsPartial = wasMisinterpreted
            };

            if (wasMisinterpreted)
            {
                ack.ResponseText = GenerateMisinterpretAck(cmd);
            }
            else
            {
                ack.ResponseText = GenerateNormalAck(cmd);
            }

            cmd.Status = CommandStatus.Acknowledged;
            cmd.AcknowledgmentText = ack.ResponseText;

            OnCommandAcknowledged?.Invoke(ack);
            PublishToEventBus(cmd, CommandStatus.Acknowledged);

            Debug.Log($"[Command] 前线回复 → {cmd.TargetUnitId}: \"{ack.ResponseText}\"");

            // 确认后进入执行状态，回调 BattleSimulator 执行数值效果
            yield return new WaitForSeconds(UnityEngine.Random.Range(10f, 60f));
            cmd.Status = CommandStatus.Executing;
            PublishToEventBus(cmd, CommandStatus.Executing);

            // 回调 BattleSimulator，根据指令类型执行对应数值方法
            if (BattleSimulator.Instance != null)
            {
                BattleSimulator.Instance.ExecuteCommand(cmd);
            }
            else
            {
                Debug.LogWarning("[Command] BattleSimulator.Instance 为空，无法执行指令数值效果");
            }
        }

        #endregion

        #region 计算逻辑

        /// <summary>
        /// 计算指令传输延迟
        /// </summary>
        private float CalculateDelay(CommandType type)
        {
            float difficultyMultiplier = GetDifficultyMultiplier();
            float typeDelay;

            // 简单指令 vs 复杂指令
            switch (type)
            {
                case CommandType.StatusQuery:
                case CommandType.Supply:
                    typeDelay = UnityEngine.Random.Range(simpleDelayRange.x, simpleDelayRange.y);
                    break;

                case CommandType.Move:
                case CommandType.Attack:
                case CommandType.Retreat:
                    typeDelay = UnityEngine.Random.Range(complexDelayRange.x, complexDelayRange.y);
                    break;

                case CommandType.ArtilleryStrike:
                    typeDelay = UnityEngine.Random.Range(45f, 90f);
                    break;

                default:
                    typeDelay = UnityEngine.Random.Range(simpleDelayRange.x, complexDelayRange.y);
                    break;
            }

            return baseDeliveryTime * difficultyMultiplier + typeDelay;
        }

        /// <summary>
        /// 获取当前难度的指令丢失率
        /// </summary>
        private float GetLossChance()
        {
            if (GameDirector.Instance == null) return lossChanceByDifficulty[1];

            int diffIndex = (int)GameDirector.Instance.difficulty;
            diffIndex = Mathf.Clamp(diffIndex, 0, lossChanceByDifficulty.Length - 1);
            return lossChanceByDifficulty[diffIndex];
        }

        /// <summary>
        /// 获取当前难度的延迟乘数
        /// </summary>
        private float GetDifficultyMultiplier()
        {
            if (GameDirector.Instance == null) return delayMultiplierByDifficulty[1];

            int diffIndex = (int)GameDirector.Instance.difficulty;
            diffIndex = Mathf.Clamp(diffIndex, 0, delayMultiplierByDifficulty.Length - 1);
            return delayMultiplierByDifficulty[diffIndex];
        }

        /// <summary>
        /// 根据士气获取误解概率
        /// </summary>
        private float GetMisinterpretChance(float morale)
        {
            int moraleIndex;
            if (morale >= 80) moraleIndex = 0;      // 高昂
            else if (morale >= 50) moraleIndex = 1;  // 正常
            else if (morale >= 30) moraleIndex = 2;  // 动摇
            else moraleIndex = 3;                     // 崩溃

            return misinterpretChanceByMorale[moraleIndex];
        }

        /// <summary>
        /// 获取目标部队的士气（从模拟系统）
        /// </summary>
        private float GetUnitMorale(string unitId)
        {
            if (BattleSimulationInterface.Instance != null)
            {
                return BattleSimulationInterface.Instance.GetUnitMorale(unitId);
            }
            // 模拟系统不可用时返回中等士气
            return 60f;
        }

        #endregion

        #region 误解生成

        /// <summary>
        /// 根据原始指令生成误解版本
        /// 模拟前线指挥官在混乱中理解错误
        /// </summary>
        private string GenerateMisinterpretation(RadioCommand cmd)
        {
            // 误解规则表
            return cmd.Type switch
            {
                CommandType.Move => MisinterpretMove(cmd),
                CommandType.Attack => MisinterpretAttack(cmd),
                CommandType.Defend => cmd.Content, // 防御指令一般不会误解
                CommandType.Retreat => cmd.Content.Replace("撤退", "原地待命"),
                CommandType.ArtilleryStrike => cmd.Content + "（坐标可能有误）",
                _ => cmd.Content + "（理解不确定）"
            };
        }

        private string MisinterpretMove(RadioCommand cmd)
        {
            string[] misinterpretations = new string[]
            {
                cmd.Content + "（方向可能偏差15°）",
                cmd.Content.Replace("北", "西北"),
                cmd.Content.Replace("向", "缓慢向"),
                cmd.Content + "（目标点可能理解错误）"
            };
            return misinterpretations[UnityEngine.Random.Range(0, misinterpretations.Length)];
        }

        private string MisinterpretAttack(RadioCommand cmd)
        {
            string[] misinterpretations = new string[]
            {
                cmd.Content.Replace("攻击", "绕过"),
                cmd.Content + "（火力不足，改为牵制）",
                cmd.Content + "（等待增援后执行）"
            };
            return misinterpretations[UnityEngine.Random.Range(0, misinterpretations.Length)];
        }

        /// <summary>
        /// 生成正常确认回复
        /// </summary>
        private string GenerateNormalAck(RadioCommand cmd)
        {
            string[] acks = cmd.Type switch
            {
                CommandType.Move => new string[] { "收到，正在向目标移动", "明白，移动中", "收到指令，出发" },
                CommandType.Attack => new string[] { "收到，准备攻击", "明白，开始进攻", "收到，正在接敌" },
                CommandType.Defend => new string[] { "收到，就地防御", "明白，构筑工事", "收到，坚守阵地" },
                CommandType.Retreat => new string[] { "收到，正在撤退", "明白，向后移动" },
                CommandType.StatusQuery => new string[] { "收到，准备汇报", "明白，正在整理信息" },
                CommandType.ArtilleryStrike => new string[] { "收到，炮击准备中", "明白，坐标确认" },
                _ => new string[] { "收到，执行中", "明白" }
            };
            return acks[UnityEngine.Random.Range(0, acks.Length)];
        }

        /// <summary>
        /// 生成误解后的确认回复
        /// </summary>
        private string GenerateMisinterpretAck(RadioCommand cmd)
        {
            return cmd.Type switch
            {
                CommandType.Move => "收到，但我们认为目标方向不太对...",
                CommandType.Attack => "收到，但我们火力不足，改为牵制行动",
                CommandType.Retreat => "收到...但我们认为还可以坚守",
                _ => "收到...请确认指令内容"
            };
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 处理输入系统的交互事件
        /// 当玩家按下通话键时创建指令
        /// </summary>
        private void HandleInteraction(InteractionEvent evt)
        {
            if (evt.type == InteractionType.RadioTransmit)
            {
                // 通话键被按下
                // 此时应由 UISystem 提供指令内容
                // 这里只记录频率
                int frequency = evt.intValue > 0 ? evt.intValue : 1;
                Debug.Log($"[Command] 通话键按下 (频率{frequency}) — 等待指令内容...");
            }
        }

        /// <summary>
        /// 发布状态变更到全局事件总线
        /// 同时发布具体事件（送达/丢失/误解）供 BattleSimulator 监听
        /// </summary>
        private void PublishToEventBus(RadioCommand cmd, CommandStatus status)
        {
            if (GameEventBus.Instance != null)
            {
                // 发布通用状态变更事件
                GameEventBus.Instance.PublishCommandStatusChanged(cmd, status);

                // 发布具体事件，供 BattleSimulator 等模块直接监听
                switch (status)
                {
                    case CommandStatus.Delivered:
                        GameEventBus.Instance.PublishCommandDelivered(cmd);
                        break;
                    case CommandStatus.Lost:
                        GameEventBus.Instance.PublishCommandLost(cmd);
                        break;
                }
            }
        }

        /// <summary>
        /// 发布误解事件到全局事件总线
        /// </summary>
        private void PublishMisinterpretedToEventBus(RadioCommand cmd, string misinterpreted)
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishCommandStatusChanged(cmd, CommandStatus.Delivered);
                GameEventBus.Instance.PublishCommandMisinterpreted(cmd, misinterpreted);
            }
        }

        #endregion

        #region 查询接口

        /// <summary>
        /// 获取指定部队的指令历史
        /// </summary>
        public List<RadioCommand> GetCommandHistory(string unitId)
        {
            return commandHistory.FindAll(c => c.TargetUnitId == unitId);
        }

        /// <summary>
        /// 获取所有待处理的指令
        /// </summary>
        public List<RadioCommand> GetPendingCommands()
        {
            return new List<RadioCommand>(pendingCommands.Values);
        }

        /// <summary>
        /// 获取最近 N 条指令
        /// </summary>
        public List<RadioCommand> GetRecentCommands(int count)
        {
            int start = Mathf.Max(0, commandHistory.Count - count);
            return commandHistory.GetRange(start, Mathf.Min(count, commandHistory.Count - start));
        }

        /// <summary>
        /// 标记指令为已完成（由模拟系统调用）
        /// </summary>
        public void MarkCommandCompleted(string commandId)
        {
            if (pendingCommands.TryGetValue(commandId, out var cmd))
            {
                cmd.Status = CommandStatus.Completed;
                pendingCommands.Remove(commandId);
                OnCommandCompleted?.Invoke(cmd);
                PublishToEventBus(cmd, CommandStatus.Completed);
            }
        }

        #endregion
    }
}
