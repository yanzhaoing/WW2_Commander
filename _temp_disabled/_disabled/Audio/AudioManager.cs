// AudioManager.cs — FMOD 音频系统总管理器
// SWO-1 指挥所模拟 Demo
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections.Generic;

namespace SWO1.Audio
{
    /// <summary>
    /// 音频系统总管理器。单例，驱动所有 FMOD 参数和事件。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("FMOD Banks")]
        [SerializeField] private string[] additionalBanks = { "Ambient", "Battle", "Radio", "Interaction" };

        [Header("默认参数值")]
        [SerializeField, Range(0f, 1f)] private float defaultRadioClarity = 0.8f;
        [SerializeField, Range(0f, 1f)] private float defaultRadioNoise = 0.15f;
        [SerializeField, Range(0f, 1f)] private float defaultBattleIntensity = 0.3f;
        [SerializeField, Range(0f, 1f)] private float defaultAmbientSeaState = 0.4f;
        [SerializeField, Range(0f, 1f)] private float defaultAmbientTension = 0.2f;

        // FMOD 参数句柄
        private PARAMETER_ID _radioClarityId;
        private PARAMETER_ID _radioNoiseId;
        private PARAMETER_ID _radioInterferenceId;
        private PARAMETER_ID _battleIntensityId;
        private PARAMETER_ID _battleDistanceId;
        private PARAMETER_ID _ambientSeaStateId;
        private PARAMETER_ID _ambientTensionId;
        private PARAMETER_ID _voiceEmotionId;
        private PARAMETER_ID _voiceDistanceId;
        private PARAMETER_ID _timeOfDayId;
        private PARAMETER_ID _combatAlertId;

        // 当前参数缓存
        private readonly Dictionary<string, float> _paramCache = new();

        // Snapshot 实例
        private EventInstance _snapshotRadioActive;
        private EventInstance _snapshotCombatIntense;
        private EventInstance _snapshotRadioSilence;
        private EventInstance _snapshotDecision;

