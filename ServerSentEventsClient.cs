//using System;

//using System.IO;
//using System.Net;
//using System.Net.Security;
//using System.Security.Cryptography.X509Certificates;
//using System.Text;
//using System.Threading;


//namespace Main.Net
//{
//    public class ServerSentEventsClient
//    {
//        private Thread m_SSEThread;
//        private Stream m_SSEStream;
//        public Action<string> OnDataUpdate;
//        private bool m_IsWork;

//        public void Open(string url)
//        {
//            if (url.StartsWith("https"))
//            {
//                ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
//            }

//            m_IsWork = true;
//            m_SSEStream = OpenSSEStream(url);
//            m_SSEThread = new Thread(ReadStreamForever);
//            m_SSEThread.Start();
//        }

//        public void Close()
//        {
//            m_IsWork = false;
//            m_SSEStream?.Dispose();
//            m_SSEThread?.Abort();
//        }

//        private Stream OpenSSEStream(string url)
//        {
//            var request = WebRequest.Create(new Uri(url));
//            ((HttpWebRequest)request).AllowReadStreamBuffering = false;
//            var response = request.GetResponse();
//            var stream = response.GetResponseStream();

//            return stream;
//        }

//        /// <summary>
//        /// sse传输消息的固定格式是data:数据，在处理消息时直接去掉data:，因此不要在数据中写data:结构
//        /// 另外，此方式会漏掉最后一次发送的消息
//        /// </summary>
//        private void ReadStreamForever()
//        {
//            var encoder = new UTF8Encoding();
//            var maxCount = 2048;
//            var buffer = new byte[maxCount];
//            var msg = "";
//            while (m_IsWork)
//            {
//                if (m_SSEStream.CanRead)
//                {
//                    var len = m_SSEStream.Read(buffer, 0, maxCount);
//                    if (len > 0)
//                    {
//                        var text = encoder.GetString(buffer, 0, len).Trim();
//                        if (text.StartsWith("data:"))
//                        {
//                            ProcessEventData(msg.Replace("data:", ""));
//                            msg = "";
//                        }

//                        msg += text;
//                    }
//                }
//            }
//        }

//        private void ProcessEventData(string data)
//        {
//            if (string.IsNullOrWhiteSpace(data))
//            {
//                return;
//            }

//            OnDataUpdate?.Invoke(data);
//        }

//        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
//        {
//            return true;
//        }
//    }
//}

//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Net;
//using System.Net.Security;
//using System.Security.Cryptography.X509Certificates;
//using System.Text;
//using System.Threading;

//namespace Main.Net
//{
//    /// <summary>
//    /// 轻量 SSE 客户端（Unity 可用）
//    /// - 按 SSE 标准按行解析：以空行结束一帧，支持多行 data:
//    /// - 通过线程持续读取，消息放入线程安全队列，供主线程取用
//    /// </summary>
//    public class ServerSentEventsClient
//    {
//        private Thread m_SSEThread;
//        private Stream m_SSEStream;
//        private volatile bool m_IsWork;
//        private HttpWebRequest m_Request;
//        private WebResponse m_Response;

//        // 将解析出来的 data（通常是 JSON 文本）放这里，主线程拉取
//        public readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();

//        /// <summary>
//        /// 连接并开始读取 SSE
//        /// </summary>
//        public void Open(string url)
//        {
//            if (url.StartsWith("https", StringComparison.OrdinalIgnoreCase))
//            {
//                ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
//            }

//            m_IsWork = true;
//            m_SSEStream = OpenSSEStream(url);
//            m_SSEThread = new Thread(ReadStreamLoop)
//            {
//                IsBackground = true,
//                Name = "SSE-Reader"
//            };
//            m_SSEThread.Start();
//        }

//        /// <summary>
//        /// 关闭连接
//        /// </summary>
//        public void Close()
//        {
//            m_IsWork = false;

//            try { m_SSEStream?.Dispose(); } catch { }
//            try { m_Response?.Close(); } catch { }
//            try { m_Request?.Abort(); } catch { }

