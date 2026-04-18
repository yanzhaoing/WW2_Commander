// MedievalUI.cs — 中世纪游戏 UI 控制器
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SWO1.Medieval
{
    public class MedievalUI : MonoBehaviour
    {
        public static MedievalUI Instance { get; private set; }

        [Header("面板引用（运行时自动查找）")]
        public GameObject ReportPanel;       // 骑士汇报区
        public InputField CommandInput;      // 指令输入
        public Text TurnText;                // 回合显示
        public Text StatusText;              // 状态概览
        public Text EnemyText;               // 敌军情报
        public GameObject PopupPanel;        // 弹窗（村庄/敌人/地形）
        public Image PopupImage;             // 弹窗图片
        public Text PopupText;               // 弹窗文字
        public GameObject GameOverPanel;     // 游戏结束面板
        public Text GameOverText;

        // 将军选择
        public static string SelectedKnight = "唐吉诃德";
        private string[] KnightNames = { "唐吉诃德", "兰斯洛特", "熙德", "全军" };

        private ScrollRect _reportScroll;
        private Transform _reportContent;

        void Awake() { Instance = this; }

        void Start()
        {
            FindReferences();
            BindButtons();
            SubscribeEvents();
            UpdateUI();

            // 隐藏弹窗和结束面板
            if (PopupPanel != null) PopupPanel.SetActive(false);
            if (GameOverPanel != null) GameOverPanel.SetActive(false);
        }

        void FindReferences()
        {
            // 自动查找 UI 引用
            if (ReportPanel == null) ReportPanel = GameObject.Find("ReportPanel");
            if (CommandInput == null)
            {
                var go = GameObject.Find("CommandInput");
                if (go != null) CommandInput = go.GetComponent<InputField>();
            }
            if (TurnText == null)
            {
                var go = GameObject.Find("TurnText");
                if (go != null) TurnText = go.GetComponent<Text>();
            }
            if (StatusText == null)
            {
                var go = GameObject.Find("StatusText");
                if (go != null) StatusText = go.GetComponent<Text>();
            }
            if (EnemyText == null)
            {
                var go = GameObject.Find("EnemyText");
                if (go != null) EnemyText = go.GetComponent<Text>();
            }

            // 查找汇报区 ScrollRect
            var scrollGo = GameObject.Find("ReportScroll");
            if (scrollGo != null)
            {
                _reportScroll = scrollGo.GetComponent<ScrollRect>();
                if (_reportScroll != null)
                    _reportContent = _reportScroll.content;
            }

            // 弹窗
            if (PopupPanel == null) PopupPanel = GameObject.Find("PopupPanel");
            if (GameOverPanel == null) GameOverPanel = GameObject.Find("GameOverPanel");
        }

        void BindButtons()
        {
            BindBtn("BtnSendCommand", OnSendCommand);
            BindBtn("BtnNextTurn", OnNextTurn);
            BindBtn("BtnClosePopup", OnClosePopup);

            // 骑士选择按钮
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                BindBtn($"Gen_{i}", () => OnSelectKnight(idx));
            }
        }

        void BindBtn(string name, UnityEngine.Events.UnityAction action)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                var btn = go.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(action);
            }
        }

        void SubscribeEvents()
        {
            var director = MedievalGameDirector.Instance;
            if (director == null) return;

            director.OnMessage += AddReport;
            director.OnKnightReport += (name, msg) => AddReport($"[{name}] {msg}");
            director.OnTerrainTrigger += ShowTerrainPopup;
            director.OnImageTrigger += ShowImagePopup;
            director.OnEnemyDiscovered += OnEnemyFound;
            director.OnCombatResult += AddReport;
            director.OnTurnChanged += OnTurnUpdate;
            director.OnGameOver += OnGameOver;
        }

        #region 按钮事件

        void OnSendCommand()
        {
            if (CommandInput == null || string.IsNullOrEmpty(CommandInput.text)) return;

            string command = CommandInput.text;
            AddReport($"[总指挥] {command}");

            var director = MedievalGameDirector.Instance;
            if (director != null)
            {
                // 先让骑士 LLM 回复
                var targetKnight = FindTargetKnight(command);
                if (targetKnight != null)
                {
                    KnightAI.Instance?.GenerateResponse(targetKnight, command, "", (response) =>
                    {
                        AddReport($"[{targetKnight.DisplayName}] {response}");
                    });
                }

                // 执行指令
                director.ExecuteCommand(command);
            }

            CommandInput.text = "";
            UpdateUI();
        }

        void OnNextTurn()
        {
            // 直接进入下一回合（跳过指令）
            AddReport("[总指挥] 待命观察...");
            UpdateUI();
        }

        void OnSelectKnight(int index)
        {
            SelectedKnight = KnightNames[index];
            Debug.Log($"[MedievalUI] 选中: {SelectedKnight}");
        }

        void OnClosePopup()
        {
            if (PopupPanel != null) PopupPanel.SetActive(false);
        }

        #endregion

        #region 事件处理

        void OnEnemyFound(EnemyData enemy)
        {
            AddReport($"🔍 发现敌军：{enemy.DisplayName}！人数约{enemy.Troops}人，战意{enemy.Morale}。");

            // 敌军心理活动
            var director = MedievalGameDirector.Instance;
            if (director != null && director.Knights.Count > 0)
            {
                string thought = KnightAI.Instance?.GenerateEnemyThought(enemy, director.Knights[0]);
                if (!string.IsNullOrEmpty(thought))
                    AddReport($"敌军心声：{thought}");
            }
        }

        void OnTurnUpdate(int turn)
        {
            if (TurnText != null)
                TurnText.text = $"第 {turn} 回合 / 20";
            UpdateUI();
        }

        void OnGameOver(GameState state)
        {
            if (GameOverPanel != null) GameOverPanel.SetActive(true);
            if (GameOverText != null)
            {
                switch (state)
                {
                    case GameState.Victory:
                        GameOverText.text = "三路敌骑已平，王土安宁。\n\n完美胜利！";
                        GameOverText.color = new Color(0.8f, 0.9f, 0.3f);
                        break;
                    case GameState.Defeat:
                        GameOverText.text = "三路骑兵全灭，敌骑愈发猖獗。\n王国的荣耀，碎在了这片荒野。";
                        GameOverText.color = new Color(0.9f, 0.3f, 0.3f);
                        break;
                }
            }
        }

        void ShowTerrainPopup(string text)
        {
            if (PopupPanel == null) return;
            PopupPanel.SetActive(true);
            if (PopupText != null) PopupText.text = text;
        }

        void ShowImagePopup(string imageType)
        {
            // 图片触发（村庄/敌人）
            Debug.Log($"[MedievalUI] 图片触发: {imageType}");
        }

        #endregion

        #region UI 更新

        void AddReport(string message)
        {
            if (_reportContent == null)
            {
                // 尝试用旧的 RadioPanel
                var content = GameObject.Find("RadioPanel/ScrollRect/Viewport/Content");
                if (content != null) _reportContent = content.transform;
            }

            if (_reportContent == null) return;

            var go = new GameObject("Msg");
            go.transform.SetParent(_reportContent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 24f;
            le.flexibleWidth = 1f;
            var t = go.AddComponent<Text>();
            t.text = message;
            t.fontSize = 15;
            t.color = new Color(0.85f, 0.85f, 0.75f);
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.supportRichText = true;

            // 自动滚动到底部
            if (_reportScroll != null)
                Canvas.ForceUpdateCanvases();
        }

        void UpdateUI()
        {
            var director = MedievalGameDirector.Instance;
            if (director == null) return;

            // 更新回合
            if (TurnText != null)
                TurnText.text = $"第 {director.CurrentTurn} 回合 / {director.MaxTurns}";

            // 更新骑士状态
            if (StatusText != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var k in director.Knights)
                {
                    string status = k.IsAlive ? $"人数{k.Troops} 战意{k.Morale}" : "已阵亡";
                    string icon = k.IsAlive ? "🟢" : "💀";
                    sb.AppendLine($"{icon} {k.DisplayName}：{status}");
                }
                StatusText.text = sb.ToString();
            }

            // 更新敌军情报
            if (EnemyText != null)
            {
                var sb = new System.Text.StringBuilder();
                var discovered = director.GetDiscoveredEnemies();
                if (discovered.Count == 0)
                {
                    sb.AppendLine("暂无敌军情报");
                }
                else
                {
                    foreach (var e in discovered)
                    {
                        string icon = e.Type == EnemyType.EnemyKnight ? "⚔️" : "🏴";
                        sb.AppendLine($"{icon} {e.DisplayName}：人数{e.Troops} 战意{e.Morale}");
                    }
                }
                // 显示未发现的敌人数量
                int undiscovered = director.Enemies.Count - discovered.Count - director.Enemies.FindAll(e => e.IsDefeated).Count;
                if (undiscovered > 0)
                    sb.AppendLine($"未知敌军：{undiscovered}队");
                EnemyText.text = sb.ToString();
            }
        }

        KnightData FindTargetKnight(string command)
        {
            var director = MedievalGameDirector.Instance;
            if (director == null) return null;

            foreach (var k in director.Knights)
            {
                if (k.IsAlive && command.Contains(k.DisplayName))
                    return k;
            }
            // 如果没有指定，默认返回第一个活着的骑士
            return director.Knights.Find(k => k.IsAlive);
        }

        #endregion
    }
}
