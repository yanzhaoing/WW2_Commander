// SetupScene2D.cs — WW2 Commander 2D 场景自动搭建脚本
// 菜单入口: WW2 Commander → Setup 2D Scene
// 自动创建纯 2D 俯视沙盘场景
//
// 注意: 使用 UnityEngine.UI (不依赖 TextMeshPro)
//       使用内置 Sprites-Default shader (不依赖 URP)
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;

namespace SWO1.Editor
{
    public static class SetupScene2D
    {
        private const string MenuPath = "WW2 Commander/Setup 2D Scene";
        private const int CanvasWidth  = 1920;
        private const int CanvasHeight = 1080;

        // ── 颜色常量 ─────────────────────────────────────
        private static readonly Color C_DarkBg       = HexColor("1a1a2e", 0.85f);
        private static readonly Color C_MilGreen     = HexColor("4a6741");
        private static readonly Color C_Amber        = HexColor("d4a84b");
        private static readonly Color C_Red          = HexColor("c01010");
        private static readonly Color C_CameraBg     = HexColor("1a1a1e");
        private static readonly Color C_White        = Color.white;
        private static readonly Color C_GrayText     = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color C_DimText      = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color C_Sand         = new Color(0.6f, 0.55f, 0.4f);
        private static readonly Color C_PopupDim     = new Color(0f, 0f, 0f, 0.6f);

        // SandTable_BG 引用
        private static GameObject sandTableBg;

        // ══════════════════════════════════════════════════
        [MenuItem(MenuPath, priority = 51)]
        public static void Execute()
        {
            ClearScene();
            Create2DCamera();
            CreateSandTableBackground();
            CreateGridMap();
            CreateCoreSystems();
            CreateAdditionalCoreSystems();
            var canvas = CreateCanvas();
            CreateEventSystem();
            var leftPanel = CreateLeftPanel(canvas.transform);
            CreateBattleReportPanel(leftPanel.transform);
            CreateCommandInputPanel(leftPanel.transform);
            CreateStatusOverviewPanel(leftPanel.transform);
            CreateSettlementPanel(canvas.transform);

            Debug.Log("[SetupScene2D] ✅ 2D 场景搭建完成！");
            EditorUtility.DisplayDialog("WW2 Commander — 2D",
                "2D 场景搭建完成！\n\n请在 Unity 中查看 Hierarchy 面板。",
                "OK");
        }

        // ══════════════════════════════════════════════════
        // 1. 清理场景
        // ══════════════════════════════════════════════════
        private static void ClearScene()
        {
            var roots = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene().GetRootGameObjects();
            foreach (var r in roots) Object.DestroyImmediate(r);
            Debug.Log("[SetupScene2D] 场景已清理");
        }

        // 沙盘右移偏移量（为左侧面板留出空间）
        private const float GridOffsetX = 24f;

        // ══════════════════════════════════════════════════
        // 2. 2D 摄像机（调整位置补偿左侧面板）
        // ══════════════════════════════════════════════════
        private static void Create2DCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            // 摄像机位置居中（地图在世界空间 GridOffsetX,0 到 32+GridOffsetX,24）
            go.transform.position = new Vector3(16f + GridOffsetX, 12f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 14f;
            cam.nearClipPlane    = 0.3f;
            cam.farClipPlane     = 20f;
            cam.depth            = 0;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = C_CameraBg;

            go.AddComponent<AudioListener>();
            go.AddComponent<SWO1.CommandPost.CameraController2D>();
            go.AddComponent<SWO1.CommandPost.InputManager2D>();
            go.AddComponent<SWO1.UI.CellHoverInfo>();
            Debug.Log("[SetupScene2D] Main Camera 创建完成 (位置已调整补偿左侧面板)");
        }

        // ══════════════════════════════════════════════════
        // 3. 沙盘背景（空，不放棋子）
        // ══════════════════════════════════════════════════
        private static void CreateSandTableBackground()
        {
            sandTableBg = new GameObject("SandTable_BG");
            // 不再使用静态背景图，网格地图会自行渲染
            Debug.Log("[SetupScene2D] 沙盘背景父物体创建完成");
        }

