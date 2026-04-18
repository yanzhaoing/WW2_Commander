// RadioAudioSystem.cs — 无线电音频系统
// 处理无线电语音播放、干扰效果、通讯条件变化
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SWO1.Audio
{
    /// <summary>
    /// 无线电通讯条件枚举
    /// </summary>
    public enum RadioCondition
    {
        ClearClose,      // 近距清晰
        Normal,          // 正常通讯
        WeakSignal,      // 弱信号
        EdgeOfRange,     // 距离边缘
        HeavyInterference, // 严重干扰
        CommsLost        // 通讯中断
    }

    /// <summary>
    /// 语音情绪状态
    /// </summary>
    public enum VoiceEmotion
    {
        Calm,           // 冷静专业
        SlightlyTense,  // 略有紧张
        Tense,          // 明显紧张
        Fearful,        // 恐惧
        Desperate       // 绝望
    }

    /// <summary>
    /// 无线电频道配置
    /// </summary>
    [Serializable]
    public class RadioChannel
    {
        public string channelId;       // "freq1_company1", "freq2_company2", etc.
        public string displayName;     // "频率1 - 第1连"
        public float baseClarity = 0.8f;
        public float baseNoise = 0.15f;
        public string voiceEventPath;  // 该频道的语音事件路径
        public Color channelColor = Color.green; // UI 频道颜色
    }

    /// <summary>
    /// 无线电音频系统核心类
    /// 管理所有无线电通讯的音频播放和效果处理
    /// </summary>
    public class RadioAudioSystem : MonoBehaviour
    {
        [Header("无线电频道")]
        [SerializeField] private RadioChannel[] channels;

        [Header("FMOD 事件")]
        [SerializeField] private EventReference radioStaticBase;
        [SerializeField] private EventReference radioInterferenceBurst;
        [SerializeField] private EventReference squelchOpen;
        [SerializeField] private EventReference squelchClose;
        [SerializeField] private EventReference keyClick;
        [SerializeField] private EventReference knobTurn;
        [SerializeField] private EventReference pttPress;
        [SerializeField] private EventReference pttRelease;

        [Header("干扰设置")]
        [SerializeField, Range(0f, 1f)] private float globalInterference = 0.1f;
        [SerializeField] private float interferenceBurstIntervalMin = 30f;
        [SerializeField] private float interferenceBurstIntervalMax = 120f;

        // 活跃的语音实例
        private readonly Dictionary<string, EventInstance> _activeVoices = new();
        private readonly Dictionary<string, EventInstance> _channelStatics = new();

        // 干扰定时器
        private float _nextInterferenceBurst;
        private Coroutine _interferenceRoutine;

        // 事件回调
        public event Action<string, string> OnRadioMessageReceived; // channelId, message
        public event Action<string> OnRadioInterference; // channelId
        public event Action<string> OnRadioCommsLost; // channelId
        public event Action<string> OnRadioCommsRestored; // channelId

        private void Start()
        {
            InitializeChannels();
            _nextInterferenceBurst = Time.time + UnityEngine.Random.Range(interferenceBurstIntervalMin, interferenceBurstIntervalMax);
            _interferenceRoutine = StartCoroutine(InterferenceRoutine());
        }

        private void InitializeChannels()
        {
            if (channels == null) return;

            foreach (var ch in channels)
            {
                // 每个频道可以有自己的静态底噪事件实例
                // 共用一个静态事件，通过参数区分
                var staticInstance = AudioManager.Instance.CreateInstance("event:/Radio/Rad_Static_Base");
                staticInstance.setParameterByName("RadioNoise", ch.baseNoise);
                staticInstance.start();
                _channelStatics[ch.channelId] = staticInstance;
            }
        }

        /// <summary>
        /// 播放无线电语音消息（核心接口）
        /// </summary>
        /// <param name="channelId">频道 ID</param>
        /// <param name="messageText">消息文本</param>
        /// <param name="condition">通讯条件</param>
        /// <param name="emotion">语音情绪</param>
        /// <param name="onComplete">播放完成回调</param>
        public void PlayRadioMessage(
            string channelId,
            string messageText,
            RadioCondition condition = RadioCondition.Normal,
            VoiceEmotion emotion = VoiceEmotion.Calm,
            Action onComplete = null)
        {
            var channel = GetChannel(channelId);
            if (channel == null)
            {
                Debug.LogWarning($"[RadioAudio] Channel '{channelId}' not found.");
                return;
            }

            // 计算音频参数
            var audioParams = CalculateAudioParams(channel, condition, emotion);

            // 播放开音
            PlaySquelch(true);

            // 延迟一小段时间模拟无线电开启
            StartCoroutine(PlayMessageWithDelay(channelId, messageText, audioParams, 0.15f, onComplete));
        }

        /// <summary>
        /// 计算音频参数组合
        /// </summary>
        private RadioAudioParams CalculateAudioParams(RadioChannel channel, RadioCondition condition, VoiceEmotion emotion)
        {
            var p = new RadioAudioParams();

            // 通讯条件 → 音质参数
            switch (condition)
            {
                case RadioCondition.ClearClose:
                    p.clarity = 0.95f; p.noise = 0.05f; p.distortion = 0.02f; p.dropoutChance = 0.01f;
                    break;
                case RadioCondition.Normal:
                    p.clarity = 0.80f; p.noise = 0.15f; p.distortion = 0.05f; p.dropoutChance = 0.03f;
                    break;
                case RadioCondition.WeakSignal:
                    p.clarity = 0.55f; p.noise = 0.35f; p.distortion = 0.15f; p.dropoutChance = 0.10f;
                    break;
                case RadioCondition.EdgeOfRange:
                    p.clarity = 0.35f; p.noise = 0.50f; p.distortion = 0.25f; p.dropoutChance = 0.20f;
                    break;
                case RadioCondition.HeavyInterference:
                    p.clarity = 0.15f; p.noise = 0.70f; p.distortion = 0.40f; p.dropoutChance = 0.35f;
                    break;
                case RadioCondition.CommsLost:
                    p.clarity = 0.02f; p.noise = 0.95f; p.distortion = 0.50f; p.dropoutChance = 0.80f;
                    break;
            }

            // 叠加频道基础值
            p.clarity *= channel.baseClarity;
            p.noise = Mathf.Lerp(p.noise, channel.baseNoise, 0.3f);
            p.interference = globalInterference;

            // 叠加全局干扰
            p.noise = Mathf.Clamp01(p.noise + globalInterference * 0.5f);
            p.clarity = Mathf.Clamp01(p.clarity - globalInterference * 0.3f);

            // 情绪 → 语速/音高修正
            switch (emotion)
            {
                case VoiceEmotion.Calm:
                    p.speedMultiplier = 1.0f; p.pitchShift = 0f; p.tremoloIntensity = 0f;
                    break;
                case VoiceEmotion.SlightlyTense:
                    p.speedMultiplier = 1.1f; p.pitchShift = 0.02f; p.tremoloIntensity = 0.1f;
                    break;
                case VoiceEmotion.Tense:
                    p.speedMultiplier = 1.25f; p.pitchShift = 0.05f; p.tremoloIntensity = 0.3f;
                    break;
                case VoiceEmotion.Fearful:
                    p.speedMultiplier = 1.4f; p.pitchShift = 0.08f; p.tremoloIntensity = 0.6f;
                    break;
                case VoiceEmotion.Desperate:
                    p.speedMultiplier = UnityEngine.Random.Range(0.9f, 1.6f);
                    p.pitchShift = UnityEngine.Random.Range(-0.05f, 0.1f);
                    p.tremoloIntensity = 0.9f;
                    break;
            }

            p.emotionValue = (float)emotion / (float)VoiceEmotion.Desperate;
            p.conditionDistance = condition switch
            {
                RadioCondition.ClearClose => 0.1f,
                RadioCondition.Normal => 0.3f,
                RadioCondition.WeakSignal => 0.55f,
                RadioCondition.EdgeOfRange => 0.75f,
                RadioCondition.HeavyInterference => 0.85f,
                RadioCondition.CommsLost => 1.0f,
                _ => 0.3f,
            };

            return p;
        }

        private IEnumerator PlayMessageWithDelay(
            string channelId, string messageText, RadioAudioParams audioParams,
            float delay, Action onComplete)
        {
            yield return new WaitForSeconds(delay);

            // 检查是否有随机丢包
            if (UnityEngine.Random.value < audioParams.dropoutChance)
            {
                // 模拟信号丢失：播放静电然后静默
                PlayInterferenceBurst(channelId, 0.5f);
                onComplete?.Invoke();
                yield break;
            }

            // 设置 FMOD 参数
            AudioManager.Instance.UpdateRadioState(audioParams.clarity, audioParams.noise, audioParams.interference);
            AudioManager.Instance.SetParameter("VoiceEmotion", audioParams.emotionValue);
            AudioManager.Instance.SetParameter("VoiceDistance", audioParams.conditionDistance);

            // 创建语音事件实例
            var voiceEvent = AudioManager.Instance.CreateInstance("event:/Radio/Rad_Voice_Receive");
            voiceEvent.setParameterByName("RadioClarity", audioParams.clarity);
            voiceEvent.setParameterByName("RadioNoise", audioParams.noise);
            voiceEvent.setParameterByName("RadioInterference", audioParams.interference);
            voiceEvent.setParameterByName("VoiceEmotion", audioParams.emotionValue);
            voiceEvent.setParameterByName("VoiceDistance", audioParams.conditionDistance);

            voiceEvent.start();
            _activeVoices[channelId] = voiceEvent;

            // 通知 UI
            OnRadioMessageReceived?.Invoke(channelId, messageText);

            // 等待语音播放完成（根据文本长度估算）
            float estimatedDuration = EstimateSpeechDuration(messageText, audioParams.speedMultiplier);

            // 在播放过程中随机加入轻微干扰脉冲
            float elapsed = 0f;
            while (elapsed < estimatedDuration)
            {
                elapsed += Time.deltaTime;

                // 随机干扰脉冲
                if (UnityEngine.Random.value < audioParams.dropoutChance * Time.deltaTime * 2f)
                {
                    PlayInterferenceBurst(channelId, 0.1f + UnityEngine.Random.value * 0.3f);
                }

                yield return null;
            }

            // 停止语音
            voiceEvent.stop(FMOD.STOP_MODE.ALLOWFADEOUT);
            voiceEvent.release();
            _activeVoices.Remove(channelId);

            // 播放关音
            PlaySquelch(false);

            // 恢复底噪
            AudioManager.Instance.UpdateRadioState(0.8f, 0.15f, globalInterference);

            onComplete?.Invoke();
        }

        /// <summary>
        /// 估算语音播放时长
        /// </summary>
        private float EstimateSpeechDuration(string text, float speedMultiplier)
        {
            // 中文约 4-5 字/秒，英文约 15-18 词/分钟
            // 简单估算：每字符 0.15 秒
            float baseDuration = text.Length * 0.15f;
            return Mathf.Clamp(baseDuration / speedMultiplier, 1.5f, 30f);
        }

        /// <summary>
        /// 播放干扰脉冲
        /// </summary>
        public void PlayInterferenceBurst(string channelId, float duration = 0.3f)
        {
            if (radioInterferenceBurst.IsNull) return;

            var instance = AudioManager.Instance.CreateInstance("event:/Radio/Rad_Interference_Burst");
            instance.setParameterByName("Duration", duration);
            instance.start();
            instance.release();

            OnRadioInterference?.Invoke(channelId);
        }

        /// <summary>
        /// 播放开/关音 (Squelch)
        /// </summary>
        private void PlaySquelch(bool open)
        {
            if (open && !squelchOpen.IsNull)
                AudioManager.Instance.PlayOneShot(squelchOpen);
            else if (!open && !squelchClose.IsNull)
                AudioManager.Instance.PlayOneShot(squelchClose);
        }

        /// <summary>
        /// 播放按键音（PTT）
        /// </summary>
        public void PlayPTT(bool pressed)
        {
            var sound = pressed ? pttPress : pttRelease;
            if (!sound.IsNull)
                AudioManager.Instance.PlayOneShot(sound);
        }

        /// <summary>
        /// 播放旋钮转动声
        /// </summary>
        public void PlayKnobTurn(float normalizedValue = 0.5f)
        {
            if (knobTurn.IsNull) return;
            var instance = AudioManager.Instance.CreateInstance("event:/Radio/Int_Knob_Turn");
            instance.setParameterByName("KnobPosition", normalizedValue);
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 播放按键点击声
        /// </summary>
        public void PlayKeyClick()
        {
            if (!keyClick.IsNull)
                AudioManager.Instance.PlayOneShot(keyClick);
        }

        /// <summary>
        /// 切换频率时播放旋钮到位声
        /// </summary>
        public void SwitchFrequency(string newChannelId, float position)
        {
            PlayKnobTurn(position);
            PlayKeyClick(); // 到位的"咔"声

            // 更新底噪参数
            var ch = GetChannel(newChannelId);
            if (ch != null)
            {
                AudioManager.Instance.UpdateRadioState(ch.baseClarity, ch.baseNoise, globalInterference);
            }
        }

        /// <summary>
        /// 触发通讯中断（某频道完全失去信号）
        /// </summary>
        public void TriggerCommsLost(string channelId, float durationSeconds = 120f)
        {
            StartCoroutine(CommsLostRoutine(channelId, durationSeconds));
        }

        private IEnumerator CommsLostRoutine(string channelId, float duration)
        {
            // 强烈的干扰脉冲
            PlayInterferenceBurst(channelId, 2f);

            // 切换到极高噪声
            AudioManager.Instance.UpdateRadioState(0.02f, 0.95f, 0.9f);

            OnRadioCommsLost?.Invoke(channelId);

            yield return new WaitForSeconds(duration);

            // 逐渐恢复
            float recoveryTime = 5f;
            float elapsed = 0f;
            while (elapsed < recoveryTime)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / recoveryTime;
                AudioManager.Instance.UpdateRadioState(
                    Mathf.Lerp(0.02f, 0.6f, t),
                    Mathf.Lerp(0.95f, 0.3f, t),
                    Mathf.Lerp(0.9f, 0.2f, t)
                );
                yield return null;
            }

            PlaySquelch(true);
            OnRadioCommsRestored?.Invoke(channelId);
        }

        /// <summary>
        /// 全局干扰脉冲定时器
        /// </summary>
        private IEnumerator InterferenceRoutine()
        {
            while (true)
            {
                float waitTime = UnityEngine.Random.Range(interferenceBurstIntervalMin, interferenceBurstIntervalMax);
                yield return new WaitForSeconds(waitTime);

                // 随机选择一个频道产生干扰
                if (channels != null && channels.Length > 0)
                {
                    var randomChannel = channels[UnityEngine.Random.Range(0, channels.Length)];
                    float burstDuration = 0.2f + UnityEngine.Random.value * 1.5f;
                    PlayInterferenceBurst(randomChannel.channelId, burstDuration);
                }
            }
        }

        /// <summary>
        /// 设置全局干扰强度（由天气、事件等驱动）
        /// </summary>
        public void SetGlobalInterference(float level)
        {
            globalInterference = Mathf.Clamp01(level);
            AudioManager.Instance.SetParameter("RadioInterference", globalInterference);
        }

        /// <summary>
        /// 停止频道的所有语音
        /// </summary>
        public void StopChannel(string channelId)
        {
            if (_activeVoices.TryGetValue(channelId, out var instance))
            {
                instance.stop(FMOD.STOP_MODE.IMMEDIATE);
                instance.release();
                _activeVoices.Remove(channelId);
            }
        }

        private RadioChannel GetChannel(string channelId)
        {
            if (channels == null) return null;
            foreach (var ch in channels)
            {
                if (ch.channelId == channelId) return ch;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (_interferenceRoutine != null)
                StopCoroutine(_interferenceRoutine);

            foreach (var kvp in _activeVoices)
            {
                kvp.Value.stop(FMOD.STOP_MODE.IMMEDIATE);
                kvp.Value.release();
            }
            foreach (var kvp in _channelStatics)
            {
                kvp.Value.stop(FMOD.STOP_MODE.ALLOWFADEOUT);
                kvp.Value.release();
            }
        }
    }

    /// <summary>
    /// 内部音频参数结构
    /// </summary>
    internal struct RadioAudioParams
    {
        public float clarity;
        public float noise;
        public float distortion;
        public float dropoutChance;
        public float interference;
        public float speedMultiplier;
        public float pitchShift;
        public float tremoloIntensity;
        public float emotionValue;
        public float conditionDistance;
    }
}
