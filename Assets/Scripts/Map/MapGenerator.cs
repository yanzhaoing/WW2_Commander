using System.Collections.Generic;
using UnityEngine;

namespace SWO1.Map
{
    public enum TerrainType
    {
        OpenGround,   // 开阔地 - 可通行
        River,        // 河流 - 不可通行(除非有桥)
        Forest,       // 树林 - 可通行，有掩护
        Village,      // 村庄 - 可通行，有掩护，可驻军
        Bridge,       // 桥梁 - 跨越河流
        Road,         // 道路 - 可通行，移动加速
        Beach         // 海滩 - 南侧边界，出发点
    }

    public class MapCell
    {
        public int X;              // 0-15
        public int Y;              // 0-11
        public string Coordinate;  // "A1"-"P12"
        public TerrainType Terrain;
        public bool Passable;      // 是否可通行
        public float CoverBonus;   // 掩护加成 0-0.5
        public bool HasRoad;       // 是否有道路
    }

    public class GameMap
    {
        public MapCell[,] Cells;   // [16,12]
        public List<Vector2Int> BridgePositions;
        public Vector2Int PlayerSpawn; // 玩家起始位置
    }

    public class MapGenerator : MonoBehaviour
    {
        public const int MapWidth = 32;   // A-P
        public const int MapHeight = 24;  // 1-12

        public GameMap CurrentMap { get; private set; }

        public GameMap GenerateMap(int seed = -1)
        {
            int actualSeed = seed == -1 ? System.Environment.TickCount : seed;
            var rng = new System.Random(actualSeed);

            var map = new GameMap
            {
                Cells = new MapCell[MapWidth, MapHeight],
                BridgePositions = new List<Vector2Int>()
            };

            // 0. 初始化所有格子为开阔地（防止 null）
            for (int x = 0; x < MapWidth; x++)
                for (int y = 0; y < MapHeight; y++)
                    map.Cells[x, y] = CreateCell(x, y, TerrainType.OpenGround, passable: true);

            // 1. 海滩：最南 2 行 (Y=10,11 → 第11,12行)
            GenerateBeach(map);

            // 2. 河流：随机选第5-7行起始，共3行横穿
            int riverStartRow = rng.Next(4, 7); // Y=4,5,6 → 第5,6,7行
            GenerateRiver(map, riverStartRow, rng);

            // 3. 桥梁：河流上 1-3 个
            int bridgeCount = rng.Next(1, 4);
            GenerateBridges(map, bridgeCount, rng);

            // 4. 树林：2-4 块，每块 2-5 格连通
            int forestCount = rng.Next(2, 5);
            GenerateForests(map, forestCount, rng);

            // 5. 村庄：2-3 个，每个 1-2 格
            int villageCount = rng.Next(2, 4);
            GenerateVillages(map, villageCount, rng);

            // 6. 道路：从海滩到最近桥梁
            GenerateRoads(map, rng);

            // 7. 玩家起始位置：海滩中心附近
            map.PlayerSpawn = new Vector2Int(MapWidth / 2, MapHeight - 1);

            CurrentMap = map;
            return map;
        }

        public string GetCoordinate(int x, int y)
        {
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return "??";
            char col = (char)('A' + x);
            int row = MapHeight - y; // Y=0 → row 12, Y=11 → row 1
            return $"{col}{row}";
        }

        // ── 生成阶段 ──────────────────────────────────────────