        // ══════════════════════════════════════════════════
        // 3A. 网格地图渲染（空白地图，只有网格线）—— 放在世界空间，不放 Canvas 下
        // ══════════════════════════════════════════════════
        private static void CreateGridMap()
        {
            // GridMap 放在场景根节点（不在 Canvas 下），这样 collider 坐标和相机世界空间一致
            var gridMap = new GameObject("GridMap");
            // 不设置 parent，直接放在场景根节点
            // 右移为左侧 UI 面板留出空间
            gridMap.transform.position = new Vector3(GridOffsetX, 0f, 0f);

            const int MapWidth = 32;
            const int MapHeight = 24;
            const float CellSize = 1f;

            // 创建白色像素 Sprite 备用
            Sprite whitePixel = CreateWhitePixelSprite();

            // 创建空白格子（半透明背景，便于点击）
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    CreateEmptyGridCell(gridMap.transform, x, y, CellSize, whitePixel);
                }
            }

            // 创建网格线
            CreateGridLines(gridMap.transform, MapWidth, MapHeight, CellSize);

            // 创建坐标标注
            CreateCoordinateLabels(gridMap.transform, MapWidth, MapHeight, CellSize);

            // 注册到 SandTable2D（如果存在）
            var sandTable = UnityEngine.Object.FindFirstObjectByType<SWO1.UI.SandTable2D>();
            if (sandTable != null)
            {
                // SandTable2D 会在 Start 中自行构建层级
            }

