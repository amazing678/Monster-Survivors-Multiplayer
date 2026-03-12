using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 游戏启动时替换所有TMP_Text组件的字体为"MaoKenWangXingYuan-2"
/// </summary>
public class TMPFontReplacer : MonoBehaviour
{
    [Tooltip("目标字体（MaoKenWangXingYuan-2）")]
    public TMP_FontAsset targetFont;

    [Tooltip("替换间隔时间（秒）")]
    public float replaceInterval = 1f;

    [Tooltip("是否在启动时立即执行一次替换")]
    public bool replaceOnStart = true;

    // 存储原始字体用于交替替换
    private Dictionary<TMP_Text, TMP_FontAsset> originalFonts = new Dictionary<TMP_Text, TMP_FontAsset>();

    // 标记当前是否使用目标字体
    private bool isUsingTargetFont = false;

    private void Start()
    {
        // 检查是否指定了目标字体
        if (targetFont == null)
        {
            Debug.LogError("请在Inspector中指定目标字体MaoKenWangXingYuan-2");
            enabled = false; // 禁用脚本
            return;
        }

        // 收集所有文本组件及其原始字体
        CollectAllTextComponents();

        // 如果需要启动时立即替换
        if (replaceOnStart)
        {
            ReplaceAllFonts();
        }

        // 启动定时替换协程
        StartCoroutine(ReplaceFontsPeriodically());
    }

    /// <summary>
    /// 收集所有TMP_Text组件及其原始字体
    /// </summary>
    private void CollectAllTextComponents()
    {
        TMP_Text[] allTextComponents = FindObjectsOfType<TMP_Text>(true);

        foreach (var textComponent in allTextComponents)
        {
            // 只记录尚未添加的组件
            if (!originalFonts.ContainsKey(textComponent))
            {
                originalFonts.Add(textComponent, textComponent.font);
            }
        }

        Debug.Log($"已收集 {originalFonts.Count} 个TMP_Text组件");
    }

    /// <summary>
    /// 定时替换字体的协程
    /// </summary>
    private IEnumerator ReplaceFontsPeriodically()
    {
        while (true)
        {
            // 等待指定间隔时间
            yield return new WaitForSeconds(replaceInterval);

            // 替换字体
            ReplaceAllFonts();
        }
    }

    /// <summary>
    /// 替换所有文本组件的字体（交替替换）
    /// </summary>
    private void ReplaceAllFonts()
    {
        // 每调用一次切换一次字体状态
        isUsingTargetFont = !isUsingTargetFont;
        int replacedCount = 0;

        foreach (var kvp in originalFonts)
        {
            TMP_Text textComponent = kvp.Key;
            TMP_FontAsset originalFont = kvp.Value;

            // 根据当前状态设置字体
            textComponent.font = isUsingTargetFont ? targetFont : originalFont;
            replacedCount++;
        }

        Debug.Log($"{(isUsingTargetFont ? "替换为目标字体" : "恢复原始字体")}，共处理 {replacedCount} 个组件");
    }

    private void OnDestroy()
    {
        // 停止协程
        StopAllCoroutines();

        // 恢复原始字体（可选）
        if (isUsingTargetFont)
        {
            foreach (var kvp in originalFonts)
            {
                kvp.Key.font = kvp.Value;
            }
        }
    }
}