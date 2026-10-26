using System;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>"New booster!": shows the booster over rays, explains it, and notes the free gift.</summary>
    public sealed class UnlockPopup : Popup
    {
        readonly Icon rays;
        readonly Icon icon;
        readonly Label name;
        readonly Label help;
        readonly Label gift;
        Action done;
        UiTween raysLoop;

        public UnlockPopup(UiContext ctx) : base(ctx, "New Booster!", "cj-ribbon--secondary", false)
        {
            var stage = Ui.El(Body, "cj-chest__stage");
            rays = Ui.Icon(stage, IconKind.Rays, "cj-chest__rays");
            rays.Tint = new Color(0.55f, 0.75f, 1f, 1f);
            var disc = Ui.El(stage, "cj-booster-disc cj-booster-disc--big");
            icon = Ui.Icon(disc, IconKind.Undo, "cj-booster-disc__icon");
            icon.Tint = IconPainter.Hex(0x4A90F0);
            name = Ui.Text(Body, "Undo", "cj-unlock__name cj-txt");
            help = Ui.Text(Body, "", "cj-popup__text");
            var giftRow = Ui.El(Body, "cj-unlock__gift");
            Ui.Icon(giftRow, IconKind.Gift, "cj-unlock__gift-icon");
            gift = Ui.Text(giftRow, "", "cj-unlock__gift-label");

            var ok = new CandyButton("Got it!", "cj-btn--primary cj-btn--big");
            ok.Clicked += () =>
            {
                var after = done;
                done = null;
                Close(after);
            };
            Body.Add(ok);
        }

        public void Show(BoosterType booster, Action doneAction)
        {
            done = doneAction;
            icon.Kind = UiText.BoosterIcon(booster);
            name.text = UiText.BoosterName(booster);
            help.text = UiText.BoosterHelp(booster);
            gift.text = $"You got {EconomyConfig.UnlockGift} for free!";
            OpenPopup();
            Anim.PopIn(icon.parent, 0.2f, 0.55f);
            UiTween.Delay(0.25f, UiFeedback.Reward);
            raysLoop?.Cancel();
            raysLoop = UiTween.Loop(t => Anim.SetRotate(rays, t * 20f));
        }

        protected override void OnClosing() => raysLoop?.Cancel();
    }
}
