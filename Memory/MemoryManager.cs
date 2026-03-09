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

                var data = new MemoryData { charaId = charaId, history = trimmed };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(path, json, System.Text.Encoding.UTF8);
                AITalkPlugin.Log.LogInfo("记忆已保存: " + charaId + " (" + trimmed.Count + "条)");
            }
            catch (Exception e)
            {
                AITalkPlugin.Log.LogError("保存记忆失败: " + e.Message);
            }
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
                var data = JsonUtility.FromJson<MemoryData>(json);
                return data?.history ?? new List<ChatMessage>();
            }
            catch (Exception e)
            {
                AITalkPlugin.Log.LogWarning("读取记忆失败: " + e.Message);
                return new List<ChatMessage>();
            }
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