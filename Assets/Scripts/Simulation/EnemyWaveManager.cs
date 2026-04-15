// EnemyWaveManager.cs — 敌军波次管理系统
// 职责：控制5波敌军的生成、调度和基础AI
// 命名空间：SWO1.Simulation（与 BattleSimulator 同级）
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;

namespace SWO1.Simulation
{
    // ================================================================
    //  数据模型
    // ================================================================

    /// <summary>敌军AI行为模式</summary>
    public enum EnemyBehavior
    {
        ReconProbe,      // 侦察试探（波次1）
        WaitForOrders,   // 主攻方向待定（波次2，由AI决定）
        FeintAttack,     // 佯攻（波次3，由AI决定）
        FullAssault,     // 全面进攻（波次4）
        FinalCharge      // 最后冲击（波次5）
    }

    /// <summary>单个敌军单位状态</summary>
    [Serializable]
    public class EnemyUnit
    {
        public string UnitId;
        public int TroopCount;
        public int MaxTroopCount;
        public float AttackPower;
        public Vector3 Position;
        public bool IsEngaging;
        public bool IsEliminated;
        public string TargetUnitId; // 当前攻击目标

        public float TroopRatio => MaxTroopCount > 0 ? (float)TroopCount / MaxTroopCount : 0f;
    }

    /// <summary>波次配置数据（可序列化，便于Inspector配置）</summary>
    [Serializable]
    public class WaveConfig
    {
        public int WaveIndex;              // 波次编号 (1-5)
        public float StartTime;            // 波次开始时间（游戏秒）
        public float EndTime;              // 波次结束时间（游戏秒）
        public int TotalTroops;            // 总兵力
        public int UnitCount;              // 分成几个小队
        public int TroopsPerUnit;          // 每小队人数
        public EnemyBehavior Behavior;     // AI行为
        public string Description;         // 波次描述
        public float AttackInterval;       // 攻击间隔（秒）
        public float BridgeDamageRate;     // 对桥伤害速率

        /// <summary>波次持续时间</summary>
        public float Duration => EndTime - StartTime;
    }

    // ================================================================
    //  敌军波次管理器
    // ================================================================

    /// <summary>
    /// 敌军波次管理器（单例）
    /// 
    /// 职责：
    /// - 定义和管理5波敌军
    /// - 根据GameDirector战役阶段触发波次
    /// - 控制敌军基础AI（前进→攻击→撤退）
    /// - 通过GameEventBus与BattleSimulator通信
    /// 
    /// GDD 5.3 敌军波次（调平后）：
    /// | 波次 | 时间       | 兵力 | 策略         |
    /// |------|------------|------|--------------|
    /// | 1    | 0:00-2:30  | 25人 | 侦察试探     |
    /// | 2    | 2:30-5:00  | 45人 | 主攻方向待定  |
    /// | 3    | 5:00-7:30  | 60人 | 可能佯攻     |
    /// | 4    | 7:30-9:00  | 75人 | 全面进攻     |
    /// | 5    | 9:00-10:00 | 50人 | 最后冲击     |
    /// </summary>
    public class EnemyWaveManager : MonoBehaviour
    {
        public static EnemyWaveManager Instance { get; private set; }

        // ============================================================
        //  Inspector 配置
        // ============================================================

        [Header("波次配置")]
        [Tooltip("波次间间隔（秒）")]
        [SerializeField] private float waveInterval = 15f;

        [Tooltip("桥头堡在沙盘上的位置")]
        [SerializeField] private Vector3 bridgePosition = new Vector3(150f, 0f, 70f);

        [Tooltip("地图边缘生成区域")]
        [SerializeField] private float mapEdgeNorth = 300f;
        [SerializeField] private float mapEdgeWest = 0f;
        [SerializeField] private float mapEdgeEast = 300f;

        [Header("调试")]
        [SerializeField] private bool enableDebugLog = true;

        // ============================================================
        //  运行时状态
        // ============================================================

        /// <summary>当前正在进行的波次索引（0=未开始, 1-5）</summary>
        public int CurrentWaveIndex { get; private set; } = 0;

