using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.AI;
using SWO1.Map;

namespace SWO1.Core
{
    /// <summary>
    /// 回合阶段枚举
    /// </summary>
    public enum TurnPhase
    {
        BattleReport,    // 显示战报
        PlayerCommand,   // 玩家输入指令
        AIThinking,      // AI 将军正在思考（显示加载）
        AIActions,       // AI 将军行动（部队移动）
        EnemyTurn,       // 敌军回合
        Settlement       // 结算检查
    }

    /// <summary>
    /// TurnManager - 回合制游戏核心控制器
    /// 
    /// 职责：
    /// - 控制回合制游戏循环：战报→玩家回合→AI回合→敌军回合→结算
    /// - 管理AI将军的决策流程
    /// - 协调各单位行动
    /// </summary>
    public class TurnManager : MonoBehaviour
    {
        public static TurnManager Instance { get; private set; }

        #region 配置常量

        public const int MaxTurns = 30;              // 总回合数
        public const int MaxMovePerTurn = 2;         // 每回合每单位最大移动格数

        #endregion

        #region 状态

        public int CurrentTurn { get; private set; } = 1;
        public TurnPhase CurrentPhase { get; private set; } = TurnPhase.BattleReport;
        public bool IsGameOver { get; private set; } = false;

        /// <summary>将军名→指令</summary>
        public Dictionary<string, string> PendingCommands { get; private set; } = new Dictionary<string, string>();

        /// <summary>将军列表（隆美尔、曼施坦因、古德里安）</summary>
        private List<GeneralAI> _generals = new List<GeneralAI>();

        /// <summary>当前处理的将军索引</summary>
        private int _currentGeneralIndex = 0;

        #endregion

        #region 事件

        public event Action<int> OnTurnStarted;              // 新回合开始
        public event Action<TurnPhase> OnPhaseChanged;       // 阶段变化
        public event Action<string, string> OnGeneralReplied; // 将军回复 (将军名, 回复文本)
        public event Action<bool, string> OnGameOver;        // 游戏结束 (胜利?, 原因)
        public event Action<string> OnStatusUpdate;          // 状态更新消息
        public event Action<bool> OnCommandPopup;            // 弹窗显示/隐藏 (true=显示)

        #endregion

        #region 依赖引用

        [SerializeField] private MapGenerator _mapGenerator;
        private GameMap _gameMap;

        #endregion

        #region Unity生命周期

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (_mapGenerator == null)
            {
                _mapGenerator = FindFirstObjectByType<MapGenerator>();
            }

            if (_mapGenerator != null)
            {
                _gameMap = _mapGenerator.CurrentMap;
            }

            InitializeGenerals();
            StartCoroutine(GameLoop());
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region 初始化

        private void InitializeGenerals()
        {
            _generals.Clear();
            
            // 查找场景中的3个将军AI
            var allGenerals = FindObjectsByType<GeneralAI>(FindObjectsSortMode.None);
            foreach (var general in allGenerals)
            {
                _generals.Add(general);
            }

            if (_generals.Count == 0)
            {
                Debug.LogWarning("[TurnManager] 未找到任何将军AI组件");
            }
            else
            {
                Debug.Log($"[TurnManager] 已初始化 {_generals.Count} 个将军");
            }
        }

        #endregion

        #region 核心流程

        // 弹窗等待
        public static bool WaitingForPlayerInput = false;

        /// <summary>
        /// 主游戏循环协程
        /// </summary>
        private IEnumerator GameLoop()
        {
            // 首回合：等一帧让 GameUIBinder 订阅事件
            yield return null;

            while (!IsGameOver)
            {
                // 1. 等待玩家指令
                SetPhase(TurnPhase.PlayerCommand);
                ShowCommandPopup();
                WaitingForPlayerInput = true;
                while (WaitingForPlayerInput) yield return null;

                // 2. 执行本回合（用刚收到的命令）
                yield return StartCoroutine(Phase_Execute());
                if (IsGameOver) yield break;

                // 3. 结算
                yield return StartCoroutine(Phase_Settlement());
                if (IsGameOver) yield break;

                // 4. 将军汇报（汇报刚执行的回合结果）
                yield return StartCoroutine(Phase_BattleReport());
                if (IsGameOver) yield break;

                // 5. 进入下一回合
                NextTurn();
            }
        }

