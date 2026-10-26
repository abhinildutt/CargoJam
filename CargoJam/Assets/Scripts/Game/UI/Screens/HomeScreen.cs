using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The home screen over the dimmed live board: route and progress, the pulsing "Level N" button
    /// (with HARD badge), the streak flame, the chest bar, the daily reward button and settings.
    /// </summary>
    public sealed class HomeScreen
    {
        readonly UiContext ctx;
        readonly VisualElement scrim;
        readonly VisualElement topRow;
        readonly VisualElement header;
        readonly VisualElement chestCard;
        readonly VisualElement playWrap;
        readonly CandyButton play;
        readonly VisualElement playHard;
        readonly Label routeLabel;
        readonly ProgressBar routeBar;
        readonly VisualElement streakChip;
        readonly Label streakLabel;
        readonly Label streakMultiplier;
        readonly ProgressBar chestBar;
        readonly Label chestHint;
        readonly Icon chestIcon;
        readonly VisualElement dailyDot;
        UiTween pulse, dotPulse;
        bool visible;

        public event Action PlayClicked;
        public event Action SettingsClicked;
        public event Action DailyClicked;

        public HomeScreen(UiContext ctx)
        {
            this.ctx = ctx;
            scrim = Ui.El(ctx.HomeLayer, "cj-fill cj-home__scrim", true);
            var safe = Ui.El(ctx.HomeLayer, "cj-fill cj-safe cj-home");

            // Top row: settings, streak, daily (coins live in the top layer at the right).
            topRow = Ui.El(safe, "cj-home__top");
            topRow.style.height = LevelHud.TopBarHeight;
            var gear = new CandyButton(null, "cj-btn--secondary cj-btn--round-m", IconKind.Gear);
            gear.Clicked += () => SettingsClicked?.Invoke();
            topRow.Add(gear);

            streakChip = Ui.El(topRow, "cj-chip");
            Ui.Icon(streakChip, IconKind.Flame, "cj-chip__icon");
            streakLabel = Ui.Text(streakChip, "0", "cj-chip__label cj-txt");
            streakMultiplier = Ui.Text(streakChip, "", "cj-chip__tag cj-txt");

            var dailyWrap = Ui.El(topRow, "cj-home__daily");
            var daily = new CandyButton(null, "cj-btn--coin cj-btn--round-m", IconKind.Gift);
            daily.Clicked += () => DailyClicked?.Invoke();
            dailyWrap.Add(daily);
            dailyDot = Ui.El(daily, "cj-dot");
            Ui.El(topRow, "cj-grow");
            Ui.El(topRow, "cj-hud__coin-space");

            header = Ui.El(safe, "cj-home__header");
            routeLabel = Ui.Text(header, "Route 1 · Harbor", "cj-home__route cj-txt");
            routeBar = new ProgressBar("cj-bar--route");
            header.Add(routeBar);

            Ui.El(safe, "cj-grow");

            chestCard = Ui.El(safe, "cj-card cj-home__chest");
            chestIcon = Ui.Icon(chestCard, IconKind.Chest, "cj-home__chest-icon");
            var chestCol = Ui.El(chestCard, "cj-home__chest-col");
            Ui.Text(chestCol, "Win levels to fill the chest!", "cj-home__chest-title");
            chestBar = new ProgressBar("cj-bar--chest");
            chestCol.Add(chestBar);
            chestHint = Ui.Text(chestCol, "", "cj-home__chest-hint");

            playWrap = Ui.El(safe, "cj-home__play");
            play = new CandyButton("Level 1", "cj-btn--primary cj-btn--huge");
            play.Clicked += () => PlayClicked?.Invoke();
            playWrap.Add(play);
            playHard = Ui.El(play, "cj-hard-badge cj-hard-badge--play");
            Ui.Text(playHard, "HARD", "cj-hard-badge__label cj-txt");

            Ui.Show(ctx.HomeLayer, false);
        }

        public bool Visible => visible;

        /// <summary>Re-reads progress, streak, chest and daily state.</summary>
        public void Refresh()
        {
            var p = ctx.Progression;
            int index = p.LevelIndex;
            routeLabel.text = UiText.RouteName(index);
            routeBar.Set(UiText.RouteStep(index), UiText.LevelsPerRoute, visible);
            play.Text = $"Level {index + 1}";
            Ui.Show(playHard, ctx.Controller.IsHardLevel(index));

            streakLabel.text = p.WinStreak.ToString();
            streakChip.EnableInClassList("cj-chip--cold", p.WinStreak <= 0);
            // What the next win will pay, so the chip is a promise rather than a receipt.
            float m = p.NextWinMultiplier;
            streakMultiplier.text = m > 1.01f ? UiText.Multiplier(m) + " coins" : "";
            Ui.Show(streakMultiplier, m > 1.01f);

            int chest = Math.Min(p.ChestProgress, EconomyConfig.ChestEvery);
            chestBar.Set(chest, EconomyConfig.ChestEvery, visible);
            bool big = p.NextChestIsBig;
            chestIcon.EnableInClassList("cj-home__chest-icon--big", big);
            chestHint.text = p.ChestReady ? "Chest ready! Win to open it."
                : big ? "Next one is a BIG chest!"
                : $"{EconomyConfig.ChestEvery - chest} more wins to a chest";

            SetDailyDot(DailyAvailable());
        }

        bool DailyAvailable()
        {
            var info = ctx.Progression.DailyStatus();
            return info != null && info.Available;
        }

        void SetDailyDot(bool on)
        {
            Ui.Show(dailyDot, on);
            if (on && dotPulse == null) dotPulse = Anim.Pulse(dailyDot, 0.2f, 0.8f);
            if (!on && dotPulse != null)
            {
                dotPulse.Cancel();
                dotPulse = null;
            }
        }

        public void Show()
        {
            Refresh();
            if (visible) return;
            visible = true;
            Ui.Show(ctx.HomeLayer, true);
            Anim.Fade(scrim, 0f, 1f, 0.3f);
            Anim.SlideIn(topRow, 0f, -120f, 0.4f);
            Anim.SlideIn(header, 0f, -80f, 0.45f, 0.05f);
            Anim.SlideIn(chestCard, 0f, 120f, 0.45f, 0.1f);
            Anim.PopIn(play, 0.18f, 0.45f);
            pulse?.Cancel();
            pulse = Anim.Pulse(playWrap, 0.05f, 1.2f);
        }

        public void Hide(Action done = null)
        {
            if (!visible)
            {
                done?.Invoke();
                return;
            }
            visible = false;
            pulse?.Cancel();
            pulse = null;
            Anim.Fade(scrim, 1f, 0f, 0.25f);
            Anim.SlideOut(topRow, 0f, -120f, 0.22f);
            Anim.SlideOut(header, 0f, -80f, 0.22f);
            Anim.SlideOut(chestCard, 0f, 160f, 0.22f);
            Anim.SlideOut(playWrap, 0f, 200f, 0.25f, () =>
            {
                Anim.SetTranslate(playWrap, 0f, 0f);
                Anim.SetOpacity(playWrap, 1f);
                if (!visible) Ui.Show(ctx.HomeLayer, false);
                done?.Invoke();
            });
        }
    }
}
