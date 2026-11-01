using System;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The launch title, shown once per app launch: the Cargo Jam wordmark over a wide establishing
    /// shot of the home diorama, one big Play button and a quiet settings gear. It says nothing about
    /// levels or progress. Play hands over to the home screen: <see cref="Leave"/> lifts the wordmark
    /// and the button away while the flow glides the camera in to the home framing. The Play button's
    /// slow breathe is the screen's one idle loop.
    /// </summary>
    public sealed class TitleScreen
    {
        /// <summary>Fallback height of the Play block (panel units) before the first layout.</summary>
        public const float FooterHeight = 330f;
        /// <summary>Fallback bottom of the wordmark (panel units below the safe-area top) before the first layout.</summary>
        public const float HeaderBottom = 620f;

        /// <summary>How far the wordmark and the Play button rise as they fade out (panel units).</summary>
        const float LogoLift = 90f;
        const float PlayLift = 50f;

        readonly UiContext ctx;
        readonly VisualElement layer;
        readonly VisualElement root;
        readonly GradientFill topShade;
        readonly GradientFill bottomShade;
        readonly VisualElement topRow;
        readonly CandyButton gear;
        readonly VisualElement header;
        readonly Logo logo;
        readonly VisualElement footer;
        readonly VisualElement playWrap;
        readonly CandyButton play;
        UiTween breathe, playIn, gearIn;
        bool visible;
        bool leaving;

        public event Action PlayClicked;
        public event Action SettingsClicked;
        /// <summary>The wordmark or the Play block was laid out (the establishing shot may change).</summary>
        public event Action LayoutChanged;

        public TitleScreen(UiContext ctx, VisualElement layer)
        {
            this.ctx = ctx;
            this.layer = layer;
            root = Ui.El(layer, "cj-fill");

            // Soft dark fades top and bottom keep the white wordmark and the button grounded over any sky.
            topShade = new GradientFill(UiPalette.A(UiPalette.Scrim, 0.6f), UiPalette.ScrimClear);
            topShade.AddToClassList("cj-title__shade");
            topShade.AddToClassList("cj-title__shade--top");
            root.Add(topShade);
            bottomShade = new GradientFill(UiPalette.ScrimClear, UiPalette.A(UiPalette.Scrim, 0.6f));
            bottomShade.AddToClassList("cj-title__shade");
            bottomShade.AddToClassList("cj-title__shade--bottom");
            root.Add(bottomShade);

            var safe = Ui.El(root, "cj-fill cj-safe cj-title");

            topRow = Ui.El(safe, "cj-title__top");
            topRow.style.height = LevelHud.TopBarHeight;
            gear = new CandyButton(null, "cj-btn--glass cj-btn--icon cj-title__settings", IconKind.Gear);
            gear.Clicked += () => SettingsClicked?.Invoke();
            topRow.Add(gear);

            header = Ui.El(safe, "cj-title__header");
            logo = new Logo();
            header.Add(logo);

            Ui.El(safe, "cj-grow");

            footer = Ui.El(safe, "cj-title__footer");
            playWrap = Ui.El(footer, "cj-title__play");
            play = new CandyButton("Play", "cj-btn--primary cj-btn--l cj-btn--wide cj-btn--raised cj-title__play-button");
            play.Clicked += () => PlayClicked?.Invoke();
            playWrap.Add(play);

            header.RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged?.Invoke());
            footer.RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged?.Invoke());
            Ui.Show(layer, false);
        }

        /// <summary>True from <see cref="Show"/> until <see cref="Leave"/> has finished.</summary>
        public bool Visible => visible;

        /// <summary>True while the title owns the camera framing: shown and not yet leaving.</summary>
        public bool Framing => visible && !leaving;

        /// <summary>
        /// Bottom of the wordmark in panel units from the top of the panel (where the establishing
        /// shot's frame starts), or a fallback before the first layout.
        /// </summary>
        public float StageTop
        {
            get
            {
                // From layout, not worldBound, so the intro's rise doesn't skew it.
                float y = header.layout.yMax;
                if (float.IsNaN(y) || y <= 1f) y = ctx.Space.SafeInsets.y + HeaderBottom;
                return y;
            }
        }

        /// <summary>
        /// Top of the Play block in panel units from the top of the panel (where the establishing
        /// shot's frame ends), or a fallback before the first layout.
        /// </summary>
        public float StageBottom
        {
            get
            {
                float h = footer.layout.height;
                if (float.IsNaN(h) || h <= 1f) h = FooterHeight;
                return ctx.Space.PanelSize.y - ctx.Space.SafeInsets.w - h;
            }
        }

        /// <summary>The wordmark drops in, then the Play button and the gear settle in under it.</summary>
        public void Show()
        {
            if (visible) return;
            visible = true;
            leaving = false;
            play.Interactable = true;
            gear.Interactable = true;
            Ui.Show(layer, true);
            Anim.Fade(topShade, 0f, 1f, Anim.Panel);
            Anim.Fade(bottomShade, 0f, 1f, Anim.Panel);
            logo.PlayIntro();
            playIn = Anim.SlideIn(playWrap, 0f, 60f, Anim.Panel, 0.45f);
            gearIn = Anim.Fade(topRow, 0f, 1f, Anim.Panel, 0.55f);
            breathe?.Cancel();
            breathe = UiTween.Delay(0.45f + Anim.Panel, () =>
            {
                if (Framing) breathe = Anim.Breathe(playWrap);
            });
        }

        /// <summary>
        /// The hand-over to Home: the wordmark and Play lift and fade (the gear and shades just fade),
        /// then the layer hides and <paramref name="done"/> runs. <see cref="Framing"/> is false from
        /// the moment this is called, so the camera can start gliding to the home framing at once.
        /// </summary>
        public void Leave(Action done = null)
        {
            if (!visible || leaving)
            {
                if (!visible) done?.Invoke();
                return;
            }
            leaving = true;
            play.Interactable = false;
            gear.Interactable = false;
            breathe?.Cancel();
            breathe = null;
            // A quick tap can land before the button has settled in: stop its entrance first.
            playIn?.Cancel();
            gearIn?.Cancel();
            Anim.SetScale(playWrap, 1f);

            Lift(header, LogoLift, Anim.Panel, 0f);
            Lift(playWrap, PlayLift, Anim.Panel, 0.04f);
            Anim.Fade(topRow, topRow.resolvedStyle.opacity, 0f, Anim.Small);
            Anim.Fade(bottomShade, 1f, 0f, Anim.Panel + 0.1f);
            Anim.Fade(topShade, 1f, 0f, Anim.Panel + 0.1f, 0f, () =>
            {
                visible = false;
                leaving = false;
                Ui.Show(layer, false);
                foreach (var e in new[] { header, playWrap, topRow })
                {
                    Anim.ClearTransform(e);
                    Anim.SetOpacity(e, 1f);
                }
                done?.Invoke();
            });
        }

        /// <summary>Hides at once with no animation (tools that skip straight into a level).</summary>
        public void HideNow()
        {
            if (!visible) return;
            visible = false;
            leaving = false;
            breathe?.Cancel();
            breathe = null;
            Ui.Show(layer, false);
        }

        /// <summary>Rises and fades together, eased in and out so the move starts and ends softly.</summary>
        static void Lift(VisualElement e, float distance, float duration, float delay)
        {
            float from = e.resolvedStyle.opacity;
            UiTween.Run(duration, t =>
            {
                Anim.SetTranslate(e, 0f, -distance * t);
                Anim.SetOpacity(e, from * (1f - t));
            }, Easing.InOutCubic, delay);
        }
    }
}
