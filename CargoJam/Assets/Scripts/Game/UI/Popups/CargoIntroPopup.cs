using System;
using CargoJam.Core;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// "New cargo!": the first time a cargo kind shows up the card names it, draws it on a disc and
    /// says in one line what it does (design v4, section 5). Modeled on <see cref="UnlockPopup"/>.
    /// </summary>
    public sealed class CargoIntroPopup : Popup
    {
        readonly VisualElement disc;
        readonly Icon icon;
        readonly Label name;
        readonly Label help;
        Action done;

        public CargoIntroPopup(UiContext ctx) : base(ctx, "New cargo!", PopupKind.Card)
        {
            disc = Ui.El(Body, "cj-booster-disc cj-booster-disc--big");
            icon = Ui.Icon(disc, IconKind.Bollard, "cj-booster-disc__icon");
            icon.Tint = UiPalette.Ink;
            name = Ui.Text(Body, "", "cj-t-heading cj-c-ink cj-unlock__name");
            help = Ui.Text(Body, "", "cj-popup__text");

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

        /// <summary>Opens the card for a cargo kind; <paramref name="doneAction"/> runs once "Got it" has closed it.</summary>
        public void Show(CargoKind kind, Action doneAction)
        {
            done = doneAction;
            icon.Kind = UiText.CargoIcon(kind);
            name.text = UiText.CargoName(kind);
            help.text = UiText.CargoHelp(kind);
            OpenPopup();
            Anim.PopInReward(disc, 0.15f);
            UiTween.Delay(0.2f, UiFeedback.Reward);
        }
    }
}
