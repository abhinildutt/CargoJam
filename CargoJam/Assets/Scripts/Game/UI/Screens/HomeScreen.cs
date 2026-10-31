using System;
using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The home screen (design v3): the live 3D diorama of the current destination fills the screen;
    /// over it sit a quiet settings button (top-left), the coin pill (top-right, global), a left-edge
    /// column of Daily and Chest icons that only appear while claimable, and the bottom block:
    /// destination caption and name, the journey strip (opens the Journey screen) and the Level N
    /// button with the streak tier chip. The Level button's slow breathe is the screen's one idle loop.
    /// </summary>
    public sealed class HomeScreen
    {
        /// <summary>Fallback height of the bottom block (panel units) before the first layout.</summary>
        public const float BottomBlockHeight = 640f;

        readonly UiContext ctx;
        readonly VisualElement root;
        readonly GradientFill shade;
        readonly VisualElement topRow;
        readonly VisualElement edge;
        readonly CandyButton dailyButton;
        readonly CandyButton chestButton;
        readonly VisualElement bottom;
        readonly Label caption;
        readonly Label destination;
        readonly JourneyStrip strip;
        readonly VisualElement playWrap;
        readonly CandyButton play;
        readonly VisualElement tierChip;
        readonly Label tierLabel;
        readonly VisualElement hardTag;
        UiTween breathe;
        bool visible;

        public event Action PlayClicked;
        public event Action SettingsClicked;
        public event Action DailyClicked;
        public event Action ChestClicked;
        public event Action JourneyClicked;
        /// <summary>The bottom block was laid out (the diorama framing may change).</summary>
        public event Action LayoutChanged;

        public HomeScreen(UiContext ctx)
        {
            this.ctx = ctx;
            root = Ui.El(ctx.HomeLayer, "cj-fill");

            // A soft fade to dark behind the bottom block keeps white text legible over any scenery.
            shade = new GradientFill(UiPalette.ScrimClear, UiPalette.A(UiPalette.Scrim, 0.78f));
            shade.AddToClassList("cj-home__shade");
            root.Add(shade);

            var safe = Ui.El(root, "cj-fill cj-safe cj-home");

            topRow = Ui.El(safe, "cj-home__top");
            topRow.style.height = LevelHud.TopBarHeight;
            var gear = new CandyButton(null, "cj-btn--glass cj-btn--icon cj-home__settings", IconKind.Gear);
            gear.Clicked += () => SettingsClicked?.Invoke();
            topRow.Add(gear);

            edge = Ui.El(safe, "cj-home__edge");
            dailyButton = EdgeButton(IconKind.Gift, "cj-home__daily", () => DailyClicked?.Invoke());
            chestButton = EdgeButton(IconKind.Chest, "cj-home__chest", () => ChestClicked?.Invoke());

            Ui.El(safe, "cj-grow");

            bottom = Ui.El(safe, "cj-home__bottom");
            caption = Ui.Text(bottom, "", "cj-home__caption cj-t-label cj-on-glass-2");
            destination = Ui.Text(bottom, "", "cj-home__name cj-t-display cj-on-glass");
            strip = new JourneyStrip();
            strip.Button.AddToClassList("cj-home__strip");
            strip.Button.Clicked += () => JourneyClicked?.Invoke();
            bottom.Add(strip.Button);

            playWrap = Ui.El(bottom, "cj-home__play");
            play = new CandyButton("Level 1", "cj-btn--primary cj-btn--l cj-btn--wide cj-btn--raised cj-home__level");
            play.Clicked += () => PlayClicked?.Invoke();
            playWrap.Add(play);
            hardTag = Ui.El(play, "cj-tag cj-home__hard");
            Ui.Text(hardTag, "HARD", "cj-tag__label");
            tierChip = Ui.El(play, "cj-tier cj-tier--on-primary cj-home__tier");
            Ui.Icon(tierChip, IconKind.Flame, "cj-tier__icon");
            tierLabel = Ui.Text(tierChip, "×1.5", "cj-tier__label");

            bottom.RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged?.Invoke());
            Ui.Show(ctx.HomeLayer, false);
        }

        CandyButton EdgeButton(IconKind icon, string cls, Action clicked)
        {
            var button = new CandyButton(null, "cj-btn--glass cj-btn--tile " + cls, icon);
            Ui.El(button, "cj-dot");
            button.Clicked += clicked;
            edge.Add(button);
            return button;
        }

        public bool Visible => visible;

        /// <summary>
        /// Top of the bottom block in panel units from the top of the panel (where the diorama's
        /// frame ends), or a fallback before the first layout.
        /// </summary>
        public float BottomBlockTop
        {
            get
            {
                // From the layout height, not worldBound, so a slide-in transform doesn't skew it.
                float h = bottom.layout.height;
                if (float.IsNaN(h) || h <= 1f) h = BottomBlockHeight;
                return ctx.Space.PanelSize.y - ctx.Space.SafeInsets.w - h;
            }
        }

        /// <summary>Re-reads destination, progress, streak tier, and what is claimable.</summary>
        public void Refresh()
        {
            var p = ctx.Progression;
            int index = p.LevelIndex;
            caption.text = UiText.StopCaption(index);
            destination.text = Journey.NameOf(Journey.StopFor(index));
            strip.Set(index);
            play.Text = $"Level {index + 1}";
            Ui.Show(hardTag, ctx.Controller.IsHardLevel(index));

            // What the next win pays (the streak counts that win), so the chip never undersells it.
            float next = p.NextWinMultiplier;
            Ui.Show(tierChip, next > 1f);
            tierLabel.text = UiText.Multiplier(next);

            var daily = p.DailyStatus();
            bool dailyReady = daily != null && daily.Available;
            Ui.Show(dailyButton, dailyReady);
            Ui.Show(chestButton, p.ChestReady);
            Ui.Show(edge, dailyReady || p.ChestReady);
        }

        public void Show()
        {
            Refresh();
            if (visible) return;
            visible = true;
            Ui.Show(ctx.HomeLayer, true);
            Anim.Fade(root, 0f, 1f, Anim.Small);
            Anim.SlideIn(topRow, 0f, -40f, Anim.Panel);
            Anim.SlideIn(edge, -40f, 0f, Anim.Panel, 0.06f);
            Anim.SlideIn(bottom, 0f, 80f, Anim.Panel, 0.04f);
            breathe?.Cancel();
            breathe = UiTween.Delay(Anim.Panel, () =>
            {
                if (visible) breathe = Anim.Breathe(playWrap);
            });
        }

        public void Hide(Action done = null)
        {
            if (!visible)
            {
                done?.Invoke();
                return;
            }
            visible = false;
            breathe?.Cancel();
            breathe = null;
            Anim.SetScale(playWrap, 1f);
            Anim.SlideOut(bottom, 0f, 60f, Anim.Small);
            Anim.Fade(root, 1f, 0f, Anim.Small, 0f, () =>
            {
                if (!visible)
                {
                    Ui.Show(ctx.HomeLayer, false);
                    Anim.ClearTransform(bottom);
                    Anim.SetOpacity(bottom, 1f);
                }
                done?.Invoke();
            });
        }
    }
}
