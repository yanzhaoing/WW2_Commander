// RadioUIBridge.cs — 无线电汇报到UI面板的桥接脚本
// 订阅 RadioSystem.OnReportDelivered 事件，将汇报显示到 RadioPanel
using UnityEngine;
using UnityEngine.UI;
using SWO1.Radio;

namespace SWO1.UI
{
    public class RadioUIBridge : MonoBehaviour
    {
        [Header("UI 引用")]
        [Tooltip("RadioPanel 下的 Content Transform")]
        public Transform contentTransform;

        [Header("消息显示设置")]
        [Tooltip("正常消息颜色（军事绿）")]
        public Color normalColor = new Color(0.29f, 0.40f, 0.25f); // #4a6741
        [Tooltip("干扰消息颜色（灰色）")]
        public Color interferedColor = new Color(0.5f, 0.5f, 0.5f);
        [Tooltip("最大消息数量")]
        public int maxMessages = 50;
        [Tooltip("文本最小高度")]
        public float minTextHeight = 30f;
        [Tooltip("字体大小")]
        public int fontSize = 14;

        private RadioSystem _radioSystem;
        private ScrollRect _scrollRect;
        private Font _font;

        void Start()
        {
            // 获取内置字体
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 查找 RadioSystem
            _radioSystem = FindFirstObjectByType<RadioSystem>();
            if (_radioSystem == null)
            {
                Debug.LogError("[RadioUIBridge] 未找到 RadioSystem！请确保场景中有 RadioSystem 组件。");
                return;
            }

            // 查找 Content Transform（如果未手动指定）
            if (contentTransform == null)
            {
                contentTransform = FindContentTransform();
            }

            if (contentTransform == null)
            {
                Debug.LogError("[RadioUIBridge] 未找到 RadioPanel/Content！请检查场景结构。");
                return;
            }

            // 查找 ScrollRect 用于自动滚动
            _scrollRect = contentTransform.GetComponentInParent<ScrollRect>();

            // 订阅事件
            _radioSystem.OnReportDelivered += HandleReport;

            Debug.Log("[RadioUIBridge] 已初始化并订阅 RadioSystem 事件");
        }

        void OnDestroy()
        {
            // 取消订阅事件
            if (_radioSystem != null)
            {
                _radioSystem.OnReportDelivered -= HandleReport;
            }
        }

        /// <summary>
        /// 处理汇报消息事件
        /// </summary>
        private void HandleReport(RadioMessage msg)
        {
            if (contentTransform == null) return;

            // 创建新的消息条目
            AddMessageToUI(msg);

            // 限制消息数量
            LimitMessageCount();

            // 自动滚动到底部
            ScrollToBottom();
        }

        /// <summary>
        /// 添加消息到 UI
        /// </summary>
        private void AddMessageToUI(RadioMessage msg)
        {
            // 创建消息 GameObject
            var msgGo = new GameObject("Msg_" + msg.Id);
            msgGo.transform.SetParent(contentTransform, false);
            var msgRt = msgGo.AddComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0f, 0f);
            msgRt.anchorMax = new Vector2(1f, 0f);
            msgRt.pivot = new Vector2(0.5f, 0.5f);

            // 添加 LayoutElement
            var layoutElement = msgGo.AddComponent<LayoutElement>();
            layoutElement.minHeight = minTextHeight;
            layoutElement.flexibleWidth = 1f;

            // 添加 Text 组件
            var text = msgGo.AddComponent<Text>();
            text.text = FormatMessage(msg);
            text.fontSize = fontSize;
            text.color = msg.IsInterfered ? interferedColor : normalColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.font = _font;

            // 确保 Content 的 VerticalLayoutGroup 重新计算
            var vlg = contentTransform.GetComponent<VerticalLayoutGroup>();
            if (vlg != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(vlg.transform as RectTransform);
            }
        }

