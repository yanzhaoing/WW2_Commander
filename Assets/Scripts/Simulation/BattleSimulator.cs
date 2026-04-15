// BattleSimulator.cs — 战场数值引擎
// 职责：桥梁HP、部队状态、战斗公式、士气系统、指令执行、胜负判定
// 通过 GameEventBus 事件驱动，零直接耦合
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.Command;
using SWO1.Intelligence;

namespace SWO1.Simulation
{
    #region 数据模型

    public enum UnitState { Idle, Engaging, Moving, Defending, Retreating }

    [Serializable]
    public class Platoon
    {
        public string UnitId;
        public string DisplayName;
        public int InitialTroops;
        public int CurrentTroops;
        public float AmmoPercent;       // 0-100
        public float Morale;            // 0-100
        public Vector3 Position;
        public UnitState State;
        public bool HasEngineers;
        public float LastContactTime;

        public float TroopRatio => (float)CurrentTroops / InitialTroops;
        public bool IsEliminated => CurrentTroops <= 0;
    }

    #endregion

    /// <summary>
    /// 战场数值引擎 — 项目基石
    /// 数据流: GameEventBus → HandleCommandDelivered → ExecuteCommand → 数值计算 → 同步 BattleSimulationInterface
    /// </summary>
    public class BattleSimulator : MonoBehaviour
    {
        public static BattleSimulator Instance { get; private set; }

        [Header("桥梁")]
        [SerializeField] private int bridgeMaxHP = 300;
        [SerializeField] private float bridgeDamageRate = 1.5f;
        [SerializeField] private float bridgeRepairRate = 2f;

        [Header("战斗公式")]
        [SerializeField] private float baseDamage = 14f;
        [SerializeField] private float defenseModifier = 0.55f;
        [SerializeField] private float randomMin = 0.8f;
        [SerializeField] private float randomMax = 1.2f;

        [Header("游戏")]
        [SerializeField] private float gameDuration = 600f;

        public int BridgeHP { get; private set; }
        public float GameTimer { get; private set; }
        public bool IsGameOver { get; private set; }
        public GameOutcome CurrentOutcome { get; private set; } = GameOutcome.InProgress;

        private List<Platoon> platoons = new List<Platoon>();
        private Dictionary<string, EnemyUnit> registeredEnemies = new Dictionary<string, EnemyUnit>();
        private System.Random rng;
        private Dictionary<string, Coroutine> activeCoroutines = new Dictionary<string, Coroutine>();

        public List<Platoon> GetPlatoons() => platoons;
        public Platoon GetPlatoon(string id) => platoons.Find(p => p.UnitId == id);
        public float GameProgress => Mathf.Clamp01(GameTimer / gameDuration);

        #region Lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            rng = new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
        }

        void Start()
        {
            InitializeBattle();
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.OnCommandDelivered += ExecuteCommand;
        }

        void OnDestroy()
        {
            if (GameEventBus.Instance != null)
                GameEventBus.Instance.OnCommandDelivered -= ExecuteCommand;
        }

        void Update()
        {
            if (IsGameOver) return;
            GameTimer += Time.deltaTime;
            TickBridgeDamage();
            TickBridgeRepair();
            TickMoraleDecay();
            TickAmmoConsumption();
            SyncToInterface();
            GameEventBus.Instance?.PublishGameTimeUpdated(GameTimer);
            CheckWinCondition();
            UpdateCampaignPhase();
        }

        #endregion

        #region 初始化

        private void InitializeBattle()
        {
            BridgeHP = bridgeMaxHP;
            GameTimer = 0f;
            IsGameOver = false;
            CurrentOutcome = GameOutcome.InProgress;

            platoons.Clear();
            platoons.Add(MakePlatoon("platoon_1", "红一排", 60, 75f, new Vector3(0, 0, 8)));
            platoons.Add(MakePlatoon("platoon_2", "蓝二排", 52, 70f, new Vector3(-6, 0, 5)));
            platoons.Add(MakePlatoon("platoon_3", "绿三排", 45, 68f, new Vector3(6, 0, 5)));
            platoons.Add(MakePlatoon("platoon_4", "黄四排", 38, 65f, new Vector3(0, 0, 3)));

            registeredEnemies.Clear();
            Debug.Log($"[BattleSimulator] 初始化完成 — 桥HP:{BridgeHP} | 4排待命");
        }

        private Platoon MakePlatoon(string id, string name, int troops, float morale, Vector3 pos)
        {
            return new Platoon
            {
                UnitId = id, DisplayName = name, InitialTroops = troops, CurrentTroops = troops,
                AmmoPercent = 80f, Morale = morale, Position = pos, State = UnitState.Idle,
                HasEngineers = (id == "platoon_3"), LastContactTime = 0f
            };
        }

