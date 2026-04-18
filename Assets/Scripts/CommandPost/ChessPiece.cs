using SWO1.Core;
// ChessPiece.cs - 沙盘兵棋标记
// 继承 InteractableObject，代表沙盘上的部队棋子
using UnityEngine;

namespace SWO1.CommandPost
{
    public enum PieceFaction
    {
        US,     // 美军(蓝色)
        German  // 德军(红色)
    }

    public enum PieceUnitType
    {
        Infantry,    // 步兵(方块)
        Armor,       // 装甲(菱形)
        HQ,          // 指挥部(圆)
        Unknown,     // 未知(问号)
        Fortification // 工事(三角)
    }

    public enum PieceStatus
    {
        Confirmed,   // 已确认(实心)
        Estimated,   // 推测(半透明)
        Expired,     // 过期(灰色)
        Moving       // 移动中(闪烁)
    }

    /// <summary>
    /// 沙盘兵棋标记。
    /// 
    /// 代替旧的 UnitController。
    /// 旧系统：单位有实际3D模型，在地图上移动。
    /// 新系统：棋子只是沙盘上的标记，不代表真实位置。
    /// 
    /// 棋子的行为：
    /// - 可被玩家抓取和移动
    /// - 参谋会根据情报自动更新位置
    /// - 颜色反映信息可靠性
    /// - 友军棋子较为准确，敌军棋子充满不确定性
    /// </summary>
    public class ChessPiece : InteractableObject
    {
        [Header("棋子属性")]
        public PieceFaction Faction;
        public PieceUnitType UnitType;
        public string UnitName;         // 例如 "第1步兵连"
        public string RadioCallsign;    // 无线电呼号 "红一"

        [Header("沙盘位置")]
        public Vector3 SandTablePosition; // 在沙盘上的逻辑坐标

        [Header("视觉")]
        [SerializeField] private Renderer pieceRenderer;
        [SerializeField] private GameObject questionMarkOverlay; // 未知标记叠加

        // 状态
        public PieceStatus Status { get; private set; } = PieceStatus.Confirmed;
        public bool IsOnSandTable { get; set; } = true;

        // 关联的情报 ID（敌军棋子才有）
        public string LinkedIntelId;

        protected override void Awake()
        {
            base.Awake();
            Type = InteractableType.ChessPiece;
            UpdateVisual();
        }

        /// <summary>
        /// 更新棋子状态（由 StaffAI 或玩家操作触发）
        /// </summary>
        public void UpdateStatus(PieceStatus newStatus)
        {
            Status = newStatus;
            UpdateVisual();
        }

        /// <summary>
        /// 更新棋子位置（参谋根据情报移动）
        /// </summary>
        public void MoveTo(Vector3 newSandTablePos, bool isStaffUpdate = false)
        {
            Vector3 oldPos = SandTablePosition;
            SandTablePosition = newSandTablePos;

            // 在沙盘上平滑移动
            StartCoroutine(SmoothMove(newSandTablePos));

            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishChessPieceMoved(this, newSandTablePos);
            }
        }

        private System.Collections.IEnumerator SmoothMove(Vector3 target)
        {
            Vector3 start = transform.position;
            float t = 0;
            float duration = 1f;

            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }

            transform.position = target;
        }

        public override void OnGrab()
        {
            base.OnGrab();
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishChessPieceGrabbed(this);
            }
        }

        public override void OnRelease()
        {
            base.OnRelease();
            if (GameEventBus.Instance != null)
            {
                GameEventBus.Instance.PublishChessPieceReleased(this);
            }
        }

        /// <summary>
        /// 根据状态更新棋子视觉
        /// </summary>
        private void UpdateVisual()
        {
            if (pieceRenderer == null) return;

            Color color = GetFactionColor();
            float alpha = GetStatusAlpha();

            color.a = alpha;
            pieceRenderer.material.color = color;

            // 未知标记
            if (questionMarkOverlay != null)
            {
                questionMarkOverlay.SetActive(UnitType == PieceUnitType.Unknown);
            }
        }

        private Color GetFactionColor()
        {
            return Faction switch
            {
                PieceFaction.US => new Color(0.2f, 0.4f, 0.9f),     // 蓝色
                PieceFaction.German => new Color(0.9f, 0.2f, 0.2f),  // 红色
                _ => Color.white
            };
        }

        private float GetStatusAlpha()
        {
            return Status switch
            {
                PieceStatus.Confirmed => 1f,
                PieceStatus.Estimated => 0.6f,
                PieceStatus.Expired => 0.3f,
                PieceStatus.Moving => 0.8f,
                _ => 1f
            };
        }

        /// <summary>
        /// 获取棋子描述文本（用于 UI 显示）
        /// </summary>
        public string GetDescription()
        {
            string faction = Faction == PieceFaction.US ? "美军" : "德军";
            string type = UnitType switch
            {
                PieceUnitType.Infantry => "步兵",
                PieceUnitType.Armor => "装甲",
                PieceUnitType.HQ => "指挥部",
                PieceUnitType.Unknown => "未知单位",
                PieceUnitType.Fortification => "防御工事",
                _ => "未知"
            };
            return $"{faction} {type}: {UnitName}";
        }
    }
}
