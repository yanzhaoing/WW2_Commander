// GameController2D.cs — 2D 侦察战斗主控制器
// 串联 MapGenerator, SandTable2D, RadioSystem, EnemySpawner, BattleSimulator
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Map;
using SWO1.Radio;
using SWO1.Simulation;

namespace SWO1.Core
{
    #region 数据模型

    public class FriendlyUnit
    {
        public string Id;
        public string Name;
        public int GridX, GridY;
        public int Strength = 100;
        public float Morale = 1.0f;
        public bool IsActive = true;
        public float MoveTimer;
        public bool IsDefending;
        public int _targetX = -1;
        public int _targetY = -1;
        public float CoverBonus;
    }

    public class BattleResult
    {
        public string UnitName;
        public int Damage;
        public int RemainingStrength;
        public int EnemyX, EnemyY;
        public bool Victory; // true=我方胜, false=持续交战
    }

    #endregion

    public class GameController2D : MonoBehaviour
    {
        [Header("系统引用")]
        public MapGenerator mapGenerator;
        public RadioSystem radioSystem;
        public EnemySpawner enemySpawner;
        public BattleSimulator battleSimulator;
        public GameDirector gameDirector;
        public SWO1.UI.SandTable2D sandTable;

        [Header("配置")]
        public int MapSeed = -1;
        public float GameDuration = 540f; // 9分钟
        public float MoveInterval = 5f;   // 每5秒移动1格

        // 运行时状态
        private GameMap currentMap;
        private List<FriendlyUnit> friendlies = new List<FriendlyUnit>();
        private List<Vector2Int> capturedBridges = new List<Vector2Int>();
        private float gameTime;
        private bool gameRunning;
        private List<BattleResult> battleResults = new List<BattleResult>();
        private int totalBridges;

        void Start()
        {
            // 自动查找引用
            if (mapGenerator == null) mapGenerator = FindObjectOfType<MapGenerator>();
            if (radioSystem == null) radioSystem = FindObjectOfType<RadioSystem>();
            if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
            if (battleSimulator == null) battleSimulator = FindObjectOfType<BattleSimulator>();
            if (gameDirector == null) gameDirector = FindObjectOfType<GameDirector>();
            if (sandTable == null) sandTable = FindObjectOfType<SWO1.UI.SandTable2D>();

            StartCoroutine(InitializeGame());
        }

        IEnumerator InitializeGame()
        {
            // 启用全局 IME（中文输入支持）
            Input.imeCompositionMode = IMECompositionMode.On;

            yield return new WaitForSeconds(0.5f);

            // 生成地图
            if (mapGenerator != null)
            {
                currentMap = mapGenerator.GenerateMap(MapSeed);
                totalBridges = currentMap.BridgePositions.Count;
                Debug.Log($"[GameController2D] 地图生成: {totalBridges} 座桥梁");
            }

            // 渲染沙盘（空白，等待侦察）
            if (sandTable != null && currentMap != null)
            {
                // 不调用 RenderMap，保持地图空白
                // sandTable.RenderMap(currentMap); // 地形由玩家侦察后标注
                Debug.Log("[GameController2D] 沙盘空白，等待侦察标注");
            }

            // 部署敌军
            if (enemySpawner != null && currentMap != null)
            {
                enemySpawner.DeployInitialEnemies(currentMap, MapSeed);
            }

            // 生成我方单位（海滩区域）
            SpawnFriendlies();

            // 订阅事件
            SubscribeEvents();

            // 发送初始侦察汇报
            yield return new WaitForSeconds(2f);
            SendInitialReports();

            // 开始游戏
            gameRunning = true;
            Debug.Log("[GameController2D] 游戏开始！");
        }

        void SpawnFriendlies()
        {
            int beachY = 10;
            friendlies.Clear();
            friendlies.Add(new FriendlyUnit { Id = "alpha_1", Name = "红一排", GridX = 4, GridY = beachY });
            friendlies.Add(new FriendlyUnit { Id = "alpha_2", Name = "红二排", GridX = 8, GridY = beachY });
            friendlies.Add(new FriendlyUnit { Id = "tank_1", Name = "蓝四坦克排", GridX = 12, GridY = beachY });

            // 不在地图上自动显示我方单位标记（由将军汇报描述位置）
            Debug.Log("[GameController2D] 我方单位部署完成: 3个排");
        }

