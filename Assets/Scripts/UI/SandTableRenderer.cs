// SandTableRenderer.cs — 2D 沙盘可视化（SpriteRenderer 版）
// 俯视地图渲染：蓝色=我方，红色=敌方，桥头堡标记，地形要素
// 使用 SpriteRenderer + 协程插值实现部队移动动画
// 命名空间：SWO1.Visualization
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.Intelligence;

namespace SWO1.Visualization
{
    #region 数据定义

    /// <summary>地形类型</summary>
    public enum TerrainType
    {
        OpenGround,
        River,
        Bridge,
        Forest,
        Village,
        Road
    }

    /// <summary>地形区域定义（归一化坐标 0-1）</summary>
    [Serializable]
    public class TerrainRegion
    {
        public TerrainType Type;
        public Rect Area;
        public string Label;
    }

    /// <summary>沙盘单位标记</summary>
    [Serializable]
    public class SandTableMarker
    {
        public string MarkerId;
        public string UnitId;
        public Vector2 Position;            // 当前渲染位置（沙盘世界坐标）
        public Vector2 PreviousPosition;    // 上一帧位置（用于移动动画起点）
        public Vector2 TargetPosition;      // 移动目标位置
        public bool IsEnemy;
        public bool IsBridgehead;
        public bool IsLostContact;
        public float LastUpdateTime;
        public float Confidence;
        public int TroopCount;
        public float Morale;
    }

    #endregion

    /// <summary>
    /// 2D 沙盘渲染器 — SpriteRenderer 实现
    /// 
    /// 功能：
    /// - 俯视地图渲染（河流/森林/道路/桥梁/村庄）
    /// - 蓝色点=我方，红色点=敌方，黄色点=桥头堡
    /// - 部队平滑移动动画（Lerp 插值）
    /// - 事件驱动：OnBattlefieldUpdated / OnUnitPositionChanged
    /// 
    /// 技术：
    /// - SpriteRenderer 分层渲染（地形层/道路层/标记层）
    /// - 程序化生成白色 Sprite，着色区分
    /// - 协程驱动移动动画
    /// </summary>
    public class SandTableRenderer : MonoBehaviour
    {
        public static SandTableRenderer Instance { get; private set; }

        #region 配置

        [Header("沙盘范围（世界坐标）")]
        [SerializeField] private float mapMinX = -10f;
        [SerializeField] private float mapMaxX = 10f;
        [SerializeField] private float mapMinY = -10f;
        [SerializeField] private float mapMaxY = 10f;

        [Header("标记尺寸")]
        [SerializeField] private float markerSize = 0.4f;
        [SerializeField] private float bridgeheadSize = 0.6f;
        [SerializeField] private float labelScale = 0.15f;

        [Header("移动动画")]
        [SerializeField] private float moveSpeed = 3f;          // 沙盘单位/秒
        [SerializeField] private AnimationCurve moveEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("新鲜度阈值（秒）")]
        [SerializeField] private float freshThreshold = 30f;
        [SerializeField] private float staleThreshold = 90f;
        [SerializeField] private float expiredThreshold = 180f;

        [Header("颜色")]
        [SerializeField] private Color friendlyColor = new Color(0.2f, 0.5f, 1f);       // 蓝色
        [SerializeField] private Color enemyColor = new Color(1f, 0.2f, 0.2f);           // 红色
        [SerializeField] private Color bridgeheadColor = new Color(1f, 0.9f, 0.1f);      // 黄色
        [SerializeField] private Color lostContactColor = new Color(0.5f, 0.5f, 0.5f);   // 灰色
        [SerializeField] private Color riverColor = new Color(0.2f, 0.4f, 0.8f);
        [SerializeField] private Color bridgeColor = new Color(0.9f, 0.8f, 0.3f);
        [SerializeField] private Color forestColor = new Color(0.1f, 0.45f, 0.15f);
        [SerializeField] private Color villageColor = new Color(0.6f, 0.4f, 0.2f);
        [SerializeField] private Color roadColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color groundColor = new Color(0.25f, 0.28f, 0.2f);

