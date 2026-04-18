// SandTable2D.cs — 2D 网格沙盘标注系统
// 16×12 网格沙盘，支持右键标注、拖拽移动标记
// 使用 SpriteRenderer 渲染（不依赖 TextMeshPro / Canvas）
// namespace: SWO1.UI
using UnityEngine;
using System;
using System.Collections.Generic;
using SWO1.Map;

namespace SWO1.UI
{
    #region 数据定义

    public enum MarkerType { Friendly, Enemy, Unknown, Note, Forest, House }

    [Serializable]
    public class MapMarker
    {
        public string Id;
        public int GridX;
        public int GridY;
        public MarkerType Type;
        public string Text;     // 便签内容
        public Color Color;
    }

    #endregion

    /// <summary>
    /// 2D 网格沙盘 — 地形渲染 + 玩家标注
    /// 
    /// 坐标系：GridX 0=A … 15=P, GridY 0=第1行(底部) … 11=第12行
    /// 每格 0.32 世界单位，总尺寸 5.12 × 3.84
    /// 
    /// 右键点击格子弹出标注菜单（纯 GameObject 叠加，无 Canvas）
    /// 标记支持拖拽移动
    /// </summary>
    public class SandTable2D : MonoBehaviour
    {
        public static SandTable2D Instance { get; private set; }

        #region 常量

        public const int GridWidth = 32;
        public const int GridHeight = 24;
        public const float CellSize = 1.0f;
        public const float GridOffsetX = 24f;  // 沙盘右移偏移量，为左侧面板留空间
        public const float MapWidthUnits = GridWidth * CellSize;
        public const float MapHeightUnits = GridHeight * CellSize;

        private static readonly Color GridLineColor = new Color(0.2f, 0.2f, 0.2f, 0.3f); // #333333 alpha 0.3

        // 地形颜色
        private static readonly Color ColorOpenGround = HexColor("8B9B6B");
        private static readonly Color ColorRiver      = HexColor("4A7AB5");
        private static readonly Color ColorForest     = HexColor("3D5C3A");
        private static readonly Color ColorVillage    = HexColor("8B7D6B");
        private static readonly Color ColorBridge     = HexColor("A0522D");
        private static readonly Color ColorRoad       = HexColor("C4A35A");
        private static readonly Color ColorBeach      = HexColor("D4C4A0");
        private static readonly Color ColorDefault    = HexColor("8B9B6B");

        // 标记颜色
        private static readonly Color MarkerFriendly = new Color(0.2f, 0.4f, 1f, 1f);     // 蓝
        private static readonly Color MarkerEnemy    = new Color(1f, 0.2f, 0.2f, 1f);      // 红
        private static readonly Color MarkerUnknown  = new Color(1f, 0.85f, 0.1f, 1f);     // 黄
        private static readonly Color MarkerNote     = new Color(0.95f, 0.95f, 0.8f, 1f);  // 便签黄
        private static readonly Color MarkerForest   = new Color(0.15f, 0.6f, 0.15f, 1f);   // 森林绿
        private static readonly Color MarkerHouse    = new Color(0.7f, 0.45f, 0.2f, 1f);     // 房屋棕

        // 排序层
        private const int TerrainSortOrder = 0;
        private const int GridLineSortOrder = 1;
        private const int IconSortOrder = 5;
        private const int MarkerSortOrder = 10;
        private const int MenuSortOrder = 20;

        #endregion

        #region 序列化字段

        [Header("排序层名称")]
        [SerializeField] private string sortingLayerName = "Default";

        #endregion

        #region 内部状态

        // 渲染层级
        private Transform gridLayer;
        private Transform terrainLayer;
        private Transform markerLayer;
        private Transform menuLayer;

        // 白色 1×1 Sprite（程序化生成）
        private Sprite whitePixel;

        // 标记数据
        private Dictionary<string, MapMarker> markers = new Dictionary<string, MapMarker>();
        private Dictionary<string, GameObject> markerObjects = new Dictionary<string, GameObject>();
        private int markerIdCounter = 0;

        // 右键菜单
        private GameObject contextMenu;
        private Vector2Int menuGridPos;
        private bool menuOpen = false;

        // 拖拽状态
        private bool isDragging = false;
        private string dragMarkerId = null;
        private Vector3 dragOffset;

        // 输入框状态
        private bool isInputActive = false;
        private string inputText = "";
        private Rect inputRect;
        private Vector2Int inputGridPos;

        // 当前地图
        private GameMap currentMap;

        #endregion

        #region 事件

        public event Action<MapMarker> OnMarkerPlaced;
        public event Action<string> OnMarkerRemoved;

        #endregion

        #region Unity 生命周期

        void Awake()
        {
            Instance = this;
            whitePixel = CreateWhiteSprite();
        }

