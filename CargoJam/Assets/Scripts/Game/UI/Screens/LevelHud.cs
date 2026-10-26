using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The in-level HUD: pause, "Level N" (+ HARD) on top, and the booster bar at the bottom. The
    /// coin counter is global (top layer) and sits at the top bar's right end.
    /// </summary>
    public sealed class LevelHud
    {
        /// <summary>Top bar height in panel units (the board's viewport starts below it).</summary>
        public const float TopBarHeight = 170f;
        /// <summary>Booster bar height in panel units (the board's viewport ends above it).</summary>
        public const float BottomBarHeight = 290f;

        readonly UiContext ctx;
        readonly VisualElement topBar;
        readonly VisualElement bottomBar;
        readonly Label levelLabel;
        readonly VisualElement hardBadge;
        readonly BoosterButton[] boosters = new BoosterButton[EconomyConfig.BoosterKinds];
        bool visible;

        public event Action PauseClicked;
        public event Action<BoosterType> BoosterClicked;

        public LevelHud(UiContext ctx)
        {
            this.ctx = ctx;
            var safe = Ui.El(ctx.HudLayer, "cj-fill cj-safe cj-hud");

            topBar = Ui.El(safe, "cj-hud__top", true);
            topBar.style.height = TopBarHeight;
            var pause = new CandyButton(null, "cj-btn--secondary cj-btn--round-m", IconKind.Pause);
            pause.Clicked += () => PauseClicked?.Invoke();
            topBar.Add(pause);

            var levelPill = Ui.El(topBar, "cj-hud__level");
            levelLabel = Ui.Text(levelPill, "Level 1", "cj-hud__level-label cj-txt");
            hardBadge = Ui.El(levelPill, "cj-hard-badge");
            Ui.Text(hardBadge, "HARD", "cj-hard-badge__label cj-txt");
            Ui.El(topBar, "cj-hud__coin-space");

            Ui.El(safe, "cj-grow");

            bottomBar = Ui.El(safe, "cj-hud__bottom", true);
            bottomBar.style.height = BottomBarHeight;
            foreach (BoosterType b in Enum.GetValues(typeof(BoosterType)))
            {
                var button = new BoosterButton(b);
                button.Clicked += type => BoosterClicked?.Invoke(type);
                boosters[(int)b] = button;
                bottomBar.Add(button);
            }

            Ui.Show(ctx.HudLayer, false);
        }

        public BoosterButton Button(BoosterType booster) => boosters[(int)booster];

        public void SetLevel(int number, bool hard)
        {
            levelLabel.text = $"Level {number}";
            Ui.Show(hardBadge, hard);
        }

        public void Show()
        {
            if (visible) return;
            visible = true;
            Ui.Show(ctx.HudLayer, true);
            Anim.SlideIn(topBar, 0f, -TopBarHeight, 0.4f);
            Anim.SlideIn(bottomBar, 0f, BottomBarHeight, 0.45f, 0.05f);
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
            Anim.SlideOut(topBar, 0f, -TopBarHeight, 0.22f);
            Anim.SlideOut(bottomBar, 0f, BottomBarHeight, 0.22f, () =>
            {
                if (!visible) Ui.Show(ctx.HudLayer, false);
            });
        }

        /// <summary>Re-reads counts, unlocks and usability for every booster.</summary>
        public void RefreshBoosters()
        {
            var p = ctx.Progression;
            var c = ctx.Controller;
            foreach (var button in boosters)
            {
                var b = button.Booster;
                bool armed = b == BoosterType.Crane && c.CraneArmed;
                button.Refresh(p.IsUnlocked(b), p.BoosterCount(b), c.CanUseBooster(b), armed);
            }
        }
    }
}
