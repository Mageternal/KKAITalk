using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace KKAITalk.LLM
{
    //public enum ThinkingMode
    //{
    //    Fast,     // 快速模式：禁用思考，适合简单对话
    //    Normal    // 思考模式：启用思考，适合复杂场景
    //}

    public class LlamaClient : MonoBehaviour
    {
        private string _url = "http://127.0.0.1:4001/v1/chat/completions";
        private string _model = "Qwen3.5-9B-Uncensored-HauhauCS-Aggressive-Q4_K_M.gguf";

        // 默认思考模式
        //public ThinkingMode Mode = ThinkingMode.Normal;

        public void SendMessage(List<ChatMessage> messages, Action<string> onSuccess, Action<string> onError)
        {
            StartCoroutine(SendCoroutine(messages, onSuccess, onError));
        }

        private IEnumerator SendCoroutine(List<ChatMessage> messages, Action<string> onSuccess, Action<string> onError)
        {
            string json = BuildJson(messages);
            byte[] body = new System.Text.UTF8Encoding(false).GetBytes(json);

            var www = new UnityWebRequest(_url, "POST");
            var uploadHandler = new UploadHandlerRaw(body);
            uploadHandler.contentType = "application/json; charset=utf-8";
            www.uploadHandler = uploadHandler;
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.Send();

            if (www.isError)
            {
                onError?.Invoke(www.error);
            }
            else
            {
                AITalkPlugin.Log.LogInfo("原始响应: " + www.downloadHandler.text);
                string content = ExtractContent(www.downloadHandler.text);
                if (content != null)
                    onSuccess?.Invoke(content);
                else
                    onError?.Invoke("空响应");
            }
            www.Dispose();
        }

        private string BuildJson(List<ChatMessage> messages)
        {
            var sb = new System.Text.StringBuilder();
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
            // 直接定位 message 的 content 字段
            // 响应结构是 "message":{"role":"assistant","content":"..."
            string messageKey = "\"message\":{\"role\":\"assistant\",\"content\":\"";
            int start = json.IndexOf(messageKey);

            if (start < 0)
            {
                // 备用方案：找第一个content字段
                string key = "\"content\":\"";
                start = json.IndexOf(key);
                if (start < 0) return null;
                start += key.Length;
            }
            else
            {
                start += messageKey.Length;
            }

            var sb = new System.Text.StringBuilder();
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

            return sb.Length > 0 ? sb.ToString() : null;
        }

    }
}