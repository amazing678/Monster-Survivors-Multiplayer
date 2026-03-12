using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using SimpleJSON;
using Main.Net; // 引用上面的命名空间

public class SseListenerMono : MonoBehaviour
{
    [Tooltip("SSE 服务地址")]
    public string sseUrl = "https://192.168.10.38/adhd/sseApi/sseGameConnect";

    private ServerSentEventsClient _client = new ServerSentEventsClient();
    private readonly List<string> _drainBuffer = new List<string>(32);

    public static bool Focused { get; private set; } = false;  // 默认false
    public static float attention { get; private set; } = 0f;  // 默认false
    public static float attentionThreshold { get; private set; } = 0f;  // 默认false

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
            Debug.Log($"[SSE][JSON] {PrettyJsonSafe(m)}");
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
    private string PrettyJsonSafe(string raw)
    {
        try
        {
            // 尝试粗略判断是否像 JSON
            string s = raw.Trim();
            if (!(s.StartsWith("{") && s.EndsWith("}")) && !(s.StartsWith("[") && s.EndsWith("]")))
                return raw;

            var sb = new StringBuilder();
            bool inString = false;
            int indent = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                if (c == '"' && (i == 0 || s[i - 1] != '\\'))
                {
                    inString = !inString;
                    sb.Append(c);
                    continue;
                }

                if (inString)
                {
                    sb.Append(c);
                    continue;
                }

                switch (c)
                {
                    case '{':
                    case '[':
                        sb.Append(c);
                        sb.Append('\n');
                        indent++;
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case '}':
                    case ']':
                        sb.Append('\n');
                        indent = Math.Max(0, indent - 1);
                        sb.Append(new string(' ', indent * 2));
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        sb.Append('\n');
                        sb.Append(new string(' ', indent * 2));
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(c))
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
        catch
        {
            return raw; // 回退
        }
    }


    private void HandleSseJson(string jsonStr)
    {
        UnityEngine.Debug.Log($"HandleSseJson:{jsonStr}");


        JSONNode root = JSONNode.Parse(jsonStr);
        if (root == null)
        {
            UnityEngine.Debug.LogWarning("[SSE] JSON 解析失败");
            return;
        }

        var type = root["type"].AsInt;
        var subType = root["subType"].AsInt;

        if (type == 3 && subType == 3024)
        {
            var attention_focus = root["content"]["attention"].AsFloat;
            var attentionThreshold_focus = root["content"]["attentionThreshold"].AsFloat;
            var attentionFlag = root["content"]["attentionFlag"].AsBool;

            UnityEngine.Debug.Log($"attentionFlag:{attentionFlag}");
            UnityEngine.Debug.Log($"attention:{attention_focus}");
            UnityEngine.Debug.Log($"attentionThreshold:{attentionThreshold_focus}");

            attention = attention_focus;
            attentionThreshold = attentionThreshold_focus;



            if (attentionFlag)
            {
                // 存到公有属性
                Focused = true;
            }
            else
            {
                Focused = false;
            }




        }
        else
        {
            UnityEngine.Debug.Log("Not True Value");
        }


    }

}
