using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // 如果你使用的是 TextMeshPr

public class DualBrainUI : MonoBehaviour
{
    [Tooltip("绑定1代表P1，绑定2代表P2")]
    public int playerId = 1;

    public TextMeshProUGUI attentionText;
    // public UnityEngine.UI.Text attentionText; // 如果使用的是旧版UGUI Text

    void Update()
    {
        if (attentionText == null) return;

        if (playerId == 1)
        {
            attentionText.text = $"P1 专注度: {SseListenerMono.P1_Attention}";
            attentionText.color = SseListenerMono.P1_Focused ? Color.green : Color.white;
        }
        else if (playerId == 2)
        {
            attentionText.text = $"P2 专注度: {SseListenerMono.P2_Attention}";
            attentionText.color = SseListenerMono.P2_Focused ? Color.green : Color.white;
        }
    }
}
