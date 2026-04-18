// BattleSimulationInterface.cs — 战场模拟系统对接接口
// 提供信息系统、指挥系统与战场模拟之间的桥接
// 当战场模拟模块完全实现后，此类直接对接 SimulationManager
using UnityEngine;
using System;
using SWO1.Core;

namespace SWO1.Intelligence
{
    /// <summary>
    /// 战场模拟对接接口（单例）
    /// 
    /// 职责：
    /// - 作为信息系统与战场模拟之间的桥梁
    /// - 提供部队状态查询
    /// - 向信息系统推送战场事件
    /// - 供指挥系统查询部队士气
    /// 
    /// 当 SimulationManager 完全实现后：
    /// - 此类的方法直接代理到 SimulationManager
    /// - 可以逐步从默认值过渡到真实模拟数据
    /// </summary>
    public class BattleSimulationInterface : MonoBehaviour
    {
        public static BattleSimulationInterface Instance { get; private set; }

        [Header("默认部队状态")]
        [SerializeField] private float defaultMorale = 65f;
        [SerializeField] private int defaultTroopCount = 150;
        [SerializeField] private float defaultAmmo = 60f;

        // 部队实时状态缓存（由模拟系统更新，或使用默认值）
        private System.Collections.Generic.Dictionary<string, UnitStatusSnapshot> unitStatusCache
            = new System.Collections.Generic.Dictionary<string, UnitStatusSnapshot>();

        // 事件推送回调
        private Action<BattleEvent> battleEventCallback;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // 初始化默认部队状态
            InitializeDefaultUnits();
        }

        #region 部队状态查询

        /// <summary>
        /// 获取部队当前状态
        /// </summary>
        public UnitStatusSnapshot GetUnitStatus(string unitId)
        {
            if (unitStatusCache.TryGetValue(unitId, out var status))
            {
                // 更新时间戳
                status.Timestamp = Time.time;
                return status;
            }

            // 返回默认状态
            return CreateDefaultStatus(unitId);
        }

        /// <summary>
        /// 获取部队士气（供指挥系统查询误解概率）
        /// </summary>
        public float GetUnitMorale(string unitId)
        {
            if (unitStatusCache.TryGetValue(unitId, out var status))
            {
                return status.Morale;
            }
            return defaultMorale;
        }

        /// <summary>
        /// 更新部队状态（由模拟系统调用）
        /// </summary>
        public void UpdateUnitStatus(string unitId, UnitStatusSnapshot status)
        {
            unitStatusCache[unitId] = status;

            // 如果状态变化显著，自动生成战场事件推送给信息系统
            CheckAndGenerateBattleEvent(unitId, status);
        }

        #endregion

        #region 战场事件推送

        /// <summary>
        /// 请求部队状态汇报
        /// 信息系统调用此方法，模拟系统生成对应的战场事件
        /// </summary>
        public void RequestUnitStatus(string unitId)
        {
            Debug.Log($"[SimInterface] 请求 {unitId} 状态汇报");

            // 如果有模拟系统，由它生成事件
            // 否则从缓存状态生成
            var status = GetUnitStatus(unitId);
            var evt = new BattleEvent
            {
                EventId = Guid.NewGuid().ToString(),
                UnitId = unitId,
                EventTime = Time.time,
                Type = status.IsInCombat ? BattleEventType.Engagement : BattleEventType.Movement,
                Position = status.Position,
                ActualTroopCount = status.TroopCount,
                ActualMorale = status.Morale,
                ActualAmmo = status.AmmoLevel,
                Description = "状态汇报请求"
            };

            battleEventCallback?.Invoke(evt);
        }

        /// <summary>
        /// 注册事件推送回调
        /// 信息系统调用此方法接收战场事件
        /// </summary>
        public void RegisterEventCallback(Action<BattleEvent> callback)
        {
            battleEventCallback += callback;
        }

        /// <summary>
        /// 取消注册回调
        /// </summary>
        public void UnregisterEventCallback(Action<BattleEvent> callback)
        {
            battleEventCallback -= callback;
        }

        /// <summary>
        /// 直接推送战场事件
        /// 模拟系统调用此方法向信息系统推送实时战场数据
        /// </summary>
        public void PushBattleEvent(BattleEvent evt)
        {
            battleEventCallback?.Invoke(evt);
        }

        #endregion

        #region 内部方法

        private void InitializeDefaultUnits()
        {
            unitStatusCache["company_1"] = new UnitStatusSnapshot
            {
                UnitId = "company_1",
                Morale = 75f,
                TroopCount = 180,
                AmmoLevel = 80f,
                IsInCombat = false,
                Position = new Vector3(100f, 0f, 50f),
                Timestamp = Time.time
            };

            unitStatusCache["company_2"] = new UnitStatusSnapshot
            {
                UnitId = "company_2",
                Morale = 60f,
                TroopCount = 160,
                AmmoLevel = 65f,
                IsInCombat = false,
                Position = new Vector3(200f, 0f, 100f),
                Timestamp = Time.time
            };

            unitStatusCache["tank_platoon"] = new UnitStatusSnapshot
            {
                UnitId = "tank_platoon",
                Morale = 80f,
                TroopCount = 45,
                AmmoLevel = 90f,
                IsInCombat = false,
                Position = new Vector3(150f, 0f, 80f),
                Timestamp = Time.time
            };

            unitStatusCache["naval_gunfire"] = new UnitStatusSnapshot
            {
                UnitId = "naval_gunfire",
                Morale = 90f,
                TroopCount = 30,
                AmmoLevel = 95f,
                IsInCombat = false,
                Position = new Vector3(0f, 0f, 0f),
                Timestamp = Time.time
            };
        }

        private UnitStatusSnapshot CreateDefaultStatus(string unitId)
        {
            return new UnitStatusSnapshot
            {
                UnitId = unitId,
                Morale = defaultMorale,
                TroopCount = defaultTroopCount,
                AmmoLevel = defaultAmmo,
                IsInCombat = false,
                Position = Vector3.zero,
                Timestamp = Time.time
            };
        }

        private void CheckAndGenerateBattleEvent(string unitId, UnitStatusSnapshot newStatus)
        {
            if (!unitStatusCache.ContainsKey(unitId)) return;

            var oldStatus = unitStatusCache[unitId];

            // 检测重大状态变化并自动生成事件
            if (newStatus.Morale < 30f && oldStatus.Morale >= 30f)
            {
                PushBattleEvent(new BattleEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    UnitId = unitId,
                    EventTime = Time.time,
                    Type = BattleEventType.MoraleChange,
                    Position = newStatus.Position,
                    ActualTroopCount = newStatus.TroopCount,
                    ActualMorale = newStatus.Morale,
                    ActualAmmo = newStatus.AmmoLevel,
                    Description = "部队士气严重下降"
                });
            }

            if (newStatus.IsInCombat && !oldStatus.IsInCombat)
            {
                PushBattleEvent(new BattleEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    UnitId = unitId,
                    EventTime = Time.time,
                    Type = BattleEventType.Engagement,
                    Position = newStatus.Position,
                    ActualTroopCount = newStatus.TroopCount,
                    ActualMorale = newStatus.Morale,
                    ActualAmmo = newStatus.AmmoLevel,
                    Description = "部队进入交战状态"
                });
            }
        }

        #endregion
    }
}
