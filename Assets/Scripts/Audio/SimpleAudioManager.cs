// SimpleAudioManager.cs — 简易音频管理器（Unity AudioSource）
// 替代 FMOD，使用 Unity 内置 AudioSource 实现所有音效
// 音频类型：背景音乐、无线电杂音、炮声、爆炸声
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SWO1.Core;
using SWO1.Command;

namespace SWO1.Audio
{
    /// <summary>
    /// 简易音频管理器 — 全局单例
    /// 
    /// 设计原则：
    /// - 零 FMOD 依赖，纯 Unity AudioSource
    /// - 通过 GameEventBus 驱动音效播放
    /// - 对象池管理短音效，避免频繁实例化
    /// - 支持淡入淡出（背景音乐切换）
    /// 
    /// 音频通道：
    ///   BGM     — 背景音乐（循环播放）
    ///   Radio   — 无线电杂音（循环播放，按需开关）
    ///   SFX     — 短音效（炮声、爆炸，使用对象池）
    /// </summary>
    public class SimpleAudioManager : MonoBehaviour
    {
        public static SimpleAudioManager Instance { get; private set; }

        #region 配置

        [Header("音频源")]
        [Tooltip("背景音乐 AudioSource")]
        [SerializeField] private AudioSource bgmSource;

        [Tooltip("无线电杂音 AudioSource")]
        [SerializeField] private AudioSource radioSource;

        [Tooltip("SFX 对象池容量")]
        [SerializeField] private int sfxPoolSize = 8;

        [Header("背景音乐")]
        [Tooltip("默认背景音乐")]
        [SerializeField] private AudioClip defaultBGM;

        [Tooltip("战斗背景音乐")]
        [SerializeField] private AudioClip combatBGM;

        [Tooltip("BGM 淡入淡出时长（秒）")]
        [SerializeField] private float crossfadeDuration = 1.5f;

        [Header("音效")]
        [Tooltip("炮声音效列表（随机选取）")]
        [SerializeField] private AudioClip[] cannonClips;

        [Tooltip("爆炸音效列表（随机选取）")]
        [SerializeField] private AudioClip[] explosionClips;

        [Tooltip("无线电杂音")]
        [SerializeField] private AudioClip radioStaticClip;

        [Header("额外音效")]
        [Tooltip("警报声")]
        [SerializeField] private AudioClip alarmClip;

        [Tooltip("发报机声")]
        [SerializeField] private AudioClip telegraphClip;

        [Tooltip("胜利音乐")]
        [SerializeField] private AudioClip victoryClip;

        [Tooltip("失败音乐")]
        [SerializeField] private AudioClip defeatClip;

        [Header("音量")]

        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float bgmVolume = 0.4f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float radioVolume = 0.3f;

        #endregion

        #region 内部状态

        private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
        private Coroutine bgmCrossfadeRoutine;
        private bool isRadioPlaying;

        #endregion

        #region 公共属性

        public float MasterVolume
        {
            get => masterVolume;
            set { masterVolume = Mathf.Clamp01(value); RefreshVolumes(); }
        }

        public float BGMVolume
        {
            get => bgmVolume;
            set { bgmVolume = Mathf.Clamp01(value); RefreshVolumes(); }
        }

        public float SFXVolume
        {
            get => sfxVolume;
            set => sfxVolume = Mathf.Clamp01(value);
        }

        public float RadioVolume
        {
            get => radioVolume;
            set { radioVolume = Mathf.Clamp01(value); if (radioSource != null) radioSource.volume = radioVolume * masterVolume; }
        }

        public bool IsRadioPlaying => isRadioPlaying;

        #endregion

        #region Lifecycle

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitAudioSources();
            InitSFXPool();
        }

        void Start()
        {
            // 订阅事件总线
            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                bus.OnCampaignPhaseChanged += HandlePhaseChanged;
                bus.OnCommandDelivered += HandleCommandDelivered;
                bus.OnBattlefieldUpdated += HandleBattlefieldUpdated;
            }

