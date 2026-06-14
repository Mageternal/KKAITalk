using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace KKAITalk.Audio
{
    /// <summary>
    /// TTS 客户端 - 对接 Qwen3-TTS 服务（Unity 旧版 API 兼容）
    /// </summary>
    public class TTSClient : MonoBehaviour
    {
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 9881;

        private string _host;
        private int _port;
        private AudioConfigManager _configManager;

        // 请求队列
        private class TTSRequest
        {
            public string Text;
            public Action<byte[]> OnComplete;
            public Action<string> OnError;
        }
        private Queue<TTSRequest> _requestQueue = new Queue<TTSRequest>();
        private bool _isProcessingQueue = false;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(AudioConfigManager configManager)
        {
            _configManager = configManager;
            _host = DefaultHost;
            _port = DefaultPort;

            AITalkPlugin.Log?.LogInfo("[TTS] TTS客户端初始化完成");
            AITalkPlugin.Log?.LogInfo("[TTS] 目标服务器: " + _host + ":" + _port);
        }

        /// <summary>
        /// 设置服务器地址
        /// </summary>
        public void SetServer(string host, int port)
        {
            _host = host;
            _port = port;
            AITalkPlugin.Log.LogInfo("[TTS] 服务器地址已更新: " + _host + ":" + _port);
        }

        /// <summary>
        /// 合成语音
        /// </summary>
        public void Synthesize(string text, Action<byte[]> onComplete, Action<string> onError = null)
        {
            if (_configManager == null)
            {
                if (onError != null) onError.Invoke("TTS客户端未初始化");
                return;
            }

            // 没有音色样本的角色直接跳过：不发起请求，不入队
            if (!_configManager.HasValidTTSConfig())
            {
                string charaName = _configManager.GetCurrentCharacter();
                if (AITalkPlugin.Log != null)
                    AITalkPlugin.Log.LogInfo("[TTS] 角色 \"" + charaName + "\" 没有音频样本，跳过 TTS 请求");
                if (onComplete != null) onComplete.Invoke(null);
                return;
            }

            var timbreConfig = _configManager.GetCurrentTimbreConfig();
            if (timbreConfig == null)
            {
                if (onError != null) onError.Invoke("未找到音色配置");
                return;
            }

            var refAudioPath = _configManager.GetRefAudioFullPath(timbreConfig.RefAudioPath);
            if (string.IsNullOrEmpty(refAudioPath) || !File.Exists(refAudioPath))
            {
                if (onError != null) onError.Invoke("参考音频文件不存在: " + refAudioPath);
                return;
            }

            var request = new TTSRequest
            {
                Text = text,
                OnComplete = onComplete,
                OnError = onError
            };

            _requestQueue.Enqueue(request);
            ProcessQueue();
        }

        /// <summary>
        /// 处理队列
        /// </summary>
        private void ProcessQueue()
        {
            if (_isProcessingQueue || _requestQueue.Count == 0)
                return;

            _isProcessingQueue = true;
            var request = _requestQueue.Dequeue();

            StartCoroutine(SendTTSRequestCoroutine(request));
        }

        /// <summary>
        /// 发送 TTS 请求（兼容旧版 Unity）
        /// </summary>
        private IEnumerator SendTTSRequestCoroutine(TTSRequest request)
        {
            var timbreConfig = _configManager.GetCurrentTimbreConfig();
            var refAudioPath = _configManager.GetRefAudioFullPath(timbreConfig.RefAudioPath);

            string uri = "http://" + _host + ":" + _port + "/v1/voice_clone";
            string logText = request.Text.Length > 20 ? request.Text.Substring(0, 20) + "..." : request.Text;
            AITalkPlugin.Log.LogInfo("[TTS] 发送请求: " + logText);

            // 构建 multipart 表单数据
            byte[] audioBytes = File.ReadAllBytes(refAudioPath);

            // 使用 UnityWebRequest + UploadHandlerRaw 发送 multipart
            // 手动构建 multipart body 以避免旧版 Unity 的兼容性问题
            string boundary = "----KKAITalkBoundary" + System.DateTime.Now.Ticks.ToString("x");
            byte[] body = BuildMultipartBody(boundary, request.Text, timbreConfig, audioBytes, refAudioPath);

            UnityWebRequest www = new UnityWebRequest(uri, "POST");
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);

            yield return www.Send();

            _isProcessingQueue = false;

            // 旧版 Unity：isError 只判断网络错误，HTTP 4xx/5xx 必须看 responseCode
            long responseCode = www.responseCode;
            bool hasError = www.isError || responseCode >= 400;
            byte[] responseBytes = www.downloadHandler.data;

            if (!hasError)
            {
                AITalkPlugin.Log.LogInfo("[TTS] 合成成功! 音频大小: " + responseBytes.Length + " bytes");
                if (request.OnComplete != null) request.OnComplete.Invoke(responseBytes);
            }
            else
            {
                string errorBody = (responseBytes != null && responseBytes.Length > 0 && responseBytes.Length < 2048)
                    ? System.Text.Encoding.UTF8.GetString(responseBytes)
                    : "";
                string errorMsg = "HTTP " + responseCode;
                if (!string.IsNullOrEmpty(www.error)) errorMsg += " (" + www.error + ")";
                if (!string.IsNullOrEmpty(errorBody)) errorMsg += " | " + errorBody;
                AITalkPlugin.Log.LogError("[TTS] 请求失败: " + errorMsg);
                if (request.OnError != null) request.OnError.Invoke(errorMsg);
            }

            // 处理下一个请求
            ProcessQueue();
        }

        /// <summary>
        /// 构建 multipart 表单数据
        /// </summary>
        private byte[] BuildMultipartBody(string boundary, string text, AudioConfigManager.TimbreConfig config, byte[] audioData, string audioPath)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"text\"").AppendLine();
            sb.AppendLine();
            sb.Append(text).AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"language\"").AppendLine();
            sb.AppendLine();
            sb.Append(config.Language ?? "chinese").AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"ref_text\"").AppendLine();
            sb.AppendLine();
            sb.Append(config.RefText ?? "").AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"temperature\"").AppendLine();
            sb.AppendLine();
            sb.Append(config.Temperature.ToString()).AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"sub_temperature\"").AppendLine();
            sb.AppendLine();
            sb.Append(config.SubTemperature.ToString()).AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"seed\"").AppendLine();
            sb.AppendLine();
            sb.Append("42").AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"sub_seed\"").AppendLine();
            sb.AppendLine();
            sb.Append("45").AppendLine();

            if (!string.IsNullOrEmpty(config.Instruct))
            {
                sb.Append("--").Append(boundary).AppendLine();
                sb.Append("Content-Disposition: form-data; name=\"instruct\"").AppendLine();
                sb.AppendLine();
                sb.Append(config.Instruct).AppendLine();
            }

            // 服务端预热过的 cache_key（命中后跳过音频编码）
            string cacheKey = _configManager.GetCacheKey();
            if (!string.IsNullOrEmpty(cacheKey))
            {
                sb.Append("--").Append(boundary).AppendLine();
                sb.Append("Content-Disposition: form-data; name=\"cache_key\"").AppendLine();
                sb.AppendLine();
                sb.Append(cacheKey).AppendLine();
            }

            // 服务端可能从 path 自己读文件（fallback）
            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"ref_audio_path\"").AppendLine();
            sb.AppendLine();
            sb.Append(audioPath ?? "").AppendLine();

            string filename = Path.GetFileName(audioPath);
            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"ref_audio\"; filename=\"").Append(filename).Append("\"").AppendLine();
            sb.Append("Content-Type: audio/wav").AppendLine();
            sb.AppendLine();

            // 编码文本部分
            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            // RFC 7578：文件 part 末尾必须有 CRLF 分隔，再接 closing boundary
            byte[] partSepBytes = System.Text.Encoding.UTF8.GetBytes("\r\n");
            byte[] boundaryBytes = System.Text.Encoding.UTF8.GetBytes("--" + boundary + "--\r\n");

            // 合并：文本 + 音频数据 + CRLF + 结束边界
            byte[] result = new byte[textBytes.Length + audioData.Length + partSepBytes.Length + boundaryBytes.Length];
            Buffer.BlockCopy(textBytes, 0, result, 0, textBytes.Length);
            Buffer.BlockCopy(audioData, 0, result, textBytes.Length, audioData.Length);
            Buffer.BlockCopy(partSepBytes, 0, result, textBytes.Length + audioData.Length, partSepBytes.Length);
            Buffer.BlockCopy(boundaryBytes, 0, result, textBytes.Length + audioData.Length + partSepBytes.Length, boundaryBytes.Length);

            return result;
        }

        /// <summary>
        /// 构建 /v1/voice_encode 用的 multipart body（不带 text / seed / temperature）
        /// </summary>
        private byte[] BuildEncodeMultipartBody(string boundary, AudioConfigManager.TimbreConfig config, byte[] audioData, string audioPath)
        {
            var sb = new System.Text.StringBuilder();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"ref_audio_path\"").AppendLine();
            sb.AppendLine();
            sb.Append(audioPath ?? "").AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"language\"").AppendLine();
            sb.AppendLine();
            sb.Append(config.Language ?? "chinese").AppendLine();

            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"ref_text\"").AppendLine();
            sb.AppendLine();
            sb.Append(config.RefText ?? "").AppendLine();

            string filename = Path.GetFileName(audioPath);
            sb.Append("--").Append(boundary).AppendLine();
            sb.Append("Content-Disposition: form-data; name=\"ref_audio\"; filename=\"").Append(filename).Append("\"").AppendLine();
            sb.Append("Content-Type: audio/wav").AppendLine();
            sb.AppendLine();

            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            byte[] partSepBytes = System.Text.Encoding.UTF8.GetBytes("\r\n");
            byte[] boundaryBytes = System.Text.Encoding.UTF8.GetBytes("--" + boundary + "--\r\n");

            byte[] result = new byte[textBytes.Length + audioData.Length + partSepBytes.Length + boundaryBytes.Length];
            Buffer.BlockCopy(textBytes, 0, result, 0, textBytes.Length);
            Buffer.BlockCopy(audioData, 0, result, textBytes.Length, audioData.Length);
            Buffer.BlockCopy(partSepBytes, 0, result, textBytes.Length + audioData.Length, partSepBytes.Length);
            Buffer.BlockCopy(boundaryBytes, 0, result, textBytes.Length + audioData.Length + partSepBytes.Length, boundaryBytes.Length);

            return result;
        }

        /// <summary>
        /// 预热当前角色音色（/v1/voice_encode），服务端会缓存音色并返回 cache_key
        /// </summary>
        public void EncodeVoice(Action<string> onSuccess, Action<string> onError)
        {
            if (_configManager == null)
            {
                if (onError != null) onError.Invoke("TTS客户端未初始化");
                return;
            }

            var timbreConfig = _configManager.GetCurrentTimbreConfig();
            if (timbreConfig == null)
            {
                if (onError != null) onError.Invoke("未找到音色配置");
                return;
            }

            var refAudioPath = _configManager.GetRefAudioFullPath(timbreConfig.RefAudioPath);
            if (string.IsNullOrEmpty(refAudioPath) || !File.Exists(refAudioPath))
            {
                if (onError != null) onError.Invoke("参考音频文件不存在: " + refAudioPath);
                return;
            }

            StartCoroutine(EncodeVoiceCoroutine(timbreConfig, refAudioPath, onSuccess, onError));
        }

        private IEnumerator EncodeVoiceCoroutine(AudioConfigManager.TimbreConfig config, string refAudioPath, Action<string> onSuccess, Action<string> onError)
        {
            byte[] audioBytes;
            try
            {
                audioBytes = File.ReadAllBytes(refAudioPath);
            }
            catch (Exception ex)
            {
                if (onError != null) onError.Invoke("读取参考音频失败: " + ex.Message);
                yield break;
            }

            string boundary = "----KKAITalkBoundary" + System.DateTime.Now.Ticks.ToString("x");
            byte[] body = BuildEncodeMultipartBody(boundary, config, audioBytes, refAudioPath);

            string uri = "http://" + _host + ":" + _port + "/v1/voice_encode";
            AITalkPlugin.Log.LogInfo("[TTS] 预热音色: " + Path.GetFileName(refAudioPath) + " (" + audioBytes.Length + " bytes)");

            UnityWebRequest www = new UnityWebRequest(uri, "POST");
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);

            yield return www.Send();

            long responseCode = www.responseCode;
            byte[] responseBytes = www.downloadHandler.data;

            if (www.isError || responseCode >= 400)
            {
                string errorBody = (responseBytes != null && responseBytes.Length > 0 && responseBytes.Length < 2048)
                    ? System.Text.Encoding.UTF8.GetString(responseBytes) : "";
                string msg = "HTTP " + responseCode;
                if (!string.IsNullOrEmpty(www.error)) msg += " (" + www.error + ")";
                if (!string.IsNullOrEmpty(errorBody)) msg += " | " + errorBody;
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogError("[TTS] 预热失败: " + msg);
                if (onError != null) onError.Invoke(msg);
                yield break;
            }

            // 解析 cache_key（服务端返回 {"cache_key": "xxx"}）
            string json = System.Text.Encoding.UTF8.GetString(responseBytes);
            string cacheKey = ExtractCacheKey(json);
            if (string.IsNullOrEmpty(cacheKey))
            {
                if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogError("[TTS] 预热响应无 cache_key: " + json);
                if (onError != null) onError.Invoke("响应无 cache_key");
                yield break;
            }

            if (AITalkPlugin.Log != null) AITalkPlugin.Log.LogInfo("[TTS] 预热成功, cache_key=" + cacheKey);
            if (onSuccess != null) onSuccess.Invoke(cacheKey);
        }

        /// <summary>
        /// 从 JSON 响应里抠 cache_key，避免引入 JsonUtility
        /// </summary>
        private static string ExtractCacheKey(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            string marker = "\"cache_key\"";
            int idx = json.IndexOf(marker);
            if (idx < 0) return "";
            int colon = json.IndexOf(':', idx + marker.Length);
            if (colon < 0) return "";
            int q1 = json.IndexOf('"', colon + 1);
            if (q1 < 0) return "";
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return "";
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        /// <summary>
        /// 检查服务器是否可用
        /// </summary>
        public void CheckServer(Action<bool> onResult)
        {
            StartCoroutine(CheckServerCoroutine(onResult));
        }

        /// <summary>
        /// 检查服务器（兼容旧版 Unity）
        /// </summary>
        private IEnumerator CheckServerCoroutine(Action<bool> onResult)
        {
            string uri = "http://" + _host + ":" + _port + "/v1/status";
            UnityWebRequest www = UnityWebRequest.Get(uri);
            yield return www.Send();
            bool isOnline = !www.isError;
            if (onResult != null) onResult.Invoke(isOnline);
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get { return _isProcessingQueue || _requestQueue.Count > 0; }
        }
    }
}