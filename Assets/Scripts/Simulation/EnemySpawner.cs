using SWO1.Map;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SWO1.Simulation
{
    public enum EnemyBehavior2D { Garrison, Patrol, Ambush, Reinforcement }

    [Serializable]
    public class EnemyUnit2D
    {
        public string Id;
        public string Name;               // "德军步兵班", "德军机枪组"
        public int GridX, GridY;
        public int Strength;              // 兵力 1-10
        public EnemyBehavior2D Behavior;
        public bool IsDiscovered;         // 是否已被玩家发现
        public bool IsActive;             // 是否还活着

        // Patrol 内部状态
        public float PatrolTimer;
        public float PatrolInterval;
        public int HomeX, HomeY;          // 巡逻中心
        public int PatrolRadius;

        // Reinforcement 内部状态
        public int TargetX, TargetY;
        public float MoveTimer;

        // 追踪已触发的发现事件
        public bool DiscoveryTriggered;
    }

    [Serializable]
    public class WaveConfig2D
    {
        public float TriggerTime;         // 触发游戏时间（分钟）
        public int UnitCount;             // 出现几个单位
        public string SpawnEdge;          // "North", "East", "West"
        public string Direction;          // 移动方向描述
        public int TargetX, TargetY;      // 目标位置
    }

    public class EnemySpawner : MonoBehaviour
    {
        private const int MapWidth = 32;
        private const int MapHeight = 24;
        private const int BeachMinDistance = 4;
        private const int BridgeRange = 2;

        private List<EnemyUnit2D> _allUnits = new List<EnemyUnit2D>();
        private List<WaveConfig2D> _waves;
        private int _nextWaveIndex = 0;
        private System.Random _rng;
        private int _seed;

        // ---- 公共接口 ----

        public List<EnemyUnit2D> DeployInitialEnemies(GameMap map, int seed = -1)
        {
            _seed = seed < 0 ? DateTime.Now.GetHashCode() : seed;
            _rng = new System.Random(_seed);
            _allUnits.Clear();
            _nextWaveIndex = 0;
            BuildWavePresets(map);

            int targetCount = _rng.Next(8, 13); // 8-12
            HashSet<int> occupied = new HashSet<int>();

            // 1. 桥梁附近：2-3 个守军
            if (map.BridgePositions != null)
            {
                foreach (var bridge in map.BridgePositions)
                {
                    int garrisonCount = _rng.Next(2, 4);
                    for (int i = 0; i < garrisonCount; i++)
                    {
                        var pos = FindSpotNearBridge(map, bridge, occupied);
                        if (pos.HasValue)
                        {
                            var unit = CreateEnemy(pos.Value.x, pos.Value.y, EnemyBehavior2D.Garrison,
                                PickName("garrison"), _rng.Next(3, 8));
                            _allUnits.Add(unit);
                            occupied.Add(pos.Value.x * MapHeight + pos.Value.y);
                        }
                    }
                }
            }

            // 2. 村庄内：70% 概率 1-2 个驻军
            if (GetPositionsByTerrain(map, TerrainType.Village) != null && _rng.NextDouble() < 0.7)
            {
                int count = _rng.Next(1, 3);
                foreach (var village in GetPositionsByTerrain(map, TerrainType.Village))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var pos = FindSpotInArea(map, village, occupied, 1);
                        if (pos.HasValue)
                        {
                            var unit = CreateEnemy(pos.Value.x, pos.Value.y, EnemyBehavior2D.Garrison,
                                PickName("garrison"), _rng.Next(2, 6));
                            _allUnits.Add(unit);
                            occupied.Add(pos.Value.x * MapHeight + pos.Value.y);
                        }
                    }
                }
            }

            // 3. 树林中：50% 概率 1-2 个伏兵
            if (GetPositionsByTerrain(map, TerrainType.Forest) != null && _rng.NextDouble() < 0.5)
            {
                int count = _rng.Next(1, 3);
                foreach (var forest in GetPositionsByTerrain(map, TerrainType.Forest))
                {
                    for (int i = 0; i < count; i++)
                    {
                        var pos = FindSpotInArea(map, forest, occupied, 1);
                        if (pos.HasValue)
                        {
                            var unit = CreateEnemy(pos.Value.x, pos.Value.y, EnemyBehavior2D.Ambush,
                                PickName("ambush"), _rng.Next(2, 5));
                            _allUnits.Add(unit);
                            occupied.Add(pos.Value.x * MapHeight + pos.Value.y);
                        }
                    }
                }
            }

            // 4. 开阔地：1-2 个巡逻队
            int patrolCount = _rng.Next(1, 3);
            for (int i = 0; i < patrolCount; i++)
            {
                var pos = FindSpotOpenTerrain(map, occupied);
                if (pos.HasValue)
                {
                    var unit = CreateEnemy(pos.Value.x, pos.Value.y, EnemyBehavior2D.Patrol,
                        PickName("patrol"), _rng.Next(2, 6));
                    unit.HomeX = pos.Value.x;
                    unit.HomeY = pos.Value.y;
                    unit.PatrolRadius = 2;
                    unit.PatrolInterval = _rng.Next(20, 41);
                    unit.PatrolTimer = (float)_rng.NextDouble() * unit.PatrolInterval;
                    _allUnits.Add(unit);
                    occupied.Add(pos.Value.x * MapHeight + pos.Value.y);
                }
            }

            return _allUnits;
        }

        public List<EnemyUnit2D> GetActiveEnemies()
        {
            return _allUnits.FindAll(u => u.IsActive);
        }

        public List<EnemyUnit2D> GetEnemiesNearPosition(int gx, int gy, int range)
        {
            return _allUnits.FindAll(u =>
                u.IsActive &&
                Math.Abs(u.GridX - gx) <= range &&
                Math.Abs(u.GridY - gy) <= range);
        }

        public void UpdateAllUnits(float gameDeltaTime)
        {
            foreach (var unit in _allUnits)
            {
                if (!unit.IsActive) continue;
                UpdateUnit(unit, gameDeltaTime);
            }
        }

        public void KillUnit(string unitId)
        {
            var unit = _allUnits.Find(u => u.Id == unitId);
            if (unit != null)
            {
                unit.IsActive = false;
            }
        }

        public WaveConfig2D GetNextWave(float currentGameTime)
        {
            if (_nextWaveIndex >= _waves.Count) return null;
            var wave = _waves[_nextWaveIndex];
            if (currentGameTime >= wave.TriggerTime)
            {
                _nextWaveIndex++;
                return wave;
            }
            return null;
        }

        public List<EnemyUnit2D> SpawnWave(WaveConfig2D wave, GameMap map)
        {
            List<EnemyUnit2D> spawned = new List<EnemyUnit2D>();
            for (int i = 0; i < wave.UnitCount; i++)
            {
                int gx, gy;
                GetSpawnPosition(wave.SpawnEdge, out gx, out gy, i);

                var unit = CreateEnemy(gx, gy, EnemyBehavior2D.Reinforcement, PickName("reinforcement"), _rng.Next(3, 7));
                unit.TargetX = wave.TargetX;
                unit.TargetY = wave.TargetY;
                _allUnits.Add(unit);
                spawned.Add(unit);
            }

            OnWaveSpawn?.Invoke(wave);
            return spawned;
        }

        // ---- 事件 ----

        public event Action<EnemyUnit2D> OnEnemyDiscovered;
        public event Action<EnemyUnit2D, EnemyUnit2D> OnEncounter;
        public event Action<WaveConfig2D> OnWaveSpawn;

        // 遭遇冷却（敌人ID → 上次遭遇时间）
        private Dictionary<string, float> _encounterCooldowns = new Dictionary<string, float>();
        private const float EncounterCooldownSeconds = 3f;

        // ---- 遭遇检测（由外部调用，传入我方单位位置） ----

        public void CheckEncounters(List<(string unitId, int gx, int gy)> playerUnits)
        {
            float now = Time.time;
            foreach (var enemy in _allUnits)
            {
                if (!enemy.IsActive) continue;

                // 冷却检查
                if (_encounterCooldowns.TryGetValue(enemy.Id, out float lastTime) && now - lastTime < EncounterCooldownSeconds)
                    continue;

                foreach (var player in playerUnits)
                {
                    int dist = Math.Abs(enemy.GridX - player.gx) + Math.Abs(enemy.GridY - player.gy);
                    if (dist <= 1)
                    {
                        _encounterCooldowns[enemy.Id] = now;

                        // Ambush 且未被发现 → 先触发发现
                        if (enemy.Behavior == EnemyBehavior2D.Ambush && !enemy.DiscoveryTriggered)
                        {
                            enemy.IsDiscovered = true;
                            enemy.DiscoveryTriggered = true;
                            OnEnemyDiscovered?.Invoke(enemy);
                        }

                        // 传递正确的玩家单位信息
                var playerUnit = new EnemyUnit2D
                {
                    Id = player.unitId,
                    GridX = player.gx,
                    GridY = player.gy,
                    IsActive = true
                };
                OnEncounter?.Invoke(enemy, playerUnit);
                        break; // 每个敌人每帧只触发一次
                    }
                }
            }
        }

        // 重载：直接传入我方坐标列表
        public void CheckEncounters(List<Vector2Int> playerPositions)
        {
            float now = Time.time;
            foreach (var enemy in _allUnits)
            {
                if (!enemy.IsActive) continue;

                // 冷却检查
                if (_encounterCooldowns.TryGetValue(enemy.Id, out float lastTime) && now - lastTime < EncounterCooldownSeconds)
                    continue;

                foreach (var pp in playerPositions)
                {
                    int dist = Math.Abs(enemy.GridX - pp.x) + Math.Abs(enemy.GridY - pp.y);
                    if (dist <= 1)
                    {
                        _encounterCooldowns[enemy.Id] = now;

                        if (enemy.Behavior == EnemyBehavior2D.Ambush && !enemy.DiscoveryTriggered)
                        {
                            enemy.IsDiscovered = true;
                            enemy.DiscoveryTriggered = true;
                            OnEnemyDiscovered?.Invoke(enemy);
                        }

                        var playerUnit = new EnemyUnit2D
                        {
                            Id = "player_" + pp.x + "_" + pp.y,
                            GridX = pp.x,
                            GridY = pp.y,
                            IsActive = true
                        };
                        OnEncounter?.Invoke(enemy, playerUnit);
                        break;
                    }
                }
            }
        }

        // ---- 内部逻辑 ----

        private void UpdateUnit(EnemyUnit2D unit, float gameDeltaTime)
        {
            switch (unit.Behavior)
            {
                case EnemyBehavior2D.Garrison:
                    // 固定不动
                    break;

                case EnemyBehavior2D.Patrol:
                    UpdatePatrol(unit, gameDeltaTime);
                    break;

                case EnemyBehavior2D.Ambush:
                    // 静止，发现由 CheckEncounters 处理
                    break;

                case EnemyBehavior2D.Reinforcement:
                    UpdateReinforcement(unit, gameDeltaTime);
                    break;
            }
        }

        private void UpdatePatrol(EnemyUnit2D unit, float gameDt)
        {
            unit.PatrolTimer += gameDt;
            if (unit.PatrolTimer < unit.PatrolInterval) return;

            unit.PatrolTimer = 0f;
            unit.PatrolInterval = _rng.Next(20, 41);

            // 在 HomeX/HomeY ± PatrolRadius 范围内随机移动
            int dx = _rng.Next(-unit.PatrolRadius, unit.PatrolRadius + 1);
            int dy = _rng.Next(-unit.PatrolRadius, unit.PatrolRadius + 1);
            int nx = Mathf.Clamp(unit.HomeX + dx, 0, MapWidth - 1);
            int ny = Mathf.Clamp(unit.HomeY + dy, 0, MapHeight - 1);

            // 不走出巡逻范围（曼哈顿距离）
            if (Math.Abs(nx - unit.HomeX) + Math.Abs(ny - unit.HomeY) <= unit.PatrolRadius * 2)
            {
                unit.GridX = nx;
                unit.GridY = ny;
            }
        }

        private void UpdateReinforcement(EnemyUnit2D unit, float gameDt)
        {
            unit.MoveTimer += gameDt;
            if (unit.MoveTimer < 3f) return; // 每 3 秒移动一步

            unit.MoveTimer = 0f;

            // 向目标移动
            int dx = Math.Sign(unit.TargetX - unit.GridX);
            int dy = Math.Sign(unit.TargetY - unit.GridY);

            // 优先沿主方向移动
            if (Math.Abs(unit.TargetX - unit.GridX) >= Math.Abs(unit.TargetY - unit.GridY))
            {
                if (dx != 0) unit.GridX += dx;
                else if (dy != 0) unit.GridY += dy;
            }
            else
            {
                if (dy != 0) unit.GridY += dy;
                else if (dx != 0) unit.GridX += dx;
            }

            unit.GridX = Mathf.Clamp(unit.GridX, 0, MapWidth - 1);
            unit.GridY = Mathf.Clamp(unit.GridY, 0, MapHeight - 1);
        }

        private EnemyUnit2D CreateEnemy(int x, int y, EnemyBehavior2D behavior, string name, int strength)
        {
            return new EnemyUnit2D
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                GridX = x,
                GridY = y,
                Strength = Mathf.Clamp(strength, 1, 10),
                Behavior = behavior,
                IsDiscovered = false,
                IsActive = true,
                DiscoveryTriggered = false,
                MoveTimer = 0f,
                PatrolTimer = 0f,
            };
        }

        private string PickName(string category)
        {
            string[][] namePool = new string[][]
            {
                new[] { "德军步兵班", "德军机枪组", "德军掷弹兵", "德军迫击炮组" },
                new[] { "德军伏击小队", "德军狙击手", "德军突击组" },
                new[] { "德军巡逻队", "德军侦察兵", "德军摩托化步兵" },
                new[] { "德军增援步兵", "德军预备队", "德军装甲掷弹兵" },
            };

            int idx = category switch
            {
                "garrison" => 0,
                "ambush" => 1,
                "patrol" => 2,
                "reinforcement" => 3,
                _ => 0,
            };

            var pool = namePool[idx];
            return pool[_rng.Next(pool.Length)];
        }

        // ---- 部署辅助 ----

        private Vector2Int? FindSpotNearBridge(GameMap map, Vector2Int bridge, HashSet<int> occupied)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int dx = _rng.Next(-BridgeRange, BridgeRange + 1);
                int dy = _rng.Next(-BridgeRange, BridgeRange + 1);
                int x = bridge.x + dx;
                int y = bridge.y + dy;

                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;
                if (occupied.Contains(x * MapHeight + y)) continue;
                if (!IsFarFromBeach(x, y)) continue;

                return new Vector2Int(x, y);
            }
            return null;
        }

        private Vector2Int? FindSpotInArea(GameMap map, Vector2Int center, HashSet<int> occupied, int range)
        {
            for (int attempt = 0; attempt < 15; attempt++)
            {
                int dx = _rng.Next(-range, range + 1);
                int dy = _rng.Next(-range, range + 1);
                int x = center.x + dx;
                int y = center.y + dy;

                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;
                if (occupied.Contains(x * MapHeight + y)) continue;
                if (!IsFarFromBeach(x, y)) continue;

                return new Vector2Int(x, y);
            }
            return null;
        }

        private Vector2Int? FindSpotOpenTerrain(GameMap map, HashSet<int> occupied)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                int x = _rng.Next(0, MapWidth);
                int y = _rng.Next(0, MapHeight);

                if (occupied.Contains(x * MapHeight + y)) continue;
                if (!IsFarFromBeach(x, y)) continue;
                if (map != null && map.Cells[x, y].Terrain != SWO1.Map.TerrainType.OpenGround) continue;

                return new Vector2Int(x, y);
            }
            return null;
        }

        private bool IsFarFromBeach(int x, int y)
        {
            int dist = x + y; // 海滩假设在 (0,0) 附近
            return dist >= BeachMinDistance;
        }

        // 也可以用显式海滩坐标检查
        private bool IsFarFromBeach(int x, int y, int beachX, int beachY)
        {
            return Math.Abs(x - beachX) + Math.Abs(y - beachY) >= BeachMinDistance;
        }

        private void GetSpawnPosition(string edge, out int gx, out int gy, int index)
        {
            switch (edge)
            {
                case "North":
                    gx = _rng.Next(0, MapWidth);
                    gy = MapHeight - 1;
                    break;
                case "East":
                    gx = MapWidth - 1;
                    gy = _rng.Next(0, MapHeight);
                    break;
                case "West":
                    gx = 0;
                    gy = _rng.Next(0, MapHeight);
                    break;
                default:
                    gx = _rng.Next(0, MapWidth);
                    gy = MapHeight - 1;
                    break;
            }

            // 错开多个单位的位置
            gx = Mathf.Clamp(gx + index, 0, MapWidth - 1);
        }

        private void BuildWavePresets(GameMap map)
        {
            int targetX = 8, targetY = 6;
            if (map != null && map.BridgePositions != null && map.BridgePositions.Count > 0)
            {
                targetX = map.BridgePositions[0].x;
                targetY = map.BridgePositions[0].y;
            }

            _waves = new List<WaveConfig2D>
            {
                new WaveConfig2D
                {
                    TriggerTime = 3f,
                    UnitCount = _rng.Next(2, 4),  // 2-3
                    SpawnEdge = "East",
                    Direction = "从东侧进攻桥梁",
                    TargetX = targetX,
                    TargetY = targetY,
                },
                new WaveConfig2D
                {
                    TriggerTime = 5f,
                    UnitCount = _rng.Next(3, 5),  // 3-4
                    SpawnEdge = "North",
                    Direction = "从北侧增援",
                    TargetX = targetX,
                    TargetY = targetY,
                },
                new WaveConfig2D
                {
                    TriggerTime = 7f,
                    UnitCount = _rng.Next(5, 8),  // 5-7, 大量
                    SpawnEdge = "West",
                    Direction = "多方向反击",
                    TargetX = targetX,
                    TargetY = targetY,
                },
            };
        }

    private List<Vector2Int> GetPositionsByTerrain(GameMap map, SWO1.Map.TerrainType terrain)
    {
        var result = new List<Vector2Int>();
        for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
            {
                var cell = map.Cells[x, y];
                if (cell != null && cell.Terrain == terrain)
                    result.Add(new Vector2Int(x, y));
            }
        return result;
    }

    }
}
