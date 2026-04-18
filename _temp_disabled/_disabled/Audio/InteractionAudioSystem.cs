// InteractionAudioSystem.cs — 交互音效系统
// 沙盘棋子、无线电台、文件等场景交互音效
using UnityEngine;
using FMODUnity;

namespace SWO1.Audio
{
    /// <summary>
    /// 场景交互音效系统
    /// 管理指挥所内所有可交互物件的音效
    /// </summary>
    public class InteractionAudioSystem : MonoBehaviour
    {
        [Header("沙盘棋子")]
        [SerializeField] private EventReference chessGrab;
        [SerializeField] private EventReference chessPlace;
        [SerializeField] private EventReference chessSlide;

        [Header("文件/纸张")]
        [SerializeField] private EventReference paperRustle;
        [SerializeField] private EventReference paperTear;

        [Header("写字")]
        [SerializeField] private EventReference penWriting;

        [Header("杯子")]
        [SerializeField] private EventReference coffeeCup;

        [Header("开关/旋钮")]
        [SerializeField] private EventReference switchToggle;
        [SerializeField] private EventReference knobTurn;

        // ========== 沙盘棋子 ==========

        /// <summary>
        /// 抓取棋子
        /// </summary>
        public void OnChessGrab(float pieceSize = 0.5f)
        {
            if (chessGrab.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Chess_Grab");
            instance.setParameterByName("PieceSize", pieceSize);
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 放置棋子
        /// </summary>
        public void OnChessPlace(float pieceSize = 0.5f, float dropHeight = 0f)
        {
            if (chessPlace.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Chess_Place");
            instance.setParameterByName("PieceSize", pieceSize);
            instance.setParameterByName("DropHeight", dropHeight);
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 滑动棋子（持续音效，需手动停止）
        /// </summary>
        public FMOD.Studio.EventInstance StartChessSlide(float pieceSize = 0.5f)
        {
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Chess_Slide");
            instance.setParameterByName("PieceSize", pieceSize);
            instance.start();
            return instance;
        }

        // ========== 文件/纸张 ==========

        /// <summary>
        /// 翻动纸张
        /// </summary>
        public void OnPaperRustle(float intensity = 0.5f)
        {
            if (paperRustle.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Paper_Rustle");
            instance.setParameterByName("Intensity", intensity);
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 撕纸（丢弃指令卡）
        /// </summary>
        public void OnPaperTear()
        {
            if (!paperTear.IsNull)
                AudioManager.Instance.PlayOneShot(paperTear);
        }

        // ========== 写字 ==========

        /// <summary>
        /// 写字（持续音效，需手动停止）
        /// </summary>
        public FMOD.Studio.EventInstance StartPenWriting(float speed = 0.5f)
        {
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Pen_Writing");
            instance.setParameterByName("WriteSpeed", speed);
            instance.start();
            return instance;
        }

        // ========== 杯子 ==========

        /// <summary>
        /// 杯子碰撞
        /// </summary>
        public void OnCoffeeCup(float intensity = 0.3f)
        {
            if (coffeeCup.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Coffee_Cup");
            instance.setParameterByName("Intensity", intensity);
            instance.start();
            instance.release();
        }

        // ========== 开关/旋钮 ==========

        /// <summary>
        /// 开关切换
        /// </summary>
        public void OnSwitchToggle(bool on)
        {
            if (switchToggle.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Switch_Toggle");
            instance.setParameterByName("SwitchState", on ? 1f : 0f);
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 旋钮转动
        /// </summary>
        public void OnKnobTurn(float normalizedPosition = 0.5f)
        {
            if (knobTurn.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Interaction/Int_Knob_Turn");
            instance.setParameterByName("KnobPosition", normalizedPosition);
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 旋钮到位（刻度咔声）
        /// </summary>
        public void OnKnobDetent()
        {
            // 复用 key click
            AudioManager.Instance.PlayOneShot("event:/Radio/Key_Click");
        }
    }
}