        void ExecuteGeneralMovements()
        {
            var gc = FindFirstObjectByType<SWO1.Core.GameController2D>();
            string mapData = "地图数据不可用";
            string unitStatus = "部队状态不可用";
            if (gc != null)
            {
                var friendlies = gc.GetFriendlies();
                var sb = new System.Text.StringBuilder();
                foreach (var u in friendlies)
                {
                    if (u.IsActive)
                        sb.AppendLine($"{u.Name}: {(char)('A' + u.GridX)}{u.GridY+1}, 兵力{u.Strength}");
                }
                unitStatus = sb.Length > 0 ? sb.ToString() : "无友军单位";
                mapData = $"地图16x12网格(A-P列,1-12行)";
            }

            foreach (var general in _generals)
            {
                // 获取给这个将军的新命令
                string cmd = null;
                if (PendingCommands.TryGetValue(general.GeneralName, out string c1) && !string.IsNullOrEmpty(c1))
                    cmd = c1;
                else if (PendingCommands.TryGetValue("全体", out string c2) && !string.IsNullOrEmpty(c2))
                    cmd = c2;

                if (cmd != null)
                {
                    // 新命令 → 调用 LLM 决策
                    Debug.Log($"[TurnManager] 向 {general.GeneralName} 下达新命令: {cmd}");
                    bool decided = false;
                    general.MakeDecision(cmd, mapData, unitStatus, (decision) =>
                    {
                        general.LastDecision = decision;
                        if (decision.Orders != null && decision.Orders.Count > 0)
                        {
                            general.CurrentTarget = new Vector2(decision.Orders[0].TargetX, decision.Orders[0].TargetY);
                            general.HasActiveTask = true;
                            Debug.Log($"[TurnManager] {general.GeneralName} 决定前往 ({(char)('A'+decision.Orders[0].TargetX)}{decision.Orders[0].TargetY+1})");
                        }
                        decided = true;
                    });
                    // 等待决策完成（最多30秒）
                    float t = 0;
                    while (!decided && t < 30f) { t += Time.deltaTime; }
                }

                // 向目标移动一格
                if (general.HasActiveTask && general.CurrentTarget.x >= 0)
                {
                    int cx = (int)general.GridPosition.x;
                    int cy = (int)general.GridPosition.y;
                    int tx = (int)general.CurrentTarget.x;
                    int ty = (int)general.CurrentTarget.y;
                    int dx = System.Math.Sign(tx - cx);
                    int dy = System.Math.Sign(ty - cy);
                    if (dx != 0 || dy != 0)
                    {
                        int nx = Mathf.Clamp(cx + dx, 0, 31);
                        int ny = Mathf.Clamp(cy + dy, 0, 23);
                        general.GridPosition = new Vector2(nx, ny);
                        Debug.Log($"[TurnManager] {general.GeneralName} 移动到 {(char)('A'+nx)}{ny+1}");
                    }
                    else
                    {
                        Debug.Log($"[TurnManager] {general.GeneralName} 已到达目标 {(char)('A'+tx)}{ty+1}");
                        general.HasActiveTask = false;
                    }
                }
            }
        }


