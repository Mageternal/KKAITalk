using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KKAITalk.Audio
{
    /// <summary>
    /// 音频配置管理器
    /// 配置文件路径: BepInEx\plugins\KKAITalk\audio_config.ini
    /// </summary>
    public class AudioConfigManager : MonoBehaviour
    {
        private static readonly string BasePath;
        static AudioConfigManager()
        {
            // 获取 BepInEx\plugins\ 路径
            string pluginPath = BepInEx.Paths.PluginPath;
            if (string.IsNullOrEmpty(pluginPath))
                pluginPath = BepInEx.Paths.BepInExRootPath;

            // 拼接插件目录名：BepInEx\plugins\KKAITalk\
            BasePath = Path.Combine(pluginPath ?? "", "KKAITalk");
        }
        private static string ConfigPath() { return Path.Combine(BasePath ?? "", "audio_config.ini"); }
        private static string AudioPath() { return Path.Combine(BasePath ?? "", "audio"); }
        private static string RespitePath() { return Path.Combine(Path.Combine(BasePath ?? "", "audio"), "respite"); }
        private static string TimbrePath() { return Path.Combine(Path.Combine(BasePath ?? "", "audio"), "timbre"); }

        /// <summary>
        /// 角色音色配置
        /// </summary>
        public class TimbreConfig
        {
            public string CharacterName { get; set; }      // 角色名
            public string RefAudioPath { get; set; }       // 参考音频路径
            public string RefText { get; set; }            // 参考文本
            public string Language { get; set; }          // 语言
            public float Temperature { get; set; }          // 采样温度
            public float SubTemperature { get; set; }      // 子采样温度
            public string Instruct { get; set; }           // 情感指令

            public TimbreConfig()
            {
                Language = "chinese";
                Temperature = 0.6f;
                SubTemperature = 0.6f;
            }
        }

        /// <summary>
        /// 角色喘息配置
        /// </summary>
        public class RespiteConfig
        {
            public string CharacterName { get; set; }
            public List<string> AudioFiles { get; set; }
            public RespiteConfig() { AudioFiles = new List<string>(); }
        }

        private Dictionary<string, TimbreConfig> _timbreConfigs = new Dictionary<string, TimbreConfig>();
        private Dictionary<string, RespiteConfig> _respiteConfigs = new Dictionary<string, RespiteConfig>();
        private string _currentCharacter = "";
        // 服务端返回的音色缓存 key，每次 Talk 场景加载时刷新
        private string _currentCacheKey = "";
        private string _pendingCacheKey = ""; // EncodeVoice 期间缓存，等待 HTTP 完成
        private bool _isPreloading = false;

        /// <summary>
        /// 初始化：创建目录、加载配置
        /// </summary>
        public void Initialize()
        {
            try
            {
                CreateDirectories();
            }
            catch (Exception ex)
            {
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogError("[Audio] CreateDirectories失败: " + ex.Message);
            }

            try
            {
                LoadConfig();
            }
            catch (Exception ex)
            {
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogError("[Audio] LoadConfig失败: " + ex.Message);
            }

            if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 音频配置初始化完成");
            if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 配置路径: " + ConfigPath());
            if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 音频路径: " + AudioPath());
        }

        /// <summary>
        /// 创建必要的目录
        /// </summary>
        private void CreateDirectories()
        {
            try
            {
                string audioDir = AudioPath();
                if (!Directory.Exists(audioDir))
                    Directory.CreateDirectory(audioDir);

                if (!Directory.Exists(RespitePath()))
                    Directory.CreateDirectory(RespitePath());

                if (!Directory.Exists(TimbrePath()))
                    Directory.CreateDirectory(TimbrePath());

                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 目录创建完成: " + audioDir);
            }
            catch (Exception ex)
            {
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogError("[Audio] 创建目录失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private void LoadConfig()
        {
            if (!File.Exists(ConfigPath()))
            {
                CreateDefaultConfig();
                return;
            }

            try
            {
                _timbreConfigs.Clear();
                _respiteConfigs.Clear();

                var lines = File.ReadAllLines(ConfigPath());
                string currentSection = "";
                TimbreConfig currentTimbre = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();


                    // 节头
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        if (currentTimbre != null)
                            _timbreConfigs[currentTimbre.CharacterName] = currentTimbre;

                        currentSection = trimmed.Substring(1, trimmed.Length - 2);

                        if (currentSection.StartsWith("timbre_"))
                        {
                            currentTimbre = new TimbreConfig();
                            currentTimbre.CharacterName = currentSection.Substring(7);
                        }
                        else if (currentSection.StartsWith("respite_"))
                        {
                            var charName = currentSection.Substring(8);
                            if (!_respiteConfigs.ContainsKey(charName))
                                _respiteConfigs[charName] = new RespiteConfig { CharacterName = charName };
                        }
                        continue;
                    }
                    if (currentTimbre != null)
                        _timbreConfigs[currentTimbre.CharacterName] = currentTimbre;

                    if (currentTimbre != null && trimmed.Contains("="))
                    {
                        var parts = trimmed.Split(new[] { '=' }, 2);
                        var key = parts[0].Trim().ToLower();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "ref_audio":
                                currentTimbre.RefAudioPath = value;
                                break;
                            case "ref_text":
                                currentTimbre.RefText = value;
                                break;
                            case "language":
                                currentTimbre.Language = value;
                                break;
                            case "temperature":
                                float.TryParse(value, out float temp);
                                currentTimbre.Temperature = temp > 0 ? temp : 0.6f;
                                break;
                            case "sub_temperature":
                                float.TryParse(value, out float subTemp);
                                currentTimbre.SubTemperature = subTemp > 0 ? subTemp : 0.6f;
                                break;
                            case "instruct":
                                currentTimbre.Instruct = value;
                                break;
                        }
                    }
                }

                // 收集 timbre 配置
                foreach (var kvp in _timbreConfigs)
                {
                    // 如果配置文件有内容会覆盖默认
                }

                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 配置文件加载完成");
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError("[Audio] 加载配置失败: " + ex.Message);
                CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 创建默认配置文件
        /// </summary>
        private void CreateDefaultConfig()
        {
            try
            {
                var content = "; KKAITalk 音频配置文件\n" +
                    "; 路径: BepInEx\\plugins\\KKAITalk\\audio_config.ini\n\n" +
                    "; ===== 角色音色配置 =====\n" +
                    "; [timbre_角色名]\n" +
                    "; ref_audio: 参考音频文件路径（相对于 audio\\timbre 文件夹）\n" +
                    "; ref_text: 参考音频对应的文本\n" +
                    "; language: 语言（chinese/english/japanese等）\n" +
                    "; temperature: 采样温度（0.0-1.0）\n" +
                    "; sub_temperature: 子采样温度（0.0-1.0）\n" +
                    "; instruct: 情感指令（如：温柔撒娇、冷静等）\n\n" +
                    "; ===== 角色喘息配置 =====\n" +
                    "; [respite_角色名]\n" +
                    "; audio_files: 喘息音频文件列表（相对于 audio\\respite 文件夹，用 | 分隔）\n\n" +
                    "; ===== 示例 =====\n" +
                    ";[timbre_test]\n" +
                    ";ref_audio=test.wav\n" +
                    ";ref_text=这是一段测试文本\n" +
                    ";language=chinese\n" +
                    ";temperature=0.6\n" +
                    ";sub_temperature=0.6\n" +
                    ";instruct=温柔地说\n\n" +
                    ";[respite_test]\n" +
                    ";audio_files=respite1.wav|respite2.wav\n";

                File.WriteAllText(ConfigPath(), content);

                if (AITalkPlugin.Log != null)
                    AITalkPlugin.Log.LogInfo("[Audio] 默认配置文件已创建: " + ConfigPath());
            }
            catch (Exception ex)
            {
                if (AITalkPlugin.Log != null)
                    AITalkPlugin.Log.LogError("[Audio] 创建配置文件失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 设置当前角色（锁定角色）
        /// </summary>
        public void SetCurrentCharacter(string characterName)
        {
            // 切换角色时清空旧 cache_key，避免误用上一角色的音色
            if (_currentCharacter != characterName)
            {
                _currentCacheKey = "";
                _pendingCacheKey = "";
                _isPreloading = false;
            }
            _currentCharacter = characterName;
            if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 当前角色锁定: " + characterName);
        }

        /// <summary>
        /// 获取当前角色名
        /// </summary>
        public string GetCurrentCharacter()
        {
            return _currentCharacter;
        }

        /// <summary>
        /// 获取当前 cache_key（可能为空，表示未预热或预热失败）
        /// </summary>
        public string GetCacheKey()
        {
            return _currentCacheKey;
        }

        /// <summary>
        /// 由 TTSClient.EncodeVoice 完成后调用，存入 cache_key
        /// </summary>
        public void SetCacheKey(string cacheKey)
        {
            _currentCacheKey = cacheKey ?? "";
            _isPreloading = false;
        }

        /// <summary>
        /// EncodeVoice 启动时调用，标记预热中
        /// </summary>
        public void BeginPreload()
        {
            _isPreloading = true;
            _pendingCacheKey = "";
        }

        /// <summary>
        /// 预热失败时调用，清空状态
        /// </summary>
        public void FailPreload()
        {
            _isPreloading = false;
            _pendingCacheKey = "";
        }

        /// <summary>
        /// 是否正在预热
        /// </summary>
        public bool IsPreloading
        {
            get { return _isPreloading; }
        }

        /// <summary>
        /// 获取当前角色的音色配置
        /// </summary>
        public TimbreConfig GetCurrentTimbreConfig()
        {
            if (string.IsNullOrEmpty(_currentCharacter))
                return null;

            if (_timbreConfigs.TryGetValue(_currentCharacter, out var config))
                return config;

            // 尝试通过文件名匹配
            return FindTimbreConfigByFile();
        }

        /// <summary>
        /// 通过文件名查找音色配置
        /// </summary>
        private TimbreConfig FindTimbreConfigByFile()
        {
            if (!Directory.Exists(TimbrePath()))
                return null;

            var files = Directory.GetFiles(TimbrePath(), "*.wav");
            foreach (var file in files)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (_timbreConfigs.TryGetValue(fileName, out var config))
                    return config;
            }

            return null;
        }

        /// <summary>
        /// 获取当前角色的喘息音频文件列表
        /// </summary>
        public List<string> GetCurrentRespiteFiles()
        {
            if (string.IsNullOrEmpty(_currentCharacter))
                return new List<string>();

            if (_respiteConfigs.TryGetValue(_currentCharacter, out var config))
            {
                var fullPaths = new List<string>();
                foreach (var file in config.AudioFiles)
                {
                    var fullPath = Path.Combine(RespitePath(), file);
                    if (File.Exists(fullPath))
                        fullPaths.Add(fullPath);
                }
                return fullPaths;
            }

            // 尝试通过角色名查找喘息文件夹
            var respiteDir = Path.Combine(RespitePath(), _currentCharacter);
            if (Directory.Exists(respiteDir))
            {
                var files = Directory.GetFiles(respiteDir, "*.wav");
                return files.ToList();
            }

            return new List<string>();
        }

        /// <summary>
        /// 获取参考音频的完整路径
        /// </summary>
        public string GetRefAudioFullPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            // 如果是绝对路径，直接返回
            if (Path.IsPathRooted(relativePath))
                return File.Exists(relativePath) ? relativePath : null;

            // 相对路径
            return Path.Combine(TimbrePath(), relativePath);
        }

        /// <summary>
        /// 获取喘息文件夹路径
        /// </summary>
        public string GetRespitePath()
        {
            return RespitePath();
        }

        /// <summary>
        /// 获取音色文件夹路径
        /// </summary>
        public string GetTimbrePath()
        {
            return TimbrePath();
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                using (var writer = new StreamWriter(ConfigPath()))
                {
                    writer.WriteLine("; KKAITalk 音频配置文件");
                    writer.WriteLine();

                    // 写入音色配置
                    writer.WriteLine("; ===== 角色音色配置 =====");
                    foreach (var kvp in _timbreConfigs)
                    {
                        writer.WriteLine("[timbre_" + kvp.Key + "]");
                        writer.WriteLine("ref_audio=" + kvp.Value.RefAudioPath);
                        writer.WriteLine("ref_text=" + kvp.Value.RefText);
                        writer.WriteLine("language=" + kvp.Value.Language);
                        writer.WriteLine("temperature=" + kvp.Value.Temperature);
                        writer.WriteLine("sub_temperature=" + kvp.Value.SubTemperature);
                        writer.WriteLine("instruct=" + (kvp.Value.Instruct ?? ""));
                        writer.WriteLine();
                    }

                    // 写入喘息配置
                    writer.WriteLine("; ===== 角色喘息配置 =====");
                    foreach (var kvp in _respiteConfigs)
                    {
                        writer.WriteLine("[respite_" + kvp.Key + "]");
                        writer.WriteLine("audio_files=" + string.Join("|", kvp.Value.AudioFiles.ToArray()));
                        writer.WriteLine();
                    }
                }

                AITalkPlugin.Log.LogInfo("[Audio] 配置已保存: " + ConfigPath());
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError("[Audio] 保存配置失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 添加或更新音色配置
        /// </summary>
        public void SetTimbreConfig(string characterName, TimbreConfig config)
        {
            _timbreConfigs[characterName] = config;
            SaveConfig();
        }

        /// <summary>
        /// 添加或更新喘息配置
        /// </summary>
        public void SetRespiteConfig(string characterName, List<string> audioFiles)
        {
            _respiteConfigs[characterName] = new RespiteConfig
            {
                CharacterName = characterName,
                AudioFiles = audioFiles
            };
            SaveConfig();
        }

        /// <summary>
        /// 检查当前角色是否有完整的TTS配置
        /// </summary>
        public bool HasValidTTSConfig()
        {
            var config = GetCurrentTimbreConfig();
            if (config == null)
                return false;

            var refPath = GetRefAudioFullPath(config.RefAudioPath);
            return !string.IsNullOrEmpty(refPath) &&
                   !string.IsNullOrEmpty(config.RefText);
        }
    }
}
