// BattleAudioSystem.cs — 战斗音效系统
// 管理远处战场的枪声、炮声、爆炸等动态音效
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;
using System.Collections.Generic;

namespace SWO1.Audio
{
    /// <summary>
    /// 战斗事件类型
    /// </summary>
    public enum BattleEventType
    {
        DistantArtillery,   // 远处炮击
        MachineGunBurst,    // 机枪连射
        SingleShot,         // 单发步枪
        Explosion,          // 爆炸
        AircraftPass,       // 飞机过顶
        ShipGunFire         // 本舰开炮
    }

    /// <summary>
    /// 战斗事件数据
    /// </summary>
    public struct BattleEvent
    {
        public BattleEventType type;
        public Vector3 worldPosition;    // 事件实际世界坐标
        public float intensity;          // 0-1 强度
        public float baseDelay;          // 基础延迟 (秒)
    }

    /// <summary>
    /// 战斗音效系统
    /// 负责远处战场音效的距离衰减、时间延迟、随机触发
    /// </summary>
    public class BattleAudioSystem : MonoBehaviour
    {
        [Header("FMOD 事件引用")]
        [SerializeField] private EventReference artilleryDistant;
        [SerializeField] private EventReference mgBurst;
        [SerializeField] private EventReference explosion;
        [SerializeField] private EventReference aircraftPass;
        [SerializeField] private EventReference shipGunFire;
        [SerializeField] private EventReference singleShot;

        [Header("随机触发设置")]
        [SerializeField] private float artilleryIntervalMin = 8f;
        [SerializeField] private float artilleryIntervalMax = 25f;
        [SerializeField] private float mgIntervalMin = 15f;
        [SerializeField] private float mgIntervalMax = 45f;
        [SerializeField] private float explosionIntervalMin = 12f;
        [SerializeField] private float explosionIntervalMax = 40f;
        [SerializeField] private float aircraftIntervalMin = 60f;
        [SerializeField] private float aircraftIntervalMax = 180f;

        [Header("距离模型")]
        [SerializeField] private float soundSpeed = 343f; // m/s
        [SerializeField] private Transform listenerPosition; // 指挥所位置
        [SerializeField] private float maxBattleDistance = 15000f; // 15km 最大距离

        // 战斗状态
        private float _currentIntensity = 0.3f;
        private float _currentAvgDistance = 0.5f;
        private readonly Queue<BattleEvent> _eventQueue = new();

        // 定时器
        private float _nextArtillery;
        private float _nextMG;
        private float _nextExplosion;
        private float _nextAircraft;

        // 活跃事件实例追踪
        private readonly List<EventInstance> _activeInstances = new();

        public float CurrentIntensity => _currentIntensity;

        private void Start()
        {
            ResetTimers();
        }

        private void ResetTimers()
        {
            float now = Time.time;
            _nextArtillery = now + Random.Range(artilleryIntervalMin, artilleryIntervalMax);
            _nextMG = now + Random.Range(mgIntervalMin, mgIntervalMax);
            _nextExplosion = now + Random.Range(explosionIntervalMin, explosionIntervalMax);
            _nextAircraft = now + Random.Range(aircraftIntervalMin, aircraftIntervalMax);
        }

        private void Update()
        {
            float now = Time.time;

            // 根据战斗强度调整触发频率
            float intensityMultiplier = Mathf.Lerp(3f, 0.5f, _currentIntensity); // 强度越高，间隔越短

            if (now >= _nextArtillery)
            {
                QueueBattleEvent(BattleEventType.DistantArtillery, GetRandomBattlePosition());
                _nextArtillery = now + Random.Range(artilleryIntervalMin, artilleryIntervalMax) * intensityMultiplier;
            }

            if (now >= _nextMG)
            {
                QueueBattleEvent(BattleEventType.MachineGunBurst, GetRandomBattlePosition());
                _nextMG = now + Random.Range(mgIntervalMin, mgIntervalMax) * intensityMultiplier;
            }

            if (now >= _nextExplosion)
            {
                QueueBattleEvent(BattleEventType.Explosion, GetRandomBattlePosition());
                _nextExplosion = now + Random.Range(explosionIntervalMin, explosionIntervalMax) * intensityMultiplier;
            }

            if (now >= _nextAircraft)
            {
                QueueBattleEvent(BattleEventType.AircraftPass, GetAircraftPosition());
                _nextAircraft = now + Random.Range(aircraftIntervalMin, aircraftIntervalMax);
            }

            // 处理队列
            ProcessEventQueue();
        }

        /// <summary>
        /// 入队战斗事件（带距离延迟）
        /// </summary>
        public void QueueBattleEvent(BattleEventType type, Vector3 worldPos, float intensity = 0.5f)
        {
            float distance = GetDistanceToListener(worldPos);
            float delay = distance / soundSpeed; // 声音传播延迟

            _eventQueue.Enqueue(new BattleEvent
            {
                type = type,
                worldPosition = worldPos,
                intensity = intensity,
                baseDelay = delay
            });
        }

        /// <summary>
        /// 直接触发战斗事件（无距离延迟，用于近距离事件）
        /// </summary>
        public void TriggerBattleEvent(BattleEventType type, float intensity = 0.5f)
        {
            StartCoroutine(DelayedPlay(type, intensity, 0f));
        }

