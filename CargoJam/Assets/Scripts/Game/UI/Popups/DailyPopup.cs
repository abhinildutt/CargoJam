using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The 7-day reward calendar: claimed days ticked, today's highlighted and pulsing, day 7 a
    /// chest. Claim grants it and flies the coins (or pops the booster) out of today's tile.
    /// </summary>
    public sealed class DailyPopup : Popup
    {
        const int Days = 7;

        readonly VisualElement[] cells = new VisualElement[Days];
        readonly VisualElement[] checks = new VisualElement[Days];
        readonly Icon[] icons = new Icon[Days];
        readonly Label streakLabel;
        readonly CandyButton claim;
        readonly Label comeBack;
        int today;
        UiTween todayPulse;
        Action closed;

        public DailyPopup(UiContext ctx) : base(ctx, "Daily Reward", "cj-ribbon--coin", true)
        {
            streakLabel = Ui.Text(Body, "", "cj-daily__streak");
            var grid = Ui.El(Body, "cj-daily__grid");
            for (int d = 0; d < Days; d++)
            {
                var reward = EconomyConfig.Daily(d);
                var cell = Ui.El(grid, d == Days - 1 ? "cj-daily__cell cj-daily__cell--chest" : "cj-daily__cell");
                Ui.Text(cell, $"Day {d + 1}", "cj-daily__day cj-txt");
                IconKind kind = reward.IsChest ? IconKind.Chest
                    : reward.Booster.HasValue ? UiText.BoosterIcon(reward.Booster.Value)
                    : IconKind.Coin;
                icons[d] = Ui.Icon(cell, kind, "cj-daily__icon");
                icons[d].Tint = IconPainter.Hex(0x4A90F0);
                string amount = reward.IsChest ? "Chest" : reward.Booster.HasValue ? "x1" : Anim.Format(reward.Coins);
                Ui.Text(cell, amount, "cj-daily__amount cj-txt");
                var check = Ui.El(cell, "cj-daily__check");
                Ui.Icon(check, IconKind.Check, "cj-daily__check-icon");
                cells[d] = cell;
                checks[d] = check;
            }

            claim = new CandyButton("Claim", "cj-btn--primary cj-btn--big");
            claim.Clicked += OnClaim;
            Body.Add(claim);
            comeBack = Ui.Text(Body, "Come back tomorrow for more!", "cj-popup__text");
        }

        /// <summary>Opens the calendar for today's status; <paramref name="closedAction"/> runs after closing.</summary>
        public void Show(DailyRewardInfo info, Action closedAction)
        {
            closed = closedAction;
            bool available = info != null && info.Available;
            today = Math.Max(0, Math.Min(Days - 1, info?.Day ?? 0));
            int streak = info?.LoginStreak ?? 0;
            streakLabel.text = streak > 1 ? $"Login streak: {streak} days" : "Come back every day!";

            for (int d = 0; d < Days; d++)
            {
                bool claimed = d < today;
                cells[d].EnableInClassList("cj-daily__cell--claimed", claimed);
                cells[d].EnableInClassList("cj-daily__cell--today", d == today && available);
                Ui.Show(checks[d], claimed);
            }
            Ui.Show(claim, available);
            Anim.SetOpacity(claim, 1f);
            claim.Interactable = available;
            Ui.Show(comeBack, !available);

            OpenPopup();
            StopPulse();
            todayPulse = available ? Anim.Pulse(cells[today], 0.06f, 0.9f) : null;
        }

        void OnClaim()
        {
            claim.Interactable = false;
            Ctx.Coins.Freeze();
            var result = Ctx.Progression.ClaimDaily();
            if (result == null)
            {
                Ctx.Coins.Unfreeze();
                Anim.Shake(claim);
                UiFeedback.Nope();
                return;
            }

            StopPulse();
            var cell = cells[today];
            cell.EnableInClassList("cj-daily__cell--today", false);
            cell.EnableInClassList("cj-daily__cell--claimed", true);
            Ui.Show(checks[today], true);
            Anim.PopIn(checks[today], 0.05f, 0.4f);
            Anim.Punch(icons[today], 0.4f, 0.35f);
            var center = cell.worldBound.center;
            Ctx.Fly.Burst(center, 18, 200f);
            UiFeedback.Reward();
            Anim.Fade(claim, 1f, 0f, 0.25f, 0f, () => Ui.Show(claim, false));

            int coins = result.Reward.Coins;
            if (coins > 0) Ctx.Fly.Coins(center, Ctx.Coins, coins, 10, 0.25f, Ctx.Coins.Unfreeze);
            else Ctx.Coins.Unfreeze();

            UiTween.Delay(1.6f, () => Close(TakeClosed()));
        }

        protected override void OnCloseButton() => Close(TakeClosed());

        protected override void OnClosing() => StopPulse();

        void StopPulse()
        {
            todayPulse?.Cancel();
            todayPulse = null;
            Anim.SetScale(cells[today], 1f);
        }

        Action TakeClosed()
        {
            var c = closed;
            closed = null;
            return c;
        }
    }
}
