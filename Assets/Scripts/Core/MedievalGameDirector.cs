// MedievalGameDirector.cs — 铁蹄核心游戏逻辑
// 管理：回合流程、骑士移动、敌人刷新、自动战斗、村庄补给
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SWO1.Medieval
{
    #region 数据模型

    public enum KnightName { DonQuixote, Lancelot, ElCid }
    public enum EnemyType { Deserter, Bandit, EnemyKnight }
    public enum GameState { Playing, Victory, Defeat }

    [Serializable]
    public class KnightData
    {
        public KnightName Name;
        public string DisplayName;
        public string Title;
        public int Troops;         // 人数
        public int MaxTroops;      // 人数上限
        public int Morale;         // 战意
        public int MaxMorale;      // 战意上限
        public Vector2Int Position;
        public bool IsAlive = true;
        public string Personality; // LLM system prompt
        public Color Color;

        public float MoveSpeed => 2f; // 所有骑士都移动2格
    }

    [Serializable]
    public class EnemyData
    {
        public EnemyType Type;
        public string DisplayName;
        public int Troops;
        public int Morale;
        public Vector2Int Position;
        public bool IsDiscovered;  // 是否已发现（3格内刷新）
        public bool IsDefeated;
    }

    [Serializable]
    public class VillageData
    {
        public Vector2Int Position;
        public int SupplyAmount;   // 1~8 随机
        public bool IsUsed;
        public string Name;
    }

    #endregion

    public class MedievalGameDirector : MonoBehaviour
    {
        public static MedievalGameDirector Instance { get; private set; }

        [Header("状态")]
        public int CurrentTurn = 1;
        public int MaxTurns = 20;
        public GameState State = GameState.Playing;

        [Header("数据")]
        public List<KnightData> Knights = new List<KnightData>();
        public List<EnemyData> Enemies = new List<EnemyData>();
        public List<VillageData> Villages = new List<VillageData>();
        public MedMap CurrentMap;

        // 事件
        public event Action<string> OnMessage;           // 通用消息
        public event Action<string, string> OnKnightReport; // 骑士汇报 (名字, 内容)
        public event Action<string> OnTerrainTrigger;    // 地形文字触发
        public event Action<string> OnImageTrigger;      // 图片触发
        public event Action<EnemyData> OnEnemyDiscovered; // 敌人发现
        public event Action<string> OnCombatResult;      // 战斗结果
        public event Action<int> OnTurnChanged;          // 回合变更
        public event Action<GameState> OnGameOver;       // 游戏结束

        void Awake() { Instance = this; }

        void Start()
        {
            InitializeKnights();
            InitializeEnemies();
            InitializeVillages();
        }

        #region 初始化

        void InitializeKnights()
        {
            Knights.Clear();

            Knights.Add(new KnightData
            {
                Name = KnightName.DonQuixote,
                DisplayName = "唐吉诃德",
                Title = "拉曼查的骑士",
                Troops = 100, MaxTroops = 100,
                Morale = 90, MaxMorale = 90,
                Position = new Vector2Int(15, 5),
                Personality = "你是唐吉诃德，一个疯狂的理想主义者。你坚信自己是世界上最伟大的骑士。说话充满戏剧性，经常引用骑士小说。你把风车当巨人，把羊群当军队。即使面对强敌也绝不退缩。用中文回复，语气要像一个中世纪骑士在飞鸽传书。",
                Color = new Color(0.8f, 0.2f, 0.2f)
            });

            Knights.Add(new KnightData
            {
                Name = KnightName.Lancelot,
                DisplayName = "兰斯洛特",
                Title = "湖上骑士",
                Troops = 120, MaxTroops = 120,
                Morale = 60, MaxMorale = 60,
                Position = new Vector2Int(16, 4),
                Personality = "你是兰斯洛特，亚瑟王最英勇的圆桌骑士。你冷静、理智、擅长战术分析。说话简洁精准，从不废话。你尊重敌人但也绝不手软。用中文回复，语气冷静专业。",
                Color = new Color(0.2f, 0.4f, 0.8f)
            });

            Knights.Add(new KnightData
            {
                Name = KnightName.ElCid,
                DisplayName = "熙德",
                Title = "卡斯蒂利亚的勇士",
                Troops = 110, MaxTroops = 110,
                Morale = 75, MaxMorale = 75,
                Position = new Vector2Int(17, 5),
                Personality = "你是熙德·罗德里戈·迪亚兹·德·比瓦尔，西班牙最伟大的骑士。你是一个务实的老兵，见过太多战争。说话沉稳老练，偶尔带点黑色幽默。你知道什么时候该进，什么时候该退。用中文回复，语气沉稳务实。",
                Color = new Color(0.9f, 0.6f, 0.1f)
            });
        }

        void InitializeEnemies()
        {
            Enemies.Clear();

            // 敌人位置根据地图动态确定
            // 如果有地图数据，使用地图的敌人刷新区
            Vector2Int e1Pos, e2Pos, e3Pos;

            if (CurrentMap != null && CurrentMap.EnemyZones.Count >= 3)
            {
                e1Pos = CurrentMap.EnemyZones[0];
                e2Pos = CurrentMap.EnemyZones[1];
                e3Pos = CurrentMap.EnemyZones[2];
            }
            else
            {
                // 默认位置
                e1Pos = new Vector2Int(12, 8);
                e2Pos = new Vector2Int(20, 18);
                e3Pos = new Vector2Int(16, 25);
            }

            Enemies.Add(new EnemyData
            {
                Type = EnemyType.Deserter,
                DisplayName = "溃散的逃兵",
                Troops = 90, Morale = 60,
                Position = e1Pos,
                IsDiscovered = false
            });

            Enemies.Add(new EnemyData
            {
                Type = EnemyType.Bandit,
                DisplayName = "马匪",
                Troops = 130, Morale = 80,
                Position = e2Pos,
                IsDiscovered = false
            });

            Enemies.Add(new EnemyData
            {
                Type = EnemyType.EnemyKnight,
                DisplayName = "斯瓦迪亚骑士团",
                Troops = 190, Morale = 110,
                Position = e3Pos,
                IsDiscovered = false
            });
        }

        void InitializeVillages()
        {
            Villages.Clear();
            var rng = new System.Random(123);

            if (CurrentMap != null && CurrentMap.Villages.Count > 0)
            {
                int idx = 0;
                foreach (var vp in CurrentMap.Villages)
                {
                    if (idx >= 3) break; // 最多3个村庄
                    Villages.Add(new VillageData
                    {
                        Position = vp,
                        SupplyAmount = rng.Next(1, 9),
                        IsUsed = false,
                        Name = GetVillageName(idx)
                    });
                    idx++;
                }
            }
            else
            {
                // 默认村庄
                string[] names = { "蒙德镇", "格兰村", "橡木堡" };
                Vector2Int[] positions = { new Vector2Int(16, 10), new Vector2Int(10, 15), new Vector2Int(22, 20) };
                for (int i = 0; i < 3; i++)
                {
                    Villages.Add(new VillageData
                    {
                        Position = positions[i],
                        SupplyAmount = rng.Next(1, 9),
                        IsUsed = false,
                        Name = names[i]
                    });
                }
            }
        }

        string GetVillageName(int idx)
        {
            string[] names = { "蒙德镇", "格兰村", "橡木堡", "溪谷庄", "石桥镇", "铁炉村" };
            return names[idx % names.Length];
        }

        #endregion

        #region 指令执行

        /// <summary>执行玩家指令</summary>
        public void ExecuteCommand(string command)
        {
            if (State != GameState.Playing) return;

            // 解析指令：找到目标骑士和行动
            var targetKnight = ParseCommandTarget(command);
            if (targetKnight == null)
            {
                OnMessage?.Invoke("无法识别指令中的骑士名称。");
                return;
            }

            Vector2Int targetPos = ParseCommandTarget(command, targetKnight);

            // 移动骑士（最多2格）
            MoveKnight(targetKnight, targetPos);

            // 检查遭遇
            CheckEncounters(targetKnight);

            // 进入下一回合
            CurrentTurn++;
            OnTurnChanged?.Invoke(CurrentTurn);

            // 检查胜负
            CheckWinLose();
        }

        KnightData ParseCommandTarget(string command)
        {
            if (command.Contains("全军") || command.Contains("全体"))
                return Knights.Find(k => k.IsAlive); // 返回第一个活着的骑士

            foreach (var k in Knights)
            {
                if (k.IsAlive && command.Contains(k.DisplayName))
                    return k;
            }
            return null;
        }

        Vector2Int ParseCommandTarget(string command, KnightData knight)
        {
            // 尝试解析方向指令
            if (command.Contains("北")) return knight.Position + new Vector2Int(0, 2);
            if (command.Contains("南")) return knight.Position + new Vector2Int(0, -2);
            if (command.Contains("东")) return knight.Position + new Vector2Int(2, 0);
            if (command.Contains("西")) return knight.Position + new Vector2Int(-2, 0);

            // 尝试找到目标村庄
            foreach (var v in Villages)
            {
                if (command.Contains(v.Name))
                    return v.Position;
            }

            // 默认向南移动
            return knight.Position + new Vector2Int(0, -2);
        }

        void MoveKnight(KnightData knight, Vector2Int target)
        {
            int dx = Mathf.Clamp(target.x - knight.Position.x, -2, 2);
            int dy = Mathf.Clamp(target.y - knight.Position.y, -2, 2);
            int newX = Mathf.Clamp(knight.Position.x + dx, 0, 31);
            int newY = Mathf.Clamp(knight.Position.y + dy, 0, 31);

            knight.Position = new Vector2Int(newX, newY);

            // 触发地形文字
            if (CurrentMap != null)
            {
                var terrain = CurrentMap.Grid[newX, newY];
                OnTerrainTrigger?.Invoke(MedMap.GetTerrainText(terrain));
            }

            // 检查是否进入村庄
            foreach (var v in Villages)
            {
                if (!v.IsUsed && Vector2Int.Distance(knight.Position, v.Position) <= 1)
                {
                    SupplyAtVillage(knight, v);
                    break;
                }
            }
        }

        void SupplyAtVillage(KnightData knight, VillageData village)
        {
            village.IsUsed = true;
            int recruits = village.SupplyAmount;
            knight.Troops = Mathf.Min(knight.Troops + recruits, knight.MaxTroops + 20); // 允许略微超过上限
            knight.Morale = knight.MaxMorale; // 战意恢复满

            OnImageTrigger?.Invoke("village");
            OnMessage?.Invoke($"进入{village.Name}，铁匠为马匹钉上新蹄铁，村民中有{recruits}人志愿加入骑兵队。{knight.DisplayName}的人数恢复至{knight.Troops}人，战意已满。");
        }

        #endregion

        #region 敌人刷新 & 遭遇

        void CheckEncounters(KnightData knight)
        {
            foreach (var enemy in Enemies)
            {
                if (enemy.IsDefeated || enemy.IsDiscovered) continue;

                float dist = Vector2Int.Distance(knight.Position, enemy.Position);
                if (dist <= 3f)
                {
                    // 刷新敌人
                    enemy.IsDiscovered = true;
                    OnEnemyDiscovered?.Invoke(enemy);
                    OnImageTrigger?.Invoke(enemy.Type == EnemyType.EnemyKnight ? "knight" : "bandit");

                    // 骑士侦察旁白
                    string intel = GenerateScoutReport(knight, enemy);
                    OnKnightReport?.Invoke(knight.DisplayName, intel);

                    // 自动进入战斗
                    StartCombat(knight, enemy);
                }
            }
        }

        string GenerateScoutReport(KnightData knight, EnemyData enemy)
        {
            float ratio = (float)knight.Troops / enemy.Troops;
            string dir = GetDirection(knight.Position, enemy.Position);

            if (ratio > 1.3f)
                return $"总指挥！{dir}方发现{enemy.DisplayName}，约{enemy.Troops}人。看他们那副样子，不堪一击！请下令进攻！";
            else if (ratio > 0.8f)
                return $"总指挥，{dir}方出现{enemy.DisplayName}，约{enemy.Troops}人。兵力相当，需谨慎应对。";
            else
                return $"总指挥...{dir}方发现{enemy.DisplayName}，人数众多，约{enemy.Troops}人。强攻恐怕不利，建议迂回或增援。";
        }

        string GetDirection(Vector2Int from, Vector2Int to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;
            if (Mathf.Abs(dx) > Mathf.Abs(dy))
                return dx > 0 ? "东" : "西";
            else
                return dy > 0 ? "北" : "南";
        }

        #endregion

        #region 战斗系统

        void StartCombat(KnightData knight, EnemyData enemy)
        {
            OnMessage?.Invoke($"⚔️ {knight.DisplayName} 与 {enemy.DisplayName} 交战！");

            var rng = new System.Random();
            int round = 0;

            while (knight.Troops > 0 && enemy.Troops > 0 && round < 10)
            {
                round++;

                // 伤害 = 战意 × 随机(0.7~1.1)
                float knightDmg = knight.Morale * (0.7f + (float)rng.NextDouble() * 0.4f) * 0.1f;
                float enemyDmg = enemy.Morale * (0.7f + (float)rng.NextDouble() * 0.4f) * 0.1f;

                enemy.Troops -= Mathf.RoundToInt(knightDmg);
                knight.Troops -= Mathf.RoundToInt(enemyDmg);

                // 战意变化
                if (knight.Troops < knight.MaxTroops * 0.3f)
                    knight.Morale -= 10;
                if (enemy.Troops < enemy.Troops * 0.3f)
                    enemy.Morale -= 10;

                // 战意低于30 → 溃逃（人数减少）
                if (knight.Morale < 30)
                {
                    int fled = knight.Troops / 3;
                    knight.Troops -= fled;
                    OnCombatResult?.Invoke($"{knight.DisplayName}的骑兵战意崩溃，{fled}人溃逃！");
                    break;
                }
                if (enemy.Morale < 30)
                {
                    int fled = enemy.Troops / 3;
                    enemy.Troops -= fled;
                    OnCombatResult?.Invoke($"{enemy.DisplayName}战意崩溃，{fled}人溃逃！");
                    break;
                }
            }

            // 战斗结果
            knight.Troops = Mathf.Max(0, knight.Troops);
            enemy.Troops = Mathf.Max(0, enemy.Troops);

            if (enemy.Troops <= 0)
            {
                enemy.IsDefeated = true;
                int growth = enemy.Type == EnemyType.Deserter ? 10 : 5;
                knight.MaxTroops += (enemy.Type == EnemyType.Deserter ? 10 : 0);
                knight.Morale = Mathf.Min(knight.Morale + growth, knight.MaxMorale + 20);
                OnCombatResult?.Invoke($"✅ {knight.DisplayName} 击败 {enemy.DisplayName}！击杀后获得成长：生命上限+{growth}");
            }
            else if (knight.Troops <= 0)
            {
                knight.IsAlive = false;
                OnCombatResult?.Invoke($"💀 {knight.DisplayName} 全军覆没...");
            }
            else
            {
                OnCombatResult?.Invoke($"⚔️ 战斗陷入僵持，双方暂时脱离接触。");
            }
        }

        #endregion

        #region 胜负判定

        void CheckWinLose()
        {
            // 检查失败：所有骑士阵亡
            bool anyAlive = false;
            foreach (var k in Knights) { if (k.IsAlive) { anyAlive = true; break; } }
            if (!anyAlive)
            {
                State = GameState.Defeat;
                OnGameOver?.Invoke(GameState.Defeat);
                return;
            }

            // 检查胜利：所有敌人被击败
            bool allDefeated = true;
            foreach (var e in Enemies) { if (!e.IsDefeated) { allDefeated = false; break; } }
            if (allDefeated)
            {
                State = GameState.Victory;
                OnGameOver?.Invoke(GameState.Victory);
                return;
            }

            // 回合数耗尽
            if (CurrentTurn > MaxTurns)
            {
                int defeated = 0;
                foreach (var e in Enemies) if (e.IsDefeated) defeated++;
                if (defeated >= 2)
                    State = GameState.Victory;
                else
                    State = GameState.Defeat;
                OnGameOver?.Invoke(State);
            }
        }

        #endregion

        #region 公共接口

        public KnightData GetKnight(KnightName name)
        {
            return Knights.Find(k => k.Name == name);
        }

        public List<EnemyData> GetDiscoveredEnemies()
        {
            return Enemies.FindAll(e => e.IsDiscovered && !e.IsDefeated);
        }

        public List<VillageData> GetAvailableVillages()
        {
            return Villages.FindAll(v => !v.IsUsed);
        }

        #endregion
    }
}
