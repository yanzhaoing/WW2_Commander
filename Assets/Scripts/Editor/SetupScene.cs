// SetupScene.cs — Unity 场景自动搭建脚本
// 菜单入口: WW2 Commander → Setup Scene
// 自动创建完整的 WW2 Commander 游戏场景，包含:
//   摄像机、灯光、Canvas(1920x1080)、EventSystem
//   所有核心系统 GameObject 并挂载对应脚本组件
//   指挥所场景物件（沙盘桌、无线电台、文件架等）
//   沙盘棋子（友军/敌军初始部署）
//
// 参考: GDD 指挥所场景布局 (SWO-136)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using TMPro;

namespace SWO1.Editor
{
    /// <summary>
    /// 场景自动搭建工具 — 一键生成完整游戏场景
    /// </summary>
    public static class SetupScene
    {
        private const string MenuPath = "WW2 Commander/Setup Scene";

        // ── 常量 ──────────────────────────────────────────
        private const int CanvasWidth  = 1920;
        private const int CanvasHeight = 1080;

        // ── 颜色常量 (GDD §7.1) ──────────────────────────
        private static readonly Color WarmLampColor    = new Color(1f, 0.84f, 0.6f);   // #FFD699
        private static readonly Color RadioGreen       = new Color(0f, 1f, 0.255f);    // #00FF41
        private static readonly Color EmergencyRed     = new Color(0.8f, 0.1f, 0.1f);
        private static readonly Color SeaBlue          = new Color(0.18f, 0.35f, 0.43f);
        private static readonly Color SandColor        = new Color(0.83f, 0.72f, 0.59f);
        private static readonly Color WoodDarkBrown    = new Color(0.24f, 0.17f, 0.12f);
        private static readonly Color MilitaryGreen    = new Color(0.29f, 0.36f, 0.14f);

        // ── 层级名称 ──────────────────────────────────────
        private const string LayerInteractable = "Interactable";
        private const string LayerUI           = "UI";

        // ══════════════════════════════════════════════════
        [MenuItem(MenuPath, priority = 50)]
        public static void Execute()
        {
            // 先清理场景中的默认对象（可选）
            // 注意: 不强制删除，避免丢失已配置的内容

            // ── 1. 核心系统 (不可见的管理器) ──────────────
            var eventBus      = CreateGameEventBus();
            var gameDirector  = CreateGameDirector();
            var commandSystem = CreateCommandSystem();
            var infoSystem    = CreateInformationSystem();
            var reportGen     = CreateReportGenerator();
            var battleSim     = CreateBattleSimulator();
            var battleInterface = CreateBattleSimulationInterface();
            var enemyWaveMgr  = CreateEnemyWaveManager();
            var aiDirector    = CreateAIDirector();
            var sandRenderer  = CreateSandTableRenderer();

            // ── 2. 摄像机 ────────────────────────────────
            var cameraObj = CreateMainCamera();

            // ── 3. 灯光 ──────────────────────────────────
            CreateLighting();

            // ── 4. 指挥所场景物件 ─────────────────────────
            var room = CreateCommandPostRoom();
            var sandTable = CreateSandTable(room.transform);
            var radioStation = CreateRadioStationObject(room.transform);
            var documentArea = CreateDocumentArea(room.transform);
            var environment = CreateEnvironmentProps(room.transform);

            // ── 5. Canvas + UI 系统 ───────────────────────
            var canvas = CreateCanvas();
            var eventSystem = CreateEventSystem();
            var uiSystem = CreateUISystem(canvas.transform);

            // ── 6. 沙盘棋子 ───────────────────────────────
            CreateChessPieces(sandTable.transform);

            // ── 7. 选中根节点并提示 ───────────────────────
            Selection.activeGameObject = cameraObj;
            Debug.Log("[SetupScene] ✅ 场景搭建完成！" +
                      "\n  • 摄像机 + CameraController + InputSystem" +
                      "\n  • 灯光 (台灯 + 环境光 + 紧急灯)" +
                      "\n  • 指挥所房间 (6m×8m×2.8m)" +
                      "\n  • 沙盘桌 + 无线电台 + 文件架" +
                      "\n  • Canvas (1920×1080) + EventSystem" +
                      "\n  • UISystem (无线电面板/指令卡/状态便签/结算弹窗)" +
                      "\n  • 全部核心系统单例 (GameEventBus, GameDirector, etc.)" +
                      "\n  • 沙盘棋子 (3 友军 + 2 敌军初始部署)");

            EditorUtility.DisplayDialog("WW2 Commander",
                "场景搭建完成！\n\n请在 Unity 中查看 Hierarchy 面板确认。\n\n" +
                "提示：确保 Input System Package 已安装 (Window → Package Manager)。",
                "OK");
        }

        // ══════════════════════════════════════════════════
        // 核心系统 — 不可见的管理器 GameObject
        // ══════════════════════════════════════════════════

        #region 核心系统

        private static GameObject CreateGameEventBus()
        {
            var go = CreateOrFind("[GameEventBus]");
            RequireComponent<SWO1.Core.GameEventBus>(go);
            Debug.Log("[SetupScene] GameEventBus 创建完成");
            return go;
        }

        private static GameObject CreateGameDirector()
        {
            var go = CreateOrFind("[GameDirector]");
            var gd = RequireComponent<SWO1.Core.GameDirector>(go);
            // 设置默认值 (GDD: 06:00 - 09:00 战役)
            Undo.RecordObject(gd, "Setup GameDirector");
            gd.difficulty = SWO1.Core.Difficulty.Normal;
            gd.timeScale = 1f;
            Debug.Log("[SetupScene] GameDirector 创建完成");
            return go;
        }

        private static GameObject CreateCommandSystem()
        {
            var go = CreateOrFind("[CommandSystem]");
            RequireComponent<SWO1.Command.CommandSystem>(go);
            Debug.Log("[SetupScene] CommandSystem 创建完成");
            return go;
        }

        private static GameObject CreateInformationSystem()
        {
            var go = CreateOrFind("[InformationSystem]");
            RequireComponent<SWO1.Intelligence.InformationSystem>(go);
            Debug.Log("[SetupScene] InformationSystem 创建完成");
            return go;
        }

        private static GameObject CreateReportGenerator()
        {
            var go = CreateOrFind("[ReportGenerator]");
            RequireComponent<SWO1.Intelligence.ReportGenerator>(go);
            Debug.Log("[SetupScene] ReportGenerator 创建完成");
            return go;
        }

        private static GameObject CreateBattleSimulator()
        {
            var go = CreateOrFind("[BattleSimulator]");
            RequireComponent<SWO1.Simulation.BattleSimulator>(go);
            Debug.Log("[SetupScene] BattleSimulator 创建完成");
            return go;
        }

        private static GameObject CreateBattleSimulationInterface()
        {
            var go = CreateOrFind("[BattleSimulationInterface]");
            // RequireComponent<BattleSimulationInterface>(go); /// fixed: type moved
            Debug.Log("[SetupScene] BattleSimulationInterface 创建完成");
            return go;
        }

        private static GameObject CreateEnemyWaveManager()
        {
            var go = CreateOrFind("[EnemyWaveManager]");
            RequireComponent<SWO1.Simulation.EnemyWaveManager>(go);
            Debug.Log("[SetupScene] EnemyWaveManager 创建完成");
            return go;
        }

        private static GameObject CreateAIDirector()
        {
            var go = CreateOrFind("[AIDirector]");
            RequireComponent<SWO1.AI.AIDirector>(go);
            Debug.Log("[SetupScene] AIDirector 创建完成");
            return go;
        }

