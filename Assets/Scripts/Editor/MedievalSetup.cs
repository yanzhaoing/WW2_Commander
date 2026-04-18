// MedievalSetup.cs — 中世纪骑兵场景搭建（编辑器一键创建）
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using SWO1.Medieval;

namespace SWO1.Editor
{
    public static class MedievalSetup
    {
        private const string MenuPath = "WW2 Commander/Setup Medieval Scene";

        private static readonly Color C_DarkBg    = new Color(0.1f, 0.08f, 0.05f, 0.9f);
        private static readonly Color C_Amber     = HexColor("d4a84b");
        private static readonly Color C_Green     = HexColor("2a7a3a");
        private static readonly Color C_Blue      = HexColor("3a5a8a");
        private static readonly Color C_White     = Color.white;
        private static readonly Color C_DimText   = new Color(0.5f, 0.5f, 0.4f);
        private static readonly Color C_DimOverlay = new Color(0, 0, 0, 0.5f);

        [MenuItem(MenuPath, priority = 53)]
        public static void Execute()
        {
            ClearScene();

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();
            CreateMap(canvas.transform);
            CreateLeftPanel(canvas.transform);
            CreatePopupPanel(canvas.transform);
            CreateGameOverPanel(canvas.transform);
            CreateGameSystems();

            // 保存场景
            string scenePath = "Assets/Scenes/Medieval.unity";
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Scenes");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene, scenePath);

            // 添加到 Build Settings
            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == scenePath))
            {
                buildScenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            Debug.Log("[MedievalSetup] ✅ 中世纪骑兵场景搭建完成！");
            EditorUtility.DisplayDialog("铁蹄 — KNIGHT'S MARCH",
                "场景搭建完成！\n\n点击 Play 即可体验。", "OK");
        }

