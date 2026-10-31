using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The 7-day reward calendar: claimed days ticked, today's outlined in gold, day 7 a chest.
    /// Claim grants it and flies the coins (or pops the booster) out of today's tile. It only opens
    /// when the player taps the Daily icon on Home; it never pops up on its own.
    /// </summary>
    public sealed class DailyPopup : Popup
    {
        const int Days = 7;

        readonly VisualElement[] cells = new VisualElement[Days];
        readonly VisualElement[] checks = new VisualElement[Days];
        readonly Icon[] icons = new Icon[Days];
        readonly Label streakLabel;
        readonly VisualElement actions;
        readonly CandyButton claim;
        readonly Label comeBack;
        int today;
        Action closed;

        public DailyPopup(UiContext ctx) : base(ctx, "Daily reward", PopupKind.Card, true)
        {
            streakLabel = Ui.Text(Body, "", "cj-t-label cj-c-ink2 cj-daily__streak");
            var grid = Ui.El(Body, "cj-daily__grid");
            for (int d = 0; d < Days; d++)
            {
                var reward = EconomyConfig.Daily(d);
                var cell = Ui.El(grid, d == Days - 1 ? "cj-daily__cell cj-daily__cell--chest" : "cj-daily__cell");
                Ui.Text(cell, $"Day {d + 1}", "cj-t-micro cj-c-ink3");
                IconKind kind = reward.IsChest ? IconKind.Chest
                    : reward.Booster.HasValue ? UiText.BoosterIcon(reward.Booster.Value)
                    : IconKind.Coin;
                icons[d] = Ui.Icon(cell, kind, "cj-daily__icon");
                icons[d].Tint = UiPalette.Ink;
                string amount = reward.IsChest ? "Chest" : reward.Booster.HasValue ? "×1" : Anim.Format(reward.Coins);
                Ui.Text(cell, amount, "cj-t-label cj-c-ink");
                var check = Ui.El(cell, "cj-daily__check");
                var tick = Ui.Icon(check, IconKind.Check, "cj-daily__check-icon");
                tick.Tint = UiPalette.OnGlass;
                cells[d] = cell;
                checks[d] = check;
            }

            actions = Ui.El(Body, "cj-popup__actions");
            claim = new CandyButton("Claim", "cj-btn--primary cj-btn--l cj-btn--wide");
            claim.Clicked += OnClaim;
            actions.Add(claim);
            comeBack = Ui.Text(Body, "Come back tomorrow for the next one", "cj-popup__note");
        }

        /// <summary>Opens the calendar for today's status; <paramref name="closedAction"/> runs after closing.</summary>
        public void Show(DailyRewardInfo info, Action closedAction)
        {
            closed = closedAction;
            bool available = info != null && info.Available;
            today = Math.Max(0, Math.Min(Days - 1, info?.Day ?? 0));
            int streak = info?.LoginStreak ?? 0;
            streakLabel.text = streak > 1 ? $"{streak} days in a row" : "A gift for every day you play";

            for (int d = 0; d < Days; d++)
            {
                bool claimed = d < today;
                cells[d].EnableInClassList("cj-daily__cell--claimed", claimed);
                cells[d].EnableInClassList("cj-daily__cell--today", d == today && available);
                Ui.Show(checks[d], claimed);
            }
            Ui.Show(actions, available);
            Anim.SetOpacity(actions, 1f);
            claim.Interactable = available;
            Ui.Show(comeBack, !available);

            OpenPopup();
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

            var cell = cells[today];
            cell.EnableInClassList("cj-daily__cell--today", false);
            cell.EnableInClassList("cj-daily__cell--claimed", true);
            Ui.Show(checks[today], true);
            Anim.PopInReward(checks[today], 0.05f, 0.36f);
            var center = cell.worldBound.center;
            Ctx.Fly.Burst(center, 14, 180f);
            UiFeedback.Reward();
            Anim.Fade(actions, 1f, 0f, Anim.Small, 0f, () => Ui.Show(actions, false));

            int coins = result.Reward.Coins;
            if (coins > 0) Ctx.Fly.Coins(center, Ctx.Coins, coins, FlyLayer.CoinCountFor(coins), 0.2f, Ctx.Coins.Unfreeze);
            else Ctx.Coins.Unfreeze();

            UiTween.Delay(1.6f, () => Close(TakeClosed()));
        }

        protected override void OnCloseButton() => Close(TakeClosed());

        Action TakeClosed()
        {
            var c = closed;
            closed = null;
            return c;
        }
    }
}