        void SendInitialReports()
        {
            // 不自动发送侦察报告（由玩家手动侦察标注）
            Debug.Log("[GameController2D] 等待玩家侦察...");
        }

        IEnumerator DelayedReport(string sender, int gx, int gy, SWO1.Map.TerrainType terrain, float delay, string content)
        {
            yield return new WaitForSeconds(delay);
            if (!gameRunning) yield break;

            if (radioSystem != null)
            {
                var r = radioSystem.GenerateTerrainReport(sender, gx, gy, (SWO1.Radio.TerrainType)terrain);
                r.Content = content;
                DeliverReport(r);
            }
        }

        void DeliverReport(RadioMessage report)
        {
            // 推送到无线电 UI
            Debug.Log($"📻 [{report.Sender}] {report.Content} (坐标:{mapGenerator?.GetCoordinate(report.GridX, report.GridY) ?? "未知"})");

            // 如果是地形汇报，在沙盘上显示地形
            if (report.Type == ReportType.Terrain && sandTable != null && currentMap != null)
            {
                var cell = currentMap.Cells[report.GridX, report.GridY];
                if (cell != null)
                {
                    sandTable.RevealTerrainAt(report.GridX, report.GridY, cell.Terrain);
                }
            }

            // 通知 EventBus
            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                // 创建旧版 RadioReport 给 EventBus（如果需要）
                // 或者直接用新事件
            }
        }

        void SubscribeEvents()
        {
            if (radioSystem != null)
            {
                radioSystem.OnCommandDelivered += OnRadioCommandDelivered;
                radioSystem.OnCommandLost += OnRadioCommandLost;
                radioSystem.OnReportDelivered += OnRadioReportDelivered;
            }

            if (enemySpawner != null)
            {
                enemySpawner.OnEnemyDiscovered += OnEnemyFound;
                enemySpawner.OnEncounter += OnEncounter;
                enemySpawner.OnWaveSpawn += OnWaveSpawn;
            }
        }

        void OnDestroy()
        {
            if (radioSystem != null)
            {
                radioSystem.OnCommandDelivered -= OnRadioCommandDelivered;
                radioSystem.OnCommandLost -= OnRadioCommandLost;
                radioSystem.OnReportDelivered -= OnRadioReportDelivered;
            }
        }

        #region 游戏循环

        void Update()
        {
            if (!gameRunning) return;

            // 由 TurnManager 驱动回合制，不再实时循环
            // gameTime += Time.deltaTime;
            // if (enemySpawner != null)
            //     enemySpawner.UpdateAllUnits(Time.deltaTime);
            // UpdateFriendlyMovement();
            // CheckEncounters();
            // CheckWaveSpawns();
            // CheckBridgeCapture();
            // CheckWinLose();
            // UpdateSandTableMarkers();
        }

        void UpdateFriendlyMovement()
        {
            foreach (var unit in friendlies)
            {
                if (!unit.IsActive || unit.IsDefending) continue;

                unit.MoveTimer -= Time.deltaTime;
                if (unit.MoveTimer <= 0 && unit._targetX >= 0)
                {
                    // 向目标移动一格
                    int dx = Math.Sign(unit._targetX - unit.GridX);
                    int dy = Math.Sign(unit._targetY - unit.GridY);

                    int newX = unit.GridX + dx;
                    int newY = unit.GridY + dy;

                    if (newX >= 0 && newX < 32 && newY >= 0 && newY < 24)
                    {
                        unit.GridX = newX;
                        unit.GridY = newY;
                        unit.MoveTimer = MoveInterval;

                        // 检查侦察汇报
                        if (currentMap != null && radioSystem != null)
                        {
                            var cell = currentMap.Cells[newX, newY];
                            if (cell.Terrain != SWO1.Map.TerrainType.OpenGround)
                            {
                                var r = radioSystem.GenerateTerrainReport(unit.Name, newX, newY, (SWO1.Radio.TerrainType)cell.Terrain);
                                DeliverReport(r);
                            }
                        }
                    }
                    else
                    {
                        unit._targetX = -1;
                        unit._targetY = -1;
                    }
                }
            }
        }

