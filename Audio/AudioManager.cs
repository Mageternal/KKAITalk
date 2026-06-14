using System;
using UnityEngine;

namespace KKAITalk.Audio
{
    /// <summary>
    /// 音频管理器 - 统一管理所有音频相关功能
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private AudioConfigManager _configManager;
        private TTSClient _ttsClient;
        private AudioPlayer _audioPlayer;

        private bool _isInitialized = false;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }
            // 全部用 AddComponent 创建，避免"游离 MonoBehaviour"在 Mono 2.0 下状态不可靠
            _configManager = gameObject.AddComponent<AudioConfigManager>();
            _ttsClient = gameObject.AddComponent<TTSClient>();
            _audioPlayer = gameObject.AddComponent<AudioPlayer>();
            
            // 初始化各组件
            _configManager.Initialize();
            _ttsClient.Initialize(_configManager);
            _audioPlayer.Initialize(_configManager, _ttsClient);
            
            _isInitialized = true;
            AITalkPlugin.Log.LogInfo("[Audio] 音频管理器初始化完成");
        }

        /// <summary>
        /// 设置当前角色（锁定角色进行TTS）
        /// </summary>
        public void SetCurrentCharacter(string characterName)
        {
            _configManager.SetCurrentCharacter(characterName);
            _audioPlayer.RefreshRespiteFiles();
        }

        /// <summary>
        /// 预热当前角色的音色（Talk 场景加载时调用）
        /// 未配置音色的角色直接跳过，日志提示，不发起 TTS 请求
        /// </summary>
        public void PreloadCurrentCharacterVoice()
        {
            string charaName = _configManager.GetCurrentCharacter();
            if (string.IsNullOrEmpty(charaName))
            {
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogWarning("[Audio] 预热跳过: 当前角色未设置");
                return;
            }

            // 未配置音色（没 [timbre_xxx] 或 ref_audio/ref_text 缺失）则跳过
            if (!_configManager.HasValidTTSConfig())
            {
                if (AITalkPlugin.Log != null)
                    AITalkPlugin.Log.LogInfo("[Audio] 未找到 \"" + charaName + "\" 的音色样本，跳过 TTS 预热（发言时也不发起 TTS 请求）");
                return;
            }

            if (_configManager.IsPreloading)
            {
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 预热已在进行中，跳过");
                return;
            }

            _configManager.BeginPreload();
            if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[Audio] 开始预热音色: " + charaName);

            _ttsClient.EncodeVoice(
                cacheKey => _configManager.SetCacheKey(cacheKey),
                err =>
                {
                    if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogError("[Audio] 预热失败: " + err);
                    _configManager.FailPreload();
                }
            );
        }

        /// <summary>
        /// 获取配置管理器
        /// </summary>
        public AudioConfigManager ConfigManager
        {
            get { return _configManager; }
        }

        /// <summary>
        /// 获取TTS客户端
        /// </summary>
        public TTSClient TTS
        {
            get { return _ttsClient; }
        }

        /// <summary>
        /// 获取音频播放器
        /// </summary>
        public AudioPlayer Player
        {
            get { return _audioPlayer; }
        }

        /// <summary>
        /// 播放发言（带 TTS 合成）
        /// </summary>
        public void Speak(string text, Action onComplete = null)
        {
            AITalkPlugin.Log.LogInfo("[Audio] Speak 被调用, text 长度=" + (text != null ? text.Length : 0) + ", chara=" + _configManager.GetCurrentCharacter());
            if (!_configManager.HasValidTTSConfig())
            {
                AITalkPlugin.Log.LogWarning("[Audio] TTS配置不完整，跳过发言");
                if (onComplete != null) onComplete.Invoke();
                return;
            }

            _audioPlayer.PlayVoice(text, onComplete);
        }

        /// <summary>
        /// 播放喘息
        /// </summary>
        public void PlayRespite()
        {
            _audioPlayer.PlayRespite();
        }

        /// <summary>
        /// 停止所有音频
        /// </summary>
        public void StopAll()
        {
            _audioPlayer.StopAll();
        }

        /// <summary>
        /// 检查服务器状态
        /// </summary>
        public void CheckServerStatus(Action<bool> onResult)
        {
            _ttsClient.CheckServer(onResult);
        }

        /// <summary>
        /// 设置 TTS 服务器地址
        /// </summary>
        public void SetServer(string host, int port)
        {
            _ttsClient.SetServer(host, port);
        }

        /// <summary>
        /// 设置发言音量
        /// </summary>
        public void SetVoiceVolume(float volume)
        {
            _audioPlayer.SetVoiceVolume(volume);
        }

        /// <summary>
        /// 设置喘息音量
        /// </summary>
        public void SetRespiteVolume(float volume)
        {
            _audioPlayer.SetRespiteVolume(volume);
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying
        {
            get { return _audioPlayer.IsPlaying; }
        }

        /// <summary>
        /// 是否正在播放发言
        /// </summary>
        public bool IsSpeaking
        {
            get { return _audioPlayer.IsPlayingVoice; }
        }
    }
}
