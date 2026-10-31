using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// A destination's postcard illustration, built from flat icon shapes with Painter2D: Harbor
    /// Town's crane and containers, the farm's barn and hay, the canyon's mesas, Pine Ridge's peaks,
    /// Metro City's towers and Frost Port's lighthouse. Locked stops draw the same shapes as a flat
    /// silhouette. Drawn in a 100 x 56 design box scaled to the element's width.
    /// </summary>
    public sealed class PostcardArt : VisualElement
    {
        const float DesignW = 100f, DesignH = 56f;

        int stop;
        bool locked;

        public PostcardArt()
        {
            AddToClassList("cj-postcard__art");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(int stopIndex, bool isLocked)
        {
            if (stop == stopIndex && locked == isLocked) return;
            stop = stopIndex;
            locked = isLocked;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width <= 0f || r.height <= 0f || float.IsNaN(r.width)) return;
            var a = new Art(context.painter2D, r, locked);
            switch (((stop % 6) + 6) % 6)
            {
                case 0: Harbor(a); break;
                case 1: Farm(a); break;
                case 2: Canyon(a); break;
                case 3: Pines(a); break;
                case 4: Metro(a); break;
                default: Frost(a); break;
            }
        }

        static void Harbor(Art a)
        {
            a.Sky(UiPalette.Art.HarborSky);
            a.Box(0f, 32f, 100f, 12f, UiPalette.Art.HarborWater);
            a.Box(0f, 43f, 100f, 13f, UiPalette.Art.HarborQuay);
            var crane = UiPalette.Art.HarborCrane;
            a.Box(17f, 9f, 2.4f, 35f, crane);
            a.Box(6f, 9f, 36f, 2.2f, crane);
            a.Box(34f, 11f, 0.7f, 12f, crane);
            a.Box(31.5f, 22.5f, 5.5f, 2.5f, crane);
            var c = UiPalette.Art.Containers;
            a.Box(50f, 37f, 14f, 6.5f, c[0]);
            a.Box(64.5f, 37f, 14f, 6.5f, c[1]);
            a.Box(57f, 30f, 14f, 6.5f, c[2]);
            a.Box(79f, 37f, 14f, 6.5f, c[3]);
            a.Box(72f, 30f, 14f, 6.5f, c[0]);
        }

        static void Farm(Art a)
        {
            a.Sky(UiPalette.Art.FarmSky);
            a.Box(0f, 30f, 100f, 12f, UiPalette.Art.FarmFieldFar);
            a.Box(0f, 40f, 100f, 16f, UiPalette.Art.FarmField);
            a.Poly(UiPalette.Art.BarnRoof, 55f, 26f, 67f, 15f, 79f, 26f);
            a.Box(57f, 25f, 20f, 17f, UiPalette.Art.Barn);
            a.Box(63.5f, 32f, 7f, 10f, UiPalette.Art.BarnRoof);
            a.Circle(22f, 44f, 4.5f, UiPalette.Art.Hay);
            a.Circle(33f, 46f, 4.5f, UiPalette.Art.Hay);
            a.Box(88f, 30f, 1.8f, 10f, UiPalette.WoodDark);
            a.Circle(89f, 27f, 6.5f, UiPalette.Art.PineLight);
        }

        static void Canyon(Art a)
        {
            a.Sky(UiPalette.Art.CanyonSky);
            a.Poly(UiPalette.Art.MesaFar, 0f, 40f, 9f, 21f, 33f, 21f, 41f, 40f);
            a.Poly(UiPalette.Art.Mesa, 52f, 44f, 62f, 16f, 86f, 16f, 98f, 44f);
            a.Box(0f, 39f, 100f, 17f, UiPalette.Art.Sand);
            var g = UiPalette.Art.Cactus;
            a.Round(27f, 27f, 3.6f, 18f, 1.8f, g);
            a.Round(22.5f, 31f, 3f, 8f, 1.5f, g);
            a.Round(22.5f, 36.5f, 6f, 3f, 1.5f, g);
            a.Round(31f, 29f, 3f, 7f, 1.5f, g);
            a.Round(28f, 33.5f, 6f, 3f, 1.5f, g);
        }

        static void Pines(Art a)
        {
            a.Sky(UiPalette.Art.PineSky);
            a.Poly(UiPalette.Art.Peak, 8f, 40f, 33f, 8f, 58f, 40f);
            a.Poly(UiPalette.Art.Peak, 42f, 40f, 70f, 13f, 98f, 40f);
            a.Poly(UiPalette.Art.PeakSnow, 27.5f, 15f, 33f, 8f, 38.5f, 15f);
            a.Poly(UiPalette.Art.PeakSnow, 65f, 18f, 70f, 13f, 75f, 18f);
            a.Box(0f, 38f, 100f, 18f, UiPalette.Art.Meadow);
            Tree(a, 12f, 48f, 20f, UiPalette.Art.Pine);
            Tree(a, 22f, 50f, 16f, UiPalette.Art.PineLight);
            Tree(a, 78f, 49f, 18f, UiPalette.Art.PineLight);
            Tree(a, 89f, 48f, 22f, UiPalette.Art.Pine);
        }

        static void Tree(Art a, float x, float baseY, float h, Color c) =>
            a.Poly(c, x - h * 0.32f, baseY, x, baseY - h, x + h * 0.32f, baseY);

        static void Metro(Art a)
        {
            a.Sky(UiPalette.Art.MetroSky);
            var far = UiPalette.Art.TowerFar;
            a.Box(14f, 16f, 12f, 34f, far);
            a.Box(46f, 8f, 12f, 42f, far);
            a.Box(74f, 18f, 12f, 32f, far);
            var near = UiPalette.Art.Tower;
            a.Box(4f, 22f, 15f, 30f, near);
            a.Box(28f, 12f, 14f, 40f, near);
            a.Box(60f, 24f, 16f, 28f, near);
            a.Box(84f, 16f, 13f, 36f, near);
            var w = UiPalette.Art.Window;
            for (int i = 0; i < 4; i++)
            {
                a.Box(7f + (i % 2) * 6f, 26f + i * 5f, 3f, 2.5f, w);
                a.Box(31f + (i % 2) * 6f, 16f + i * 7f, 3f, 2.5f, w);
                a.Box(63f + ((i + 1) % 2) * 7f, 28f + i * 5f, 3f, 2.5f, w);
                a.Box(87f + (i % 2) * 5f, 20f + i * 7f, 3f, 2.5f, w);
            }
            a.Box(0f, 50f, 100f, 6f, UiPalette.Art.Street);
        }

        static void Frost(Art a)
        {
            a.Sky(UiPalette.Art.FrostSky);
            a.Box(0f, 32f, 100f, 12f, UiPalette.Art.Ice);
            a.Box(0f, 42f, 100f, 14f, UiPalette.Art.Snow);
            a.Circle(20f, 44f, 9f, UiPalette.Art.Snow);
            a.Circle(34f, 45f, 6f, UiPalette.Art.Snow);
            a.Poly(UiPalette.Art.Lighthouse, 69f, 44f, 72.5f, 14f, 79.5f, 14f, 83f, 44f);
            a.Box(71f, 25f, 10f, 4.5f, UiPalette.Art.LighthouseBand);
            a.Box(71.5f, 9.5f, 9f, 4.5f, UiPalette.Ink2);
            a.Box(74f, 10.5f, 4f, 2.5f, UiPalette.Art.Window);
            a.Box(40f, 35f, 14f, 9f, UiPalette.Art.Lighthouse);
            a.Poly(UiPalette.Ink2, 38.5f, 35.5f, 47f, 29f, 55.5f, 35.5f);
            a.Box(45.5f, 38f, 3.5f, 3f, UiPalette.Art.Window);
        }

        /// <summary>Draws in design units; in silhouette mode every shape takes one flat tone.</summary>
        readonly struct Art
        {
            readonly Painter2D p;
            readonly Rect r;
            readonly float k;
            readonly bool silhouette;

            public Art(Painter2D painter, Rect rect, bool silhouette)
            {
                p = painter;
                r = rect;
                k = rect.width / DesignW;
                this.silhouette = silhouette;
            }

            Vector2 P(float x, float y) => new Vector2(r.x + x * k, r.y + y * k);

            Color C(Color c) => silhouette ? UiPalette.Art.Silhouette : c;

            /// <summary>The sky fills the whole box (inset tone when locked).</summary>
            public void Sky(Color c)
            {
                p.fillColor = silhouette ? UiPalette.Surface2 : c;
                p.BeginPath();
                p.MoveTo(new Vector2(r.xMin, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.xMin, r.yMax));
                p.ClosePath();
                p.Fill();
            }

            public void Box(float x, float y, float w, float h, Color c)
            {
                // Bottom bands stretch to the element's real height (it may be taller than 56 units).
                float bottom = y + h >= DesignH - 0.01f ? (r.yMax - r.y) / k : y + h;
                Poly(c, x, y, x + w, y, x + w, bottom, x, bottom);
            }

            public void Round(float x, float y, float w, float h, float radius, Color c)
            {
                radius = Mathf.Min(radius, w * 0.5f, h * 0.5f);
                p.fillColor = C(c);
                p.BeginPath();
                p.MoveTo(P(x + radius, y));
                p.LineTo(P(x + w - radius, y));
                p.ArcTo(P(x + w, y), P(x + w, y + radius), radius * k);
                p.LineTo(P(x + w, y + h - radius));
                p.ArcTo(P(x + w, y + h), P(x + w - radius, y + h), radius * k);
                p.LineTo(P(x + radius, y + h));
                p.ArcTo(P(x, y + h), P(x, y + h - radius), radius * k);
                p.LineTo(P(x, y + radius));
                p.ArcTo(P(x, y), P(x + radius, y), radius * k);
                p.ClosePath();
                p.Fill();
            }

            public void Circle(float cx, float cy, float radius, Color c)
            {
                p.fillColor = C(c);
                p.BeginPath();
                p.Arc(P(cx, cy), radius * k, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
                p.ClosePath();
                p.Fill();
            }

            public void Poly(Color c, params float[] xy)
            {
                p.fillColor = C(c);
                p.BeginPath();
                p.MoveTo(P(xy[0], xy[1]));
                for (int i = 2; i + 1 < xy.Length; i += 2) p.LineTo(P(xy[i], xy[i + 1]));
                p.ClosePath();
                p.Fill();
            }
        }
    }

    /// <summary>The gold "postcard earned" stamp: a scalloped ring with a check, drawn with Painter2D.</summary>
    public sealed class PostcardStamp : VisualElement
    {
        public PostcardStamp()
        {
            AddToClassList("cj-stamp");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        static void Draw(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            if (r.width <= 0f || float.IsNaN(r.width)) return;
            var p = context.painter2D;
            var c = r.center;
            float R = Mathf.Min(r.width, r.height) * 0.5f;

            // Scalloped outer edge.
            p.fillColor = UiPalette.Gold;
            p.BeginPath();
            const int bumps = 18;
            for (int i = 0; i <= bumps * 4; i++)
            {
                float a = i / (float)(bumps * 4) * Mathf.PI * 2f;
                float rad = R * (0.93f + 0.07f * Mathf.Cos(a * bumps));
                var pt = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();

            p.fillColor = UiPalette.Surface;
            p.BeginPath();
            p.Arc(c, R * 0.72f, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.ClosePath();
            p.Fill();

            p.strokeColor = UiPalette.GoldDeep;
            p.lineWidth = R * 0.16f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(c + new Vector2(-0.36f, 0.02f) * R);
            p.LineTo(c + new Vector2(-0.1f, 0.28f) * R);
            p.LineTo(c + new Vector2(0.38f, -0.24f) * R);
            p.Stroke();
        }
    }
}
