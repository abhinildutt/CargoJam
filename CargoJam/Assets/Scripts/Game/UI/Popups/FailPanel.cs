using System;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// "Out of space!": how close the player was, a big "Continue +1 slot" for coins (greyed with
    /// "Not enough coins" when unaffordable) and a smaller "Give up" that warns about the streak.
    /// </summary>
    public sealed class FailPanel : Popup
    {
        readonly Label leftLabel;
        readonly CandyButton continueButton;
        readonly Label priceLabel;
        readonly Label poorLabel;
        readonly Label streakWarning;
        readonly CandyButton giveUp;
        readonly Icon slotIcon;
        UiTween pulse;

        /// <summary>The player wants to pay for one more slot.</summary>
        public event Action ContinueClicked;
        public event Action GiveUpClicked;

        public FailPanel(UiContext ctx) : base(ctx, "Out of space!", "cj-ribbon--danger", false)
        {
            var badge = Ui.El(Body, "cj-fail__badge");
            slotIcon = Ui.Icon(badge, IconKind.PlusSlot, "cj-fail__icon");
            leftLabel = Ui.Text(Body, "", "cj-fail__left cj-txt");

            continueButton = new CandyButton(null, "cj-btn--primary cj-btn--big cj-fail__continue");
            var text = Ui.El(continueButton.Content, "cj-fail__continue-text");
            Ui.Text(text, "Continue", "cj-btn__label cj-txt");
            Ui.Text(text, "+1 slot", "cj-fail__continue-sub cj-txt");
            var price = Ui.El(continueButton.Content, "cj-price");
            Ui.Icon(price, IconKind.Coin, "cj-price__coin");
            priceLabel = Ui.Text(price, "300", "cj-price__label cj-txt");
            continueButton.Clicked += () => ContinueClicked?.Invoke();
            Body.Add(continueButton);

            poorLabel = Ui.Text(Body, "Not enough coins", "cj-fail__poor");
            streakWarning = Ui.Text(Body, "", "cj-fail__warning");

            giveUp = new CandyButton("Give up", "cj-btn--danger cj-btn--small cj-fail__giveup");
            giveUp.Clicked += () => GiveUpClicked?.Invoke();
            Body.Add(giveUp);
        }

        /// <summary>Opens (or refreshes) the panel.</summary>
        /// <param name="parcelsLeft">Parcels still to deliver.</param>
        /// <param name="cost">Coin price of the next continue.</param>
        /// <param name="affordable">Whether the player has the coins.</param>
        /// <param name="canContinue">False when the dock can't grow any more.</param>
        /// <param name="streak">Current win streak (warned about when above zero).</param>
        public void Show(int parcelsLeft, int cost, bool affordable, bool canContinue, int streak)
        {
            leftLabel.text = parcelsLeft == 1 ? "Only 1 parcel left!" : $"Only {parcelsLeft} parcels left!";
            priceLabel.text = Anim.Format(cost);
            Ui.Show(continueButton, canContinue);
            continueButton.Greyed = !affordable;
            Ui.Show(poorLabel, canContinue && !affordable);
            streakWarning.text = streak > 0 ? UiText.StreakLoss(streak) : "";
            Ui.Show(streakWarning, streak > 0);

            bool wasOpen = IsOpen;
            OpenPopup();
            if (wasOpen) return;
            Anim.PopIn(slotIcon, 0.25f, 0.5f);
            Anim.PopIn(leftLabel, 0.35f);
            pulse?.Cancel();
            pulse = null;
            if (canContinue && affordable)
                UiTween.Delay(0.6f, () => { if (IsOpen) pulse = Anim.Pulse(continueButton.Content, 0.05f, 0.9f); });
        }

        /// <summary>"No" feedback on the Continue button (couldn't pay).</summary>
        public void RejectContinue()
        {
            continueButton.Greyed = true;
            Ui.Show(poorLabel, true);
            Anim.Shake(continueButton);
            Anim.Punch(poorLabel, 0.2f);
            UiFeedback.Nope();
        }

        protected override void OnClosing()
        {
            pulse?.Cancel();
            pulse = null;
        }
    }
}