//            // 安全结束读取线程（避免 Abort）
//            if (m_SSEThread != null && m_SSEThread.IsAlive)
//            {
//                if (!m_SSEThread.Join(500))
//                {
//                    // 如果 0.5s 内还没结束，放弃等待（不要强制 Abort）
//                }
//            }
//        }

//        //private Stream OpenSSEStream(string url)
//        //{
//        //    m_Request = (HttpWebRequest)WebRequest.Create(new Uri(url));
//        //    m_Request.Method = "GET";
//        //    m_Request.AllowReadStreamBuffering = false;         // 关闭缓冲，边到边读
//        //    m_Request.KeepAlive = true;
//        //    m_Request.Timeout = Timeout.Infinite;               // 不超时
//        //    m_Request.ReadWriteTimeout = Timeout.Infinite;
//        //    m_Request.Headers.Add(HttpRequestHeader.Accept, "text/event-stream");
//        //    m_Request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

//        //    m_Response = m_Request.GetResponse();
//        //    var stream = m_Response.GetResponseStream();
//        //    return stream;
//        //}
//        private Stream OpenSSEStream(string url)
//        {
//            m_Request = (HttpWebRequest)WebRequest.Create(new Uri(url));
//            m_Request.Method = "GET";
//            m_Request.ProtocolVersion = HttpVersion.Version11;
//            m_Request.KeepAlive = true;
//            m_Request.Proxy = null;                                // 避免系统代理缓冲
//            m_Request.AllowReadStreamBuffering = false;

//            m_Request.Accept = "text/event-stream";
//            // 关闭压缩，避免某些代理对压缩流做缓冲
//            m_Request.AutomaticDecompression = DecompressionMethods.None;
//            m_Request.Headers[HttpRequestHeader.AcceptEncoding] = "identity";
//            // 明确禁用缓存
//            m_Request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
//            m_Request.Headers[HttpRequestHeader.Pragma] = "no-cache";

//            // 有些平台不接受 Infinite，用较大的超时时间
//            const int TenMinutes = 600_000;
//            m_Request.Timeout = TenMinutes;
//            m_Request.ReadWriteTimeout = TenMinutes;

//            // 可选：TCP keepalive，避免中间设备闲置掐断
//            try { m_Request.ServicePoint.SetTcpKeepAlive(true, 15000, 15000); } catch { }

//            m_Response = m_Request.GetResponse();
//            return m_Response.GetResponseStream();
//        }


//        /// <summary>
//        /// 标准 SSE 解析：逐行读取，空行表示一个事件结束；允许多行 data:
//        /// 支持忽略以 ':' 开头的注释行，兼容 event:/id:/retry: 字段（当前只使用 data）
//        /// </summary>
//        private void ReadStreamLoop()
//        {
//            if (m_SSEStream == null) return;

//            using (var reader = new StreamReader(m_SSEStream, new UTF8Encoding(false)))
//            {
//                string line;
//                var sbData = new StringBuilder();

//                try
//                {
//                    while (m_IsWork)
//                    {
//                        line = reader.ReadLine(); // 阻塞等待一行
//                        if (line == null) break;  // 断开

//                        //UnityEngine.Debug.Log($"[SSE][line] '{line}'"); // 调试：看到每一行

//                        if (line.Length == 0)
//                        {
//                            // 规范：空行 => 结束一帧
//                            if (sbData.Length > 0)
//                            {
//                                var payload = sbData.ToString();
//                                MessageQueue.Enqueue(payload);
//                                sbData.Length = 0;
//                                //UnityEngine.Debug.Log($"[SSE][payload] '{payload}'");
//                            }
//                            continue;
//                        }

//                        if (line.StartsWith(":", StringComparison.Ordinal))
//                        {
//                            // 心跳/注释行也打印一下，证明连接活着
//                            //UnityEngine.Debug.Log($"[SSE][heartbeat] {line}");
//                            continue;
//                        }

//                        int idx = line.IndexOf(':');
//                        string field, value;
//                        if (idx >= 0)
//                        {
//                            field = line.Substring(0, idx).Trim();
//                            value = line.Substring(idx + 1).TrimStart();
//                        }
//                        else
//                        {
//                            field = "data";
//                            value = line;
//                        }

