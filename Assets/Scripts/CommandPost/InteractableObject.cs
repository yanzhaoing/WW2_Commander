// InteractableObject.cs — 可交互对象基类 (更新版)
// 沙盘棋子、无线电台、文件等继承此类
using UnityEngine;

namespace SWO1.CommandPost
{
    public enum InteractableType
    {
        ChessPiece,     // 沙盘兵棋标记（可抓取/移动/旋转）
        RadioStation,   // 无线电台（点击进入电台模式）
        Document,       // 文件/情报（点击进入阅读模式）
        Notepad,        // 便签纸（可书写）
        Switch,         // 开关/旋钮（可旋转）
        CoffeeCup       // 氛围物件（纯装饰）
    }

    /// <summary>
    /// 所有可交互场景物件的基类。
    /// 处理高亮、悬停、抓取状态。
    /// 
    /// 继承体系：
    /// InteractableObject (abstract)
    /// ├── ChessPiece      ← 沙盘兵棋，可抓取移动
    /// ├── RadioStation    ← 无线电台，点击进入模式
    /// ├── DocumentObject  ← 文件架，点击进入阅读
    /// ├── NotepadObject   ← 便签纸，可书写
    /// └── SwitchObject    ← 开关/旋钮，可旋转
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class InteractableObject : MonoBehaviour
    {
        [Header("交互设置")]
        public InteractableType Type;
        public string DisplayName;
        [TextArea] public string Description;

        [Tooltip("是否可交互（运行时可切换）")]
        public bool IsInteractable = true;

        [Header("高亮")]
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.5f, 0.3f);
        [SerializeField] private Renderer outlineRenderer;

        // 状态
        public bool IsHovered { get; private set; }
        public bool IsGrabbed { get; private set; }

        // 原始材质颜色
        private Color originalColor;
        private MaterialPropertyBlock propBlock;

        protected virtual void Awake()
        {
            propBlock = new MaterialPropertyBlock();
            if (outlineRenderer != null)
            {
                outlineRenderer.GetPropertyBlock(propBlock);
                originalColor = outlineRenderer.material.color;
            }
        }

        /// <summary>
        /// 鼠标悬停进入
        /// </summary>
        public virtual void OnHoverEnter()
        {
            if (!IsInteractable) return;
            IsHovered = true;
            SetHighlight(true);
        }

        /// <summary>
        /// 鼠标悬停退出
        /// </summary>
        public virtual void OnHoverExit()
        {
            IsHovered = false;
            SetHighlight(false);
        }

        /// <summary>
        /// 被抓取
        /// </summary>
        public virtual void OnGrab()
        {
            IsGrabbed = true;
        }

        /// <summary>
        /// 被释放
        /// </summary>
        public virtual void OnRelease()
        {
            IsGrabbed = false;
        }

        /// <summary>
        /// 设置高亮效果
        /// </summary>
        protected virtual void SetHighlight(bool active)
        {
            if (outlineRenderer == null) return;

            if (active)
            {
                propBlock.SetColor("_BaseColor", highlightColor);
            }
            else
            {
                propBlock.SetColor("_BaseColor", originalColor);
            }
            outlineRenderer.SetPropertyBlock(propBlock);
        }
    }
}