        [Header("排序层")]
        [SerializeField] private string terrainSortingLayer = "Default";
        [SerializeField] private int terrainOrder = 0;
        [SerializeField] private int roadOrder = 5;
        [SerializeField] private int markerOrder = 10;
        [SerializeField] private int labelOrder = 15;

        #endregion

        #region 内部状态

        // 渲染层级
        private Transform terrainLayer;
        private Transform roadLayer;
        private Transform markerLayer;

        // 标记追踪
        private Dictionary<string, Transform> markerObjects = new Dictionary<string, Transform>();
        private Dictionary<string, Coroutine> moveCoroutines = new Dictionary<string, Coroutine>();
        private List<SandTableMarker> markers = new List<SandTableMarker>();

        // 地形区域
        private List<TerrainRegion> terrainRegions = new List<TerrainRegion>();

        // 1x1 白色 Sprite（程序化生成）
        private Sprite whitePixel;

        private bool initialized = false;

        #endregion

        #region Unity 生命周期

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            whitePixel = CreateWhiteSprite();
            InitializeTerrainRegions();
            BuildLayerHierarchy();
            DrawTerrain();
            DrawRoads();
            PlaceInitialMarkers();
            initialized = true;
            Debug.Log("[SandTable] 初始化完成 — SpriteRenderer 沙盘就绪");
        }

        void OnEnable()
        {
            SubscribeEvents();
        }

        void OnDisable()
        {
            UnsubscribeEvents();
        }

        void Update()
        {
            if (!initialized) return;
            RefreshMarkerVisuals();
        }

        #endregion

        #region 初始化

        /// <summary>生成 1x1 白色 Sprite 用于程序化着色</summary>
        private Sprite CreateWhiteSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>定义地形区域（归一化 0-1 坐标）</summary>
        private void InitializeTerrainRegions()
        {
            terrainRegions.Clear();

            terrainRegions.Add(new TerrainRegion
            {
                Type = TerrainType.River,
                Area = new Rect(0.05f, 0.52f, 0.9f, 0.08f),
                Label = "河流"
            });

            terrainRegions.Add(new TerrainRegion
            {
                Type = TerrainType.Bridge,
                Area = new Rect(0.43f, 0.50f, 0.14f, 0.12f),
                Label = "★ 桥梁"
            });

            terrainRegions.Add(new TerrainRegion
            {
                Type = TerrainType.Forest,
                Area = new Rect(0.08f, 0.65f, 0.30f, 0.25f),
                Label = "森林"
            });

            terrainRegions.Add(new TerrainRegion
            {
                Type = TerrainType.Village,
                Area = new Rect(0.60f, 0.70f, 0.22f, 0.18f),
                Label = "村庄"
            });

            terrainRegions.Add(new TerrainRegion
            {
                Type = TerrainType.Forest,
                Area = new Rect(0.05f, 0.25f, 0.18f, 0.20f),
                Label = ""
            });

            terrainRegions.Add(new TerrainRegion
            {
                Type = TerrainType.OpenGround,
                Area = new Rect(0.30f, 0.62f, 0.40f, 0.30f),
                Label = ""
            });
        }

        /// <summary>创建渲染层级</summary>
        private void BuildLayerHierarchy()
        {
            terrainLayer = CreateLayer("TerrainLayer");
            roadLayer = CreateLayer("RoadLayer");
            markerLayer = CreateLayer("MarkerLayer");
        }

        private Transform CreateLayer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        #endregion

        #region 地形绘制

        private void DrawTerrain()
        {
            // 背景
            CreateSprite("Ground", groundColor, Vector2.zero,
                new Vector2(mapMaxX - mapMinX, mapMaxY - mapMinY), terrainLayer, terrainOrder);

            foreach (var region in terrainRegions)
            {
                Vector2 center = NormalizedToWorld(region.Area.center);
                Vector2 size = NormalizedToWorldSize(region.Area.size);

                switch (region.Type)
                {
                    case TerrainType.River:
                        CreateSprite("River", riverColor, center, size, terrainLayer, terrainOrder);
                        // 波纹装饰条
                        for (int i = 0; i < 5; i++)
                        {
                            float wx = Mathf.Lerp(center.x - size.x * 0.4f, center.x + size.x * 0.4f, (i + 0.5f) / 5f);
                            CreateSprite($"Wave_{i}", new Color(0.3f, 0.55f, 0.9f, 0.5f),
                                new Vector2(wx, center.y), new Vector2(0.3f, size.y * 0.6f),
                                terrainLayer, terrainOrder + 1);
                        }
                        break;

                    case TerrainType.Bridge:
                        CreateSprite("Bridge", bridgeColor, center, size, terrainLayer, terrainOrder + 1);
                        // 桥梁脉冲高亮
                        var bridgeGo = CreateSprite("BridgePulse", bridgeColor, center, size * 1.2f, terrainLayer, terrainOrder);
                        bridgeGo.AddComponent<BridgeHighlightSprite>();
                        break;

                    case TerrainType.Forest:
                        CreateSprite("Forest", forestColor, center, size, terrainLayer, terrainOrder);
                        // 树冠散布
                        int trees = Mathf.Max(8, Mathf.FloorToInt(region.Area.width * region.Area.height * 200));
                        var rng = new System.Random(42);
                        for (int i = 0; i < trees; i++)
                        {
                            float tx = (float)(rng.NextDouble() - 0.5) * size.x * 0.9f;
                            float ty = (float)(rng.NextDouble() - 0.5) * size.y * 0.9f;
                            float ts = 0.15f + (float)rng.NextDouble() * 0.15f;
                            CreateCircle($"Tree_{i}", new Color(0.05f, 0.35f, 0.1f, 0.8f),
                                center + new Vector2(tx, ty), ts, terrainLayer, terrainOrder + 1);
                        }
                        break;

                    case TerrainType.Village:
                        CreateSprite("Village", villageColor, center, size, terrainLayer, terrainOrder);
                        // 建筑散布
                        var buildRng = new System.Random(99);
                        for (int i = 0; i < 6; i++)
                        {
                            float bx = (float)(buildRng.NextDouble() - 0.5) * size.x * 0.8f;
                            float by = (float)(buildRng.NextDouble() - 0.5) * size.y * 0.8f;
                            float bw = 0.1f + (float)buildRng.NextDouble() * 0.1f;
                            CreateSprite($"Building_{i}", new Color(0.5f, 0.3f, 0.15f, 0.9f),
                                center + new Vector2(bx, by), new Vector2(bw, bw),
                                terrainLayer, terrainOrder + 1);
                        }
                        break;
                }

                // 标签（用 TextMesh）
                if (!string.IsNullOrEmpty(region.Label))
                {
                    CreateTextMesh(region.Label, center + Vector2.up * (size.y * 0.5f + 0.3f), 0.3f, labelOrder);
                }
            }
        }

        /// <summary>绘制道路</summary>
        private void DrawRoads()
        {
            DrawRoad(NormalizedToWorld(new Vector2(0.50f, 0.05f)), NormalizedToWorld(new Vector2(0.50f, 0.50f)), 0.15f);
            DrawRoad(NormalizedToWorld(new Vector2(0.50f, 0.56f)), NormalizedToWorld(new Vector2(0.25f, 0.65f)), 0.1f);
            DrawRoad(NormalizedToWorld(new Vector2(0.50f, 0.56f)), NormalizedToWorld(new Vector2(0.70f, 0.72f)), 0.1f);
            DrawRoad(NormalizedToWorld(new Vector2(0.15f, 0.40f)), NormalizedToWorld(new Vector2(0.45f, 0.50f)), 0.1f);

            CreateTextMesh("移动+50%", NormalizedToWorld(new Vector2(0.52f, 0.28f)), 0.2f, labelOrder);
        }

        private void DrawRoad(Vector2 start, Vector2 end, float width)
        {
            Vector2 diff = end - start;
            float length = diff.magnitude;
            Vector2 center = (start + end) * 0.5f;

            var go = CreateSprite("Road", roadColor, center, new Vector2(length, width), roadLayer, roadOrder);
            float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        #endregion

        #region Sprite 工厂方法

        /// <summary>创建矩形 Sprite</summary>
        private GameObject CreateSprite(string name, Color color, Vector2 position, Vector2 size,
            Transform parent, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = color;
            sr.sortingLayerName = terrainSortingLayer;
            sr.sortingOrder = sortingOrder;

            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        /// <summary>创建圆形标记（用 Sprite 缩放模拟）</summary>
        private GameObject CreateCircle(string name, Color color, Vector2 position, float radius,
            Transform parent, int sortingOrder)
        {
            return CreateSprite(name, color, position, new Vector2(radius * 2f, radius * 2f), parent, sortingOrder);
        }

        /// <summary>创建文字标签（TextMesh）</summary>
        private void CreateTextMesh(string text, Vector2 position, float size, int sortingOrder)
        {
            var go = new GameObject($"Label_{text}");
            go.transform.SetParent(terrainLayer, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 32;
            tm.characterSize = size;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = terrainSortingLayer;
                mr.sortingOrder = sortingOrder;
            }
        }

        #endregion

        #region 单位标记管理

        /// <summary>放置初始标记</summary>
        private void PlaceInitialMarkers()
        {
            // 桥头堡
            AddOrUpdateMarker(new SandTableMarker
            {
                MarkerId = "bridgehead",
                UnitId = "bridgehead",
                Position = NormalizedToWorld(new Vector2(0.50f, 0.56f)),
                IsBridgehead = true,
                IsEnemy = false,
                LastUpdateTime = 0f,
                Confidence = 1f
            });

            // 我方 4 个排
            AddOrUpdateMarker(new SandTableMarker
            {
                MarkerId = "platoon_1", UnitId = "platoon_1",
                Position = new Vector3(0, 0, 8).ToVector2XZ(),
                IsEnemy = false, LastUpdateTime = 0f, Confidence = 1f, TroopCount = 60
            });

            AddOrUpdateMarker(new SandTableMarker
            {
                MarkerId = "platoon_2", UnitId = "platoon_2",
                Position = new Vector3(-6, 0, 5).ToVector2XZ(),
                IsEnemy = false, LastUpdateTime = 0f, Confidence = 1f, TroopCount = 52
            });

            AddOrUpdateMarker(new SandTableMarker
            {
                MarkerId = "platoon_3", UnitId = "platoon_3",
                Position = new Vector3(6, 0, 5).ToVector2XZ(),
                IsEnemy = false, LastUpdateTime = 0f, Confidence = 1f, TroopCount = 45
            });

            AddOrUpdateMarker(new SandTableMarker
            {
                MarkerId = "platoon_4", UnitId = "platoon_4",
                Position = new Vector3(0, 0, 3).ToVector2XZ(),
                IsEnemy = false, LastUpdateTime = 0f, Confidence = 1f, TroopCount = 38
            });
        }

        /// <summary>添加或更新标记（带动画）</summary>
        public void AddOrUpdateMarker(SandTableMarker marker)
        {
            int existingIdx = markers.FindIndex(m => m.MarkerId == marker.MarkerId);
            if (existingIdx >= 0)
            {
                var old = markers[existingIdx];
                marker.PreviousPosition = old.Position;
                markers[existingIdx] = marker;

                // 启动移动动画
                if (markerObjects.TryGetValue(marker.MarkerId, out var existingGo))
                {
                    StartMoveAnimation(marker.MarkerId, existingGo.transform, old.Position, marker.Position);
                }
            }
            else
            {
                marker.PreviousPosition = marker.Position;
                markers.Add(marker);
                CreateMarkerSprite(marker);
            }
        }

        /// <summary>创建标记 Sprite</summary>
        private void CreateMarkerSprite(SandTableMarker marker)
        {
            string name = marker.IsBridgehead ? "Bridgehead" :
                          marker.IsEnemy ? $"Enemy_{marker.UnitId}" :
                          $"Friendly_{marker.UnitId}";

            Color color = marker.IsBridgehead ? bridgeheadColor :
                          marker.IsEnemy ? enemyColor :
                          friendlyColor;

            float size = marker.IsBridgehead ? bridgeheadSize : markerSize;

            var go = new GameObject(name);
            go.transform.SetParent(markerLayer, false);
            go.transform.localPosition = new Vector3(marker.Position.x, marker.Position.y, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = color;
            sr.sortingLayerName = terrainSortingLayer;
            sr.sortingOrder = markerOrder;

            // 敌军用方形，我方用圆形（缩放区分）
            if (marker.IsEnemy && !marker.IsBridgehead)
            {
                go.transform.localScale = new Vector3(size * 0.8f, size * 0.8f, 1f);
            }
            else if (marker.IsBridgehead)
            {
                // 桥头堡 → 圆形（正方形缩放 + 脉冲组件）
                go.transform.localScale = new Vector3(size, size, 1f);
                go.AddComponent<BridgeHighlightSprite>();
            }
            else
            {
                // 我方 → 稍微拉长为椭圆模拟三角感
                go.transform.localScale = new Vector3(size * 0.8f, size, 1f);
            }

            markerObjects[marker.MarkerId] = go.transform;
        }

        /// <summary>移除标记</summary>
        public void RemoveMarker(string markerId)
        {
            markers.RemoveAll(m => m.MarkerId == markerId);
            if (moveCoroutines.TryGetValue(markerId, out var co))
            {
                StopCoroutine(co);
                moveCoroutines.Remove(markerId);
            }
            if (markerObjects.TryGetValue(markerId, out var go))
            {
                Destroy(go.gameObject);
                markerObjects.Remove(markerId);
            }
        }

        /// <summary>清除所有标记</summary>
        public void ClearAllMarkers()
        {
            foreach (var co in moveCoroutines.Values) StopCoroutine(co);
            moveCoroutines.Clear();
            foreach (var go in markerObjects.Values) Destroy(go.gameObject);
            markerObjects.Clear();
            markers.Clear();
        }

        /// <summary>标记为失联</summary>
        public void MarkAsLostContact(string markerId)
        {
            int idx = markers.FindIndex(m => m.MarkerId == markerId);
            if (idx >= 0)
            {
                markers[idx].IsLostContact = true;
                if (markerObjects.TryGetValue(markerId, out var go))
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = lostContactColor;
                }
            }
        }

        #endregion

        #region 移动动画

        /// <summary>启动平滑移动动画</summary>
        private void StartMoveAnimation(string markerId, Transform markerTransform, Vector2 from, Vector2 to)
        {
            if (moveCoroutines.TryGetValue(markerId, out var existing))
            {
                StopCoroutine(existing);
            }
            moveCoroutines[markerId] = StartCoroutine(SmoothMove(markerId, markerTransform, from, to));
        }

        /// <summary>协程：Lerp 平滑移动</summary>
        private IEnumerator SmoothMove(string markerId, Transform markerTransform, Vector2 from, Vector2 to)
        {
            float distance = Vector2.Distance(from, to);
            if (distance < 0.01f)
            {
                markerTransform.localPosition = new Vector3(to.x, to.y, 0f);
                moveCoroutines.Remove(markerId);
                yield break;
            }

            float duration = distance / moveSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = moveEase.Evaluate(Mathf.Clamp01(elapsed / duration));
                Vector2 pos = Vector2.Lerp(from, to, t);
                markerTransform.localPosition = new Vector3(pos.x, pos.y, 0f);
                yield return null;
            }

            markerTransform.localPosition = new Vector3(to.x, to.y, 0f);
            moveCoroutines.Remove(markerId);
        }

        #endregion

        #region 视觉刷新

        /// <summary>刷新标记颜色（新鲜度 + 状态）</summary>
        private void RefreshMarkerVisuals()
        {
            float currentTime = Time.time;

            foreach (var marker in markers)
            {
                if (!markerObjects.TryGetValue(marker.MarkerId, out var go)) continue;

                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) continue;

                // 桥头堡不受新鲜度影响
                if (marker.IsBridgehead) continue;

                // 失联
                if (marker.IsLostContact)
                {
                    sr.color = lostContactColor;
                    continue;
                }

                // 新鲜度
                float age = currentTime - marker.LastUpdateTime;
                Color baseColor = marker.IsEnemy ? enemyColor : friendlyColor;

                if (age > expiredThreshold)
                {
                    sr.color = lostContactColor;
                }
                else if (age > staleThreshold)
                {
                    sr.color = Color.Lerp(baseColor, new Color(1f, 0.55f, 0f), 0.6f);
                }
                else if (age > freshThreshold)
                {
                    sr.color = Color.Lerp(baseColor, Color.yellow, 0.3f);
                }
                else
                {
                    sr.color = baseColor;
                }

                // 战斗中 → 闪烁
                if (marker.Morale < 30f)
                {
                    float flash = Mathf.PingPong(Time.time * 3f, 1f);
                    sr.color = Color.Lerp(sr.color, Color.red, flash * 0.4f);
                }
            }
        }

        #endregion

        #region 事件驱动

        private void SubscribeEvents()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.OnBattlefieldUpdated += HandleBattlefieldUpdated;
                GameEventBus.Instance.OnUnitPositionChanged += HandleUnitPositionChanged;
                GameEventBus.Instance.OnSandTableUpdated += HandleSandTableUpdated;
                GameEventBus.Instance.OnRadioReportDelivered += HandleRadioReportDelivered;
            }
        }

        private void UnsubscribeEvents()
        {
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.OnBattlefieldUpdated -= HandleBattlefieldUpdated;
                GameEventBus.Instance.OnUnitPositionChanged -= HandleUnitPositionChanged;
                GameEventBus.Instance.OnSandTableUpdated -= HandleSandTableUpdated;
                GameEventBus.Instance.OnRadioReportDelivered -= HandleRadioReportDelivered;
            }
        }

        /// <summary>
        /// 战场数据更新 — 刷新整体状态（桥梁HP、波次等）
        /// </summary>
        private void HandleBattlefieldUpdated(BattlefieldData data)
        {
            if (data == null) return;
            // 桥梁HP变化可通过颜色/脉冲反馈（后续扩展）
            Debug.Log($"[SandTable] 战场更新 — 桥HP:{data.BridgeHP}/{data.BridgeMaxHP} 波次:{data.CurrentWave}");
        }

        /// <summary>
        /// 单位位置变更 — 更新标记位置，触发移动动画
        /// </summary>
        private void HandleUnitPositionChanged(UnitPositionData data)
        {
            if (data == null || string.IsNullOrEmpty(data.UnitId)) return;

            Vector2 worldPos = new Vector2(data.WorldPosition.x, data.WorldPosition.z);

            int idx = markers.FindIndex(m => m.UnitId == data.UnitId);

            if (idx >= 0)
            {
                var marker = markers[idx];
                Vector2 oldPos = marker.Position;
                marker.Position = worldPos;
                marker.TroopCount = data.TroopCount;
                marker.Morale = data.Morale;
                marker.LastUpdateTime = Time.time;
                markers[idx] = marker;

                // 启动移动动画
                if (markerObjects.TryGetValue(marker.MarkerId, out var go))
                {
                    StartMoveAnimation(marker.MarkerId, go.transform, oldPos, worldPos);
                }

                Debug.Log($"[SandTable] 单位移动: {marker.MarkerId} → ({worldPos.x:F1}, {worldPos.y:F1})");
            }
            else
            {
                // 新单位
                AddOrUpdateMarker(new SandTableMarker
                {
                    MarkerId = data.UnitId,
                    UnitId = data.UnitId,
                    Position = worldPos,
                    IsEnemy = data.IsEnemy,
                    LastUpdateTime = Time.time,
                    TroopCount = data.TroopCount,
                    Morale = data.Morale,
                    Confidence = 1f
                });
            }
        }

        /// <summary>沙盘情报更新</summary>
        private void HandleSandTableUpdated(IntelligenceEntry entry)
        {
            if (entry == null) return;

            Vector2 worldPos = new Vector2(entry.Position.x, entry.Position.z);
            string markerId = entry.IsEnemy ? $"enemy_{entry.EntryId}" : entry.UnitId;

            AddOrUpdateMarker(new SandTableMarker
            {
                MarkerId = markerId,
                UnitId = entry.UnitId,
                Position = worldPos,
                IsEnemy = entry.IsEnemy,
                IsLostContact = entry.Freshness < 0.1f,
                LastUpdateTime = entry.LastUpdateTime,
                Confidence = entry.Confidence
            });
        }

        /// <summary>无线电汇报 — 更新单位位置</summary>
        private void HandleRadioReportDelivered(RadioReport report)
        {
            if (report?.Content?.ReportedPosition == null) return;

            Vector2 worldPos = new Vector2(report.Content.ReportedPosition.Value.x,
                                           report.Content.ReportedPosition.Value.z);

            int idx = markers.FindIndex(m => m.UnitId == report.SourceUnitId);
            if (idx >= 0)
            {
                var marker = markers[idx];
                marker.Position = worldPos;
                marker.LastUpdateTime = report.DeliveredTime ?? Time.time;
                marker.Confidence = report.Accuracy.OverallAccuracy;
                markers[idx] = marker;

                if (markerObjects.TryGetValue(marker.MarkerId, out var go))
                {
                    StartMoveAnimation(marker.MarkerId, go.transform,
                        new Vector2(go.transform.localPosition.x, go.transform.localPosition.y), worldPos);
                }
            }
        }

        #endregion

        #region 坐标转换

        /// <summary>归一化坐标 (0-1) → 沙盘世界坐标</summary>
        private Vector2 NormalizedToWorld(Vector2 normalized)
        {
            return new Vector2(
                Mathf.Lerp(mapMinX, mapMaxX, normalized.x),
                Mathf.Lerp(mapMinY, mapMaxY, normalized.y)
            );
        }

        /// <summary>归一化尺寸 → 沙盘世界尺寸</summary>
        private Vector2 NormalizedToWorldSize(Vector2 normalizedSize)
        {
            return new Vector2(
                normalizedSize.x * (mapMaxX - mapMinX),
                normalizedSize.y * (mapMaxY - mapMinY)
            );
        }

        /// <summary>世界坐标 → 沙盘世界坐标（XZ 平面映射）</summary>
        private Vector2 WorldToSandTable(Vector3 worldPos)
        {
            // 假设世界坐标范围: X(-500,500), Z(-500,500) → 沙盘坐标(mapMinX~mapMaxX, mapMinY~mapMaxY)
            float nx = Mathf.Clamp01((worldPos.x + 500f) / 1000f);
            float ny = Mathf.Clamp01((worldPos.z + 500f) / 1000f);
            return NormalizedToWorld(new Vector2(nx, ny));
        }

        #endregion

        #region 公共 API

        /// <summary>设置沙盘整体透明度</summary>
        public void SetAlpha(float alpha)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                var c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        /// <summary>获取当前所有标记（只读）</summary>
        public IReadOnlyList<SandTableMarker> GetMarkers() => markers.AsReadOnly();

        #endregion
    }

    #region 辅助组件

    /// <summary>桥梁高亮脉冲（SpriteRenderer 版）</summary>
    public class BridgeHighlightSprite : MonoBehaviour
    {
        private SpriteRenderer sr;
        private Color baseColor;
        private float pulseSpeed = 2f;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr != null) baseColor = sr.color;
        }

        void Update()
        {
            if (sr == null) return;
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(0.6f, 1f, pulse);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }

    #endregion

    #region 扩展方法

    /// <summary>Vector3 → Vector2 (XZ 平面)</summary>
    public static class VectorExtensions
    {
        public static Vector2 ToVector2XZ(this Vector3 v) => new Vector2(v.x, v.z);
    }

    #endregion
}