        #endregion

        #region 桥梁系统

        private float bridgeDamageAccum = 0f;

        private void TickBridgeDamage()
        {
            if (BridgeHP <= 0) return;
            float factor = 0f;
            foreach (var kvp in registeredEnemies)
            {
                var e = kvp.Value;
                if (!e.IsEliminated && e.TargetUnitId == "bridge")
                    factor += e.TroopCount / 50f;
            }
            if (factor <= 0) return;

            bridgeDamageAccum += bridgeDamageRate * factor * Time.deltaTime;
            if (bridgeDamageAccum >= 1f)
            {
                int dmg = Mathf.FloorToInt(bridgeDamageAccum);
                BridgeHP = Mathf.Max(0, BridgeHP - dmg);
                bridgeDamageAccum -= dmg;
            }
        }

        private void TickBridgeRepair()
        {
            if (BridgeHP >= bridgeMaxHP) return;
            foreach (var p in platoons)
            {
                if (p.HasEngineers && !p.IsEliminated && p.State != UnitState.Retreating)
                {
                    BridgeHP = Mathf.Min(bridgeMaxHP, BridgeHP + Mathf.CeilToInt(bridgeRepairRate * Time.deltaTime));
                }
            }
        }

        #endregion

        #region 战斗公式

        /// <summary>
        /// 伤害 = 基础伤害 × 兵力系数 × 士气系数 × 防御修正 × 随机扰动
        /// 兵力系数 = 当前/初始, 士气系数 = 0.5 + morale/200, 防御修正 = 0.6或1.0, 随机 = 0.8~1.2
        /// </summary>
        public float CalculateDamage(Platoon attacker)
        {
            if (attacker == null || attacker.IsEliminated) return 0f;
            float d = baseDamage * attacker.TroopRatio * (0.5f + attacker.Morale / 200f);
            if (attacker.State == UnitState.Defending) d *= defenseModifier;
            d *= (float)(rng.NextDouble() * (randomMax - randomMin) + randomMin);
            if (attacker.AmmoPercent < 20f) d *= 0.5f;
            return Mathf.Max(0f, d);
        }

        /// <summary>EnemyWaveManager 兼容：敌军 vs 我方伤害计算</summary>
        public float CalculateDamage(EnemyUnit attacker, Platoon defender)
        {
            if (attacker == null || attacker.IsEliminated || defender == null) return 0f;
            float d = attacker.AttackPower;
            if (defender.State == UnitState.Defending) d *= 0.6f;
            d *= (float)(rng.NextDouble() * 0.4 + 0.8);
            return Mathf.Max(0f, d);
        }

        #endregion

        #region 士气系统

        public void ModifyMorale(string unitId, float delta, string reason)
        {
            var p = GetPlatoon(unitId);
            if (p == null) return;
            p.Morale = Mathf.Clamp(p.Morale + delta, 0f, 100f);
            if (p.Morale < 15f && p.State != UnitState.Retreating)
            {
                p.State = UnitState.Retreating;
                Debug.LogWarning($"[BattleSimulator] {p.DisplayName} 士气崩溃，撤退！");
            }
        }

        private void TickMoraleDecay()
        {
            foreach (var p in platoons)
            {
                if (p.IsEliminated) continue;
                if (p.AmmoPercent < 20f) ModifyMorale(p.UnitId, -3f * Time.deltaTime, "弹药不足");
                if (GameTimer - p.LastContactTime > 150f) ModifyMorale(p.UnitId, -2f * Time.deltaTime, "长时间无通讯");
            }
        }

        private void TickAmmoConsumption()
        {
            foreach (var p in platoons)
            {
                if (p.IsEliminated) continue;
                if (p.State == UnitState.Engaging || p.State == UnitState.Defending)
                    p.AmmoPercent = Mathf.Max(0f, p.AmmoPercent - 1.2f * Time.deltaTime);
            }
        }

        #endregion

        #region 指令执行

        /// <summary>根据 RadioCommand 类型执行数值效果</summary>
        public void ExecuteCommand(RadioCommand cmd)
        {
            if (cmd == null || IsGameOver) return;
            var p = GetPlatoon(cmd.TargetUnitId);
            if (p == null) { Debug.LogWarning($"[BattleSimulator] 未找到部队 {cmd.TargetUnitId}"); return; }

            p.LastContactTime = GameTimer;
            switch (cmd.Type)
            {
                case CommandType.Move:           ExecMove(p); break;
                case CommandType.Attack:         ExecAttack(p); break;
                case CommandType.Defend:         ExecDefend(p); break;
                case CommandType.ArtilleryStrike:ExecArtillery(); break;
                case CommandType.Supply:         ExecSupply(p); break;
                case CommandType.Recon:          ExecRecon(p); break;
                case CommandType.Retreat:        ExecRetreat(p); break;
            }
        }