        public void ExecuteTurn()
        {
            gameTime += 30f; // 每回合30秒
            battleResults.Clear();

            // 更新敌军
            if (enemySpawner != null)
                enemySpawner.UpdateAllUnits(30f);

            // 检查遭遇（记录战斗结果）
            ExecuteEncounters();

            // 检查波次
            ExecuteWaveSpawns();

            // 检查桥梁占领
            CheckBridgeCapture();
        }

        void ExecuteEncounters()
        {
            if (enemySpawner == null) return;

            var playerPositions = new List<(string unitId, int gx, int gy)>();
            foreach (var u in friendlies)
            {
                if (u.IsActive)
                    playerPositions.Add((u.Id, u.GridX, u.GridY));
            }

            enemySpawner.CheckEncounters(playerPositions);
        }

        void ExecuteWaveSpawns()
        {
            if (enemySpawner == null) return;

            float gameMinutes = gameTime / 60f;
            var wave = enemySpawner.GetNextWave(gameMinutes);
            if (wave != null && currentMap != null)
            {
                enemySpawner.SpawnWave(wave, currentMap);
            }
        }

        public List<BattleResult> GetBattleResults() => new List<BattleResult>(battleResults);
        public List<FriendlyUnit> GetFriendlies() => new List<FriendlyUnit>(friendlies);
        public int GetGameTurn() => Mathf.FloorToInt(gameTime / 30f) + 1;

        void CheckBridgeCapture()
        {
            if (currentMap == null) return;

            foreach (var bridge in currentMap.BridgePositions)
            {
                if (capturedBridges.Contains(bridge)) continue;

                // 检查是否有我方单位在桥梁位置
                bool friendlyOnBridge = false;
                foreach (var u in friendlies)
                {
                    if (u.IsActive && u.GridX == bridge.x && u.GridY == bridge.y)
                    {
                        friendlyOnBridge = true;
                        break;
                    }
                }

                if (!friendlyOnBridge) continue;

                // 检查桥梁位置是否有敌军
                var enemiesAtBridge = enemySpawner?.GetEnemiesNearPosition(bridge.x, bridge.y, 0);
                if (enemiesAtBridge == null || enemiesAtBridge.Count == 0)
                {
                    capturedBridges.Add(bridge);
                    Debug.Log($"🌉 桥梁占领: {mapGenerator?.GetCoordinate(bridge.x, bridge.y)}");

                    // 更新沙盘
                    if (sandTable != null)
                    {
                        sandTable.AddMarker(new SWO1.UI.MapMarker
                        {
                            Id = "bridge_" + bridge.x + "_" + bridge.y,
                            GridX = bridge.x,
                            GridY = bridge.y,
                            Type = SWO1.UI.MarkerType.Friendly,
                            Text = "已占领",
                            Color = new Color(0f, 0.8f, 0.3f)
                        });
                    }

                    // 发送汇报
                    if (radioSystem != null)
                    {
                        var r = radioSystem.GenerateTerrainReport("指挥部", bridge.x, bridge.y, (SWO1.Radio.TerrainType)SWO1.Map.TerrainType.Bridge);
                        r.Content = $"桥梁 {mapGenerator?.GetCoordinate(bridge.x, bridge.y)} 已占领！";
                        DeliverReport(r);
                    }
                }
            }
        }

