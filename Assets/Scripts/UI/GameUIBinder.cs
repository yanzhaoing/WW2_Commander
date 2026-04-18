// GameUIBinder.cs — 运行时绑定按钮事件
using UnityEngine;
using UnityEngine.UI;
using SWO1.Core;
using SWO1.AI;

namespace SWO1.UI
{
    public class GameUIBinder : MonoBehaviour
    {
        private bool _bound = false;
        private GameObject _commandPopup;

        // 将军选择
        public static string SelectedGeneral = "全体";
        private static readonly string[] GenNames = { "隆美尔", "曼施坦因", "古德里安", "全体" };
        private Button[] _genButtons = new Button[4];

        void Start()
        {
            // 延迟一帧确保 TurnManager.Instance 完全就绪
            StartCoroutine(DelayedBind());
        }

        System.Collections.IEnumerator DelayedBind()
        {
            yield return null; // 等一帧
            TryBind();
            // 订阅事件
            var tm = TurnManager.Instance;
            if (tm != null)
            {
                tm.OnCommandPopup += OnCommandPopupChanged;
                tm.OnGeneralReplied += OnGeneralReport;
                Debug.Log("[GameUIBinder] 事件订阅完成");
            }
        }

        void Update()
        {
            if (!_bound) TryBind();
        }

        void TryBind()
        {
            // 绑定下一回合按钮
            var nextTurnBtn = GameObject.Find("BtnNextTurn")?.GetComponent<Button>();
            if (nextTurnBtn != null)
            {
                nextTurnBtn.onClick.AddListener(OnNextTurn);
                Debug.Log("[GameUIBinder] BtnNextTurn 已绑定");
                _bound = true;
            }

            // 绑定下达指令按钮
            var sendBtn = GameObject.Find("BtnSendCommand")?.GetComponent<Button>();
            if (sendBtn != null)
            {
                sendBtn.onClick.AddListener(OnSendCommand);
                Debug.Log("[GameUIBinder] BtnSendCommand 已绑定");
                _bound = true;
            }

            // 绑定弹窗确认按钮
            var confirmBtn = GameObject.Find("BtnConfirm")?.GetComponent<Button>();
            if (confirmBtn != null)
            {
                confirmBtn.onClick.AddListener(OnConfirmReady);
                Debug.Log("[GameUIBinder] BtnConfirm 已绑定");
            }

            // 查找弹窗面板
            if (_commandPopup == null)
            {
                _commandPopup = GameObject.Find("CommandPopup");
            }

            // 绑定将军选择按钮
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var genBtn = GameObject.Find($"Gen_{i}")?.GetComponent<Button>();
                if (genBtn != null)
                {
                    genBtn.onClick.AddListener(() => OnSelectGeneral(idx));
                    _genButtons[i] = genBtn;
                }
            }

            if (!_bound)
                Debug.LogWarning("[GameUIBinder] 按钮未找到，等待重试...");
        }

        void OnCommandPopupChanged(bool show)
        {
            if (_commandPopup == null)
                _commandPopup = GameObject.Find("CommandPopup");
            if (_commandPopup != null)
                _commandPopup.SetActive(show);
        }

        void OnConfirmReady()
        {
            Debug.Log("[GameUIBinder] ✅ 玩家确认，准备下一回合");
            var tm = TurnManager.Instance;
            if (tm != null)
            {
                // 先发送当前指令
                SendCurrentCommand();
                // 通知 TurnManager 玩家已准备好
                tm.OnPlayerReady();
            }
        }

        void OnNextTurn()
        {
            Debug.Log("[GameUIBinder] ▶ 下一回合");
            var tm = TurnManager.Instance;
            if (tm != null)
            {
                SendCurrentCommand();
                tm.OnPlayerReady();
            }
        }

        void OnSendCommand()
        {
            Debug.Log("[GameUIBinder] 📡 下达指令");
            SendCurrentCommand();
        }

        void OnScoutReportMsg(string msg)
        {
            Debug.Log($"📡 侦察报告: {msg}");
            var panel = GameObject.Find("ReconReport");
            if (panel != null)
            {
                var text = panel.GetComponent<Text>();
                if (text != null) text.text = msg;
            }
        }

        void OnUnitStatusMsg(string msg)
        {
            // 部队状态 → 左下角侦察面板
            Debug.Log($"📋 部队状态: {msg}");
            var panel = GameObject.Find("ContactReport");
            if (panel != null)
            {
                var text = panel.GetComponent<Text>();
                if (text != null) text.text = msg;
            }
        }

        void OnGeneralReport(string name, string report)
        {
            // 将军汇报 → RadioPanel
            Debug.Log($"📻 [{name}] {report}");
            var content = GameObject.Find("RadioPanel/ScrollRect/Viewport/Content");
            if (content != null)
            {
                var go = new GameObject("Msg");
                go.transform.SetParent(content.transform, false);
                var le = go.AddComponent<LayoutElement>();
                le.minHeight = 24f;
                le.flexibleWidth = 1f;
                var t = go.AddComponent<Text>();
                t.text = $"[{name}] {report}";
                t.fontSize = 16;
                t.color = new Color(0.8f, 0.9f, 0.6f);
                t.alignment = TextAnchor.UpperLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.supportRichText = true;
                t.alignByGeometry = true;
            }
        }
        void OnSelectGeneral(int index)
        {
            SelectedGeneral = GenNames[index];
            for (int i = 0; i < 4; i++)
            {
                if (_genButtons[i] != null)
                {
                    var colors = _genButtons[i].colors;
                    colors.normalColor = (i == index) ? Color.white : colors.normalColor;
                    _genButtons[i].colors = colors;
                }
            }
            Debug.Log($"[GameUIBinder] 选中将军: {SelectedGeneral}");
        }

        void SendCurrentCommand()
        {
            var inputGo = GameObject.Find("CommandInput");
            {
                var inputField = inputGo.GetComponent<InputField>();
                if (inputField != null && !string.IsNullOrEmpty(inputField.text))
                {
                    string command = inputField.text;
                    Debug.Log($"[GameUIBinder] 指令内容: {command}");
                    var tm = TurnManager.Instance;
                    if (tm != null)
                    {
                        tm.SubmitCommand(SelectedGeneral, command);
                        inputField.text = "";
                    }
                }
            }
        }
    }
}