            Debug.Log("[SetupScene2D] 空白网格地图创建完成 (16×12)，位于世界空间，等待玩家侦察标注");
        }

        private static void CreateEmptyGridCell(Transform parent, int x, int y, float cellSize, Sprite whitePixel)
        {
            var cell = new GameObject($"Cell_{x}_{y}");
            cell.transform.SetParent(parent, false);
            cell.transform.localPosition = new Vector3(x * cellSize, y * cellSize, 0f);

            var sr = cell.AddComponent<SpriteRenderer>();
            sr.sprite = whitePixel;
            sr.color = new Color(0.3f, 0.3f, 0.35f, 0.3f); // 半透明格子背景
            sr.sortingOrder = -10;
            cell.transform.localScale = new Vector3(cellSize, cellSize, 1f);

            // 添加碰撞器用于点击检测
            var col = cell.AddComponent<BoxCollider2D>();
            col.size = new Vector2(cellSize, cellSize);
        }

        private static void CreateGridLines(Transform parent, int width, int height, float cellSize)
        {
            Color gridColor = new Color(0.5f, 0.5f, 0.55f, 0.8f);
            Sprite whitePixel = CreateWhitePixelSprite();

            // 竖线
            for (int x = 0; x <= width; x++)
            {
                var line = new GameObject($"VLine_{x}");
                line.transform.SetParent(parent, false);
                line.transform.localPosition = new Vector3(x * cellSize, height * cellSize * 0.5f, 0f);
                
                var sr = line.AddComponent<SpriteRenderer>();
                sr.sprite = whitePixel;
                sr.color = gridColor;
                sr.sortingOrder = -5;
                line.transform.localScale = new Vector3(0.04f, height * cellSize, 1f);
            }

            // 横线
            for (int y = 0; y <= height; y++)
            {
                var line = new GameObject($"HLine_{y}");
                line.transform.SetParent(parent, false);
                line.transform.localPosition = new Vector3(width * cellSize * 0.5f, y * cellSize, 0f);
                
                var sr = line.AddComponent<SpriteRenderer>();
                sr.sprite = whitePixel;
                sr.color = gridColor;
                sr.sortingOrder = -5;
                line.transform.localScale = new Vector3(width * cellSize, 0.04f, 1f);
            }
        }

        private static Sprite CreateWhitePixelSprite()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        private static void CreateCoordinateLabels(Transform parent, int width, int height, float cellSize)
        {
            // 顶部列标注 A-P
            for (int x = 0; x < width; x++)
            {
                char col = (char)('A' + x);
                var labelGo = new GameObject($"ColLabel_{col}");
                labelGo.transform.SetParent(parent, false);
                labelGo.transform.localPosition = new Vector3(x * cellSize + cellSize * 0.5f, height * cellSize + 0.3f, 0f);

                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = col.ToString();
                tm.fontSize = 10;
                tm.characterSize = 0.15f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 1f, 1f, 0.7f);
            }

            // 左侧行标注 1-12
            for (int y = 0; y < height; y++)
            {
                int row = y + 1;
                var labelGo = new GameObject($"RowLabel_{row}");
                labelGo.transform.SetParent(parent, false);
                labelGo.transform.localPosition = new Vector3(-0.4f, y * cellSize + cellSize * 0.5f, 0f);

                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = row.ToString();
                tm.fontSize = 10;
                tm.characterSize = 0.15f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(1f, 1f, 1f, 0.7f);
            }
        }

        // ══════════════════════════════════════════════════
        // 4. 核心系统 GameObjects
        // ══════════════════════════════════════════════════
        private static void CreateCoreSystems()
        {
            CreateSingleton("[GameDirector]",   typeof(SWO1.Core.GameDirector));
            CreateSingleton("[GameEventBus]",   typeof(SWO1.Core.GameEventBus));
            CreateSingleton("[BattleSimulator]",typeof(SWO1.Simulation.BattleSimulator));

            // AudioManager 挂载两个组件
            var audioGo = new GameObject("[AudioManager]");
            audioGo.AddComponent<SWO1.Audio.SimpleAudioManager>();
            audioGo.AddComponent<SWO1.Audio.AudioEvents>();

            Debug.Log("[SetupScene2D] 核心系统创建完成 (4 个)");
        }

        // ══════════════════════════════════════════════════
        // 4A. 额外的核心系统 GameObjects (GameController2D 等)
        // ══════════════════════════════════════════════════
        private static void CreateAdditionalCoreSystems()
        {
            // GameController2D - 2D侦察战斗主控制器
            var gameControllerGo = new GameObject("[GameController2D]");
            gameControllerGo.AddComponent<SWO1.Core.GameController2D>();

            // MapGenerator - 地图生成器
            var mapGeneratorGo = new GameObject("[MapGenerator]");
            mapGeneratorGo.AddComponent<SWO1.Map.MapGenerator>();

            // RadioSystem - 无线电系统
            var radioSystemGo = new GameObject("[RadioSystem]");
            radioSystemGo.AddComponent<SWO1.Radio.RadioSystem>();

            // EnemySpawner - 敌人生成器
            var enemySpawnerGo = new GameObject("[EnemySpawner]");
            enemySpawnerGo.AddComponent<SWO1.Simulation.EnemySpawner>();

            // SandTable2D - 2D沙盘UI
            var sandTableGo = new GameObject("[SandTable2D]");
            sandTableGo.transform.position = new Vector3(GridOffsetX, 0f, 0f);
            sandTableGo.AddComponent<SWO1.UI.SandTable2D>();

            // 按钮事件绑定 + 无线电 UI 桥接
            var binderGo = new GameObject("[CommandMenuBinder]");
            binderGo.AddComponent<SWO1.UI.CommandMenuBinder>();

            // UI 按钮事件绑定（运行时）
            var uiBinderGo = new GameObject("[GameUIBinder]");
            uiBinderGo.AddComponent<SWO1.UI.GameUIBinder>();

            var bridgeGo = new GameObject("[RadioUIBridge]");
            bridgeGo.AddComponent<SWO1.UI.RadioUIBridge>();

            // TurnManager - 回合制控制器
            var turnMgrGo = new GameObject("[TurnManager]");
            turnMgrGo.AddComponent<SWO1.Core.TurnManager>();

            // LLMClient - 小米 API 客户端
            var llmGo = new GameObject("[LLMClient]");
            llmGo.AddComponent<SWO1.AI.LLMClient>();

            // 三位 AI 将军
            var rommelGo = new GameObject("[General_Rommel]");
            var rommel = rommelGo.AddComponent<SWO1.AI.GeneralAI>();
            rommel.Initialize(SWO1.AI.GeneralAI.CreateRommel());
            rommel.GridPosition = new Vector2(4, 2);

            var mansteinGo = new GameObject("[General_Manstein]");
            var manstein = mansteinGo.AddComponent<SWO1.AI.GeneralAI>();
            manstein.Initialize(SWO1.AI.GeneralAI.CreateManstein());
            manstein.GridPosition = new Vector2(8, 1);

            var guderianGo = new GameObject("[General_Guderian]");
            var guderian = guderianGo.AddComponent<SWO1.AI.GeneralAI>();
            guderian.Initialize(SWO1.AI.GeneralAI.CreateGuderian());
            guderian.GridPosition = new Vector2(12, 2);

            // UI 组件
            // BattleReportUI 和 CommandInputUI 已由 SetupScene2D 直接创建面板替代，不再需要
            // var battleReportGo = new GameObject("[BattleReportUI]");
            // battleReportGo.AddComponent<SWO1.UI.BattleReportUI>();
            // var cmdInputGo = new GameObject("[CommandInputUI]");
            // cmdInputGo.AddComponent<SWO1.UI.CommandInputUI>();

            Debug.Log("[SetupScene2D] 额外核心系统创建完成 (GameController2D, MapGenerator, RadioSystem, EnemySpawner, SandTable2D, CommandMenuBinder, RadioUIBridge, TurnManager, LLMClient, 3x GeneralAI, BattleReportUI, CommandInputUI)");
        }

        private static void CreateSingleton(string name, System.Type type)
        {
            var go = new GameObject(name);
            go.AddComponent(type);
        }

        // ══════════════════════════════════════════════════
        // 5. Canvas + EventSystem
        // ══════════════════════════════════════════════════
        private static GameObject CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            Debug.Log("[SetupScene2D] Canvas 创建完成 (1920×1080)");
            return go;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            Debug.Log("[SetupScene2D] EventSystem 创建完成");
        }

        // ══════════════════════════════════════════════════
        // 6A. LeftPanel（左侧父面板）
        // ══════════════════════════════════════════════════
        private static GameObject CreateLeftPanel(Transform canvas)
        {
            var panel = CreatePanel(canvas, "LeftPanel",
                new Vector2(0f, 0f), new Vector2(0.25f, 1.0f), new Color(0, 0, 0, 0));
            Debug.Log("[SetupScene2D] LeftPanel 创建完成");
            return panel;
        }

        // ══════════════════════════════════════════════════
        // 6B. RadioPanel（无线电汇报，占左侧上半部分50%）
        // ══════════════════════════════════════════════════
        private static void CreateBattleReportPanel(Transform leftPanel)
        {
            var panel = CreatePanel(leftPanel, "RadioPanel",
                new Vector2(0f, 0.5f), new Vector2(1f, 1.0f), HexColor("1a1a2e", 0.9f));

            // 标题
            AddText(panel.transform, "Title", "📻 将军汇报",
                20, C_Amber, TextAnchor.MiddleLeft,
                new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.97f));

            // ScrollRect
            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRT = SetRect(scrollGo.GetComponent<RectTransform>(),
                new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.86f));
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollGo.AddComponent<SWO1.UI.RadioPanelScrollHelper>();

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            Stretch(vpRT);
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 1f);
            cRT.anchorMax = new Vector2(1f, 1f);
            cRT.pivot     = new Vector2(0.5f, 1f);
            cRT.sizeDelta = Vector2.zero;
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth   = true;
            vlg.childControlHeight  = true;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 3f;
            vlg.padding = new RectOffset(6, 6, 4, 4);
            content.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = cRT;

            // 示例消息（将军回复）
            AddRadioMsg(content.transform, "隆美尔: 收到，总部。我的装甲营已经就位，燃油补给还剩七成，弹药充足。如果今晚能拿下B5高地，明天就能从侧翼包抄敌军。建议批准推进。");
            AddRadioMsg(content.transform, "曼施坦因: 总部，侧翼侦察完成。东面树林有轻微动静，但不构成威胁。建议保持警戒，同时准备迂回C7方向。");
            AddRadioMsg(content.transform, "古德里安: 装甲部队状态良好，三个营全部满编。如果给我一个回合准备，我可以从D3发起钳形攻势。");

            Debug.Log("[SetupScene2D] BattleReportPanel 创建完成");
        }

        private static void AddRadioMsg(Transform content, string text)
        {
            var go = new GameObject("Msg");
            go.transform.SetParent(content, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            le.flexibleWidth = 1f;
            var t = go.AddComponent<Text>();
            t.text      = text;
            t.fontSize  = 16;
            t.color     = C_White;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.supportRichText = true;
            t.alignByGeometry = true;
        }

        // ══════════════════════════════════════════════════
        // 6C. CommandInputPanel（命令输入面板，占左侧中部30%）
        // ══════════════════════════════════════════════════
        private static void CreateCommandInputPanel(Transform leftPanel)
        {
            var panel = CreatePanel(leftPanel, "CommandInputPanel",
                new Vector2(0f, 0.2f), new Vector2(1f, 0.5f), HexColor("1a1a2e", 0.9f));

            // 将军选择（4 个按钮替代 Dropdown）
            string[] generals = { "隆美尔", "曼施坦因", "古德里安", "全体" };
            Color[] genColors = {
                HexColor("c04040"),
                HexColor("4060c0"),
                HexColor("505050"),
                HexColor("4a6741")
            };
            for (int i = 0; i < 4; i++)
            {
                float xMin = 0.05f + i * 0.24f;
                float xMax = 0.05f + (i + 1) * 0.24f - 0.02f;
                Btn(panel.transform, $"Gen_{i}", generals[i],
                    new Vector2(xMin, 0.72f), new Vector2(xMax, 0.92f),
                    genColors[i]);
            }

            // 指令输入 InputField
            var inputGo = new GameObject("CommandInput", typeof(RectTransform));
            inputGo.transform.SetParent(panel.transform, false);
            var inputRT = SetRect(inputGo.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.65f));

            var inputBg = AddImage(inputGo.transform, "BG",
                new Color(0.1f, 0.1f, 0.12f, 0.9f));
            Stretch(inputBg.GetComponent<RectTransform>());

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGo.transform, false);
            Stretch(textArea.AddComponent<RectTransform>());
            textArea.AddComponent<RectMask2D>();

            var dispGo = new GameObject("DisplayText");
            dispGo.transform.SetParent(textArea.transform, false);
            var dRT = Stretch(dispGo.AddComponent<RectTransform>());
            dRT.offsetMin = new Vector2(4f, 2f);
            dRT.offsetMax = new Vector2(-4f, -2f);
            var dTxt = dispGo.AddComponent<Text>();
            dTxt.fontSize  = 16;
            dTxt.color     = C_White;
            dTxt.alignment = TextAnchor.MiddleLeft;
            dTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            var phRT = Stretch(phGo.AddComponent<RectTransform>());
            phRT.offsetMin = new Vector2(4f, 2f);
            phRT.offsetMax = new Vector2(-4f, -2f);
            var phTxt = phGo.AddComponent<Text>();
            phTxt.text      = "输入指令...";
            phTxt.fontSize  = 16;
            phTxt.fontStyle = FontStyle.Italic;
            phTxt.color     = C_DimText;
            phTxt.alignment = TextAnchor.MiddleLeft;
            phTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var inputField = inputGo.AddComponent<InputField>();
            inputField.textComponent = dTxt;
            inputField.placeholder   = phTxt;
            inputField.contentType = InputField.ContentType.Standard;

            // 下达指令按钮（绿色）
            Btn(panel.transform, "BtnSendCommand", "📡 下达指令",
                new Vector2(0.05f, 0.08f), new Vector2(0.48f, 0.32f),
                HexColor("2a7a3a"));

            // 下一回合按钮（琥珀色）
            Btn(panel.transform, "BtnNextTurn", "▶ 下一回合",
                new Vector2(0.52f, 0.08f), new Vector2(0.95f, 0.32f),
                HexColor("d4a84b"));

            Debug.Log("[SetupScene2D] CommandInputPanel 创建完成");
        }

        // ══════════════════════════════════════════════════
        // 6D. StatusOverviewPanel（状态概览，占左侧底部20%）
        // ══════════════════════════════════════════════════
        private static void CreateStatusOverviewPanel(Transform leftPanel)
        {
            var panel = CreatePanel(leftPanel, "StatusOverviewPanel",
                new Vector2(0f, 0f), new Vector2(1f, 0.2f), HexColor("1a1a2e", 0.9f));

            // 回合显示
            AddText(panel.transform, "TurnText", "回合: 1/30",
                17, C_Amber, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.97f));

            // 侦察与交火报告
            AddText(panel.transform, "ReconReport", "📡 侦察报告: 等待侦察兵汇报...",
                15, C_White, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.42f), new Vector2(0.95f, 0.78f));

            AddText(panel.transform, "ContactReport", "⚔️ 交火记录: 暂无交火",
                15, new Color(0.9f, 0.5f, 0.3f), TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.02f), new Vector2(0.95f, 0.40f));

            Debug.Log("[SetupScene2D] StatusOverviewPanel → ReconReportPanel 创建完成");
        }

        // ══════════════════════════════════════════════════
        // 工具方法: 创建 Dropdown（含 Template）
        // ══════════════════════════════════════════════════
        private static void CreateDropdown(Transform parent, string[] options)
        {
            var go = new GameObject("Dropdown", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);

            // 背景
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            // Label
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(8, 2);
            labelRT.offsetMax = new Vector2(-25, -2);
            var labelTxt = labelGo.AddComponent<Text>();
            labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelTxt.fontSize = 16;
            labelTxt.color = C_White;
            labelTxt.alignment = TextAnchor.MiddleLeft;

            // Arrow
            var arrowGo = new GameObject("Arrow", typeof(RectTransform));
            arrowGo.transform.SetParent(go.transform, false);
            var arrowRT = arrowGo.GetComponent<RectTransform>();
            arrowRT.anchorMin = new Vector2(1, 0.5f);
            arrowRT.anchorMax = new Vector2(1, 0.5f);
            arrowRT.pivot = new Vector2(1, 0.5f);
            arrowRT.anchoredPosition = new Vector2(-8, 0);
            arrowRT.sizeDelta = new Vector2(14, 14);
            var arrowImg = arrowGo.AddComponent<Image>();
            arrowImg.color = C_GrayText;

            // Template
            var templateGo = new GameObject("Template", typeof(RectTransform));
            templateGo.transform.SetParent(go.transform, false);
            var templateRT = templateGo.GetComponent<RectTransform>();
            templateRT.anchorMin = new Vector2(0, 0);
            templateRT.anchorMax = new Vector2(1, 0);
            templateRT.pivot = new Vector2(0.5f, 1);
            templateRT.anchoredPosition = new Vector2(0, 0);
            templateRT.sizeDelta = new Vector2(0, 100);

            var templateImg = templateGo.AddComponent<Image>();
            templateImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

            // Template 必须有 CanvasGroup（Dropdown.Show() 用它做渐变）
            templateGo.AddComponent<CanvasGroup>();

            // Viewport
            var vpGo = new GameObject("Viewport", typeof(RectTransform));
            vpGo.transform.SetParent(templateGo.transform, false);
            var vpRT = vpGo.GetComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            vpRT.offsetMin = new Vector2(2, 2);
            vpRT.offsetMax = new Vector2(-2, -2);

            var vpImg = vpGo.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            vpGo.AddComponent<Mask>().showMaskGraphic = false;

            // Content
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(vpGo.transform, false);
            var contentRT = contentGo.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 28 * options.Length);

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 0;

            // Item
            var itemGo = new GameObject("Item", typeof(RectTransform));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRT = itemGo.GetComponent<RectTransform>();
            itemRT.anchorMin = new Vector2(0, 1);
            itemRT.anchorMax = new Vector2(1, 1);
            itemRT.pivot = new Vector2(0.5f, 1);
            itemRT.sizeDelta = new Vector2(0, 28);

            var itemBg = itemGo.AddComponent<Image>();
            itemBg.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            var itemToggle = itemGo.AddComponent<Toggle>();

            // Item Label
            var itemLabelGo = new GameObject("Item Label", typeof(RectTransform));
            itemLabelGo.transform.SetParent(itemGo.transform, false);
            var itemLabelRT = itemLabelGo.GetComponent<RectTransform>();
            itemLabelRT.anchorMin = Vector2.zero;
            itemLabelRT.anchorMax = Vector2.one;
            itemLabelRT.offsetMin = new Vector2(8, 2);
            itemLabelRT.offsetMax = new Vector2(-8, -2);
            var itemLabelTxt = itemLabelGo.AddComponent<Text>();
            itemLabelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemLabelTxt.fontSize = 16;
            itemLabelTxt.color = C_White;
            itemLabelTxt.alignment = TextAnchor.MiddleLeft;

            // Dropdown component
            var dropdown = go.AddComponent<Dropdown>();
            dropdown.targetGraphic = bg;
            dropdown.template = templateRT;
            dropdown.captionText = labelTxt;
            dropdown.itemText = itemLabelTxt;

            // Add options
            foreach (var opt in options)
            {
                dropdown.options.Add(new Dropdown.OptionData(opt));
            }
            dropdown.value = 0;
            dropdown.RefreshShownValue();

            templateGo.SetActive(false);
        }

        // ══════════════════════════════════════════════════
        // 6D. SettlementPanel（中央，默认隐藏）
        // ══════════════════════════════════════════════════
        private static void CreateSettlementPanel(Transform canvas)
        {
            // 全屏暗化遮罩
            var overlay = new GameObject("SettlementPanel");
            overlay.transform.SetParent(canvas, false);
            var ovRT = Stretch(overlay.AddComponent<RectTransform>());
            AddImage(overlay.transform, "Dim", C_PopupDim);
            overlay.AddComponent<CanvasGroup>();

            // 弹窗
            var popup = new GameObject("Popup");
            popup.transform.SetParent(overlay.transform, false);
            var pRT = SetRect(popup.AddComponent<RectTransform>(),
                new Vector2(0.25f, 0.20f), new Vector2(0.75f, 0.80f));
            AddImage(popup.transform, "BG", new Color(0.1f, 0.1f, 0.15f, 0.95f));

            // 标题
            AddText(popup.transform, "Title", "战斗结算",
                26, C_Amber, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f));

            Divider(popup.transform, 0.80f);

            // 结果
            AddText(popup.transform, "Result",
                "战斗结果: --\n消灭敌军: --\n我方损失: --\n作战时长: --",
                28, C_White, TextAnchor.UpperLeft,
                new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.78f));

            // 重新开始按钮
            Btn(popup.transform, "RestartBtn", "🔄 重新开始",
                new Vector2(0.30f, 0.06f), new Vector2(0.70f, 0.20f),
                C_MilGreen);

            overlay.SetActive(false);
            Debug.Log("[SetupScene2D] SettlementPanel 创建完成（默认隐藏）");

            // 指令弹窗（回合结束后弹出"请下达指令"）
            var cmdOverlay = new GameObject("CommandPopup");
            cmdOverlay.transform.SetParent(canvas, false);
            Stretch(cmdOverlay.AddComponent<RectTransform>());
            AddImage(cmdOverlay.transform, "Dim", new Color(0f, 0f, 0f, 0.4f));

            var cmdBox = new GameObject("Box");
            cmdBox.transform.SetParent(cmdOverlay.transform, false);
            SetRect(cmdBox.AddComponent<RectTransform>(),
                new Vector2(0.30f, 0.38f), new Vector2(0.70f, 0.58f));
            AddImage(cmdBox.transform, "BG", new Color(0.12f, 0.12f, 0.18f, 0.95f));

            AddText(cmdBox.transform, "Title", "📻 将军汇报完毕",
                20, C_Amber, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.60f), new Vector2(0.95f, 0.95f));

            AddText(cmdBox.transform, "Hint", "请下达指令，然后点击确认",
                17, C_White, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.58f));

            Btn(cmdBox.transform, "BtnConfirm", "✅ 确认，等待下一回合",
                new Vector2(0.15f, 0.03f), new Vector2(0.85f, 0.28f),
                HexColor("2a7a3a"));

            cmdOverlay.SetActive(false);
            Debug.Log("[SetupScene2D] CommandPopup 创建完成（默认隐藏）");
        }

        // ══════════════════════════════════════════════════
        // 工具方法
        // ══════════════════════════════════════════════════

        private static GameObject CreatePanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            SetRect(rt, anchorMin, anchorMax);
            var img = go.AddComponent<Image>();
            img.color = bg;
            return go;
        }

        private static Text AddText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SetRect(go.AddComponent<RectTransform>(), anchorMin, anchorMax);
            var t = go.AddComponent<Text>();
            t.text      = content;
            t.fontSize  = fontSize;
            t.color     = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.supportRichText = false;
            t.alignByGeometry = true;
            return t;
        }

        private static Image AddImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static GameObject Btn(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bg, UnityEngine.Events.UnityAction onClick = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = SetRect(go.AddComponent<RectTransform>(), anchorMin, anchorMax);
            var img = go.AddComponent<Image>();
            img.color = bg;

            var btn = go.AddComponent<Button>();
            var c = btn.colors;
            c.normalColor      = bg;
            c.highlightedColor = bg * 1.3f;
            c.pressedColor     = bg * 0.7f;
            c.selectedColor    = bg * 1.2f;
            btn.colors = c;
            btn.targetGraphic = img;

            if (onClick != null)
                btn.onClick.AddListener(onClick);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            Stretch(txtGo.AddComponent<RectTransform>());
            var t = txtGo.AddComponent<Text>();
            t.text      = label;
            t.fontSize  = 16;
            t.color     = C_White;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return go;
        }

        private static void Divider(Transform parent, float y)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            SetRect(go.AddComponent<RectTransform>(),
                new Vector2(0.03f, y), new Vector2(0.97f, y + 0.006f));
            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
        }

        private static RectTransform SetRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin        = min;
            rt.anchorMax        = max;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
            return rt;
        }

        private static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin        = Vector2.zero;
            rt.anchorMax        = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = Vector2.zero;
            return rt;
        }

        private static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            return new Color(1, 0, 1, alpha); // magenta = error
        }
    }
}
