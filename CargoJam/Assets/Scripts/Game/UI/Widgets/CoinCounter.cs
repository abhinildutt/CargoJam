using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The quiet glass coin pill shown over every screen. The number rolls to new values; while
    /// frozen it holds still so a fly-in can tick it up coin by coin (squashing on each landing),
    /// then it catches up to the real balance.
    /// </summary>
    public sealed class CoinCounter : VisualElement
    {
        readonly Icon icon;
        readonly Label label;
        int shown;
        int target;
        int freezes;
        UiTween roll, squash;

        public CoinCounter()
        {
            AddToClassList("cj-coins");
            AddToClassList("cj-glass");
            pickingMode = PickingMode.Position;
            icon = Ui.Icon(this, IconKind.Coin, "cj-coins__icon");
            label = Ui.Text(this, "0", "cj-coins__label cj-on-glass");
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

        /// <summary>A flying coin hit the pill: squash it and bump the number by the coin's worth.</summary>
        public void Land(int worth)
        {
            AddShown(worth);
            squash?.Cancel();
            squash = Anim.Squash(this, 0.07f, 0.15f);
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
            else Anim.Punch(icon, 0.12f);
            roll = Anim.CountUp(label, from, value, 0.6f, 0f, "", v => shown = v);
        }
    }
}
