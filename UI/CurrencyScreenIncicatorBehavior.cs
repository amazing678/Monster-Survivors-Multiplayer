using OctoberStudio.UI;
using UnityEngine;

namespace OctoberStudio.Currency
{
    public class CurrencyScreenIncicatorBehavior : ScalingLabelBehavior
    {
        [Tooltip("The unique identificator of the currency that is attached to this ui. There must be an entry with the same id in the Currencies Database")]
        [SerializeField] string currencyID;

        public CurrencySave Currency { get; private set; }
        private int lastGold = -1;
        private void Start()
        {
            //Currency = GameController.SaveManager.GetSave<CurrencySave>(currencyID);

            //SetAmount(Currency.Amount);

            icon.sprite = GameController.CurrenciesManager.GetIcon(currencyID);

            //Currency.onGoldAmountChanged += SetAmount;
        }

        //private void OnDestroy()
        //{
        //    Currency.onGoldAmountChanged -= SetAmount;
        //}
        private void Update()
        {
            // 实时读取对应玩家的个人钱包数据
            var player = (playerId == 1) ? PlayerBehavior.Player1 : PlayerBehavior.Player2;
            if (player != null && player.gold != lastGold)
            {
                lastGold = player.gold;
                SetAmount(lastGold);
                transform.localScale = Vector3.one * 1.3f; // 吃金币时的跳动动画
            }
        }
    }
}