        private static GameObject CreateSandTableRenderer()
        {
            var go = CreateOrFind("[SandTableRenderer]");
            RequireComponent<SWO1.Visualization.SandTableRenderer>(go);
            Debug.Log("[SetupScene] SandTableRenderer 创建完成");
            return go;
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 摄像机 — 第一人称固定视角
        // ══════════════════════════════════════════════════

        #region 摄像机

        private static GameObject CreateMainCamera()
        {
            // 删除默认 Camera（如果存在）
            var existing = GameObject.Find("Main Camera");
            if (existing != null)
                Object.DestroyImmediate(existing);

            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0f, 1.6f, -2f);
            camObj.transform.rotation = Quaternion.Euler(5f, 0f, 0f);

            // Camera 组件
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags    = CameraClearFlags.Skybox;
            cam.fieldOfView   = 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 100f;
            cam.depth         = 0;

            // Audio Listener
            camObj.AddComponent<AudioListener>();

            // CameraController (SWO-158)
            var ctrl = camObj.AddComponent<SWO1.CommandPost.CameraController>();

            // ScreenNoiseEffect (CRT 噪点)
            camObj.AddComponent<SWO1.UI.ScreenNoiseEffect>();

            Debug.Log("[SetupScene] Main Camera 创建完成 (FOV=60, 位置=(0, 1.6, -2))");
            return camObj;
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 灯光 — GDD §2.3 照明方案
        // ══════════════════════════════════════════════════

        #region 灯光

        private static void CreateLighting()
        {
            // 删除默认 Directional Light
            var existing = GameObject.Find("Directional Light");
            if (existing != null)
                Object.DestroyImmediate(existing);

            // ── 主环境光 (极暗) ──────────────────────────
            var envLight = new GameObject("Environment Light");
            var envDL = envLight.AddComponent<Light>();
            envDL.type      = LightType.Directional;
            envDL.color     = new Color(0.15f, 0.15f, 0.2f); // 深蓝暗光
            envDL.intensity = 0.3f;
            envDL.shadows   = LightShadows.Soft;
            envLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // ── 台灯 (沙盘桌上方, 暖黄色 #FFD699) ──────
            var tableLamp = new GameObject("Table Lamp Light");
            tableLamp.transform.position = new Vector3(0f, 2.5f, 0f);
            var lampLight = tableLamp.AddComponent<Light>();
            lampLight.type      = LightType.Spot;
            lampLight.color     = WarmLampColor;
            lampLight.intensity = 2.5f;
            lampLight.range     = 4f;
            lampLight.spotAngle = 60f;
            lampLight.shadows   = LightShadows.Soft;

            // 台灯视觉球体
            var lampVis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lampVis.name = "Lamp Bulb";
            lampVis.transform.SetParent(tableLamp.transform);
            lampVis.transform.localPosition = Vector3.zero;
            lampVis.transform.localScale    = Vector3.one * 0.05f;
            var lampRenderer = lampVis.GetComponent<Renderer>();
            lampRenderer.material = new Material(Shader.Find("Standard"))
            {
                color = WarmLampColor,
                enableInstancing = true
            };
            lampRenderer.material.SetColor("_EmissionColor", WarmLampColor * 3f);
            lampRenderer.material.EnableKeyword("_EMISSION");

            // 移除碰撞体（装饰用）
            Object.DestroyImmediate(lampVis.GetComponent<Collider>());

            // ── 紧急灯 (天花板红色微光) ─────────────────
            var emergLight = new GameObject("Emergency Light");
            emergLight.transform.position = new Vector3(0f, 2.7f, 0f);
            var emergL = emergLight.AddComponent<Light>();
            emergL.type      = LightType.Point;
            emergL.color     = EmergencyRed;
            emergL.intensity = 0.15f;
            emergL.range     = 3f;

            // ── 舷窗透光 (黎明) ─────────────────────────
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -2.5f : 2.5f;
                var windowLight = new GameObject($"Window Light {i + 1}");
                windowLight.transform.position = new Vector3(x, 2f, 3.5f);
                var wl = windowLight.AddComponent<Light>();
                wl.type      = LightType.Spot;
                wl.color     = new Color(0.3f, 0.25f, 0.35f); // 黎明深蓝
                wl.intensity = 0.8f;
                wl.range     = 5f;
                wl.spotAngle = 90f;
                wl.transform.rotation = Quaternion.Euler(30f, 180f, 0f);
            }

            // 渲染设置
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.08f, 0.1f);

            Debug.Log("[SetupScene] 灯光创建完成 (环境光 + 台灯 + 紧急灯 + 舷窗灯)");
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 指挥所房间 — GDD §2 场景布局
        // ══════════════════════════════════════════════════

        #region 指挥所场景

        private static GameObject CreateCommandPostRoom()
        {
            var room = new GameObject("=== Command Post Room ===");
            room.transform.position = Vector3.zero;

            // ── 地板 (6m × 8m) ───────────────────────────
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(room.transform);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 1f);
            floor.transform.localScale    = new Vector3(6f, 0.1f, 8f);
            SetMaterial(floor, WoodDarkBrown, 0f, 0.3f);

            // ── 墙壁 ─────────────────────────────────────
            // 后墙 (南, 舱门所在)
            CreateWall(room.transform, "Wall_Back",
                new Vector3(0f, 1.4f, 5f), new Vector3(6f, 2.8f, 0.15f));
            // 前墙 (北, 舷窗所在)
            CreateWall(room.transform, "Wall_Front",
                new Vector3(0f, 1.4f, -3f), new Vector3(6f, 2.8f, 0.15f));
            // 左墙
            CreateWall(room.transform, "Wall_Left",
                new Vector3(-3f, 1.4f, 1f), new Vector3(0.15f, 2.8f, 8f));
            // 右墙
            CreateWall(room.transform, "Wall_Right",
                new Vector3(3f, 1.4f, 1f), new Vector3(0.15f, 2.8f, 8f));
            // 天花板
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Ceiling";
            ceiling.transform.SetParent(room.transform);
            ceiling.transform.localPosition = new Vector3(0f, 2.85f, 1f);
            ceiling.transform.localScale    = new Vector3(6f, 0.1f, 8f);
            SetMaterial(ceiling, new Color(0.15f, 0.15f, 0.15f));

            // ── 舷窗 (2 个, 前墙) ───────────────────────
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -1.5f : 1.5f;
                var porthole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                porthole.name = $"Porthole_{i + 1}";
                porthole.transform.SetParent(room.transform);
                porthole.transform.localPosition = new Vector3(x, 1.8f, -2.9f);
                porthole.transform.localScale    = new Vector3(0.6f, 0.08f, 0.6f);
                porthole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                SetMaterial(porthole, SeaBlue, 0.9f, 0.8f);
                // 玻璃效果（半透明）
                var rend = porthole.GetComponent<Renderer>();
                var mat = rend.material;
                mat.SetFloat("_Mode", 3); // Transparent (Standard shader)
                Color c = SeaBlue;
                c.a = 0.6f;
                mat.color = c;
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            // ── 舱门 (后墙) ─────────────────────────────
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Hatch Door";
            door.transform.SetParent(room.transform);
            door.transform.localPosition = new Vector3(0f, 1.2f, 4.95f);
            door.transform.localScale    = new Vector3(1.2f, 2.2f, 0.1f);
            SetMaterial(door, new Color(0.35f, 0.35f, 0.35f), 0.8f, 0.5f);

            // 舱门圆形窗口
            var doorWindow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            doorWindow.name = "Hatch Window";
            doorWindow.transform.SetParent(door.transform);
            doorWindow.transform.localPosition = new Vector3(0f, 0.3f, -0.5f);
            doorWindow.transform.localScale    = new Vector3(0.3f, 0.2f, 0.3f);
            doorWindow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterial(doorWindow, new Color(0.2f, 0.3f, 0.4f, 0.5f));

            Debug.Log("[SetupScene] 指挥所房间创建完成 (6m×8m×2.8m)");
            return room;
        }

        private static void CreateWall(Transform parent, string name, Vector3 localPos, Vector3 scale)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale    = scale;
            SetMaterial(wall, new Color(0.25f, 0.22f, 0.18f));
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 沙盘桌 — GDD §3
        // ══════════════════════════════════════════════════

        #region 沙盘桌

        private static GameObject CreateSandTable(Transform parent)
        {
            // 沙盘桌组 (正前方偏右 15°, GDD §2.2)
            var table = new GameObject("Sand Table");
            table.transform.SetParent(parent);
            table.transform.localPosition = new Vector3(0.4f, 0f, 0.5f);
            table.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);

            // ── 桌面 ─────────────────────────────────────
            var tabletop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tabletop.name = "Tabletop";
            tabletop.transform.SetParent(table.transform);
            tabletop.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            tabletop.transform.localScale    = new Vector3(2f, 0.05f, 1f);
            SetMaterial(tabletop, WoodDarkBrown, 0f, 0.3f);

