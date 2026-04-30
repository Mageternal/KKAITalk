using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace KKAITalk.Audio
{
    /// <summary>
    /// TTS 客户端 - 对接 Qwen3-TTS 服务
    /// </summary>
    public class TTSClient : MonoBehaviour
    {
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 9881;

        private string _host;
        private int _port;
        private AudioConfigManager _configManager;

        // 缓存的音色 key
        private string _cachedVoiceKey = "";

        // 回调
        private System.Action<byte[]> _onAudioReceived;
        private System.Action<string> _onError;

        // 线程安全
        private readonly object _lockObj = new object();
        private bool _isProcessing = false;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(AudioConfigManager configManager)
        {
            _configManager = configManager;
            _host = DefaultHost;
            _port = DefaultPort;

            AITalkPlugin.Log.LogInfo($"[TTS] TTS客户端初始化完成");
            AITalkPlugin.Log.LogInfo($"[TTS] 目标服务器: {_host}:{_port}");
        }

        /// <summary>
        /// 设置服务器地址
        /// </summary>
        public void SetServer(string host, int port)
        {
            _host = host;
            _port = port;
            AITalkPlugin.Log.LogInfo($"[TTS] 服务器地址已更新: {_host}:{_port}");
        }

        /// <summary>
        /// 合成语音
        /// </summary>
        /// <param name="text">要合成的文本</param>
        /// <param name="onComplete">完成回调 (音频数据)</param>
        /// <param name="onError">错误回调</param>
        public void Synthesize(string text, System.Action<byte[]> onComplete, System.Action<string> onError = null)
        {
            if (_configManager == null)
            {
                onError?.Invoke("TTS客户端未初始化");
                return;
            }

            var timbreConfig = _configManager.GetCurrentTimbreConfig();
            if (timbreConfig == null)
            {
                onError?.Invoke("未找到音色配置");
                return;
            }

            var refAudioPath = _configManager.GetRefAudioFullPath(timbreConfig.RefAudioPath);
            if (string.IsNullOrEmpty(refAudioPath) || !File.Exists(refAudioPath))
            {
                onError?.Invoke($"参考音频文件不存在: {refAudioPath}");
                return;
            }

            lock (_lockObj)
            {
                if (_isProcessing)
                {
                    onError?.Invoke("正在处理上一个请求");
                    return;
                }
                _isProcessing = true;
            }

            _onAudioReceived = onComplete;
            _onError = onError;

            StartCoroutine(SendTTSRequest(text, timbreConfig, refAudioPath));
        }

        /// <summary>
        /// 发送 TTS 请求
        /// </summary>
        private IEnumerator SendTTSRequest(string text, AudioConfigManager.TimbreConfig config, string refAudioPath)
        {
            var url = $"http://{_host}:{_port}/v1/voice_clone";

            // 读取参考音频
            byte[] refAudioData = null;
            try
            {
                refAudioData = File.ReadAllBytes(refAudioPath);
            }
            catch (Exception ex)
            {
                AITalkPlugin.Log.LogError($"[TTS] 读取参考音频失败: {ex.Message}");
                lock (_lockObj) { _isProcessing = false; }
                _onError?.Invoke($"读取参考音频失败: {ex.Message}");
                yield break;
            }

            // 生成 form
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("text", text),
                new MultipartFormDataSection("language", config.Language ?? "chinese"),
                new MultipartFormDataSection("ref_text", config.RefText ?? ""),
                new MultipartFormDataSection("temperature", config.Temperature.ToString()),
                new MultipartFormDataSection("sub_temperature", config.SubTemperature.ToString()),
                new MultipartFormDataSection("seed", "42"),
                new MultipartFormDataSection("sub_seed", "45"),
            };

            // 添加 instruct（如果有）
            if (!string.IsNullOrEmpty(config.Instruct))
            {
                form.Add(new MultipartFormDataSection("instruct", config.Instruct));
            }

            // 添加参考音频
            string fileName = Path.GetFileName(refAudioPath);
            form.Add(new MultipartFormFileSection("ref_audio", refAudioData, fileName, "audio/wav"));

            AITalkPlugin.Log.LogInfo($"[TTS] 发送请求: {text.Substring(0, Mathf.Min(20, text.Length))}...");

            using (var request = UnityWebRequest.Post(url, form))
            {
                request.timeout = 60; // 60秒超时

                yield return request.SendWebRequest();

                float startTime = Time.time;

                while (!request.isDone && !request.isNetworkError)
                {
                    yield return new WaitForSeconds(0.1f);

                    // 防止无限等待
                    if (Time.time - startTime > 60f)
                    {
                        request.Abort();
                        break;
                    }
                }

                if (request.isNetworkError)
                {
                    AITalkPlugin.Log.LogError($"[TTS] 网络错误: {request.error}");
                    lock (_lockObj) { _isProcessing = false; }
                    _onError?.Invoke($"网络错误: {request.error}");
                    yield break;
                }

                if (request.responseCode == 200)
                {
                    var audioData = request.downloadHandler.data;
                    AITalkPlugin.Log.LogInfo($"[TTS] 合成成功! 音频大小: {audioData.Length} bytes");

                    lock (_lockObj) { _isProcessing = false; }
                    _onAudioReceived?.Invoke(audioData);
                }
                else
                {
                    string errorMsg = $"请求失败: {request.responseCode}";
                    AITalkPlugin.Log.LogError($"[TTS] {errorMsg}");
                    lock (_lockObj) { _isProcessing = false; }
                    _onError?.Invoke(errorMsg);
                }
            }
        }

        /// <summary>
        /// 检查服务器是否可用
        /// </summary>
        public void CheckServer(System.Action<bool> onResult)
        {
            StartCoroutine(CheckServerCoroutine(onResult));
        }

        private IEnumerator CheckServerCoroutine(System.Action<bool> onResult)
        {
            var url = $"http://{_host}:{_port}/v1/status";

            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 5;

                yield return request.SendWebRequest();

                if (request.isNetworkError)
                {
                    AITalkPlugin.Log.LogWarning($"[TTS] 服务器不可用: {request.error}");
                    onResult?.Invoke(false);
                }
                else if (request.responseCode == 200)
                {
                    AITalkPlugin.Log.LogInfo($"[TTS] 服务器在线!");
                    onResult?.Invoke(true);
                }
                else
                {
                    AITalkPlugin.Log.LogWarning($"[TTS] 服务器响应异常: {request.responseCode}");
                    onResult?.Invoke(false);
                }
            }
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get
            {
                lock (_lockObj)
                {
                    return _isProcessing;
                }
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            _cachedVoiceKey = "";
            AITalkPlugin.Log.LogInfo($"[TTS] 缓存已清除");
        }
    }
}
