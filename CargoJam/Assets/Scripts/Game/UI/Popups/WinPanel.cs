using System;
using System.Collections.Generic;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The win sequence's card (design v3): a bottom-anchored card rises with "Delivered" and
    /// "Level N · Destination"; the reward counts up while 8–15 coins arc into the coin pill (each
    /// landing ticks and squashes it); the streak-tier chip and progress, and the journey progress,
    /// advance; Next appears at about 2.2 s. A tap anywhere jumps to the end state.
    /// The coin counter must be frozen before the win is recorded; this panel releases it.
    /// </summary>
    public sealed class WinPanel : Popup
    {
        const float CountAt = 0.3f;
        const float CoinsAt = 0.55f;
        const float BarsAt = 0.9f;
        const float NextAt = 2.2f;

        readonly Label caption;
        readonly Icon coinIcon;
        readonly Label coinsLabel;
        readonly VisualElement tierChip;
        readonly Label tierLabel;
        readonly Label streakLeft;
        readonly Label streakRight;
        readonly ProgressBar streakBar;
        readonly Label journeyLeft;
        readonly Label journeyRight;
        readonly ProgressBar journeyBar;
        readonly VisualElement postcardRow;
        readonly Label postcardLabel;
        readonly VisualElement nextWrap;
        readonly CandyButton next;
        readonly List<UiTween> timeline = new List<UiTween>();
        Action onNext;
        bool holdingCoins;
        bool coinsLaunched;
        bool finished;
        UiTween breathe;

        // End state of the current win, applied by the timeline or a skip.
        int earned;
        float streakTo, journeyTo;
        bool hasPostcard;

        public WinPanel(UiContext ctx) : base(ctx, "Delivered", PopupKind.BottomCard)
        {
            caption = new Label("");
            caption.pickingMode = PickingMode.Ignore;
            Ui.AddClasses(caption, "cj-t-label cj-c-ink2 cj-win__caption");
            Panel.Insert(Panel.IndexOf(TitleLabel), caption);

            var reward = Ui.El(Body, "cj-win__reward");
            coinIcon = Ui.Icon(reward, IconKind.Coin, "cj-win__coin");
            coinsLabel = Ui.Text(reward, "+0", "cj-win__coins");
            tierChip = Ui.El(reward, "cj-tier cj-win__tier");
            Ui.Icon(tierChip, IconKind.Flame, "cj-tier__icon");
            tierLabel = Ui.Text(tierChip, "×1.5", "cj-tier__label");

            var progress = Ui.El(Body, "cj-inset cj-win__progress");
            var streakRow = Ui.El(progress, "cj-win__row");
            streakLeft = Ui.Text(streakRow, "", "cj-t-caption cj-c-ink2");
            Ui.El(streakRow, "cj-grow");
            streakRight = Ui.Text(streakRow, "", "cj-t-caption cj-c-ink2");
            streakBar = new ProgressBar("cj-bar--gold cj-win__bar");
            progress.Add(streakBar);

            var journeyRow = Ui.El(progress, "cj-win__row cj-win__row--gap");
            journeyLeft = Ui.Text(journeyRow, "", "cj-t-caption cj-c-ink2");
            Ui.El(journeyRow, "cj-grow");
            journeyRight = Ui.Text(journeyRow, "", "cj-t-caption cj-c-ink2");
            journeyBar = new ProgressBar("cj-win__bar");
            progress.Add(journeyBar);

            postcardRow = Ui.El(Body, "cj-win__postcard");
            var stamp = new PostcardStamp();
            stamp.AddToClassList("cj-win__stamp");
            postcardRow.Add(stamp);
            postcardLabel = Ui.Text(postcardRow, "", "cj-t-label cj-c-ink");

            // Breathe the wrapper, not the button, so the press scale stays its own.
            nextWrap = Ui.El(Body, "cj-win__next-wrap");
            next = new CandyButton("Next", "cj-btn--primary cj-btn--l cj-btn--wide cj-win__next");
            next.Clicked += OnNext;
            nextWrap.Add(next);

            // A tap anywhere (but Next) jumps to the end state.
            Frame.RegisterCallback<PointerDownEvent>(_ => SkipToEnd());
        }

        protected override void OnScrimTapped() => SkipToEnd();

        /// <summary>Opens the card for a recorded win; <paramref name="nextAction"/> runs after it closes.</summary>
        /// <param name="levelIndex">The 0-based index of the level just won (used when the result is missing).</param>
        public void Show(WinResult result, int levelIndex, Action nextAction)
        {
            onNext = nextAction;
            holdingCoins = true;
            coinsLaunched = false;
            finished = false;
            Clear();

            int won = result != null ? result.NewLevelIndex - 1 : levelIndex;
            int stop = Journey.StopFor(won);
            caption.text = $"Level {won + 1} · {Journey.NameOf(stop)}";

            // Coins and the tier chip.
            earned = result?.CoinsEarned ?? 0;
            float mult = result?.Multiplier ?? 1f;
            coinsLabel.text = "+0";
            tierLabel.text = UiText.Multiplier(mult);
            Ui.Show(tierChip, mult > 1.01f);
            Anim.SetOpacity(tierChip, 0f);

            // Streak progress towards the next tier.
            int streak = result?.StreakAfter ?? Ctx.Progression.WinStreak;
            int tier = result?.TierAfter ?? Progression.StreakTierFor(streak);
            int nextAt = result?.NextTierAt ?? Progression.NextTierThreshold(streak);
            int floor = EconomyConfig.StreakTierStart(tier);
            streakLeft.text = UiText.WinsInARow(streak);
            float streakFrom;
            if (nextAt < 0)
            {
                streakTo = 1f;
                streakFrom = 1f;
                streakRight.text = "Top tier · free undo each level";
            }
            else
            {
                float span = Mathf.Max(1, nextAt - floor);
                streakTo = (streak - floor) / span;
                streakFrom = result != null && result.TierUp ? 0f : Mathf.Max(0f, (streak - 1 - floor) / span);
                int left = nextAt - streak;
                streakRight.text = $"{left} more for {UiText.Multiplier(Progression.TierMultiplier(tier + 1))}";
            }
            streakBar.SetFraction(streakFrom, false);

            // Journey progress within the destination.
            int done = Journey.StepInStop(won) + 1;
            float journeyFrom = (done - 1) / (float)Journey.LevelsPerStop;
            journeyTo = done / (float)Journey.LevelsPerStop;
            journeyLeft.text = Journey.NameOf(stop);
            int arrived = result?.ArrivedAt ?? -1;
            journeyRight.text = arrived >= 0
                ? $"Arrived at {Journey.NameOf(arrived)}"
                : UiText.Deliveries(Journey.LevelsPerStop - done, Journey.NextStop(won));
            journeyBar.SetFraction(journeyFrom, false);

            int postcard = result?.PostcardEarned ?? -1;
            hasPostcard = postcard >= 0;
            postcardLabel.text = hasPostcard ? $"Postcard earned · {Journey.NameOf(postcard)}" : "";
            Ui.Show(postcardRow, hasPostcard);
            Anim.SetOpacity(postcardRow, 0f);

            next.Interactable = false;
            Anim.SetOpacity(next, 0f);

            OpenPopup();

            timeline.Add(Anim.CountUp(coinsLabel, 0, earned, 0.6f, CountAt, "+"));
            if (mult > 1.01f)
            {
                bool tierUp = result != null && result.TierUp;
                timeline.Add(UiTween.Delay(CountAt + 0.1f, () =>
                {
                    timeline.Add(Anim.PopInReward(tierChip));
                    if (tierUp)
                    {
                        UiFeedback.Sparkle();
                        Ctx.Fly.Burst(tierChip.worldBound.center, 12, 150f, 0.2f);
                    }
                }));
            }
            timeline.Add(UiTween.Delay(CoinsAt, LaunchCoins));
            timeline.Add(UiTween.Delay(BarsAt, () =>
            {
                streakBar.SetFraction(streakTo, true, 0f, 0.7f);
                journeyBar.SetFraction(journeyTo, true, 0.12f, 0.7f);
            }));
            if (hasPostcard)
            {
                timeline.Add(UiTween.Delay(BarsAt + 0.6f, () =>
                {
                    timeline.Add(Anim.PopInReward(postcardRow));
                    UiFeedback.Sparkle();
                }));
            }
            timeline.Add(UiTween.Delay(NextAt, ShowNext));
        }

        void LaunchCoins()
        {
            if (coinsLaunched) return;
            coinsLaunched = true;
            Ctx.Fly.Coins(coinIcon.worldBound.center, Ctx.Coins, earned, FlyLayer.CoinCountFor(earned), 0f, ReleaseCoins);
        }

        void ShowNext()
        {
            if (finished) return;
            finished = true;
            next.Interactable = true;
            Anim.Fade(next, Mathf.Clamp01(next.resolvedStyle.opacity), 1f, Anim.Small);
            breathe?.Cancel();
            breathe = Anim.Breathe(nextWrap);
        }

        /// <summary>Jumps every part of the sequence to its end state (coins in flight keep flying).</summary>
        void SkipToEnd()
        {
            if (!IsOpen || finished) return;
            foreach (var t in timeline) t.Cancel();
            timeline.Clear();
            coinsLabel.text = "+" + Anim.Format(earned);
            if (tierChip.style.display != DisplayStyle.None)
            {
                Anim.SetOpacity(tierChip, 1f);
                Anim.SetScale(tierChip, 1f);
            }
            streakBar.SetFraction(streakTo, false);
            journeyBar.SetFraction(journeyTo, false);
            if (hasPostcard)
            {
                Anim.SetOpacity(postcardRow, 1f);
                Anim.SetScale(postcardRow, 1f);
            }
            // Coins that never left don't fly; the counter simply rolls to the balance.
            if (!coinsLaunched)
            {
                coinsLaunched = true;
                ReleaseCoins();
            }
            ShowNext();
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
            if (!coinsLaunched) ReleaseCoins();
        }

        void Clear()
        {
            foreach (var t in timeline) t.Cancel();
            timeline.Clear();
            breathe?.Cancel();
            breathe = null;
            Anim.SetScale(nextWrap, 1f);
        }
    }
}
