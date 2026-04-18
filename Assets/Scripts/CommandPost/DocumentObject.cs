// DocumentObject.cs — 文件/情报交互对象
// 继承 InteractableObject，管理文件的查看和翻阅
using UnityEngine;
using System;
using System.Collections.Generic;

namespace SWO1.CommandPost
{
    /// <summary>
    /// 文件类型枚举
    /// </summary>
    public enum DocumentType
    {
        MissionBriefing,    // 任务简报
        IntelligenceReport, // 情报报告
        MapOverlay,         // 地图叠加层
        PersonalNote,       // 个人笔记
        Order               // 师部命令
    }

    /// <summary>
    /// 单页文档数据
    /// </summary>
    [Serializable]
    public class DocumentPage
    {
        public string Title;
        [TextArea(3, 10)] public string Content;
        public float Timestamp; // 该页对应的游戏时间
    }

    /// <summary>
    /// 文件交互对象 — 指挥所中的文件架/情报堆
    /// 
    /// 交互方式：
    /// - 点击进入阅读模式
    /// - 鼠标滚轮翻页
    /// - ESC 退出阅读
    /// 
    /// 继承体系：
    /// InteractableObject → DocumentObject
    /// </summary>
    public class DocumentObject : InteractableObject
    {
        [Header("文件配置")]
        [SerializeField] private DocumentType documentType;
        [SerializeField] private string documentTitle;

        [Header("页面内容")]
        [Tooltip("文件页内容（按顺序）")]
        [SerializeField] private List<DocumentPage> pages = new List<DocumentPage>();

        [Header("视觉")]
        [Tooltip("文档封面渲染器")]
        [SerializeField] private Renderer coverRenderer;

        [Tooltip("页面显示面板（阅读模式时激活）")]
        [SerializeField] private GameObject pagePanel;

        [Tooltip("页面标题文本")]
        [SerializeField] private TMPro.TextMeshProUGUI pageTitleText;

        [Tooltip("页面内容文本")]
        [SerializeField] private TMPro.TextMeshProUGUI pageContentText;

        [Tooltip("页码指示")]
        [SerializeField] private TMPro.TextMeshProUGUI pageIndicator;

        // === 状态 ===
        public int CurrentPage { get; private set; } = 0;
        public bool IsOpen { get; private set; }
        public int PageCount => pages.Count;

        // === 事件 ===
        public event Action<DocumentObject> OnDocumentOpened;
        public event Action<DocumentObject> OnDocumentClosed;
        public event Action<int> OnPageChanged;

        protected override void Awake()
        {
            base.Awake();
            Type = InteractableType.Document;
            DisplayName = documentTitle ?? "未命名文件";
            Description = $"文件类型: {documentType}";
        }

        void Start()
        {
            if (pagePanel != null) pagePanel.SetActive(false);
        }

        /// <summary>
        /// 打开文件，进入阅读模式
        /// </summary>
        public void Open()
        {
            if (pages.Count == 0)
            {
                Debug.LogWarning($"[Document] {DisplayName} 没有页面内容");
                return;
            }

            IsOpen = true;
            CurrentPage = 0;
            DisplayCurrentPage();

            if (pagePanel != null) pagePanel.SetActive(true);

            OnDocumentOpened?.Invoke(this);
            Debug.Log($"[Document] 打开文件: {DisplayName}");
        }

        /// <summary>
        /// 关闭文件
        /// </summary>
        public void Close()
        {
            IsOpen = false;
            if (pagePanel != null) pagePanel.SetActive(false);
            OnDocumentClosed?.Invoke(this);
            Debug.Log($"[Document] 关闭文件: {DisplayName}");
        }

        /// <summary>
        /// 翻到下一页
        /// </summary>
        public bool NextPage()
        {
            if (CurrentPage >= pages.Count - 1) return false;
            CurrentPage++;
            DisplayCurrentPage();
            OnPageChanged?.Invoke(CurrentPage);
            return true;
        }

        /// <summary>
        /// 翻到上一页
        /// </summary>
        public bool PreviousPage()
        {
            if (CurrentPage <= 0) return false;
            CurrentPage--;
            DisplayCurrentPage();
            OnPageChanged?.Invoke(CurrentPage);
            return true;
        }

        /// <summary>
        /// 翻到指定页
        /// </summary>
        public void GoToPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= pages.Count) return;
            CurrentPage = pageIndex;
            DisplayCurrentPage();
            OnPageChanged?.Invoke(CurrentPage);
        }

        /// <summary>
        /// 添加页面（运行时动态添加情报）
        /// </summary>
        public void AddPage(DocumentPage page)
        {
            pages.Add(page);
            if (IsOpen)
            {
                DisplayCurrentPage();
            }
        }

        public override void OnGrab()
        {
            base.OnGrab();
            if (!IsOpen)
            {
                Open();
            }
        }

        #region 显示

        private void DisplayCurrentPage()
        {
            if (pages.Count == 0 || CurrentPage >= pages.Count) return;

            var page = pages[CurrentPage];

            if (pageTitleText != null)
                pageTitleText.text = page.Title;
            if (pageContentText != null)
                pageContentText.text = page.Content;
            if (pageIndicator != null)
                pageIndicator.text = $"{CurrentPage + 1} / {pages.Count}";
        }

        #endregion
    }
}