        void Start()
        {
            BuildLayerHierarchy();
            // 网格线和坐标由 SetupScene2D 创建，此处不重复绘制
            // DrawGrid();
            // DrawCoordinateLabels();

            // === 自动右移沙盘，为左侧面板留出空间 ===
            float targetX = GridOffsetX;
            if (Mathf.Abs(transform.position.x - targetX) > 0.1f)
            {
                transform.position = new Vector3(targetX, 0f, 0f);
                Debug.Log($"[SandTable2D] 已设置位置 X={targetX}");
            }

            // 同时右移 GridMap（SetupScene2D 创建的网格）
            var gridMap = GameObject.Find("GridMap");
            if (gridMap != null && Mathf.Abs(gridMap.transform.position.x - targetX) > 0.1f)
            {
                gridMap.transform.position = new Vector3(targetX, 0f, 0f);
                Debug.Log($"[SandTable2D] GridMap 位置已设为 X={targetX}");
            }

            // 同时设置摄像机
            if (Camera.main != null)
            {
                var camCtrl = Camera.main.GetComponent<SWO1.CommandPost.CameraController2D>();
                if (camCtrl != null)
                {
                    camCtrl.MinBounds = new Vector2(-2f + targetX, -2f);
                    camCtrl.MaxBounds = new Vector2(34f + targetX, 26f);
                }
                Camera.main.transform.position = new Vector3(16f + targetX, 12f, -10f);
            }
        }

        void Update()
        {
            HandleMouseInput();
        }

        void OnGUI()
        {
            if (isInputActive)
            {
                DrawTextInput();
            }
        }

        #endregion

        #region 初始化

        private Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private void BuildLayerHierarchy()
        {
            gridLayer    = CreateLayer("GridLayer");
            terrainLayer = CreateLayer("TerrainLayer");
            markerLayer  = CreateLayer("MarkerLayer");
            menuLayer    = CreateLayer("MenuLayer");
        }

        private Transform CreateLayer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        #endregion

        #region 网格绘制

        /// <summary>绘制 16×12 网格线</summary>
        private void DrawGrid()
        {
            // 竖线 (33 条, x = 0..32)
            for (int x = 0; x <= GridWidth; x++)
            {
                float worldX = x * CellSize;
                CreateGridLine($"VLine_{x}",
                    new Vector3(worldX, 0, 0),
                    new Vector3(worldX, MapHeightUnits, 0));
            }

            // 横线 (25 条, y = 0..24)
            for (int y = 0; y <= GridHeight; y++)
            {
                float worldY = y * CellSize;
                CreateGridLine($"HLine_{y}",
                    new Vector3(0, worldY, 0),
                    new Vector3(MapWidthUnits, worldY, 0));
            }
        }

        private void CreateGridLine(string name, Vector3 start, Vector3 end)
        {
            var go = new GameObject(name);
            go.transform.SetParent(gridLayer, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            lr.startWidth = 0.008f;
            lr.endWidth = 0.008f;
            lr.material = CreateLineMaterial();
            lr.startColor = GridLineColor;
            lr.endColor = GridLineColor;
            lr.sortingLayerName = sortingLayerName;
            lr.sortingOrder = GridLineSortOrder;
        }

        private Material CreateLineMaterial()
        {
            // 使用 Sprites/Default 材质，支持透明
            var mat = new Material(Shader.Find("Sprites/Default"));
            return mat;
        }

        /// <summary>绘制坐标标签 (A-P, 1-12) — 用 Sprite 缩放 + 简单形状</summary>
        private void DrawCoordinateLabels()
        {
            // 列标签 (在每格顶部)
            for (int x = 0; x < GridWidth; x++)
            {
                char col = (char)('A' + x);
                float wx = (x + 0.5f) * CellSize + GridOffsetX;
                float wy = MapHeightUnits + 0.08f;
                CreateTextSprite(col.ToString(), new Vector2(wx, wy), 0.08f, GridLineSortOrder + 1);
            }

            // 行标签 (在每格右侧)
            for (int y = 0; y < GridHeight; y++)
            {
                int row = y + 1;
                float wx = MapWidthUnits + GridOffsetX + 0.08f;
                float wy = (y + 0.5f) * CellSize;
                CreateTextSprite(row.ToString(), new Vector2(wx, wy), 0.08f, GridLineSortOrder + 1);
            }
        }

        /// <summary>
        /// 创建文字 Sprite — 使用 TextMesh 渲染坐标
        /// （TextMesh 是 Unity 内置组件，非 TextMeshPro）
        /// </summary>
        private void CreateTextSprite(string text, Vector2 position, float charSize, int sortOrder)
        {
            var go = new GameObject($"Coord_{text}");
            go.transform.SetParent(gridLayer, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 24;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.7f, 0.7f, 0.7f, 0.6f);

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = sortingLayerName;
                mr.sortingOrder = sortOrder;
            }
        }

        #endregion

        #region 地形侦察显示 —— 根据无线电汇报显示地形

        private HashSet<Vector2Int> revealedCells = new HashSet<Vector2Int>();
        private Dictionary<Vector2Int, GameObject> terrainVisuals = new Dictionary<Vector2Int, GameObject>();

