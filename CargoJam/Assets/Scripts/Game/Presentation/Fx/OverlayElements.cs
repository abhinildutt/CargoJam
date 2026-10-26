using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// A screen-edge vignette faked with nested borders whose alpha falls off toward the middle, so it
    /// needs no texture or shader. Optionally tints the whole screen as well.
    /// </summary>
    public sealed class EdgeVignette : VisualElement
    {
        // How far the glow reaches in from each edge, as a fraction of the texture (stretched to the screen).
        const float ReachX = 0.16f;
        const float ReachY = 0.09f;
        const float PeakAlpha = 0.8f;

        static Texture2D falloff;

        readonly VisualElement glow;
        readonly float tintAlpha;

        /// <param name="tintAlpha">Alpha of a flat full-screen tint under the edges (0 for none).</param>
        public EdgeVignette(Color color, float tintAlpha = 0f)
        {
            this.tintAlpha = tintAlpha;
            Fill(this);
            glow = new VisualElement();
            Fill(glow);
            glow.style.backgroundImage = new StyleBackground(Falloff());
            glow.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            Add(glow);
            SetColor(color);
            style.opacity = 0f;
            style.display = DisplayStyle.None;
        }

        /// <summary>Recolors the edge glow and the flat tint.</summary>
        public void SetColor(Color color)
        {
            var c = color;
            c.a = 1f;
            glow.style.unityBackgroundImageTintColor = c;
            var tint = color;
            tint.a = tintAlpha;
            style.backgroundColor = tint;
        }

        /// <summary>Shows the vignette at an opacity; at zero it leaves the render tree entirely.</summary>
        public void SetIntensity(float value)
        {
            bool visible = value > 0.003f;
            style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible) style.opacity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// A white texture whose alpha fades smoothly in from every edge. One small texture, stretched
        /// to the screen, instead of stacked borders that showed visible steps.
        /// </summary>
        static Texture2D Falloff()
        {
            if (falloff != null) return falloff;
            const int w = 64, h = 128;
            falloff = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Vignette Falloff",
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float ex = Mathf.Clamp01(Mathf.Min(u, 1f - u) / ReachX);
                float ey = Mathf.Clamp01(Mathf.Min(v, 1f - v) / ReachY);
                // Strongest at the edges, eased so the inner boundary is invisible.
                float edge = 1f - Mathf.Min(ex, ey);
                float a = PeakAlpha * edge * edge * (3f - 2f * edge);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            falloff.SetPixels32(pixels);
            falloff.Apply(false, true);
            return falloff;
        }

        internal static void Fill(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            e.style.position = Position.Absolute;
            e.style.left = e.style.right = e.style.top = e.style.bottom = 0f;
        }
    }

    /// <summary>A green badge with a white tick, drawn with the vector API so it stays crisp.</summary>
    public sealed class CheckBadge : VisualElement
    {
        static readonly Color Fill = new Color(0.23f, 0.79f, 0.36f);
        static readonly Color Rim = new Color(0.16f, 0.59f, 0.27f);

        /// <summary>Creates a badge of <paramref name="size"/> panel units, centered on its left/top position.</summary>
        public CheckBadge(float size)
        {
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.width = style.height = size;
            style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            style.display = DisplayStyle.None;
            generateVisualContent += Draw;
        }

        static void Draw(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 1f) return;
            var c = r.center;
            var p = ctx.painter2D;

            p.fillColor = Fill;
            p.strokeColor = Rim;
            p.lineWidth = s * 0.07f;
            p.BeginPath();
            p.Arc(c, s * 0.46f, Angle.Degrees(0f), Angle.Degrees(360f));
            p.ClosePath();
            p.Fill();
            p.Stroke();

            p.strokeColor = Color.white;
            p.lineWidth = s * 0.13f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            // Panel space is y-down: dip to the bottom middle, then up to the top right.
            p.MoveTo(c + new Vector2(-0.22f, 0.01f) * s);
            p.LineTo(c + new Vector2(-0.07f, 0.17f) * s);
            p.LineTo(c + new Vector2(0.23f, -0.15f) * s);
            p.Stroke();
        }
    }
}