        // 持续事件实例
        private EventInstance _ambientEngine;
        private EventInstance _ambientWaves;
        private EventInstance _ambientVentilation;
        private EventInstance _radioStatic;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeBanks();
            CacheParameterIds();
            InitializePersistentEvents();
            SetDefaultParameters();
        }

        private void InitializeBanks()
        {
            // FMOD Studio 自动加载 Master.bank，这里加载额外 banks
            foreach (var bankName in additionalBanks)
            {
                try
                {
                    RuntimeManager.LoadBank(bankName, true);
                }
                catch (BankLoadException e)
                {
                    Debug.LogError($"[AudioManager] Failed to load bank '{bankName}': {e.Message}");
                }
            }
        }

        private void CacheParameterIds()
        {
            // 通过全局参数描述缓存 ID，避免每帧查找
            TryGetGlobalParameterId("RadioClarity", out _radioClarityId);
            TryGetGlobalParameterId("RadioNoise", out _radioNoiseId);
            TryGetGlobalParameterId("RadioInterference", out _radioInterferenceId);
            TryGetGlobalParameterId("BattleIntensity", out _battleIntensityId);
            TryGetGlobalParameterId("BattleDistance", out _battleDistanceId);
            TryGetGlobalParameterId("AmbientSeaState", out _ambientSeaStateId);
            TryGetGlobalParameterId("AmbientTension", out _ambientTensionId);
            TryGetGlobalParameterId("VoiceEmotion", out _voiceEmotionId);
            TryGetGlobalParameterId("VoiceDistance", out _voiceDistanceId);
            TryGetGlobalParameterId("TimeOfDay", out _timeOfDayId);
            TryGetGlobalParameterId("CombatAlert", out _combatAlertId);
        }

        private void TryGetGlobalParameterId(string name, out PARAMETER_ID id)
        {
            id = default;
            try
            {
                var result = RuntimeManager.StudioSystem.getParameterDescriptionByName(name, out PARAMETER_DESCRIPTION desc);
                if (result == FMOD.RESULT.OK)
                    id = desc.id;
            }
            catch
            {
                Debug.LogWarning($"[AudioManager] Global parameter '{name}' not found in banks.");
            }
        }

        private void InitializePersistentEvents()
        {
            _ambientEngine = RuntimeManager.CreateInstance("event:/Ambient/Amb_Ship_Engine");
            _ambientWaves = RuntimeManager.CreateInstance("event:/Ambient/Amb_Sea_Waves");
            _ambientVentilation = RuntimeManager.CreateInstance("event:/Ambient/Amb_Ventilation");
            _radioStatic = RuntimeManager.CreateInstance("event:/Radio/Rad_Static_Base");

            _ambientEngine.start();
            _ambientWaves.start();
            _ambientVentilation.start();
            _radioStatic.start();
        }

        private void SetDefaultParameters()
        {
            SetParameter("RadioClarity", defaultRadioClarity);
            SetParameter("RadioNoise", defaultRadioNoise);
            SetParameter("BattleIntensity", defaultBattleIntensity);
            SetParameter("AmbientSeaState", defaultAmbientSeaState);
            SetParameter("AmbientTension", defaultAmbientTension);
        }

        /// <summary>
        /// 设置 FMOD 全局参数（带缓存，避免重复设置）
        /// </summary>
        public void SetParameter(string name, float value, bool force = false)
        {
            if (!force && _paramCache.TryGetValue(name, out var cached) && Mathf.Approximately(cached, value))
                return;

            _paramCache[name] = value;
            RuntimeManager.StudioSystem.setParameterByName(name, value);
        }

        /// <summary>
        /// 用缓存的 ParameterID 设置（高频调用时更高效）
        /// </summary>
        public void SetParameterById(PARAMETER_ID id, float value)
        {
            RuntimeManager.StudioSystem.setParameterByID(id, value);
        }

        /// <summary>
        /// 获取当前参数值
        /// </summary>
        public float GetParameter(string name)
        {
            return _paramCache.TryGetValue(name, out var val) ? val : 0f;
        }

        /// <summary>
        /// 播放一次性音效（交互、触发式音效等）
        /// </summary>
        public void PlayOneShot(EventReference eventRef, Vector3 position = default)
        {
            RuntimeManager.PlayOneShot(eventRef, position);
        }

        /// <summary>
        /// 播放一次性音效（按路径字符串）
        /// </summary>
        public void PlayOneShot(string eventPath, Vector3 position = default)
        {
            RuntimeManager.PlayOneShot(eventPath, position);
        }

        /// <summary>
        /// 创建可控制的事件实例
        /// </summary>
        public EventInstance CreateInstance(string eventPath)
        {
            return RuntimeManager.CreateInstance(eventPath);
        }

        /// <summary>
        /// 触发 Snapshot（混音快照切换）
        /// </summary>
        public void SetSnapshot(string snapshotPath, float weight = 1f)
        {
            // FMOD Snapshot 通过 EventInstance 控制
            var instance = RuntimeManager.CreateInstance(snapshotPath);
            instance.setParameterByName("Weight", weight);
            instance.start();
            instance.release(); // 标记为自动回收
        }

        /// <summary>
        /// 更新游戏时间参数（06:00 - 09:00 映射到 0-1）
        /// </summary>
        public void UpdateTimeOfDay(float gameMinutes)
        {
            // 06:00 = 360min, 09:00 = 540min
            float normalized = Mathf.InverseLerp(360f, 540f, gameMinutes);
            SetParameter("TimeOfDay", normalized);
        }

        /// <summary>
        /// 设置战斗强度（由 SimulationManager 驱动）
        /// </summary>
        public void UpdateBattleState(float intensity, float avgDistance)
        {
            SetParameter("BattleIntensity", Mathf.Clamp01(intensity));
            SetParameter("BattleDistance", Mathf.Clamp01(avgDistance));
            SetParameter("CombatAlert", intensity > 0.7f ? 1f : 0f);
        }

        /// <summary>
        /// 设置无线电状态
        /// </summary>
        public void UpdateRadioState(float clarity, float noise, float interference)
        {
            SetParameter("RadioClarity", Mathf.Clamp01(clarity));
            SetParameter("RadioNoise", Mathf.Clamp01(noise));
            SetParameter("RadioInterference", Mathf.Clamp01(interference));
        }

        /// <summary>
        /// 设置环境状态
        /// </summary>
        public void UpdateAmbientState(float seaState, float tension)
        {
            SetParameter("AmbientSeaState", Mathf.Clamp01(seaState));
            SetParameter("AmbientTension", Mathf.Clamp01(tension));
        }

        private void OnDestroy()
        {
            _ambientEngine.stop(FMOD.STOP_MODE.ALLOWFADEOUT);
            _ambientWaves.stop(FMOD.STOP_MODE.ALLOWFADEOUT);
            _ambientVentilation.stop(FMOD.STOP_MODE.ALLOWFADEOUT);
            _radioStatic.stop(FMOD.STOP_MODE.ALLOWFADEOUT);

            _ambientEngine.release();
            _ambientWaves.release();
            _ambientVentilation.release();
            _radioStatic.release();
        }
    }
}
