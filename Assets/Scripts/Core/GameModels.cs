// GameModels.cs — 公共数据模型
// 集中管理跨模块共享的枚举和数据结构
using UnityEngine;
using System;

namespace SWO1.Core
{
    /// <summary>
    /// 战役结局（别名，部分模块引用此名称）
    /// </summary>
    public enum CampaignOutcome
    {
        InProgress,
        PerfectVictory,
        PyrrhicVictory,
        PartialVictory,
        Defeat,
        TotalDefeat
    }

    /// <summary>
    /// 沙盘标记事件
    /// </summary>
    [Serializable]
    public class SandTableEvent
    {
        public string EntryId;
        public Vector3 Position;
        public SandTableAction Action;
        public string UnitId;
        public bool IsEnemy;
    }

    public enum SandTableAction
    {
        Added,
        Updated,
        Removed,
        Moved
    }

    /// <summary>
    /// 部队状态快照 — 供各系统读取
    /// </summary>
    [Serializable]
    public class UnitStatusSnapshot
    {
        public string UnitId;
        public float Morale;
        public int TroopCount;
        public float AmmoLevel;
        public bool IsInCombat;
        public Vector3 Position;
        public float Timestamp;

        public string MoraleDescription
        {
            get
            {
                if (Morale >= 80) return "士气高昂";
                if (Morale >= 50) return "状态正常";
                if (Morale >= 30) return "士气动摇";
                return "濒临崩溃";
            }
        }

        public string AmmoDescription
        {
            get
            {
                if (AmmoLevel >= 70) return "弹药充足";
                if (AmmoLevel >= 40) return "弹药消耗中";
                if (AmmoLevel >= 20) return "弹药不足";
                return "弹药告急";
            }
        }
    }

    /// <summary>
    /// 通用事件数据 — 用于跨模块事件传递
    /// </summary>
    [Serializable]
    public class GenericEventData
    {
        public string EventId;
        public string SourceModule;
        public string EventType;
        public float Timestamp;
        public string StringData;
        public int IntData;
        public float FloatData;
    }
}