        private void GenerateBeach(GameMap map)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = MapHeight - 2; y < MapHeight; y++) // Y=10,11
                {
                    map.Cells[x, y] = CreateCell(x, y, TerrainType.Beach, passable: true);
                }
            }
        }

        private void GenerateRiver(GameMap map, int startRow, System.Random rng)
        {
            // 河流占据 startRow ~ startRow+2 共3行，横穿整张地图
            for (int x = 0; x < MapWidth; x++)
            {
                for (int offset = 0; offset < 3; offset++)
                {
                    int y = startRow + offset;
                    // 跳过已被海滩占用的格子
                    if (map.Cells[x, y] != null && map.Cells[x, y].Terrain == TerrainType.Beach)
                        continue;

                    map.Cells[x, y] = CreateCell(x, y, TerrainType.River, passable: false);
                }
            }
        }

        private void GenerateBridges(GameMap map, int count, System.Random rng)
        {
            // 收集河流列（每列是否有河流格）
            List<int> riverCols = new List<int>();
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    if (map.Cells[x, y] != null && map.Cells[x, y].Terrain == TerrainType.River)
                    {
                        riverCols.Add(x);
                        break;
                    }
                }
            }

            // 在河流列上随机放桥梁（每列只放一个桥梁格）
            List<int> available = new List<int>(riverCols);
            int actualCount = Mathf.Min(count, available.Count);
            for (int i = 0; i < actualCount; i++)
            {
                int idx = rng.Next(available.Count);
                int bx = available[idx];
                available.RemoveAt(idx);

                // 找到该列的河流格，只取中间一个改为桥梁
                List<int> riverRows = new List<int>();
                for (int y = 0; y < MapHeight; y++)
                {
                    if (map.Cells[bx, y] != null && map.Cells[bx, y].Terrain == TerrainType.River)
                    {
                        riverRows.Add(y);
                    }
                }

                if (riverRows.Count > 0)
                {
                    // 只取中间一行作为桥梁
                    int bridgeY = riverRows[riverRows.Count / 2];
                    map.Cells[bx, bridgeY].Terrain = TerrainType.Bridge;
                    map.Cells[bx, bridgeY].Passable = true;
                    map.BridgePositions.Add(new Vector2Int(bx, bridgeY));
                }
            }
        }

        private void GenerateForests(GameMap map, int count, System.Random rng)
        {
            int maxAttempts = 200;

            for (int i = 0; i < count; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < maxAttempts && !placed; attempt++)
                {
                    int sx = rng.Next(0, MapWidth);
                    int sy = rng.Next(0, MapHeight - 2); // 不在海滩上
                    int patchSize = rng.Next(2, 6);       // 2-5 格

                    if (map.Cells[sx, sy] != null) continue;

                    // 用 BFS 展开树丛连通块
                    var patch = new List<Vector2Int>();
                    var visited = new HashSet<int>();
                    var queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(sx, sy));
                    visited.Add(sy * MapWidth + sx);

                    while (queue.Count > 0 && patch.Count < patchSize)
                    {
                        var cur = queue.Dequeue();
                        if (map.Cells[cur.x, cur.y] != null) continue;
                        patch.Add(cur);

                        // 四方向扩展
                        int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
                        foreach (var d in dirs)
                        {
                            int nx = cur.x + d[0];
                            int ny = cur.y + d[1];
                            int key = ny * MapWidth + nx;
                            if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight - 2 && !visited.Contains(key))
                            {
                                visited.Add(key);
                                queue.Enqueue(new Vector2Int(nx, ny));
                            }
                        }
                    }

                    if (patch.Count >= 2)
                    {
                        foreach (var p in patch)
                        {
                            map.Cells[p.x, p.y] = CreateCell(p.x, p.y, TerrainType.Forest, passable: true, coverBonus: 0.3f);
                        }
                        placed = true;
                    }
                }
            }
        }

        private void GenerateVillages(GameMap map, int count, System.Random rng)
        {
            for (int i = 0; i < count; i++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 100 && !placed; attempt++)
                {
                    int vx = rng.Next(0, MapWidth);
                    int vy = rng.Next(0, MapHeight - 2);
                    if (map.Cells[vx, vy] != null) continue;

                    map.Cells[vx, vy] = CreateCell(vx, vy, TerrainType.Village, passable: true, coverBonus: 0.4f);
                    placed = true;

                    // 50% 概率扩展第二格
                    if (rng.NextDouble() < 0.5)
                    {
                        int[][] dirs = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
                        var shuffled = new List<int[]>(dirs);
                        for (int s = shuffled.Count - 1; s > 0; s--)
                        {
                            int j = rng.Next(s + 1);
                            var tmp = shuffled[s];
                            shuffled[s] = shuffled[j];
                            shuffled[j] = tmp;
                        }

                        foreach (var d in shuffled)
                        {
                            int nx = vx + d[0];
                            int ny = vy + d[1];
                            if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight - 2 && map.Cells[nx, ny] == null)
                            {
                                map.Cells[nx, ny] = CreateCell(nx, ny, TerrainType.Village, passable: true, coverBonus: 0.4f);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void GenerateRoads(GameMap map, System.Random rng)
        {
            if (map.BridgePositions.Count == 0) return;

            // 找离海滩中心最近的桥梁
            Vector2Int beachCenter = new Vector2Int(MapWidth / 2, MapHeight - 1);
            Vector2Int nearestBridge = map.BridgePositions[0];
            float minDist = float.MaxValue;

            foreach (var bp in map.BridgePositions)
            {
                float dist = Vector2.Distance(beachCenter, bp);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestBridge = bp;
                }
            }

            // 先垂直向下（Y递减）到桥梁行，再水平移动到桥梁列
            int cx = beachCenter.x;
            int cy = beachCenter.y;

            // 垂直段
            while (cy > nearestBridge.y)
            {
                cy--;
                ApplyRoad(map, cx, cy);
            }

            // 水平段
            while (cx != nearestBridge.x)
            {
                cx += (nearestBridge.x > cx) ? 1 : -1;
                ApplyRoad(map, cx, cy);
            }
        }

        // ── 工具方法 ──────────────────────────────────────────

        private MapCell CreateCell(int x, int y, TerrainType terrain, bool passable, float coverBonus = 0f)
        {
            return new MapCell
            {
                X = x,
                Y = y,
                Coordinate = GetCoordinate(x, y),
                Terrain = terrain,
                Passable = passable,
                CoverBonus = coverBonus,
                HasRoad = false
            };
        }

        private void ApplyRoad(GameMap map, int x, int y)
        {
            var cell = map.Cells[x, y];
            if (cell == null)
            {
                // 覆盖空格为道路
                map.Cells[x, y] = CreateCell(x, y, TerrainType.Road, passable: true);
                map.Cells[x, y].HasRoad = true;
            }
            else if (cell.Terrain == TerrainType.OpenGround || cell.Terrain == TerrainType.Forest)
            {
                // 在开阔地或树林上叠加道路属性
                cell.HasRoad = true;
                cell.Terrain = TerrainType.Road;
            }
            // 河流/海滩等不覆盖
        }
    }
}