            // 开始播放默认 BGM
            if (defaultBGM != null)
                PlayBGM(defaultBGM);
        }

        void OnDestroy()
        {
            var bus = GameEventBus.Instance;
            if (bus != null)
            {
                bus.OnCampaignPhaseChanged -= HandlePhaseChanged;
                bus.OnCommandDelivered -= HandleCommandDelivered;
                bus.OnBattlefieldUpdated -= HandleBattlefieldUpdated;
            }
        }

        #endregion

        #region 初始化

        private void InitAudioSources()
        {
            // 确保 BGM AudioSource 配置正确
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0f; // 2D
            }

            // 确保无线电 AudioSource 配置正确
            if (radioSource == null)
            {
                radioSource = gameObject.AddComponent<AudioSource>();
                radioSource.playOnAwake = false;
                radioSource.loop = true;
                radioSource.spatialBlend = 0f;
            }

            RefreshVolumes();
        }

        private void InitSFXPool()
        {
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 0f;
                sfxPool.Enqueue(src);
            }
        }

        #endregion

        #region 背景音乐

        /// <summary>
        /// 播放背景音乐（立即切换）
        /// </summary>
        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || bgmSource == null) return;
            bgmSource.clip = clip;
            bgmSource.volume = bgmVolume * masterVolume;
            bgmSource.Play();
            Debug.Log($"[Audio] BGM 播放: {clip.name}");
        }

        /// <summary>
        /// 播放背景音乐（带淡入淡出）
        /// </summary>
        public void PlayBGMWithFade(AudioClip clip)
        {
            if (clip == null) return;
            if (bgmCrossfadeRoutine != null)
                StopCoroutine(bgmCrossfadeRoutine);
            bgmCrossfadeRoutine = StartCoroutine(CrossfadeBGM(clip));
        }

        /// <summary>
        /// 停止背景音乐
        /// </summary>
        public void StopBGM()
        {
            if (bgmSource != null) bgmSource.Stop();
        }

        /// <summary>
        /// 暂停/恢复 BGM
        /// </summary>
        public void PauseBGM(bool pause)
        {
            if (bgmSource == null) return;
            if (pause) bgmSource.Pause();
            else bgmSource.UnPause();
        }

        private IEnumerator CrossfadeBGM(AudioClip newClip)
        {
            float timer = 0f;
            float startVol = bgmSource.volume;

            // 淡出
            while (timer < crossfadeDuration / 2f)
            {
                timer += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, timer / (crossfadeDuration / 2f));
                yield return null;
            }

            // 切换
            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.Play();

            // 淡入
            timer = 0f;
            float targetVol = bgmVolume * masterVolume;
            while (timer < crossfadeDuration / 2f)
            {
                timer += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0f, targetVol, timer / (crossfadeDuration / 2f));
                yield return null;
            }

            bgmSource.volume = targetVol;
            bgmCrossfadeRoutine = null;
        }

        #endregion

        #region 无线电杂音

        /// <summary>
        /// 开始播放无线电杂音
        /// </summary>
        public void StartRadioStatic()
        {
            if (isRadioPlaying || radioSource == null || radioStaticClip == null) return;
            radioSource.clip = radioStaticClip;
            radioSource.volume = radioVolume * masterVolume;
            radioSource.Play();
            isRadioPlaying = true;
            Debug.Log("[Audio] 无线电杂音 ON");
        }

        /// <summary>
        /// 停止无线电杂音
        /// </summary>
        public void StopRadioStatic()
        {
            if (!isRadioPlaying || radioSource == null) return;
            radioSource.Stop();
            isRadioPlaying = false;
            Debug.Log("[Audio] 无线电杂音 OFF");
        }

        /// <summary>
        /// 切换无线电杂音
        /// </summary>
        public void ToggleRadioStatic()
        {
            if (isRadioPlaying) StopRadioStatic();
            else StartRadioStatic();
        }

        #endregion

        #region 短音效（炮声 & 爆炸）

        /// <summary>
        /// 播放炮声
        /// </summary>
        public void PlayCannon()
        {
            PlayOneShotFromPool(cannonClips);
        }

        /// <summary>
        /// 播放炮声（指定位置，3D 音效）
        /// </summary>
        public void PlayCannon(Vector3 worldPosition)
        {
            PlayOneShotFromPool(cannonClips, worldPosition);
        }

        /// <summary>
        /// 播放爆炸声
        /// </summary>
        public void PlayExplosion()
        {
            PlayOneShotFromPool(explosionClips);
        }

        /// <summary>
        /// 播放爆炸声（指定位置，3D 音效）
        /// </summary>
        public void PlayExplosion(Vector3 worldPosition)
        {
            PlayOneShotFromPool(explosionClips, worldPosition);
        }

        /// <summary>
        /// 通用单次音效播放
        /// </summary>
        public void PlaySFX(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            var src = GetPooledSource();
            if (src == null) return;
            src.clip = clip;
            src.volume = sfxVolume * masterVolume * volume;
            src.spatialBlend = 0f;
            src.Play();
            StartCoroutine(ReturnToPool(src, clip.length));
        }

        /// <summary>
        /// 通用单次音效播放（3D 位置）
        /// 使用 AudioSource.PlayClipAtPoint 实现，无需共享 Transform
        /// </summary>
        public void PlaySFX(AudioClip clip, Vector3 worldPosition, float volume = 1f)
        {
            if (clip == null) return;
            // PlayClipAtPoint 创建临时 GameObject，播完自动销毁
            AudioSource.PlayClipAtPoint(clip, worldPosition, sfxVolume * masterVolume * volume);
        }

        #endregion

        #region SFX 对象池

        private AudioSource GetPooledSource()
        {
            // 从池中取一个可用的 AudioSource
            int attempts = sfxPool.Count;
            while (attempts > 0)
            {
                var src = sfxPool.Dequeue();
                if (!src.isPlaying)
                    return src;
                sfxPool.Enqueue(src);
                attempts--;
            }

            // 池满且全在用，强制复用最旧的
            Debug.LogWarning("[Audio] SFX 池已满，复用最早音源");
            var oldest = sfxPool.Dequeue();
            oldest.Stop();
            return oldest;
        }

        private IEnumerator ReturnToPool(AudioSource src, float delay)
        {
            yield return new WaitForSeconds(delay + 0.1f);
            src.spatialBlend = 0f;
            sfxPool.Enqueue(src);
        }

        private void PlayOneShotFromPool(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip);
        }

        private void PlayOneShotFromPool(AudioClip[] clips, Vector3 position)
        {
            if (clips == null || clips.Length == 0) return;
            var clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip, position);
        }

        #endregion

        #region 额外音效

        /// <summary>
        /// 播放警报声
        /// </summary>
        public void PlayAlarm(AudioClip clip = null)
        {
            if (clip != null)
                PlaySFX(clip);
            else
                Debug.Log("[Audio] 警报声 (无 Clip，跳过)");
        }

        /// <summary>
        /// 播放发报机/电键声
        /// </summary>
        public void PlayTelegraph(AudioClip clip = null)
        {
            if (clip != null)
                PlaySFX(clip);
            else
                Debug.Log("[Audio] 发报机声 (无 Clip，跳过)");
        }

        /// <summary>
        /// 播放胜利音乐
        /// </summary>
        public void PlayVictory(AudioClip clip = null)
        {
            if (clip != null)
                PlayBGM(clip);
            else
                Debug.Log("[Audio] 胜利音乐 (无 Clip，跳过)");
        }

        /// <summary>
        /// 播放失败音乐
        /// </summary>
        public void PlayDefeat(AudioClip clip = null)
        {
            if (clip != null)
                PlayBGM(clip);
            else
                Debug.Log("[Audio] 失败音乐 (无 Clip，跳过)");
        }

        #endregion

        #region 音量控制

        private void RefreshVolumes()
        {
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (radioSource != null) radioSource.volume = radioVolume * masterVolume;
        }

        /// <summary>
        /// 设置静音
        /// </summary>
        public void SetMute(bool mute)
        {
            AudioListener.volume = mute ? 0f : 1f;
        }

        #endregion

        #region 事件驱动

        private void HandlePhaseChanged(CampaignPhase phase)
        {
            switch (phase)
            {
                case CampaignPhase.Briefing:
                    // 简报阶段，保持默认 BGM，不开无线电
                    break;

                case CampaignPhase.FirstWaveLanding:
                case CampaignPhase.SecondWaveLanding:
                    // 战斗阶段切换战斗 BGM，开启无线电杂音
                    if (combatBGM != null)
                        PlayBGMWithFade(combatBGM);
                    StartRadioStatic();
                    break;

                case CampaignPhase.CounterAttack:
                    // 反击阶段 — 炮声增强暗示
                    if (combatBGM != null)
                        PlayBGMWithFade(combatBGM);
                    break;

                case CampaignPhase.CriticalDecision:
                    // 关键决策 — 停 BGM 营造紧张感
                    StopBGM();
                    break;

                case CampaignPhase.Resolution:
                    // 战役结束 — 恢复默认 BGM，关闭无线电
                    StopRadioStatic();
                    if (defaultBGM != null)
                        PlayBGMWithFade(defaultBGM);
                    break;
            }
        }

        /// <summary>
        /// 指令发送时播放无线电杂音脉冲
        /// </summary>
        private void HandleCommandDelivered(RadioCommand cmd)
        {
            if (cmd == null) return;
            // 短暂开启无线电杂音模拟通讯
            if (!isRadioPlaying)
            {
                StartRadioStatic();
                StartCoroutine(StopRadioAfterDelay(2f));
            }
        }

        private IEnumerator StopRadioAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            // 只在战斗阶段外自动关闭
            StopRadioStatic();
        }

        /// <summary>
        /// 战场数据更新 — 根据战斗状态触发音效
        /// </summary>
        private void HandleBattlefieldUpdated(BattlefieldData data)
        {
            if (data == null) return;

            // 桥梁受损时播放爆炸音效
            if (data.BridgeHP < 80)
            {
                // 降低概率，避免过于频繁
                if (Random.value < 0.3f)
                    PlayExplosion();
            }

            // 高波次时增强炮声频率
            if (data.CurrentWave >= 3 && Random.value < 0.2f)
            {
                PlayCannon();
            }
        }

        #endregion
    }
}
