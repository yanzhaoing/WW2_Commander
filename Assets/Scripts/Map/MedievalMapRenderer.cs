// MedievalMapRenderer.cs — 中世纪地图渲染器
// 用 SpriteRenderer 显示 Tiled 地图数据
using UnityEngine;
using System.Collections.Generic;

namespace SWO1.Medieval
{
    public class MedievalMapRenderer : MonoBehaviour
    {
        [Header("资源")]
        public TextAsset MapJson;
        public Texture2D TilesetTexture;

        [Header("配置")]
        public float CellSize = 1f;
        public int MapWidth = 32;
        public int MapHeight = 32;

        // 地形颜色（无 tileset 时的 fallback）
        private static readonly Color C_Plains   = new Color(0.45f, 0.55f, 0.35f);
        private static readonly Color C_Forest   = new Color(0.2f, 0.35f, 0.15f);
        private static readonly Color C_River    = new Color(0.25f, 0.45f, 0.65f);
        private static readonly Color C_Mountain = new Color(0.55f, 0.45f, 0.3f);
        private static readonly Color C_Road     = new Color(0.7f, 0.6f, 0.4f);
        private static readonly Color C_Village  = new Color(0.6f, 0.45f, 0.25f);

        private Sprite whitePixel;
        private MedMap _map;
        private Dictionary<Vector2Int, GameObject> _tileObjects = new Dictionary<Vector2Int, GameObject>();
        private Dictionary<Vector2Int, GameObject> _markers = new Dictionary<Vector2Int, GameObject>();

        // tileset sprite 缓存
        private Sprite[] _tileSprites;

        void Start()
        {
            whitePixel = CreateWhiteSprite();

            // 解析地图
            var parser = MapParser.Instance;
            if (parser != null)
            {
                parser.MapJson = MapJson;
                parser.TilesetTexture = TilesetTexture;
                _map = parser.ParseMap();
            }
            else
            {
                _map = new MedMap { Width = 32, Height = 32 };
                _map.Grid = new Terrain[32, 32];
                for (int x = 0; x < 32; x++)
                    for (int y = 0; y < 32; y++)
                        _map.Grid[x, y] = Terrain.Plains;
                _map.PlayerStart = new Vector2Int(16, 5);
            }

            // 将地图数据传递给 GameDirector
            var director = MedievalGameDirector.Instance;
            if (director != null)
                director.CurrentMap = _map;

            // 初始化 tileset
            InitTileset();

            // 渲染地图
            RenderMap();

            // 标记村庄
            MarkVillages();

            // 标记骑士位置
            MarkKnights();
        }

        void InitTileset()
        {
            if (TilesetTexture == null) return;

            int cols = 27; // tileset 列数（从 tsx 确认）
            int tileW = 16, tileH = 16;
            int texW = TilesetTexture.width;
            int texH = TilesetTexture.height;
            int totalTiles = (texW / tileW) * (texH / tileH);

            _tileSprites = new Sprite[totalTiles];
            for (int i = 0; i < totalTiles; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = col * tileW;
                float y = texH - (row + 1) * tileH; // Unity Y轴向上
                _tileSprites[i] = Sprite.Create(TilesetTexture,
                    new Rect(x, y, tileW, tileH),
                    new Vector2(0.5f, 0.5f), 16f);
            }
        }

        void RenderMap()
        {
            if (_map == null) return;

            for (int x = 0; x < _map.Width; x++)
            {
                for (int y = 0; y < _map.Height; y++)
                {
                    var terrain = _map.Grid[x, y];
                    var go = CreateTile(x, y, terrain);
                    _tileObjects[new Vector2Int(x, y)] = go;
                }
            }
        }

        GameObject CreateTile(int x, int y, Terrain terrain)
        {
            var go = new GameObject($"Tile_{x}_{y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x * CellSize, y * CellSize, 0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = GetTerrainColor(terrain);
            sr.sortingOrder = 0;
            go.transform.localScale = new Vector3(CellSize, CellSize, 1);

            // 添加 collider 用于点击检测
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(CellSize, CellSize);

            return go;
        }

        Color GetTerrainColor(Terrain t)
        {
            switch (t)
            {
                case Terrain.Plains:   return C_Plains;
                case Terrain.Forest:   return C_Forest;
                case Terrain.River:    return C_River;
                case Terrain.Mountain: return C_Mountain;
                case Terrain.Road:     return C_Road;
                case Terrain.Village:  return C_Village;
                default:               return C_Plains;
            }
        }

        void MarkVillages()
        {
            if (_map == null) return;
            foreach (var v in _map.Villages)
            {
                MarkPosition(v, "🏘️", Color.yellow, 5);
            }
        }

        void MarkKnights()
        {
            var director = MedievalGameDirector.Instance;
            if (director == null) return;

            foreach (var k in director.Knights)
            {
                if (k.IsAlive)
                    MarkPosition(k.Position, GetKnightIcon(k.Name), k.Color, 10);
            }
        }

        string GetKnightIcon(KnightName name)
        {
            switch (name)
            {
                case KnightName.DonQuixote: return "🔴";
                case KnightName.Lancelot:   return "🔵";
                case KnightName.ElCid:      return "🟠";
                default: return "⚪";
            }
        }

        public void MarkPosition(Vector2Int pos, string icon, Color color, int sortOrder)
        {
            if (_markers.ContainsKey(pos))
            {
                Destroy(_markers[pos]);
                _markers.Remove(pos);
            }

            var go = new GameObject($"Marker_{pos.x}_{pos.y}");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(pos.x * CellSize + CellSize * 0.5f, pos.y * CellSize + CellSize * 0.5f, 0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = color;
            sr.sortingOrder = sortOrder;
            go.transform.localScale = new Vector3(CellSize * 0.7f, CellSize * 0.7f, 1);

            _markers[pos] = go;
        }

        /// <summary>更新骑士标记位置</summary>
        public void UpdateKnightMarkers()
        {
            var director = MedievalGameDirector.Instance;
            if (director == null) return;

            foreach (var k in director.Knights)
            {
                if (k.IsAlive)
                {
                    string key = $"knight_{k.Name}";
                    var markerPos = k.Position;
                    MarkPosition(markerPos, GetKnightIcon(k.Name), k.Color, 10);
                }
            }
        }

        Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