        /// <summary>
        /// 显示侦察到的地形（根据无线电汇报）
        /// </summary>
        public void RevealTerrainAt(int gx, int gy, SWO1.Map.TerrainType terrain)
        {
            var pos = new Vector2Int(gx, gy);
            if (revealedCells.Contains(pos)) return; // 已经侦察过了

            revealedCells.Add(pos);

            // 删除旧的地形显示（如果有）
            if (terrainVisuals.TryGetValue(pos, out var oldGo) && oldGo != null)
            {
                Destroy(oldGo);
            }

            // 创建新的地形可视化
            var go = CreateTerrainVisual(gx, gy, terrain);
            terrainVisuals[pos] = go;

            Debug.Log($"[SandTable2D] 侦察地形: {GetCoordinateLabel(gx, gy)} - {TerrainName(terrain)}");
        }

        /// <summary>
        /// 批量显示多个侦察到的地形
        /// </summary>
        public void RevealTerrains(List<(int x, int y, SWO1.Map.TerrainType terrain)> terrains)
        {
            foreach (var t in terrains)
            {
                RevealTerrainAt(t.x, t.y, t.terrain);
            }
        }

        private GameObject CreateTerrainVisual(int gx, int gy, SWO1.Map.TerrainType terrain)
        {
            Vector2 center = GridToWorld(gx, gy);
            string coord = GetCoordinateLabel(gx, gy);

            var parent = new GameObject($"Terrain_{coord}");
            parent.transform.SetParent(terrainLayer, false);
            parent.transform.position = new Vector3(center.x, center.y, 0f);

            // 地形底色
            Color color = GetTerrainColor(terrain);
            CreateTerrainSprite(parent, "Bg", color, Vector2.zero, 
                new Vector2(CellSize - 0.01f, CellSize - 0.01f), TerrainSortOrder);

            // 地形图标
            switch (terrain)
            {
                case SWO1.Map.TerrainType.Forest:
                    CreateForestIcon(parent);
                    break;
                case SWO1.Map.TerrainType.Village:
                    CreateVillageIcon(parent);
                    break;
                case SWO1.Map.TerrainType.Bridge:
                    CreateBridgeIcon(parent);
                    break;
                case SWO1.Map.TerrainType.River:
                    CreateRiverIcon(parent);
                    break;
                case SWO1.Map.TerrainType.Road:
                    CreateRoadIcon(parent);
                    break;
                case SWO1.Map.TerrainType.Beach:
                    CreateBeachIcon(parent);
                    break;
            }

            // 添加侦察标记（小图标表示这是侦察到的）
            CreateReconMarker(parent, coord);

            return parent;
        }

        private void CreateForestIcon(GameObject parent)
        {
            float s = CellSize * 0.40f;
            // 树冠
            CreateTerrainSprite(parent, "TreeTop", new Color(0.15f, 0.5f, 0.15f),
                new Vector2(0, s * 0.3f), new Vector2(s * 1.2f, s * 0.8f), IconSortOrder);
            // 树干
            CreateTerrainSprite(parent, "Trunk", new Color(0.4f, 0.25f, 0.1f),
                new Vector2(0, -s * 0.3f), new Vector2(s * 0.2f, s * 0.5f), IconSortOrder);
        }

        private void CreateVillageIcon(GameObject parent)
        {
            float s = CellSize * 0.38f;
            // 屋顶
            CreateTerrainSprite(parent, "Roof", new Color(0.5f, 0.25f, 0.1f),
                new Vector2(0, s * 0.4f), new Vector2(s * 1.2f, s * 0.4f), IconSortOrder);
            // 墙体
            CreateTerrainSprite(parent, "Wall", new Color(0.7f, 0.6f, 0.5f),
                new Vector2(0, -s * 0.2f), new Vector2(s * 0.8f, s * 0.5f), IconSortOrder);
        }

        private void CreateBridgeIcon(GameObject parent)
        {
            float s = CellSize * 0.45f;
            // 桥面
            CreateTerrainSprite(parent, "Deck", new Color(0.6f, 0.4f, 0.2f),
                Vector2.zero, new Vector2(s * 1.5f, s * 0.4f), IconSortOrder);
            // 桥栏
            CreateTerrainSprite(parent, "Rail1", new Color(0.4f, 0.3f, 0.15f),
                new Vector2(0, s * 0.25f), new Vector2(s * 1.5f, s * 0.08f), IconSortOrder + 1);
            CreateTerrainSprite(parent, "Rail2", new Color(0.4f, 0.3f, 0.15f),
                new Vector2(0, -s * 0.25f), new Vector2(s * 1.5f, s * 0.08f), IconSortOrder + 1);
        }

        private void CreateRiverIcon(GameObject parent)
        {
            float s = CellSize * 0.50f;
            // 波纹效果（用几个小椭圆表示）
            CreateTerrainSprite(parent, "Wave1", new Color(0.3f, 0.5f, 0.7f, 0.7f),
                new Vector2(-s * 0.3f, s * 0.2f), new Vector2(s * 0.4f, s * 0.15f), IconSortOrder);
            CreateTerrainSprite(parent, "Wave2", new Color(0.3f, 0.5f, 0.7f, 0.7f),
                new Vector2(s * 0.2f, -s * 0.1f), new Vector2(s * 0.5f, s * 0.15f), IconSortOrder);
        }

