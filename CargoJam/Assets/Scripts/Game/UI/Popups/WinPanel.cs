using System;
using System.Collections.Generic;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// "Level Complete!": stars pop, the coin reward counts up (flashing the streak multiplier when
    /// it applies), coins fly into the counter, the chest bar fills, then a big Next button.
    /// The coin counter must be frozen before the win is recorded; this panel releases it.
    /// </summary>
    public sealed class WinPanel : Popup
    {
        readonly Icon[] stars = new Icon[3];
        readonly Icon coinIcon;
        readonly Label coinsLabel;
        readonly VisualElement multBadge;
        readonly Label multLabel;
        readonly Label streakLabel;
        readonly VisualElement chestRow;
        readonly Icon chestIcon;
        readonly ProgressBar chestBar;
        readonly Label chestReady;
        readonly CandyButton next;
        readonly List<UiTween> timeline = new List<UiTween>();
        Action onNext;
        bool holdingCoins;
        UiTween nextPulse;

        public WinPanel(UiContext ctx) : base(ctx, "Level Complete!", "cj-ribbon--primary", false)
        {
            var starRow = Ui.El(Body, "cj-win__stars");
            for (int i = 0; i < stars.Length; i++)
                stars[i] = Ui.Icon(starRow, IconKind.Star, i == 1 ? "cj-win__star cj-win__star--mid" : "cj-win__star");

            var reward = Ui.El(Body, "cj-win__reward");
            coinIcon = Ui.Icon(reward, IconKind.Coin, "cj-win__coin");
            coinsLabel = Ui.Text(reward, "+0", "cj-win__coins cj-txt");

            multBadge = Ui.El(Body, "cj-win__mult");
            Ui.Icon(multBadge, IconKind.Flame, "cj-win__mult-icon");
            multLabel = Ui.Text(multBadge, "Streak x1.5!", "cj-win__mult-label cj-txt");
            streakLabel = Ui.Text(Body, "", "cj-win__streak");

            chestRow = Ui.El(Body, "cj-win__chest");
            chestIcon = Ui.Icon(chestRow, IconKind.Chest, "cj-win__chest-icon");
            var chestCol = Ui.El(chestRow, "cj-win__chest-col");
            chestBar = new ProgressBar("cj-bar--chest");
            chestCol.Add(chestBar);
            chestReady = Ui.Text(chestCol, "Chest ready!", "cj-win__chest-ready cj-txt");

            next = new CandyButton("Next", "cj-btn--primary cj-btn--big cj-win__next");
            next.Clicked += OnNext;
            Body.Add(next);
        }

        /// <summary>Opens the panel for a recorded win; <paramref name="nextAction"/> runs after it closes.</summary>
        public void Show(WinResult result, Action nextAction)
        {
            onNext = nextAction;
            holdingCoins = true;
            Clear();

            int baseCoins = result?.BaseCoins ?? 0;
            int earned = result?.CoinsEarned ?? baseCoins;
            float mult = result?.Multiplier ?? 1f;
            bool hasMult = mult > 1.01f && earned > baseCoins;
            int chestBefore = Math.Min(result?.ChestBefore ?? 0, EconomyConfig.ChestEvery);
            int chestAfter = Math.Min(result?.ChestAfter ?? 0, EconomyConfig.ChestEvery);
            bool chestFull = result != null && result.ChestReady;

            coinsLabel.text = "+0";
            Ui.Show(multBadge, false);
            int streak = result?.StreakAfter ?? 0;
            streakLabel.text = streak >= 2 ? $"{streak} wins in a row!" : "";
            Ui.Show(streakLabel, streak >= 2);
            chestBar.Set(chestBefore, EconomyConfig.ChestEvery, false);
            Ui.Show(chestReady, false);
            next.Interactable = false;
            Ui.Show(next, false);

            OpenPopup();

            for (int i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                timeline.Add(Anim.PopIn(star, 0.25f + i * 0.14f, 0.4f));
                timeline.Add(UiTween.Delay(0.25f + i * 0.14f, () => UiFeedback.CoinTick(4)));
            }

            timeline.Add(Anim.CountUp(coinsLabel, 0, baseCoins, 0.55f, 0.55f, "+"));
            float flyAt = 1.25f;
            if (hasMult)
            {
                multLabel.text = $"Streak {UiText.Multiplier(mult)}!";
                timeline.Add(UiTween.Delay(1.15f, () =>
                {
                    Ui.Show(multBadge, true);
                    timeline.Add(Anim.PopIn(multBadge, 0f, 0.35f));
                    timeline.Add(UiTween.Delay(0.3f, () => Anim.Punch(multBadge, 0.2f)));
                    UiFeedback.Reward();
                }));
                timeline.Add(Anim.CountUp(coinsLabel, baseCoins, earned, 0.45f, 1.35f, "+"));
                timeline.Add(UiTween.Delay(1.8f, () => Anim.Punch(coinsLabel, 0.25f)));
                flyAt = 2.0f;
            }

            timeline.Add(UiTween.Delay(flyAt, () =>
                Ctx.Fly.Coins(coinIcon.worldBound.center, Ctx.Coins, earned, 10, 0f, ReleaseCoins)));

            timeline.Add(UiTween.Delay(1.0f, () => chestBar.Set(chestAfter, EconomyConfig.ChestEvery, true)));
            if (chestFull)
            {
                timeline.Add(UiTween.Delay(1.65f, () =>
                {
                    Ui.Show(chestReady, true);
                    Anim.PopIn(chestReady);
                    Anim.Punch(chestIcon, 0.3f, 0.4f);
                }));
            }

            timeline.Add(UiTween.Delay(flyAt + 0.35f, () =>
            {
                Ui.Show(next, true);
                next.Interactable = true;
                Anim.PopIn(next, 0f, 0.4f);
                nextPulse = Anim.Pulse(next.Content, 0.06f, 1f);
            }));
        }

        void OnNext()
        {
            var after = onNext;
            onNext = null;
            Close(after);
        }

        /// <summary>Lets the coin counter catch up to the real balance (once).</summary>
        void ReleaseCoins()
        {
            if (!holdingCoins) return;
            holdingCoins = false;
            Ctx.Coins.Unfreeze();
        }

        protected override void OnClosing()
        {
            Clear();
            ReleaseCoins();
        }

        void Clear()
        {
            foreach (var t in timeline) t.Cancel();
            timeline.Clear();
            nextPulse?.Cancel();
            nextPulse = null;
        }
    }
}
