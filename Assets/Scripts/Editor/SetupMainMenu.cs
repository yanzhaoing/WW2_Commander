// SetupMainMenu.cs — 主菜单场景自动搭建
// 菜单入口: WW2 Commander → Setup Main Menu
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SWO1.Editor
{
    public static class SetupMainMenu
    {
        private const string MenuPath = "WW2 Commander/Setup Main Menu";
        private const string MenuPath2 = "WW2 Commander/Add Main Menu to GameScene";

        private static readonly Color C_BgDark      = HexColor("0d0d1a");
        private static readonly Color C_TitleColor   = HexColor("d4a84b");
        private static readonly Color C_SubtitleColor = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color C_BtnGreen     = HexColor("2a7a3a");
        private static readonly Color C_BtnBlue      = HexColor("3a5a8a");
        private static readonly Color C_White        = Color.white;
        private static readonly Color C_PopupBg      = HexColor("1a1a2e", 0.95f);
        private static readonly Color C_DimOverlay   = new Color(0f, 0f, 0f, 0.6f);

        [MenuItem(MenuPath, priority = 50)]
        public static void Execute()
        {
            // 创建新场景
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            var canvas = CreateCanvas();
            CreateEventSystem();
            CreateBackground(canvas.transform);
            CreateTitle(canvas.transform);
            CreateButtons(canvas.transform);
            CreateTutorialPanel(canvas.transform);
            CreateFooter(canvas.transform);

            // 添加主菜单管理器
            var go = new GameObject("[MainMenuManager]");
            go.AddComponent<SWO1.UI.MainMenuManager>();

            // 保存场景
            string scenePath = "Assets/Scenes/MainMenu.unity";
            System.IO.Directory.CreateDirectory(Application.dataPath + "/Scenes");
            EditorSceneManager.SaveScene(scene, scenePath);

            Debug.Log("[SetupMainMenu] ✅ 主菜单场景创建完成！");
            EditorUtility.DisplayDialog("WW2 Commander — 主菜单",
                "主菜单场景搭建完成！\n\n请在 Build Settings 中添加场景:\n1. MainMenu\n2. GameScene",
                "OK");
        }

        /// <summary>在当前游戏场景中添加主菜单 UI（不创建新场景）</summary>
        [MenuItem(MenuPath2, priority = 52)]
        public static void AddToCurrentScene()
        {
            // 找到现有 Canvas
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("错误", "当前场景没有 Canvas！请先运行 Setup 2D Scene。", "OK");
                return;
            }

            // 检查是否已有主菜单
            if (canvas.transform.Find("MainMenu") != null)
            {
                EditorUtility.DisplayDialog("提示", "主菜单已存在！", "OK");
                return;
            }

            // 创建主菜单容器
            var menuRoot = new GameObject("MainMenu");
            menuRoot.transform.SetParent(canvas.transform, false);
            var menuRT = menuRoot.AddComponent<RectTransform>();
            Stretch(menuRT);

            // 创建主菜单内容
            CreateBackground(menuRoot.transform);
            CreateTitle(menuRoot.transform);
            CreateButtons(menuRoot.transform);
            CreateTutorialPanel(menuRoot.transform);
            CreateFooter(menuRoot.transform);

            // 添加 MainMenuManager
            var manager = menuRoot.AddComponent<SWO1.UI.MainMenuManager>();
            manager.MainMenuRoot = menuRoot;

            // 尝试找游戏 UI 根节点（通常是 LeftPanel + StatusOverviewPanel 等）
            var leftPanel = canvas.transform.Find("LeftPanel");
            if (leftPanel != null)
            {
                // 游戏 UI 默认隐藏，由 MainMenuManager 控制
                leftPanel.gameObject.SetActive(false);
                manager.GameUIRoot = leftPanel.gameObject;
            }

            Debug.Log("[SetupMainMenu] ✅ 主菜单已添加到当前游戏场景！");
            EditorUtility.DisplayDialog("WW2 Commander",
                "主菜单已添加到当前场景！\n\n" +
                "MainMenuRoot = 主菜单容器\n" +
                "GameUIRoot = LeftPanel (游戏 UI)\n\n" +
                "点击 Play 即可测试。",
                "OK");
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0, 0, -10);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = C_BgDark;
            go.AddComponent<AudioListener>();
        }

        private static GameObject CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private static void CreateBackground(Transform canvas)
        {
            // 全屏深色背景
            var bg = new GameObject("Background");
            bg.transform.SetParent(canvas, false);
            var rt = bg.AddComponent<RectTransform>();
            Stretch(rt);
            var img = bg.AddComponent<Image>();
            img.color = C_BgDark;

            // 装饰线（顶部）
            var topLine = new GameObject("TopLine");
            topLine.transform.SetParent(bg.transform, false);
            var tlRT = topLine.AddComponent<RectTransform>();
            tlRT.anchorMin = new Vector2(0f, 0.92f);
            tlRT.anchorMax = new Vector2(1f, 0.925f);
            tlRT.anchoredPosition = Vector2.zero;
            tlRT.sizeDelta = Vector2.zero;
            var tlImg = topLine.AddComponent<Image>();
            tlImg.color = HexColor("d4a84b", 0.6f);

            // 装饰线（底部）
            var botLine = new GameObject("BotLine");
            botLine.transform.SetParent(bg.transform, false);
            var blRT = botLine.AddComponent<RectTransform>();
            blRT.anchorMin = new Vector2(0f, 0.08f);
            blRT.anchorMax = new Vector2(1f, 0.085f);
            blRT.anchoredPosition = Vector2.zero;
            blRT.sizeDelta = Vector2.zero;
            var blImg = botLine.AddComponent<Image>();
            blImg.color = HexColor("d4a84b", 0.6f);
        }

        private static void CreateTitle(Transform canvas)
        {
            // 主标题
            var title = new GameObject("Title");
            title.transform.SetParent(canvas, false);
            var rt = title.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.55f);
            rt.anchorMax = new Vector2(0.9f, 0.85f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var t = title.AddComponent<Text>();
            t.text = "WW2 COMMANDER";
            t.fontSize = 72;
            t.fontStyle = FontStyle.Bold;
            t.color = C_TitleColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // 副标题
            var sub = new GameObject("Subtitle");
            sub.transform.SetParent(canvas, false);
            var srt = sub.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.15f, 0.48f);
            srt.anchorMax = new Vector2(0.85f, 0.56f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = Vector2.zero;
            var st = sub.AddComponent<Text>();
            st.text = "无线电指挥系统 — 诺曼底登陆";
            st.fontSize = 28;
            st.color = C_SubtitleColor;
            st.alignment = TextAnchor.MiddleCenter;
            st.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void CreateButtons(Transform canvas)
        {
            // 开始游戏按钮
            MakeButton(canvas, "BtnStart", "⚔️  开始游戏",
                new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.42f), C_BtnGreen);

            // 游戏教程按钮
            MakeButton(canvas, "BtnTutorial", "📖  游戏教程",
                new Vector2(0.30f, 0.15f), new Vector2(0.70f, 0.27f), C_BtnBlue);
        }

        private static void MakeButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bg;

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = bg;
            colors.highlightedColor = bg * 1.2f;
            colors.pressedColor = bg * 0.8f;
            colors.selectedColor = bg * 1.1f;
            btn.colors = colors;
            btn.targetGraphic = img;

            // 按钮文字
            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.anchoredPosition = Vector2.zero;
            trt.sizeDelta = Vector2.zero;
            var t = txtGo.AddComponent<Text>();
            t.text = label;
            t.fontSize = 32;
            t.fontStyle = FontStyle.Bold;
            t.color = C_White;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static void CreateTutorialPanel(Transform canvas)
        {
            // 全屏暗化遮罩
            var overlay = new GameObject("TutorialPanel");
            overlay.transform.SetParent(canvas, false);
            var ovRT = overlay.AddComponent<RectTransform>();
            Stretch(ovRT);
            var ovImg = overlay.AddComponent<Image>();
            ovImg.color = C_DimOverlay;
            overlay.AddComponent<CanvasGroup>();

            // 弹窗主体
            var popup = new GameObject("Popup");
            popup.transform.SetParent(overlay.transform, false);
            var pRT = popup.AddComponent<RectTransform>();
            pRT.anchorMin = new Vector2(0.15f, 0.08f);
            pRT.anchorMax = new Vector2(0.85f, 0.92f);
            pRT.anchoredPosition = Vector2.zero;
            pRT.sizeDelta = Vector2.zero;
            var pImg = popup.AddComponent<Image>();
            pImg.color = C_PopupBg;

            // 弹窗标题
            AddText(popup.transform, "Title", "📖 游戏教程",
                36, C_TitleColor, TextAnchor.MiddleCenter,
                new Vector2(0.05f, 0.90f), new Vector2(0.95f, 0.98f));

            // 分隔线
            var divider = new GameObject("Divider");
            divider.transform.SetParent(popup.transform, false);
            var dRT = divider.AddComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.05f, 0.895f);
            dRT.anchorMax = new Vector2(0.95f, 0.90f);
            dRT.anchoredPosition = Vector2.zero;
            dRT.sizeDelta = Vector2.zero;
            divider.AddComponent<Image>().color = HexColor("d4a84b", 0.4f);

            // 教程内容（ScrollRect）
            var scrollGo = new GameObject("ScrollRect");
            scrollGo.transform.SetParent(popup.transform, false);
            var scrollRT = scrollGo.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.05f, 0.10f);
            scrollRT.anchorMax = new Vector2(0.95f, 0.88f);
            scrollRT.anchoredPosition = Vector2.zero;
            scrollRT.sizeDelta = Vector2.zero;
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.anchoredPosition = Vector2.zero;
            vpRT.sizeDelta = Vector2.zero;
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0, 0, 0, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0, 1);
            cRT.anchorMax = new Vector2(1, 1);
            cRT.pivot = new Vector2(0.5f, 1);
            cRT.sizeDelta = new Vector2(0, 800);
            scrollRect.content = cRT;

            // 教程文字
            var tutorialText = content.AddComponent<Text>();
            tutorialText.text = GetTutorialContent();
            tutorialText.fontSize = 20;
            tutorialText.color = C_White;
            tutorialText.alignment = TextAnchor.UpperLeft;
            tutorialText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tutorialText.verticalOverflow = VerticalWrapMode.Overflow;
            tutorialText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tutorialText.supportRichText = true;
            tutorialText.lineSpacing = 1.4f;

            // 关闭按钮
            MakeButton(popup.transform, "BtnCloseTutorial", "✕  关闭",
                new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.08f), HexColor("555555"));
        }

        private static void CreateFooter(Transform canvas)
        {
            AddText(canvas, "Version", "v0.1 — Alpha Build",
                16, new Color(0.4f, 0.4f, 0.4f), TextAnchor.MiddleCenter,
                new Vector2(0.3f, 0.01f), new Vector2(0.7f, 0.06f));
        }

        private static string GetTutorialContent()
        {
            return "<b>【游戏目标】</b>\n\n" +
                "占领地图上的所有桥梁，同时保护你的部队不被全歼。\n" +
                "每个回合你需要做出关键决策，通过无线电向三位将军下达指令。\n\n" +
                "<b>【基本操作】</b>\n\n" +
                "\u2022 左侧面板选择要指挥的将军（隆美尔/曼施坦因/古德里安/全体）\n" +
                "\u2022 在输入框中输入战术指令，点击下达指令按钮\n" +
                "\u2022 点击下一回合按钮进入下一回合\n" +
                "\u2022 右键点击地图格子可以标注敌军/我方位置\n" +
                "\u2022 拖拽标记可以移动标注位置\n\n" +
                "<b>【三位将军】</b>\n\n" +
                "\U0001F534 隆美尔 - 沙漠之狐\n" +
                "激进果断，擅长闪电突袭。喜欢冲锋，但总担心补给。\n\n" +
                "\U0001F535 曼施坦因 - 战略大师\n" +
                "冷静精密，喜欢迂回包抄。每一步都像下棋。\n\n" +
                "\U0001F7E0 古德里安 - 闪电战之父\n" +
                "固执坚定，信奉装甲至上。鄙视分散兵力和静态防御。\n\n" +
                "<b>【地图系统】</b>\n\n" +
                "\u2022 地图初始全黑，需要通过侦察逐步揭开地形\n" +
                "\u2022 绿色格子 = 树林（有掩护）\n" +
                "\u2022 棕色格子 = 村庄（可驻军）\n" +
                "\u2022 蓝色格子 = 河流（不可通行）\n" +
                "\u2022 黄色格子 = 道路（移动加速）\n" +
                "\u2022 桥梁 = 跨越河流的关键目标\n\n" +
                "<b>【无线电系统】</b>\n\n" +
                "将军们通过无线电汇报战况，通讯可能有延迟和干扰。\n" +
                "你需要根据不完整的信息做出决策——这就是战争。\n\n" +
                "<b>【回合流程】</b>\n\n" +
                "1. \U0001F4FB 将军汇报上一回合战况\n" +
                "2. \U0001F4CB 你下达新的战术指令\n" +
                "3. \u2694\uFE0F 部队执行指令（移动/交战）\n" +
                "4. \U0001F504 敌军行动\n" +
                "5. \U0001F4CA 结算检查胜负\n\n" +
                "<b>【胜利条件】</b>\n\n" +
                "\u2705 占领所有桥梁 = 胜利\n" +
                "\u274C 所有部队被歼灭 = 失败\n" +
                "\u23F0 回合数耗尽 = 根据占领情况判定\n\n" +
                "<b>【小贴士】</b>\n\n" +
                "\u2022 不要分散兵力，集中力量突破一点\n" +
                "\u2022 注意弹药补给，弹药耗尽战斗力大幅下降\n" +
                "\u2022 士气很重要，重大伤亡会导致部队撤退\n" +
                "\u2022 侦察优先，知己知彼才能百战百胜\n\n" +
                "祝你好运，指挥官。";
        }

        // ── 工具方法 ──

        private static Text AddText(Transform parent, string name, string content,
            int fontSize, Color color, TextAnchor anchor, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.supportRichText = true;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private static Color HexColor(string hex, float alpha = 1f)
        {
            if (ColorUtility.TryParseHtmlString("#" + hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            return new Color(1, 0, 1, alpha);
        }
    }
}