        private void CreateRoadIcon(GameObject parent)
        {
            float s = CellSize * 0.50f;
            CreateTerrainSprite(parent, "Road", new Color(0.7f, 0.6f, 0.4f),
                Vector2.zero, new Vector2(s * 1.4f, s * 0.4f), IconSortOrder);
        }

        private void CreateBeachIcon(GameObject parent)
        {
            float s = CellSize * 0.50f;
            CreateTerrainSprite(parent, "Sand", new Color(0.85f, 0.75f, 0.5f),
                Vector2.zero, new Vector2(s * 1.4f, s * 1.2f), IconSortOrder);
        }

        private void CreateReconMarker(GameObject parent, string coord)
        {
            // 右上角小标记表示这是侦察到的
            float s = CellSize * 0.15f;
            CreateTerrainSprite(parent, "ReconMark", new Color(1f, 0.8f, 0.2f, 0.8f),
                new Vector2(CellSize * 0.63f, CellSize * 0.63f), 
                new Vector2(s, s), IconSortOrder + 5);
        }

        private GameObject CreateTerrainSprite(GameObject parent, string name, Color color,
            Vector2 localPos, Vector2 size, int sortOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.01f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = color;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortOrder;

            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        private string GetCoordinateLabel(int gx, int gy)
        {
            char col = (char)('A' + Mathf.Clamp(gx, 0, 25));
            return $"{col}{gy + 1}";
        }

        private string TerrainName(SWO1.Map.TerrainType terrain)
        {
            return terrain switch
            {
                SWO1.Map.TerrainType.Forest => "树林",
                SWO1.Map.TerrainType.Village => "村庄",
                SWO1.Map.TerrainType.Bridge => "桥梁",
                SWO1.Map.TerrainType.River => "河流",
                SWO1.Map.TerrainType.Road => "道路",
                SWO1.Map.TerrainType.Beach => "海滩",
                _ => "开阔地"
            };
        }

        // GetTerrainColor 方法在下面 #region 地形渲染 中定义

        #endregion

        #region 地形渲染

        /// <summary>渲染地图地形</summary>
        public void RenderMap(GameMap map)
        {
            currentMap = map;
            if (map?.Cells == null) return;

            // 清除旧地形
            for (int i = terrainLayer.childCount - 1; i >= 0; i--)
            {
                Destroy(terrainLayer.GetChild(i).gameObject);
            }

            for (int x = 0; x < GridWidth && x < map.Cells.GetLength(0); x++)
            {
                for (int y = 0; y < GridHeight && y < map.Cells.GetLength(1); y++)
                {
                    var cell = map.Cells[x, y];
                    if (cell == null) continue;
                    RenderCell(cell);
                }
            }
        }

        private void RenderCell(MapCell cell)
        {
            Color color = GetTerrainColor(cell.Terrain);
            Vector2 center = new Vector2((cell.X + 0.5f) * CellSize, (cell.Y + 0.5f) * CellSize);

            // 地形底色
            CreateSprite($"Terrain_{cell.X}_{cell.Y}", color, center,
                new Vector2(CellSize - 0.005f, CellSize - 0.005f),
                terrainLayer, TerrainSortOrder, 1f);

            // 地形图标
            switch (cell.Terrain)
            {
                case TerrainType.Forest:
                    DrawTreeIcon(center);
                    break;
                case TerrainType.Village:
                    DrawHouseIcon(center);
                    break;
                case TerrainType.Bridge:
                    DrawBridgeIcon(center);
                    break;
            }
        }

        private Color GetTerrainColor(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.OpenGround: return ColorOpenGround;
                case TerrainType.River:      return ColorRiver;
                case TerrainType.Forest:     return ColorForest;
                case TerrainType.Village:    return ColorVillage;
                case TerrainType.Bridge:     return ColorBridge;
                case TerrainType.Road:       return ColorRoad;
                case TerrainType.Beach:      return ColorBeach;
                default:                     return ColorDefault;
            }
        }

        #region 地形图标

        /// <summary>画小树图标 — 三角形树冠 + 树干</summary>
        private void DrawTreeIcon(Vector2 center)
        {
            float s = CellSize * 0.45f;

            // 树冠 (三角形用 3 个小方块模拟)
            CreateSprite("TreeTop", new Color(0.15f, 0.5f, 0.15f, 0.9f),
                center + new Vector2(0, s * 0.3f),
                new Vector2(s * 1.2f, s * 0.6f),
                terrainLayer, IconSortOrder, 1f);

            CreateSprite("TreeTop2", new Color(0.1f, 0.4f, 0.1f, 0.9f),
                center + new Vector2(0, s * 0.7f),
                new Vector2(s * 0.8f, s * 0.4f),
                terrainLayer, IconSortOrder, 1f);

            // 树干
            CreateSprite("TreeTrunk", new Color(0.4f, 0.25f, 0.1f, 0.9f),
                center + new Vector2(0, -s * 0.4f),
                new Vector2(s * 0.2f, s * 0.4f),
                terrainLayer, IconSortOrder, 1f);
        }

