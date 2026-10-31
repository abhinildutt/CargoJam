using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// One 156-unit glass booster disc with a white icon plus a count badge, a price chip (none
    /// owned), a lock chip with "Lv N" (not unlocked yet), or a "Free" badge (the top streak tier's
    /// Undo). While the Crane is armed the disc turns light with an ink icon and a slow ring.
    /// </summary>
    public sealed class BoosterButton : VisualElement
    {
        readonly CandyButton button;
        readonly Icon icon;
        readonly VisualElement badge;
        readonly Label badgeLabel;
        readonly VisualElement priceTag;
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
            button = new CandyButton(null, "cj-btn--glass cj-btn--disc", UiText.BoosterIcon(booster));
            icon = button.Icon;
            icon.Tint = UiPalette.OnGlass;
            button.Clicked += () => Clicked?.Invoke(Booster);
            Add(button);

            badge = Ui.El(button, "cj-booster__badge");
            badgeLabel = Ui.Text(badge, "0", "cj-booster__badge-label");

            // Chips hang centered under the disc: a full-width row holds each pill.
            priceTag = Ui.El(button, "cj-booster__chip");
            var pricePill = Ui.El(priceTag, "cj-booster__pill");
            Ui.Icon(pricePill, IconKind.Coin, "cj-booster__chip-icon");
            Ui.Text(pricePill, Anim.Format(EconomyConfig.BoosterPrice(booster)), "cj-booster__chip-label");

            lockTag = Ui.El(button, "cj-booster__chip");
            var lockPill = Ui.El(lockTag, "cj-booster__pill");
            var lockIcon = Ui.Icon(lockPill, IconKind.Lock, "cj-booster__chip-icon");
            lockIcon.Tint = UiPalette.Ink2;
            Ui.Text(lockPill, "Lv " + EconomyConfig.UnlockLevel(booster), "cj-booster__chip-label");

            Ui.Show(ring, false);
        }

        /// <summary>Shakes the disc: "can't do that right now".</summary>
        public void Reject() => Anim.Shake(button, 12f, 0.3f);

        /// <param name="count">Usable charges (owned plus any free charge).</param>
        /// <param name="free">The count includes a free tier charge: show "Free" instead of the number.</param>
        public void Refresh(bool unlocked, int count, bool usable, bool armed, bool free = false)
        {
            EnableInClassList("cj-booster--locked", !unlocked);
            EnableInClassList("cj-booster--unusable", unlocked && !usable && !armed);
            EnableInClassList("cj-booster--free", unlocked && free);

            Ui.Show(lockTag, !unlocked);
            Ui.Show(badge, unlocked && count > 0);
            Ui.Show(priceTag, unlocked && count <= 0);
            badgeLabel.text = free ? "Free" : count.ToString();
            if (lastCount >= 0 && count > lastCount) Anim.PopInReward(badge, 0f, 0.36f);
            lastCount = count;

            SetArmed(armed);
        }

        void SetArmed(bool armed)
        {
            bool wasArmed = ringLoop != null;
            EnableInClassList("cj-booster--armed", armed);
            icon.Tint = armed ? UiPalette.Ink : UiPalette.OnGlass;
            if (armed == wasArmed) return;
            if (armed)
            {
                Ui.Show(ring, true);
                // A state indicator, not decoration: a slow ring widening and fading while armed.
                ringLoop = UiTween.Loop(time =>
                {
                    float k = (time / 1.4f) % 1f;
                    Anim.SetScale(ring, 1f + Easing.OutQuint(k) * 0.22f);
                    ring.style.opacity = 0.9f * (1f - k);
                });
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
