// StandaloneTestRunner.cs — WW2 Commander 独立测试运行器
// 不依赖 Unity，可直接用 dotnet/mcs 编译运行
// 测试范围：BattleSimulator 逻辑、GameDirector 逻辑、CommandSystem 逻辑
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SWO1.Tests
{
    #region Unity Mock — 最小化模拟 Unity API

    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
        public static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        public static float Clamp01(float value) => Clamp(value, 0f, 1f);
        public static int RoundToInt(float value) => (int)Math.Round(value);
        public static int FloorToInt(float value) => (int)Math.Floor(value);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Abs(float value) => Math.Abs(value);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        private static Random _rng = new Random(42);
        public static float RandomRange(float min, float max) => (float)(_rng.NextDouble() * (max - min) + min);
        public static int RandomRange(int min, int max) => _rng.Next(min, max);
        public static void SeedRandom(int seed) => _rng = new Random(seed);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);

        public static float Distance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
    }

    #endregion

    #region 数据模型 (从 GameModels.cs 提取的纯逻辑版本)

    public class CombatUnit
    {
        public string UnitId;
        public int TroopCount;
        public int MaxTroopCount;
        public float Morale;        // 0-100
        public float AmmoLevel;     // 0-100
        public Vector3 Position;
        public bool IsDefending;
        public bool IsEliminated;

        public float TroopRatio => MaxTroopCount > 0 ? (float)TroopCount / MaxTroopCount : 0f;
        public float MoraleCoeff => 0.5f + (Morale / 200f);
        public float DefenseModifier => IsDefending ? 0.6f : 1.0f;
    }

    public enum CampaignPhase
    {
        Briefing, Embarkation, FirstWaveLanding, FirstReports,
        SecondWaveLanding, ThirdWaveLanding, CounterAttack,
        CriticalDecision, Resolution
    }

    public enum GameOutcome
    {
        InProgress, PerfectVictory, PyrrhicVictory,
        PartialVictory, Defeat, TotalDefeat
    }

    public enum Difficulty { Easy, Normal, Hard }

    public enum CommandType
    {
        Move, Attack, Defend, Retreat, Recon,
        ArtilleryStrike, StatusQuery, Supply, Custom
    }

    public enum CommandStatus
    {
        Draft, Sending, InTransit, Delivered,
        Acknowledged, Executing, Completed, Lost, Failed
    }

    #endregion

    #region BattleSimulator 逻辑引擎 (无 MonoBehaviour)

    public class BattleSimulatorLogic
    {
        public float BridgeHP { get; private set; } = 100f;
        public float BridgeMaxHP { get; private set; } = 100f;
        public float BridgeDamagePerSecond { get; set; } = 2f;
        public float BaseDamage { get; set; } = 10f;
        public float RandomPerturbMin { get; set; } = 0.8f;
        public float RandomPerturbMax { get; set; } = 1.2f;
        public float LowAmmoMoralePenalty { get; set; } = -5f;
        public float NoCommMoralePenalty { get; set; } = -3f;
        public float AmmoLowThreshold { get; set; } = 20f;

        private Dictionary<string, CombatUnit> friendlyUnits = new Dictionary<string, CombatUnit>();
        private Dictionary<string, CombatUnit> enemyUnits = new Dictionary<string, CombatUnit>();

        public int TotalFriendlyCasualties { get; private set; }
        public int TotalEnemyCasualties { get; private set; }
        public int InitialFriendlyTroops { get; private set; }
        public int InitialEnemyTroops { get; private set; }

        public float BridgeHPRatio => BridgeMaxHP > 0 ? BridgeHP / BridgeMaxHP : 0f;
        public bool IsBridgeDestroyed => BridgeHP <= 0f;

        // 事件追踪
        public List<string> EventLog { get; } = new List<string>();

        public void RegisterFriendlyUnit(string unitId, int troopCount, float morale, float ammo, Vector3 position)
        {
            var unit = new CombatUnit
            {
                UnitId = unitId, TroopCount = troopCount, MaxTroopCount = troopCount,
                Morale = morale, AmmoLevel = ammo, Position = position,
                IsDefending = false, IsEliminated = false
            };
            friendlyUnits[unitId] = unit;
            InitialFriendlyTroops += troopCount;
        }

        public void RegisterEnemyUnit(string unitId, int troopCount, float morale, Vector3 position)
        {
            var unit = new CombatUnit
            {
                UnitId = unitId, TroopCount = troopCount, MaxTroopCount = troopCount,
                Morale = morale, AmmoLevel = 100f, Position = position,
                IsDefending = false, IsEliminated = false
            };
            enemyUnits[unitId] = unit;
            InitialEnemyTroops += troopCount;
        }

        public void UnregisterEnemyUnit(string unitId) => enemyUnits.Remove(unitId);

        public float CalculateDamage(CombatUnit attacker, CombatUnit defender)
        {
            float damage = BaseDamage * attacker.TroopRatio * attacker.MoraleCoeff
                           * defender.DefenseModifier
                           * Mathf.RandomRange(RandomPerturbMin, RandomPerturbMax);
            return Mathf.Max(0f, damage);
        }

        public int ApplyDamageToUnit(string defenderId, float damage)
        {
            if (!friendlyUnits.TryGetValue(defenderId, out var unit)) return 0;
            if (unit.IsEliminated) return 0;

            int actualDamage = Mathf.Min(Mathf.RoundToInt(damage), unit.TroopCount);
            unit.TroopCount -= actualDamage;
            TotalFriendlyCasualties += actualDamage;

            if (unit.TroopCount <= 0)
            {
                unit.TroopCount = 0;
                unit.IsEliminated = true;
                EventLog.Add($"UnitEliminated:{defenderId}");
            }

            if (actualDamage > unit.MaxTroopCount * 0.1f)
                ModifyMorale(defenderId, -15f);

            return actualDamage;
        }

        public int ApplyDamageToEnemy(string enemyId, float damage)
        {
            if (!enemyUnits.TryGetValue(enemyId, out var unit)) return 0;
            if (unit.IsEliminated) return 0;

            int actualDamage = Mathf.Min(Mathf.RoundToInt(damage), unit.TroopCount);
            unit.TroopCount -= actualDamage;
            TotalEnemyCasualties += actualDamage;

            if (unit.TroopCount <= 0)
            {
                unit.TroopCount = 0;
                unit.IsEliminated = true;
                EventLog.Add($"EnemyEliminated:{enemyId}");
            }

            return actualDamage;
        }

        public float ApplyBridgeDamage(float damagePerSecond, float deltaTime)
        {
            if (BridgeHP <= 0f) return 0f;
            float damage = damagePerSecond * deltaTime;
            BridgeHP = Mathf.Max(0f, BridgeHP - damage);
            if (BridgeHP <= 0f) EventLog.Add("BridgeDestroyed");
            return damage;
        }

        public void EnemyAttackBridge(string enemyId, float deltaTime)
        {
            if (!enemyUnits.TryGetValue(enemyId, out var unit)) return;
            if (unit.IsEliminated) return;
            float multiplier = 1f + (unit.TroopRatio * 0.5f);
            ApplyBridgeDamage(BridgeDamagePerSecond * multiplier, deltaTime);
        }

        public void ModifyMorale(string unitId, float delta)
        {
            if (!friendlyUnits.TryGetValue(unitId, out var unit)) return;
            unit.Morale = Mathf.Clamp(unit.Morale + delta, 0f, 100f);
        }

        public void ApplyReinforcementBonus(string unitId) => ModifyMorale(unitId, 5f);
        public void ApplyRepelBonus(string unitId) => ModifyMorale(unitId, 10f);
        public void ApplyArtillerySupportBonus(string unitId) => ModifyMorale(unitId, 8f);

        public void SetUnitDefending(string unitId, bool defending)
        {
            if (friendlyUnits.TryGetValue(unitId, out var unit))
                unit.IsDefending = defending;
        }

        public void ApplyMoraleTick()
        {
            foreach (var kvp in friendlyUnits)
            {
                var unit = kvp.Value;
                if (unit.IsEliminated) continue;
                if (unit.AmmoLevel < AmmoLowThreshold)
                    ModifyMorale(kvp.Key, LowAmmoMoralePenalty);
            }
        }

        public void ApplyNoCommPenalty(string unitId) => ModifyMorale(unitId, NoCommMoralePenalty);

        public CombatUnit GetFriendlyUnit(string unitId)
        {
            friendlyUnits.TryGetValue(unitId, out var unit);
            return unit;
        }

        public CombatUnit GetEnemyUnit(string unitId)
        {
            enemyUnits.TryGetValue(unitId, out var unit);
            return unit;
        }

        public bool AreAllFriendlyUnitsEliminated()
        {
            foreach (var unit in friendlyUnits.Values)
                if (!unit.IsEliminated) return false;
            return friendlyUnits.Count > 0;
        }

        public float GetFriendlyCasualtyRate()
        {
            return InitialFriendlyTroops > 0 ? (float)TotalFriendlyCasualties / InitialFriendlyTroops : 0f;
        }

        public void RepairBridge(float amount)
        {
            BridgeHP = Mathf.Min(BridgeMaxHP, BridgeHP + amount);
        }

        public void Reset()
        {
            BridgeHP = 100f;
            friendlyUnits.Clear();
            enemyUnits.Clear();
            TotalFriendlyCasualties = 0;
            TotalEnemyCasualties = 0;
            InitialFriendlyTroops = 0;
            InitialEnemyTroops = 0;
            EventLog.Clear();
        }
    }

    #endregion

    #region GameDirector 逻辑引擎 (无 MonoBehaviour)

    public class GameDirectorLogic
    {
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;
        public float TimeScale { get; set; } = 1f;
        public float CampaignStartGameTime { get; set; } = 360f; // 06:00
        public float CampaignEndGameTime { get; set; } = 540f;   // 09:00

        public float CurrentGameTime { get; private set; }
        public CampaignPhase CurrentPhase { get; private set; } = CampaignPhase.Briefing;
        public bool IsPaused { get; private set; }
        public GameOutcome Outcome { get; private set; } = GameOutcome.InProgress;

        private bool[] objectivesCaptured = new bool[3];

        public List<string> EventLog { get; } = new List<string>();

        public void Initialize()
        {
            CurrentGameTime = CampaignStartGameTime;
            CurrentPhase = CampaignPhase.Briefing;
            Outcome = GameOutcome.InProgress;
            objectivesCaptured = new bool[3];
            EventLog.Clear();
        }

        public void Update(float deltaTime)
        {
            if (IsPaused || Outcome != GameOutcome.InProgress) return;

            CurrentGameTime += (deltaTime * TimeScale) / 60f;
            UpdatePhase();
            CheckOutcome();
        }

        private void UpdatePhase()
        {
            CampaignPhase newPhase;
            if (CurrentGameTime >= 540f) newPhase = CampaignPhase.Resolution;
            else if (CurrentGameTime >= 480f) newPhase = CampaignPhase.CriticalDecision;
            else if (CurrentGameTime >= 450f) newPhase = CampaignPhase.CounterAttack;
            else if (CurrentGameTime >= 420f) newPhase = CampaignPhase.ThirdWaveLanding;
            else if (CurrentGameTime >= 405f) newPhase = CampaignPhase.SecondWaveLanding;
            else if (CurrentGameTime >= 395f) newPhase = CampaignPhase.FirstReports;
            else if (CurrentGameTime >= 390f) newPhase = CampaignPhase.FirstWaveLanding;
            else if (CurrentGameTime >= 375f) newPhase = CampaignPhase.Embarkation;
            else newPhase = CampaignPhase.Briefing;

            if (newPhase != CurrentPhase)
            {
                CurrentPhase = newPhase;
                EventLog.Add($"PhaseChanged:{newPhase}");
            }
        }

        public void ReportObjectiveCaptured(int index)
        {
            if (index >= 0 && index < objectivesCaptured.Length)
                objectivesCaptured[index] = true;
        }

        private void CheckOutcome()
        {
            if (CurrentGameTime < CampaignEndGameTime) return;
            int captured = objectivesCaptured.Count(o => o);
            if (captured >= 1) Outcome = GameOutcome.PartialVictory;
            else Outcome = GameOutcome.Defeat;
            EventLog.Add($"OutcomeDetermined:{Outcome}");
        }

        public void SetFinalOutcome(float casualtyRate)
        {
            int captured = objectivesCaptured.Count(o => o);
            if (captured == 3 && casualtyRate < 0.3f) Outcome = GameOutcome.PerfectVictory;
            else if (captured == 3 && casualtyRate >= 0.5f) Outcome = GameOutcome.PyrrhicVictory;
            else if (captured == 3) Outcome = GameOutcome.PartialVictory;
            else if (captured >= 1) Outcome = GameOutcome.PartialVictory;
            else Outcome = GameOutcome.Defeat;
            EventLog.Add($"FinalOutcome:{Outcome}(casualty={casualtyRate:F2})");
        }

        public void SetTotalDefeat()
        {
            Outcome = GameOutcome.TotalDefeat;
            EventLog.Add("TotalDefeat");
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        public string GetFormattedTime()
        {
            int hours = Mathf.FloorToInt(CurrentGameTime / 60f);
            int minutes = Mathf.FloorToInt(CurrentGameTime % 60f);
            return $"{hours:D2}:{minutes:D2}";
        }
    }

    #endregion

    #region CommandSystem 逻辑引擎 (无 MonoBehaviour)

    public class CommandSystemLogic
    {
        public float BaseDeliveryTime { get; set; } = 30f;
        public float[] LossChanceByDifficulty = { 0.05f, 0.15f, 0.30f };
        public float[] DelayMultiplierByDifficulty = { 0.7f, 1.0f, 1.8f };
        public float[] MisinterpretChanceByMorale = { 0.05f, 0.12f, 0.25f, 0.45f };
        public float NoReplyChance { get; set; } = 0.15f;

        private Dictionary<string, (CommandType type, string content)> pendingCommands
            = new Dictionary<string, (CommandType, string)>();
        public List<(string id, CommandStatus status)> CommandHistory = new List<(string, CommandStatus)>();

        public List<string> EventLog { get; } = new List<string>();

        /// <summary>计算指令延迟 (确定性版本)</summary>
        public float CalculateDelay(CommandType type, Difficulty difficulty)
        {
            float diffMult = DelayMultiplierByDifficulty[(int)difficulty];
            float typeDelay = type switch
            {
                CommandType.StatusQuery or CommandType.Supply => 45f,
                CommandType.Move or CommandType.Attack or CommandType.Retreat => 120f,
                CommandType.ArtilleryStrike => 67f,
                _ => 75f
            };
            return BaseDeliveryTime * diffMult + typeDelay;
        }

        /// <summary>获取丢失概率</summary>
        public float GetLossChance(Difficulty difficulty)
        {
            int idx = Mathf.Clamp((int)difficulty, 0, 2);
            return LossChanceByDifficulty[idx];
        }

        /// <summary>获取误解概率</summary>
        public float GetMisinterpretChance(float morale)
        {
            int idx;
            if (morale >= 80) idx = 0;
            else if (morale >= 50) idx = 1;
            else if (morale >= 30) idx = 2;
            else idx = 3;
            return MisinterpretChanceByMorale[idx];
        }

        /// <summary>模拟指令送达 (确定性)</summary>
        public CommandStatus SimulateDelivery(CommandType type, float morale, Difficulty difficulty, float roll)
        {
            float lossChance = GetLossChance(difficulty);
            if (roll < lossChance)
            {
                EventLog.Add($"CommandLost:{type}");
                return CommandStatus.Lost;
            }

            float adjustedRoll = roll - lossChance;
            float misChance = GetMisinterpretChance(morale);
            if (adjustedRoll < misChance * (1f - lossChance))
            {
                EventLog.Add($"CommandMisinterpreted:{type}");
                return CommandStatus.Delivered; // 误解也算送达
            }

            EventLog.Add($"CommandDelivered:{type}");
            return CommandStatus.Delivered;
        }

        /// <summary>生成误解指令文本</summary>
        public string GenerateMisinterpretation(CommandType type, string content)
        {
            return type switch
            {
                CommandType.Move => content + "（方向可能偏差15°）",
                CommandType.Attack => content.Replace("攻击", "绕过"),
                CommandType.Defend => content, // 防御不会误解
                CommandType.Retreat => content.Replace("撤退", "原地待命"),
                CommandType.ArtilleryStrike => content + "（坐标可能有误）",
                _ => content + "（理解不确定）"
            };
        }

        public void Reset()
        {
            pendingCommands.Clear();
            CommandHistory.Clear();
            EventLog.Clear();
        }
    }

    #endregion

    #region 测试框架

    public class TestResult
    {
        public string Name;
        public bool Passed;
        public string Message;
        public string Category;
    }

    public static class TestRunner
    {
        private static List<TestResult> results = new List<TestResult>();

        public static void Assert(bool condition, string name, string category, string failMsg = "")
        {
            results.Add(new TestResult
            {
                Name = name,
                Passed = condition,
                Message = condition ? "OK" : $"FAIL: {failMsg}",
                Category = category
            });
        }

        public static void AssertNear(float actual, float expected, float tolerance, string name, string category)
        {
            bool pass = Mathf.Abs(actual - expected) <= tolerance;
            results.Add(new TestResult
            {
                Name = name,
                Passed = pass,
                Message = pass ? "OK" : $"FAIL: expected ~{expected:F3}, got {actual:F3} (tol={tolerance:F3})",
                Category = category
            });
        }

        public static void AssertEqual<T>(T actual, T expected, string name, string category) where T : IEquatable<T>
        {
            bool pass = actual.Equals(expected);
            results.Add(new TestResult
            {
                Name = name,
                Passed = pass,
                Message = pass ? "OK" : $"FAIL: expected {expected}, got {actual}",
                Category = category
            });
        }

        public static List<TestResult> GetResults() => results;

        public static void PrintSummary()
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("WW2 Commander 集成测试报告");
            Console.WriteLine(new string('=', 70));

            var categories = results.GroupBy(r => r.Category);
            foreach (var cat in categories)
            {
                int passed = cat.Count(r => r.Passed);
                int total = cat.Count();
                Console.WriteLine($"\n[{cat.Key}] {passed}/{total} 通过");

                foreach (var r in cat)
                {
                    string icon = r.Passed ? "✅" : "❌";
                    Console.WriteLine($"  {icon} {r.Name}: {r.Message}");
                }
            }

            int totalPassed = results.Count(r => r.Passed);
            int totalAll = results.Count();
            double passRate = totalAll > 0 ? (double)totalPassed / totalAll * 100 : 0;

            Console.WriteLine($"\n{new string('-', 70)}");
            Console.WriteLine($"总计: {totalPassed}/{totalAll} 通过 ({passRate:F1}%)");
            Console.WriteLine(new string('=', 70));
        }
    }

    #endregion

    #region 测试用例

    public static class BattleSimulatorTests
    {
        public static void RunAll()
        {
            TestDamageCalculation();
            TestMoraleCoefficients();
            TestDefenseModifier();
            TestBridgeDamage();
            TestBridgeDestroyed();
            TestUnitElimination();
            TestMoraleSystem();
            TestCasualtyTracking();
            TestAllUnitsEliminated();
            TestBridgeRepair();
            TestEdgeCases();
        }

        static void TestDamageCalculation()
        {
            var sim = new BattleSimulatorLogic();
            var attacker = new CombatUnit { TroopCount = 50, MaxTroopCount = 100, Morale = 75f, IsDefending = false };
            var defender = new CombatUnit { TroopCount = 50, MaxTroopCount = 100, Morale = 50f, IsDefending = false };

            // 手动计算期望值: 10 * 0.5 * (0.5+75/200) * 1.0 * random(0.8~1.2)
            // = 10 * 0.5 * 0.875 * 1.0 * [0.8~1.2] = [3.5 ~ 5.25]
            float damage = sim.CalculateDamage(attacker, defender);
            TestRunner.Assert(damage >= 0f, "伤害值非负", "BattleSimulator",
                $"伤害值 {damage:F3} 为负数");
            TestRunner.Assert(damage >= 3.5f && damage <= 5.25f, "伤害值在预期范围内", "BattleSimulator",
                $"伤害值 {damage:F3} 不在 [3.5, 5.25] 范围内");
        }

        static void TestMoraleCoefficients()
        {
            // 士气系数 = 0.5 + morale/200
            var unit100 = new CombatUnit { Morale = 100f };
            var unit50 = new CombatUnit { Morale = 50f };
            var unit0 = new CombatUnit { Morale = 0f };

            TestRunner.AssertNear(unit100.MoraleCoeff, 1.0f, 0.001f, "士气100→系数1.0", "BattleSimulator");
            TestRunner.AssertNear(unit50.MoraleCoeff, 0.75f, 0.001f, "士气50→系数0.75", "BattleSimulator");
            TestRunner.AssertNear(unit0.MoraleCoeff, 0.5f, 0.001f, "士气0→系数0.5", "BattleSimulator");
        }

        static void TestDefenseModifier()
        {
            var unit = new CombatUnit { IsDefending = false };
            TestRunner.AssertNear(unit.DefenseModifier, 1.0f, 0.001f, "非防御状态修正=1.0", "BattleSimulator");
            unit.IsDefending = true;
            TestRunner.AssertNear(unit.DefenseModifier, 0.6f, 0.001f, "防御状态修正=0.6", "BattleSimulator");
        }

        static void TestBridgeDamage()
        {
            var sim = new BattleSimulatorLogic();
            float initialHP = sim.BridgeHP;

            float damage = sim.ApplyBridgeDamage(2f, 10f); // 2 DPS * 10s = 20 damage
            TestRunner.AssertNear(damage, 20f, 0.001f, "桥头堡伤害计算正确", "BattleSimulator");
            TestRunner.AssertNear(sim.BridgeHP, 80f, 0.001f, "桥头堡HP减少正确", "BattleSimulator");
        }

        static void TestBridgeDestroyed()
        {
            var sim = new BattleSimulatorLogic();
            sim.ApplyBridgeDamage(100f, 2f); // 200 damage → HP = 0
            TestRunner.Assert(sim.BridgeHP <= 0f, "桥头堡HP降为0", "BattleSimulator",
                $"桥头堡HP = {sim.BridgeHP}");
            TestRunner.Assert(sim.IsBridgeDestroyed, "IsBridgeDestroyed = true", "BattleSimulator",
                $"IsBridgeDestroyed = {sim.IsBridgeDestroyed}");
            TestRunner.Assert(sim.EventLog.Contains("BridgeDestroyed"), "触发BridgeDestroyed事件", "BattleSimulator",
                $"事件日志: [{string.Join(", ", sim.EventLog)}]");

            // 已摧毁后不应再造成伤害
            float extraDamage = sim.ApplyBridgeDamage(10f, 1f);
            TestRunner.AssertEqual(extraDamage, 0f, "已摧毁后不再受伤害", "BattleSimulator");
        }

        static void TestUnitElimination()
        {
            var sim = new BattleSimulatorLogic();
            sim.RegisterFriendlyUnit("test_unit", 10, 75f, 80f, Vector3.zero);

            int dmg = sim.ApplyDamageToUnit("test_unit", 15f); // 超过10人
            TestRunner.AssertEqual(dmg, 10, "实际伤害不超过剩余兵力", "BattleSimulator");

            var unit = sim.GetFriendlyUnit("test_unit");
            TestRunner.Assert(unit.IsEliminated, "部队标记为歼灭", "BattleSimulator",
                $"TroopCount={unit.TroopCount}, IsEliminated={unit.IsEliminated}");
            TestRunner.AssertEqual(unit.TroopCount, 0, "歼灭后兵力为0", "BattleSimulator");
        }

        static void TestMoraleSystem()
        {
            var sim = new BattleSimulatorLogic();
            sim.RegisterFriendlyUnit("morale_test", 100, 50f, 80f, Vector3.zero);

            // 增援 +5
            sim.ApplyReinforcementBonus("morale_test");
            TestRunner.AssertNear(sim.GetFriendlyUnit("morale_test").Morale, 55f, 0.001f, "增援指令→士气+5", "BattleSimulator");

            // 击退 +10
            sim.ApplyRepelBonus("morale_test");
            TestRunner.AssertNear(sim.GetFriendlyUnit("morale_test").Morale, 65f, 0.001f, "击退敌军→士气+10", "BattleSimulator");

            // 炮击 +8
            sim.ApplyArtillerySupportBonus("morale_test");
            TestRunner.AssertNear(sim.GetFriendlyUnit("morale_test").Morale, 73f, 0.001f, "炮击支援→士气+8", "BattleSimulator");

            // 无通讯 -3
            sim.ApplyNoCommPenalty("morale_test");
            TestRunner.AssertNear(sim.GetFriendlyUnit("morale_test").Morale, 70f, 0.001f, "无通讯→士气-3", "BattleSimulator");

            // 士气边界: 上限100
            sim.ModifyMorale("morale_test", 50f);
            TestRunner.AssertEqual(sim.GetFriendlyUnit("morale_test").Morale, 100f, "士气上限100", "BattleSimulator");

            // 士气边界: 下限0
            sim.ModifyMorale("morale_test", -200f);
            TestRunner.AssertEqual(sim.GetFriendlyUnit("morale_test").Morale, 0f, "士气下限0", "BattleSimulator");
        }

        static void TestCasualtyTracking()
        {
            var sim = new BattleSimulatorLogic();
            sim.RegisterFriendlyUnit("unit_a", 50, 75f, 80f, Vector3.zero);
            sim.RegisterFriendlyUnit("unit_b", 50, 75f, 80f, Vector3.zero);
            sim.RegisterEnemyUnit("enemy_a", 100, 60f, Vector3.zero);

            TestRunner.AssertEqual(sim.InitialFriendlyTroops, 100, "初始我方兵力=100", "BattleSimulator");
            TestRunner.AssertEqual(sim.InitialEnemyTroops, 100, "初始敌方兵力=100", "BattleSimulator");

            sim.ApplyDamageToUnit("unit_a", 10f);
            sim.ApplyDamageToUnit("unit_b", 5f);
            TestRunner.AssertEqual(sim.TotalFriendlyCasualties, 15, "我方伤亡统计正确", "BattleSimulator");

            sim.ApplyDamageToEnemy("enemy_a", 20f);
            TestRunner.AssertEqual(sim.TotalEnemyCasualties, 20, "敌方伤亡统计正确", "BattleSimulator");

            float rate = sim.GetFriendlyCasualtyRate();
            TestRunner.AssertNear(rate, 0.15f, 0.001f, "伤亡率=15%", "BattleSimulator");
        }

        static void TestAllUnitsEliminated()
        {
            var sim = new BattleSimulatorLogic();
            TestRunner.Assert(!sim.AreAllFriendlyUnitsEliminated(), "空部队列表不算全灭", "BattleSimulator");

            sim.RegisterFriendlyUnit("solo", 5, 75f, 80f, Vector3.zero);
            TestRunner.Assert(!sim.AreAllFriendlyUnitsEliminated(), "有存活部队时未全灭", "BattleSimulator");

            sim.ApplyDamageToUnit("solo", 10f);
            TestRunner.Assert(sim.AreAllFriendlyUnitsEliminated(), "全部歼灭时返回true", "BattleSimulator");
        }

        static void TestBridgeRepair()
        {
            var sim = new BattleSimulatorLogic();
            sim.ApplyBridgeDamage(50f, 1f); // HP=50
            sim.RepairBridge(30f);
            TestRunner.AssertNear(sim.BridgeHP, 80f, 0.001f, "修复30HP→80", "BattleSimulator");

            sim.RepairBridge(50f); // 超出上限
            TestRunner.AssertEqual(sim.BridgeHP, 100f, "修复不超过上限", "BattleSimulator");
        }

        static void TestEdgeCases()
        {
            var sim = new BattleSimulatorLogic();

            // 攻击不存在的单位
            int dmg1 = sim.ApplyDamageToUnit("nonexistent", 10f);
            TestRunner.AssertEqual(dmg1, 0, "攻击不存在的我方单位→0", "BattleSimulator");

            int dmg2 = sim.ApplyDamageToEnemy("nonexistent", 10f);
            TestRunner.AssertEqual(dmg2, 0, "攻击不存在的敌方单位→0", "BattleSimulator");

            // 攻击已歼灭单位
            sim.RegisterFriendlyUnit("dead", 5, 75f, 80f, Vector3.zero);
            sim.ApplyDamageToUnit("dead", 10f); // 歼灭
            int dmg3 = sim.ApplyDamageToUnit("dead", 5f); // 再攻击
            TestRunner.AssertEqual(dmg3, 0, "攻击已歼灭单位→0", "BattleSimulator");

            // 兵力系数 = 0
            var emptyUnit = new CombatUnit { TroopCount = 0, MaxTroopCount = 100 };
            TestRunner.AssertNear(emptyUnit.TroopRatio, 0f, 0.001f, "0兵力→兵力系数0", "BattleSimulator");

            // MaxTroopCount = 0 (除零保护)
            var brokenUnit = new CombatUnit { TroopCount = 10, MaxTroopCount = 0 };
            TestRunner.AssertEqual(brokenUnit.TroopRatio, 0f, "MaxTroopCount=0→系数0", "BattleSimulator");

            // 修改不存在的单位士气
            sim.ModifyMorale("ghost", 50f); // 不应崩溃
            TestRunner.Assert(true, "修改不存在单位士气不崩溃", "BattleSimulator");
        }
    }

    public static class GameDirectorTests
    {
        public static void RunAll()
        {
            TestPhaseProgression();
            TestPhaseTimeline();
            TestObjectiveCapture();
            TestVictoryConditions();
            TestPauseResume();
            TestTimeFormat();
            TestGameLoop();
        }

        static void TestPhaseProgression()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            TestRunner.AssertEqual(director.CurrentPhase, CampaignPhase.Briefing, "初始阶段=简报", "GameDirector");

            // 推进到 06:15 (375分钟)
            director.Update(15f * 60f); // 15真实秒 * timeScale=1
            TestRunner.AssertEqual(director.CurrentPhase, CampaignPhase.Embarkation, "15分钟后=登艇", "GameDirector");
        }

        static void TestPhaseTimeline()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            // 时间→阶段映射表 (游戏分钟 → 阶段)
            var timeline = new[]
            {
                (360f, CampaignPhase.Briefing),
                (375f, CampaignPhase.Embarkation),
                (390f, CampaignPhase.FirstWaveLanding),
                (395f, CampaignPhase.FirstReports),
                (405f, CampaignPhase.SecondWaveLanding),
                (420f, CampaignPhase.ThirdWaveLanding),
                (450f, CampaignPhase.CounterAttack),
                (480f, CampaignPhase.CriticalDecision),
                (540f, CampaignPhase.Resolution),
            };

            foreach (var (time, expectedPhase) in timeline)
            {
                director.Initialize();
                // 直接推进到目标时间
                float deltaTime = (time - director.CampaignStartGameTime) * 60f; // 转换为真实秒
                director.Update(deltaTime);
                TestRunner.AssertEqual(director.CurrentPhase, expectedPhase,
                    $"时间{time/60f:F0}:{time%60f:F0}→{expectedPhase}", "GameDirector");
            }
        }

        static void TestObjectiveCapture()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            director.ReportObjectiveCaptured(0);
            director.ReportObjectiveCaptured(2);

            // 推进到游戏结束
            director.Update((540f - 360f) * 60f);

            // 部分占领 → 部分胜利 (但由于 CheckOutcome 只在 >=540 才判定)
            TestRunner.AssertEqual(director.Outcome, GameOutcome.PartialVictory, "占领2目标→部分胜利", "GameDirector");
        }

        static void TestVictoryConditions()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            // 全占 + 低伤亡 → 完美胜利
            director.ReportObjectiveCaptured(0);
            director.ReportObjectiveCaptured(1);
            director.ReportObjectiveCaptured(2);
            director.SetFinalOutcome(0.2f);
            TestRunner.AssertEqual(director.Outcome, GameOutcome.PerfectVictory,
                "全占+20%伤亡→完美胜利", "GameDirector");

            // 全占 + 高伤亡 → 惨胜
            director.Initialize();
            director.ReportObjectiveCaptured(0);
            director.ReportObjectiveCaptured(1);
            director.ReportObjectiveCaptured(2);
            director.SetFinalOutcome(0.6f);
            TestRunner.AssertEqual(director.Outcome, GameOutcome.PyrrhicVictory,
                "全占+60%伤亡→惨胜", "GameDirector");

            // 零占领 → 失败
            director.Initialize();
            director.SetFinalOutcome(0.1f);
            TestRunner.AssertEqual(director.Outcome, GameOutcome.Defeat,
                "零占领→失败", "GameDirector");

            // 全军覆没
            director.Initialize();
            director.SetTotalDefeat();
            TestRunner.AssertEqual(director.Outcome, GameOutcome.TotalDefeat,
                "SetTotalDefeat→全军覆没", "GameDirector");
        }

        static void TestPauseResume()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            director.Pause();
            director.Update(60f); // 暂停时不推进时间
            TestRunner.AssertNear(director.CurrentGameTime, 360f, 0.001f, "暂停时时间不推进", "GameDirector");

            director.Resume();
            director.Update(60f); // 恢复后推进
            TestRunner.Assert(director.CurrentGameTime > 360f, "恢复后时间推进", "GameDirector",
                $"当前时间={director.CurrentGameTime}");
        }

        static void TestTimeFormat()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            TestRunner.AssertEqual(director.GetFormattedTime(), "06:00", "初始时间格式化=06:00", "GameDirector");

            director.Update(15f * 60f); // 15分钟
            TestRunner.AssertEqual(director.GetFormattedTime(), "06:15", "15分钟后=06:15", "GameDirector");
        }

        static void TestGameLoop()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            // 模拟完整游戏循环: 10分钟 (游戏内180分钟)
            // 每帧0.016秒 (60fps)，timeScale=1
            float elapsed = 0f;
            float totalRealSeconds = (540f - 360f) * 60f; // 180游戏分钟 = 10800真实秒

            // 加速模拟
            director.TimeScale = 60f; // 1秒真实 = 1分钟游戏
            int frames = 0;
            while (director.Outcome == GameOutcome.InProgress && frames < 10000)
            {
                director.Update(0.016f);
                elapsed += 0.016f;
                frames++;
            }

            TestRunner.Assert(director.CurrentPhase == CampaignPhase.Resolution,
                "完整游戏循环最终到达Resolution阶段", "GameDirector",
                $"最终阶段={director.CurrentPhase}, 历时{elapsed:F1}s, {frames}帧");
            TestRunner.Assert(frames < 10000, "游戏在合理帧数内结束", "GameDirector",
                $"帧数={frames}");
        }
    }

    public static class CommandSystemTests
    {
        public static void RunAll()
        {
            TestDelayCalculation();
            TestLossChance();
            TestMisinterpretChance();
            TestDeliverySimulation();
            TestMisinterpretation();
            TestDifficultyScaling();
        }

        static void TestDelayCalculation()
        {
            var cmd = new CommandSystemLogic();

            // Easy难度的移动指令
            float delay = cmd.CalculateDelay(CommandType.Move, Difficulty.Easy);
            TestRunner.Assert(delay > 0f, "延迟>0", "CommandSystem",
                $"延迟={delay:F1}s");

            // Hard难度应该比Easy长
            float easyDelay = cmd.CalculateDelay(CommandType.Move, Difficulty.Easy);
            float hardDelay = cmd.CalculateDelay(CommandType.Move, Difficulty.Hard);
            TestRunner.Assert(hardDelay > easyDelay, "Hard延迟>Easy延迟", "CommandSystem",
                $"Easy={easyDelay:F1}s, Hard={hardDelay:F1}s");

            // StatusQuery比Move短
            float queryDelay = cmd.CalculateDelay(CommandType.StatusQuery, Difficulty.Normal);
            float moveDelay = cmd.CalculateDelay(CommandType.Move, Difficulty.Normal);
            TestRunner.Assert(queryDelay < moveDelay, "StatusQuery延迟<Move延迟", "CommandSystem",
                $"Query={queryDelay:F1}s, Move={moveDelay:F1}s");
        }

        static void TestLossChance()
        {
            var cmd = new CommandSystemLogic();

            TestRunner.AssertNear(cmd.GetLossChance(Difficulty.Easy), 0.05f, 0.001f,
                "Easy丢失率=5%", "CommandSystem");
            TestRunner.AssertNear(cmd.GetLossChance(Difficulty.Normal), 0.15f, 0.001f,
                "Normal丢失率=15%", "CommandSystem");
            TestRunner.AssertNear(cmd.GetLossChance(Difficulty.Hard), 0.30f, 0.001f,
                "Hard丢失率=30%", "CommandSystem");
        }

        static void TestMisinterpretChance()
        {
            var cmd = new CommandSystemLogic();

            TestRunner.AssertNear(cmd.GetMisinterpretChance(90f), 0.05f, 0.001f,
                "士气90→误解率5%", "CommandSystem");
            TestRunner.AssertNear(cmd.GetMisinterpretChance(60f), 0.12f, 0.001f,
                "士气60→误解率12%", "CommandSystem");
            TestRunner.AssertNear(cmd.GetMisinterpretChance(40f), 0.25f, 0.001f,
                "士气40→误解率25%", "CommandSystem");
            TestRunner.AssertNear(cmd.GetMisinterpretChance(10f), 0.45f, 0.001f,
                "士气10→误解率45%", "CommandSystem");
        }

        static void TestDeliverySimulation()
        {
            var cmd = new CommandSystemLogic();

            // 非常低的roll → 应该丢失
            var status1 = cmd.SimulateDelivery(CommandType.Move, 75f, Difficulty.Easy, 0.01f);
            TestRunner.AssertEqual(status1, CommandStatus.Lost, "极低roll→丢失", "CommandSystem");

            // 中等roll → 正常送达
            var status2 = cmd.SimulateDelivery(CommandType.Move, 75f, Difficulty.Easy, 0.5f);
            TestRunner.AssertEqual(status2, CommandStatus.Delivered, "中等roll→送达", "CommandSystem");

            // 边界: roll刚好在丢失边界
            cmd.Reset();
            var status3 = cmd.SimulateDelivery(CommandType.Move, 75f, Difficulty.Easy, 0.049f);
            TestRunner.AssertEqual(status3, CommandStatus.Lost, "边界roll(0.049<0.05)→丢失", "CommandSystem");

            cmd.Reset();
            var status4 = cmd.SimulateDelivery(CommandType.Move, 75f, Difficulty.Easy, 0.051f);
            TestRunner.AssertEqual(status4, CommandStatus.Delivered, "边界roll(0.051>0.05)→送达", "CommandSystem");
        }

        static void TestMisinterpretation()
        {
            var cmd = new CommandSystemLogic();

            // Move指令误解
            string mis1 = cmd.GenerateMisinterpretation(CommandType.Move, "向北移动至目标Alpha");
            TestRunner.Assert(mis1 != "向北移动至目标Alpha", "Move指令被误解", "CommandSystem",
                $"原始='向北移动至目标Alpha', 误解='{mis1}'");

            // Defend指令不被误解
            string mis2 = cmd.GenerateMisinterpretation(CommandType.Defend, "就地防御");
            TestRunner.AssertEqual(mis2, "就地防御", "Defend指令不被误解", "CommandSystem");

            // Retreat指令误解
            string mis3 = cmd.GenerateMisinterpretation(CommandType.Retreat, "撤退至海堤");
            TestRunner.Assert(mis3.Contains("原地待命"), "Retreat被误解为待命", "CommandSystem",
                $"误解='{mis3}'");
        }

        static void TestDifficultyScaling()
        {
            var cmd = new CommandSystemLogic();

            // 丢失误率随难度递增
            float easyLoss = cmd.GetLossChance(Difficulty.Easy);
            float normalLoss = cmd.GetLossChance(Difficulty.Normal);
            float hardLoss = cmd.GetLossChance(Difficulty.Hard);
            TestRunner.Assert(easyLoss < normalLoss && normalLoss < hardLoss,
                "丢失率递增: Easy<Normal<Hard", "CommandSystem",
                $"Easy={easyLoss}, Normal={normalLoss}, Hard={hardLoss}");

            // 延迟随难度递增
            float easyDelay = cmd.CalculateDelay(CommandType.Move, Difficulty.Easy);
            float normalDelay = cmd.CalculateDelay(CommandType.Move, Difficulty.Normal);
            float hardDelay = cmd.CalculateDelay(CommandType.Move, Difficulty.Hard);
            TestRunner.Assert(easyDelay < normalDelay && normalDelay < hardDelay,
                "延迟递增: Easy<Normal<Hard", "CommandSystem",
                $"Easy={easyDelay:F1}, Normal={normalDelay:F1}, Hard={hardDelay:F1}");
        }
    }

    public static class IntegrationTests
    {
        public static void RunAll()
        {
            TestFullGameLoop();
            TestDamageToOutcome();
            TestMoraleAffectsCommand();
            TestBridgeHPZeroBehavior();
            TestAllEliminatedBehavior();
        }

        /// <summary>完整10分钟游戏循环模拟</summary>
        static void TestFullGameLoop()
        {
            var sim = new BattleSimulatorLogic();
            var director = new GameDirectorLogic();
            var cmd = new CommandSystemLogic();

            director.Initialize();
            director.TimeScale = 60f; // 加速

            // 注册部队
            sim.RegisterFriendlyUnit("company_1", 55, 75f, 80f, new Vector3(100, 0, 50));
            sim.RegisterFriendlyUnit("company_2", 50, 65f, 65f, new Vector3(200, 0, 100));
            sim.RegisterFriendlyUnit("tank_platoon", 45, 80f, 90f, new Vector3(150, 0, 80));

            sim.RegisterEnemyUnit("german_1", 80, 70f, new Vector3(120, 0, 60));
            sim.RegisterEnemyUnit("german_2", 60, 60f, new Vector3(180, 0, 90));

            int frames = 0;
            int combatEvents = 0;

            while (director.Outcome == GameOutcome.InProgress && frames < 10000)
            {
                director.Update(0.016f);

                // 模拟战斗 (每5秒一次)
                if (frames % 300 == 0 && frames > 0)
                {
                    // 敌军攻击我方
                    var attacker = sim.GetEnemyUnit("german_1");
                    var defender = sim.GetFriendlyUnit("company_1");
                    if (attacker != null && defender != null && !attacker.IsEliminated && !defender.IsEliminated)
                    {
                        float dmg = sim.CalculateDamage(attacker, defender);
                        sim.ApplyDamageToUnit("company_1", dmg);
                        combatEvents++;
                    }

                    // 我方攻击敌军
                    if (defender != null && attacker != null && !defender.IsEliminated && !attacker.IsEliminated)
                    {
                        float dmg = sim.CalculateDamage(defender, attacker);
                        sim.ApplyDamageToEnemy("german_1", dmg);
                    }

                    // 敌军攻击桥头堡
                    sim.EnemyAttackBridge("german_1", 5f);
                    sim.EnemyAttackBridge("german_2", 5f);
                }

                // 模拟占领目标
                if (frames == 3000) director.ReportObjectiveCaptured(0);
                if (frames == 5000) director.ReportObjectiveCaptured(1);

                frames++;
            }

            // 设置最终结局
            director.SetFinalOutcome(sim.GetFriendlyCasualtyRate());

            TestRunner.Assert(frames < 10000, "游戏循环在合理帧数内结束", "Integration",
                $"帧数={frames}");
            TestRunner.Assert(combatEvents > 0, "有战斗事件发生", "Integration",
                $"战斗事件={combatEvents}");
            TestRunner.Assert(director.CurrentPhase == CampaignPhase.Resolution,
                "最终到达Resolution阶段", "Integration",
                $"阶段={director.CurrentPhase}");
            TestRunner.Assert(sim.TotalFriendlyCasualties + sim.TotalEnemyCasualties > 0,
                "有伤亡统计", "Integration",
                $"我方伤亡={sim.TotalFriendlyCasualties}, 敌方伤亡={sim.TotalEnemyCasualties}");
            TestRunner.Assert(director.Outcome != GameOutcome.InProgress,
                "游戏有明确结局", "Integration",
                $"结局={director.Outcome}");
        }

        /// <summary>指令→数值→结局全链路</summary>
        static void TestDamageToOutcome()
        {
            var sim = new BattleSimulatorLogic();
            sim.RegisterFriendlyUnit("unit", 100, 75f, 80f, Vector3.zero);
            sim.RegisterEnemyUnit("enemy", 100, 60f, Vector3.zero);

            // 模拟连续攻击直到一方全灭
            int rounds = 0;
            while (rounds < 1000)
            {
                var fUnit = sim.GetFriendlyUnit("unit");
                var eUnit = sim.GetEnemyUnit("enemy");

                if (fUnit.IsEliminated || eUnit.IsEliminated) break;

                float fDmg = sim.CalculateDamage(fUnit, eUnit);
                float eDmg = sim.CalculateDamage(eUnit, fUnit);

                sim.ApplyDamageToEnemy("enemy", fDmg);
                sim.ApplyDamageToUnit("unit", eDmg);
                rounds++;
            }

            TestRunner.Assert(rounds < 1000, "战斗在合理轮次内结束", "Integration",
                $"轮次={rounds}");
            TestRunner.Assert(sim.GetFriendlyUnit("unit").IsEliminated || sim.GetEnemyUnit("enemy").IsEliminated,
                "至少一方被歼灭", "Integration",
                $"我方歼灭={sim.GetFriendlyUnit("unit").IsEliminated}, 敌方歼灭={sim.GetEnemyUnit("enemy").IsEliminated}");
        }

        /// <summary>士气影响指挥系统</summary>
        static void TestMoraleAffectsCommand()
        {
            var cmd = new CommandSystemLogic();

            // 低士气 → 高误解率
            float lowMoraleMis = cmd.GetMisinterpretChance(10f);
            float highMoraleMis = cmd.GetMisinterpretChance(90f);
            TestRunner.Assert(lowMoraleMis > highMoraleMis,
                "低士气误解率 > 高士气误解率", "Integration",
                $"低={lowMoraleMis:F2}, 高={highMoraleMis:F2}");

            // 统计: 低士气下100次模拟应该有更多误解
            int lowMoraleMisCount = 0;
            int highMoraleMisCount = 0;
            var rng = new Random(42);

            for (int i = 0; i < 1000; i++)
            {
                float roll = (float)rng.NextDouble();
                var s1 = cmd.SimulateDelivery(CommandType.Move, 10f, Difficulty.Normal, roll);
                if (cmd.EventLog.Count > 0 && cmd.EventLog[^1].Contains("Misinterpreted"))
                    lowMoraleMisCount++;

                cmd.Reset();
                roll = (float)rng.NextDouble();
                var s2 = cmd.SimulateDelivery(CommandType.Move, 90f, Difficulty.Normal, roll);
                if (cmd.EventLog.Count > 0 && cmd.EventLog[^1].Contains("Misinterpreted"))
                    highMoraleMisCount++;

                cmd.Reset();
            }

            TestRunner.Assert(lowMoraleMisCount > highMoraleMisCount,
                "1000次模拟: 低士气误解次数 > 高士气", "Integration",
                $"低={lowMoraleMisCount}, 高={highMoraleMisCount}");
        }

        /// <summary>桥HP为0时的行为</summary>
        static void TestBridgeHPZeroBehavior()
        {
            var sim = new BattleSimulatorLogic();
            sim.ApplyBridgeDamage(200f, 1f); // 摧毁

            // 多次攻击不应变为负数
            sim.ApplyBridgeDamage(10f, 1f);
            sim.ApplyBridgeDamage(10f, 1f);
            TestRunner.AssertEqual(sim.BridgeHP, 0f, "桥HP不会变为负数", "Integration");

            // 桥摧毁事件只触发一次
            int destroyCount = sim.EventLog.Count(e => e == "BridgeDestroyed");
            TestRunner.AssertEqual(destroyCount, 1, "BridgeDestroyed事件只触发一次", "Integration");
        }

        /// <summary>所有部队被歼灭时的行为</summary>
        static void TestAllEliminatedBehavior()
        {
            var sim = new BattleSimulatorLogic();
            var director = new GameDirectorLogic();
            director.Initialize();

            sim.RegisterFriendlyUnit("unit_a", 10, 75f, 80f, Vector3.zero);
            sim.RegisterFriendlyUnit("unit_b", 10, 75f, 80f, Vector3.zero);

            // 歼灭所有部队
            sim.ApplyDamageToUnit("unit_a", 20f);
            sim.ApplyDamageToUnit("unit_b", 20f);

            TestRunner.Assert(sim.AreAllFriendlyUnitsEliminated(), "全灭检测正确", "Integration");
            TestRunner.AssertNear(sim.GetFriendlyCasualtyRate(), 1.0f, 0.001f, "全灭时伤亡率=100%", "Integration");

            // 全灭 + 0目标 → 失败
            director.SetFinalOutcome(1.0f);
            TestRunner.AssertEqual(director.Outcome, GameOutcome.Defeat,
                "全灭+0目标→失败", "Integration");

            // SetTotalDefeat 覆盖
            director.SetTotalDefeat();
            TestRunner.AssertEqual(director.Outcome, GameOutcome.TotalDefeat,
                "SetTotalDefeat→全军覆没", "Integration");
        }
    }

    #endregion

    #region Bug 检测器

    public static class BugDetector
    {
        public static List<(string severity, string module, string description, string fix)> Bugs
            = new List<(string, string, string, string)>();

        public static void RunAll()
        {
            CheckBridgeDamageOverflow();
            CheckMoraleTickMissingNoComm();
            CheckInitialTroopsAccumulation();
            CheckEnemyUnitAmmo();
            CheckSetTotalDefeatOverride();
            CheckCommandHistoryPersistence();
            CheckUnitEliminatedEvent();
            CheckMoraleDamageThreshold();
        }

        /// <summary>Bug: BridgeDamagePerSecond 在 EnemyAttackBridge 中被乘以倍率
        /// 但 ApplyBridgeDamage 的参数名是 damagePerSecond，可能误导</summary>
        static void CheckBridgeDamageOverflow()
        {
            var sim = new BattleSimulatorLogic();
            // 多个敌军同时攻击桥头堡 → HP可以快速下降
            sim.RegisterEnemyUnit("e1", 100, 80f, Vector3.zero);
            sim.RegisterEnemyUnit("e2", 100, 80f, Vector3.zero);
            sim.RegisterEnemyUnit("e3", 100, 80f, Vector3.zero);

            // 3个满编敌军 × 5秒 = 大量伤害
            sim.EnemyAttackBridge("e1", 5f);
            sim.EnemyAttackBridge("e2", 5f);
            sim.EnemyAttackBridge("e3", 5f);

            if (sim.BridgeHP < 50f)
            {
                Bugs.Add(("P1", "BattleSimulator",
                    $"3个敌军各攻击5秒后桥HP={sim.BridgeHP:F1}/100 (降了{100f-sim.BridgeHP:F1f}点)。GDD要求-2/s基础，但EnemyAttackBridge叠加兵力倍率(最高1.5x)，3敌军=9 DPS，100HP仅撑11秒。10分钟游戏不够合理。",
                    "降低 bridgeDamagePerSecond 或限制同时攻击桥头堡的敌军数量上限"));
            }
        }

        /// <summary>Bug: ApplyMoraleTick 只检查弹药不足，不检查无通讯</summary>
        static void CheckMoraleTickMissingNoComm()
        {
            // 代码审查: ApplyMoraleTick() 方法只处理了弹药不足的-5惩罚
            // 但 GDD 要求 "长时间无通讯 → -3/tick"
            // 当前实现中 ApplyNoCommPenalty 需要外部手动调用
            Bugs.Add(("P2", "BattleSimulator",
                "ApplyMoraleTick() 缺少无通讯士气衰减逻辑。GDD 5.5 要求'长时间无通讯→-3/tick'，但当前实现只处理了弹药不足(-5/tick)。ApplyNoCommPenalty需外部调用，缺乏自动检测。",
                "在 ApplyMoraleTick 中增加通讯状态追踪，自动应用无通讯惩罚"));
        }

        /// <summary>Bug: RegisterFriendlyUnit 重复注册会累加 InitialTroops</summary>
        static void CheckInitialTroopsAccumulation()
        {
            var sim = new BattleSimulatorLogic();
            sim.RegisterFriendlyUnit("unit", 50, 75f, 80f, Vector3.zero);
            int first = sim.InitialFriendlyTroops;

            // 重新注册同ID
            sim.RegisterFriendlyUnit("unit", 50, 75f, 80f, Vector3.zero);
            int second = sim.InitialFriendlyTroops;

            if (second != first)
            {
                Bugs.Add(("P2", "BattleSimulator",
                    $"重复注册同ID的部队会累加InitialFriendlyTroops: {first}→{second}。导致伤亡率计算错误。",
                    "注册前检查是否已存在，若存在则跳过或替换而非累加"));
            }
        }

        /// <summary>Bug: RegisterEnemyUnit 不设置 AmmoLevel 以外的弹药消耗逻辑</summary>
        static void CheckEnemyUnitAmmo()
        {
            var sim = new BattleSimulatorLogic();
            sim.RegisterEnemyUnit("enemy", 50, 70f, Vector3.zero);
            var enemy = sim.GetEnemyUnit("enemy");

            // 敌军弹药固定100且无消耗逻辑
            TestRunner.AssertEqual(enemy.AmmoLevel, 100f, "[设计] 敌军弹药固定100", "BugCheck");

            Bugs.Add(("P3", "BattleSimulator",
                "敌军弹药始终为100且无消耗逻辑。当前设计中敌军是'简化AI'，但可能导致平衡问题：敌军永远不会因弹药不足而削弱。",
                "（可选）为敌军增加弹药消耗系统，或在GDD中明确此为设计决策"));
        }

        /// <summary>Bug: SetTotalDefeat 不能被 SetFinalOutcome 覆盖</summary>
        static void CheckSetTotalDefeatOverride()
        {
            var director = new GameDirectorLogic();
            director.Initialize();

            director.SetTotalDefeat();
            director.SetFinalOutcome(0.1f); // 应该不能覆盖 TotalDefeat

            // 但实际上 SetFinalOutcome 会覆盖！
            if (director.Outcome != GameOutcome.TotalDefeat)
            {
                Bugs.Add(("P1", "GameDirector",
                    $"SetTotalDefeat()后调用SetFinalOutcome()会覆盖结局: {GameOutcome.TotalDefeat}→{director.Outcome}。全军覆没应是最高优先级结局。",
                    "SetFinalOutcome 中增加 if(Outcome == TotalDefeat) return; 保护"));
            }
        }

        /// <summary>Bug: GameDirector.CheckOutcome 只在 >=540 时才判定，不检查全灭</summary>
        static void CheckNoTotalDefeatCheck()
        {
            // CheckOutcome 不检查 AreAllFriendlyUnitsEliminated
            // 游戏中即使全灭，也要等到 09:00 才会结束
            Bugs.Add(("P1", "GameDirector",
                "GameDirector.CheckOutcome() 不检测部队全灭。即使所有部队被歼灭，游戏仍等到09:00才结束。应在 Update() 中增加全灭检测。",
                "在 GameDirector.Update() 中调用 BattleSimulator.AreAllFriendlyUnitsEliminated() 并触发 SetTotalDefeat()"));
        }

        /// <summary>Bug: 指令历史在 MarkCommandCompleted 时不移出 pendingCommands</summary>
        static void CheckCommandHistoryPersistence()
        {
            // Code review: MarkCommandCompleted 从 pendingCommands 移除
            // 但 commandHistory 保留所有记录
            // 这是合理的设计，但没有最大历史限制
            Bugs.Add(("P3", "CommandSystem",
                "commandHistory 无限增长，长时间游戏可能占用大量内存。",
                "添加最大历史记录限制（如100条），超出时移除最旧记录"));
        }

        /// <summary>Bug: OnUnitEliminated 事件在 ApplyDamageToUnit 中触发但外部无法订阅</summary>
        static void CheckUnitEliminatedEvent()
        {
            // 纯逻辑版本没有事件，但 MonoBehaviour 版本有
            // 检查: 事件是否正确在歼灭时触发
            var sim = new BattleSimulatorLogic();
            sim.RegisterFriendlyUnit("unit", 10, 75f, 80f, Vector3.zero);
            sim.ApplyDamageToUnit("unit", 20f);

            bool hasEvent = sim.EventLog.Contains("UnitEliminated:unit");
            TestRunner.Assert(hasEvent, "歼灭事件正确记录", "BugCheck");
        }

        /// <summary>Bug: 重大伤亡阈值 >10% 是否合理</summary>
        static void CheckMoraleDamageThreshold()
        {
            // GDD: "遭受重大伤亡 → -15"
            // 代码: actualDamage > unit.MaxTroopCount * 0.1f
            // 100人损失11人即触发-15士气
            // 战斗中可能频繁触发
            Bugs.Add(("P2", "BattleSimulator",
                "重大伤亡阈值为10%，战斗中频繁触发-15士气惩罚。100人的部队每次受11点伤害就会-15士气，可能几个回合就士气崩溃。",
                "提高阈值至20-25%，或增加冷却时间（如60秒内不重复触发）"));
        }
    }

    #endregion

    #region 主入口

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("🎯 WW2 Commander 集成测试运行器");
            Console.WriteLine("测试范围: BattleSimulator, GameDirector, CommandSystem, 全链路集成");
            Console.WriteLine(new string('=', 70));

            // 设置随机种子确保可复现
            Mathf.SeedRandom(42);

            // 1. 单元测试
            Console.WriteLine("\n📋 [Phase 1] BattleSimulator 单元测试...");
            BattleSimulatorTests.RunAll();

            Console.WriteLine("\n📋 [Phase 2] GameDirector 单元测试...");
            GameDirectorTests.RunAll();

            Console.WriteLine("\n📋 [Phase 3] CommandSystem 单元测试...");
            CommandSystemTests.RunAll();

            // 2. 集成测试
            Console.WriteLine("\n🔗 [Phase 4] 全链路集成测试...");
            IntegrationTests.RunAll();

            // 3. Bug检测
            Console.WriteLine("\n🔍 [Phase 5] Bug 检测...");
            BugDetector.RunAll();

            // 4. 打印测试报告
            TestRunner.PrintSummary();

            // 5. 打印 Bug 报告
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("🐛 Bug 报告");
            Console.WriteLine(new string('=', 70));

            if (BugDetector.Bugs.Count == 0)
            {
                Console.WriteLine("  未发现 Bug ✅");
            }
            else
            {
                foreach (var (severity, module, desc, fix) in BugDetector.Bugs)
                {
                    Console.WriteLine($"\n  [{severity}] {module}");
                    Console.WriteLine($"  问题: {desc}");
                    Console.WriteLine($"  修复: {fix}");
                }
            }

            // 6. 缺失模块报告
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("⚠️ 缺失模块报告");
            Console.WriteLine(new string('=', 70));
            Console.WriteLine("  SWO-145 EnemyWaveManager.cs — ❌ 未找到");
            Console.WriteLine("    → 影响: 无法测试波次触发逻辑、敌军生成和撤退");
            Console.WriteLine("    → 建议: 优先实现，它是 BattleSimulator 敌军数据的主要来源");
            Console.WriteLine();
            Console.WriteLine("  SWO-146 AIDirector.cs — ❌ 未找到");
            Console.WriteLine("    → 影响: 无法测试难度自适应、事件触发和降级机制");
            Console.WriteLine("    → 建议: 至少实现降级机制(fallback)，避免LLM API超时时游戏卡死");
            Console.WriteLine();
            Console.WriteLine("  SWO-147 SandTableRenderer.cs — ❌ 未找到");
            Console.WriteLine("    → 影响: 无法测试2D沙盘可视化");
            Console.WriteLine("    → 建议: 沙盘是核心UI，应尽快实现");
            Console.WriteLine();
            Console.WriteLine("  SWO-148 CommandSystem→BattleSimulator 对接 — ⚠️ 部分完成");
            Console.WriteLine("    → 问题: CommandSystem.SendCommand 不调用 BattleSimulator");
            Console.WriteLine("    → 防御指令不会实际改变 IsDefending 状态");
            Console.WriteLine("    → 炮击指令不会实际对敌军造成伤害");
            Console.WriteLine("    → 建议: 在 CommandSystem 中增加 OnCommandExecuted 回调，对接 BattleSimulator");

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("📊 测试完成");
            Console.WriteLine(new string('=', 70));
        }
    }

    #endregion
}
