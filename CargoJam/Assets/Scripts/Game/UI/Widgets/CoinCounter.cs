using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The coin pill shown over every screen. The number rolls to new values; while frozen it holds
    /// still so a fly-in can tick it up coin by coin, then it catches up to the real balance.
    /// </summary>
    public sealed class CoinCounter : VisualElement
    {
        readonly Icon icon;
        readonly Label label;
        int shown;
        int target;
        int freezes;
        UiTween roll;

        public CoinCounter()
        {
            AddToClassList("cj-coins");
            pickingMode = PickingMode.Position;
            icon = Ui.Icon(this, IconKind.Coin, "cj-coins__icon");
            label = Ui.Text(this, "0", "cj-coins__label cj-txt");
        }

        /// <summary>Where flying coins land.</summary>
        public VisualElement Target => icon;

        public int Shown => shown;

        /// <summary>Sets the real balance; rolls to it unless frozen.</summary>
        public void SetValue(int value, bool animate = true)
        {
            target = value;
            if (freezes > 0) return;
            RollTo(value, animate);
        }

        /// <summary>Holds the displayed number until <see cref="Unfreeze"/> (nestable).</summary>
        public void Freeze() => freezes++;

        public void Unfreeze()
        {
            if (freezes == 0) return;
            freezes--;
            if (freezes == 0) RollTo(target, true);
        }

        /// <summary>
        /// Bumps the displayed number by a landed coin's worth. Ignored once unfrozen, because the
        /// counter is then already rolling to the real balance.
        /// </summary>
        public void AddShown(int delta)
        {
            if (freezes == 0) return;
            roll?.Cancel();
            shown += delta;
            label.text = Anim.Format(shown);
        }

        void RollTo(int value, bool animate)
        {
            roll?.Cancel();
            if (!animate || value == shown)
            {
                shown = value;
                label.text = Anim.Format(value);
                return;
            }
            int from = shown;
            if (value < from) Anim.Shake(this, 8f, 0.3f);
            else Anim.Punch(icon, 0.25f);
            roll = Anim.CountUp(label, from, value, 0.6f, 0f, "", v => shown = v);
        }
    }
}
