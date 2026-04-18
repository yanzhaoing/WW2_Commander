// AudioEvents.cs — GameEventBus → SimpleAudioManager 事件桥接
// 订阅全局事件总线，调用 SimpleAudioManager 播放对应音效
// 无需真实音频文件即可编译运行（PlayOneShot 配合 null clip 安全跳过）
using UnityEngine;
using SWO1.Core;
using SWO1.Command;
using SWO1.Intelligence;

namespace SWO1.Audio
{
    /// <summary>
    /// 音频事件桥接器
    /// 
    /// 职责：将 GameEventBus 的游戏事件映射到音频播放调用。
    /// 与 SimpleAudioManager 解耦 — 只负责"何时播放什么"。
    /// 
    /// 事件映射：
    ///   战役阶段变更  → BGM 切换 / 无线电开关
    ///   无线电汇报    → 无线电杂音脉冲
    ///   指令发送      → 发报机声
    ///   波次来临      → 警报声
    ///   战场更新      → 爆炸 / 炮声
    ///   游戏结局      → 胜利 / 失败音乐
    /// </summary>
    public class AudioEvents : MonoBehaviour
    {
        [Header("可选音频覆盖")]
        [Tooltip("警报音效（可选，覆盖 SimpleAudioManager 中的配置）")]
        [SerializeField] private AudioClip alarmOverride;

        [Tooltip("发报机音效（可选）")]
        [SerializeField] private AudioClip telegraphOverride;

        [Tooltip("胜利音乐（可选）")]
        [SerializeField] private AudioClip victoryOverride;

        [Tooltip("失败音乐（可选）")]
        [SerializeField] private AudioClip defeatOverride;

        private SimpleAudioManager audioManager;

        #region Lifecycle

        void Start()
        {
            audioManager = SimpleAudioManager.Instance;
            SubscribeEvents();
        }

        void OnDestroy()
        {
            UnsubscribeEvents();
        }

        #endregion

        #region 事件订阅

        private void SubscribeEvents()
        {
            var bus = GameEventBus.Instance;
            if (bus == null)
            {
                Debug.LogWarning("[AudioEvents] GameEventBus 未找到，跳过订阅");
                return;
            }

            bus.OnCampaignPhaseChanged      += OnPhaseChanged;
            bus.OnRadioReportDelivered       += OnRadioReport;
            bus.OnRadioReportGenerated       += OnRadioReportGenerated;
            bus.OnRadioCommandSent           += OnCommandSent;
            bus.OnBattlefieldUpdated         += OnBattlefieldUpdated;
            bus.OnGameOutcomeChanged         += OnGameOutcome;
            bus.OnRadioInterference          += OnRadioInterference;

            Debug.Log("[AudioEvents] 已订阅 GameEventBus 事件");
        }

        private void UnsubscribeEvents()
        {
            var bus = GameEventBus.Instance;
            if (bus == null) return;

            bus.OnCampaignPhaseChanged      -= OnPhaseChanged;
            bus.OnRadioReportDelivered       -= OnRadioReport;
            bus.OnRadioReportGenerated       -= OnRadioReportGenerated;
            bus.OnRadioCommandSent           -= OnCommandSent;
            bus.OnBattlefieldUpdated         -= OnBattlefieldUpdated;
            bus.OnGameOutcomeChanged         -= OnGameOutcome;
            bus.OnRadioInterference          -= OnRadioInterference;
        }

        #endregion

        #region 事件处理

        /// <summary>战役阶段变更 → BGM / 无线电切换</summary>
        private void OnPhaseChanged(CampaignPhase phase)
        {
            if (audioManager == null) return;

            switch (phase)
            {
                case CampaignPhase.FirstWaveLanding:
                case CampaignPhase.SecondWaveLanding:
                    // 战斗开始 → 警报声
                    audioManager.PlayAlarm(alarmOverride);
                    break;

                case CampaignPhase.CounterAttack:
                    // 反击 → 爆炸音效
                    audioManager.PlayExplosion();
                    break;

                case CampaignPhase.CriticalDecision:
                    // 关键决策 → 停 BGM 营造紧张
                    audioManager.StopBGM();
                    break;
            }
        }

        /// <summary>无线电汇报送达 → 无线电噪音脉冲</summary>
        private void OnRadioReport(RadioReport report)
        {
            if (audioManager == null) return;
            // 短暂开启无线电杂音模拟通讯
            if (!audioManager.IsRadioPlaying)
            {
                audioManager.StartRadioStatic();
                StartCoroutine(StopRadioAfterDelay(2f));
            }
        }

        /// <summary>无线电汇报生成 → 短促发报机声</summary>
        private void OnRadioReportGenerated(RadioReport report)
        {
            if (audioManager == null) return;
            audioManager.PlayTelegraph(telegraphOverride);
        }

        /// <summary>指令发送 → 发报机声音</summary>
        private void OnCommandSent(RadioCommand cmd)
        {
            if (audioManager == null) return;
            audioManager.PlayTelegraph(telegraphOverride);
            // 顺便触发一下无线电杂音
            if (!audioManager.IsRadioPlaying)
            {
                audioManager.StartRadioStatic();
                StartCoroutine(StopRadioAfterDelay(2f));
            }
        }

        /// <summary>战场更新 → 爆炸 / 炮声</summary>
        private void OnBattlefieldUpdated(BattlefieldData data)
        {
            if (audioManager == null || data == null) return;

            // 桥梁受损时随机爆炸
            if (data.BridgeHP < 80 && Random.value < 0.3f)
                audioManager.PlayExplosion();

            // 高波次时随机炮声
            if (data.CurrentWave >= 3 && Random.value < 0.2f)
                audioManager.PlayCannon();
        }

        /// <summary>游戏结局 → 胜利 / 失败音乐</summary>
        private void OnGameOutcome(GameOutcome outcome)
        {
            if (audioManager == null) return;

            audioManager.StopRadioStatic();

            switch (outcome)
            {
                case GameOutcome.PerfectVictory:
                case GameOutcome.PyrrhicVictory:
                case GameOutcome.PartialVictory:
                    audioManager.PlayVictory(victoryOverride);
                    break;
                case GameOutcome.Defeat:
                case GameOutcome.TotalDefeat:
                    audioManager.PlayDefeat(defeatOverride);
                    break;
                case GameOutcome.InProgress:
                default:
                    break;
            }
        }

        /// <summary>无线电干扰 → 短暂杂音脉冲</summary>
        private void OnRadioInterference(RadioReport report)
        {
            if (audioManager == null) return;
            if (!audioManager.IsRadioPlaying)
            {
                audioManager.StartRadioStatic();
                StartCoroutine(StopRadioAfterDelay(1.5f));
            }
        }

        #endregion

        #region 辅助协程

        private System.Collections.IEnumerator StopRadioAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            audioManager?.StopRadioStatic();
        }

        #endregion
    }
}
