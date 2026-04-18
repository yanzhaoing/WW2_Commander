// AmbientAudioSystem.cs — 环境音效系统
// 管理指挥所内部持续环境音效
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;

namespace SWO1.Audio
{
    /// <summary>
    /// 环境音效系统
    /// 驱动船舱引擎、海浪、金属吱嘎、通风等持续环境音
    /// </summary>
    public class AmbientAudioSystem : MonoBehaviour
    {
        [Header("FMOD 事件")]
        [SerializeField] private EventReference shipEngine;
        [SerializeField] private EventReference seaWaves;
        [SerializeField] private EventReference shipCreaks;
        [SerializeField] private EventReference ventilation;
        [SerializeField] private EventReference clockTick;
        [SerializeField] private EventReference windHowl; // 恶劣天气时的风声

        [Header("环境参数")]
        [SerializeField, Range(0f, 1f)] private float seaState = 0.4f;
        [SerializeField, Range(0f, 1f)] private float tension = 0.2f;
        [SerializeField, Range(0f, 1f)] private float weatherIntensity = 0f;

        [Header("金属吱嘎触发")]
        [SerializeField] private float creakIntervalMin = 5f;
        [SerializeField] private float creakIntervalMax = 20f;

        [Header("随机咖啡杯碰撞")]
        [SerializeField] private float coffeeCupIntervalMin = 60f;
        [SerializeField] private float coffeeCupIntervalMax = 180f;

        // 持续事件实例
        private EventInstance _engineInstance;
        private EventInstance _wavesInstance;
        private EventInstance _ventInstance;

        // 引用交互系统播放随机音效
        private InteractionAudioSystem _interaction;

        // 定时器
        private float _nextCreak;
        private float _nextCoffeeCup;

        private void Start()
        {
            _interaction = GetComponent<InteractionAudioSystem>();

            // 创建持续事件
            _engineInstance = CreateAndStart(shipEngine);
            _wavesInstance = CreateAndStart(seaWaves);
            _ventInstance = CreateAndStart(ventilation);

            // 设置初始参数
            UpdateParameters();

            // 初始化定时器
            _nextCreak = Time.time + Random.Range(creakIntervalMin, creakIntervalMax);
            _nextCoffeeCup = Time.time + Random.Range(coffeeCupIntervalMin, coffeeCupIntervalMax);
        }

        private EventInstance CreateAndStart(EventReference eventRef)
        {
            if (eventRef.IsNull) return default;
            var instance = RuntimeManager.CreateInstance(eventRef);
            instance.start();
            return instance;
        }

        private void Update()
        {
            float now = Time.time;

            // 随机船体吱嘎（海况越高越频繁）
            if (now >= _nextCreak)
            {
                PlayCreak();
                float intervalMultiplier = Mathf.Lerp(1.5f, 0.5f, seaState);
                _nextCreak = now + Random.Range(creakIntervalMin, creakIntervalMax) * intervalMultiplier;
            }

            // 偶尔的咖啡杯碰撞（增加生活感）
            if (now >= _nextCoffeeCup)
            {
                if (_interaction != null)
                    _interaction.OnCoffeeCup(Random.Range(0.1f, 0.4f));
                _nextCoffeeCup = now + Random.Range(coffeeCupIntervalMin, coffeeCupIntervalMax);
            }
        }

        /// <summary>
        /// 更新所有环境参数
        /// </summary>
        public void UpdateParameters()
        {
            AudioManager.Instance.UpdateAmbientState(seaState, tension);

            // 引擎音量随紧张度变化
            _engineInstance.setParameterByName("AmbientTension", tension);

            // 海浪随海况变化
            _wavesInstance.setParameterByName("AmbientSeaState", seaState);

            // 通风基本恒定
            _ventInstance.setParameterByName("AmbientTension", tension * 0.3f);
        }

        /// <summary>
        /// 播放船体吱嘎声
        /// </summary>
        private void PlayCreak()
        {
            if (shipCreaks.IsNull) return;
            var instance = RuntimeManager.CreateInstance(shipCreaks);
            // 海况影响吱嘎强度
            instance.setParameterByName("AmbientSeaState", seaState);
            instance.setPitch(Random.Range(0.8f, 1.2f)); // 随机音高变化
            instance.start();
            instance.release();
        }

        /// <summary>
        /// 设置海况
        /// </summary>
        public void SetSeaState(float state)
        {
            seaState = Mathf.Clamp01(state);
            UpdateParameters();
        }

        /// <summary>
        /// 设置紧张度
        /// </summary>
        public void SetTension(float value)
        {
            tension = Mathf.Clamp01(value);
            UpdateParameters();
        }

        /// <summary>
        /// 设置天气强度（0=晴天, 1=暴风雨）
        /// </summary>
        public void SetWeather(float intensity)
        {
            weatherIntensity = Mathf.Clamp01(intensity);

            // 恶劣天气增加海况和紧张度
            if (weatherIntensity > 0.5f)
            {
                seaState = Mathf.Max(seaState, weatherIntensity * 0.8f);
                tension = Mathf.Max(tension, weatherIntensity * 0.6f);
            }

            // 风声
            if (weatherIntensity > 0.3f && !windHowl.IsNull)
            {
                // 风声由 FMOD 参数驱动强度
                AudioManager.Instance.SetParameter("AmbientSeaState", seaState);
            }

            UpdateParameters();
        }

        private void OnDestroy()
        {
            StopInstance(_engineInstance);
            StopInstance(_wavesInstance);
            StopInstance(_ventInstance);
        }

        private void StopInstance(EventInstance instance)
        {
            instance.stop(FMOD.STOP_MODE.ALLOWFADEOUT);
            instance.release();
        }
    }
}
