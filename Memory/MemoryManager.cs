using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using UnityEngine;
using KKAITalk.LLM;

namespace KKAITalk.Memory
{
    public static class MemoryManager
    {
        // 最多保留的对话轮数
        private const int MaxHistory = 20;

        // 存储根目录
        private static string RootDir
        {
            get
            {
                return BepInEx.Paths.BepInExRootPath + "/plugins/KKAITalk/saves";
            }
        }

        // 获取当前存档ID
        public static string GetSaveId()
        {
            var saveData = Manager.Game.Instance?.saveData;
            string accName = saveData?.accademyName ?? "default";
            string savePath = SaveData.Path ?? "";
            string saveFileName = System.IO.Path.GetFileNameWithoutExtension(savePath);
            return string.IsNullOrEmpty(saveFileName)
                ? accName
                : accName + "_" + saveFileName;
        }


        // 读取角色历史记录
        public static void SaveHistory(string saveId, string charaId, List<ChatMessage> history)
        {
            try
            {
                string safeName = charaId.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
                string dir = RootDir + "/" + saveId;
                string path = dir + "/" + safeName + ".json";

                Directory.CreateDirectory(dir);

                var trimmed = history.Count > MaxHistory
                    ? history.GetRange(history.Count - MaxHistory, MaxHistory)
                    : history;

                // 手动构建JSON，绕过JsonUtility对嵌套List的限制
                var sb = new System.Text.StringBuilder();
                sb.Append("{\n");
                sb.AppendFormat("  \"charaId\": \"{0}\",\n", EscapeJson(charaId));
                sb.Append("  \"history\": [\n");
                for (int i = 0; i < trimmed.Count; i++)
                {
                    sb.Append("    {");
                    sb.AppendFormat("\"role\": \"{0}\", ", EscapeJson(trimmed[i].role));
                    sb.AppendFormat("\"content\": \"{0}\"", EscapeJson(trimmed[i].content));
                    sb.Append(i < trimmed.Count - 1 ? "},\n" : "}\n");
                }
                sb.Append("  ]\n}");

                string json = sb.ToString();
                AITalkPlugin.Log.LogInfo($"保存路径: {path}, 条数: {trimmed.Count}, 内容: {json}");
                File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                AITalkPlugin.Log.LogInfo("记忆已保存: " + charaId + " (" + trimmed.Count + "条)");
            }
            catch (Exception e)
            {
                AITalkPlugin.Log.LogError("保存记忆失败: " + e.Message);
            }
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r");
        }

        public static List<ChatMessage> LoadHistory(string saveId, string charaId)
        {
            string safeName = charaId.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
            string path = RootDir + "/" + saveId + "/" + safeName + ".json";

            if (!File.Exists(path))
                return new List<ChatMessage>();

            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                return ParseHistory(json);
            }
            catch (Exception e)
            {
                AITalkPlugin.Log.LogWarning("读取记忆失败: " + e.Message);
                return new List<ChatMessage>();
            }
        }

        private static List<ChatMessage> ParseHistory(string json)
        {
            var result = new List<ChatMessage>();
            // 找到 history 数组
            int arrStart = json.IndexOf("\"history\"");
            if (arrStart < 0) return result;
            arrStart = json.IndexOf('[', arrStart);
            if (arrStart < 0) return result;
            int arrEnd = json.LastIndexOf(']');
            if (arrEnd < 0) return result;

            string arrContent = json.Substring(arrStart + 1, arrEnd - arrStart - 1);

            // 逐个解析 {role:..., content:...} 对象
            int pos = 0;
            while (pos < arrContent.Length)
            {
                int objStart = arrContent.IndexOf('{', pos);
                if (objStart < 0) break;
                int objEnd = arrContent.IndexOf('}', objStart);
                if (objEnd < 0) break;

                string obj = arrContent.Substring(objStart + 1, objEnd - objStart - 1);
                string role = ExtractJsonString(obj, "role");
                string content = ExtractJsonString(obj, "content");

                if (role != null && content != null)
                    result.Add(new ChatMessage { role = role, content = content });

                pos = objEnd + 1;
            }
            return result;
        }

        private static string ExtractJsonString(string obj, string key)
        {
            string search = "\"" + key + "\"";
            int keyIdx = obj.IndexOf(search);
            if (keyIdx < 0) return null;
            int colon = obj.IndexOf(':', keyIdx + search.Length);
            if (colon < 0) return null;
            int quoteStart = obj.IndexOf('"', colon + 1);
            if (quoteStart < 0) return null;

            var sb = new System.Text.StringBuilder();
            for (int i = quoteStart + 1; i < obj.Length; i++)
            {
                if (obj[i] == '\\' && i + 1 < obj.Length)
                {
                    char next = obj[i + 1];
                    if (next == '"') sb.Append('"');
                    else if (next == 'n') sb.Append('\n');
                    else if (next == '\\') sb.Append('\\');
                    i++;
                }
                else if (obj[i] == '"') break;
                else sb.Append(obj[i]);
            }
            return sb.ToString();
        }
    }

    // JSON数据结构
    [Serializable]
    public class MemoryData
    {
        public string charaId;
        public List<ChatMessage> history = new List<ChatMessage>(); // 加初始化
    }
}