        /// <summary>画小房子图标</summary>
        private void DrawHouseIcon(Vector2 center)
        {
            float s = CellSize * 0.40f;

            // 屋顶 (三角形用方块)
            CreateSprite("Roof", new Color(0.5f, 0.25f, 0.1f, 0.9f),
                center + new Vector2(0, s * 0.5f),
                new Vector2(s * 1.4f, s * 0.4f),
                terrainLayer, IconSortOrder, 1f);

            // 墙体
            CreateSprite("Wall", new Color(0.7f, 0.6f, 0.5f, 0.9f),
                center + new Vector2(0, -s * 0.1f),
                new Vector2(s * 1.0f, s * 0.6f),
                terrainLayer, IconSortOrder, 1f);
        }

        /// <summary>画桥图标</summary>
        private void DrawBridgeIcon(Vector2 center)
        {
            float s = CellSize * 0.45f;

            // 桥面
            CreateSprite("BridgePlank", new Color(0.6f, 0.35f, 0.1f, 0.9f),
                center,
                new Vector2(s * 1.6f, s * 0.3f),
                terrainLayer, IconSortOrder, 1f);

            // 桥栏
            CreateSprite("BridgeRail1", new Color(0.5f, 0.3f, 0.1f, 0.9f),
                center + new Vector2(0, s * 0.35f),
                new Vector2(s * 1.6f, s * 0.08f),
                terrainLayer, IconSortOrder + 1, 1f);

            CreateSprite("BridgeRail2", new Color(0.5f, 0.3f, 0.1f, 0.9f),
                center + new Vector2(0, -s * 0.35f),
                new Vector2(s * 1.6f, s * 0.08f),
                terrainLayer, IconSortOrder + 1, 1f);
        }

        #endregion

        #endregion

        #region Sprite 工厂

        private GameObject CreateSprite(string name, Color color, Vector2 position, Vector2 size,
            Transform parent, int sortOrder, float alpha = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = new Color(color.r, color.g, color.b, color.a * alpha);
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortOrder;

            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        #endregion

        #region 标记系统 — 公共 API

        /// <summary>添加标记</summary>
        public void AddMarker(MapMarker marker)
        {
            if (marker == null) return;

            if (string.IsNullOrEmpty(marker.Id))
            {
                marker.Id = $"marker_{markerIdCounter++}";
            }

            // 默认颜色
            if (marker.Color == default)
            {
                marker.Color = GetMarkerColor(marker.Type);
            }

            markers[marker.Id] = marker;
            CreateMarkerVisual(marker);
            OnMarkerPlaced?.Invoke(marker);
        }

        /// <summary>移除标记</summary>
        public void RemoveMarker(string markerId)
        {
            if (markers.ContainsKey(markerId))
            {
                markers.Remove(markerId);
                if (markerObjects.TryGetValue(markerId, out var go))
                {
                    Destroy(go);
                    markerObjects.Remove(markerId);
                }
                OnMarkerRemoved?.Invoke(markerId);
            }
        }

        /// <summary>清空所有标记</summary>
        public void ClearAllMarkers()
        {
            var ids = new List<string>(markers.Keys);
            foreach (var id in ids)
            {
                RemoveMarker(id);
            }
        }

        /// <summary>获取所有标记</summary>
        public List<MapMarker> GetAllMarkers()
        {
            return new List<MapMarker>(markers.Values);
        }

        /// <summary>屏幕坐标转网格坐标</summary>
        public Vector2Int ScreenToGrid(Vector2 screenPos)
        {
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -Camera.main.transform.position.z));
            float localX = worldPos.x - transform.position.x;
            float localY = worldPos.y - transform.position.y;
            int gx = Mathf.FloorToInt(localX / CellSize);
            int gy = Mathf.FloorToInt(localY / CellSize);
            gx = Mathf.Clamp(gx, 0, GridWidth - 1);
            gy = Mathf.Clamp(gy, 0, GridHeight - 1);
            return new Vector2Int(gx, gy);
        }

        /// <summary>网格坐标转世界坐标（格子中心），包含沙盘偏移量</summary>
        public Vector2 GridToWorld(int x, int y)
        {
            return new Vector2(
                (x + 0.5f) * CellSize + transform.position.x,
                (y + 0.5f) * CellSize + transform.position.y);
        }

        #endregion

        #region 标记可视化

        private Color GetMarkerColor(MarkerType type)
        {
            switch (type)
            {
                case MarkerType.Friendly: return MarkerFriendly;
                case MarkerType.Enemy:    return MarkerEnemy;
                case MarkerType.Unknown:  return MarkerUnknown;
                case MarkerType.Note:     return MarkerNote;
                case MarkerType.Forest:   return MarkerForest;
                case MarkerType.House:    return MarkerHouse;
                default:                  return MarkerUnknown;
            }
        }