        void CollectGeneralIntel()
        {
            var es = FindFirstObjectByType<SWO1.Simulation.EnemySpawner>();
            if (es == null) return;

            var enemies = es.GetActiveEnemies();

            foreach (var general in _generals)
            {
                var sb = new System.Text.StringBuilder();
                int gx = (int)general.GridPosition.x;
                int gy = (int)general.GridPosition.y;

                foreach (var e in enemies)
                {
                    int dist = System.Math.Abs(e.GridX - gx) + System.Math.Abs(e.GridY - gy);
                    if (dist <= 5)
                    {
                        string coord = $"{(char)('A' + e.GridX)}{e.GridY + 1}";
                        string dir;
                        int dx = e.GridX - gx;
                        int dy = e.GridY - gy;
                        if (System.Math.Abs(dx) > System.Math.Abs(dy))
                            dir = dx > 0 ? "东" : "西";
                        else
                            dir = dy > 0 ? "北" : "南";

                        if (dist <= 2)
                            sb.AppendLine($"{dir}方{coord}方向确认有敌军活动，距离{dist}格");
                        else if (dist <= 4)
                            sb.AppendLine($"{dir}方{coord}附近疑似有敌军出没");
                        else
                            sb.AppendLine($"远处{dir}方有动静，不确定是否敌军");
                    }
                }

                general.LatestIntel = sb.Length > 0 ? sb.ToString() : "前方区域暂无敌情";
                Debug.Log($"[侦察] {general.GeneralName}: {general.LatestIntel.Trim()}");
            }
        }

