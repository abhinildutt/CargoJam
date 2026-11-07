using System;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// "Out of space" as a bottom sheet (at most 45% of the screen) so the stuck board stays in view:
    /// "N parcels to go", Continue +1 slot · price (greyed with "Not enough coins" when unaffordable),
    /// Try again (gives the level up and replays it), and one neutral line when giving up would drop
    /// the streak tier. No timers, no guilt copy.
    /// </summary>
    public sealed class FailPanel : Popup
    {
        readonly Label leftLabel;
        readonly CandyButton continueButton;
        readonly Label priceLabel;
        readonly Label poorLabel;
        readonly CandyButton tryAgain;
        readonly Label streakLine;

        /// <summary>The player wants to pay for one more slot.</summary>
        public event Action ContinueClicked;
        /// <summary>The player gives the level up and starts it again.</summary>
        public event Action TryAgainClicked;

        public FailPanel(UiContext ctx) : base(ctx, "Out of space", PopupKind.Sheet)
        {
            leftLabel = Ui.Text(Body, "", "cj-t-body cj-c-ink2 cj-t-center cj-fail__left");

            var actions = Ui.El(Body, "cj-popup__actions");
            continueButton = new CandyButton("Continue +1 slot", "cj-btn--primary cj-btn--l cj-btn--wide cj-fail__continue");
            continueButton.Label.AddToClassList("cj-fail__continue-label");
            var price = Ui.El(continueButton.Content, "cj-price");
            Ui.Icon(price, IconKind.Coin, "cj-price__coin");
            priceLabel = Ui.Text(price, "300", "cj-price__label");
            continueButton.Clicked += () => ContinueClicked?.Invoke();
            actions.Add(continueButton);
            poorLabel = Ui.Text(actions, "Not enough coins", "cj-popup__note cj-fail__poor");

            tryAgain = new CandyButton("Try again", "cj-btn--quiet cj-btn--m cj-btn--wide cj-fail__retry");
            tryAgain.Clicked += () => TryAgainClicked?.Invoke();
            actions.Add(tryAgain);

            streakLine = Ui.Text(Body, "", "cj-popup__note");
        }

        /// <summary>Opens (or refreshes) the sheet.</summary>
        /// <param name="parcelsLeft">Parcels still to deliver.</param>
        /// <param name="cost">Coin price of the next continue.</param>
        /// <param name="affordable">Whether the player has the coins.</param>
        /// <param name="canContinue">False when another slot wouldn't help (dock can't grow, padlocks).</param>
        /// <param name="streakWarning">"Streak will drop to ×1.5" when giving up drops a tier, else null.</param>
        public void Show(int parcelsLeft, int cost, bool affordable, bool canContinue, string streakWarning)
        {
            leftLabel.text = UiText.ParcelsToGo(parcelsLeft);
            priceLabel.text = Anim.Format(cost);
            Ui.Show(continueButton, canContinue);
            continueButton.Greyed = !affordable;
            Ui.Show(poorLabel, canContinue && !affordable);
            // Without a continue, Try again is the one action and takes the primary look.
            tryAgain.EnableInClassList("cj-btn--quiet", canContinue);
            tryAgain.EnableInClassList("cj-btn--primary", !canContinue);
            streakLine.text = streakWarning ?? "";
            Ui.Show(streakLine, !string.IsNullOrEmpty(streakWarning));
            OpenPopup();
        }

        /// <summary>"No" feedback on the Continue button (couldn't pay).</summary>
        public void RejectContinue()
        {
            continueButton.Greyed = true;
            Ui.Show(poorLabel, true);
            Anim.Shake(continueButton);
            UiFeedback.Nope();
        }
    }
}
