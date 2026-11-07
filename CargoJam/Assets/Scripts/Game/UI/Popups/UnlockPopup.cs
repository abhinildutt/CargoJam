using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>"New booster": the booster disc pops in (a reward moment), explained, with the free gift noted.</summary>
    public sealed class UnlockPopup : Popup
    {
        readonly VisualElement disc;
        readonly Icon icon;
        readonly Label name;
        readonly Label help;
        readonly Label gift;
        Action done;

        public UnlockPopup(UiContext ctx) : base(ctx, "New booster", PopupKind.Card)
        {
            disc = Ui.El(Body, "cj-booster-disc cj-booster-disc--big");
            icon = Ui.Icon(disc, IconKind.Undo, "cj-booster-disc__icon");
            icon.Tint = UiPalette.Ink;
            name = Ui.Text(Body, "Undo", "cj-t-heading cj-c-ink cj-unlock__name");
            help = Ui.Text(Body, "", "cj-popup__text");
            var giftRow = Ui.El(Body, "cj-inset cj-unlock__gift");
            Ui.Icon(giftRow, IconKind.Gift, "cj-unlock__gift-icon");
            gift = Ui.Text(giftRow, "", "cj-t-label cj-c-ink");

            var actions = Ui.El(Body, "cj-popup__actions");
            var ok = new CandyButton("Got it", "cj-btn--primary cj-btn--l cj-btn--wide");
            ok.Clicked += () =>
            {
                var after = done;
                done = null;
                Close(after);
            };
            actions.Add(ok);
        }

        public void Show(BoosterType booster, Action doneAction)
        {
            done = doneAction;
            icon.Kind = UiText.BoosterIcon(booster);
            name.text = UiText.BoosterName(booster);
            help.text = UiText.BoosterHelp(booster);
            gift.text = EconomyConfig.UnlockGift == 1 ? "1 free to start" : $"{EconomyConfig.UnlockGift} free to start";
            OpenPopup();
            Anim.PopInReward(disc, 0.15f);
            UiTween.Delay(0.2f, UiFeedback.Reward);
        }
    }
}