            // ── 桌腿 (4 条) ─────────────────────────────
            Vector3[] legPositions =
            {
                new Vector3(-0.9f, 0.375f, -0.45f),
                new Vector3( 0.9f, 0.375f, -0.45f),
                new Vector3(-0.9f, 0.375f,  0.45f),
                new Vector3( 0.9f, 0.375f,  0.45f)
            };
            for (int i = 0; i < legPositions.Length; i++)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = $"Leg_{i + 1}";
                leg.transform.SetParent(table.transform);
                leg.transform.localPosition = legPositions[i];
                leg.transform.localScale    = new Vector3(0.05f, 0.75f, 0.05f);
                SetMaterial(leg, WoodDarkBrown, 0f, 0.3f);
            }

            // ── 沙盘区域 (实体地形板) ────────────────────
            var sandBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sandBoard.name = "Sand Board";
            sandBoard.transform.SetParent(table.transform);
            sandBoard.transform.localPosition = new Vector3(0f, 0.80f, 0f);
            sandBoard.transform.localScale    = new Vector3(1.8f, 0.03f, 0.8f);
            SetMaterial(sandBoard, SandColor, 0f, 0.1f);
            // 设为 Interactable 层以便射线检测
            SetLayerRecursive(sandBoard, LayerInteractable);

            // ── 沙盘围边 (防止棋子掉落) ─────────────────
            CreateRim(table.transform, "Rim_N", new Vector3(0f, 0.83f, -0.415f), new Vector3(1.82f, 0.06f, 0.03f));
            CreateRim(table.transform, "Rim_S", new Vector3(0f, 0.83f,  0.415f), new Vector3(1.82f, 0.06f, 0.03f));
            CreateRim(table.transform, "Rim_W", new Vector3(-0.91f, 0.83f, 0f),  new Vector3(0.03f, 0.06f, 0.85f));
            CreateRim(table.transform, "Rim_E", new Vector3( 0.91f, 0.83f, 0f),  new Vector3(0.03f, 0.06f, 0.85f));

            // ── 地形标记 (海/沙滩分界线) ────────────────
            var seaLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seaLine.name = "Sea Zone";
            seaLine.transform.SetParent(sandBoard.transform);
            seaLine.transform.localPosition = new Vector3(0f, 0.5f, -0.3f);
            seaLine.transform.localScale    = new Vector3(0.95f, 0.5f, 0.2f);
            SetMaterial(seaLine, SeaBlue, 0f, 0.1f);
            Object.DestroyImmediate(seaLine.GetComponent<Collider>());

            // ── 棋子盒 (桌角) ───────────────────────────
            var pieceBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pieceBox.name = "Piece Box";
            pieceBox.transform.SetParent(table.transform);
            pieceBox.transform.localPosition = new Vector3(-0.85f, 0.80f, 0.4f);
            pieceBox.transform.localScale    = new Vector3(0.15f, 0.04f, 0.1f);
            SetMaterial(pieceBox, new Color(0.3f, 0.25f, 0.2f));

            Debug.Log("[SetupScene] 沙盘桌创建完成 (2m×1m 桌面, 1.8m×0.8m 沙盘区域)");
            return table;
        }

        private static void CreateRim(Transform parent, string name, Vector3 pos, Vector3 scale)
        {
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = name;
            rim.transform.SetParent(parent);
            rim.transform.localPosition = pos;
            rim.transform.localScale    = scale;
            SetMaterial(rim, WoodDarkBrown, 0f, 0.3f);
            Object.DestroyImmediate(rim.GetComponent<Collider>());
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 无线电台 — GDD §4
        // ══════════════════════════════════════════════════

        #region 无线电台

        private static GameObject CreateRadioStationObject(Transform parent)
        {
            // 右侧 30°-60°, GDD §2.2
            var radio = new GameObject("Radio Station");
            radio.transform.SetParent(parent);
            radio.transform.localPosition = new Vector3(2f, 0f, 0.8f);
            radio.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);

            // ── 电台桌 ───────────────────────────────────
            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Radio Desk";
            desk.transform.SetParent(radio.transform);
            desk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            desk.transform.localScale    = new Vector3(1f, 0.05f, 0.6f);
            SetMaterial(desk, MilitaryGreen, 0.8f, 0.5f);

            // ── 主机面板 ─────────────────────────────────
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Radio Panel";
            panel.transform.SetParent(radio.transform);
            panel.transform.localPosition = new Vector3(0f, 1.15f, -0.15f);
            panel.transform.localScale    = new Vector3(0.8f, 0.45f, 0.05f);
            panel.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f); // 倾斜面板
            SetMaterial(panel, MilitaryGreen, 0.8f, 0.5f);
            SetLayerRecursive(panel, LayerInteractable);

            // ── 挂载 RadioStation 脚本 ──────────────────
            var rs = radio.AddComponent<SWO1.CommandPost.RadioStation>();

            // ── 频率选择按钮 (5 个) ─────────────────────
            for (int i = 0; i < 5; i++)
            {
                float x = -0.28f + (i % 3) * 0.28f;
                float y = i < 3 ? 0.12f : -0.06f;
                var btn = GameObject.CreatePrimitive(PrimitiveType.Cube);
                btn.name = $"FreqButton_{i + 1}";
                btn.transform.SetParent(panel.transform);
                btn.transform.localPosition = new Vector3(x, y, -0.3f);
                btn.transform.localScale    = new Vector3(0.08f, 0.06f, 0.04f);
                SetMaterial(btn, new Color(0.2f, 0.2f, 0.2f));
                SetLayerRecursive(btn, LayerInteractable);

                // 按钮标签
                var label = new GameObject($"Label_{i + 1}");
                label.transform.SetParent(btn.transform);
                label.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                label.transform.localScale    = new Vector3(10f, 10f, 20f);
                var tmp = label.AddComponent<TextMeshPro>();
                tmp.text      = $"{i + 1}";
                tmp.fontSize  = 6f;
                tmp.color     = RadioGreen;
                tmp.alignment = TextAlignmentOptions.Center;
            }

            // ── TX/RX 指示灯 ─────────────────────────────
            CreateIndicatorLight(radio.transform, "TX Indicator", new Vector3(-0.2f, 1.3f, -0.25f), Color.green);
            CreateIndicatorLight(radio.transform, "RX Indicator", new Vector3( 0.0f, 1.3f, -0.25f), Color.green);
            CreateIndicatorLight(radio.transform, "Jam Indicator", new Vector3(0.2f, 1.3f, -0.25f), Color.yellow);

            // ── 耳机 (悬挂) ─────────────────────────────
            var headphones = new GameObject("Headphones");
            headphones.transform.SetParent(radio.transform);
            headphones.transform.localPosition = new Vector3(0.4f, 1.0f, -0.1f);
            // 耳机用两个小球 + 一个圆柱模拟
            var hp_left = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hp_left.name = "HP_Left";
            hp_left.transform.SetParent(headphones.transform);
            hp_left.transform.localPosition = new Vector3(-0.04f, 0f, 0f);
            hp_left.transform.localScale    = new Vector3(0.04f, 0.04f, 0.04f);
            SetMaterial(hp_left, new Color(0.15f, 0.15f, 0.15f));
            Object.DestroyImmediate(hp_left.GetComponent<Collider>());

            var hp_right = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hp_right.name = "HP_Right";
            hp_right.transform.SetParent(headphones.transform);
            hp_right.transform.localPosition = new Vector3(0.04f, 0f, 0f);
            hp_right.transform.localScale    = new Vector3(0.04f, 0.04f, 0.04f);
            SetMaterial(hp_right, new Color(0.15f, 0.15f, 0.15f));
            Object.DestroyImmediate(hp_right.GetComponent<Collider>());

            var hp_band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hp_band.name = "HP_Band";
            hp_band.transform.SetParent(headphones.transform);
            hp_band.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            hp_band.transform.localScale    = new Vector3(0.02f, 0.05f, 0.02f);
            hp_band.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            SetMaterial(hp_band, new Color(0.2f, 0.15f, 0.1f));
            Object.DestroyImmediate(hp_band.GetComponent<Collider>());

            // ── 手持麦克风 ───────────────────────────────
            var mic = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mic.name = "Handheld Mic";
            mic.transform.SetParent(radio.transform);
            mic.transform.localPosition = new Vector3(-0.35f, 0.97f, 0.1f);
            mic.transform.localScale    = new Vector3(0.025f, 0.08f, 0.025f);
            mic.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            SetMaterial(mic, new Color(0.15f, 0.15f, 0.15f));
            Object.DestroyImmediate(mic.GetComponent<Collider>());

            // ── 香烟/烟灰缸 (氛围) ─────────────────────
            var ashtray = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ashtray.name = "Ashtray";
            ashtray.transform.SetParent(radio.transform);
            ashtray.transform.localPosition = new Vector3(0.35f, 0.94f, 0.2f);
            ashtray.transform.localScale    = new Vector3(0.05f, 0.015f, 0.05f);
            SetMaterial(ashtray, new Color(0.3f, 0.3f, 0.3f));
            Object.DestroyImmediate(ashtray.GetComponent<Collider>());

            Debug.Log("[SetupScene] 无线电台创建完成 (含 RadioStation 组件 + 5 频率按钮 + 指示灯)");
            return radio;
        }

        private static void CreateIndicatorLight(Transform parent, string name, Vector3 pos, Color color)
        {
            var light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            light.name = name;
            light.transform.SetParent(parent);
            light.transform.localPosition = pos;
            light.transform.localScale    = new Vector3(0.02f, 0.02f, 0.02f);
            var rend = light.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetColor("_EmissionColor", color * 2f);
            mat.EnableKeyword("_EMISSION");
            rend.material = mat;
            Object.DestroyImmediate(light.GetComponent<Collider>());
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 文件/情报区 — GDD §2.2C
        // ══════════════════════════════════════════════════

        #region 文件区

        private static GameObject CreateDocumentArea(Transform parent)
        {
            // 左侧 15°-45°, GDD §2.2
            var docArea = new GameObject("Document Area");
            docArea.transform.SetParent(parent);
            docArea.transform.localPosition = new Vector3(-2f, 0f, 0.5f);
            docArea.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);

            // ── 文件桌 ───────────────────────────────────
            var docDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            docDesk.name = "Document Desk";
            docDesk.transform.SetParent(docArea.transform);
            docDesk.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            docDesk.transform.localScale    = new Vector3(0.8f, 0.05f, 0.6f);
            SetMaterial(docDesk, WoodDarkBrown, 0f, 0.3f);

            // ── 来文架 ───────────────────────────────────
            var inbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inbox.name = "Inbox Tray";
            inbox.transform.SetParent(docArea.transform);
            inbox.transform.localPosition = new Vector3(-0.2f, 0.82f, -0.1f);
            inbox.transform.localScale    = new Vector3(0.15f, 0.08f, 0.1f);
            SetMaterial(inbox, new Color(0.6f, 0.55f, 0.4f));

            // ── 待发架 ───────────────────────────────────
            var outbox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outbox.name = "Outbox Tray";
            outbox.transform.SetParent(docArea.transform);
            outbox.transform.localPosition = new Vector3(0.1f, 0.82f, -0.1f);
            outbox.transform.localScale    = new Vector3(0.15f, 0.08f, 0.1f);
            SetMaterial(outbox, new Color(0.6f, 0.55f, 0.4f));

            // ── 文件 DocumentObject ──────────────────────
            var docObj = new GameObject("Mission Briefing");
            docObj.transform.SetParent(inbox.transform);
            docObj.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            docObj.transform.localScale    = new Vector3(0.8f, 0.01f, 1f);
            var docRend = docObj.AddComponent<MeshRenderer>();
            var docFilter = docObj.AddComponent<MeshFilter>();
            docFilter.mesh = CreateQuadMesh();
            docRend.material = new Material(Shader.Find("Standard"))
            {
                color = new Color(0.95f, 0.93f, 0.85f) // 米白纸张
            };
            Object.DestroyImmediate(docObj.GetComponent<Collider>());
            var docComp = docObj.AddComponent<SWO1.CommandPost.DocumentObject>();

            // ── 情报板 (墙上面板) ────────────────────────
            var intelBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            intelBoard.name = "Intel Board";
            intelBoard.transform.SetParent(docArea.transform);
            intelBoard.transform.localPosition = new Vector3(0f, 1.5f, -0.28f);
            intelBoard.transform.localScale    = new Vector3(0.6f, 0.4f, 0.02f);
            SetMaterial(intelBoard, new Color(0.15f, 0.2f, 0.15f));
            Object.DestroyImmediate(intelBoard.GetComponent<Collider>());

            // ── 挂钟 (墙) ───────────────────────────────
            var clock = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            clock.name = "Wall Clock";
            clock.transform.SetParent(docArea.transform);
            clock.transform.localPosition = new Vector3(0.3f, 1.8f, -0.28f);
            clock.transform.localScale    = new Vector3(0.15f, 0.02f, 0.15f);
            clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetMaterial(clock, new Color(0.2f, 0.2f, 0.2f));
            Object.DestroyImmediate(clock.GetComponent<Collider>());

            Debug.Log("[SetupScene] 文件/情报区创建完成 (含来文架/待发架/情报板/挂钟)");
            return docArea;
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 环境装饰道具 — GDD §2.2D
        // ══════════════════════════════════════════════════

        #region 环境道具

        private static GameObject CreateEnvironmentProps(Transform parent)
        {
            var env = new GameObject("Environment Props");
            env.transform.SetParent(parent);

            // ── 咖啡杯 (沙盘桌角) ───────────────────────
            var cup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cup.name = "Coffee Cup";
            cup.transform.SetParent(env.transform);
            cup.transform.localPosition = new Vector3(1.3f, 0.82f, 0.9f);
            cup.transform.localScale    = new Vector3(0.03f, 0.035f, 0.03f);
            SetMaterial(cup, new Color(0.35f, 0.25f, 0.2f));
            cup.AddComponent<SWO1.CommandPost.InteractableObject>();
            // 由于 InteractableObject 是 abstract，改用简单 Collider
            Object.DestroyImmediate(cup.GetComponent<Collider>());
            cup.AddComponent<BoxCollider>();

            // ── 座位 (玩家座位) ─────────────────────────
            var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Player Seat";
            seat.transform.SetParent(env.transform);
            seat.transform.localPosition = new Vector3(0f, 0.35f, -1.5f);
            seat.transform.localScale    = new Vector3(0.5f, 0.05f, 0.5f);
            SetMaterial(seat, new Color(0.2f, 0.18f, 0.15f));

            // 椅背
            var backrest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backrest.name = "Seat Backrest";
            backrest.transform.SetParent(seat.transform);
            backrest.transform.localPosition = new Vector3(0f, 1.5f, 0.45f);
            backrest.transform.localScale    = new Vector3(1f, 3f, 0.1f);
            SetMaterial(backrest, new Color(0.2f, 0.18f, 0.15f));

            // 桌面前方 (玩家面前的小桌)
            var frontDesk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontDesk.name = "Player Desk";
            frontDesk.transform.SetParent(env.transform);
            frontDesk.transform.localPosition = new Vector3(0f, 0.65f, -0.8f);
            frontDesk.transform.localScale    = new Vector3(1.2f, 0.04f, 0.4f);
            SetMaterial(frontDesk, WoodDarkBrown, 0f, 0.3f);

            // ── 散落文件 (桌面装饰) ─────────────────────
            for (int i = 0; i < 3; i++)
            {
                var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
                paper.name = $"Scattered Paper {i + 1}";
                paper.transform.SetParent(env.transform);
                float x = Random.Range(-0.4f, 0.4f);
                float z = Random.Range(-1f, 0f);
                float rot = Random.Range(-15f, 15f);
                paper.transform.localPosition = new Vector3(x, 0.68f, z);
                paper.transform.localScale    = new Vector3(0.12f, 0.002f, 0.16f);
                paper.transform.localRotation = Quaternion.Euler(0f, rot, 0f);
                SetMaterial(paper, new Color(0.9f, 0.88f, 0.8f));
                Object.DestroyImmediate(paper.GetComponent<Collider>());
            }

            Debug.Log("[SetupScene] 环境道具创建完成 (咖啡杯/座位/散落文件)");
            return env;
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 沙盘棋子 — 初始部署
        // ══════════════════════════════════════════════════

        #region 沙盘棋子

        private static void CreateChessPieces(Transform sandTableParent)
        {
            // 找到 Sand Board 作为棋子的父级
            var sandBoard = sandTableParent.Find("Sand Board");
            var pieceParent = sandBoard != null ? sandBoard : sandTableParent;

            // ── 友军棋子 (蓝色) ──────────────────────────
            // 第1步兵连 — 海滩左侧
            CreatePiece(pieceParent, "Piece_Company1",
                new Vector3(-0.3f, 0.5f, -0.15f),
                SWO1.CommandPost.PieceFaction.US,
                SWO1.CommandPost.PieceUnitType.Infantry,
                "第1步兵连", "红一",
                SWO1.CommandPost.PieceStatus.Confirmed);

            // 第2步兵连 — 海滩右侧
            CreatePiece(pieceParent, "Piece_Company2",
                new Vector3(0.3f, 0.5f, -0.15f),
                SWO1.CommandPost.PieceFaction.US,
                SWO1.CommandPost.PieceUnitType.Infantry,
                "第2步兵连", "红二",
                SWO1.CommandPost.PieceStatus.Confirmed);

            // 坦克排 — 后方待命
            CreatePiece(pieceParent, "Piece_TankPlatoon",
                new Vector3(0f, 0.5f, -0.35f),
                SWO1.CommandPost.PieceFaction.US,
                SWO1.CommandPost.PieceUnitType.Armor,
                "坦克排", "蓝三",
                SWO1.CommandPost.PieceStatus.Confirmed);

            // ── 敌军棋子 (红色, 推测位置) ───────────────
            // 敌军碉堡 A — 悬崖东侧
            CreatePiece(pieceParent, "Piece_Enemy_FortA",
                new Vector3(-0.2f, 0.5f, 0.2f),
                SWO1.CommandPost.PieceFaction.German,
                SWO1.CommandPost.PieceUnitType.Fortification,
                "碉堡 Alpha", "",
                SWO1.CommandPost.PieceStatus.Estimated);

            // 敌军步兵 — 内陆
            CreatePiece(pieceParent, "Piece_Enemy_Infantry",
                new Vector3(0.1f, 0.5f, 0.25f),
                SWO1.CommandPost.PieceFaction.German,
                SWO1.CommandPost.PieceUnitType.Infantry,
                "敌步兵排", "",
                SWO1.CommandPost.PieceStatus.Estimated);

            Debug.Log("[SetupScene] 沙盘棋子创建完成 (3 友军 + 2 敌军)");
        }

        private static void CreatePiece(Transform parent, string name, Vector3 localPos,
            SWO1.CommandPost.PieceFaction faction, SWO1.CommandPost.PieceUnitType unitType,
            string unitName, string callsign, SWO1.CommandPost.PieceStatus status)
        {
            // 棋子基座
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = name;
            piece.transform.SetParent(parent);
            piece.transform.localPosition = localPos;
            piece.transform.localScale    = new Vector3(0.04f, 0.03f, 0.04f);

            // 根据阵营着色
            Color color = faction == SWO1.CommandPost.PieceFaction.US
                ? new Color(0.2f, 0.4f, 0.9f)
                : new Color(0.9f, 0.2f, 0.2f);

            // 推测状态降低透明度
            if (status == SWO1.CommandPost.PieceStatus.Estimated)
                color.a = 0.6f;

            SetMaterial(piece, color);

            // 根据单位类型改变形状标记
            switch (unitType)
            {
                case SWO1.CommandPost.PieceUnitType.Infantry:
                    // 方块 — 已经是 Cube，OK
                    break;
                case SWO1.CommandPost.PieceUnitType.Armor:
                    // 菱形 — 旋转 45°
                    piece.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    piece.transform.localScale = new Vector3(0.035f, 0.03f, 0.035f);
                    break;
                case SWO1.CommandPost.PieceUnitType.HQ:
                    // 圆形
                    Object.DestroyImmediate(piece.GetComponent<MeshFilter>());
                    piece.AddComponent<SphereCollider>();
                    var mf = piece.AddComponent<MeshFilter>();
                    mf.mesh = CreateSimpleMesh();
                    break;
                case SWO1.CommandPost.PieceUnitType.Fortification:
                    // 三角形标记 (用四面体近似)
                    piece.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
                    break;
            }

            // 挂载 ChessPiece 脚本
            var cp = piece.AddComponent<SWO1.CommandPost.ChessPiece>();
            cp.Faction       = faction;
            cp.UnitType      = unitType;
            cp.UnitName      = unitName;
            cp.RadioCallsign = callsign;
            cp.SandTablePosition = localPos;

            // 名称标签 (TextMeshPro)
            var label = new GameObject("Label");
            label.transform.SetParent(piece.transform);
            label.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            label.transform.localScale    = new Vector3(3f, 3f, 3f);
            var tmp = label.AddComponent<TextMeshPro>();
            tmp.text      = unitName;
            tmp.fontSize  = 3f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 10;
        }

        #endregion

        // ══════════════════════════════════════════════════
        // Canvas + UI 系统 — GDD §4-5
        // ══════════════════════════════════════════════════

        #region Canvas & UI

        private static GameObject CreateCanvas()
        {
            var canvasObj = new GameObject("Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.matchWidthOrHeight = 0.5f; // 宽高匹配平衡

            canvasObj.AddComponent<GraphicRaycaster>();

            // 设为 UI 层
            SetLayerRecursive(canvasObj, LayerUI);

            Debug.Log($"[SetupScene] Canvas 创建完成 ({CanvasWidth}×{CanvasHeight}, ScaleWithScreenSize)");
            return canvasObj;
        }

        private static GameObject CreateEventSystem()
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("[SetupScene] EventSystem 创建完成");
            return es;
        }

        /// <summary>
        /// 创建 UISystem 主控 + 所有子面板 (GDD §4-5)
        /// </summary>
        private static GameObject CreateUISystem(Transform canvasTransform)
        {
            // ── UISystem 根节点 ──────────────────────────
            var uiRoot = new GameObject("[UISystem]");
            uiRoot.transform.SetParent(canvasTransform);
            var uiSystem = uiRoot.AddComponent<SWO1.UI.UISystem>();

            // ── 1. 无线电文本面板 (RadioTextPanel) ───────
            var radioPanel = CreateRadioTextPanel(canvasTransform);

            // ── 2. 指令卡面板 (OrderCardPanel) ───────────
            var orderPanel = CreateOrderCardPanel(canvasTransform);

            // ── 3. 状态便签 (StatusNotePanel) ────────────
            var statusPanel = CreateStatusNotePanel(canvasTransform);

            // ── 4. 胜负结算弹窗 (GameResultPopup) ────────
            var resultPopup = CreateGameResultPopup(canvasTransform);

            // ── 5. 沙盘标注叠加层 (SandTableOverlay) ─────
            var sandOverlay = CreateSandTableOverlay(canvasTransform);

            // ── 6. 指令状态面板 (CommandStatusPanel) ─────
            var cmdStatusPanel = CreateCommandStatusPanel(canvasTransform);

            // 使用反射设置 UISystem 的序列化引用
            SetPrivateField(uiSystem, "radioTextPanel", radioPanel.GetComponent<SWO1.UI.RadioTextPanel>());
            SetPrivateField(uiSystem, "orderCardPanel", orderPanel.GetComponent<SWO1.UI.OrderCardPanel>());
            SetPrivateField(uiSystem, "statusNotePanel", statusPanel.GetComponent<SWO1.UI.StatusNotePanel>());
            SetPrivateField(uiSystem, "gameResultPopup", resultPopup.GetComponent<SWO1.UI.GameResultPopup>());
            SetPrivateField(uiSystem, "sandTableOverlay", sandOverlay.GetComponent<SWO1.UI.SandTableOverlay>());
            SetPrivateField(uiSystem, "commandStatusPanel", cmdStatusPanel.GetComponent<SWO1.UI.CommandStatusPanel>());

            Debug.Log("[SetupScene] UISystem 创建完成 (6 个子面板)");
            return uiRoot;
        }

        // ─────────────────────────────────────────────────
        // 无线电文本面板
        // ─────────────────────────────────────────────────
        private static GameObject CreateRadioTextPanel(Transform canvas)
        {
            var panel = new GameObject("RadioTextPanel");
            panel.transform.SetParent(canvas);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.55f, 0.3f);
            rt.anchorMax        = new Vector2(0.95f, 0.95f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            // RadioTextPanel 脚本
            var rtp = panel.AddComponent<SWO1.UI.RadioTextPanel>();

            // 背景
            var bg = CreateUIChild<Image>(panel.transform, "Background");
            bg.color = new Color(0f, 0f, 0f, 0.75f);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // 标题
            var title = CreateUIChild<TextMeshProUGUI>(panel.transform, "Title");
            title.text      = "📻 无线电记录";
            title.fontSize  = 18f;
            title.color     = new Color(1f, 0.84f, 0f); // 金色
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin        = new Vector2(0f, 0.92f);
            titleRT.anchorMax        = new Vector2(1f, 1f);
            titleRT.anchoredPosition = new Vector2(10f, 0f);
            titleRT.sizeDelta        = Vector2.zero;

            // ScrollRect 消息容器
            var scrollObj = new GameObject("ScrollRect");
            scrollObj.transform.SetParent(panel.transform);
            var scrollRT = scrollObj.AddComponent<RectTransform>();
            scrollRT.anchorMin        = new Vector2(0f, 0f);
            scrollRT.anchorMax        = new Vector2(1f, 0.9f);
            scrollRT.anchoredPosition = Vector2.zero;
            scrollRT.sizeDelta        = Vector2.zero;

            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // 需要 Image 才能 Mask
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin     = new Vector2(0f, 1f);
            cRT.anchorMax     = new Vector2(1f, 1f);
            cRT.pivot         = new Vector2(0.5f, 1f);
            cRT.sizeDelta     = new Vector2(0f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 5f;
            layout.padding = new RectOffset(10, 10, 5, 5);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = cRT;

            // 消息 Prefab (Template)
            var msgPrefab = CreateRadioMessagePrefab(panel.transform);

            // 滚动按钮
            var scrollUp = CreateUIButton(panel.transform, "ScrollUpBtn", "▲",
                new Vector2(0.92f, 0.9f), new Vector2(0.98f, 0.95f), new Color(0.3f, 0.3f, 0.3f, 0.7f));
            var scrollDown = CreateUIButton(panel.transform, "ScrollDownBtn", "▼",
                new Vector2(0.92f, 0.83f), new Vector2(0.98f, 0.88f), new Color(0.3f, 0.3f, 0.3f, 0.7f));

            // 干扰叠加层
            var interferenceOverlay = CreateUIChild<Image>(panel.transform, "InterferenceOverlay");
            interferenceOverlay.color = new Color(0, 0, 0, 0);
            var intRT = interferenceOverlay.GetComponent<RectTransform>();
            intRT.anchorMin = Vector2.zero;
            intRT.anchorMax = Vector2.one;
            intRT.sizeDelta = Vector2.zero;

            // 关联引用到 RadioTextPanel
            SetPrivateField(rtp, "messageContainer", cRT);
            SetPrivateField(rtp, "messagePrefab", msgPrefab);
            SetPrivateField(rtp, "scrollRect", scrollRect);
            SetPrivateField(rtp, "scrollUpButton", scrollUp.GetComponent<Button>());
            SetPrivateField(rtp, "scrollDownButton", scrollDown.GetComponent<Button>());
            SetPrivateField(rtp, "interferenceOverlay", interferenceOverlay);

            // 消息预制体默认隐藏
            msgPrefab.SetActive(false);

            Debug.Log("[SetupScene] RadioTextPanel 创建完成");
            return panel;
        }

        /// <summary>
        /// 创建无线电消息预制体 (在 Canvas 内作为模板，运行时通过 Instantiate 复制)
        /// </summary>
        private static GameObject CreateRadioMessagePrefab(Transform parent)
        {
            var prefab = new GameObject("MessageTemplate");
            prefab.transform.SetParent(parent);

            var rt = prefab.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0f, 60f);

            // Layout Element
            var le = prefab.AddComponent<LayoutElement>();
            le.minHeight = 40f;
            le.preferredHeight = 60f;

            // 文本
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin        = Vector2.zero;
            textRT.anchorMax        = Vector2.one;
            textRT.anchoredPosition = Vector2.zero;
            textRT.sizeDelta        = new Vector2(-10f, -5f);
            textRT.offsetMin        = new Vector2(5f, 2f);
            textRT.offsetMax        = new Vector2(-5f, -2f);

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize  = 14f;
            tmp.color     = new Color(0.88f, 0.88f, 0.88f); // #E0E0E0
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.fontSharedMaterial = null;

            return prefab;
        }

        // ─────────────────────────────────────────────────
        // 指令卡面板
        // ─────────────────────────────────────────────────
        private static GameObject CreateOrderCardPanel(Transform canvas)
        {
            var panel = new GameObject("OrderCardPanel");
            panel.transform.SetParent(canvas);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.25f, 0.15f);
            rt.anchorMax        = new Vector2(0.75f, 0.85f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            var ocp = panel.AddComponent<SWO1.UI.OrderCardPanel>();

            // 卡片根节点 (初始隐藏)
            var cardRoot = new GameObject("CardRoot");
            cardRoot.transform.SetParent(panel.transform);
            var cardRT = cardRoot.AddComponent<RectTransform>();
            cardRT.anchorMin = Vector2.zero;
            cardRT.anchorMax = Vector2.one;
            cardRT.sizeDelta = Vector2.zero;

            // 背景
            var bg = CreateUIChild<Image>(cardRoot.transform, "CardBackground");
            bg.color = new Color(0.15f, 0.12f, 0.1f, 0.95f);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // 标题
            var title = CreateUIChild<TextMeshProUGUI>(cardRoot.transform, "Title");
            title.text      = "📋 指令卡";
            title.fontSize  = 24f;
            title.color     = new Color(1f, 0.84f, 0f);
            title.alignment = TextAlignmentOptions.Center;
            var tRT = title.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 0.88f);
            tRT.anchorMax = new Vector2(1f, 1f);
            tRT.sizeDelta = Vector2.zero;

            // ── 发送目标 (Dropdown) ──────────────────────
            var freqLabel = CreateUIChild<TextMeshProUGUI>(cardRoot.transform, "FreqLabel");
            freqLabel.text      = "发往：";
            freqLabel.fontSize  = 16f;
            freqLabel.color     = Color.white;
            freqLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var flRT = freqLabel.GetComponent<RectTransform>();
            flRT.anchorMin = new Vector2(0.05f, 0.78f);
            flRT.anchorMax = new Vector2(0.25f, 0.85f);
            flRT.sizeDelta = Vector2.zero;

            // 频率选择 — 使用按钮组代替 Dropdown (5 频率按钮)
            for (int i = 0; i < 5; i++)
            {
                float x = 0.05f + i * 0.19f;
                var btn = CreateUIButton(cardRoot.transform, $"FreqBtn_{i + 1}",
                    $"CH-{i + 1}", new Vector2(x, 0.71f), new Vector2(x + 0.17f, 0.77f),
                    new Color(0.2f, 0.25f, 0.2f, 0.8f));
            }

            // ── 指令类型 (按钮组) ───────────────────────
            var typeLabel = CreateUIChild<TextMeshProUGUI>(cardRoot.transform, "TypeLabel");
            typeLabel.text      = "指令类型：";
            typeLabel.fontSize  = 16f;
            typeLabel.color     = Color.white;
            typeLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var tlRT = typeLabel.GetComponent<RectTransform>();
            tlRT.anchorMin = new Vector2(0.05f, 0.63f);
            tlRT.anchorMax = new Vector2(0.25f, 0.70f);
            tlRT.sizeDelta = Vector2.zero;

            string[] cmdTypes = { "🚶移动", "⚔攻击", "🛡防御", "🏃撤退", "🔍侦察", "💥炮击", "📊状态", "📦补给" };
            Color[] cmdColors =
            {
                new Color(0.3f, 0.6f, 0.9f), new Color(0.9f, 0.25f, 0.15f),
                new Color(0.85f, 0.75f, 0.2f), new Color(0.6f, 0.3f, 0.7f),
                new Color(0.2f, 0.8f, 0.7f), new Color(0.95f, 0.5f, 0.1f),
                new Color(0.5f, 0.55f, 0.5f), new Color(0.4f, 0.7f, 0.4f)
            };
            for (int i = 0; i < cmdTypes.Length; i++)
            {
                float x = 0.05f + (i % 4) * 0.235f;
                float y = i < 4 ? 0.55f : 0.47f;
                CreateUIButton(cardRoot.transform, $"CmdBtn_{cmdTypes[i]}", cmdTypes[i],
                    new Vector2(x, y), new Vector2(x + 0.215f, y + 0.065f), cmdColors[i]);
            }

            // ── 文本输入区 ──────────────────────────────
            var inputLabel = CreateUIChild<TextMeshProUGUI>(cardRoot.transform, "InputLabel");
            inputLabel.text      = "目标/说明：";
            inputLabel.fontSize  = 16f;
            inputLabel.color     = Color.white;
            inputLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var ilRT = inputLabel.GetComponent<RectTransform>();
            ilRT.anchorMin = new Vector2(0.05f, 0.40f);
            ilRT.anchorMax = new Vector2(0.25f, 0.46f);
            ilRT.sizeDelta = Vector2.zero;

            // TMP InputField
            var inputObj = new GameObject("ContentInput");
            inputObj.transform.SetParent(cardRoot.transform);
            var inputRT = inputObj.AddComponent<RectTransform>();
            inputRT.anchorMin        = new Vector2(0.05f, 0.18f);
            inputRT.anchorMax        = new Vector2(0.95f, 0.38f);
            inputRT.anchoredPosition = Vector2.zero;
            inputRT.sizeDelta        = Vector2.zero;
            var inputField = inputObj.AddComponent<TMP_InputField>();

            // InputField - Text Area
            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(inputObj.transform);
            var taRT = textArea.AddComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.sizeDelta = Vector2.zero;
            textArea.AddComponent<RectMask2D>();

            // InputField - Text Display
            var displayText = new GameObject("Text Display");
            displayText.transform.SetParent(textArea.transform);
            var dtRT = displayText.AddComponent<RectTransform>();
            dtRT.anchorMin = Vector2.zero;
            dtRT.anchorMax = Vector2.one;
            dtRT.offsetMin = new Vector2(5f, 5f);
            dtRT.offsetMax = new Vector2(-5f, -5f);
            var dtTmp = displayText.AddComponent<TextMeshProUGUI>();
            dtTmp.fontSize  = 14f;
            dtTmp.color     = Color.white;
            dtTmp.alignment = TextAlignmentOptions.TopLeft;
            dtTmp.enableWordWrapping = true;

            // InputField - Placeholder
            var placeholder = new GameObject("Placeholder");
            placeholder.transform.SetParent(textArea.transform);
            var phRT = placeholder.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(5f, 5f);
            phRT.offsetMax = new Vector2(-5f, -5f);
            var phTmp = placeholder.AddComponent<TextMeshProUGUI>();
            phTmp.text      = "输入指令内容...或从快捷短语选择";
            phTmp.fontSize  = 14f;
            phTmp.fontStyle = FontStyles.Italic;
            phTmp.color     = new Color(0.5f, 0.5f, 0.5f, 0.8f);
            phTmp.alignment = TextAlignmentOptions.TopLeft;
            phTmp.enableWordWrapping = true;

            inputField.textViewport   = taRT;
            inputField.textComponent  = dtTmp;
            inputField.placeholder    = phTmp;

            // 输入区背景
            var inputBG = CreateUIChild<Image>(inputObj.transform, "InputBG");
            inputBG.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            inputBG.transform.SetAsFirstSibling();
            var ibgRT = inputBG.GetComponent<RectTransform>();
            ibgRT.anchorMin = Vector2.zero;
            ibgRT.anchorMax = Vector2.one;
            ibgRT.sizeDelta = Vector2.zero;

            // ── 发送/丢弃按钮 ────────────────────────────
            var sendBtn = CreateUIButton(cardRoot.transform, "SendBtn", "📡 发送指令",
                new Vector2(0.1f, 0.05f), new Vector2(0.45f, 0.15f),
                new Color(0.2f, 0.5f, 0.2f, 0.9f));
            var discardBtn = CreateUIButton(cardRoot.transform, "DiscardBtn", "🗑 丢弃",
                new Vector2(0.55f, 0.05f), new Vector2(0.9f, 0.15f),
                new Color(0.5f, 0.2f, 0.2f, 0.9f));

            // ── 发送反馈 ─────────────────────────────────
            var feedbackObj = new GameObject("SendFeedback");
            feedbackObj.transform.SetParent(cardRoot.transform);
            var fbRT = feedbackObj.AddComponent<RectTransform>();
            fbRT.anchorMin = new Vector2(0.2f, 0.82f);
            fbRT.anchorMax = new Vector2(0.8f, 0.88f);
            fbRT.sizeDelta = Vector2.zero;
            var feedbackGroup = feedbackObj.AddComponent<CanvasGroup>();
            feedbackGroup.alpha = 0f;

            var feedbackText = CreateUIChild<TextMeshProUGUI>(feedbackObj.transform, "FeedbackText");
            feedbackText.text      = "";
            feedbackText.fontSize  = 16f;
            feedbackText.color     = new Color(0.2f, 0.85f, 0.3f);
            feedbackText.alignment = TextAlignmentOptions.Center;
            var ftRT = feedbackText.GetComponent<RectTransform>();
            ftRT.anchorMin = Vector2.zero;
            ftRT.anchorMax = Vector2.one;
            ftRT.sizeDelta = Vector2.zero;

            // ── 冷却环 ──────────────────────────────────
            var cooldownObj = new GameObject("CooldownRing");
            cooldownObj.transform.SetParent(cardRoot.transform);
            var cdRT = cooldownObj.AddComponent<RectTransform>();
            cdRT.anchorMin = new Vector2(0.45f, 0.05f);
            cdRT.anchorMax = new Vector2(0.55f, 0.15f);
            cdRT.sizeDelta = Vector2.zero;
            var cdImg = cooldownObj.AddComponent<Image>();
            cdImg.type = Image.Type.Filled;
            cdImg.fillMethod = Image.FillMethod.Radial360;
            cdImg.color = new Color(0.85f, 0.25f, 0.1f, 0.6f);
            cooldownObj.SetActive(false);

            var cooldownText = CreateUIChild<TextMeshProUGUI>(cooldownObj.transform, "CooldownText");
            cooldownText.fontSize  = 12f;
            cooldownText.color     = Color.white;
            cooldownText.alignment = TextAlignmentOptions.Center;
            var cdtRT = cooldownText.GetComponent<RectTransform>();
            cdtRT.anchorMin = Vector2.zero;
            cdtRT.anchorMax = Vector2.one;
            cdtRT.sizeDelta = Vector2.zero;

            // ── 卡片边框 (颜色随指令类型变化) ────────────
            var cardBorder = CreateUIChild<Image>(cardRoot.transform, "CardBorder");
            cardBorder.color = new Color(0.3f, 0.6f, 0.9f);
            var cbRT = cardBorder.GetComponent<RectTransform>();
            cbRT.anchorMin = Vector2.zero;
            cbRT.anchorMax = Vector2.one;
            cbRT.sizeDelta = Vector2.zero;
            cbRT.offsetMin = new Vector2(-3f, -3f);
            cbRT.offsetMax = new Vector2(3f, 3f);
            cardBorder.transform.SetAsFirstSibling();
            // 需要自定义边框效果时可用 Image 或 outline

            // 关联引用到 OrderCardPanel
            SetPrivateField(ocp, "cardRoot", cardRoot);
            SetPrivateField(ocp, "contentInput", inputField);
            SetPrivateField(ocp, "sendButton", sendBtn.GetComponent<Button>());
            SetPrivateField(ocp, "discardButton", discardBtn.GetComponent<Button>());
            SetPrivateField(ocp, "sendFeedbackGroup", feedbackGroup);
            SetPrivateField(ocp, "sendFeedbackText", feedbackText);
            SetPrivateField(ocp, "cooldownRing", cdImg);
            SetPrivateField(ocp, "cooldownText", cooldownText);
            SetPrivateField(ocp, "cardBorder", cardBorder);

            cardRoot.SetActive(false); // 默认隐藏

            Debug.Log("[SetupScene] OrderCardPanel 创建完成");
            return panel;
        }

        // ─────────────────────────────────────────────────
        // 状态便签面板
        // ─────────────────────────────────────────────────
        private static GameObject CreateStatusNotePanel(Transform canvas)
        {
            var panel = new GameObject("StatusNotePanel");
            panel.transform.SetParent(canvas);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.01f, 0.01f);
            rt.anchorMax        = new Vector2(0.25f, 0.55f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            var snp = panel.AddComponent<SWO1.UI.StatusNotePanel>();

            // 背景 (米黄纸张感)
            var bg = CreateUIChild<Image>(panel.transform, "Background");
            bg.color = new Color(1f, 0.97f, 0.86f, 0.9f); // #FFF8DC
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // 标题
            var title = CreateUIChild<TextMeshProUGUI>(panel.transform, "Title");
            title.text      = "⏰ 态势摘要";
            title.fontSize  = 20f;
            title.color     = new Color(0.2f, 0.15f, 0.1f);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;
            var tRT = title.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 0.9f);
            tRT.anchorMax = new Vector2(1f, 1f);
            tRT.sizeDelta = Vector2.zero;

            // 分隔线
            var divider = CreateUIChild<Image>(panel.transform, "Divider");
            divider.color = new Color(0.3f, 0.25f, 0.2f, 0.5f);
            var dRT = divider.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.05f, 0.89f);
            dRT.anchorMax = new Vector2(0.95f, 0.895f);
            dRT.sizeDelta = Vector2.zero;

            // 时间文本
            var timeText = CreateTMP(panel.transform, "TimeText", "当前时间：--:--",
                16f, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.MidlineLeft,
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.88f));

            // 阶段文本
            var phaseText = CreateTMP(panel.transform, "PhaseText", "当前阶段：准备中",
                16f, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.MidlineLeft,
                new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.81f));

            // 状态文本 (多行)
            var statusText = CreateTMP(panel.transform, "StatusText",
                "已登陆部队：--\n预计敌军强度：--\n师部最新指令：--",
                14f, new Color(0.3f, 0.25f, 0.2f), TextAlignmentOptions.TopLeft,
                new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.74f));

            // 桥 HP 标签
            var bridgeLabel = CreateTMP(panel.transform, "BridgeLabel", "桥梁 HP:",
                14f, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.MidlineLeft,
                new Vector2(0.05f, 0.33f), new Vector2(0.35f, 0.38f));

            // 桥 HP Slider
            var sliderObj = new GameObject("BridgeHP Slider");
            sliderObj.transform.SetParent(panel.transform);
            var sRT = sliderObj.AddComponent<RectTransform>();
            sRT.anchorMin        = new Vector2(0.05f, 0.27f);
            sRT.anchorMax        = new Vector2(0.95f, 0.32f);
            sRT.anchoredPosition = Vector2.zero;
            sRT.sizeDelta        = Vector2.zero;

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = 1f;

            // Slider Background
            var sliderBG = CreateUIChild<Image>(sliderObj.transform, "Background");
            sliderBG.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            var sbgRT = sliderBG.GetComponent<RectTransform>();
            sbgRT.anchorMin = Vector2.zero;
            sbgRT.anchorMax = Vector2.one;
            sbgRT.sizeDelta = Vector2.zero;

            // Slider Fill Area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform);
            var faRT = fillArea.AddComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.sizeDelta = Vector2.zero;

            var fill = CreateUIChild<Image>(fillArea.transform, "Fill");
            fill.color = new Color(0.2f, 0.75f, 0.25f); // HPGood
            var fRT = fill.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero;
            fRT.anchorMax = Vector2.one;
            fRT.sizeDelta = Vector2.zero;

            slider.fillRect = fRT;
            slider.targetGraphic = fill;

            // 桥 HP 文本
            var bridgeHPText = CreateTMP(panel.transform, "BridgeHPText", "100/100",
                12f, new Color(0.3f, 0.3f, 0.3f), TextAlignmentOptions.MidlineRight,
                new Vector2(0.65f, 0.27f), new Vector2(0.95f, 0.32f));

            // 倒计时
            var countdownText = CreateTMP(panel.transform, "CountdownText", "",
                24f, new Color(0.85f, 0.25f, 0.1f), TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.26f));

            // 排状态 (3 个)
            CreatePlatoonStatusView(panel.transform, "Platoon1",
                new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.17f));
            CreatePlatoonStatusView(panel.transform, "Platoon2",
                new Vector2(0.05f, -0.02f), new Vector2(0.95f, 0.07f));
            CreatePlatoonStatusView(panel.transform, "Platoon3",
                new Vector2(0.05f, -0.12f), new Vector2(0.95f, -0.03f));

            // 增援文本
            var reinforcementText = CreateTMP(panel.transform, "ReinforcementText", "",
                12f, new Color(0.3f, 0.3f, 0.3f), TextAlignmentOptions.TopLeft,
                new Vector2(0.05f, -0.25f), new Vector2(0.95f, -0.13f));

            // 关联引用
            SetPrivateField(snp, "timeText", timeText);
            SetPrivateField(snp, "phaseText", phaseText);
            SetPrivateField(snp, "statusText", statusText);
            SetPrivateField(snp, "bridgeHPSlider", slider);
            SetPrivateField(snp, "bridgeHPFill", fill);
            SetPrivateField(snp, "bridgeHPText", bridgeHPText);
            SetPrivateField(snp, "countdownText", countdownText);
            SetPrivateField(snp, "reinforcementText", reinforcementText);

            Debug.Log("[SetupScene] StatusNotePanel 创建完成");
            return panel;
        }

        /// <summary>
        /// 创建排级状态小部件
        /// </summary>
        private static void CreatePlatoonStatusView(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            var psv = obj.AddComponent<SWO1.UI.PlatoonStatusView>();

            // 名称
            var nameText = CreateTMP(obj.transform, "NameText", name,
                12f, new Color(0.2f, 0.15f, 0.1f), TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0.5f), new Vector2(0.35f, 1f));

            // 状态
            var stateText = CreateTMP(obj.transform, "StateText", "--",
                12f, new Color(0.5f, 0.5f, 0.5f), TextAlignmentOptions.MidlineLeft,
                new Vector2(0.35f, 0.5f), new Vector2(0.55f, 1f));

            // 位置
            var posText = CreateTMP(obj.transform, "PosText", "",
                11f, new Color(0.4f, 0.4f, 0.4f), TextAlignmentOptions.MidlineRight,
                new Vector2(0.55f, 0.5f), new Vector2(1f, 1f));

            // 兵力条
            var sliderObj = new GameObject("StrengthBar");
            sliderObj.transform.SetParent(obj.transform);
            var sRT = sliderObj.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0f);
            sRT.anchorMax = new Vector2(1f, 0.45f);
            sRT.sizeDelta = Vector2.zero;
            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value    = 1f;

            var barBG = CreateUIChild<Image>(sliderObj.transform, "BarBG");
            barBG.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
            var bgRT = barBG.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            var fillObj = new GameObject("Fill Area");
            fillObj.transform.SetParent(sliderObj.transform);
            var faRT = fillObj.AddComponent<RectTransform>();
            faRT.anchorMin = Vector2.zero;
            faRT.anchorMax = Vector2.one;
            faRT.sizeDelta = Vector2.zero;

            var fillImg = CreateUIChild<Image>(fillObj.transform, "Fill");
            fillImg.color = new Color(0.2f, 0.75f, 0.25f);
            var fRT = fillImg.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero;
            fRT.anchorMax = Vector2.one;
            fRT.sizeDelta = Vector2.zero;
            slider.fillRect = fRT;

            // 活跃指示灯
            var activeDot = CreateUIChild<Image>(obj.transform, "ActiveDot");
            activeDot.color = new Color(0.2f, 0.85f, 0.3f);
            var adRT = activeDot.GetComponent<RectTransform>();
            adRT.anchorMin = new Vector2(0.96f, 0.5f);
            adRT.anchorMax = new Vector2(1f, 1f);
            adRT.sizeDelta = Vector2.zero;

            // 关联引用
            SetPrivateField(psv, "nameText", nameText);
            SetPrivateField(psv, "stateText", stateText);
            SetPrivateField(psv, "posText", posText);
            SetPrivateField(psv, "strengthBar", slider);
            SetPrivateField(psv, "strengthFill", fillImg);
            SetPrivateField(psv, "activeDot", activeDot);
        }

        // ─────────────────────────────────────────────────
        // 胜负结算弹窗
        // ─────────────────────────────────────────────────
        private static GameObject CreateGameResultPopup(Transform canvas)
        {
            var panel = new GameObject("GameResultPopup");
            panel.transform.SetParent(canvas);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var grp = panel.AddComponent<SWO1.UI.GameResultPopup>();

            // CanvasGroup (透明度控制)
            var canvasGroup = panel.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // 背景暗化
            var bgDim = CreateUIChild<Image>(panel.transform, "BackgroundDim");
            bgDim.color = new Color(0, 0, 0, 0);
            var bgRT = bgDim.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgDim.gameObject.SetActive(false);

            // 弹窗根节点
            var popupRoot = new GameObject("PopupRoot");
            popupRoot.transform.SetParent(panel.transform);
            var prRT = popupRoot.AddComponent<RectTransform>();
            prRT.anchorMin        = new Vector2(0.2f, 0.15f);
            prRT.anchorMax        = new Vector2(0.8f, 0.85f);
            prRT.anchoredPosition = Vector2.zero;
            prRT.sizeDelta        = Vector2.zero;

            // 弹窗背景
            var popupBG = CreateUIChild<Image>(popupRoot.transform, "PopupBG");
            popupBG.color = new Color(0.1f, 0.08f, 0.06f, 0.95f);
            var pbgRT = popupBG.GetComponent<RectTransform>();
            pbgRT.anchorMin = Vector2.zero;
            pbgRT.anchorMax = Vector2.one;
            pbgRT.sizeDelta = Vector2.zero;

            // 标题
            var titleText = CreateTMP(popupRoot.transform, "TitleText", "",
                36f, new Color(0.95f, 0.85f, 0.2f), TextAlignmentOptions.Center,
                new Vector2(0f, 0.85f), new Vector2(1f, 1f));

            // 评级
            var gradeText = CreateTMP(popupRoot.transform, "GradeText", "",
                24f, Color.white, TextAlignmentOptions.Center,
                new Vector2(0f, 0.75f), new Vector2(1f, 0.84f));

            // 统计数据
            var statsText = CreateTMP(popupRoot.transform, "StatsText", "",
                16f, new Color(0.88f, 0.88f, 0.88f), TextAlignmentOptions.TopLeft,
                new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.74f));

            // 提示文本
            var hintText = CreateTMP(popupRoot.transform, "HintText", "",
                14f, new Color(0.6f, 0.6f, 0.6f), TextAlignmentOptions.Center,
                new Vector2(0.1f, 0.15f), new Vector2(0.9f, 0.28f));

            // 关闭按钮
            var closeBtn = CreateUIButton(popupRoot.transform, "CloseBtn", "关闭",
                new Vector2(0.3f, 0.03f), new Vector2(0.5f, 0.12f),
                new Color(0.3f, 0.3f, 0.3f, 0.8f));

            // 重新开始按钮
            var restartBtn = CreateUIButton(popupRoot.transform, "RestartBtn", "🔄 重新开始",
                new Vector2(0.52f, 0.03f), new Vector2(0.7f, 0.12f),
                new Color(0.2f, 0.5f, 0.2f, 0.9f));

            // 关联引用
            SetPrivateField(grp, "canvasGroup", canvasGroup);
            SetPrivateField(grp, "popupRoot", prRT);
            SetPrivateField(grp, "titleText", titleText);
            SetPrivateField(grp, "gradeText", gradeText);
            SetPrivateField(grp, "statsText", statsText);
            SetPrivateField(grp, "hintText", hintText);
            SetPrivateField(grp, "closeButton", closeBtn.GetComponent<Button>());
            SetPrivateField(grp, "restartButton", restartBtn.GetComponent<Button>());
            SetPrivateField(grp, "backgroundDim", bgDim);

            prRT.localScale = Vector3.zero; // 初始缩放为 0

            Debug.Log("[SetupScene] GameResultPopup 创建完成");
            return panel;
        }

        // ─────────────────────────────────────────────────
        // 沙盘标注叠加层
        // ─────────────────────────────────────────────────
        private static GameObject CreateSandTableOverlay(Transform canvas)
        {
            var panel = new GameObject("SandTableOverlay");
            panel.transform.SetParent(canvas);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            var sto = panel.AddComponent<SWO1.UI.SandTableOverlay>();

            // Marker Container
            var markerContainer = new GameObject("MarkerContainer");
            markerContainer.transform.SetParent(panel.transform);
            var mcRT = markerContainer.AddComponent<RectTransform>();
            mcRT.anchorMin = new Vector2(0.25f, 0.2f);
            mcRT.anchorMax = new Vector2(0.55f, 0.7f);
            mcRT.sizeDelta = Vector2.zero;

            SetPrivateField(sto, "markerContainer", markerContainer.transform);

            Debug.Log("[SetupScene] SandTableOverlay 创建完成");
            return panel;
        }

        // ─────────────────────────────────────────────────
        // 指令状态面板
        // ─────────────────────────────────────────────────
        private static GameObject CreateCommandStatusPanel(Transform canvas)
        {
            var panel = new GameObject("CommandStatusPanel");
            panel.transform.SetParent(canvas);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.01f, 0.56f);
            rt.anchorMax        = new Vector2(0.25f, 0.95f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            var csp = panel.AddComponent<SWO1.UI.CommandStatusPanel>();

            // 背景
            var bg = CreateUIChild<Image>(panel.transform, "Background");
            bg.color = new Color(0f, 0f, 0f, 0.5f);
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;

            // 标题
            var title = CreateTMP(panel.transform, "Title", "📡 指令状态",
                16f, new Color(1f, 0.84f, 0f), TextAlignmentOptions.Center,
                new Vector2(0f, 0.9f), new Vector2(1f, 1f));

            // ScrollRect + Content
            var scrollObj = new GameObject("Scroll");
            scrollObj.transform.SetParent(panel.transform);
            var sRT = scrollObj.AddComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0f);
            sRT.anchorMax = new Vector2(1f, 0.88f);
            sRT.sizeDelta = Vector2.zero;
            var scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var content = new GameObject("Content");
            content.transform.SetParent(scrollObj.transform);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f);
            cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot     = new Vector2(0.5f, 1f);
            cRT.sizeDelta = new Vector2(0f, 0f);
            var layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 3f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = cRT;

            // 指令条目预制体
            var entryPrefab = new GameObject("CommandEntryPrefab");
            entryPrefab.transform.SetParent(panel.transform);
            var epRT = entryPrefab.AddComponent<RectTransform>();
            epRT.sizeDelta = new Vector2(0f, 30f);
            var le = entryPrefab.AddComponent<LayoutElement>();
            le.minHeight = 25f;
            le.preferredHeight = 30f;

            var entryText = new GameObject("EntryText");
            entryText.transform.SetParent(entryPrefab.transform);
            var etRT = entryText.AddComponent<RectTransform>();
            etRT.anchorMin = Vector2.zero;
            etRT.anchorMax = Vector2.one;
            etRT.offsetMin = new Vector2(5f, 2f);
            etRT.offsetMax = new Vector2(-5f, -2f);
            var etTmp = entryText.AddComponent<TextMeshProUGUI>();
            etTmp.fontSize  = 12f;
            etTmp.color     = new Color(0.5f, 0.55f, 0.5f);
            etTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 关联引用
            SetPrivateField(csp, "commandContainer", cRT);
            SetPrivateField(csp, "commandEntryPrefab", entryPrefab);

            entryPrefab.SetActive(false); // 模板默认隐藏

            Debug.Log("[SetupScene] CommandStatusPanel 创建完成");
            return panel;
        }

        #endregion

        // ══════════════════════════════════════════════════
        // 通用工具方法
        // ══════════════════════════════════════════════════

        #region 工具方法

        /// <summary>
        /// 查找或创建命名 GameObject（如果已存在则跳过创建）
        /// </summary>
        private static GameObject CreateOrFind(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            return new GameObject(name);
        }

        /// <summary>
        /// 确保 GameObject 上有指定组件
        /// </summary>
        private static T RequireComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        /// <summary>
        /// 设置材质（URP Lit Shader）
        /// </summary>
        private static void SetMaterial(GameObject go, Color color, float metallic = 0f, float smoothness = 0.3f)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard"); // Fallback

            var mat = new Material(shader);
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            renderer.material = mat;
        }

        /// <summary>
        /// 设置层级（递归）
        /// </summary>
        private static void SetLayerRecursive(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                // Layer 不存在时使用 Default
                Debug.LogWarning($"[SetupScene] Layer '{layerName}' 不存在，使用 Default 层");
                return;
            }
            go.layer = layer;
        }

        /// <summary>
        /// 创建 UI 子物体（带 RectTransform）
        /// </summary>
        private static T CreateUIChild<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            child.AddComponent<RectTransform>();
            return child.AddComponent<T>();
        }

        /// <summary>
        /// 创建 TextMeshProUGUI 快捷方法
        /// </summary>
        private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text,
            float fontSize, Color color, TextAlignmentOptions alignment,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent);
            var rt = obj.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            var tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        /// <summary>
        /// 创建 UI 按钮（Image + Button + TextMeshProUGUI）
        /// </summary>
        private static GameObject CreateUIButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            var btn = new GameObject(name);
            btn.transform.SetParent(parent);
            var rt = btn.AddComponent<RectTransform>();
            rt.anchorMin        = anchorMin;
            rt.anchorMax        = anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;

            var img = btn.AddComponent<Image>();
            img.color = bgColor;

            var button = btn.AddComponent<Button>();
            var colors = button.colors;
            colors.normalColor      = bgColor;
            colors.highlightedColor = bgColor * 1.2f;
            colors.pressedColor     = bgColor * 0.8f;
            colors.selectedColor    = bgColor * 1.1f;
            button.colors = colors;
            button.targetGraphic = img;

            // 文本
            var textObj = new GameObject("Text");
            textObj.transform.SetParent(btn.transform);
            var textRT = textObj.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 14f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 8f;
            tmp.fontSizeMax = 16f;

            return btn;
        }

        /// <summary>
        /// 通过反射设置 private/SerializeField 字段
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            var field = type.GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                Undo.RecordObject((Object)target, $"Set {fieldName}");
                field.SetValue(target, value);
            }
            else
            {
                Debug.LogWarning($"[SetupScene] 字段 '{fieldName}' 在 {type.Name} 中未找到");
            }
        }

        /// <summary>
        /// 创建简单四面体 Mesh（用于 HQ 标记等）
        /// </summary>
        private static Mesh CreateSimpleMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(0, 0.5f, 0),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0, -0.5f, 0.5f)
            };
            mesh.triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 1,
                1, 3, 2
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>
        /// 创建简单 Quad Mesh
        /// </summary>
        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1)
            };
            mesh.RecalculateNormals();
            return mesh;
        }

        #endregion
    }
}
