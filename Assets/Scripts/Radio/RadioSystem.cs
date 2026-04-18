using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SWO1.Radio
{
    public enum ReportType { Terrain, Enemy, Status, Request, Retreat, Reinforcement }

    public enum CommandType { Move, Defend, Scout, Attack, Retreat, Status, Wait }

    public class RadioMessage
    {
        public string Id;
        public ReportType Type;
        public string Sender;
        public string Content;
        public int GridX, GridY;
        public float GameTime;
        public float Delay;
        public bool IsInterfered;
        public bool IsDelivered;
        public float Accuracy;
    }

    public class RadioCommand
    {
        public string CommandId = System.Guid.NewGuid().ToString();
        public string TargetUnit;
        public float SentTime;
        public string Content;
        public CommandType Type;
        public int TargetX, TargetY;
        public int Frequency;
        public float Delay;
        public bool IsLost;
        public string MisreadContent;
    }

    public class RadioSystem : MonoBehaviour
    {
        public event Action<RadioMessage> OnReportDelivered;
        public event Action<RadioCommand> OnCommandDelivered;
        public event Action<RadioCommand> OnCommandLost;
        public event Action<string> OnRadioInterference;

        private readonly System.Random _rng = new System.Random();
        private int _msgCounter;

        // ─── 频率参数 ───
        // 频率 1-5：频率越高 → 延迟越低、干扰越大
        // 频率 1 → 延迟 8~15s, 干扰率 0%
        // 频率 5 → 延迟 1~3s,  干扰率 25%
        private static readonly float[] MinDelayPerFreq = { 8f, 5f, 3.5f, 2f, 1f };
        private static readonly float[] MaxDelayPerFreq = { 15f, 10f, 7f, 5f, 3f };
        private static readonly float[] InterferencePerFreq = { 0f, 0.05f, 0.10f, 0.18f, 0.25f };
        private static readonly float[] CommandLossPerFreq = { 0.02f, 0.05f, 0.10f, 0.15f, 0.22f };
        private static readonly string[] CommandNames =
        {
            "移动", "防御", "侦察", "进攻", "撤退", "报告状态", "待命"
        };

        // ═══════════════════════════════════════
        //  汇报生成
        // ═══════════════════════════════════════

        public RadioMessage GenerateTerrainReport(string sender, int gx, int gy, TerrainType terrain)
        {
            string content = $"报告：{GridLabel(gx, gy)} 附近发现{TerrainName(terrain)}";
            return CreateReport(ReportType.Terrain, sender, content, gx, gy, 0.9f);
        }

        public RadioMessage GenerateEnemyReport(string sender, int gx, int gy, int enemyCount)
        {
            string content = $"注意！{GridLabel(gx, gy)} 方向发现约 {enemyCount} 名敌军";
            return CreateReport(ReportType.Enemy, sender, content, gx, gy, 0.7f);
        }

        public RadioMessage GenerateStatusReport(string sender, float hp, float morale)
        {
            string content = $"状态汇报：兵力 {hp * 100:F0}%，士气 {(morale > 0.6f ? "良好" : morale > 0.3f ? "一般" : "低落")}";
            return CreateReport(ReportType.Status, sender, content, 0, 0, 1f);
        }

        public RadioMessage GenerateRetreatReport(string sender, int gx, int gy)
        {
            string content = $"请求撤退！当前位置 {GridLabel(gx, gy)} 已无法坚守";
            return CreateReport(ReportType.Retreat, sender, content, gx, gy, 0.8f);
        }

        public RadioMessage GenerateRequestReport(string sender, int gx, int gy)
        {
            string content = $"请求增援！{GridLabel(gx, gy)} 方向急需支援";
            return CreateReport(ReportType.Request, sender, content, gx, gy, 0.85f);
        }

        public RadioMessage GenerateReinforcementWarning(int gx, int gy, string direction)
        {
            string content = $"警告！{direction} 方向 {GridLabel(gx, gy)} 发现敌军增援";
            return CreateReport(ReportType.Reinforcement, "侦察兵", content, gx, gy, 0.6f);
        }

        // ═══════════════════════════════════════
        //  指令发送
        // ═══════════════════════════════════════

        public void SendCommand(RadioCommand cmd)
        {
            if (cmd == null) return;

            // 频率参数
            int freqIdx = Mathf.Clamp(cmd.Frequency - 1, 0, 4);
            float lossChance = CommandLossPerFreq[freqIdx];

            // 如果调用者没指定 Delay，根据频率自动计算
            if (cmd.Delay <= 0f)
            {
                float minD = MinDelayPerFreq[freqIdx];
                float maxD = MaxDelayPerFreq[freqIdx];
                cmd.Delay = (float)(_rng.NextDouble() * (maxD - minD) + minD);
            }

            // 丢失判定
            cmd.IsLost = _rng.NextDouble() < lossChance;

            // 误解内容生成（低概率发生）
            if (!cmd.IsLost && _rng.NextDouble() < 0.08f)
            {
                cmd.MisreadContent = GenerateMisread(cmd);
            }

            StartCoroutine(DeliverCommandCoroutine(cmd));
        }

        // ═══════════════════════════════════════
        //  内部：汇报创建
        // ═══════════════════════════════════════

        private RadioMessage CreateReport(ReportType type, string sender, string content, int gx, int gy, float accuracy)
        {
            var msg = new RadioMessage
            {
                Id = $"RPT-{++_msgCounter:D4}",
                Type = type,
                Sender = sender,
                Content = content,
                GridX = gx,
                GridY = gy,
                GameTime = Time.time / 60f,
                Accuracy = accuracy,
                IsDelivered = false,
            };

            // 干扰判定（基于 Accuracy）
            float interferenceChance = Mathf.Lerp(0.30f, 0f, accuracy);
            msg.IsInterfered = _rng.NextDouble() < interferenceChance;

            // 坐标偏移
            if (accuracy < 1f)
            {
                int maxOffset = Mathf.CeilToInt((1f - accuracy) * 3f);
                msg.GridX += _rng.Next(-maxOffset, maxOffset + 1);
                msg.GridY += _rng.Next(-maxOffset, maxOffset + 1);
            }

            // 延迟：2~8s 基础，准确性越低延迟越大
            msg.Delay = (float)(_rng.NextDouble() * (6f * (1f - accuracy) + 2f) + 1f);

            StartCoroutine(DeliverReportCoroutine(msg));
            return msg;
        }

        // ═══════════════════════════════════════
        //  内部：协程处理
        // ═══════════════════════════════════════

        private IEnumerator DeliverReportCoroutine(RadioMessage msg)
        {
            yield return new WaitForSeconds(msg.Delay);

            msg.IsDelivered = true;

            // 干扰文字替换
            string deliveredContent = msg.Content;
            if (msg.IsInterfered)
            {
                deliveredContent = ApplyInterference(msg.Content);
                OnRadioInterference?.Invoke($"[{msg.Sender}] 信号受到干扰");
            }

            // 用替换后的内容更新（保留原始 Content 引用由调用者处理）
            msg.Content = deliveredContent;

            OnReportDelivered?.Invoke(msg);
        }

        private IEnumerator DeliverCommandCoroutine(RadioCommand cmd)
        {
            yield return new WaitForSeconds(cmd.Delay);

            if (cmd.IsLost)
            {
                OnCommandLost?.Invoke(cmd);
            }
            else
            {
                OnCommandDelivered?.Invoke(cmd);
            }
        }

        // ═══════════════════════════════════════
        //  内部：干扰效果
        // ═══════════════════════════════════════

        private string ApplyInterference(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            char[] chars = text.ToCharArray();
            // 替换 10%~30% 的字符
            double ratio = _rng.NextDouble() * 0.2 + 0.1;
            int replaceCount = Mathf.Max(1, Mathf.RoundToInt(chars.Length * (float)ratio));

            // 随机选择要替换的位置
            var indices = new List<int>();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsWhiteSpace(chars[i]) && chars[i] != '：' && chars[i] != '，' && chars[i] != '！')
                    indices.Add(i);
            }

            // Fisher-Yates 部分打乱取前 N 个
            for (int i = 0; i < Mathf.Min(replaceCount, indices.Count); i++)
            {
                int j = _rng.Next(i, indices.Count);
                (indices[i], indices[j]) = (indices[j], indices[i]);
                chars[indices[i]] = '▓';
            }

            return new string(chars);
        }

        // ═══════════════════════════════════════
        //  内部：指令误解
        // ═══════════════════════════════════════

        private string GenerateMisread(RadioCommand cmd)
        {
            // 随机把指令类型换为另一个
            var otherTypes = new List<CommandType>();
            for (int i = 0; i < CommandNames.Length; i++)
            {
                if ((CommandType)i != cmd.Type)
                    otherTypes.Add((CommandType)i);
            }

            int pick = _rng.Next(otherTypes.Count);
            return $"{cmd.TargetUnit}，误以为收到指令：{CommandNames[(int)otherTypes[pick]]}";
        }

        // ═══════════════════════════════════════
        //  工具方法
        // ═══════════════════════════════════════

        private string GridLabel(int gx, int gy)
        {
            // A=0, B=1 ... Z=25
            char col = (char)('A' + Mathf.Clamp(gx, 0, 25));
            return $"{col}{gy + 1}";
        }

        private string TerrainName(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest:   return "树林";
                case TerrainType.Hill:     return "高地";
                case TerrainType.River:    return "河流";
                case TerrainType.Swamp:    return "沼泽";
                case TerrainType.Road:     return "道路";
                case TerrainType.Plain:    return "平原";
                case TerrainType.Ruins:    return "废墟";
                case TerrainType.Bridge:   return "桥梁";
                default:                   return "地形";
            }
        }
    }

    /// <summary>
    /// 地形类型枚举 — 与游戏主逻辑共用。
    /// 如果项目已有 TerrainType 定义，可删除此重复定义。
    /// </summary>
    public enum TerrainType
    {
        Forest, Hill, River, Swamp, Road, Plain, Ruins, Bridge
    }
}