        static void ClearScene()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots) Object.DestroyImmediate(r);
        }

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(16, 16, -10);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 18f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.12f, 0.08f);
            go.AddComponent<AudioListener>();
            go.AddComponent<SWO1.CommandPost.CameraController2D>();
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static GameObject CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        // ── 地图渲染 ──
        static void CreateMap(Transform canvas)
        {
            var mapGo = new GameObject("[MedievalMap]");
            var mapRenderer = mapGo.AddComponent<MedievalMapRenderer>();

            // 尝试加载 Tiled 地图
            string tmjPath = System.IO.Path.Combine(Application.dataPath, "Map/world.tmj");
            string pngPath = System.IO.Path.Combine(Application.dataPath, "Map/punyworld-overworld-tileset.png");

            if (System.IO.File.Exists(tmjPath))
            {
                mapRenderer.MapJson = new TextAsset(System.IO.File.ReadAllText(tmjPath));
                Debug.Log("[MedievalSetup] 已加载 Tiled 地图: world.tmj");
            }

            if (System.IO.File.Exists(pngPath))
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(System.IO.File.ReadAllBytes(pngPath));
                tex.filterMode = FilterMode.Point;
                mapRenderer.TilesetTexture = tex;
                Debug.Log("[MedievalSetup] 已加载 tileset: punyworld-overworld-tileset.png");
            }
        }

        // ── 左侧面板（飞鸽传书 + 指令 + 状态 + 敌军）──
        static void CreateLeftPanel(Transform canvas)
        {
            // 飞鸽传书面板（上半部分 55%）
            var reportPanel = CreatePanel(canvas, "ReportPanel",
                new Vector2(0, 0.45f), new Vector2(0.25f, 1f), C_DarkBg);
            AddText(reportPanel.transform, "Title", "📜 飞鸽传书", 20, C_Amber,
                TextAnchor.MiddleLeft, new Vector2(0.05f, 0.92f), new Vector2(0.95f, 0.99f));

            // ScrollRect for reports
            var scrollGo = new GameObject("ReportScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(reportPanel.transform, false);
            SetRect(scrollGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.90f));
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot = new Vector2(0.5f, 1);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 3f; vlg.padding = new RectOffset(6, 6, 4, 4);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = cRT;

            // 示例消息
            AddRadioMsg(content.transform, "唐吉诃德：总指挥！我的骑兵已整装待发，随时可以发起光荣的冲锋！");
            AddRadioMsg(content.transform, "兰斯洛特：斥候已就位，前方区域暂无敌情。");
            AddRadioMsg(content.transform, "熙德：阁下，马匹状态良好，建议尽快出发。");

            // 指令面板（中下部分 25%）
            var cmdPanel = CreatePanel(canvas, "CommandPanel",
                new Vector2(0, 0.20f), new Vector2(0.25f, 0.45f), C_DarkBg);
            AddText(cmdPanel.transform, "Title", "🐎 下达指令", 18, C_Amber,
                TextAnchor.MiddleLeft, new Vector2(0.05f, 0.85f), new Vector2(0.95f, 0.98f));

            // 骑士选择按钮
            string[] names = { "唐吉诃德", "兰斯洛特", "熙德", "全军" };
            Color[] colors = { HexColor("c04040"), HexColor("4060c0"), HexColor("c08020"), C_Green };
            for (int i = 0; i < 4; i++)
            {
                float xMin = 0.03f + i * 0.24f;
                float xMax = 0.03f + (i + 1) * 0.24f - 0.02f;
                Btn(cmdPanel.transform, $"Gen_{i}", names[i],
                    new Vector2(xMin, 0.60f), new Vector2(xMax, 0.82f), colors[i]);
            }

            // 指令输入
            var inputGo = new GameObject("CommandInput", typeof(RectTransform));
            inputGo.transform.SetParent(cmdPanel.transform, false);
            SetRect(inputGo.GetComponent<RectTransform>(), new Vector2(0.03f, 0.30f), new Vector2(0.97f, 0.55f));
            var inputBg = inputGo.AddComponent<Image>();
            inputBg.color = new Color(0.08f, 0.06f, 0.04f, 0.9f);

            var textArea = new GameObject("TextArea");
            textArea.transform.SetParent(inputGo.transform, false);
            textArea.AddComponent<RectTransform>().anchorMin = Vector2.zero;
            textArea.GetComponent<RectTransform>().anchorMax = Vector2.one;
            textArea.AddComponent<RectMask2D>();

            var dispGo = new GameObject("DisplayText");
            dispGo.transform.SetParent(textArea.transform, false);
            var dRT = dispGo.AddComponent<RectTransform>();
            dRT.anchorMin = Vector2.zero; dRT.anchorMax = Vector2.one;
            dRT.offsetMin = new Vector2(4, 2); dRT.offsetMax = new Vector2(-4, -2);
            var dTxt = dispGo.AddComponent<Text>();
            dTxt.fontSize = 16; dTxt.color = C_White;
            dTxt.alignment = TextAnchor.MiddleLeft;
            dTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            var phRT = phGo.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
            phRT.offsetMin = new Vector2(4, 2); phRT.offsetMax = new Vector2(-4, -2);
            var phTxt = phGo.AddComponent<Text>();
            phTxt.text = "例：唐吉诃德，带领骑兵前往蒙德镇";
            phTxt.fontSize = 16; phTxt.fontStyle = FontStyle.Italic;
            phTxt.color = C_DimText; phTxt.alignment = TextAnchor.MiddleLeft;
            phTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var inputField = inputGo.AddComponent<InputField>();
            inputField.textComponent = dTxt;
            inputField.placeholder = phTxt;

            // 按钮
            Btn(cmdPanel.transform, "BtnSendCommand", "🕊️ 发送飞鸽",
                new Vector2(0.03f, 0.03f), new Vector2(0.48f, 0.25f), C_Green);
            Btn(cmdPanel.transform, "BtnNextTurn", "⏳ 待命观察",
                new Vector2(0.52f, 0.03f), new Vector2(0.97f, 0.25f), HexColor("555555"));

            // 状态面板（底部 20%）
            var statusPanel = CreatePanel(canvas, "StatusPanel",
                new Vector2(0, 0f), new Vector2(0.25f, 0.20f), C_DarkBg);

            AddText(statusPanel.transform, "TurnText", "第 1 回合 / 20", 17, C_Amber,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.97f));

            AddText(statusPanel.transform, "StatusText", "🟢 唐吉诃德：人数100 战意90\n🟢 兰斯洛特：人数120 战意60\n🟢 熙德：人数110 战意75",
                14, C_White, TextAnchor.UpperLeft, new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.78f));

            AddText(statusPanel.transform, "EnemyText", "⚔️ 敌军情报\n暂无敌军情报",
                14, new Color(0.9f, 0.5f, 0.3f), TextAnchor.UpperLeft, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.38f));
        }

        // ── 弹窗面板 ──
        static void CreatePopupPanel(Transform canvas)
        {
            var popup = new GameObject("PopupPanel");
            popup.transform.SetParent(canvas, false);
            var rt = popup.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = popup.AddComponent<Image>();
            bg.color = C_DimOverlay;

            var box = new GameObject("Box");
            box.transform.SetParent(popup.transform, false);
            var boxRT = box.AddComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.25f, 0.30f);
            boxRT.anchorMax = new Vector2(0.75f, 0.70f);
            boxRT.offsetMin = Vector2.zero; boxRT.offsetMax = Vector2.zero;
            var boxBg = box.AddComponent<Image>();
            boxBg.color = HexColor("1a1a2e", 0.95f);

            AddText(box.transform, "PopupText", "", 20, C_White,
                TextAnchor.MiddleCenter, new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.85f));

            Btn(box.transform, "BtnClosePopup", "✕ 关闭",
                new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.15f), HexColor("555555"));

            popup.SetActive(false);
        }

        // ── 游戏结束面板 ──
        static void CreateGameOverPanel(Transform canvas)
        {
            var panel = new GameObject("GameOverPanel");
            panel.transform.SetParent(canvas, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            AddText(panel.transform, "GameOverText", "", 40, C_Amber,
                TextAnchor.MiddleCenter, new Vector2(0.2f, 0.3f), new Vector2(0.8f, 0.7f));

            panel.SetActive(false);
        }

        // ── 游戏系统 ──
        static void CreateGameSystems()
        {
            var mapGo = new GameObject("[MapParser]");
            mapGo.AddComponent<MapParser>();

            var directorGo = new GameObject("[MedievalGameDirector]");
            directorGo.AddComponent<MedievalGameDirector>();

            var knightAIGo = new GameObject("[KnightAI]");
            knightAIGo.AddComponent<KnightAI>();

            // LLM Client（复用现有的）
            if (Object.FindFirstObjectByType<SWO1.AI.LLMClient>() == null)
            {
                var llmGo = new GameObject("[LLMClient]");
                llmGo.AddComponent<SWO1.AI.LLMClient>();
            }

            var uiGo = new GameObject("[MedievalUI]");
            uiGo.AddComponent<MedievalUI>();
        }

        // ── 工具方法 ──

        static GameObject CreatePanel(Transform parent, string name, Vector2 min, Vector2 max, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            SetRect(rt, min, max);
            go.AddComponent<Image>().color = bg;
            return go;
        }

        static Text AddText(Transform parent, string name, string text, int size, Color color,
            TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SetRect(go.AddComponent<RectTransform>(), min, max);
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        static void AddRadioMsg(Transform content, string text)
        {
            var go = new GameObject("Msg");
            go.transform.SetParent(content, false);
            go.AddComponent<LayoutElement>().minHeight = 24f;
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = 15;
            t.color = new Color(0.85f, 0.85f, 0.75f);
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        static GameObject Btn(Transform parent, string name, string label,
            Vector2 min, Vector2 max, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            SetRect(go.AddComponent<RectTransform>(), min, max);
            var img = go.AddComponent<Image>();
            img.color = bg;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = bg; c.highlightedColor = bg * 1.2f;
            c.pressedColor = bg * 0.8f; btn.colors = c;

            var txt = new GameObject("Text");
            txt.transform.SetParent(go.transform, false);
            var trt = txt.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            var t = txt.AddComponent<Text>();
            t.text = label; t.fontSize = 16; t.fontStyle = FontStyle.Bold;
            t.color = C_White; t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return go;
        }

        static void SetRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min; rt.anchorMax = max;
            rt.anchoredPosition = Vector2.zero; rt.sizeDelta = Vector2.zero;
        }

        static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c)) { c.a = alpha; return c; }
            return new Color(1, 0, 1, alpha);
        }
    }
}
