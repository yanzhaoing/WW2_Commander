// MapParser.cs — 解析 Tiled .tmj 地图文件
using UnityEngine;
using System.Collections.Generic;

namespace SWO1.Medieval
{
    public enum Terrain { Plains, Forest, River, Mountain, Road, Village }

    [System.Serializable]
    public class MedMap
    {
        public int Width, Height;
        public Terrain[,] Grid;
        public List<Vector2Int> Villages = new List<Vector2Int>();
        public List<Vector2Int> EnemyZones = new List<Vector2Int>();
        public Vector2Int PlayerStart;

        // 地形文字触发
        public static string GetTerrainText(Terrain t)
        {
            switch (t)
            {
                case Terrain.Plains:   return "骑兵在草地上飞驰，马蹄扬起尘土。";
                case Terrain.Forest:   return "骑兵穿入密林，树枝刮过盔甲，马匹在狭窄的林道中艰难前行。";
                case Terrain.River:    return "骑兵涉水渡河，冰冷的河水没过马膝，马匹不安地嘶鸣。";
                case Terrain.Mountain: return "骑兵登上山丘，风从背后吹来，远处的村庄冒着炊烟。";
                case Terrain.Road:     return "骑兵沿大道行进，马蹄声整齐有力。";
                case Terrain.Village:  return "骑兵进入村庄，村民们好奇地围上来，铁匠为马匹钉上新蹄铁。";
                default: return "";
            }
        }
    }

    public class MapParser : MonoBehaviour
    {
        public static MapParser Instance { get; private set; }

        [Header("地图文件")]
        public TextAsset MapJson;
        public Texture2D TilesetTexture;

        // tile ID → terrain 类型的映射
        // 根据 punyworld-overworld tileset 分析
        private static readonly HashSet<int> WaterTiles  = new HashSet<int> { 87 };
        private static readonly HashSet<int> MountainTiles = new HashSet<int> { 5,6,7,59,60,61 };
        private static readonly HashSet<int> RoadTiles   = new HashSet<int> { 11,12,13,34,38 };
        private static readonly HashSet<int> ForestTiles = new HashSet<int> { 39,40,65,66,67 };
        private static readonly HashSet<int> BuildingTiles = new HashSet<int> {
            272,273,274,275,276,277,278,279,280,281,282,283,284,285,286,287,288,289,290,291,292,293,294,295,296,297,
            298,299,300,301,302,303,304,305,306,307,308,309,310,311,312,313,314,315,316,317,318,319,
            320,321,322,323,324,325,326,327,328,329,330,331,332,333,334,335,336,337,338,339,340,
            353,354,355,356,357,358,359,360,361,386,387,388,413,414,514,517
        };

        void Awake() { Instance = this; }

