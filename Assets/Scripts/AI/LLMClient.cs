using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace SWO1.AI
{
    /// <summary>
    /// LLM客户端 - 封装小米API的HTTP调用
    /// 使用UnityWebRequest发送请求，不使用第三方JSON库
    /// </summary>
    public class LLMClient : MonoBehaviour
    {
        [Header("API Configuration")]
        [Tooltip("API地址")]
        public string ApiUrl = "https://token-plan-cn.xiaomimimo.com/v1/chat/completions";
        
        [Tooltip("API密钥")]
        public string ApiKey = "tp-cu3xxezvib0sx9dxg66i4gocxx335q2ttfewj7oafw4k6nsb";
        
        [Tooltip("模型名称")]
        public string ModelName = "mimo-v2-pro";
        
        [Tooltip("Temperature参数")]
        [Range(0f, 2f)]
        public float Temperature = 0.7f;
        
        [Tooltip("最大生成token数")]
        public int MaxTokens = 4000;
        
        [Tooltip("超时时间(秒)")]
        public int TimeoutSeconds = 60;

        /// <summary>
        /// 异步调用LLM，回调返回结果字符串
        /// </summary>
        /// <param name="systemPrompt">系统提示词</param>
        /// <param name="userMessage">用户消息</param>
        /// <param name="onComplete">成功回调</param>
        /// <param name="onError">错误回调</param>
        public void Chat(string systemPrompt, string userMessage, Action<string> onComplete, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                onError?.Invoke("API Key未设置");
                return;
            }

            StartCoroutine(ChatCoroutine(systemPrompt, userMessage, onComplete, onError));
        }

        /// <summary>
        /// 协程处理HTTP请求
        /// </summary>
        private IEnumerator ChatCoroutine(string systemPrompt, string userMessage, Action<string> onComplete, Action<string> onError)
        {
            // 手动构造JSON请求体（不依赖第三方JSON库）
            string jsonBody = BuildRequestJson(systemPrompt, userMessage);
            
            using (UnityWebRequest request = new UnityWebRequest(ApiUrl, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                
                // 设置请求头
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {ApiKey}");
                
                // 设置超时
                request.timeout = TimeoutSeconds;
                
                // 发送请求
                yield return request.SendWebRequest();
                
                // 处理响应
                if (request.result == UnityWebRequest.Result.ConnectionError || 
                    request.result == UnityWebRequest.Result.ProtocolError)
                {
                    string errorMsg = $"请求失败: {request.error} (HTTP {request.responseCode})";
                    Debug.LogError($"[LLMClient] {errorMsg}");
                    onError?.Invoke(errorMsg);
                }
                else
                {
                    string responseText = request.downloadHandler.text;
                    string content = ExtractContentFromResponse(responseText);
                    
                    if (!string.IsNullOrEmpty(content))
                    {
                        Debug.Log($"[LLMClient] 成功获取响应，长度: {content.Length}");
                        onComplete?.Invoke(content);
                    }
                    else
                    {
                        string errorMsg = "无法从响应中提取内容";
                        Debug.LogError($"[LLMClient] {errorMsg}\n原始响应: {responseText}");
                        onError?.Invoke(errorMsg);
                    }
                }
            }
        }

        /// <summary>
        /// 手动构造JSON请求体
        /// </summary>
        private string BuildRequestJson(string systemPrompt, string userMessage)
        {
            // 转义特殊字符
            string escapedSystemPrompt = EscapeJsonString(systemPrompt);
            string escapedUserMessage = EscapeJsonString(userMessage);
            
            return $"{{" +
                $"\"model\":\"{ModelName}\"," +
                $"\"messages\":[" +
                $"{{\"role\":\"system\",\"content\":\"{escapedSystemPrompt}\"}}," +
                $"{{\"role\":\"user\",\"content\":\"{escapedUserMessage}\"}}" +
                $"]," +
                $"\"temperature\":{Temperature}," +
                $"\"max_tokens\":{MaxTokens}" +
                $"}}";
        }

        /// <summary>
        /// 转义JSON字符串中的特殊字符
        /// </summary>
        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";
            
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\b", "\\b")
                .Replace("\f", "\\f");
        }

        /// <summary>
        /// 从响应JSON中提取content字段
        /// 使用正则表达式解析，不依赖第三方JSON库
        /// </summary>
        /// <summary>
        /// 从响应JSON中提取content字段
        /// 优先取 content，为空时兜底 reasoning_content
        /// </summary>
        private string ExtractContentFromResponse(string responseJson)
        {
            if (string.IsNullOrEmpty(responseJson))
                return null;

            try
            {
                // 优先匹配 content 字段（非空值）
                string pattern = "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"";
                Match match = Regex.Match(responseJson, pattern);
                if (match.Success && !string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    return UnescapeJsonString(match.Groups[1].Value);
                }

                // 兜底：reasoning_content（思考模型输出在此）
                int rcIdx = responseJson.IndexOf("reasoning_content");
                if (rcIdx >= 0)
                {
                    int colonIdx = responseJson.IndexOf(':', rcIdx);
                    if (colonIdx >= 0)
                    {
                        int startQuote = responseJson.IndexOf('"', colonIdx + 1);
                        if (startQuote >= 0)
                        {
                            int endQuote = -1;
                            for (int i = startQuote + 1; i < responseJson.Length; i++)
                            {
                                char c = responseJson[i];
                                if (c == '\\' && i + 1 < responseJson.Length) { i++; continue; }
                                if (c == '"') { endQuote = i; break; }
                            }
                            if (endQuote > startQuote)
                            {
                                string val = responseJson.Substring(startQuote + 1, endQuote - startQuote - 1);
                                val = UnescapeJsonString(val);
                                if (!string.IsNullOrEmpty(val)) return val;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LLMClient] 解析响应失败: {e.Message}");
                return null;
            }
        }


        /// <summary>
        /// 反转义JSON字符串
        /// </summary>
        private string UnescapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            // 先处理 Unicode 转义序列 \uXXXX
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < input.Length)
            {
                if (i + 5 < input.Length && input[i] == '\\' && input[i + 1] == 'u')
                {
                    string hex = input.Substring(i + 2, 4);
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int codePoint))
                    {
                        sb.Append(char.ConvertFromUtf32(codePoint));
                        i += 6;
                        continue;
                    }
                }
                sb.Append(input[i]);
                i++;
            }
            input = sb.ToString();

            // 再处理其他转义（顺序很重要：先 \\ 再其他，避免 \\n 被错误替换为 \n）
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
        /// 测试连接
        /// </summary>
        public void TestConnection(Action<string> onComplete, Action<string> onError = null)
        {
            string testSystemPrompt = "你是一个 helpful 助手。";
            string testUserMessage = "请回复 'Connection OK' 确认连接正常。";
            Chat(testSystemPrompt, testUserMessage, onComplete, onError);
        }
    }
}