        /// <summary>
        /// 格式化消息文本: [{time}] {sender}: {content}
        /// </summary>
        private string FormatMessage(RadioMessage msg)
        {
            // 将游戏时间转换为 HH:mm 格式
            int totalMinutes = Mathf.FloorToInt(msg.GameTime);
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            string timeStr = $"{hours:D2}:{minutes:D2}";

            return $"[{timeStr}] {msg.Sender}: {msg.Content}";
        }

        /// <summary>
        /// 限制消息数量，超过时删除最早的
        /// </summary>
        private void LimitMessageCount()
        {
            int childCount = contentTransform.childCount;
            if (childCount > maxMessages)
            {
                int removeCount = childCount - maxMessages;
                for (int i = 0; i < removeCount; i++)
                {
                    // 删除最早的（第一个子对象，因为 Content 的 pivot 是顶部）
                    Transform oldestChild = contentTransform.GetChild(0);
                    if (oldestChild != null)
                    {
                        Destroy(oldestChild.gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 自动滚动到最新消息
        /// </summary>
        private void ScrollToBottom()
        {
            // 在下一帧执行滚动，确保 ContentSizeFitter 已更新
            StartCoroutine(ScrollToBottomCoroutine());
        }

        private System.Collections.IEnumerator ScrollToBottomCoroutine()
        {
            // 等待一帧让布局更新
            yield return null;
            yield return null;

            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        /// <summary>
        /// 自动查找 Content Transform
        /// 路径: Canvas > RadioPanel > ScrollRect > Viewport > Content
        /// </summary>
        private Transform FindContentTransform()
        {
            // 方法1: 通过名称查找 RadioPanel
            var radioPanel = GameObject.Find("RadioPanel");
            if (radioPanel != null)
            {
                var scrollRect = radioPanel.GetComponentInChildren<ScrollRect>(true);
                if (scrollRect != null && scrollRect.content != null)
                {
                    return scrollRect.content;
                }
            }

            // 方法2: 通过 ScrollRect 组件查找
            var scrollRects = FindObjectsByType<ScrollRect>(FindObjectsSortMode.None);
            foreach (var sr in scrollRects)
            {
                // 检查是否属于 RadioPanel（通过父对象名称）
                Transform parent = sr.transform.parent;
                if (parent != null && parent.name == "RadioPanel")
                {
                    if (sr.content != null)
                    {
                        return sr.content;
                    }
                }
            }

            // 方法3: 直接查找名为 "Content" 的对象（在 RadioPanel 层级下）
            var contentGo = GameObject.Find("RadioPanel/ScrollRect/Viewport/Content");
            if (contentGo != null)
            {
                return contentGo.transform;
            }

            // 方法4: 查找任意名为 "Content" 且有 ContentSizeFitter 的对象
            var csfs = FindObjectsByType<ContentSizeFitter>(FindObjectsSortMode.None);
            foreach (var csf in csfs)
            {
                // 检查是否在 RadioPanel 层级下
                Transform t = csf.transform;
                while (t.parent != null)
                {
                    if (t.parent.name == "RadioPanel")
                    {
                        return csf.transform;
                    }
                    t = t.parent;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        // 编辑器辅助：手动触发测试消息
        [ContextMenu("添加测试消息")]
        private void AddTestMessage()
        {
            var testMsg = new RadioMessage
            {
                Id = "TEST-001",
                Type = ReportType.Status,
                Sender = "测试单位",
                Content = "这是一条测试汇报消息。",
                GameTime = Time.time / 60f,
                IsInterfered = false,
                IsDelivered = true
            };
            HandleReport(testMsg);
        }

        [ContextMenu("添加干扰测试消息")]
        private void AddInterferedTestMessage()
        {
            var testMsg = new RadioMessage
            {
                Id = "TEST-002",
                Type = ReportType.Enemy,
                Sender = "侦察兵",
                Content = "注意！A3 方向发现约 ▓▓ 名敌军",
                GameTime = Time.time / 60f,
                IsInterfered = true,
                IsDelivered = true
            };
            HandleReport(testMsg);
        }
#endif
    }
}
