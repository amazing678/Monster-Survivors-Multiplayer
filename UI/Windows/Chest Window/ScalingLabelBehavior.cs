using OctoberStudio.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OctoberStudio.UI
{
    public class ScalingLabelBehavior : MonoBehaviour
    {
        [Tooltip("填 1 代表 P1，填 2 代表 P2")]
        public int playerId = 1;
        [Tooltip("如果这个UI是用来显示击杀数的，请勾选这个开关")]
        public bool isKillCounter = false;

        [SerializeField] protected TMP_Text label;
        [SerializeField] protected Image icon;
        [SerializeField] AligmentType aligment;

        private float spacing;
        private int lastValue = -1;

        private void Awake()
        {
            spacing = label.rectTransform.anchoredPosition.x - label.rectTransform.sizeDelta.x / 2 - icon.rectTransform.anchoredPosition.x - icon.rectTransform.sizeDelta.x / 2;
        }

        private void Update()
        {
            // 如果是击杀数 UI，就在这里读取对应玩家的击杀数据
            if (isKillCounter)
            {
                var player = (playerId == 1) ? PlayerBehavior.Player1 : PlayerBehavior.Player2;
                if (player != null && player.kills != lastValue)
                {
                    lastValue = player.kills;
                    SetAmount(lastValue);
                    transform.localScale = Vector3.one * 1.3f; // 击杀跳动动画
                }
            }

            // 平滑恢复大小动画
            if (transform.localScale.x > 1.01f)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, Time.unscaledDeltaTime * 10f);
            }
        }

        public void SetAmount(int amount)
        {
            label.text = amount.ToString();
            RecalculatePositions();
        }

        private void RecalculatePositions()
        {
            label.SetSizeDeltaX(label.preferredWidth);

            var iconWidth = icon.rectTransform.sizeDelta.x;
            var textWidth = label.rectTransform.sizeDelta.x;
            var width = iconWidth + spacing + textWidth;

            switch (aligment)
            {
                case AligmentType.Center:

                    icon.SetAnchoredPositionX(-width / 2f + iconWidth / 2f);
                    label.SetAnchoredPositionX(width / 2f - textWidth / 2f);
                    break;

                case AligmentType.Left:

                    icon.SetAnchoredPositionX(iconWidth / 2f);
                    label.SetAnchoredPositionX(iconWidth + spacing + textWidth / 2f);

                    break;

                case AligmentType.Right:

                    icon.SetAnchoredPositionX(-textWidth - spacing - iconWidth / 2f);
                    label.SetAnchoredPositionX(-textWidth / 2f);

                    break;
            }
        }

        public enum AligmentType
        {
            Left, Center, Right
        }
    }
}
