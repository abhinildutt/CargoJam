using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>"Buy Undo for 120?" with the booster explained, or "Not enough coins".</summary>
    public sealed class BuyBoosterPopup : Popup
    {
        readonly Icon icon;
        readonly Label help;
        readonly CandyButton buy;
        readonly Label priceLabel;
        readonly Label poor;
        BoosterType booster;
        Func<bool> tryBuy;
        Action bought;

        public BuyBoosterPopup(UiContext ctx) : base(ctx, "Buy Undo?", "cj-ribbon--secondary", true)
        {
            var disc = Ui.El(Body, "cj-booster-disc");
            icon = Ui.Icon(disc, IconKind.Undo, "cj-booster-disc__icon");
            icon.Tint = IconPainter.Hex(0x4A90F0);
            help = Ui.Text(Body, "", "cj-popup__text");

            buy = new CandyButton("Buy", "cj-btn--primary cj-btn--big");
            var price = Ui.El(buy.Content, "cj-price");
            Ui.Icon(price, IconKind.Coin, "cj-price__coin");
            priceLabel = Ui.Text(price, "120", "cj-price__label cj-txt");
            buy.Clicked += OnBuy;
            Body.Add(buy);
            poor = Ui.Text(Body, "Not enough coins", "cj-fail__poor");
        }

        /// <param name="buyAction">Spends the coins; false when it couldn't.</param>
        /// <param name="boughtAction">Runs after a successful purchase, once the popup has closed.</param>
        public void Show(BoosterType type, bool affordable, Func<bool> buyAction, Action boughtAction)
        {
            booster = type;
            tryBuy = buyAction;
            bought = boughtAction;
            string name = UiText.BoosterName(type);
            Title = affordable ? $"Buy {name}?" : "Not enough coins";
            icon.Kind = UiText.BoosterIcon(type);
            help.text = UiText.BoosterHelp(type);
            priceLabel.text = Anim.Format(EconomyConfig.BoosterPrice(type));
            buy.Greyed = !affordable;
            Ui.Show(poor, !affordable);
            OpenPopup();
            Anim.PopIn(icon, 0.2f, 0.45f);
        }

        void OnBuy()
        {
            if (buy.Greyed || tryBuy == null || !tryBuy())
            {
                buy.Greyed = true;
                Ui.Show(poor, true);
                Anim.Shake(buy);
                UiFeedback.Nope();
                return;
            }
            UiFeedback.Reward();
            var after = bought;
            bought = null;
            tryBuy = null;
            Close(after);
        }
    }
}