//                        if (field == "data")
//                        {
//                            // 兼容：部分服务端不发空行，直接连发下一条 data:
//                            // 这时先把上一条当成一个完整事件发出去
//                            if (sbData.Length > 0)
//                            {
//                                MessageQueue.Enqueue(sbData.ToString());
//                                sbData.Length = 0;
//                            }
//                            sbData.Append(value);
//                        }
//                        // event/id/retry 如需用可在此记录
//                    }
//                }
//                catch (Exception ex)
//                {
//                    UnityEngine.Debug.Log($"[SSE] Read loop exception: {ex.Message}");
//                }
//                finally
//                {
//                    if (sbData.Length > 0)
//                        MessageQueue.Enqueue(sbData.ToString());
//                }
//            }
//        }


//        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
//        {
//            // 开发环境忽略证书（生产环境请务必校验！）
//            return true;
//        }
//    }

//}



using System;
using System.Collections.Concurrent;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Main.Net
{
    public class ServerSentEventsClient : IDisposable
    {
        private UnityWebRequest _uwr;
        private SseDownloadHandler _downloadHandler;
        public readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();

        private bool _isRunning;

        // 跳过证书验证的类
        private class BypassCertHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                return true; // 永远信任证书
            }
        }

        public void Open(string url)
        {
            if (_isRunning) return;

            _downloadHandler = new SseDownloadHandler(MessageQueue);
            _uwr = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET);
            _uwr.downloadHandler = _downloadHandler;
            _uwr.certificateHandler = new BypassCertHandler(); //跳过 SSL 验证
            _uwr.disposeCertificateHandlerOnDispose = true;

            _uwr.SetRequestHeader("Accept", "text/event-stream");
            _uwr.SetRequestHeader("Cache-Control", "no-cache");
            _uwr.SetRequestHeader("Accept-Encoding", "identity");

            // 开始协程执行
            _isRunning = true;
            CoroutineRunner.Instance.StartCoroutine(RunRequest());
        }

        private System.Collections.IEnumerator RunRequest()
        {
            using (_uwr)
            {
                yield return _uwr.SendWebRequest();

                if (_uwr.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[SSE] Error: {_uwr.error}");
                }
                else
                {
                    Debug.Log("[SSE] Connection closed normally.");
                }
            }
            _isRunning = false;
        }

        public void Close()
        {
            if (!_isRunning) return;
            _isRunning = false;
            try { _uwr?.Abort(); } catch { }
        }

        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// 静态协程承载器，方便非 MonoBehaviour 类跑协程
        /// </summary>
        private class CoroutineRunner : MonoBehaviour
        {
            private static CoroutineRunner _instance;
            public static CoroutineRunner Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("[SSE CoroutineRunner]");
                        DontDestroyOnLoad(go);
                        _instance = go.AddComponent<CoroutineRunner>();
                    }
                    return _instance;
                }
            }
        }
    }

    /// <summary>
    /// 自定义 DownloadHandler，按 SSE 协议解析 text/event-stream
    /// </summary>
    public class SseDownloadHandler : DownloadHandlerScript
    {
        private StringBuilder _sbData = new StringBuilder();
        private readonly ConcurrentQueue<string> _queue;

        public SseDownloadHandler(ConcurrentQueue<string> queue)
        {
            _queue = queue;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength == 0)
                return false;

            var chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            var lines = chunk.Split(new[] { "\n" }, StringSplitOptions.None);

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r');
                if (string.IsNullOrEmpty(line))
                {
                    if (_sbData.Length > 0)
                    {
                        _queue.Enqueue(_sbData.ToString());
                        _sbData.Length = 0;
                    }
                    continue;
                }

                if (line.StartsWith(":"))
                {
                    // 注释/心跳
                    continue;
                }

                int idx = line.IndexOf(':');
                string field, value;
                if (idx >= 0)
                {
                    field = line.Substring(0, idx).Trim();
                    value = line.Substring(idx + 1).TrimStart();
                }
                else
                {
                    field = "data";
                    value = line;
                }

                if (field == "data")
                {
                    if (_sbData.Length > 0) _sbData.Append('\n');
                    _sbData.Append(value);
                }
            }
            return true;
        }
    }
}
