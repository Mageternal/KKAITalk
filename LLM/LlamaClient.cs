using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace KKAITalk.LLM
{
    public class LlamaClient : MonoBehaviour
    {
        private string _url = "http://127.0.0.1:4000/v1/chat/completions";
        private string _model = "Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf";

        // 挂起状态
        private class PendingRequest
        {
            public List<ChatMessage> Messages;
            public Action<string> OnSuccess;
            public Action<string> OnError;
        }
        private PendingRequest _pending;
        private bool _wwwBusy = false;
        private UnityWebRequest _currentWww;

        public void SendMessage(List<ChatMessage> messages, Action<string> onSuccess, Action<string> onError)
        {
            if (_wwwBusy)
            {
                AITalkPlugin.Log.LogWarning("LLM: 上一个请求尚未完成，跳过");
                onError?.Invoke("busy");
                return;
            }
            _pending = new PendingRequest { Messages = messages, OnSuccess = onSuccess, OnError = onError };
            _wwwBusy = true;

            string json = BuildJson(messages);
            byte[] body = new UTF8Encoding(false).GetBytes(json);

            _currentWww = new UnityWebRequest(_url, "POST");
            var uploadHandler = new UploadHandlerRaw(body);
            uploadHandler.contentType = "application/json; charset=utf-8";
            _currentWww.uploadHandler = uploadHandler;
            _currentWww.downloadHandler = new DownloadHandlerBuffer();

            StartCoroutine(WaitForRequest());
        }

        private IEnumerator WaitForRequest()
        {
            yield return _currentWww.Send();
            _wwwBusy = false;

            AITalkPlugin.Log.LogInfo("SendCoroutine.www请求完成");

            if (_currentWww.isError)
            {
                AITalkPlugin.Log.LogInfo("SendCoroutine.www，开始返回错误内容");
                if (_pending != null)
                    _pending.OnError(_currentWww.error);
            }
            else
            {
                string text = _currentWww.downloadHandler.text;
                AITalkPlugin.Log.LogInfo("原始响应(前200字): " + text.Substring(0, Mathf.Min(200, text.Length)));
                string content = ExtractContent(text);
                AITalkPlugin.Log.LogInfo("ExtractContent结果: " + (content ?? "null"));
                if (content != null)
                {
                    AITalkPlugin.Log.LogInfo("content不为null");
                    if (_pending != null)
                        _pending.OnSuccess(content);
                }
                else
                {
                    if (_pending != null)
                        _pending.OnError("空响应");
                }
            }

            AITalkPlugin.Log.LogInfo("开始调用www.Dispose()");
            _currentWww.Dispose();
            _currentWww = null;
            _pending = null;
            AITalkPlugin.Log.LogInfo("SendCoroutine函数结束");
        }

        private string BuildJson(List<ChatMessage> messages)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.AppendFormat("\"model\":\"{0}\",", _model);
            sb.Append("\"stream\":false,");
            sb.Append("\"messages\":[");

            for (int i = 0; i < messages.Count; i++)
            {
                sb.Append("{");
                sb.AppendFormat("\"role\":\"{0}\",", messages[i].role);
                sb.AppendFormat("\"content\":\"{0}\"", EscapeJson(messages[i].content));
                sb.Append("}");
                if (i < messages.Count - 1) sb.Append(",");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r");
        }

        private string ExtractContent(string json)
        {
            string messageKey = "\"message\":{\"role\":\"assistant\",\"content\":\"";
            int start = json.IndexOf(messageKey);

            if (start < 0)
            {
                string key = "\"content\":\"";
                start = json.IndexOf(key);
                if (start < 0) return null;
                start += key.Length;
            }
            else
            {
                start += messageKey.Length;
            }

            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    if (next == '"') sb.Append('"');
                    else if (next == 'n') sb.Append('\n');
                    else if (next == '\\') sb.Append('\\');
                    i++;
                }
                else if (json[i] == '"') break;
                else sb.Append(json[i]);
            }

            string raw = sb.Length > 0 ? sb.ToString() : null;
            if (raw == null) return null;

            raw = StripThinkingTags(raw);
            return raw;
        }

        private string StripThinkingTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            text = text.Replace("\\n", "\n");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<\|im_(start|end)\|>", "");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<think>[\s\S]*?", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<thought>[\s\S]*?</thought>", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"<think>", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
            text = text.Trim();

            return text;
        }
    }
}