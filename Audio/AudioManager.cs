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
                return;

            // 创建子对象
            var configObj = new GameObject("AudioConfigManager");
            configObj.transform.SetParent(transform);
            _configManager = configObj.AddComponent<AudioConfigManager>();

            var ttsObj = new GameObject("TTSClient");
            ttsObj.transform.SetParent(transform);
            _ttsClient = ttsObj.AddComponent<TTSClient>();

            var playerObj = new GameObject("AudioPlayer");
            playerObj.transform.SetParent(transform);
            _audioPlayer = playerObj.AddComponent<AudioPlayer>();

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
        /// 获取配置管理器
        /// </summary>
        public AudioConfigManager ConfigManager => _configManager;

        /// <summary>
        /// 获取TTS客户端
        /// </summary>
        public TTSClient TTS => _ttsClient;

        /// <summary>
        /// 获取音频播放器
        /// </summary>
        public AudioPlayer Player => _audioPlayer;

        /// <summary>
        /// 播放发言（带 TTS 合成）
        /// </summary>
        public void Speak(string text, Action onComplete = null)
        {
            if (!_configManager.HasValidTTSConfig())
            {
                AITalkPlugin.Log.LogWarning("[Audio] TTS配置不完整，跳过发言");
                onComplete?.Invoke();
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
        public bool IsPlaying => _audioPlayer.IsPlaying;

        /// <summary>
        /// 是否正在播放发言
        /// </summary>
        public bool IsSpeaking => _audioPlayer.IsPlayingVoice;
    }
}
