using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>"Buy Undo?" with the booster explained and a price, or "Not enough coins".</summary>
    public sealed class BuyBoosterPopup : Popup
    {
        readonly Icon icon;
        readonly Label help;
        readonly CandyButton buy;
        readonly Label priceLabel;
        readonly Label poor;
        Func<bool> tryBuy;
        Action bought;

        public BuyBoosterPopup(UiContext ctx) : base(ctx, "Buy Undo?", PopupKind.Card, true)
        {
            var disc = Ui.El(Body, "cj-booster-disc");
            icon = Ui.Icon(disc, IconKind.Undo, "cj-booster-disc__icon");
            icon.Tint = UiPalette.Ink;
            help = Ui.Text(Body, "", "cj-popup__text");

            var actions = Ui.El(Body, "cj-popup__actions");
            buy = new CandyButton("Buy", "cj-btn--primary cj-btn--l cj-btn--wide");
            var price = Ui.El(buy.Content, "cj-price");
            Ui.Icon(price, IconKind.Coin, "cj-price__coin");
            priceLabel = Ui.Text(price, "120", "cj-price__label");
            buy.Clicked += OnBuy;
            actions.Add(buy);
            poor = Ui.Text(actions, "Not enough coins", "cj-popup__note");
        }

        /// <param name="buyAction">Spends the coins; false when it couldn't.</param>
        /// <param name="boughtAction">Runs after a successful purchase, once the popup has closed.</param>
        public void Show(BoosterType type, bool affordable, Func<bool> buyAction, Action boughtAction)
        {
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