        void CheckWinLose()
        {
            // 全部桥梁占领 → 胜利
            if (capturedBridges.Count >= totalBridges && totalBridges > 0)
            {
                EndGame(true, "完美胜利！所有桥梁已占领");
                return;
            }

            // 所有单位被消灭 → 失败
            bool anyAlive = false;
            foreach (var u in friendlies)
            {
                if (u.IsActive) { anyAlive = true; break; }
            }
            if (!anyAlive)
            {
                EndGame(false, "全军覆没...");
                return;
            }

            // 时间到
            if (gameTime >= GameDuration)
            {
                string result = capturedBridges.Count > 0
                    ? $"时间到！占领 {capturedBridges.Count}/{totalBridges} 座桥梁"
                    : "时间到！未能占领任何桥梁";
                EndGame(capturedBridges.Count > 0, result);
            }
        }

        void UpdateSandTableMarkers()
        {
            if (sandTable == null) return;

            // 更新我方单位标记位置
            foreach (var u in friendlies)
            {
                if (!u.IsActive) continue;
                // SandTable2D 的标记位置更新需要通过 AddMarker（会覆盖）或修改现有标记
                // 简化处理：每帧更新标记
            }
        }

        #endregion

        #region 指令处理

        /// <summary>供 CommandMenu 调用</summary>
        public void ExecuteCommand(string unitId, SWO1.Radio.CommandType type, int targetX, int targetY)
        {
            var unit = friendlies.Find(u => u.Id == unitId);
            if (unit == null || !unit.IsActive) return;

            switch (type)
            {
                case SWO1.Radio.CommandType.Move:
                    unit._targetX = targetX;
                    unit._targetY = targetY;
                    unit.IsDefending = false;
                    unit.MoveTimer = 1f; // 1秒后开始移动
                    Debug.Log($"📻 指令: {unit.Name} 推进到 {mapGenerator?.GetCoordinate(targetX, targetY)}");
                    break;

                case SWO1.Radio.CommandType.Defend:
                    unit.IsDefending = true;
                    unit._targetX = -1;
                    unit._targetY = -1;
                    unit.CoverBonus = 0.2f;
                    Debug.Log($"📻 指令: {unit.Name} 就地防御");
                    break;

                case SWO1.Radio.CommandType.Scout:
                    unit._targetX = Mathf.Clamp(unit.GridX + (targetX > 0 ? 2 : -2), 0, 15);
                    unit._targetY = Mathf.Clamp(unit.GridY + (targetY > 0 ? 2 : -2), 0, 11);
                    unit.IsDefending = false;
                    unit.MoveTimer = 1f;
                    Debug.Log($"📻 指令: {unit.Name} 侦察");
                    break;

                case SWO1.Radio.CommandType.Attack:
                    unit._targetX = targetX;
                    unit._targetY = targetY;
                    unit.IsDefending = false;
                    unit.MoveTimer = 1f;
                    Debug.Log($"📻 指令: {unit.Name} 攻击 {mapGenerator?.GetCoordinate(targetX, targetY)}");
                    break;

                case SWO1.Radio.CommandType.Retreat:
                    unit._targetX = Mathf.Clamp(unit.GridX, 0, 31);
                    unit._targetY = Mathf.Min(unit.GridY + 3, 23); // 向海滩撤退
                    unit.IsDefending = false;
                    unit.MoveTimer = 1f;
                    Debug.Log($"📻 指令: {unit.Name} 撤退");
                    break;

                case SWO1.Radio.CommandType.Status:
                    if (radioSystem != null)
                    {
                        var r = radioSystem.GenerateStatusReport(unit.Name, unit.Strength / 100f, unit.Morale);
                        DeliverReport(r);
                    }
                    break;

                case SWO1.Radio.CommandType.Wait:
                    unit.IsDefending = false;
                    unit._targetX = -1;
                    unit._targetY = -1;
                    Debug.Log($"📻 指令: {unit.Name} 待命");
                    break;
            }
        }

        #endregion

        #region 事件处理

        void OnRadioCommandDelivered(SWO1.Radio.RadioCommand cmd)
        {
            Debug.Log($"📻 指令已送达: {cmd.TargetUnit}");
        }

        void OnRadioCommandLost(SWO1.Radio.RadioCommand cmd)
        {
            Debug.Log($"📻 指令丢失: {cmd.TargetUnit}");
        }

        void OnRadioReportDelivered(SWO1.Radio.RadioMessage msg)
        {
            DeliverReport(msg);
        }