        private void CreateMarkerVisual(MapMarker marker)
        {
            // 如果已有旧的，先删除
            if (markerObjects.TryGetValue(marker.Id, out var oldGo))
            {
                Destroy(oldGo);
            }

            Vector2 worldPos = GridToWorld(marker.GridX, marker.GridY);
            float s = CellSize * 0.50f;

            var go = new GameObject($"Marker_{marker.Id}");
            go.transform.SetParent(markerLayer, false);
            go.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            if (marker.Type == MarkerType.Note)
            {
                // 便签：背景方块
                var bg = CreateChildSprite(go, "NoteBg", MarkerNote,
                    Vector2.zero, new Vector2(CellSize * 0.70f, CellSize * 0.70f), MarkerSortOrder);

                // 文字
                CreateChildText(go, "NoteText",
                    string.IsNullOrEmpty(marker.Text) ? "?" : marker.Text,
                    Vector2.zero, 0.035f, MarkerSortOrder + 1);
            }
            else
            {
                // 图钉：三角形（Friendly 向上，Enemy 向下）或方块（Unknown）
                switch (marker.Type)
                {
                    case MarkerType.Friendly:
                        // 向上三角形 — 用拉伸的方块模拟
                        CreateChildSprite(go, "Pin", marker.Color,
                            Vector2.zero, new Vector2(s * 0.8f, s * 1.2f), MarkerSortOrder);
                        // 尖端
                        CreateChildSprite(go, "PinTip", marker.Color,
                            new Vector2(0, -s * 0.8f), new Vector2(s * 0.4f, s * 0.4f), MarkerSortOrder);
                        break;

                    case MarkerType.Enemy:
                        // 向下三角形
                        CreateChildSprite(go, "Pin", marker.Color,
                            Vector2.zero, new Vector2(s * 0.8f, s * 1.2f), MarkerSortOrder);
                        CreateChildSprite(go, "PinTip", marker.Color,
                            new Vector2(0, -s * 0.8f), new Vector2(s * 0.4f, s * 0.4f), MarkerSortOrder);
                        break;

                    case MarkerType.Unknown:
                        // 圆形（用方块近似）
                        CreateChildSprite(go, "Pin", marker.Color,
                            Vector2.zero, new Vector2(s * 1.0f, s * 1.0f), MarkerSortOrder);
                        // 问号
                        CreateChildText(go, "Question", "?",
                            Vector2.zero, 0.03f, MarkerSortOrder + 1);
                        break;

                    case MarkerType.Forest:
                        // 森林标记：绿色三角树冠
                        CreateChildSprite(go, "TreeTop", marker.Color,
                            new Vector2(0, s * 0.3f), new Vector2(s * 1.2f, s * 0.8f), MarkerSortOrder);
                        CreateChildSprite(go, "TreeTop2", new Color(0.1f, 0.45f, 0.1f),
                            new Vector2(0, s * 0.7f), new Vector2(s * 0.8f, s * 0.5f), MarkerSortOrder);
                        CreateChildSprite(go, "Trunk", new Color(0.4f, 0.25f, 0.1f),
                            new Vector2(0, -s * 0.3f), new Vector2(s * 0.15f, s * 0.4f), MarkerSortOrder);
                        break;

                    case MarkerType.House:
                        // 房屋标记：棕色屋顶 + 墙体
                        CreateChildSprite(go, "Roof", marker.Color,
                            new Vector2(0, s * 0.4f), new Vector2(s * 1.4f, s * 0.5f), MarkerSortOrder);
                        CreateChildSprite(go, "Wall", new Color(0.8f, 0.7f, 0.55f),
                            new Vector2(0, -s * 0.1f), new Vector2(s * 1.0f, s * 0.6f), MarkerSortOrder);
                        break;
                }
            }

            markerObjects[marker.Id] = go;
        }

        private GameObject CreateChildSprite(GameObject parent, string name, Color color,
            Vector2 localPos, Vector2 size, int sortOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.01f);

            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = color;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortOrder;

