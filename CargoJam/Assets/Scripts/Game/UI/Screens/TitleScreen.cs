using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The launch title, shown once per app launch, built like the title of a top mobile puzzle game:
    /// the harbor diorama full-bleed behind it with the camera drifting slowly, the Cargo Jam wordmark
    /// bouncing in over a warm glow and turning sunburst, twinkling sparkles, and one big glossy Play
    /// button that pulses and catches a shine every few seconds. It says nothing about levels or
    /// progress. Play hands over to the home screen: <see cref="Leave"/> flings the wordmark up, blows
    /// the sunburst out and drops the button away while the flow glides the camera in to the home
    /// framing. The title is the one screen that may break design v3's calm-motion rules (overshoot,
    /// more than one idle loop): it is the shop window.
    /// </summary>
    public sealed class TitleScreen
    {
        /// <summary>Fallback height of the Play block (panel units) before the first layout.</summary>
        public const float FooterHeight = 360f;
        /// <summary>Fallback bottom of the wordmark (panel units below the safe-area top) before the first layout.</summary>
        public const float HeaderBottom = 560f;

        /// <summary>When the Play button lands after the wordmark starts dropping in (seconds).</summary>
        const float PlayInAt = 0.62f;
        /// <summary>The wordmark's drop: from this far above its spot, with an OutBack landing.</summary>
        const float LogoDrop = 320f;
        const float LogoDropTime = 0.62f;
        /// <summary>Wordmark idle: a slow bob and sway.</summary>
        const float BobHeight = 9f;
        const float BobPeriod = 3.2f;
        const float SwayDegrees = 1.1f;
        const float SwayPeriod = 4.6f;
        /// <summary>Degrees per second the sunburst turns.</summary>
        const float RaySpeed = 7f;
        /// <summary>The Play button's pulse and the shine that sweeps across it.</summary>
        const float PulseAmount = 0.045f;
        const float PulsePeriod = 1.7f;
        const float ShineEvery = 2.8f;
        const float ShineTime = 0.55f;
        const float ShineWidth = 110f;
        /// <summary>The hand-over: how long the wordmark's fling and the sunburst's blow-out take.</summary>
        const float LeaveTime = 0.42f;

        /// <summary>Sparkles around the wordmark: position (0..1 of the wordmark box), size, phase offset.</summary>
        static readonly (float x, float y, float size, float phase)[] SparkleSpots =
        {
            (0.03f, 0.12f, 84f, 0.0f),
            (0.99f, 0.00f, 64f, 0.37f),
            (0.93f, 0.88f, 92f, 0.71f),
            (0.08f, 0.94f, 58f, 0.18f),
            (0.52f, -0.08f, 50f, 0.55f),
            (0.44f, 1.04f, 70f, 0.86f),
        };
        const float SparklePeriod = 2.2f;

        readonly UiContext ctx;
        readonly VisualElement layer;
        readonly VisualElement root;
        readonly GradientFill topShade;
        readonly GradientFill bottomShade;
        readonly VisualElement topRow;
        readonly CandyButton gear;
        readonly VisualElement header;
        readonly VisualElement halo;
        readonly RadialGlow glow;
        readonly Sunburst rays;
        readonly VisualElement logoWrap;
        readonly Logo logo;
        readonly Icon[] sparkles = new Icon[SparkleSpots.Length];
        readonly VisualElement footer;
        readonly VisualElement playWrap;
        readonly CandyButton play;
        readonly VisualElement shine;
        UiTween idle, logoIn, playIn, gearIn;
        bool visible;
        bool leaving;
        /// <summary>Time.unscaledTime when the idle loops' clock started (the wordmark's landing).</summary>
        float shownAt;

        public event Action PlayClicked;
        public event Action SettingsClicked;
        /// <summary>The wordmark or the Play block was laid out (the establishing shot may change).</summary>
        public event Action LayoutChanged;

        public TitleScreen(UiContext ctx, VisualElement layer)
        {
            this.ctx = ctx;
            this.layer = layer;
            root = Ui.El(layer, "cj-fill");

            // Deep fades top and bottom: the sky reads as dusk-blue behind the wordmark, and the
            // button sits on a dark floor, whatever the destination's colors.
            topShade = new GradientFill(UiPalette.A(TitleSky, 0.96f), UiPalette.A(TitleSky, 0f), 0.45f);
            topShade.AddToClassList("cj-title__shade");
            topShade.AddToClassList("cj-title__shade--top");
            root.Add(topShade);
            bottomShade = new GradientFill(UiPalette.ScrimClear, UiPalette.A(UiPalette.Scrim, 0.85f));
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
            // The halo (glow and sunburst) sits behind the wordmark, centered on it.
            halo = Ui.El(header, "cj-title__halo");
            glow = new RadialGlow(UiPalette.A(UiPalette.GoldLight, 0.38f));
            glow.AddToClassList("cj-title__glow");
            halo.Add(glow);
            rays = new Sunburst(UiPalette.A(Color.white, 0.2f));
            rays.AddToClassList("cj-title__rays");
            halo.Add(rays);

            logoWrap = Ui.El(header, "cj-title__logo");
            logo = new Logo();
            logoWrap.Add(logo);
            for (int i = 0; i < sparkles.Length; i++)
            {
                var (x, y, size, _) = SparkleSpots[i];
                var s = Ui.Icon(logoWrap, IconKind.Star, "cj-title__sparkle");
                s.Tint = i % 2 == 0 ? Color.white : UiPalette.GoldLight;
                s.style.left = Length.Percent(x * 100f);
                s.style.top = Length.Percent(y * 100f);
                s.style.width = size;
                s.style.height = size;
                s.style.marginLeft = -size * 0.5f;
                s.style.marginTop = -size * 0.5f;
                sparkles[i] = s;
            }

            Ui.El(safe, "cj-grow");

            footer = Ui.El(safe, "cj-title__footer");
            playWrap = Ui.El(footer, "cj-title__play");
            play = new CandyButton("Play", "cj-btn--primary cj-btn--l cj-btn--wide cj-btn--raised cj-title__play-button");
            play.Clicked += OnPlay;
            // Gloss between the face and the label: a lighter top half and the shine that sweeps across.
            var gloss = Ui.El(null, "cj-title__gloss");
            Ui.El(gloss, "cj-title__gloss-top");
            shine = Ui.El(gloss, "cj-title__shine");
            shine.style.width = ShineWidth;
            play.Insert(2, gloss);
            playWrap.Add(play);

            header.RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged?.Invoke());
            footer.RegisterCallback<GeometryChangedEvent>(_ => LayoutChanged?.Invoke());
            Ui.Show(layer, false);
        }

        /// <summary>The title's sky tint at the top of the screen, behind the wordmark.</summary>
        static Color TitleSky => new Color(16 / 255f, 44 / 255f, 78 / 255f, 1f);

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
                // From layout, not worldBound, so the intro's drop doesn't skew it.
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

        /// <summary>
        /// The wordmark drops in and lands with a bounce as the glow and sunburst bloom behind it,
        /// then the Play button pops up and the gear fades in. The idle loops start on the landing.
        /// </summary>
        public void Show()
        {
            if (visible) return;
            visible = true;
            leaving = false;
            play.Interactable = true;
            gear.Interactable = true;
            Ui.Show(layer, true);
            idle?.Cancel();
            foreach (var e in new[] { header, logoWrap, halo, playWrap, topRow }) Reset(e);
            foreach (var s in sparkles) Anim.SetScale(s, 0f);
            Anim.SetTranslate(shine, -ShineWidth * 2f, 0f);

            Anim.Fade(topShade, 0f, 1f, Anim.Panel);
            Anim.Fade(bottomShade, 0f, 1f, Anim.Panel);
            logo.PlayIntro();
            logoIn = UiTween.Run(LogoDropTime, t =>
            {
                Anim.SetTranslate(logoWrap, 0f, Mathf.LerpUnclamped(-LogoDrop, 0f, Easing.OutBack(t, 1.7f)));
                Anim.SetOpacity(logoWrap, Mathf.Clamp01(t * 4f));
            }, null);
            UiTween.Run(0.7f, t =>
            {
                Anim.SetScale(halo, Mathf.LerpUnclamped(0.3f, 1f, Easing.OutBack(t)));
                Anim.SetOpacity(halo, Mathf.Clamp01(t * 2f));
            }, null, 0.12f);
            playIn = UiTween.Run(Anim.Reward, t =>
            {
                Anim.SetScale(playWrap, Mathf.LerpUnclamped(0.5f, 1f, Easing.OutBack(t)));
                Anim.SetOpacity(playWrap, Mathf.Clamp01(t * 3f));
            }, null, PlayInAt);
            gearIn = Anim.Fade(topRow, 0f, 1f, Anim.Panel, PlayInAt + 0.1f);
            UiTween.Delay(LogoDropTime, () =>
            {
                if (!Framing) return;
                Squash(logoWrap);
                shownAt = Time.unscaledTime;
                idle?.Cancel();
                idle = UiTween.Loop(Tick);
            });
        }

        /// <summary>The idle loops, on one clock: wordmark bob, sunburst turn, sparkles, button pulse and shine.</summary>
        void Tick(float time)
        {
            Presentation.FramePacer.KeepAwake();
            // The wordmark's landing squash owns its scale for a moment; the bob only moves and tilts it.
            Anim.SetTranslate(logoWrap, 0f, -BobHeight * Mathf.Sin(time * Mathf.PI * 2f / BobPeriod));
            Anim.SetRotate(logoWrap, SwayDegrees * Mathf.Sin(time * Mathf.PI * 2f / SwayPeriod));
            Anim.SetRotate(rays, time * RaySpeed);
            Anim.SetScale(glow, 1f + 0.06f * Mathf.Sin(time * Mathf.PI * 2f / 3.6f));

            for (int i = 0; i < sparkles.Length; i++)
            {
                // Each sparkle twinkles: grows and spins in, then shrinks away, then rests.
                float phase = Mathf.Repeat(time / SparklePeriod + SparkleSpots[i].phase, 1f);
                float k = phase < 0.45f ? Mathf.Sin(phase / 0.45f * Mathf.PI) : 0f;
                Anim.SetScale(sparkles[i], k);
                Anim.SetRotate(sparkles[i], phase * 180f);
            }

            if (play.Interactable)
                Anim.SetScale(playWrap, 1f + PulseAmount * (0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2f / PulsePeriod)));
            float s = Mathf.Repeat(time, ShineEvery);
            float width = play.layout.width;
            if (float.IsNaN(width) || width <= 1f) width = 800f;
            float u = Mathf.Clamp01(s / ShineTime);
            Anim.SetTranslate(shine, Mathf.Lerp(-ShineWidth * 2f, width + ShineWidth, Easing.InOutCubic(u)), 0f);
        }

        void OnPlay()
        {
            if (!Framing) return;
            ctx.Fly.Burst(play.worldBound.center, 22, 300f);
            PlayClicked?.Invoke();
        }

        /// <summary>
        /// The hand-over to Home: the wordmark winds down a touch and flings up and away, the glow and
        /// sunburst blow out, the button drops and fades, the gear and shades fade; then the layer hides
        /// and <paramref name="done"/> runs. <see cref="Framing"/> is false from the moment this is
        /// called, so the camera can start gliding to the home framing at once.
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
            idle?.Cancel();
            idle = null;
            // A quick tap can land before things have settled in: stop the entrances first.
            logoIn?.Cancel();
            playIn?.Cancel();
            gearIn?.Cancel();

            var logoFrom = logoWrap.resolvedStyle.translate;
            float logoOpacity = logoWrap.resolvedStyle.opacity;
            UiTween.Run(LeaveTime, t =>
            {
                // InBack: a small dip before the fling.
                Anim.SetTranslate(logoWrap, 0f, Mathf.LerpUnclamped(logoFrom.y, logoFrom.y - 520f, Easing.InBack(t)));
                Anim.SetScale(logoWrap, Mathf.Lerp(1f, 1.12f, t));
                Anim.SetOpacity(logoWrap, logoOpacity * (1f - t * t));
            }, null);
            float haloOpacity = halo.resolvedStyle.opacity;
            UiTween.Run(LeaveTime, t =>
            {
                Anim.SetScale(halo, Mathf.Lerp(1f, 1.8f, t));
                Anim.SetOpacity(halo, haloOpacity * (1f - t));
            }, Easing.OutCubic);
            float playScale = playWrap.resolvedStyle.scale.value.x;
            float playOpacity = playWrap.resolvedStyle.opacity;
            UiTween.Run(Anim.Panel, t =>
            {
                Anim.SetTranslate(playWrap, 0f, 160f * t);
                Anim.SetScale(playWrap, Mathf.Lerp(playScale, 0.9f, t));
                Anim.SetOpacity(playWrap, playOpacity * (1f - t));
            }, Easing.InOutCubic, 0.05f);
            Anim.Fade(topRow, topRow.resolvedStyle.opacity, 0f, Anim.Small);
            Anim.Fade(bottomShade, 1f, 0f, LeaveTime + 0.1f);
            Anim.Fade(topShade, 1f, 0f, LeaveTime + 0.1f, 0f, () =>
            {
                visible = false;
                leaving = false;
                Ui.Show(layer, false);
                foreach (var e in new[] { header, logoWrap, halo, playWrap, topRow }) Reset(e);
                done?.Invoke();
            });
        }

        /// <summary>Hides at once with no animation (tools that skip straight into a level).</summary>
        public void HideNow()
        {
            if (!visible) return;
            visible = false;
            leaving = false;
            idle?.Cancel();
            idle = null;
            Ui.Show(layer, false);
        }

        static void Reset(VisualElement e)
        {
            Anim.ClearTransform(e);
            Anim.SetOpacity(e, 1f);
        }

        /// <summary>The landing: wide and low for a beat, then back to round.</summary>
        static void Squash(VisualElement e)
        {
            UiTween.Run(0.3f, t =>
            {
                float k = Mathf.Sin(t * Mathf.PI) * 0.07f * (1f - t);
                Anim.SetScale(e, 1f + k, 1f - k);
            }, null, 0f, () => Anim.SetScale(e, 1f));
        }

        /// <summary>Soft sunburst wedges that fade from their color at the center to clear at the tips.</summary>
        sealed class Sunburst : VisualElement
        {
            const int Wedges = 14;
            readonly Color color;

            public Sunburst(Color color)
            {
                this.color = color;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                if (r.width <= 0f || r.height <= 0f || float.IsNaN(r.width)) return;
                var center = r.center;
                float radius = Mathf.Min(r.width, r.height) * 0.5f;
                var clear = new Color(color.r, color.g, color.b, 0f);
                var mesh = context.Allocate(Wedges * 3, Wedges * 3);
                float step = Mathf.PI * 2f / Wedges;
                for (int i = 0; i < Wedges; i++)
                {
                    float a0 = i * step, a1 = a0 + step * 0.5f;
                    var tip = i % 2 == 0 ? color : UiPalette.A(color, color.a * 0.7f);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = tip });
                    mesh.SetNextVertex(new Vertex { position = Point(center, radius, a0), tint = clear });
                    mesh.SetNextVertex(new Vertex { position = Point(center, radius, a1), tint = clear });
                    mesh.SetNextIndex((ushort)(i * 3));
                    mesh.SetNextIndex((ushort)(i * 3 + 1));
                    mesh.SetNextIndex((ushort)(i * 3 + 2));
                }
            }

            static Vector3 Point(Vector2 center, float radius, float angle) =>
                new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, Vertex.nearZ);
        }

        /// <summary>A soft round glow: a disc that fades from its color at the center to clear at the rim.</summary>
        sealed class RadialGlow : VisualElement
        {
            const int Segments = 48;
            readonly Color color;

            public RadialGlow(Color color)
            {
                this.color = color;
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext context)
            {
                var r = contentRect;
                if (r.width <= 0f || r.height <= 0f || float.IsNaN(r.width)) return;
                var center = r.center;
                var clear = new Color(color.r, color.g, color.b, 0f);
                var mesh = context.Allocate(Segments + 1, Segments * 3);
                mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = color });
                for (int i = 0; i < Segments; i++)
                {
                    float a = i * Mathf.PI * 2f / Segments;
                    var p = center + new Vector2(Mathf.Cos(a) * r.width * 0.5f, Mathf.Sin(a) * r.height * 0.5f);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(p.x, p.y, Vertex.nearZ), tint = clear });
                }
                for (int i = 0; i < Segments; i++)
                {
                    mesh.SetNextIndex(0);
                    mesh.SetNextIndex((ushort)(1 + i));
                    mesh.SetNextIndex((ushort)(1 + (i + 1) % Segments));
                }
            }
        }
    }
}
