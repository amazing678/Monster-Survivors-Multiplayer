using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SimpleJSON;
using Main.Net; // 引用上面的命名空间

public class SseListenerMono : MonoBehaviour
{
    public static SseListenerMono Instance { get; private set; }

    [Tooltip("SSE 服务地址")]
    public string sseUrl = "http://192.168.10.41:8888";

    [Header("双人设备 MAC 地址绑定")]
    public string player1Mac = "C8:3F:50:D0:EF:48";
    public string player2Mac = "F6:9D:8F:CF:77:30";
    public float attentionThreshold = 50f; // 触发自动移动的阈值

    private ServerSentEventsClient _client = new ServerSentEventsClient();
    private readonly List<string> _drainBuffer = new List<string>(32);

    // 玩家 1 的状态
    public static float P1_Attention { get; private set; } = 0f;
    public static bool P1_Focused { get; private set; } = false;

    // 玩家 2 的状态
    public static float P2_Attention { get; private set; } = 0f;
    public static bool P2_Focused { get; private set; } = false;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Debug.Log("[SSE] Connecting: " + sseUrl);
        try
        {
            _client.Open(sseUrl);
        }
        catch (Exception e)
        {
            Debug.LogError("[SSE] Open failed: " + e);
        }
    }

    void Update()
    {
        // 在主线程把消息队列清空并打印
        _drainBuffer.Clear();
        while (_client.MessageQueue.TryDequeue(out var msg))
        {
            _drainBuffer.Add(msg);
        }

        foreach (var m in _drainBuffer)
        {
            //Debug.Log($"[SSE][JSON] {PrettyJsonSafe(m)}");
            HandleSseJson(m);
        }
    }

    void OnDestroy()
    {
        _client.Close();
    }

    /// <summary>
    /// 简单 JSON 美化（不做严格解析，遇到非法 JSON 会回退原串）
    /// 无第三方库情况下的轻量方案
    /// </summary>
    //private string PrettyJsonSafe(string raw)
    //{
    //    try
    //    {
    //        // 尝试粗略判断是否像 JSON
    //        string s = raw.Trim();
    //        if (!(s.StartsWith("{") && s.EndsWith("}")) && !(s.StartsWith("[") && s.EndsWith("]")))
    //            return raw;

    //        var sb = new StringBuilder();
    //        bool inString = false;
    //        int indent = 0;

    //        for (int i = 0; i < s.Length; i++)
    //        {
    //            char c = s[i];

    //            if (c == '"' && (i == 0 || s[i - 1] != '\\'))
    //            {
    //                inString = !inString;
    //                sb.Append(c);
    //                continue;
    //            }

    //            if (inString)
    //            {
    //                sb.Append(c);
    //                continue;
    //            }

    //            switch (c)
    //            {
    //                case '{':
    //                case '[':
    //                    sb.Append(c);
    //                    sb.Append('\n');
    //                    indent++;
    //                    sb.Append(new string(' ', indent * 2));
    //                    break;
    //                case '}':
    //                case ']':
    //                    sb.Append('\n');
    //                    indent = Math.Max(0, indent - 1);
    //                    sb.Append(new string(' ', indent * 2));
    //                    sb.Append(c);
    //                    break;
    //                case ',':
    //                    sb.Append(c);
    //                    sb.Append('\n');
    //                    sb.Append(new string(' ', indent * 2));
    //                    break;
    //                case ':':
    //                    sb.Append(": ");
    //                    break;
    //                default:
    //                    if (!char.IsWhiteSpace(c))
    //                        sb.Append(c);
    //                    break;
    //            }
    //        }
    //        return sb.ToString();
    //    }
    //    catch
    //    {
    //        return raw; // 回退
    //    }
    //}


    private void HandleSseJson(string jsonStr)
    {
        JSONNode root = JSONNode.Parse(jsonStr);
        if (root == null || !root.IsArray) return;

        foreach (JSONNode node in root.AsArray)
        {
            string addr = node["addr"].Value;
            float att = node["attention"].AsFloat;
            bool isFocused = att >= attentionThreshold;

            if (addr == player1Mac)
            {
                P1_Attention = att;
                P1_Focused = isFocused;
            }
            else if (addr == player2Mac)
            {
                P2_Attention = att;
                P2_Focused = isFocused;
            }
        }
    }

}