            child.transform.localScale = new Vector3(size.x, size.y, 1f);
            return child;
        }

        private void CreateChildText(GameObject parent, string name, string text,
            Vector2 localPos, float charSize, int sortOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(localPos.x, localPos.y, -0.02f);

            var tm = child.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 24;
            tm.characterSize = charSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.black;

            var mr = child.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = sortingLayerName;
                mr.sortingOrder = sortOrder;
            }
        }

        #endregion

        #region 鼠标输入处理

        private void HandleMouseInput()
        {
            // 拖拽中
            if (isDragging)
            {
                HandleDragging();
                return;
            }

            // 右键 — 弹出菜单
            if (Input.GetMouseButtonDown(1))
            {
                Vector2Int grid = ScreenToGrid(Input.mousePosition);
                ShowContextMenu(grid);
            }
            // 左键 — 关闭菜单 / 开始拖拽
            else if (Input.GetMouseButtonDown(0))
            {
                if (menuOpen)
                {
                    // 检查是否点击了菜单项
                    if (!IsMouseOverContextMenu())
                    {
                        CloseContextMenu();
                    }
                }
                else
                {
                    // 尝试拖拽标记
                    TryStartDrag();
                }
            }
            // 中键 — 关闭菜单
            else if (Input.GetMouseButtonDown(2))
            {
                if (menuOpen) CloseContextMenu();
            }
        }

        #endregion

        #region 右键菜单（纯 GameObject 实现）

        private void ShowContextMenu(Vector2Int gridPos)
        {
            CloseContextMenu();
            menuGridPos = gridPos;

            contextMenu = new GameObject("ContextMenu");
            contextMenu.transform.SetParent(menuLayer, false);

            Vector2 worldPos = GridToWorld(gridPos.x, gridPos.y);
            contextMenu.transform.position = new Vector3(worldPos.x, worldPos.y, 0f);

            float itemHeight = CellSize * 0.65f;
            float menuWidth = CellSize * 2.0f;
            string[] labels = { "我方标记", "敌方标记", "不确定", "🌲 森林", "🏠 房屋", "文字便签", "删除标记" };
            Color[] colors = { MarkerFriendly, MarkerEnemy, MarkerUnknown, MarkerForest, MarkerHouse, MarkerNote, new Color(0.5f, 0.5f, 0.5f) };

            // 背景
            float totalHeight = labels.Length * itemHeight + 0.04f;
            CreateChildSprite(contextMenu, "MenuBg",
                new Color(0.15f, 0.15f, 0.15f, 0.9f),
                new Vector2(menuWidth * 0.5f + 0.02f, -totalHeight * 0.5f + itemHeight * 0.5f),
                new Vector2(menuWidth + 0.04f, totalHeight),
                MenuSortOrder);

            for (int i = 0; i < labels.Length; i++)
            {
                float y = -i * itemHeight;

                // 菜单项背景
                var item = CreateChildSprite(contextMenu, $"Item_{i}",
                    new Color(0.25f, 0.25f, 0.25f, 0.95f),
                    new Vector2(menuWidth * 0.5f + 0.02f, y),
                    new Vector2(menuWidth, itemHeight - 0.01f),
                    MenuSortOrder + 1);

                // 颜色指示器
                if (i < 6)
                {
                    CreateChildSprite(item, "ColorDot", colors[i],
                        new Vector2(-menuWidth * 0.35f, 0),
                        new Vector2(itemHeight * 0.4f, itemHeight * 0.4f),
                        MenuSortOrder + 2);
                }

                // 文字标签
                CreateChildText(item, "Label", labels[i],
                    new Vector2(menuWidth * 0.05f, 0),
                    0.035f, MenuSortOrder + 2);

                // 点击检测用 collider
                var col = item.AddComponent<BoxCollider2D>();
                col.size = new Vector2(menuWidth, itemHeight - 0.01f);

                // 菜单项点击脚本
                var clickHandler = item.AddComponent<MenuItemClick>();
                clickHandler.Index = i;
                clickHandler.OnClick = OnMenuItemClicked;
            }

            menuOpen = true;
        }

        private void OnMenuItemClicked(int index)
        {
            switch (index)
            {
                case 0: // 我方
                    PlaceMarker(menuGridPos, MarkerType.Friendly);
                    break;
                case 1: // 敌方
                    PlaceMarker(menuGridPos, MarkerType.Enemy);
                    break;
                case 2: // 不确定
                    PlaceMarker(menuGridPos, MarkerType.Unknown);
                    break;
                case 3: // 森林
                    PlaceMarker(menuGridPos, MarkerType.Forest);
                    break;
                case 4: // 房屋
                    PlaceMarker(menuGridPos, MarkerType.House);
                    break;
                case 5: // 文字便签
                    OpenTextInput(menuGridPos);
                    break;
                case 6: // 删除
                    DeleteMarkerAt(menuGridPos);
                    break;
            }
            CloseContextMenu();
        }

        private void CloseContextMenu()
        {
            if (contextMenu != null)
            {
                Destroy(contextMenu);
                contextMenu = null;
            }
            menuOpen = false;
        }

        private bool IsMouseOverContextMenu()
        {
            if (contextMenu == null) return false;

            Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var colliders = contextMenu.GetComponentsInChildren<BoxCollider2D>();
            foreach (var col in colliders)
            {
                if (col.OverlapPoint(mouseWorld))
                    return true;
            }
            return false;
        }

        #endregion

        #region 标记操作

        private void PlaceMarker(Vector2Int grid, MarkerType type)
        {
            // 检查该格是否已有标记，有则替换
            string existingId = FindMarkerAt(grid.x, grid.y);
            if (existingId != null)
            {
                RemoveMarker(existingId);
            }

            var marker = new MapMarker
            {
                GridX = grid.x,
                GridY = grid.y,
                Type = type,
                Color = GetMarkerColor(type)
            };
            AddMarker(marker);
        }

        private void DeleteMarkerAt(Vector2Int grid)
        {
            string id = FindMarkerAt(grid.x, grid.y);
            if (id != null)
            {
                RemoveMarker(id);
            }
        }

        private string FindMarkerAt(int x, int y)
        {
            foreach (var kvp in markers)
            {
                if (kvp.Value.GridX == x && kvp.Value.GridY == y)
                    return kvp.Key;
            }
            return null;
        }

        #endregion

        #region 文字便签输入（OnGUI 实现）

        private void OpenTextInput(Vector2Int grid)
        {
            inputGridPos = grid;
            inputText = "";

            // 检查该格是否已有便签，有则加载文字
            string existingId = FindMarkerAt(grid.x, grid.y);
            if (existingId != null && markers[existingId].Type == MarkerType.Note)
            {
                inputText = markers[existingId].Text ?? "";
            }

            // 计算屏幕位置
            Vector2 worldPos = GridToWorld(grid.x, grid.y);
            Vector3 screenPt = Camera.main.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0));
            inputRect = new Rect(screenPt.x + 20, Screen.height - screenPt.y - 20, 200, 60);

            isInputActive = true;
        }

        private void DrawTextInput()
        {
            // 半透明背景
            GUI.Box(inputRect, "");
            GUI.Label(new Rect(inputRect.x + 5, inputRect.y + 5, 190, 20),
                $"便签 ({(char)('A' + inputGridPos.x)}{inputGridPos.y + 1}):");

            GUI.SetNextControlName("NoteInput");
            inputText = GUI.TextField(
                new Rect(inputRect.x + 5, inputRect.y + 25, 190, 25), inputText, 50);

            if (GUI.Button(new Rect(inputRect.x + 5, inputRect.y + 55, 90, 20), "确定") ||
                (Event.current.isKey && Event.current.keyCode == KeyCode.Return &&
                 GUI.GetNameOfFocusedControl() == "NoteInput"))
            {
                ConfirmTextInput();
            }

            if (GUI.Button(new Rect(inputRect.x + 105, inputRect.y + 55, 90, 20), "取消"))
            {
                isInputActive = false;
            }
        }

        private void ConfirmTextInput()
        {
            // 删除旧标记
            string existingId = FindMarkerAt(inputGridPos.x, inputGridPos.y);
            if (existingId != null)
            {
                RemoveMarker(existingId);
            }

            if (!string.IsNullOrEmpty(inputText.Trim()))
            {
                var marker = new MapMarker
                {
                    GridX = inputGridPos.x,
                    GridY = inputGridPos.y,
                    Type = MarkerType.Note,
                    Text = inputText.Trim(),
                    Color = MarkerNote
                };
                AddMarker(marker);
            }

            isInputActive = false;
        }

        #endregion

        #region 拖拽移动

        private void TryStartDrag()
        {
            Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            foreach (var kvp in markerObjects)
            {
                var col = kvp.Value.GetComponentInChildren<BoxCollider2D>();
                if (col == null)
                {
                    // 给标记对象临时加 collider 检测
                    var sr = kvp.Value.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null)
                    {
                        Bounds b = sr.bounds;
                        if (b.Contains(mouseWorld))
                        {
                            StartDrag(kvp.Key, kvp.Value);
                            return;
                        }
                    }
                    continue;
                }

                if (col.OverlapPoint(mouseWorld))
                {
                    StartDrag(kvp.Key, kvp.Value);
                    return;
                }
            }
        }

        private void StartDrag(string markerId, GameObject markerGo)
        {
            isDragging = true;
            dragMarkerId = markerId;
            Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            dragOffset = markerGo.transform.position - new Vector3(mouseWorld.x, mouseWorld.y, 0);
        }

        private void HandleDragging()
        {
            if (Input.GetMouseButtonUp(0))
            {
                // 放下 — 更新网格坐标
                Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                int gx = Mathf.FloorToInt((mouseWorld.x - transform.position.x) / CellSize);
                int gy = Mathf.FloorToInt((mouseWorld.y - transform.position.y) / CellSize);
                gx = Mathf.Clamp(gx, 0, GridWidth - 1);
                gy = Mathf.Clamp(gy, 0, GridHeight - 1);

                if (markers.TryGetValue(dragMarkerId, out var marker))
                {
                    marker.GridX = gx;
                    marker.GridY = gy;
                    CreateMarkerVisual(marker); // 重建视觉（居中）
                }

                isDragging = false;
                dragMarkerId = null;
                return;
            }

            // 跟随鼠标
            if (markers.TryGetValue(dragMarkerId, out var m) &&
                markerObjects.TryGetValue(dragMarkerId, out var go))
            {
                Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                go.transform.position = new Vector3(
                    mouseWorld.x + dragOffset.x,
                    mouseWorld.y + dragOffset.y,
                    go.transform.position.z);
            }
        }

        #endregion

        #region 辅助

        private static Color HexColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color c))
                return c;
            return Color.white;
        }

        #endregion
    }

    #region 菜单项点击检测

    /// <summary>右键菜单项的点击检测组件</summary>
    public class MenuItemClick : MonoBehaviour
    {
        public int Index;
        public Action<int> OnClick;

        void OnMouseOver()
        {
            // 高亮效果
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.35f, 0.35f, 0.4f, 1f);
            }
        }

        void OnMouseExit()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = new Color(0.25f, 0.25f, 0.25f, 0.95f);
            }
        }

        void OnMouseDown()
        {
            if (Input.GetMouseButtonDown(0))
            {
                OnClick?.Invoke(Index);
            }
        }
    }

    #endregion
}
