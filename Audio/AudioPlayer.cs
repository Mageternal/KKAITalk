using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KKAITalk.Audio
{
    /// <summary>
    /// 音频播放器 - 管理喘息和发言的混合播放
    /// </summary>
    public class AudioPlayer : MonoBehaviour
    {
        private AudioConfigManager _configManager;
        private TTSClient _ttsClient;

        // 音频源
        private AudioSource _voiceSource;    // 发言音频
        private AudioSource _respiteSource;   // 喘息音频

        // 音量
        private float _voiceVolume = 1.0f;
        private float _respiteVolume = 1.0f;

        // 状态
        private bool _isPlayingVoice = false;
        private bool _isPlayingRespite = false;

        // 当前播放的喘息索引
        private int _currentRespiteIndex = 0;
        private List<string> _respiteFiles = new List<string>();

        // 发言队列
        private Queue<string> _voiceQueue = new Queue<string>();
        private bool _isProcessingQueue = false;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(AudioConfigManager configManager, TTSClient ttsClient)
        {
            _configManager = configManager;
            _ttsClient = ttsClient;

            // 创建发言 AudioSource
            var voiceObj = new GameObject("TTSVoice");
            voiceObj.transform.SetParent(transform);
            _voiceSource = voiceObj.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
            _voiceSource.loop = false;
            _voiceSource.volume = _voiceVolume;

            // 创建喘息 AudioSource
            var respiteObj = new GameObject("RespiteVoice");
            respiteObj.transform.SetParent(transform);
            _respiteSource = respiteObj.AddComponent<AudioSource>();
            _respiteSource.playOnAwake = false;
            _respiteSource.loop = false;
            _respiteSource.volume = _respiteVolume;

            // 加载喘息文件
            LoadRespiteFiles();

            AITalkPlugin.Log.LogInfo($"[Audio] 音频播放器初始化完成");
        }

        /// <summary>
        /// 加载当前角色的喘息文件
        /// </summary>
        private void LoadRespiteFiles()
        {
            _respiteFiles = _configManager.GetCurrentRespiteFiles();
            _currentRespiteIndex = 0;

            if (_respiteFiles.Count > 0)
            {
                AITalkPlugin.Log.LogInfo($"[Audio] 加载喘息文件: {_respiteFiles.Count} 个");
            }
            else
            {
                AITalkPlugin.Log.LogWarning($"[Audio] 未找到喘息文件");
            }
        }

        /// <summary>
        /// 刷新喘息文件（角色切换时调用）
        /// </summary>
        public void RefreshRespiteFiles()
        {
            LoadRespiteFiles();
        }

        /// <summary>
        /// 播放发言
        /// </summary>
        public void PlayVoice(string text, Action onComplete = null)
        {
            _voiceQueue.Enqueue(text);
            ProcessVoiceQueue(onComplete);
        }

        /// <summary>
        /// 处理发言队列
        /// </summary>
        private void ProcessVoiceQueue(Action onComplete)
        {
            if (_isProcessingQueue || _voiceQueue.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            if (_ttsClient == null || !_ttsClient.IsProcessing)
            {
                _isProcessingQueue = true;
                var text = _voiceQueue.Dequeue();

                _ttsClient.Synthesize(
                    text,
                    (audioData) =>
                    {
                        PlayAudioData(audioData, _voiceSource);
                        _isProcessingQueue = false;
                    },
                    (error) =>
                    {
                        AITalkPlugin.Log.LogError($"[Audio] TTS合成失败: {error}");
                        _isProcessingQueue = false;
                    }
                );
            }
        }

        /// <summary>
        /// 播放字节数组音频数据
        /// </summary>
        private void PlayAudioData(byte[] audioData, AudioSource source)
        {
            if (audioData == null || audioData.Length == 0)
            {
                AITalkPlugin.Log.LogWarning("[Audio] 音频数据为空");
                return;
            }

            try
            {
                // WAV 格式检测
                if (audioData.Length > 44 &&
                    audioData[0] == 'R' && audioData[1] == 'I' &&
                    audioData[2] == 'F' && audioData[3] == 'F')
                {
                    // 跳过 WAV 头
                    var audioClip = CreateAudioClipFromWav(audioData);
                    if (audioClip != null)
                    {
                        source.clip = audioClip;
                        source.Play();

                        AITalkPlugin.Log.LogInfo($"[Audio] 播放音频，时长: {audioClip.length:F2}s");
                    }
                }
                else
                {
                    AITalkPlugin.Log.LogWarning("[Audio] 未知音频格式");
                }
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[Audio] 播放音频失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 WAV 字节数组创建 AudioClip
        /// </summary>
        private AudioClip CreateAudioClipFromWav(byte[] wavData)
        {
            // 解析 WAV 头
            int channels = BitConverter.ToInt16(wavData, 22);
            int sampleRate = BitConverter.ToInt32(wavData, 24);
            int bitsPerSample = BitConverter.ToInt16(wavData, 34);

            // 找到数据块
            int dataIndex = 44;
            for (int i = 44; i < wavData.Length - 8; i++)
            {
                if (wavData[i] == 'd' && wavData[i + 1] == 'a' &&
                    wavData[i + 2] == 't' && wavData[i + 3] == 'a')
                {
                    dataIndex = i + 8;
                    break;
                }
            }

            int dataSize = wavData.Length - dataIndex;
            float[] samples = new float[dataSize / 2];

            // 转换为 float
            for (int i = 0; i < samples.Length; i++)
            {
                short sample = BitConverter.ToInt16(wavData, dataIndex + i * 2);
                samples[i] = sample / 32768f;
            }

            // 创建 AudioClip
            var clip = AudioClip.Create("TTSVoice", samples.Length, channels, sampleRate, false);
            clip.SetData(samples, 0);

            return clip;
        }

        /// <summary>
        /// 播放喘息
        /// </summary>
        public void PlayRespite()
        {
            if (_respiteFiles.Count == 0)
                return;

            if (_respiteSource.isPlaying)
                return;

            var file = _respiteFiles[_currentRespiteIndex];
            _currentRespiteIndex = (_currentRespiteIndex + 1) % _respiteFiles.Count;

            try
            {
                var audioData = File.ReadAllBytes(file);
                PlayAudioData(audioData, _respiteSource);

                AITalkPlugin.Log.LogInfo($"[Audio] 播放喘息: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[Audio] 播放喘息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止发言
        /// </summary>
        public void StopVoice()
        {
            _voiceSource.Stop();
            _voiceQueue.Clear();
            _isProcessingQueue = false;
        }

        /// <summary>
        /// 停止喘息
        /// </summary>
        public void StopRespite()
        {
            _respiteSource.Stop();
        }

        /// <summary>
        /// 停止所有音频
        /// </summary>
        public void StopAll()
        {
            StopVoice();
            StopRespite();
        }

        /// <summary>
        /// 设置发言音量
        /// </summary>
        public void SetVoiceVolume(float volume)
        {
            _voiceVolume = Mathf.Clamp01(volume);
            _voiceSource.volume = _voiceVolume;
        }

        /// <summary>
        /// 设置喘息音量
        /// </summary>
        public void SetRespiteVolume(float volume)
        {
            _respiteVolume = Mathf.Clamp01(volume);
            _respiteSource.volume = _respiteVolume;
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying
        {
            get { return _voiceSource.isPlaying || _respiteSource.isPlaying || _voiceQueue.Count > 0; }
        }

        /// <summary>
        /// 是否正在播放发言
        /// </summary>
        public bool IsPlayingVoice => _voiceSource.isPlaying || _voiceQueue.Count > 0;

        /// <summary>
        /// 是否正在播放喘息
        /// </summary>
        public bool IsPlayingRespite => _respiteSource.isPlaying;
    }
}
