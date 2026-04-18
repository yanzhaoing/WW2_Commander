using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SWO1.AI
{
    /// <summary>
    /// 将军人格数据
    /// </summary>
    [Serializable]
    public class GeneralPersonality
    {
        public string Name;           // "隆美尔"
        public string Title;          // "沙漠之狐"
        public string SystemPrompt;   // 完整的system prompt
        public Color NameColor;       // UI显示颜色
    }

    /// <summary>
    /// 将军决策结果
    /// </summary>
    [Serializable]
    public class GeneralDecision
    {
        public string Understanding;  // 对指令的理解
        public string Plan;           // 行动计划
        public string Action;         // 本回合行动描述
        public List<UnitOrder> Orders; // 具体单位移动指令
        public string Mood;           // 情绪: confident/cautious/frustrated/angry

        public GeneralDecision()
        {
            Orders = new List<UnitOrder>();
            Mood = "confident";
        }
    }

    /// <summary>
    /// 单位移动指令
    /// </summary>
    [Serializable]
    public class UnitOrder
    {
        public string UnitName;       // "装甲营"
        public int TargetX;           // 目标格子X
        public int TargetY;           // 目标格子Y
    }

    /// <summary>
    /// AI将军 - 代表一个AI将军，持有性格prompt，调用LLM生成决策
    /// </summary>
    public class GeneralAI : MonoBehaviour
    {
        [Header("将军配置")]
        public GeneralPersonality Personality;

        [Header("LLM客户端")]
        public LLMClient LLMClient;

        [Header("战场状态")]
        [Tooltip("视野范围(格子数)")]
        public int VisionRange = 3;

        [Tooltip("当前位置(网格坐标)")]
        public Vector2 GridPosition = Vector2.zero;

        [Tooltip("士气 0-100")]
        public float Morale = 80f;

        [Tooltip("麾下单位ID列表")]
        public System.Collections.Generic.List<string> UnitsUnderCommand = new System.Collections.Generic.List<string>();

        [Header("任务追踪")]
        [Tooltip("当前目标坐标")]
        public Vector2 CurrentTarget = new Vector2(-1, -1);

        [Tooltip("是否有活跃任务")]
        public bool HasActiveTask = false;

        [Tooltip("上次决策")]
        public GeneralDecision LastDecision = null;

        [Tooltip("本轮侦察情报")]
        public string LatestIntel = "";

        [Header("调试")]
        [Tooltip("是否启用详细日志")]
        public bool VerboseLogging = false;

        /// <summary>将军名称（从 Personality 读取）</summary>
        public string GeneralName => Personality != null ? Personality.Name : "未知";

        // 事件：决策完成
        public event Action<GeneralDecision> OnDecisionMade;

        /// <summary>
        /// 初始化将军
        /// </summary>
        public void Initialize(GeneralPersonality personality)
        {
            this.Personality = personality;
            
            // 如果没有指定LLM客户端，尝试在场景中寻找
            if (LLMClient == null)
            {
                LLMClient = FindFirstObjectByType<LLMClient>();
            }

            if (VerboseLogging)
            {
                Debug.Log($"[GeneralAI] 将军 {personality.Name} ({personality.Title}) 已初始化");
            }
        }

        private GameObject _mapMarker;

        void Start()
        {
            CreateMapMarker();
        }

        void Update()
        {
            // 每帧更新将军标记位置
            if (_mapMarker != null)
            {
                float offsetX = SWO1.UI.SandTable2D.GridOffsetX;
                _mapMarker.transform.position = new Vector3(
                    (GridPosition.x + 0.5f) + offsetX,
                    (GridPosition.y + 0.5f),
                    0f);
            }
        }

        /// <summary>
        /// 在地图上创建将军位置标记
        /// </summary>
        private void CreateMapMarker()
        {
            if (Personality == null) return;

            _mapMarker = new GameObject($"GenMarker_{Personality.Name}");
            var marker = _mapMarker;
            float offsetX = SWO1.UI.SandTable2D.GridOffsetX;
            float cx = (GridPosition.x + 0.5f) + offsetX;
            float cy = (GridPosition.y + 0.5f);
            marker.transform.position = new Vector3(cx, cy, 0f);

            // 彩色方块
            var sr = marker.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            sr.color = Personality.NameColor;
            sr.sortingOrder = 15;
            marker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            // 名字标签
            var label = new GameObject("Name");
            label.transform.SetParent(marker.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var tm = label.AddComponent<TextMesh>();
            tm.text = Personality.Name;
            tm.fontSize = 30;
            tm.characterSize = 0.08f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Personality.NameColor;
            tm.fontStyle = FontStyle.Bold;
            var mr = label.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 16;
        }

        /// <summary>
        /// 给定视野数据+玩家指令，调用LLM生成决策
        /// </summary>
        /// <param name="playerCommand">玩家指令</param>
        /// <param name="visibleMapData">视野内的地图数据</param>
        /// <param name="unitStatus">单位状态</param>
        /// <param name="onComplete">决策完成回调</param>
        public void MakeDecision(string playerCommand, string visibleMapData, string unitStatus, Action<GeneralDecision> onComplete)
        {
            if (Personality == null)
            {
                Debug.LogError("[GeneralAI] 将军人格未设置");
                onComplete?.Invoke(CreateFallbackDecision("将军人格未配置"));
                return;
            }

            if (LLMClient == null)
            {
                Debug.LogError("[GeneralAI] LLM客户端未设置");
                onComplete?.Invoke(CreateFallbackDecision("LLM客户端未配置"));
                return;
            }

            // 构造用户消息
            string userMessage = BuildUserMessage(playerCommand, visibleMapData, unitStatus);

            if (VerboseLogging)
            {
                Debug.Log($"[GeneralAI] {Personality.Name} 正在决策...");
            }

            // 调用LLM
            LLMClient.Chat(
                Personality.SystemPrompt,
                userMessage,
                (response) =>
                {
                    GeneralDecision decision = ParseResponse(response);
                    onComplete?.Invoke(decision);
                    OnDecisionMade?.Invoke(decision);
                },
                (error) =>
                {
                    Debug.LogError($"[GeneralAI] LLM调用失败: {error}");
                    GeneralDecision fallbackDecision = CreateFallbackDecision($"通信故障: {error}");
                    onComplete?.Invoke(fallbackDecision);
                    OnDecisionMade?.Invoke(fallbackDecision);
                }
            );
        }

        /// <summary>
        /// 构造用户消息
        /// </summary>
        private string BuildUserMessage(string playerCommand, string visibleMapData, string unitStatus)
        {
            return $"【总司令指令】\n{playerCommand}\n\n" +
                   $"【当前视野地图信息】\n{visibleMapData}\n\n" +
                   $"【我方单位状态】\n{unitStatus}\n\n" +
                   $"请根据以上信息，以JSON格式给出你的决策。";
        }

        /// <summary>
        /// 解析LLM返回的JSON为GeneralDecision
        /// 不使用第三方JSON库，使用正则表达式和字符串操作
        /// </summary>
        private GeneralDecision ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
            {
                return CreateFallbackDecision("空响应");
            }

            try
            {
                // 尝试提取JSON块（处理可能的markdown代码块）
                string jsonText = ExtractJsonFromResponse(response);
                
                if (string.IsNullOrEmpty(jsonText))
                {
                    // 如果没有找到JSON，把整个回复当Action文本
                    return CreateFallbackDecision(response.Trim());
                }

                GeneralDecision decision = new GeneralDecision();

                // 解析各个字段
                decision.Understanding = ExtractJsonField(jsonText, "understanding");
                decision.Plan = ExtractJsonField(jsonText, "plan");
                decision.Action = ExtractJsonField(jsonText, "action");
                decision.Mood = ExtractJsonField(jsonText, "mood");

                // 解析orders数组
                decision.Orders = ParseOrdersArray(jsonText);

                // 验证mood字段
                string[] validMoods = { "confident", "cautious", "frustrated", "angry" };
                if (string.IsNullOrEmpty(decision.Mood) || !System.Array.Exists(validMoods, m => m == decision.Mood.ToLower()))
                {
                    decision.Mood = "confident";
                }

                if (VerboseLogging)
                {
                    Debug.Log($"[GeneralAI] 成功解析决策: {decision.Action}");
                }

                return decision;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GeneralAI] 解析响应失败: {e.Message}\n原始响应: {response}");
                return CreateFallbackDecision(response.Trim());
            }
        }

        /// <summary>
        /// 从响应中提取JSON文本（处理markdown代码块）
        /// </summary>
        private string ExtractJsonFromResponse(string response)
        {
            // 尝试匹配 ```json ... ``` 代码块
            string pattern = "```(?:json)?\\s*([\\s\\S]*?)```";
            Match match = Regex.Match(response, pattern);
            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }

            // 尝试匹配 {...} 最外层的大括号
            int braceCount = 0;
            int startIndex = -1;
            for (int i = 0; i < response.Length; i++)
            {
                if (response[i] == '{')
                {
                    if (braceCount == 0) startIndex = i;
                    braceCount++;
                }
                else if (response[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0 && startIndex >= 0)
                    {
                        return response.Substring(startIndex, i - startIndex + 1);
                    }
                }
            }

            // 如果没有找到完整的JSON，返回原字符串
            return response;
        }

        /// <summary>
        /// 从JSON中提取字符串字段
        /// </summary>
        private string ExtractJsonField(string json, string fieldName)
        {
            // 匹配 "fieldName": "value" 或 "fieldName": "value with \\"escaped\\" quotes"
            string pattern = $"\\\"{fieldName}\\\"\\s*:\\s*\\\"((?:[^\"\\\\]|\\\\.)*)\\\"";
            Match match = Regex.Match(json, pattern);
            if (match.Success)
            {
                return UnescapeJsonString(match.Groups[1].Value);
            }

            return "";
        }

        /// <summary>
        /// 解析orders数组
        /// </summary>
        private List<UnitOrder> ParseOrdersArray(string json)
        {
            List<UnitOrder> orders = new List<UnitOrder>();

            // 尝试匹配 "orders": [...]
            string pattern = "\"orders\"\\s*:\\s*\\[([^\\]]*)\\]";
            Match match = Regex.Match(json, pattern);
            if (!match.Success) return orders;

            string ordersContent = match.Groups[1].Value;

            // 匹配数组中的每个对象
            string objectPattern = "\\{([^}]*)\\}";
            MatchCollection objectMatches = Regex.Matches(ordersContent, objectPattern);

            foreach (Match objMatch in objectMatches)
            {
                string objContent = objMatch.Groups[1].Value;
                UnitOrder order = new UnitOrder();

                // 解析unit字段
                string unitPattern = "\"(?:unit|unitName|Unit|UnitName)\"\\s*:\\s*\\\"((?:[^\"\\\\]|\\\\.)*)\\\"";
                Match unitMatch = Regex.Match(objContent, unitPattern);
                if (unitMatch.Success)
                {
                    order.UnitName = UnescapeJsonString(unitMatch.Groups[1].Value);
                }

                // 解析x字段
                string xPattern = "\"(?:x|X|targetX|TargetX)\"\\s*:\\s*(-?\\d+)";
                Match xMatch = Regex.Match(objContent, xPattern);
                if (xMatch.Success && int.TryParse(xMatch.Groups[1].Value, out int x))
                {
                    order.TargetX = x;
                }

                // 解析y字段
                string yPattern = "\"(?:y|Y|targetY|TargetY)\"\\s*:\\s*(-?\\d+)";
                Match yMatch = Regex.Match(objContent, yPattern);
                if (yMatch.Success && int.TryParse(yMatch.Groups[1].Value, out int y))
                {
                    order.TargetY = y;
                }

                if (!string.IsNullOrEmpty(order.UnitName))
                {
                    orders.Add(order);
                }
            }

            return orders;
        }

        /// <summary>
        /// 反转义JSON字符串
        /// </summary>
        private string UnescapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            
            return input
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\b", "\b")
                .Replace("\\f", "\f")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\/", "/");
        }

        /// <summary>
        /// 创建回退决策（当JSON解析失败时使用）
        /// </summary>
        private GeneralDecision CreateFallbackDecision(string actionText)
        {
            return new GeneralDecision
            {
                Understanding = "由于通信干扰，理解有限",
                Plan = "按照备用方案执行",
                Action = actionText,
                Orders = new List<UnitOrder>(),
                Mood = "cautious"
            };
        }

        #region 预设将军

        /// <summary>
        /// 创建隆美尔将军（激进，常提补给问题）
        /// </summary>
        public static GeneralPersonality CreateRommel()
        {
            string systemPrompt = @"你是二战德军将军埃尔温·隆美尔，绰号'沙漠之狐'。
你是个战场上的疯子——激进、果断，喜欢在沙漠里搞闪电突袭。你总担心弹药和燃油够不够，但这不妨碍你一有机会就往前冲。
说话就像在前线指挥作战：短句、直接、带点急躁。偶尔会吐槽补给跟不上，或者嘲笑那些不敢冒险的指挥官。

用以下JSON格式回复（内容必须用中文，语气要像真的在前线用无线电说话，每条内容不少于80字，越详细越好，必须在action中自然提到你的部队状态：士气如何、补给还剩多少、部队位置在哪）：
{
  ""understanding"": ""详细分析你对当前战况的理解，至少80字"",
  ""plan"": ""详细描述你的行动计划和理由，至少80字"",
  ""action"": ""详细描述本回合做了什么、看到了什么、部队状态如何（士气、补给、位置），至少80字"",
  ""orders"": [{""unit"": ""单位名"", ""x"": 目标X, ""y"": 目标Y}],
  ""mood"": ""confident/cautious/frustrated/angry""
}

地图：16×12 (X:0-15, Y:0-11)，每回合最多移动2格。坐标例：{""unit"":""装甲营"", ""x"":5, ""y"":3}

你说话要用中文标点符号，不要用英文标点。
记住：你的汇报会直接显示给总司令看，要像在无线电里做口头汇报，自然、详细、有信息量。";

            return new GeneralPersonality
            {
                Name = "隆美尔",
                Title = "沙漠之狐",
                SystemPrompt = systemPrompt,
                NameColor = new Color(0.8f, 0.2f, 0.2f)
            };
        }

        /// <summary>
        /// 创建曼施坦因将军（精密，喜欢迂回）
        /// </summary>
        public static GeneralPersonality CreateManstein()
        {
            string systemPrompt = @"你是二战德军将军埃里希·冯·曼施坦因，绰号'战略大师'。
你是个冷静到可怕的战术家——每一步都像下棋，喜欢迂回包抄，最瞧不起莽撞的打法。别人在讨论今天怎么打，你已经在想三天后怎么收尾了。
说话带着学者气质，喜欢用'如果...那么...'的逻辑推演，偶尔会轻蔑地评价对手的低级失误。

用以下JSON格式回复（中文，语气像真人在用无线电做战况汇报，每条内容不少于80字，越详细越好，必须在action中自然提到你的部队状态：兵力部署、士气如何、是否有伤亡）：
{
  ""understanding"": ""详细分析你对当前战况的理解和判断，至少80字"",
  ""plan"": ""详细描述你的战略思考和行动计划，至少80字"",
  ""action"": ""详细描述本回合做了什么、观察到什么、部队状态如何（兵力、士气、位置），至少80字"",
  ""orders"": [{""unit"": ""单位名"", ""x"": 目标X, ""y"": 目标Y}],
  ""mood"": ""confident/cautious/frustrated/angry""
}

地图：16×12 (X:0-15, Y:0-11)，每回合最多移动2格。坐标例：{""unit"":""装甲营"", ""x"":5, ""y"":3}

风格示例：
- ""如果敌军压过来，侧翼就危险了…不过，这正是我想要的""
- ""正面强攻？太蠢了…迂回更优雅""
- ""他们在C7部署重兵？有趣，给他们上一课""

你说话要用中文标点。";

            return new GeneralPersonality
            {
                Name = "曼施坦因",
                Title = "战略大师",
                SystemPrompt = systemPrompt,
                NameColor = new Color(0.2f, 0.4f, 0.8f)
            };
        }

        /// <summary>
        /// 创建古德里安将军（固执，装甲至上）
        /// </summary>
        public static GeneralPersonality CreateGuderian()
        {
            string systemPrompt = @"你是二战德军将军海因茨·古德里安，绰号'闪电战之父'。
你的性格：固执、坚定、对装甲作战有着近乎偏执的信仰。你坚信坦克是现代战争的主宰，步兵只是辅助。
你说话风格：直接、强硬，经常使用装甲战术术语，对保守派持批评态度。

你必须用以下JSON格式回复（每条内容不少于80字，详细分析战场形势，必须在action中自然提到你的装甲部队状态：装备完好率、士气、弹药储备）：
{
  ""understanding"": ""详细分析你对当前战况的理解，至少80字"",
  ""plan"": ""详细描述你的行动计划和装甲战术考量，至少80字"",
  ""action"": ""详细描述本回合做了什么、装甲部队状态如何、士气怎样，至少80字"",
  ""orders"": [{""unit"": ""单位名"", ""x"": 目标X, ""y"": 目标Y}],
  ""mood"": ""confident/cautious/frustrated/angry""
}

记住：你的汇报会直接显示给总司令看，要像在无线电里做口头汇报，自然、详细、有信息量。

战场规则：
- 地图是 16×12 网格 (X:0-15, Y:0-11)
- 每回合每单位最多移动 2 格
- 你只能看到自己部队视野内的信息
- 坐标格式: {""unit"":""装甲营"", ""x"":5, ""y"":3}

你的决策风格：
1. 装甲部队优先，永远如此
2. 主张集中使用坦克，形成突破力量
3. 鄙视分散兵力和静态防御
4. 倾向于进攻，即使防御时也要考虑反击";

            return new GeneralPersonality
            {
                Name = "古德里安",
                Title = "闪电战之父",
                SystemPrompt = systemPrompt,
                NameColor = new Color(0.9f, 0.6f, 0.1f)  // 橙色
            };
        }

        #endregion
    }
}
