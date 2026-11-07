using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The in-level HUD (design v3), a glass treatment so the scenery shows through: a pause circle,
    /// the "Level N" chip (+ HARD tag) on top, and three glass booster discs at the bottom. The coin
    /// pill is global (top layer) and sits at the top row's right end.
    /// </summary>
    public sealed class LevelHud
    {
        /// <summary>Top row height in panel units (the board's viewport starts below it).</summary>
        public const float TopBarHeight = 150f;
        /// <summary>Booster row height in panel units (the board's viewport ends above it).</summary>
        public const float BottomBarHeight = 250f;

        readonly UiContext ctx;
        readonly VisualElement topBar;
        readonly VisualElement bottomBar;
        readonly Label levelLabel;
        readonly VisualElement hardTag;
        readonly BoosterButton[] boosters = new BoosterButton[EconomyConfig.BoosterKinds];
        bool visible;

        public event Action PauseClicked;
        public event Action<BoosterType> BoosterClicked;

        public LevelHud(UiContext ctx)
        {
            this.ctx = ctx;
            var safe = Ui.El(ctx.HudLayer, "cj-fill cj-safe cj-hud");

            topBar = Ui.El(safe, "cj-hud__top");
            topBar.style.height = TopBarHeight;
            var pause = new CandyButton(null, "cj-btn--glass cj-btn--icon cj-hud__pause", IconKind.Pause);
            pause.Clicked += () => PauseClicked?.Invoke();
            topBar.Add(pause);

            var levelChip = Ui.El(topBar, "cj-chip cj-glass cj-hud__level", true);
            levelLabel = Ui.Text(levelChip, "Level 1", "cj-chip__label cj-on-glass");
            hardTag = Ui.El(levelChip, "cj-tag cj-hud__hard");
            Ui.Text(hardTag, "HARD", "cj-tag__label");
            Ui.El(topBar, "cj-hud__coin-space");

            Ui.El(safe, "cj-grow");

            bottomBar = Ui.El(safe, "cj-hud__bottom");
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
            Ui.Show(hardTag, hard);
        }

        public void Show()
        {
            if (visible) return;
            visible = true;
            Ui.Show(ctx.HudLayer, true);
            Anim.SlideIn(topBar, 0f, -60f, Anim.Panel);
            Anim.SlideIn(bottomBar, 0f, 80f, Anim.Panel, 0.04f);
        }

        public void Hide()
        {
            if (!visible) return;
            visible = false;
            Anim.SlideOut(topBar, 0f, -60f, Anim.Small);
            Anim.SlideOut(bottomBar, 0f, 80f, Anim.Small, () =>
            {
                if (!visible) Ui.Show(ctx.HudLayer, false);
            });
        }

        /// <summary>Re-reads counts (free Undo charges included), unlocks and usability for every booster.</summary>
        public void RefreshBoosters()
        {
            var p = ctx.Progression;
            var c = ctx.Controller;
            foreach (var button in boosters)
            {
                var b = button.Booster;
                bool armed = b == BoosterType.Crane && c.CraneArmed;
                bool free = b == BoosterType.Undo && p.FreeUndoCharges > 0;
                button.Refresh(p.IsUnlocked(b), p.UsableBoosterCount(b), c.CanUseBooster(b), armed, free);
            }
        }
    }
}