        private void ExecMove(Platoon p)
        {
            float delay = UnityEngine.Random.Range(30f, 60f);
            p.State = UnitState.Moving;
            StartCmd(p, delay, () => {
                p.Position += new Vector3(UnityEngine.Random.Range(-3f, 3f), 0, UnityEngine.Random.Range(-2f, 2f));
                p.State = UnitState.Idle;
            });
        }

        private void ExecAttack(Platoon p)
        {
            float delay = UnityEngine.Random.Range(30f, 60f);
            p.State = UnitState.Engaging;
            StartCmd(p, delay, () => {
                float dmg = CalculateDamage(p);
                ApplyDamageToEnemies(dmg, p);
                Debug.Log($"[BattleSimulator] {p.DisplayName} 攻击造成 {dmg:F1} 伤害");
            });
        }

        private void ExecDefend(Platoon p)
        {
            StartCmd(p, UnityEngine.Random.Range(15f, 30f), () => {
                p.State = UnitState.Defending;
                Debug.Log($"[BattleSimulator] {p.DisplayName} 进入防御");
            });
        }

        private void ExecArtillery()
        {
            float delay = UnityEngine.Random.Range(45f, 90f);
            // 炮击是全局效果，用第一个排作为触发源
            var source = platoons.Find(p => !p.IsEliminated);
            if (source == null) return;
            StartCmd(source, delay, () => {
                float dmg = baseDamage * 2.5f * (float)(rng.NextDouble() * 0.4 + 0.8);
                ApplyDamageToEnemies(dmg, null);
                foreach (var pl in platoons) if (!pl.IsEliminated) ModifyMorale(pl.UnitId, 8f, "收到炮击支援");
                Debug.Log($"[BattleSimulator] 炮击覆盖 — 伤害 {dmg:F1}");
            });
        }

        private void ExecSupply(Platoon p)
        {
            StartCmd(p, UnityEngine.Random.Range(60f, 120f), () => {
                p.AmmoPercent = Mathf.Min(100f, p.AmmoPercent + 40f);
                ModifyMorale(p.UnitId, 5f, "收到补给");
                Debug.Log($"[BattleSimulator] {p.DisplayName} 补给 → 弹药 {p.AmmoPercent:F0}%");
            });
        }

        private void ExecRecon(Platoon p)
        {
            p.State = UnitState.Engaging;
            StartCmd(p, UnityEngine.Random.Range(20f, 40f), () => p.State = UnitState.Idle);
        }

        private void ExecRetreat(Platoon p)
        {
            p.State = UnitState.Retreating;
            StartCmd(p, UnityEngine.Random.Range(15f, 30f), () => {
                p.Position = new Vector3(p.Position.x, 0, Mathf.Max(0, p.Position.z - 5f));
                p.State = UnitState.Idle;
            });
        }

        private void StartCmd(Platoon p, float delay, Action cb)
        {
            if (activeCoroutines.ContainsKey(p.UnitId)) StopCoroutine(activeCoroutines[p.UnitId]);
            activeCoroutines[p.UnitId] = StartCoroutine(Delayed(delay, cb));
        }

        private IEnumerator Delayed(float sec, Action cb) { yield return new WaitForSeconds(sec); cb?.Invoke(); }

        #endregion

        #region 伤害应用

        private void ApplyDamageToEnemies(float damage, Platoon source)
        {
            if (registeredEnemies.Count == 0) return;
            float perUnit = damage / registeredEnemies.Count;
            var toRemove = new List<string>();
            foreach (var kvp in registeredEnemies)
            {
                if (kvp.Value.IsEliminated) continue;
                kvp.Value.TroopCount -= Mathf.RoundToInt(perUnit);
                if (kvp.Value.TroopCount <= 0)
                {
                    kvp.Value.TroopCount = 0;
                    kvp.Value.IsEliminated = true;
                    toRemove.Add(kvp.Key);
                    if (source != null) ModifyMorale(source.UnitId, 10f, "成功击退敌军");
                }
            }
            foreach (var id in toRemove) registeredEnemies.Remove(id);
        }

        /// <summary>对我方排造成伤害（EnemyWaveManager 调用）</summary>
        public void ApplyDamageToUnit(string unitId, float damage)
        {
            var p = GetPlatoon(unitId);
            if (p == null) return;
            int casualties = Mathf.Max(1, Mathf.RoundToInt(damage));
            p.CurrentTroops = Mathf.Max(0, p.CurrentTroops - casualties);
            if (casualties > 5) ModifyMorale(unitId, -15f, "遭受重大伤亡");
            if (p.IsEliminated)
            {
                p.State = UnitState.Retreating;
                Debug.LogWarning($"[BattleSimulator] {p.DisplayName} 被全歼！");
            }
        }

