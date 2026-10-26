using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// One round booster button: its icon plus a count badge, a price tag (none owned), or a lock
    /// with "Lv N" (not unlocked yet). The Crane also shows an armed state while waiting for a tap.
    /// </summary>
    public sealed class BoosterButton : VisualElement
    {
        readonly CandyButton button;
        readonly Icon icon;
        readonly VisualElement badge;
        readonly Label badgeLabel;
        readonly VisualElement priceTag;
        readonly Label priceLabel;
        readonly VisualElement lockTag;
        readonly VisualElement ring;
        UiTween ringLoop;
        int lastCount = -1;

        public BoosterType Booster { get; }

        /// <summary>Fired on tap with this button's booster.</summary>
        public event Action<BoosterType> Clicked;

        public BoosterButton(BoosterType booster)
        {
            Booster = booster;
            AddToClassList("cj-booster");
            pickingMode = PickingMode.Ignore;

            ring = Ui.El(this, "cj-booster__ring");
            button = new CandyButton(null, "cj-btn--white cj-btn--booster", UiText.BoosterIcon(booster));
            icon = button.Icon;
            icon.Tint = IconPainter.Hex(0x4A90F0);
            button.Clicked += () => Clicked?.Invoke(Booster);
            Add(button);

            badge = Ui.El(button, "cj-booster__badge");
            badgeLabel = Ui.Text(badge, "0", "cj-booster__badge-label cj-txt");

            priceTag = Ui.El(button, "cj-booster__price");
            Ui.Icon(priceTag, IconKind.Coin, "cj-booster__price-coin");
            priceLabel = Ui.Text(priceTag, EconomyConfig.BoosterPrice(booster).ToString(), "cj-booster__price-label cj-txt");

            lockTag = Ui.El(button, "cj-booster__lock");
            Ui.Text(lockTag, "Lv " + EconomyConfig.UnlockLevel(booster), "cj-booster__lock-label cj-txt");

            Ui.Show(ring, false);
        }

        /// <summary>Shakes the button: "can't do that right now".</summary>
        public void Reject() => Anim.Shake(button, 14f, 0.35f);

        public void Refresh(bool unlocked, int count, bool usable, bool armed)
        {
            button.Greyed = !unlocked;
            icon.Kind = unlocked ? UiText.BoosterIcon(Booster) : IconKind.Lock;
            icon.Tint = unlocked ? IconPainter.Hex(0x4A90F0) : IconPainter.Hex(0x8F877C);
            EnableInClassList("cj-booster--unusable", unlocked && !usable && !armed);

            Ui.Show(lockTag, !unlocked);
            Ui.Show(badge, unlocked && count > 0);
            Ui.Show(priceTag, unlocked && count <= 0);
            badgeLabel.text = count.ToString();
            if (lastCount >= 0 && count != lastCount && count > 0) Anim.Punch(badge, 0.35f);
            lastCount = count;

            SetArmed(armed);
        }

        void SetArmed(bool armed)
        {
            bool wasArmed = ringLoop != null;
            EnableInClassList("cj-booster--armed", armed);
            if (armed == wasArmed) return;
            if (armed)
            {
                Ui.Show(ring, true);
                ringLoop = UiTween.Loop(time =>
                {
                    float k = (time * 1.6f) % 1f;
                    Anim.SetScale(ring, 1f + k * 0.35f);
                    ring.style.opacity = 1f - k;
                });
                Anim.Punch(button, 0.15f);
            }
            else
            {
                ringLoop.Cancel();
                ringLoop = null;
                Ui.Show(ring, false);
            }
        }
    }
}
