using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReceiveBrainSignal : MonoBehaviour
{
    public static bool Focused { get; private set; } = false;  // 默认false

    public static float attention { get; private set; } = 0f;  // 默认false
    public static float attentionThreshold { get; private set; } = 0f;  // 默认false

    [Serializable]
    public class Signal
    {
        public float attention;
        public float attentionThreshold;
        public bool attentionFlag;
    }

    // JS 通过 SendMessage 调这个方法，参数只能是 string 或 float（推荐 string）
    public void ReceiveSignal(string json)
    {
        Debug.Log("[Unity] OnJsMessage raw: " + json);

        try
        {
            var data = JsonUtility.FromJson<Signal>(json);
            if (data != null)
            {
                Debug.Log($"[Unity] attention={data.attention}, attentionThreshold={data.attentionThreshold}, attentionFlag={data.attentionFlag}");
                // TODO: 根据 data.type 分发处理
                attention = data.attention;
                attentionThreshold = data.attentionThreshold;
                Focused = data.attentionFlag;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[Unity] JSON parse error: " + e);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