        private void ProcessEventQueue()
        {
            while (_eventQueue.Count > 0)
            {
                var evt = _eventQueue.Dequeue();
                float distance = GetDistanceToListener(evt.worldPosition);
                float delay = distance / soundSpeed;

                // 触发播放（协程处理延迟）
                StartCoroutine(DelayedPlay(evt.type, evt.intensity, delay, distance));
            }
        }

        private IEnumerator DelayedPlay(BattleEventType type, float intensity, float delay, float distance = 0f)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            PlayBattleSound(type, intensity, distance);
        }

        /// <summary>
        /// 播放战斗音效
        /// </summary>
        private void PlayBattleSound(BattleEventType type, float intensity, float distance)
        {
            EventReference eventRef = GetEventReference(type);
            if (eventRef.IsNull) return;

            var instance = RuntimeManager.CreateInstance(eventRef);

            // 距离衰减参数
            float normalizedDistance = Mathf.Clamp01(distance / maxBattleDistance);
            instance.setParameterByName("BattleDistance", normalizedDistance);
            instance.setParameterByName("BattleIntensity", _currentIntensity);
            instance.setParameterByName("EventIntensity", intensity);

            // 随机化音高（每次炮声/枪声略有不同）
            float pitchRandom = Random.Range(-0.05f, 0.05f);
            instance.setPitch(1f + pitchRandom);

            instance.start();

            // 追踪实例以确保清理
            _activeInstances.Add(instance);

            // 自动停止和释放
            StartCoroutine(StopAndRelease(instance, 10f));
        }

        private IEnumerator StopAndRelease(EventInstance instance, float maxDuration)
        {
            yield return new WaitForSeconds(maxDuration);

            PLAYBACK_STATE state;
            instance.getPlaybackState(out state);
            if (state != PLAYBACK_STATE.STOPPED)
                instance.stop(FMOD.STOP_MODE.ALLOWFADEOUT);

            instance.release();
            _activeInstances.Remove(instance);
        }

        private EventReference GetEventReference(BattleEventType type)
        {
            return type switch
            {
                BattleEventType.DistantArtillery => artilleryDistant,
                BattleEventType.MachineGunBurst => mgBurst,
                BattleEventType.Explosion => explosion,
                BattleEventType.AircraftPass => aircraftPass,
                BattleEventType.ShipGunFire => shipGunFire,
                BattleEventType.SingleShot => singleShot,
                _ => artilleryDistant,
            };
        }

        /// <summary>
        /// 获取随机战斗位置（战场在岸边 1-5km 范围）
        /// </summary>
        private Vector3 GetRandomBattlePosition()
        {
            float distance = Random.Range(500f, 5000f);
            float angle = Random.Range(0f, 360f);
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) * distance
            );
            return (listenerPosition ? listenerPosition.position : Vector3.zero) + offset;
        }

        /// <summary>
        /// 飞机位置（从一侧飞到另一侧，距离较远）
        /// </summary>
        private Vector3 GetAircraftPosition()
        {
            float distance = Random.Range(500f, 2000f);
            float height = Random.Range(300f, 800f);
            return (listenerPosition ? listenerPosition.position : Vector3.zero)
                + Vector3.forward * distance + Vector3.up * height;
        }

        private float GetDistanceToListener(Vector3 worldPos)
        {
            if (listenerPosition == null) return 1000f; // 默认 1km
            return Vector3.Distance(listenerPosition.position, worldPos);
        }

        /// <summary>
        /// 更新战斗状态（由 SimulationManager 驱动）
        /// </summary>
        public void UpdateBattleState(float intensity, float avgDistance)
        {
            _currentIntensity = Mathf.Clamp01(intensity);
            _currentAvgDistance = Mathf.Clamp01(avgDistance);

            AudioManager.Instance.UpdateBattleState(intensity, avgDistance);
        }

        /// <summary>
        /// 触发特定战斗事件（由事件系统驱动，如"第 1 连遭遇交火"）
        /// </summary>
        public void TriggerCombatEvent(BattleEventType type, Vector3 worldPosition, float intensity)
        {
            float distance = GetDistanceToListener(worldPosition);
            float delay = distance / soundSpeed;

            // 如果距离太远（超过最大距离的 80%），可能听不到
            if (distance > maxBattleDistance * 0.8f)
                intensity *= 0.3f;

            QueueBattleEvent(type, worldPosition, intensity);
        }

        /// <summary>
        /// 触发舰炮开火（本舰，无距离延迟）
        /// </summary>
        public void FireShipGun()
        {
            var instance = RuntimeManager.CreateInstance(shipGunFire);
            instance.setParameterByName("BattleDistance", 0.05f); // 很近
            instance.setParameterByName("BattleIntensity", _currentIntensity);
            instance.start();
            instance.release();

            // 舰炮的远距离回声
            StartCoroutine(DelayedPlay(BattleEventType.ShipGunFire, 0.3f, 2f, 3000f));
        }

        private void OnDestroy()
        {
            foreach (var instance in _activeInstances)
            {
                instance.stop(FMOD.STOP_MODE.IMMEDIATE);
                instance.release();
            }
            _activeInstances.Clear();
        }
    }
}
