using CargoJam.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// The arrival reveal's destination card: a glass card in the upper third reading "Arrived" (gold
    /// caption) over the destination name (display type), with a postcard stamp that punches down on
    /// its corner. Built once, never takes input, animated by <see cref="Step"/> from the overlay's
    /// Update with no allocations.
    /// </summary>
    public sealed class ArrivalCard : VisualElement
    {
        /// <summary>Seconds after <see cref="Show"/> when the card starts rising in.</summary>
        public const float CardIn = 0.1f;
        /// <summary>Seconds after <see cref="Show"/> when the stamp starts its punch.</summary>
        public const float StampStart = 0.55f;
        /// <summary>Seconds after <see cref="Show"/> when the stamp hits the card (chime and haptic).</summary>
        public const float StampImpact = StampStart + StampPunch * 0.42f;
        /// <summary>Seconds after <see cref="Show"/> when the card starts to fade away.</summary>
        public const float FadeStart = 1.6f;
        /// <summary>Total lifetime in seconds.</summary>
        public const float Life = FadeStart + Fade;

        const float Rise = 0.32f;
        const float StampPunch = 0.48f;
        const float Fade = 0.3f;
        const float StampSize = 156f;
        const float StampRest = -9f;

        readonly VisualElement card;
        readonly Label caption;
        readonly Label title;
        readonly PostcardStamp stamp;
        float start = -1f;

        public ArrivalCard()
        {
            pickingMode = PickingMode.Ignore;
            var s = style;
            s.position = Position.Absolute;
            s.left = 0f;
            s.right = 0f;
            s.top = Length.Percent(17f);
            s.alignItems = Align.Center;
            s.display = DisplayStyle.None;

            card = new VisualElement { pickingMode = PickingMode.Ignore };
            var c = card.style;
            c.backgroundColor = UiPalette.Glass;
            c.borderTopLeftRadius = c.borderTopRightRadius = c.borderBottomLeftRadius = c.borderBottomRightRadius = 32f;
            var hairline = new Color(1f, 1f, 1f, 0.14f);
            c.borderTopWidth = c.borderBottomWidth = c.borderLeftWidth = c.borderRightWidth = 1f;
            c.borderTopColor = c.borderBottomColor = c.borderLeftColor = c.borderRightColor = hairline;
            c.paddingTop = 32f;
            c.paddingBottom = 40f;
            c.paddingLeft = c.paddingRight = 64f;
            c.alignItems = Align.Center;
            c.maxWidth = Length.Percent(88f);
            Add(card);

            caption = MakeLabel("Arrived", 32f, UiPalette.Gold);
            caption.style.letterSpacing = 3f;
            card.Add(caption);

            var rule = new VisualElement { pickingMode = PickingMode.Ignore };
            var r = rule.style;
            r.width = 72f;
            r.height = 4f;
            r.marginTop = 10f;
            r.marginBottom = 8f;
            r.backgroundColor = UiPalette.Gold;
            r.borderTopLeftRadius = r.borderTopRightRadius = r.borderBottomLeftRadius = r.borderBottomRightRadius = 2f;
            card.Add(rule);

            title = MakeLabel("", 88f, UiPalette.OnGlass);
            card.Add(title);

            stamp = new PostcardStamp(StampSize);
            var st = stamp.style;
            st.position = Position.Absolute;
            st.right = -StampSize * 0.38f;
            st.top = -StampSize * 0.42f;
            card.Add(stamp);
        }

        static Label MakeLabel(string text, float size, Color color)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            var s = label.style;
            s.fontSize = size;
            s.color = color;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.whiteSpace = WhiteSpace.NoWrap;
            s.marginTop = s.marginBottom = 0f;
            s.paddingTop = s.paddingBottom = 0f;
            FxType.Display(label);
            return label;
        }

        public bool Active => start >= 0f;

        /// <summary>Starts the card for <paramref name="destination"/> now.</summary>
        public void Show(string destination)
        {
            title.text = destination;
            start = Time.time;
            style.display = DisplayStyle.Flex;
            BringToFront();
            Step(start);
        }

        public void Hide()
        {
            start = -1f;
            style.display = DisplayStyle.None;
        }

        public void Step(float now)
        {
            if (start < 0f) return;
            float t = now - start;
            if (t >= Life)
            {
                Hide();
                return;
            }

            // Card: a calm OutQuint rise (panel motion), then a short fade up and away.
            float rise = OutQuint(Mathf.Clamp01((t - CardIn) / Rise));
            float alpha = rise;
            float lift = 28f * (1f - rise);
            if (t > FadeStart)
            {
                float k = Mathf.Clamp01((t - FadeStart) / Fade);
                alpha *= 1f - k * k;
                lift -= 16f * k;
            }
            var c = card.style;
            c.opacity = alpha;
            c.translate = new Translate(0f, lift);

            // Stamp: the reward moment, so it gets the overshoot. Drops from big and tilted, hits
            // the card at StampImpact, settles with OutBack(1.2).
            float u = (t - StampStart) / StampPunch;
            var s = stamp.style;
            if (u <= 0f)
            {
                s.opacity = 0f;
                return;
            }
            u = Mathf.Clamp01(u);
            float scale = Mathf.LerpUnclamped(1.9f, 1f, OutBack(u, 1.2f));
            s.opacity = Mathf.Clamp01(u * 6f);
            s.scale = new Scale(new Vector2(scale, scale));
            s.rotate = new Rotate(Angle.Degrees(Mathf.LerpUnclamped(-26f, StampRest, OutQuint(u))));
        }

        static float OutQuint(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u * u * u;
        }

        static float OutBack(float t, float overshoot)
        {
            float c3 = overshoot + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + overshoot * u * u;
        }
    }

    /// <summary>
    /// A postcard stamp: a cream sheet with a perforated (scalloped) edge, a gold-deep inner frame
    /// and a gold sun disc with a white star. Vector-drawn so it stays crisp at any scale.
    /// </summary>
    public sealed class PostcardStamp : VisualElement
    {
        const int Bumps = 7;

        public PostcardStamp(float size)
        {
            pickingMode = PickingMode.Ignore;
            style.width = style.height = size;
            style.opacity = 0f;
            generateVisualContent += Draw;
        }

        static void Draw(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size <= 1f) return;
            var p = ctx.painter2D;
            var c = r.center;
            float half = size * 0.44f;
            float x0 = c.x - half, x1 = c.x + half, y0 = c.y - half, y1 = c.y + half;
            float rad = half * 2f / (Bumps * 2f);

            // Soft underlay, offset like the v3 fake elevation.
            p.fillColor = new Color(12f / 255f, 18f / 255f, 28f / 255f, 0.18f);
            Scallops(p, x0, y0 + size * 0.04f, x1, y1 + size * 0.04f, rad);
            p.Fill();

            p.fillColor = UiPalette.Surface;
            Scallops(p, x0, y0, x1, y1, rad);
            p.Fill();

            float inset = half * 0.2f;
            p.strokeColor = UiPalette.GoldDeep;
            p.lineWidth = size * 0.022f;
            p.BeginPath();
            p.MoveTo(new Vector2(x0 + inset, y0 + inset));
            p.LineTo(new Vector2(x1 - inset, y0 + inset));
            p.LineTo(new Vector2(x1 - inset, y1 - inset));
            p.LineTo(new Vector2(x0 + inset, y1 - inset));
            p.ClosePath();
            p.Stroke();

            p.fillColor = UiPalette.Gold;
            p.BeginPath();
            p.Arc(c, half * 0.46f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Fill();

            p.fillColor = Color.white;
            p.BeginPath();
            float outer = half * 0.3f, inner = outer * 0.45f;
            for (int i = 0; i < 10; i++)
            {
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                float rr = i % 2 == 0 ? outer : inner;
                var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();
        }

        /// <summary>A rectangle whose edges are rows of outward half-circles (panel space is y-down).</summary>
        static void Scallops(Painter2D p, float x0, float y0, float x1, float y1, float rad)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x0, y0));
            for (int i = 0; i < Bumps; i++)
                p.Arc(new Vector2(x0 + rad + i * 2f * rad, y0), rad, Angle.Degrees(180f), Angle.Degrees(360f));
            for (int i = 0; i < Bumps; i++)
                p.Arc(new Vector2(x1, y0 + rad + i * 2f * rad), rad, Angle.Degrees(270f), Angle.Degrees(450f));
            for (int i = 0; i < Bumps; i++)
                p.Arc(new Vector2(x1 - rad - i * 2f * rad, y1), rad, Angle.Degrees(0f), Angle.Degrees(180f));
            for (int i = 0; i < Bumps; i++)
                p.Arc(new Vector2(x0, y1 - rad - i * 2f * rad), rad, Angle.Degrees(90f), Angle.Degrees(270f));
            p.ClosePath();
        }
    }
}
