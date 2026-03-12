using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

// 单个设备数据结构（与JSON字段一一对应）
[Serializable]
public class DeviceData
{
    public string addr;        // 蓝牙MAC地址
    public int attention;      // 专注度
    public int meditation;     // 放松度
    public int signal;         // 信号质量
    public string time;        // 更新时间
}

// JSON数组解析辅助类（解决Unity JsonUtility不支持直接解析数组的问题）
[Serializable]
public class DeviceDataList
{
    public List<DeviceData> devices;
}

public class JsonDataReceiver : MonoBehaviour
{
    // 目标服务器配置
    private const string TargetIp = "192.168.10.41";
    private const int TargetPort = 8888;

    // TCP通信核心对象
    private TcpClient _tcpClient;
    private Thread _receiveThread;
    private bool _isListening = false;

    // 新增：TCP粘包处理缓冲区（拼接分片的JSON数据）
    private readonly StringBuilder _jsonBuffer = new StringBuilder();

    void Start()
    {
        StartReceiving();
    }

    /// <summary>
    /// 启动接收线程
    /// </summary>
    private void StartReceiving()
    {
        if (_isListening) return;

        _isListening = true;
        _receiveThread = new Thread(ReceiveDataLoop);
        _receiveThread.IsBackground = true;
        _receiveThread.Start();
        Debug.Log("开始尝试连接数据服务器...");
    }

    /// <summary>
    /// 数据接收循环（子线程）- 优化粘包/拆包+数组解析
    /// </summary>
    private void ReceiveDataLoop()
    {
        try
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(TargetIp, TargetPort);
            Debug.Log($"成功连接到 {TargetIp}:{TargetPort}");

            var stream = _tcpClient.GetStream();
            byte[] buffer = new byte[4096];

            while (_isListening && _tcpClient.Connected)
            {
                if (stream.DataAvailable)
                {
                    int readLength = stream.Read(buffer, 0, buffer.Length);
                    if (readLength > 0)
                    {
                        // 1. 拼接接收到的JSON片段（解决TCP拆包）
                        string receivedStr = Encoding.UTF8.GetString(buffer, 0, readLength);
                        _jsonBuffer.Append(receivedStr);

                        // 2. 检查是否接收到完整的JSON数组（简单判断：以]结尾，适配你的数据格式）
                        string fullJson = _jsonBuffer.ToString();
                        if (fullJson.Trim().EndsWith("]"))
                        {
                            // 3. 输出原始JSON数据
                            Debug.Log($"接收到完整JSON数据：\n{fullJson}");

                            // 4. 解析JSON数组并逐行输出设备数据
                            ParseAndLogDeviceData(fullJson);

                            // 5. 清空缓冲区，准备接收下一次数据
                            _jsonBuffer.Clear();
                        }
                    }
                }
                else
                {
                    Thread.Sleep(10); // 降低CPU占用
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"接收数据异常：{ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            StopReceiving();
        }
    }

    /// <summary>
    /// 解析JSON数组并打印每个设备的数据（兼容1组/多组）
    /// </summary>
    /// <param name="jsonStr">完整的JSON数组字符串</param>
    private void ParseAndLogDeviceData(string jsonStr)
    {
        try
        {
            // 处理Unity JsonUtility不支持直接解析数组的问题：包装成对象
            string wrappedJson = $"{{\"devices\": {jsonStr}}}";
            DeviceDataList dataList = JsonUtility.FromJson<DeviceDataList>(wrappedJson);

            if (dataList?.devices == null || dataList.devices.Count == 0)
            {
                Debug.LogWarning("解析出空的设备数据列表");
                return;
            }

            // 遍历所有设备数据，逐个输出
            Debug.Log($"共解析到 {dataList.devices.Count} 组设备数据：");
            for (int i = 0; i < dataList.devices.Count; i++)
            {
                DeviceData device = dataList.devices[i];
                Debug.Log($"├─ 设备{i + 1}：" +
                          $"MAC={device.addr} | " +
                          $"专注度={device.attention} | " +
                          $"放松度={device.meditation} | " +
                          $"信号={device.signal} | " +
                          $"时间={device.time}");
            }
            Debug.Log("└─ 数据解析完成\n");
        }
        catch (Exception ex)
        {
            Debug.LogError($"解析JSON数据失败：{ex.Message}\n原始JSON：{jsonStr}");
        }
    }

    /// <summary>
    /// 停止接收并释放资源
    /// </summary>
    private void StopReceiving()
    {
        _isListening = false;
        _jsonBuffer.Clear(); // 清空缓冲区

        if (_tcpClient != null)
        {
            if (_tcpClient.Connected)
            {
                try
                {
                    _tcpClient.GetStream().Close();
                }
                catch { }
                _tcpClient.Close();
            }
            _tcpClient = null;
            Debug.Log("TCP连接已关闭");
        }

        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            _receiveThread.Join(1000);
            if (_receiveThread.IsAlive) _receiveThread.Abort();
            _receiveThread = null;
        }
    }

    // Unity生命周期：销毁/暂停/退出时清理资源
    void OnDestroy() => StopReceiving();
    void OnApplicationPause(bool pause)
    {
        if (pause) StopReceiving();
        else if (!_isListening) StartReceiving();
    }
    void OnApplicationQuit() => StopReceiving();
}