        /// <summary>是否所有波次已完成</summary>
        public bool AllWavesCompleted { get; private set; } = false;

        /// <summary>游戏开始后的计时（秒）</summary>
        private float gameTimer = 0f;

        /// <summary>波次配置列表</summary>
        private List<WaveConfig> waveConfigs = new List<WaveConfig>();

        /// <summary>当前活跃的敌军单位</summary>
        private Dictionary<string, EnemyUnit> activeEnemies = new Dictionary<string, EnemyUnit>();

        /// <summary>波次是否已触发</summary>
        private bool[] waveTriggered = new bool[6]; // 索引 1-5

        /// <summary>当前波次协程</summary>
        private Coroutine currentWaveCoroutine;

        // ============================================================
        //  事件
        // ============================================================

        /// <summary>波次开始</summary>
        public event Action<int> OnWaveStarted;

        /// <summary>波次结束</summary>
        public event Action<int> OnWaveEnded;

        /// <summary>敌军单位生成</summary>
        public event Action<EnemyUnit> OnEnemySpawned;

        /// <summary>敌军单位被消灭</summary>
        public event Action<EnemyUnit> OnEnemyKilled;

        /// <summary>所有波次完成</summary>
        public event Action OnAllWavesCompleted;

        // ============================================================
        //  Unity 生命周期
        // ============================================================

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            InitializeWaveConfigs();
            SubscribeToEvents();
        }

        void Update()
        {
            if (GameDirector.Instance != null && GameDirector.Instance.IsPaused) return;
            if (GameDirector.Instance != null && GameDirector.Instance.Outcome != GameOutcome.InProgress) return;

            gameTimer += Time.deltaTime;

            // 检查是否需要触发下一个波次
            CheckWaveTrigger();

            // 更新敌军AI
            UpdateEnemyAI();
        }

        void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        // ============================================================
        //  波次配置初始化（GDD 5.3 数据）
        // ============================================================

        /// <summary>初始化5波敌军配置</summary>
        private void InitializeWaveConfigs()
        {
            waveConfigs.Clear();

            // 波次1: 0:00-2:30, 25人, 侦察试探
            waveConfigs.Add(new WaveConfig
            {
                WaveIndex = 1,
                StartTime = 0f,
                EndTime = 150f,
                TotalTroops = 25,
                UnitCount = 3,
                TroopsPerUnit = 8, // 3×8=24, 余1
                Behavior = EnemyBehavior.ReconProbe,
                Description = "侦察试探",
                AttackInterval = 6f,
                BridgeDamageRate = 0.8f
            });

            // 波次2: 2:30-5:00, 45人, 主攻方向待定
            waveConfigs.Add(new WaveConfig
            {
                WaveIndex = 2,
                StartTime = 150f,
                EndTime = 300f,
                TotalTroops = 45,
                UnitCount = 4,
                TroopsPerUnit = 11, // 4×11=44, 余1
                Behavior = EnemyBehavior.WaitForOrders,
                Description = "主攻方向待定",
                AttackInterval = 5f,
                BridgeDamageRate = 1.2f
            });

            // 波次3: 5:00-7:30, 60人, 可能佯攻
            waveConfigs.Add(new WaveConfig
            {
                WaveIndex = 3,
                StartTime = 300f,
                EndTime = 450f,
                TotalTroops = 60,
                UnitCount = 5,
                TroopsPerUnit = 12, // 5×12=60
                Behavior = EnemyBehavior.FeintAttack,
                Description = "可能佯攻",
                AttackInterval = 4f,
                BridgeDamageRate = 1.5f
            });

            // 波次4: 7:30-9:00, 75人, 全面进攻
            waveConfigs.Add(new WaveConfig
            {
                WaveIndex = 4,
                StartTime = 450f,
                EndTime = 540f,
                TotalTroops = 75,
                UnitCount = 5,
                TroopsPerUnit = 15, // 5×15=75
                Behavior = EnemyBehavior.FullAssault,
                Description = "全面进攻",
                AttackInterval = 3f,
                BridgeDamageRate = 2.0f
            });

            // 波次5: 9:00-10:00, 50人, 最后冲击
            waveConfigs.Add(new WaveConfig
            {
                WaveIndex = 5,
                StartTime = 540f,
                EndTime = 600f,
                TotalTroops = 50,
                UnitCount = 4,
                TroopsPerUnit = 12, // 4×12=48, 余2
                Behavior = EnemyBehavior.FinalCharge,
                Description = "最后冲击",
                AttackInterval = 2.5f,
                BridgeDamageRate = 1.8f
            });

            Log("波次配置初始化完成：5波敌军");
        }