        void OnEnemyFound(EnemyUnit2D enemy)
        {
            Debug.Log($"🔍 发现敌军: {enemy.Name} 位于 {mapGenerator?.GetCoordinate(enemy.GridX, enemy.GridY)}");

            // 生成汇报
            if (radioSystem != null)
            {
                var r = radioSystem.GenerateEnemyReport("侦察兵", enemy.GridX, enemy.GridY, enemy.Strength);
                DeliverReport(r);
            }
        }

        void OnEncounter(EnemyUnit2D enemy, EnemyUnit2D other)
        {
            if (enemy == null || !enemy.IsActive) return;

            FriendlyUnit closest = null;
            float minDist = float.MaxValue;
            foreach (var u in friendlies)
            {
                if (!u.IsActive) continue;
                float d = Vector2.Distance(new Vector2(u.GridX, u.GridY), new Vector2(enemy.GridX, enemy.GridY));
                if (d < minDist) { minDist = d; closest = u; }
            }
            if (closest == null) { Debug.LogWarning("[GameController2D] OnEncounter: 无活跃友军单位"); return; }

            int ex = enemy.GridX;
            int ey = enemy.GridY;
            int prevStrength = closest.Strength;

            float friendlyPower = closest.Strength * closest.Morale * (1f + closest.CoverBonus);
            float enemyPower = enemy.Strength * 10f;

            if (friendlyPower > enemyPower * 1.5f)
            {
                int damage = Mathf.Min(Mathf.RoundToInt(enemyPower * 0.3f), closest.Strength);
                enemySpawner?.KillUnit(enemy.Id);
                closest.Strength -= damage;
                battleResults.Add(new BattleResult { UnitName = closest.Name, Damage = damage, RemainingStrength = closest.Strength, EnemyX = ex, EnemyY = ey, Victory = true });
                Debug.Log($"⚔️ {closest.Name} 击退敌军，损失 {damage} 兵力 (剩余:{closest.Strength})");
            }
            else if (enemyPower > friendlyPower * 1.5f)
            {
                int damage = Mathf.Min(Mathf.RoundToInt(friendlyPower * 0.5f), closest.Strength);
                closest.Strength -= damage;
                closest.Morale -= 0.3f;
                closest.GridY = Mathf.Min(closest.GridY + 2, MapGenerator.MapHeight - 1);
                battleResults.Add(new BattleResult { UnitName = closest.Name, Damage = damage, RemainingStrength = closest.Strength, EnemyX = ex, EnemyY = ey, Victory = false });
                Debug.Log($"⚔️ {closest.Name} 损失 {damage} 兵力，撤退");
                if (closest.Strength <= 0) { closest.IsActive = false; Debug.Log($"💀 {closest.Name} 全军覆没"); }
            }
            else
            {
                int fDmg = Mathf.Min(Mathf.RoundToInt(enemyPower * 0.2f), closest.Strength);
                int eDmg = Mathf.Min(Mathf.RoundToInt(friendlyPower * 0.2f), enemy.Strength);
                closest.Strength -= fDmg;
                enemy.Strength -= eDmg;
                battleResults.Add(new BattleResult { UnitName = closest.Name, Damage = fDmg, RemainingStrength = closest.Strength, EnemyX = ex, EnemyY = ey, Victory = false });
                Debug.Log($"⚔️ {closest.Name} 与敌军持续交战中 (损失:{fDmg})");
            }
        }

        void OnWaveSpawn(WaveConfig2D wave)
        {
            Debug.Log($"⚠️ 敌军增援出现！方向:{wave.SpawnEdge}");
            if (radioSystem != null)
            {
                var r = radioSystem.GenerateReinforcementWarning(wave.TargetX, wave.TargetY, wave.SpawnEdge);
                DeliverReport(r);
            }
        }

        #endregion

        void EndGame(bool victory, string message)
        {
            gameRunning = false;
            Debug.Log($"🏁 游戏结束: {message}");

            if (gameDirector != null)
            {
                gameDirector.SetFinalOutcome(0.3f);
            }
        }
    }

}