        /// <summary>
        /// 执行阶段：移动、战斗、敌军
        /// </summary>
        private IEnumerator Phase_Execute()
        {
            SetPhase(TurnPhase.AIActions);
            OnStatusUpdate?.Invoke($"第 {CurrentTurn} 回合执行中...");

            // 执行将军移动命令
            ExecuteGeneralMovements();
            CollectGeneralIntel();

            // 执行战斗 + 敌军（由 GameController2D）
            var gc = FindFirstObjectByType<SWO1.Core.GameController2D>();
            if (gc != null)
            {
                gc.ExecuteTurn();
            }

            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// 战报阶段：将军汇报战况 + 侦察报告
        /// </summary>
        private IEnumerator Phase_BattleReport()
        {
            SetPhase(TurnPhase.BattleReport);
            OnStatusUpdate?.Invoke($"第 {CurrentTurn} 回合战报");

            // 获取战斗结果
            var gc = FindFirstObjectByType<SWO1.Core.GameController2D>();
            var results = gc != null ? gc.GetBattleResults() : new List<SWO1.Core.BattleResult>();
            var friendlies = gc != null ? gc.GetFriendlies() : new List<SWO1.Core.FriendlyUnit>();

            // 构造战场全貌（供将军在汇报中口述）
            var battlefieldSb = new System.Text.StringBuilder();
            if (results.Count > 0)
            {
                foreach (var r in results)
                {
                    if (r.Victory)
                        battlefieldSb.AppendLine($"{r.UnitName}在{(char)('A' + r.EnemyX)}{r.EnemyY + 1}击退敌军，伤亡{r.Damage}人");
                    else
                        battlefieldSb.AppendLine($"{r.UnitName}在{(char)('A' + r.EnemyX)}{r.EnemyY + 1}与敌军激战，损失{r.Damage}人，剩余{r.RemainingStrength}人");
                }
            }
            else
            {
                battlefieldSb.AppendLine("本回合前线无交火，局势平静");
            }
            string battlefieldInfo = battlefieldSb.ToString();

            // 让每个将军通过 LLM 汇报战况
            foreach (var general in _generals)
            {
                // 生成将军汇报（基于当前位置 + 最新决策）
                string report = "（暂无汇报）";
                var llm = general.GetComponent<SWO1.AI.LLMClient>();
                if (llm == null) llm = FindFirstObjectByType<SWO1.AI.LLMClient>();

                // 构造战场全貌
                var reportSb = new System.Text.StringBuilder();
                reportSb.AppendLine($"战场全貌：");
                reportSb.AppendLine(battlefieldInfo);
                reportSb.AppendLine();
                string posStr = $"{(char)('A' + (int)general.GridPosition.x)}{(int)general.GridPosition.y + 1}";
                reportSb.AppendLine($"你的当前位置：{posStr}");
                if (general.HasActiveTask)
                {
                    string tgt = $"{(char)('A' + (int)general.CurrentTarget.x)}{(int)general.CurrentTarget.y + 1}";
                    reportSb.AppendLine($"你的当前任务：前往 {tgt}");
                }
                string reportContext = reportSb.ToString();

                if (llm != null)
                {
                    bool reportDone = false;
                    string prompt = $"你是{general.GeneralName}。请用中文向总司令做口头战况汇报。\n\n" +
                        $"【战场信息】\n{reportContext}\n\n" +
                        $"请像在无线电里说话一样汇报：1）你当前的位置和状态 2）战场发生了什么 3）你的下一步计划。必须提到你的具体位置。不少于80字。";

                    llm.Chat("你是二战德军将军，请用中文口语化汇报。", prompt,
                        (resp) => { report = resp; reportDone = true; },
                        (err) => { report = "（通信故障）"; reportDone = true; });

                    float t2 = 0;
                    while (!reportDone && t2 < 30f) { t2 += Time.deltaTime; yield return null; }
                    if (!reportDone) report = "（超时）";
                }

                OnGeneralReplied?.Invoke(general.GeneralName, report);
            }

            yield return new WaitForSeconds(0.5f);
        }
        /// AI思考阶段 - AI将军分析战场情况
        /// </summary>
        private IEnumerator Phase_AIThinking()
        {
            SetPhase(TurnPhase.AIThinking);
            OnStatusUpdate?.Invoke("将军们正在思考...");

            // 显示思考状态
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>
        /// AI行动阶段 - 执行AI将军的决策（三个将军并行调用LLM）
        /// </summary>
        private IEnumerator Phase_AIActions()
        {
            SetPhase(TurnPhase.AIActions);
            OnStatusUpdate?.Invoke("将军们正在汇报...");

            // 并行：同时给所有将军发决策请求
            int pendingCount = _generals.Count;
            var decisions = new Dictionary<GeneralAI, GeneralDecision>();

            foreach (var general in _generals)
            {
                string playerCommand = GetPendingCommandForGeneral(general.GeneralName);
                string visibleMapData = CollectVisibleMapData(general);
                string unitStatus = CollectUnitStatus(general);

                general.MakeDecision(
                    playerCommand,
                    visibleMapData,
                    unitStatus,
                    (result) =>
                    {
                        decisions[general] = result;
                        pendingCount--;
                    }
                );
            }

            // 等待所有将军决策完成
            while (pendingCount > 0)
            {
                yield return null;
            }

            // 按顺序处理结果（保持汇报顺序一致）
            foreach (var general in _generals)
            {
                if (decisions.TryGetValue(general, out var decision))
                {
                    OnGeneralDecided(general, decision);
                }
            }

            PendingCommands.Clear();
        }

        /// <summary>
        /// 敌军回合阶段 - 执行敌军AI行动
        /// </summary>
        private IEnumerator Phase_EnemyTurn()
        {
            SetPhase(TurnPhase.EnemyTurn);
            OnStatusUpdate?.Invoke("敌军行动中...");

            ExecuteEnemyTurn();

            yield return new WaitForSeconds(1.0f);
        }

        /// <summary>
        /// 结算阶段 - 检查游戏胜负条件
        /// </summary>
        private IEnumerator Phase_Settlement()
        {
            SetPhase(TurnPhase.Settlement);
            OnStatusUpdate?.Invoke("回合结算中...");

            var gc = FindFirstObjectByType<SWO1.Core.GameController2D>();
            if (gc != null)
            {
                var units = gc.GetFriendlies();
                bool anyAlive = false;
                foreach (var u in units) { if (u.IsActive) { anyAlive = true; break; } }
                if (!anyAlive) { EndGame(false, "全军覆没..."); yield break; }
            }

            yield return new WaitForSeconds(0.3f);
        }

        void ShowCommandPopup() => OnCommandPopup?.Invoke(true);
        void HideCommandPopup() => OnCommandPopup?.Invoke(false);

        /// <summary>供 GameUIBinder 调用：玩家点击"下达指令"或"下一回合"</summary>
        public void OnPlayerReady()
        {
            HideCommandPopup();
            WaitingForPlayerInput = false;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 玩家点击"下一回合"时调用
        /// </summary>
        public void NextTurn()
        {
            if (IsGameOver) return;

            CurrentTurn++;
            
            OnTurnStarted?.Invoke(CurrentTurn);
            OnStatusUpdate?.Invoke($"第 {CurrentTurn} 回合开始");

            Debug.Log($"[TurnManager] 第 {CurrentTurn} 回合开始");
        }

        /// <summary>
        /// 玩家下达指令时调用
        /// </summary>
        public void SubmitCommand(string generalName, string command)
        {
            if (string.IsNullOrEmpty(generalName) || command == null)
            {
                Debug.LogWarning("[TurnManager] 无效的指令提交");
                return;
            }

            PendingCommands[generalName] = command;
            OnStatusUpdate?.Invoke($"已向 {generalName} 下达指令");
            
            Debug.Log($"[TurnManager] 向将军 {generalName} 下达指令: {command}");
        }

        #endregion

        #region AI回合处理

        /// <summary>
        /// 获取指定将军的玩家指令
        /// </summary>
        private string GetPendingCommandForGeneral(string generalName)
        {
            if (PendingCommands.TryGetValue(generalName, out string command))
            {
                return command;
            }
            return string.Empty;
        }

        /// <summary>
        /// 收集指定将军的视野地图数据
        /// </summary>
        private string CollectVisibleMapData(GeneralAI general)
        {
            if (_gameMap == null || general == null)
            {
                return "地图数据不可用";
            }

            // 获取将军的视野范围
            int visionRange = general.VisionRange;
            Vector2Int generalPos = new Vector2Int((int)general.GridPosition.x, (int)general.GridPosition.y);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"将军位置: {_mapGenerator.GetCoordinate(generalPos.x, generalPos.y)}");
            sb.AppendLine($"视野范围: {visionRange}格");
            sb.AppendLine("可见区域:");

            // 收集视野内的格子信息
            for (int dx = -visionRange; dx <= visionRange; dx++)
            {
                for (int dy = -visionRange; dy <= visionRange; dy++)
                {
                    int x = generalPos.x + dx;
                    int y = generalPos.y + dy;

                    if (x < 0 || x >= MapGenerator.MapWidth || y < 0 || y >= MapGenerator.MapHeight)
                        continue;

                    var cell = _gameMap.Cells[x, y];
                    if (cell != null)
                    {
                        string terrainName = GetTerrainDisplayName(cell.Terrain);
                        sb.AppendLine($"  {_mapGenerator.GetCoordinate(x, y)}: {terrainName}");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// 收集指定将军的部队状态
        /// </summary>
        private string CollectUnitStatus(GeneralAI general)
        {
            if (general == null)
            {
                return "部队状态不可用";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"将军: {general.GeneralName}");
            sb.AppendLine($"士气: {general.Morale:F0}%");
            sb.AppendLine($"位置: {_mapGenerator.GetCoordinate((int)general.GridPosition.x, (int)general.GridPosition.y)}");
            sb.AppendLine("所属部队:");

            foreach (var unitId in general.UnitsUnderCommand)
            {
                if (unitId != null)
                {
                    sb.AppendLine($"  - {unitId}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// AI将军决策完成后的回调
        /// </summary>
        private void OnGeneralDecided(GeneralAI general, GeneralDecision decision)
        {
            if (general == null || decision == null)
            {
                Debug.LogWarning("[TurnManager] 无效的决策数据");
                return;
            }

            // 触发将军回复事件
            string replyText = FormatGeneralReply(decision);
            OnGeneralReplied?.Invoke(general.GeneralName, replyText);

            Debug.Log($"[TurnManager] {general.GeneralName} 决策完成: {decision.Action}");

            // 执行单位移动命令
            if (decision.Orders != null && decision.Orders.Count > 0)
            {
                foreach (var order in decision.Orders)
                {
                    ExecuteUnitOrder(order);
                }
            }

            // 检查是否遭遇敌军
            CheckForEnemyEncounter(general);
        }

        /// <summary>
        /// 格式化将军回复文本（包含战况分析、计划、行动、部队状态）
        /// </summary>
        private string FormatGeneralReply(GeneralDecision decision)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(decision.Understanding))
            {
                sb.AppendLine($"📊 战况分析: {decision.Understanding}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(decision.Plan))
            {
                sb.AppendLine($"📋 作战计划: {decision.Plan}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(decision.Action))
            {
                sb.AppendLine($"⚡ 行动汇报: {decision.Action}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 执行单位移动命令
        /// </summary>
        private void ExecuteUnitOrder(UnitOrder order)
        {
            // 在实际实现中，这里应该调用单位移动系统
            // 限制每回合最大移动格数
            Debug.Log($"[TurnManager] 单位 {order.UnitName} 移动到 ({order.TargetX}, {order.TargetY})");
            
            // 触发单位位置变化事件
            if (GameEventBus.Instance != null)
            {
                var unitData = new UnitPositionData
                {
                    UnitId = order.UnitName,
                    WorldPosition = new Vector3(order.TargetX, 0, order.TargetY)
                };
                GameEventBus.Instance.PublishUnitPositionChanged(unitData);
            }
        }

        /// <summary>
        /// 检查是否遭遇敌军
        /// </summary>
        private void CheckForEnemyEncounter(GeneralAI general)
        {
            // 在实际实现中，检查视野范围内是否有敌军
            // 如果遭遇敌军，触发战斗
            Debug.Log($"[TurnManager] 检查 {general.GeneralName} 是否遭遇敌军");
        }

        #endregion

        #region 敌军行动

        /// <summary>
        /// 执行敌军AI行动
        /// </summary>
        private void ExecuteEnemyTurn()
        {
            // 获取所有敌军单位
            var enemyUnits = FindEnemyUnits();
            
            foreach (var enemy in enemyUnits)
            {
                ExecuteEnemyUnitAction(enemy);
            }
        }

        /// <summary>
        /// 查找所有敌军单位
        /// </summary>
        private List<UnitPositionData> FindEnemyUnits()
        {
            var enemies = new List<UnitPositionData>();
            
            // 在实际实现中，从单位管理器获取敌军列表
            // 这里返回空列表作为占位
            
            return enemies;
        }

        /// <summary>
        /// 执行单个敌军单位的行动
        /// </summary>
        private void ExecuteEnemyUnitAction(UnitPositionData enemyUnit)
        {
            // 简单规则AI：
            // 1. 向最近的我方单位移动1格
            // 2. 如果在攻击范围内，触发战斗

            var nearestFriendly = FindNearestFriendlyUnit(enemyUnit.WorldPosition);
            
            if (nearestFriendly != null)
            {
                // 计算移动方向
                Vector3 moveDir = (nearestFriendly.WorldPosition - enemyUnit.WorldPosition).normalized;
                Vector3 newPos = enemyUnit.WorldPosition + moveDir;

                // 限制移动1格
                newPos.x = Mathf.Round(newPos.x);
                newPos.z = Mathf.Round(newPos.z);

                // 更新位置
                enemyUnit.PreviousPosition = enemyUnit.WorldPosition;
                enemyUnit.WorldPosition = newPos;

                Debug.Log($"[TurnManager] 敌军单位 {enemyUnit.UnitId} 移动到 {newPos}");

                // 检查攻击范围并触发战斗
                float attackRange = 1.5f;
                float distance = Vector3.Distance(newPos, nearestFriendly.WorldPosition);
                
                if (distance <= attackRange)
                {
                    TriggerCombat(enemyUnit, nearestFriendly);
                }
            }
        }

        /// <summary>
        /// 查找最近的我方单位
        /// </summary>
        private UnitPositionData FindNearestFriendlyUnit(Vector3 fromPosition)
        {
            UnitPositionData nearest = null;
            float minDistance = float.MaxValue;

            // 获取所有我方单位
            var friendlyUnits = FindFriendlyUnits();

            foreach (var unit in friendlyUnits)
            {
                float distance = Vector3.Distance(fromPosition, unit.WorldPosition);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = unit;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 查找所有我方单位
        /// </summary>
        private List<UnitPositionData> FindFriendlyUnits()
        {
            var friendlies = new List<UnitPositionData>();
            
            // 在实际实现中，从单位管理器获取我方单位列表
            // 这里返回空列表作为占位
            
            return friendlies;
        }

        /// <summary>
        /// 触发战斗
        /// </summary>
        private void TriggerCombat(UnitPositionData attacker, UnitPositionData defender)
        {
            Debug.Log($"[TurnManager] 战斗触发: {attacker.UnitId} 攻击 {defender.UnitId}");
            
            // 通过事件总线通知战斗系统
            if (GameEventBus.Instance != null)
            {
                // 触发战斗事件
                OnStatusUpdate?.Invoke($"战斗: {attacker.UnitId} vs {defender.UnitId}");
            }
        }

        #endregion

        #region 结算检查

        /// <summary>
        /// 检查游戏胜负条件
        /// </summary>
        private void CheckSettlement()
        {
            // 检查失败条件
            if (CheckDefeatCondition())
            {
                EndGame(false, "我方单位全灭");
                return;
            }

            // 检查超时
            if (CurrentTurn > MaxTurns)
            {
                EndGame(false, "回合数耗尽，任务失败");
                return;
            }

            // 检查胜利条件
            if (CheckVictoryCondition())
            {
                EndGame(true, "所有目标点被占领");
                return;
            }

            OnStatusUpdate?.Invoke($"第 {CurrentTurn} 回合结束");
        }

        /// <summary>
        /// 检查失败条件：所有我方单位全灭
        /// </summary>
        private bool CheckDefeatCondition()
        {
            var friendlyUnits = FindFriendlyUnits();
            return friendlyUnits.Count == 0;
        }

        /// <summary>
        /// 检查胜利条件：所有目标点被占领
        /// </summary>
        private bool CheckVictoryCondition()
        {
            // 在实际实现中，检查所有目标点的占领状态
            // 这里返回false作为占位
            return false;
        }

        /// <summary>
        /// 结束游戏
        /// </summary>
        private void EndGame(bool isVictory, string reason)
        {
            IsGameOver = true;
            OnGameOver?.Invoke(isVictory, reason);
            OnStatusUpdate?.Invoke(isVictory ? "胜利！" : "失败！");

            Debug.Log($"[TurnManager] 游戏结束 - {(isVictory ? "胜利" : "失败")}: {reason}");

            // 通过事件总线通知
            if (GameEventBus.Instance != null)
            {
                var outcome = isVictory ? GameOutcome.PerfectVictory : GameOutcome.Defeat;
                GameEventBus.Instance.PublishGameOutcomeChanged(outcome);
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 设置当前阶段并触发事件
        /// </summary>
        private void SetPhase(TurnPhase newPhase)
        {
            CurrentPhase = newPhase;
            OnPhaseChanged?.Invoke(newPhase);
            Debug.Log($"[TurnManager] 阶段变更: {newPhase}");
        }

        /// <summary>
        /// 获取地形显示名称
        /// </summary>
        private string GetTerrainDisplayName(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.OpenGround: return "开阔地";
                case TerrainType.River: return "河流";
                case TerrainType.Forest: return "树林";
                case TerrainType.Village: return "村庄";
                case TerrainType.Bridge: return "桥梁";
                case TerrainType.Road: return "道路";
                case TerrainType.Beach: return "海滩";
                default: return "未知";
            }
        }

        #endregion

        #region 调试




        #endregion
    }
}