        // ============================================================
        //  波次触发逻辑
        // ============================================================

        /// <summary>检查是否需要触发下一个波次</summary>
        private void CheckWaveTrigger()
        {
            if (CurrentWaveIndex >= 5 || AllWavesCompleted) return;

            int nextWave = CurrentWaveIndex + 1;
            if (nextWave > 5 || waveTriggered[nextWave]) return;

            if (gameTimer >= waveConfigs[nextWave - 1].StartTime)
            {
                StartWave(nextWave);
            }
        }

        /// <summary>启动指定波次</summary>
        private void StartWave(int waveIndex)
        {
            if (waveIndex < 1 || waveIndex > 5) return;
            if (waveTriggered[waveIndex]) return;

            var config = waveConfigs[waveIndex - 1];
            waveTriggered[waveIndex] = true;
            CurrentWaveIndex = waveIndex;

            Log($"=== 波次 {waveIndex} 开始：{config.Description} ({config.TotalTroops}人) ===");

            // 生成敌军单位
            SpawnWaveEnemies(config);

            // 启动波次AI协程
            if (currentWaveCoroutine != null)
            {
                StopCoroutine(currentWaveCoroutine);
            }
            currentWaveCoroutine = StartCoroutine(WaveAICoroutine(config));

            OnWaveStarted?.Invoke(waveIndex);

            // 发布事件到 EventBus
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishGameTimeUpdated(gameTimer);
            }
        }

        /// <summary>结束当前波次</summary>
        private void EndWave(int waveIndex)
        {
            var config = waveConfigs[waveIndex - 1];
            Log($"=== 波次 {waveIndex} 结束 ===");

            // 清除残余敌军
            ClearWaveEnemies();

            OnWaveEnded?.Invoke(waveIndex);

            // 检查是否所有波次完成
            if (waveIndex >= 5)
            {
                AllWavesCompleted = true;
                OnAllWavesCompleted?.Invoke();
                Log("所有5波敌军已结束！");
            }
        }

        // ============================================================
        //  敌军生成
        // ============================================================

        /// <summary>生成一波敌军</summary>
        private void SpawnWaveEnemies(WaveConfig config)
        {
            int spawned = 0;

            for (int i = 0; i < config.UnitCount; i++)
        {
                // 计算该小队人数
                int troops = config.TroopsPerUnit;
                if (spawned + troops > config.TotalTroops)
                {
                    troops = config.TotalTroops - spawned;
                }
                if (troops <= 0) break;

                // 生成位置：地图北侧边缘，均匀分布
                float spawnX = mapEdgeWest + (mapEdgeEast - mapEdgeWest) * ((float)(i + 1) / (config.UnitCount + 1));
                float spawnZ = mapEdgeNorth;
                Vector3 spawnPos = new Vector3(spawnX, 0f, spawnZ);

                string unitId = $"enemy_wave{config.WaveIndex}_unit{i + 1}";

                var enemy = new EnemyUnit
                {
                    UnitId = unitId,
                    TroopCount = troops,
                    MaxTroopCount = troops,
                    AttackPower = 5f + config.WaveIndex * 2f, // 波次越高攻击力越强
                    Position = spawnPos,
                    IsEngaging = false,
                    IsEliminated = false,
                    TargetUnitId = null
                };

                activeEnemies[unitId] = enemy;

                // 注册到 BattleSimulator (使用波次递增的攻击力)
                float simAttackPower = 5f + config.WaveIndex * 2.5f;
                if (BattleSimulator.Instance != null)
                {
                    BattleSimulator.Instance.RegisterEnemyUnit(unitId, troops, simAttackPower, spawnPos);
                }

                OnEnemySpawned?.Invoke(enemy);
                spawned++;

                Log($"  生成敌军: {unitId} ({troops}人) @ {spawnPos}");
            }
        }

        /// <summary>清除当前波次的敌军</summary>
        private void ClearWaveEnemies()
        {
            var toRemove = new List<string>();
            foreach (var kvp in activeEnemies)
            {
                if (BattleSimulator.Instance != null)
                {
                    BattleSimulator.Instance.UnregisterEnemyUnit(kvp.Key);
                }
                toRemove.Add(kvp.Key);
            }
            foreach (var id in toRemove)
            {
                activeEnemies.Remove(id);
            }
        }

        // ============================================================
        //  敌军基础 AI
        // ============================================================

        /// <summary>波次AI协程 — 控制整波敌军的行为节奏</summary>
        private IEnumerator WaveAICoroutine(WaveConfig config)
        {
            // 阶段1: 向桥头堡移动（前1/3时间）
            float movePhaseDuration = config.Duration * 0.3f;
            Log($"  波次{config.WaveIndex} 阶段1: 前进 ({movePhaseDuration:F0}s)");

            float elapsed = 0f;
            while (elapsed < movePhaseDuration)
            {
                MoveEnemiesTowardBridge(config);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 阶段2: 交战（中间1/3时间）
            float engagePhaseDuration = config.Duration * 0.4f;
            Log($"  波次{config.WaveIndex} 阶段2: 交战 ({engagePhaseDuration:F0}s)");

            float engageTimer = 0f;
            while (engageTimer < engagePhaseDuration)
            {
                ExecuteCombatBehavior(config);
                engageTimer += Time.deltaTime;
                yield return new WaitForSeconds(config.AttackInterval);
            }

            // 波次1（侦察）提前撤退，不等结束
            if (config.Behavior == EnemyBehavior.ReconProbe)
            {
                Log($"  波次1 侦察结束，撤退");
                yield break;
            }

            // 阶段3: 波次4-5 全力冲锋到最后
            if (config.Behavior == EnemyBehavior.FullAssault || config.Behavior == EnemyBehavior.FinalCharge)
            {
                float finalDuration = config.Duration * 0.3f;
                Log($"  波次{config.WaveIndex} 阶段3: 全力冲锋 ({finalDuration:F0}s)");

                float finalTimer = 0f;
                while (finalTimer < finalDuration)
                {
                    ExecuteFullAssault(config);
                    finalTimer += Time.deltaTime;
                    yield return new WaitForSeconds(config.AttackInterval * 0.5f);
                }
            }

            // 波次结束
            EndWave(config.WaveIndex);
        }

        /// <summary>敌军向桥头堡移动</summary>
        private void MoveEnemiesTowardBridge(WaveConfig config)
        {
            float moveSpeed = 5f; // 沙盘单位/秒

            foreach (var kvp in activeEnemies)
            {
                var enemy = kvp.Value;
                if (enemy.IsEliminated) continue;

                // 方向：朝桥头堡
                Vector3 direction = (bridgePosition - enemy.Position).normalized;

                // 根据行为模式添加偏移
                Vector3 offset = GetBehaviorOffset(config.Behavior, enemy.UnitId);
                Vector3 targetDir = (direction + offset * 0.3f).normalized;

                enemy.Position += targetDir * moveSpeed * config.AttackInterval;
            }
        }

        /// <summary>执行基础交战行为</summary>
        private void ExecuteCombatBehavior(WaveConfig config)
        {
            if (BattleSimulator.Instance == null) return;

            foreach (var kvp in activeEnemies)
            {
                var enemy = kvp.Value;
                if (enemy.IsEliminated) continue;

                // 选择最近的我方单位作为目标
                string targetId = FindNearestFriendlyUnit(enemy.Position);

                if (!string.IsNullOrEmpty(targetId))
                {
                    enemy.IsEngaging = true;
                    enemy.TargetUnitId = targetId;

                    // 通过 BattleSimulator 计算伤害
                    var attackerUnit = BattleSimulator.Instance.GetEnemyUnit(enemy.UnitId);
                    var defenderUnit = BattleSimulator.Instance.GetFriendlyUnit(targetId);

                    if (attackerUnit != null && defenderUnit != null)
                    {
                        float damage = BattleSimulator.Instance.CalculateDamage(attackerUnit, defenderUnit);
                        BattleSimulator.Instance.ApplyDamageToUnit(targetId, damage);
                    }
                }
                else
                {
                    // 无我方单位可攻击 → 攻击桥头堡
                    enemy.IsEngaging = true;
                    enemy.TargetUnitId = "bridge";
                    BattleSimulator.Instance.EnemyAttackBridge(enemy.UnitId, config.AttackInterval);
                }
            }
        }

        /// <summary>全力冲锋行为（波次4-5）</summary>
        private void ExecuteFullAssault(WaveConfig config)
        {
            if (BattleSimulator.Instance == null) return;

            foreach (var kvp in activeEnemies)
            {
                var enemy = kvp.Value;
                if (enemy.IsEliminated) continue;

                // 全力冲锋：直接攻击桥头堡，忽略我方单位
                enemy.IsEngaging = true;
                enemy.TargetUnitId = "bridge";

                // 更高伤害
                BattleSimulator.Instance.EnemyAttackBridge(enemy.UnitId, config.AttackInterval * 1.5f);

                // 同时也攻击附近的我方单位
                string nearestId = FindNearestFriendlyUnit(enemy.Position);
                if (!string.IsNullOrEmpty(nearestId))
                {
                    var attackerUnit = BattleSimulator.Instance.GetEnemyUnit(enemy.UnitId);
                    var defenderUnit = BattleSimulator.Instance.GetFriendlyUnit(nearestId);
                    if (attackerUnit != null && defenderUnit != null)
                    {
                        float damage = BattleSimulator.Instance.CalculateDamage(attackerUnit, defenderUnit) * 0.5f;
                        BattleSimulator.Instance.ApplyDamageToUnit(nearestId, damage);
                    }
                }
            }
        }

        /// <summary>根据行为模式获取位置偏移</summary>
        private Vector3 GetBehaviorOffset(EnemyBehavior behavior, string unitId)
        {
            // 用unitId的hash生成确定性偏移
            float hashOffset = (unitId.GetHashCode() % 100) / 100f;

            switch (behavior)
            {
                case EnemyBehavior.ReconProbe:
                    // 侦察：分散前进，试探性
                    return new Vector3(Mathf.Sin(hashOffset * Mathf.PI * 2) * 30f, 0f, 0f);

                case EnemyBehavior.WaitForOrders:
                    // 待命：小范围集结
                    return new Vector3(Mathf.Sin(hashOffset * Mathf.PI) * 15f, 0f, -10f);

                case EnemyBehavior.FeintAttack:
                    // 佯攻：部分单位偏离主方向
                    bool isFeint = hashOffset > 0.6f;
                    return isFeint
                        ? new Vector3((hashOffset > 0.8f ? 1f : -1f) * 50f, 0f, 0f)
                        : Vector3.zero;

                case EnemyBehavior.FullAssault:
                    // 全面进攻：相对集中
                    return new Vector3(Mathf.Sin(hashOffset * Mathf.PI) * 10f, 0f, 0f);

                case EnemyBehavior.FinalCharge:
                    // 最后冲击：密集冲锋
                    return new Vector3(Mathf.Sin(hashOffset * Mathf.PI * 2) * 5f, 0f, 0f);

                default:
                    return Vector3.zero;
            }
        }

        /// <summary>寻找最近的我方单位</summary>
        private string FindNearestFriendlyUnit(Vector3 position)
        {
            if (BattleSimulator.Instance == null) return null;

            string nearestId = null;
            float nearestDist = float.MaxValue;

            foreach (var unit in BattleSimulator.Instance.GetFriendlyUnits())
            {
                if (unit.IsEliminated) continue;

                float dist = Vector3.Distance(position, unit.Position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestId = unit.UnitId;
                }
            }

            return nearestId;
        }

        // ============================================================
        //  敌军实时更新
        // ============================================================

        /// <summary>每帧更新敌军状态</summary>
        private void UpdateEnemyAI()
        {
            // 同步 BattleSimulator 中的伤亡
            if (BattleSimulator.Instance == null) return;

            var toRemove = new List<string>();
            foreach (var kvp in activeEnemies)
            {
                var simUnit = BattleSimulator.Instance.GetEnemyUnit(kvp.Key);
                if (simUnit != null)
                {
                    kvp.Value.TroopCount = simUnit.TroopCount;
                    kvp.Value.IsEliminated = simUnit.IsEliminated;

                    if (simUnit.IsEliminated)
                    {
                        OnEnemyKilled?.Invoke(kvp.Value);
                        toRemove.Add(kvp.Key);
                        Log($"  敌军被消灭: {kvp.Key}");
                    }
                }
            }

            foreach (var id in toRemove)
            {
                activeEnemies.Remove(id);
            }
        }

        // ============================================================
        //  事件监听
        // ============================================================

        private void SubscribeToEvents()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.OnCampaignPhaseChanged += HandlePhaseChanged;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.OnCampaignPhaseChanged -= HandlePhaseChanged;
            }
        }

        /// <summary>战役阶段变更时的响应</summary>
        private void HandlePhaseChanged(CampaignPhase phase)
        {
            Log($"战役阶段变更: {phase}");

            // CounterAttack 阶段可以作为波次加速的信号
            if (phase == CampaignPhase.CounterAttack)
            {
                // 敌军进入反击模式，行为更激进
                Log("敌军进入反击模式！");
            }
        }

        // ============================================================
        //  公共查询接口
        // ============================================================

        /// <summary>获取当前活跃敌军数量</summary>
        public int GetActiveEnemyCount() => activeEnemies.Count;

        /// <summary>获取当前波次配置</summary>
        public WaveConfig GetCurrentWaveConfig()
        {
            if (CurrentWaveIndex >= 1 && CurrentWaveIndex <= 5)
                return waveConfigs[CurrentWaveIndex - 1];
            return null;
        }

        /// <summary>获取所有波次配置</summary>
        public List<WaveConfig> GetAllWaveConfigs() => new List<WaveConfig>(waveConfigs);

        /// <summary>获取活跃敌军列表</summary>
        public List<EnemyUnit> GetActiveEnemies() => new List<EnemyUnit>(activeEnemies.Values);

        /// <summary>获取指定波次的敌军</summary>
        public List<EnemyUnit> GetEnemiesByWave(int waveIndex)
        {
            var result = new List<EnemyUnit>();
            string prefix = $"enemy_wave{waveIndex}_";
            foreach (var kvp in activeEnemies)
            {
                if (kvp.Key.StartsWith(prefix))
                    result.Add(kvp.Value);
            }
            return result;
        }

        /// <summary>游戏计时（秒）</summary>
        public float GameTimer => gameTimer;

        /// <summary>格式化游戏时间 MM:SS</summary>
        public string GetFormattedTimer()
        {
            int minutes = Mathf.FloorToInt(gameTimer / 60f);
            int seconds = Mathf.FloorToInt(gameTimer % 60f);
            return $"{minutes:D2}:{seconds:D2}";
        }

        // ============================================================
        //  调试
        // ============================================================

        private void Log(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"[EnemyWave] {message}");
            }
        }

        /// <summary>调试用：强制触发指定波次</summary>
        [ContextMenu("Debug: Force Start Wave 1")]
        public void DebugForceWave1() => StartWave(1);

        [ContextMenu("Debug: Force Start Wave 3")]
        public void DebugForceWave3() => StartWave(3);

        [ContextMenu("Debug: Force Start Wave 5")]
        public void DebugForceWave5() => StartWave(5);
    }
}
