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
        // Outer to inner; the sum is how far the glow reaches in from the edge (panel units).
        static readonly float[] Widths = { 16f, 18f, 22f, 26f, 32f };
        static readonly float[] Alphas = { 0.62f, 0.40f, 0.25f, 0.13f, 0.05f };

        readonly VisualElement[] layers = new VisualElement[Widths.Length];
        readonly float tintAlpha;

        /// <param name="tintAlpha">Alpha of a flat full-screen tint under the edges (0 for none).</param>
        public EdgeVignette(Color color, float tintAlpha = 0f)
        {
            this.tintAlpha = tintAlpha;
            Fill(this);
            VisualElement parent = this;
            for (int i = 0; i < Widths.Length; i++)
            {
                var layer = new VisualElement();
                Fill(layer);
                layer.style.borderTopWidth = layer.style.borderBottomWidth =
                    layer.style.borderLeftWidth = layer.style.borderRightWidth = Widths[i];
                parent.Add(layer);
                layers[i] = layer;
                parent = layer;
            }
            SetColor(color);
            style.opacity = 0f;
            style.display = DisplayStyle.None;
        }

        /// <summary>Recolors every layer, keeping the alpha falloff.</summary>
        public void SetColor(Color color)
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var c = color;
                c.a = Alphas[i];
                var s = layers[i].style;
                s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = c;
            }
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
