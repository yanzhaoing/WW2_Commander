// KnightAI.cs — 骑士 AI 飞鸽传书（LLM 集成）
using UnityEngine;
using System;

namespace SWO1.Medieval
{
    public class KnightAI : MonoBehaviour
    {
        public static KnightAI Instance { get; private set; }

        private SWO1.AI.LLMClient _llm;

        void Awake() { Instance = this; }

        void Start()
        {
            _llm = FindObjectOfType<SWO1.AI.LLMClient>();
            if (_llm == null)
                Debug.LogWarning("[KnightAI] LLMClient 未找到，飞鸽传书将使用默认文本");
        }

        /// <summary>让骑士用 LLM 回复指令</summary>
        public void GenerateResponse(KnightData knight, string playerCommand, string terrainText, Action<string> onComplete)
        {
            if (_llm == null)
            {
                onComplete?.Invoke(FallbackResponse(knight, playerCommand));
                return;
            }

            string systemPrompt = knight.Personality;
            string context = BuildContext(knight, playerCommand, terrainText);

            _llm.Chat(systemPrompt, context,
                (response) => onComplete?.Invoke(response),
                (error) => onComplete?.Invoke(FallbackResponse(knight, playerCommand))
            );
        }

        string BuildContext(KnightData knight, string command, string terrain)
        {
            var director = MedievalGameDirector.Instance;
            var sb = new System.Text.StringBuilder();

            sb.AppendLine($"【总指挥指令】{command}");
            sb.AppendLine();
            sb.AppendLine($"【你的状态】");
            sb.AppendLine($"姓名：{knight.DisplayName}（{knight.Title}）");
            sb.AppendLine($"人数：{knight.Troops}/{knight.MaxTroops}");
            sb.AppendLine($"战意：{knight.Morale}/{knight.MaxMorale}");
            sb.AppendLine($"位置：({knight.Position.x}, {knight.Position.y})");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(terrain))
            {
                sb.AppendLine($"【地形】{terrain}");
                sb.AppendLine();
            }

            // 显示已发现的敌人
            if (director != null)
            {
                var enemies = director.GetDiscoveredEnemies();
                if (enemies.Count > 0)
                {
                    sb.AppendLine("【已发现的敌军】");
                    foreach (var e in enemies)
                    {
                        sb.AppendLine($"- {e.DisplayName}: {e.Troops}人, 战意{e.Morale}");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("请用飞鸽传书的方式回复总指挥，描述你的位置、观察到的情况、以及下一步打算。");
            sb.AppendLine("回复要自然，像在中世纪战场上写信。不少于50字。");

            return sb.ToString();
        }

        string FallbackResponse(KnightData knight, string command)
        {
            switch (knight.Name)
            {
                case KnightName.DonQuixote:
                    return $"总指挥！{knight.DisplayName}已遵命行事。我的{ knight.Troops}名骑兵已准备就绪，战意高昂！任何敌人在我的骑枪面前都将化为尘土！为了荣誉！";
                case KnightName.Lancelot:
                    return $"总指挥，{knight.DisplayName}已收到指令。当前人数{knight.Troops}人，战意{knight.Morale}。已按命令执行，将在下一回合汇报进展。";
                case KnightName.ElCid:
                    return $"阁下，{knight.DisplayName}遵命。我军{knight.Troops}人已就位。前方区域尚无异常，但我已派出斥候侦察。请放心。";
                default:
                    return $"收到指令，{knight.DisplayName}正在执行。";
            }
        }

        /// <summary>生成敌军心理活动</summary>
        public string GenerateEnemyThought(EnemyData enemy, KnightData knight)
        {
            float ratio = (float)knight.Troops / enemy.Troops;

            if (ratio > 1.5f)
                return $"「又是骑兵？看那阵势...是正规骑士团。弟兄们，咱们惹不起，要不...跑吧？」";
            else if (ratio > 0.8f)
                return $"「他们来了。人数差不多，别慌。拿起武器，准备迎战！」";
            else
                return $"「这帮骑士人不多，围上去！干掉他们，马匹和装备就归咱们了！」";
        }

        /// <summary>生成战斗描述</summary>
        public string GenerateCombatNarration(KnightData knight, EnemyData enemy)
        {
            if (_llm == null)
                return FallbackCombat(knight, enemy);

            string prompt = $"你是中世纪战场旁白者。请描述以下战斗场景：\n" +
                $"{knight.DisplayName}（{knight.Troops}人，战意{knight.Morale}）vs {enemy.DisplayName}（{enemy.Troops}人，战意{enemy.Morale}）\n" +
                "用生动的中文描述战斗场面，不少于80字。包含骑枪冲锋、马匹嘶鸣、刀剑碰撞等元素。";

            string result = FallbackCombat(knight, enemy);
            _llm.Chat("你是中世纪战场旁白者，用中文生动描述战斗。", prompt,
                (r) => result = r,
                (e) => { }
            );
            return result;
        }

        string FallbackCombat(KnightData knight, EnemyData enemy)
        {
            return $"{knight.DisplayName}高举骑枪，率领{knight.Troops}名骑兵发起冲锋。" +
                   $"马蹄如雷，尘土飞扬。骑枪刺穿了{enemy.DisplayName}的防线，" +
                   $"刀剑碰撞声、战马嘶鸣声、伤者的哀嚎声响彻战场。" +
                   $"经过一番激战，{enemy.DisplayName}的阵线开始动摇。";
        }
    }
}