        public MedMap ParseMap()
        {
            var map = new MedMap();

            if (MapJson == null)
            {
                // 尝试从文件加载
                string path = System.IO.Path.Combine(Application.streamingAssetsPath, "world.tmj");
                if (System.IO.File.Exists(path))
                    MapJson = new TextAsset(System.IO.File.ReadAllText(path));
            }

            if (MapJson == null)
            {
                Debug.LogWarning("[MapParser] 未找到地图文件，使用默认地图");
                return GenerateDefaultMap();
            }

            var json = JsonUtility.FromJson<TiledMap>(MapJson.text);
            map.Width = json.width;
            map.Height = json.height;
            map.Grid = new Terrain[map.Width, map.Height];

            // 解析第一层（地面层）
            if (json.layers != null && json.layers.Length > 0)
            {
                var layer = json.layers[0];
                for (int i = 0; i < layer.data.Length && i < map.Width * map.Height; i++)
                {
                    int x = i % map.Width;
                    int y = map.Height - 1 - (i / map.Width); // Tiled Y轴向下，Unity向上
                    int tileId = layer.data[i];

                    map.Grid[x, y] = ClassifyTile(tileId);
                }
            }

            // 解析第二层（装饰层），识别村庄
            if (json.layers != null && json.layers.Length > 1)
            {
                var layer2 = json.layers[1];
                for (int i = 0; i < layer2.data.Length && i < map.Width * map.Height; i++)
                {
                    int x = i % map.Width;
                    int y = map.Height - 1 - (i / map.Width);
                    int tileId = layer2.data[i];

                    if (BuildingTiles.Contains(tileId) && !map.Villages.Contains(new Vector2Int(x, y)))
                    {
                        // 检查是否靠近已有的村庄（合并邻近建筑）
                        bool nearExisting = false;
                        foreach (var v in map.Villages)
                        {
                            if (Mathf.Abs(v.x - x) <= 2 && Mathf.Abs(v.y - y) <= 2)
                            { nearExisting = true; break; }
                        }
                        if (!nearExisting)
                            map.Villages.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 设置玩家起始位置（地图底部中心附近）
            map.PlayerStart = new Vector2Int(map.Width / 2, 5);

            // 识别可通行区域作为敌人刷新区
            IdentifyEnemyZones(map);

            Debug.Log($"[MapParser] 地图加载完成: {map.Width}x{map.Height}, {map.Villages.Count} 个村庄");
            return map;
        }

        Terrain ClassifyTile(int id)
        {
            if (id == 0) return Terrain.Plains; // 空白 = 平原
            if (WaterTiles.Contains(id)) return Terrain.River;
            if (MountainTiles.Contains(id)) return Terrain.Mountain;
            if (RoadTiles.Contains(id)) return Terrain.Road;
            if (ForestTiles.Contains(id)) return Terrain.Forest;
            if (BuildingTiles.Contains(id)) return Terrain.Village;
            return Terrain.Plains; // 默认平原
        }

        void IdentifyEnemyZones(MedMap map)
        {
            // 在远离玩家起始位置的可通行区域放置敌人刷新区
            var candidates = new List<Vector2Int>();
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    if (map.Grid[x, y] == Terrain.Plains || map.Grid[x, y] == Terrain.Forest)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(map.PlayerStart.x, map.PlayerStart.y));
                        if (dist > 5f && dist < 25f)
                            candidates.Add(new Vector2Int(x, y));
                    }
                }
            }

            // 随机选3个位置作为敌人刷新区
            if (candidates.Count > 0)
            {
                var rng = new System.Random(42);
                for (int i = 0; i < 3 && candidates.Count > 0; i++)
                {
                    int idx = rng.Next(candidates.Count);
                    map.EnemyZones.Add(candidates[idx]);
                    candidates.RemoveAt(idx);
                }
            }
        }

        MedMap GenerateDefaultMap()
        {
            var map = new MedMap { Width = 32, Height = 32 };
            map.Grid = new Terrain[32, 32];

            // 全部平原
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                    map.Grid[x, y] = Terrain.Plains;

            // 添加一些村庄
            map.Villages.Add(new Vector2Int(16, 20));
            map.Villages.Add(new Vector2Int(8, 15));
            map.Villages.Add(new Vector2Int(24, 10));

            // 添加河流
            for (int x = 5; x < 25; x++)
            {
                map.Grid[x, 12] = Terrain.River;
                map.Grid[x, 13] = Terrain.River;
            }

            // 添加山丘
            for (int x = 10; x < 14; x++)
                for (int y = 8; y < 11; y++)
                    map.Grid[x, y] = Terrain.Mountain;

            map.PlayerStart = new Vector2Int(16, 5);
            map.EnemyZones.Add(new Vector2Int(12, 8));
            map.EnemyZones.Add(new Vector2Int(20, 18));
            map.EnemyZones.Add(new Vector2Int(16, 25));

            return map;
        }

        // ── Tiled JSON 结构 ──
        [System.Serializable]
        class TiledMap
        {
            public int width, height;
            public TiledLayer[] layers;
        }

        [System.Serializable]
        class TiledLayer
        {
            public string name;
            public int[] data;
        }
    }
}
