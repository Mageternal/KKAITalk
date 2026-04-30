using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace KKAITalk.Audio
{
    /// <summary>
    /// 音频配置管理器
    /// 配置文件路径: BepInEx\plugins\KKAITalk\audio_config.ini
    /// </summary>
    public class AudioConfigManager
    {
        private static readonly string BasePath = Paths.PluginPath;
        private static readonly string ConfigPath = Path.Combine(BasePath, "audio_config.ini");
        private static readonly string AudioPath = Path.Combine(BasePath, "audio");
        private static readonly string RespitePath = Path.Combine(AudioPath, "respite");
        private static readonly string TimbrePath = Path.Combine(AudioPath, "timbre");

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
            public List<string> AudioFiles { get; set; } = new List<string>();
        }

        private Dictionary<string, TimbreConfig> _timbreConfigs = new Dictionary<string, TimbreConfig>();
        private Dictionary<string, RespiteConfig> _respiteConfigs = new Dictionary<string, RespiteConfig>();
        private string _currentCharacter = "";

        /// <summary>
        /// 初始化：创建目录、加载配置
        /// </summary>
        public void Initialize()
        {
            // 创建目录
            CreateDirectories();

            // 加载配置
            LoadConfig();

            AITalkPlugin.Log.LogInfo($"[Audio] 音频配置初始化完成");
            AITalkPlugin.Log.LogInfo($"[Audio] 配置路径: {ConfigPath}");
            AITalkPlugin.Log.LogInfo($"[Audio] 音频路径: {AudioPath}");
        }

        /// <summary>
        /// 创建必要的目录
        /// </summary>
        private void CreateDirectories()
        {
            try
            {
                if (!Directory.Exists(AudioPath))
                    Directory.CreateDirectory(AudioPath);

                if (!Directory.Exists(RespitePath))
                    Directory.CreateDirectory(RespitePath);

                if (!Directory.Exists(TimbrePath))
                    Directory.CreateDirectory(TimbrePath);

                AITalkPlugin.Log.LogInfo($"[Audio] 目录创建完成: {AudioPath}");
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[Audio] 创建目录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                CreateDefaultConfig();
                return;
            }

            try
            {
                _timbreConfigs.Clear();
                _respiteConfigs.Clear();

                var lines = File.ReadAllLines(ConfigPath);
                string currentSection = "";
                TimbreConfig currentTimbre = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // 节头
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
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

                AITalkPlugin.Log.LogInfo($"[Audio] 配置文件加载完成");
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[Audio] 加载配置失败: {ex.Message}");
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
                using (var writer = new StreamWriter(ConfigPath))
                {
                    writer.WriteLine("; KKAITalk 音频配置文件");
                    writer.WriteLine("; 路径: BepInEx\\plugins\\KKAITalk\\audio_config.ini");
                    writer.WriteLine();
                    writer.WriteLine("; ===== 角色音色配置 =====");
                    writer.WriteLine("; [timbre_角色名]");
                    writer.WriteLine("; ref_audio: 参考音频文件路径（相对于 audio\\timbre 文件夹）");
                    writer.WriteLine("; ref_text: 参考音频对应的文本");
                    writer.WriteLine("; language: 语言（chinese/english/japanese等）");
                    writer.WriteLine("; temperature: 采样温度（0.0-1.0）");
                    writer.WriteLine("; sub_temperature: 子采样温度（0.0-1.0）");
                    writer.WriteLine("; instruct: 情感指令（如：温柔撒娇、冷静等）");
                    writer.WriteLine();
                    writer.WriteLine("; ===== 角色喘息配置 =====");
                    writer.WriteLine("; [respite_角色名]");
                    writer.WriteLine("; audio_files: 喘息音频文件列表（相对于 audio\\respite 文件夹，用 | 分隔）");
                    writer.WriteLine();
                    writer.WriteLine("; ===== 示例 =====");
                    writer.WriteLine(";[timbre_test]");
                    writer.WriteLine(";ref_audio=test.wav");
                    writer.WriteLine(";ref_text=这是一段测试文本");
                    writer.WriteLine(";language=chinese");
                    writer.WriteLine(";temperature=0.6");
                    writer.WriteLine(";sub_temperature=0.6");
                    writer.WriteLine(";instruct=温柔地说");
                    writer.WriteLine();
                    writer.WriteLine(";[respite_test]");
                    writer.WriteLine(";audio_files=respite1.wav|respite2.wav");
                }

                AITalkPlugin.Log.LogInfo($"[Audio] 默认配置文件已创建: {ConfigPath}");
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[Audio] 创建配置文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置当前角色（锁定角色）
        /// </summary>
        public void SetCurrentCharacter(string characterName)
        {
            _currentCharacter = characterName;
            AITalkPlugin.Log.LogInfo($"[Audio] 当前角色锁定: {characterName}");
        }

        /// <summary>
        /// 获取当前角色名
        /// </summary>
        public string GetCurrentCharacter()
        {
            return _currentCharacter;
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
            if (!Directory.Exists(TimbrePath))
                return null;

            var files = Directory.GetFiles(TimbrePath, "*.wav");
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
                    var fullPath = Path.Combine(RespitePath, file);
                    if (File.Exists(fullPath))
                        fullPaths.Add(fullPath);
                }
                return fullPaths;
            }

            // 尝试通过角色名查找喘息文件夹
            var respiteDir = Path.Combine(RespitePath, _currentCharacter);
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
            return Path.Combine(TimbrePath, relativePath);
        }

        /// <summary>
        /// 获取喘息文件夹路径
        /// </summary>
        public string GetRespitePath()
        {
            return RespitePath;
        }

        /// <summary>
        /// 获取音色文件夹路径
        /// </summary>
        public string GetTimbrePath()
        {
            return TimbrePath;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void SaveConfig()
        {
            try
            {
                using (var writer = new StreamWriter(ConfigPath))
                {
                    writer.WriteLine("; KKAITalk 音频配置文件");
                    writer.WriteLine();

                    // 写入音色配置
                    writer.WriteLine("; ===== 角色音色配置 =====");
                    foreach (var kvp in _timbreConfigs)
                    {
                        writer.WriteLine($"[timbre_{kvp.Key}]");
                        writer.WriteLine($"ref_audio={kvp.Value.RefAudioPath}");
                        writer.WriteLine($"ref_text={kvp.Value.RefText}");
                        writer.WriteLine($"language={kvp.Value.Language}");
                        writer.WriteLine($"temperature={kvp.Value.Temperature}");
                        writer.WriteLine($"sub_temperature={kvp.Value.SubTemperature}");
                        writer.WriteLine($"instruct={kvp.Value.Instruct}");
                        writer.WriteLine();
                    }

                    // 写入喘息配置
                    writer.WriteLine("; ===== 角色喘息配置 =====");
                    foreach (var kvp in _respiteConfigs)
                    {
                        writer.WriteLine($"[respite_{kvp.Key}]");
                        writer.WriteLine($"audio_files={string.Join("|", kvp.Value.AudioFiles)}");
                        writer.WriteLine();
                    }
                }

                AITalkPlugin.Log.LogInfo($"[Audio] 配置已保存: {ConfigPath}");
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[Audio] 保存配置失败: {ex.Message}");
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