        #endregion

        #region EnemyWaveManager 兼容接口

        /// <summary>注册敌军单位（EnemyWaveManager 调用）</summary>
        public void RegisterEnemyUnit(string unitId, int troops, float attackPower, Vector3 spawnPos)
        {
            registeredEnemies[unitId] = new EnemyUnit
            {
                UnitId = unitId,
                TroopCount = troops,
                MaxTroopCount = troops,
                AttackPower = attackPower,
                Position = spawnPos,
                IsEngaging = false,
                IsEliminated = false,
                TargetUnitId = null
            };
        }

        /// <summary>注销敌军单位（EnemyWaveManager 调用）</summary>
        public void UnregisterEnemyUnit(string unitId)
        {
            registeredEnemies.Remove(unitId);
        }

        /// <summary>获取敌军单位</summary>
        public EnemyUnit GetEnemyUnit(string unitId)
        {
            registeredEnemies.TryGetValue(unitId, out var e);
            return e;
        }

        /// <summary>获取我方单位（返回 Platoon 适配）</summary>
        public Platoon GetFriendlyUnit(string unitId)
        {
            return GetPlatoon(unitId);
        }

        /// <summary>获取所有存活我方单位</summary>
        public List<Platoon> GetFriendlyUnits()
        {
            return platoons.FindAll(p => !p.IsEliminated);
        }

        /// <summary>敌军攻击桥梁</summary>
        public void EnemyAttackBridge(string enemyUnitId, float interval)
        {
            if (registeredEnemies.TryGetValue(enemyUnitId, out var e))
            {
                e.TargetUnitId = "bridge";
                e.IsEngaging = true;
            }
        }

        #endregion

        #region 同步 & 阶段 & 胜负

        private void SyncToInterface()
        {
            if (BattleSimulationInterface.Instance == null) return;
            foreach (var p in platoons)
            {
                BattleSimulationInterface.Instance.UpdateUnitStatus(p.UnitId, new UnitStatusSnapshot
                {
                    UnitId = p.UnitId, Morale = p.Morale, TroopCount = p.CurrentTroops,
                    AmmoLevel = p.AmmoPercent, IsInCombat = p.State == UnitState.Engaging,
                    Position = p.Position, Timestamp = Time.time
                });
            }
        }

        private float lastPhaseCheck = -1f;
        private void UpdateCampaignPhase()
        {
            if (GameTimer - lastPhaseCheck < 30f) return;
            lastPhaseCheck = GameTimer;
            CampaignPhase phase;
            if (GameTimer < 30f) phase = CampaignPhase.Briefing;
            else if (GameTimer < 150f) phase = CampaignPhase.FirstWaveLanding;
            else if (GameTimer < 300f) phase = CampaignPhase.SecondWaveLanding;
            else if (GameTimer < 450f) phase = CampaignPhase.CounterAttack;
            else phase = CampaignPhase.CriticalDecision;
            GameEventBus.Instance?.PublishCampaignPhaseChanged(phase);
        }

        private void CheckWinCondition()
        {
            if (IsGameOver) return;

            if (BridgeHP <= 0) { EndGame(GameOutcome.Defeat, "桥梁被炸毁"); return; }

            bool allDead = true;
            foreach (var p in platoons) if (!p.IsEliminated) { allDead = false; break; }
            if (allDead) { EndGame(GameOutcome.TotalDefeat, "所有部队被歼灭"); return; }

            if (GameTimer >= gameDuration)
            {
                float totalRatio = 0f; int alive = 0;
                foreach (var p in platoons) if (!p.IsEliminated) { totalRatio += p.TroopRatio; alive++; }
                float avg = alive > 0 ? totalRatio / alive : 0f;
                var outcome = avg >= 0.6f && BridgeHP >= bridgeMaxHP * 0.5f ? GameOutcome.PerfectVictory
                            : avg >= 0.4f || BridgeHP > 0 ? GameOutcome.PyrrhicVictory : GameOutcome.PartialVictory;
                EndGame(outcome, $"坚守成功 — 桥HP:{BridgeHP} 存活率:{avg:P0}");
            }
        }

        private void EndGame(GameOutcome outcome, string reason)
        {
            IsGameOver = true;
            CurrentOutcome = outcome;
            Debug.Log($"[BattleSimulator] 游戏结束: {outcome} — {reason}");
            GameEventBus.Instance?.PublishGameOutcomeChanged(outcome);
            GameEventBus.Instance?.PublishCampaignPhaseChanged(CampaignPhase.Resolution);
        }

        #endregion
    }
}
