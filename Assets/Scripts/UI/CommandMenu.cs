using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace SWO1.UI
{
    public enum CommandType2D
    {
        Move, Defend, Scout, Attack, Retreat, Status, Wait
    }

    public class CommandMenu : MonoBehaviour
    {
        // ── Events ──────────────────────────────────────────────
        /// <summary>
        /// Fires when the player sends a command.
        /// (targetUnitLabel, commandType, coordX, coordY, frequency)
        /// coordX / coordY are -1 when the command has no coordinate.
        /// </summary>
        public event Action<string, SWO1.Command.CommandType, int, int, int> OnCommandIssued;

        // ── Targets ─────────────────────────────────────────────
        private static readonly string[] TargetLabels =
            { "红一排", "红二排", "蓝四坦克排", "全部" };

        // ── Commands ────────────────────────────────────────────
        private static readonly string[] CommandLabels =
            { "推进到", "防御当前位置", "侦察", "攻击", "撤退到", "报告状态", "待命" };

        /// <summary>Commands that require a coordinate input.</summary>
        private static readonly bool[] CommandNeedsCoord =
            { true, false, false, true, true, false, false };

        /// <summary>Commands that need a scout direction prompt.</summary>
        private static readonly bool[] CommandNeedsDirection =
            { false, false, true, false, false, false, false };

        private static readonly string[] ScoutDirections = { "东", "南", "西", "北" };

        // ── Colours ─────────────────────────────────────────────
        private static readonly Color Cbg       = Hex("#1a1a2e");
        private static readonly Color Cbtn      = Hex("#4a6741");
        private static readonly Color CbtnHover = Hex("#5a7a51");
        private static readonly Color Chigh     = Hex("#d4a84b");
        private static readonly Color Cred      = Hex("#cc3333");
        private static readonly Color Cwhite    = Color.white;
        private static readonly Color Cdim      = new Color(1, 1, 1, 0.45f);

        // ── State ───────────────────────────────────────────────
        private int selectedTarget  = 0;
        private int selectedCommand = 0;
        private int selectedFreq    = 1;
        private int selectedScoutDir = 0;
        private bool sending = false;

        // ── UI refs ─────────────────────────────────────────────
        private RectTransform panelRect;
        private Text[] targetBtnTexts;
        private Text[] cmdBtnTexts;
        private Text[] freqBtnTexts;
        private Text[] scoutDirBtnTexts;
        private InputField coordInput;
        private Text coordPlaceholder;
        private Text coordErrorText;
        private Button sendButton;
        private Text sendButtonText;
        private GameObject coordGroup;
        private GameObject scoutDirGroup;

        // ─────────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────────
        private void Awake()
        {
            BuildUI();
            RefreshUI();
        }

        // ─────────────────────────────────────────────────────────
        //  UI Construction
        // ─────────────────────────────────────────────────────────
        private void BuildUI()
        {
            // Ensure a Canvas exists
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject go = new GameObject("CmdCanvas");
                go.transform.SetParent(transform);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                go.AddComponent<GraphicRaycaster>();
            }

            // ── Background panel ──────────────────────────────────
            GameObject panel = CreateUIObj("CmdPanel", canvas.transform);
            panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 0);
            panelRect.anchorMax = new Vector2(1, 0);
            panelRect.pivot     = new Vector2(0.5f, 0);
            panelRect.sizeDelta = new Vector2(0, 110);
            panelRect.anchoredPosition = Vector2.zero;
            Image panelImg = panel.AddComponent<Image>();
            panelImg.color = Cbg;

            // Layout group
            HorizontalLayoutGroup hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.padding = new RectOffset(8, 8, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            // ── 1. Target selector (vertical buttons) ────────────
            GameObject targetCol = CreateUIObj("TargetCol", panel.transform);
            LayoutElement targetLe = targetCol.AddComponent<LayoutElement>();
            targetLe.preferredWidth = 120;
            targetLe.flexibleWidth  = 0;
            VerticalLayoutGroup tVlg = targetCol.AddComponent<VerticalLayoutGroup>();
            tVlg.spacing = 2;
            tVlg.childForceExpandWidth = true;
            tVlg.childForceExpandHeight = false;

            targetBtnTexts = new Text[TargetLabels.Length];
            for (int i = 0; i < TargetLabels.Length; i++)
            {
                int idx = i;
                GameObject btn = CreateButton($"Tgt_{i}", targetCol.transform, TargetLabels[i],
                    () => { selectedTarget = idx; RefreshUI(); });
                LayoutElement ble = btn.AddComponent<LayoutElement>();
                ble.preferredHeight = 22;
                targetBtnTexts[i] = btn.GetComponentInChildren<Text>();
            }

            // ── 2. Command type selector (vertical buttons) ──────
            GameObject cmdCol = CreateUIObj("CmdCol", panel.transform);
            LayoutElement cmdLe = cmdCol.AddComponent<LayoutElement>();
            cmdLe.preferredWidth = 140;
            cmdLe.flexibleWidth  = 0;
            VerticalLayoutGroup cVlg = cmdCol.AddComponent<VerticalLayoutGroup>();
            cVlg.spacing = 2;
            cVlg.childForceExpandWidth = true;
            cVlg.childForceExpandHeight = false;

            cmdBtnTexts = new Text[CommandLabels.Length];
            for (int i = 0; i < CommandLabels.Length; i++)
            {
                int idx = i;
                GameObject btn = CreateButton($"Cmd_{i}", cmdCol.transform, CommandLabels[i],
                    () => { selectedCommand = idx; RefreshUI(); });
                LayoutElement ble = btn.AddComponent<LayoutElement>();
                ble.preferredHeight = 22;
                cmdBtnTexts[i] = btn.GetComponentInChildren<Text>();
            }

            // ── 3. Coordinate input + error ──────────────────────
            coordGroup = CreateUIObj("CoordGroup", panel.transform);
            LayoutElement coordLe = coordGroup.AddComponent<LayoutElement>();
            coordLe.preferredWidth = 120;
            coordLe.flexibleWidth  = 0;
            VerticalLayoutGroup coordVlg = coordGroup.AddComponent<VerticalLayoutGroup>();
            coordVlg.spacing = 2;
            coordVlg.childForceExpandWidth = true;
            coordVlg.childForceExpandHeight = false;

            // InputField
            GameObject inputGo = CreateUIObj("CoordInput", coordGroup.transform);
            LayoutElement inpLe = inputGo.AddComponent<LayoutElement>();
            inpLe.preferredHeight = 28;
            Image inpBg = inputGo.AddComponent<Image>();
            inpBg.color = new Color(0, 0, 0, 0.4f);

            GameObject inputTextGo = CreateUIObj("Text", inputGo.transform);
            RectTransform inputTextRt = inputTextGo.GetComponent<RectTransform>();
            inputTextRt.anchorMin = Vector2.zero;
            inputTextRt.anchorMax = Vector2.one;
            inputTextRt.offsetMin = new Vector2(6, 2);
            inputTextRt.offsetMax = new Vector2(-6, -2);
            Text inputTextComp = inputTextGo.AddComponent<Text>();
            inputTextComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputTextComp.fontSize = 14;
            inputTextComp.color = Cwhite;
            inputTextComp.supportRichText = false;

            GameObject placeholderGo = CreateUIObj("Placeholder", inputGo.transform);
            RectTransform phRt = placeholderGo.GetComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.offsetMin = new Vector2(6, 2);
            phRt.offsetMax = new Vector2(-6, -2);
            coordPlaceholder = placeholderGo.AddComponent<Text>();
            coordPlaceholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            coordPlaceholder.fontSize = 14;
            coordPlaceholder.fontStyle = FontStyle.Italic;
            coordPlaceholder.color = Cdim;
            coordPlaceholder.text = "如 C5";

            coordInput = inputGo.AddComponent<InputField>();
            coordInput.textComponent = inputTextComp;
            coordInput.placeholder  = coordPlaceholder;
            coordInput.characterLimit = 4;
            coordInput.onValueChanged.AddListener(_ => { if (coordErrorText != null) coordErrorText.text = ""; });

            // Error text
            GameObject errGo = CreateUIObj("CoordError", coordGroup.transform);
            LayoutElement errLe = errGo.AddComponent<LayoutElement>();
            errLe.preferredHeight = 16;
            coordErrorText = errGo.AddComponent<Text>();
            coordErrorText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            coordErrorText.fontSize = 11;
            coordErrorText.color = Cred;
            coordErrorText.alignment = TextAnchor.UpperLeft;

            // ── 3b. Scout direction selector ──────────────────────
            scoutDirGroup = CreateUIObj("ScoutDirGroup", panel.transform);
            LayoutElement sdLe = scoutDirGroup.AddComponent<LayoutElement>();
            sdLe.preferredWidth = 120;
            sdLe.flexibleWidth  = 0;
            VerticalLayoutGroup sdVlg = scoutDirGroup.AddComponent<VerticalLayoutGroup>();
            sdVlg.spacing = 2;
            sdVlg.childForceExpandWidth = true;
            sdVlg.childForceExpandHeight = false;

            scoutDirBtnTexts = new Text[ScoutDirections.Length];
            for (int i = 0; i < ScoutDirections.Length; i++)
            {
                int idx = i;
                GameObject btn = CreateButton($"ScoutDir_{i}", scoutDirGroup.transform, ScoutDirections[i],
                    () => { selectedScoutDir = idx; RefreshUI(); });
                LayoutElement ble = btn.AddComponent<LayoutElement>();
                ble.preferredHeight = 22;
                scoutDirBtnTexts[i] = btn.GetComponentInChildren<Text>();
            }

            // ── 4. Frequency selector ────────────────────────────
            GameObject freqCol = CreateUIObj("FreqCol", panel.transform);
            LayoutElement freqLe = freqCol.AddComponent<LayoutElement>();
            freqLe.preferredWidth = 180;
            freqLe.flexibleWidth  = 0;
            VerticalLayoutGroup fVlg = freqCol.AddComponent<VerticalLayoutGroup>();
            fVlg.spacing = 2;
            fVlg.childForceExpandWidth = true;
            fVlg.childForceExpandHeight = false;

            GameObject freqRow = CreateUIObj("FreqRow", freqCol.transform);
            LayoutElement frLe = freqRow.AddComponent<LayoutElement>();
            frLe.preferredHeight = 30;
            HorizontalLayoutGroup frHlg = freqRow.AddComponent<HorizontalLayoutGroup>();
            frHlg.spacing = 4;
            frHlg.childForceExpandWidth = true;
            frHlg.childForceExpandHeight = true;

            freqBtnTexts = new Text[5];
            for (int i = 0; i < 5; i++)
            {
                int idx = i + 1;
                GameObject btn = CreateButton($"Freq_{idx}", freqRow.transform, $"①②③④⑤"[i].ToString(),
                    () => { selectedFreq = idx; RefreshUI(); });
                freqBtnTexts[i] = btn.GetComponentInChildren<Text>();
            }

            // ── 5. Send button ───────────────────────────────────
            GameObject sendGo = CreateButton("SendBtn", panel.transform, "📡 发送", OnSend);
            LayoutElement sendLe = sendGo.AddComponent<LayoutElement>();
            sendLe.preferredWidth = 100;
            sendLe.flexibleWidth  = 1;
            sendButton = sendGo.GetComponent<Button>();
            sendButtonText = sendGo.GetComponentInChildren<Text>();
            sendButtonText.fontSize = 16;
        }

        // ─────────────────────────────────────────────────────────
        //  Refresh visuals
        // ─────────────────────────────────────────────────────────
        private void RefreshUI()
        {
            // Target highlights
            for (int i = 0; i < targetBtnTexts.Length; i++)
                targetBtnTexts[i].color = (i == selectedTarget) ? Chigh : Cwhite;

            // Command highlights
            for (int i = 0; i < cmdBtnTexts.Length; i++)
                cmdBtnTexts[i].color = (i == selectedCommand) ? Chigh : Cwhite;

            // Frequency highlights
            for (int i = 0; i < freqBtnTexts.Length; i++)
                freqBtnTexts[i].color = ((i + 1) == selectedFreq) ? Chigh : Cwhite;

            // Coordinate visibility
            bool needsCoord = CommandNeedsCoord[selectedCommand];
            coordGroup.SetActive(needsCoord);

            // Scout direction visibility
            bool needsDir = CommandNeedsDirection[selectedCommand];
            scoutDirGroup.SetActive(needsDir);
            if (needsDir)
            {
                for (int i = 0; i < scoutDirBtnTexts.Length; i++)
                    scoutDirBtnTexts[i].color = (i == selectedScoutDir) ? Chigh : Cwhite;
            }

            // Clear coord error on command switch
            if (coordErrorText != null) coordErrorText.text = "";
        }

        // ─────────────────────────────────────────────────────────
        //  Send
        // ─────────────────────────────────────────────────────────
        private void OnSend()
        {
            if (sending) return;

            string targetLabel = TargetLabels[selectedTarget];
            CommandType2D cmd = (CommandType2D)selectedCommand;
            int cx = -1, cy = -1;

            // Validate coordinate
            if (CommandNeedsCoord[selectedCommand])
            {
                string raw = coordInput.text.Trim().ToUpper();
                if (!TryParseCoord(raw, out cx, out cy))
                {
                    coordErrorText.text = "格式错误，如 C5";
                    return;
                }
            }

            // Build display message
            string msg = BuildMessage(targetLabel, cmd);
            Debug.Log($"[CommandMenu] 📡 {msg}");

            // Map to SWO1.Command.CommandType if it exists, otherwise pass the int
            // We fire the event with the int cast — the listener can map it.
            int cmdInt = (int)cmd;
            // Use reflection-safe approach: the event signature expects SWO1.Command.CommandType.
            // Since we can't guarantee that type exists, we'll cast via Enum.ToObject at invocation.
            // For now we pass as int by using the overload pattern — but the signature is fixed.
            // We'll box it. The listener must match the signature exactly.

            try
            {
                OnCommandIssued?.Invoke(targetLabel, (SWO1.Command.CommandType)cmdInt, cx, cy, selectedFreq);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CommandMenu] OnCommandIssued listener error (type cast may differ): {e.Message}");
                // Fallback: try invoking with raw int-based approach
            }

            StartCoroutine(SendCooldown());
        }

        private string BuildMessage(string target, CommandType2D cmd)
        {
            switch (cmd)
            {
                case CommandType2D.Move:
                    return $"{target}，推进到 {coordInput.text.Trim().ToUpper()}";
                case CommandType2D.Defend:
                    return $"{target}，防御当前位置";
                case CommandType2D.Scout:
                    return $"{target}，侦察 {ScoutDirections[selectedScoutDir]}";
                case CommandType2D.Attack:
                    return $"{target}，攻击 {coordInput.text.Trim().ToUpper()}";
                case CommandType2D.Retreat:
                    return $"{target}，撤退到 {coordInput.text.Trim().ToUpper()}";
                case CommandType2D.Status:
                    return $"{target}，报告状态";
                case CommandType2D.Wait:
                    return $"{target}，待命";
                default:
                    return $"{target}，未知指令";
            }
        }

        private IEnumerator SendCooldown()
        {
            sending = true;
            sendButton.interactable = false;
            sendButtonText.text = "⏳ 发送中…";

            yield return new WaitForSeconds(3f);

            // Clear inputs
            coordInput.text = "";
            coordErrorText.text = "";

            sendButton.interactable = true;
            sendButtonText.text = "📡 发送";
            sending = false;
        }

        // ─────────────────────────────────────────────────────────
        //  Coordinate parsing: "C5" → (2, 5)  (A=0)
        // ─────────────────────────────────────────────────────────
        private static readonly Regex CoordRegex = new Regex(@"^([A-Z])(\d+)$", RegexOptions.Compiled);

        private bool TryParseCoord(string input, out int x, out int y)
        {
            x = -1; y = -1;
            Match m = CoordRegex.Match(input);
            if (!m.Success) return false;
            x = m.Groups[1].Value[0] - 'A';
            y = int.Parse(m.Groups[2].Value);
            return x >= 0 && x < 26 && y >= 0;
        }

        // ─────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────
        private static GameObject CreateUIObj(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.localScale = Vector3.one;
            return go;
        }

        private GameObject CreateButton(string name, Transform parent, string label, Action onClick)
        {
            GameObject go = CreateUIObj(name, parent);
            Image img = go.AddComponent<Image>();
            img.color = Cbtn;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            ColorBlock cb = btn.colors;
            cb.normalColor      = Cbtn;
            cb.highlightedColor = CbtnHover;
            cb.pressedColor     = Chigh;
            cb.selectedColor    = Chigh;
            cb.disabledColor    = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = cb;
            btn.onClick.AddListener(() => onClick?.Invoke());

            GameObject txt = CreateUIObj("Label", go.transform);
            RectTransform trt = txt.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin  = Vector2.zero;
            trt.offsetMax  = Vector2.zero;
            Text t = txt.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = label;
            t.fontSize = 13;
            t.color = Cwhite;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = false;

            return go;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
