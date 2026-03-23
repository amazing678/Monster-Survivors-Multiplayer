using OctoberStudio.Extensions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OctoberStudio
{
    public class ExperienceUI : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;

        [SerializeField] RectMask2D rectMask;
        [SerializeField] TMP_Text levelText;

        [Tooltip("填 1 代表 P1，填 2 代表 P2")]
        public int playerId = 1;

        private void Update()
        {
            var player = (playerId == 1) ? PlayerBehavior.Player1 : PlayerBehavior.Player2;
            if (player == null) return;

            if (player.maxXp > 0)
            {
                // 调用原作者写好的完美遮罩切图法
                SetProgress((float)player.xp / player.maxXp);
            }
            SetLevelText(player.level);
        }

        public void SetProgress(float progress)
        {
            Vector4 padding = rectMask.padding;
            padding.z = rectMask.rectTransform.rect.width * (1 - progress);
            rectMask.padding = padding;
        }

        public void SetLevelText(int levelNumber)
        {
            levelText.text = $"等级 {levelNumber}";
        }
    }
}
