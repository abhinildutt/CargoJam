using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Draws every <see cref="IconKind"/> into a 100x100 design box scaled to the element. Arcs are
    /// built as polylines so they don't depend on Painter2D's arc-direction conventions.
    /// Design v3: single-color icons are flat (ink or white, no drop shadow); the multi-color ones
    /// (coin, chest, flame, gift, star, truck) use the gold and palette tokens from <see cref="UiPalette"/>.
    /// </summary>
    static class IconPainter
    {
        static Color Dark => UiPalette.Ink;
        static Color CoinFace => UiPalette.Gold;
        static Color CoinRim => UiPalette.GoldDeep;
        static Color CoinShine => UiPalette.GoldLight;

        /// <summary>Maps design coordinates (0..100) into the element's content rect.</summary>
        readonly struct Frame
        {
            readonly Vector2 origin;
            readonly float scale;

            public Frame(Vector2 origin, float scale)
            {
                this.origin = origin;
                this.scale = scale;
            }

            public Vector2 P(float x, float y) => new Vector2(origin.x + x * scale, origin.y + y * scale);
            public float L(float v) => v * scale;
        }

        delegate void Shape(Painter2D p, Frame f, Color c);

        public static void Draw(Painter2D p, IconKind kind, Rect r, Color tint)
        {
            float size = Mathf.Min(r.width, r.height);
            var f = new Frame(new Vector2(r.x + (r.width - size) * 0.5f, r.y + (r.height - size) * 0.5f), size / 100f);
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            switch (kind)
            {
                case IconKind.Coin: Coin(p, f); break;
                case IconKind.Flame: Flame(p, f); break;
                case IconKind.Chest: Chest(p, f, false); break;
                case IconKind.ChestOpen: Chest(p, f, true); break;
                case IconKind.Hand: Hand(p, f); break;
                case IconKind.Gift: Gift(p, f); break;
                case IconKind.Star: Star(p, f, CoinFace, true); break;
                case IconKind.Rays: Rays(p, f, tint); break;
                case IconKind.Lock: Mono(p, f, tint, Lock); break;
                case IconKind.Gear: Mono(p, f, tint, Gear); break;
                case IconKind.Pause: Mono(p, f, tint, Pause); break;
                case IconKind.Undo: Mono(p, f, tint, Undo); break;
                case IconKind.Crane: Mono(p, f, tint, Crane); break;
                case IconKind.PlusSlot: Mono(p, f, tint, PlusSlot); break;
                case IconKind.Sound: Mono(p, f, tint, (q, g, c) => Speaker(q, g, c, true)); break;
                case IconKind.SoundOff: Mono(p, f, tint, (q, g, c) => Speaker(q, g, c, false)); break;
                case IconKind.Music: Mono(p, f, tint, Music); break;
                case IconKind.MusicOff: Mono(p, f, tint, Music); Slash(p, f); break;
                case IconKind.Haptics: Mono(p, f, tint, Phone); break;
                case IconKind.HapticsOff: Mono(p, f, tint, Phone); Slash(p, f); break;
                case IconKind.Check: Mono(p, f, tint, Check); break;
                case IconKind.Close: Mono(p, f, tint, Close); break;
                case IconKind.Home: Mono(p, f, tint, House); break;
                case IconKind.Restart: Mono(p, f, tint, Restart); break;
                case IconKind.Play: Mono(p, f, tint, PlayTriangle); break;
                case IconKind.Back: Mono(p, f, tint, (q, g, c) => Chevron(q, g, c, true)); break;
                case IconKind.ChevronRight: Mono(p, f, tint, (q, g, c) => Chevron(q, g, c, false)); break;
                case IconKind.Truck: Truck(p, f); break;
                case IconKind.Map: Mono(p, f, tint, MapPin); break;
            }
        }

        /// <summary>Single-colour icons are flat: one fill, no shade.</summary>
        static void Mono(Painter2D p, Frame f, Color tint, Shape shape) => shape(p, f, tint);

        // ---- Coloured icons -----------------------------------------------------------------------

        static void Coin(Painter2D p, Frame f)
        {
            Circle(p, f, 50f, 50f, 46f, CoinRim);
            Circle(p, f, 50f, 46f, 42f, CoinFace);
            // A raised bar in the middle reads as a coin even at small sizes.
            RoundRect(p, f, 42f, 24f, 16f, 44f, 8f, CoinRim);
            ArcStroke(p, f, 50f, 46f, 32f, 200f, 250f, 6f, CoinShine);
        }

        static void Flame(Painter2D p, Frame f)
        {
            p.fillColor = UiPalette.Gold;
            p.BeginPath();
            p.MoveTo(f.P(50f, 3f));
            p.BezierCurveTo(f.P(70f, 28f), f.P(88f, 44f), f.P(86f, 66f));
            p.BezierCurveTo(f.P(84f, 88f), f.P(68f, 98f), f.P(50f, 98f));
            p.BezierCurveTo(f.P(32f, 98f), f.P(16f, 88f), f.P(14f, 66f));
            p.BezierCurveTo(f.P(13f, 50f), f.P(24f, 40f), f.P(30f, 26f));
            p.BezierCurveTo(f.P(36f, 34f), f.P(38f, 40f), f.P(40f, 44f));
            p.BezierCurveTo(f.P(42f, 30f), f.P(46f, 16f), f.P(50f, 3f));
            p.ClosePath();
            p.Fill();

            p.fillColor = UiPalette.GoldLight;
            p.BeginPath();
            p.MoveTo(f.P(50f, 42f));
            p.BezierCurveTo(f.P(60f, 56f), f.P(70f, 64f), f.P(68f, 77f));
            p.BezierCurveTo(f.P(66f, 90f), f.P(58f, 94f), f.P(50f, 94f));
            p.BezierCurveTo(f.P(42f, 94f), f.P(32f, 90f), f.P(32f, 77f));
            p.BezierCurveTo(f.P(32f, 64f), f.P(42f, 56f), f.P(50f, 42f));
            p.ClosePath();
            p.Fill();
        }

        static void Chest(Painter2D p, Frame f, bool open)
        {
            var wood = UiPalette.Wood;
            var woodDark = UiPalette.WoodDark;
            var lid = UiPalette.WoodLight;
            RoundRect(p, f, 8f, 48f, 84f, 46f, 9f, woodDark);
            RoundRect(p, f, 10f, 46f, 80f, 44f, 8f, wood);
            if (open)
            {
                // Light spilling out of the open chest.
                RoundRect(p, f, 14f, 38f, 72f, 14f, 6f, UiPalette.GoldLight);
                RoundRect(p, f, 6f, 6f, 88f, 26f, 12f, woodDark);
                RoundRect(p, f, 8f, 4f, 84f, 24f, 12f, lid);
                RoundRect(p, f, 44f, 4f, 12f, 24f, 2f, CoinFace);
            }
            else
            {
                RoundRect(p, f, 8f, 18f, 84f, 34f, 14f, woodDark);
                RoundRect(p, f, 10f, 16f, 80f, 32f, 14f, lid);
                RoundRect(p, f, 10f, 44f, 80f, 7f, 2f, woodDark);
                RoundRect(p, f, 44f, 16f, 12f, 35f, 2f, CoinFace);
            }
            RoundRect(p, f, 44f, 50f, 12f, 40f, 2f, CoinFace);
            RoundRect(p, f, 39f, 46f, 22f, 20f, 5f, CoinRim);
            RoundRect(p, f, 41f, 47f, 18f, 17f, 4f, CoinFace);
            Circle(p, f, 50f, 55f, 3.5f, woodDark);
        }

        /// <summary>A pointing hand with the fingertip at the top-left (about x=41, y=4).</summary>
        static void Hand(Painter2D p, Frame f)
        {
            // Outline pass first, then the fill pass, so overlapping parts merge into one silhouette.
            for (int pass = 0; pass < 2; pass++)
            {
                var c = pass == 0 ? Dark : UiPalette.Surface;
                float g = pass == 0 ? 4f : 0f;
                RoundRect(p, f, 32f - g, 4f - g, 18f + g * 2, 56f + g * 2, 9f + g, c);
                RoundRect(p, f, 47f - g, 32f - g, 15f + g * 2, 30f + g * 2, 7.5f + g, c);
                RoundRect(p, f, 59f - g, 36f - g, 15f + g * 2, 28f + g * 2, 7.5f + g, c);
                RoundRect(p, f, 70f - g, 42f - g, 13f + g * 2, 24f + g * 2, 6.5f + g, c);
                RoundRect(p, f, 14f - g, 50f - g, 22f + g * 2, 30f + g * 2, 11f + g, c);
                RoundRect(p, f, 26f - g, 48f - g, 57f + g * 2, 46f + g * 2, 18f + g, c);
            }
        }

        static void Gift(Painter2D p, Frame f)
        {
            RoundRect(p, f, 12f, 46f, 76f, 48f, 7f, UiPalette.GiftWrapDark);
            RoundRect(p, f, 14f, 44f, 72f, 46f, 6f, UiPalette.GiftWrap);
            RoundRect(p, f, 8f, 30f, 84f, 20f, 6f, UiPalette.GiftWrap);
            RoundRect(p, f, 43f, 30f, 14f, 60f, 2f, CoinFace);
            Circle(p, f, 37f, 22f, 12f, CoinRim);
            Circle(p, f, 63f, 22f, 12f, CoinRim);
            Circle(p, f, 37f, 22f, 7f, CoinFace);
            Circle(p, f, 63f, 22f, 7f, CoinFace);
            Circle(p, f, 50f, 28f, 7f, CoinRim);
        }

        static void Star(Painter2D p, Frame f, Color c, bool rim)
        {
            if (rim) StarPath(p, f, 50f, 54f, 48f, 21f, CoinRim);
            StarPath(p, f, 50f, 50f, 48f, 21f, c);
        }

        static void StarPath(Painter2D p, Frame f, float cx, float cy, float outer, float inner, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            for (int i = 0; i < 10; i++)
            {
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                float rad = i % 2 == 0 ? outer : inner;
                var pt = f.P(cx + Mathf.Cos(a) * rad, cy + Mathf.Sin(a) * rad);
                if (i == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.ClosePath();
            p.Fill();
        }

        /// <summary>A sunburst of soft wedges, drawn behind rewards.</summary>
        static void Rays(Painter2D p, Frame f, Color tint)
        {
            const int count = 12;
            for (int i = 0; i < count; i++)
            {
                float a0 = (i * 360f / count) * Mathf.Deg2Rad;
                float a1 = a0 + (360f / count * 0.5f) * Mathf.Deg2Rad;
                p.fillColor = UiPalette.A(tint, tint.a * (i % 2 == 0 ? 0.55f : 0.35f));
                p.BeginPath();
                p.MoveTo(f.P(50f, 50f));
                p.LineTo(f.P(50f + Mathf.Cos(a0) * 70f, 50f + Mathf.Sin(a0) * 70f));
                p.LineTo(f.P(50f + Mathf.Cos(a1) * 70f, 50f + Mathf.Sin(a1) * 70f));
                p.ClosePath();
                p.Fill();
            }
        }

        // ---- Single-colour shapes -----------------------------------------------------------------

        static void Lock(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(11f);
            p.BeginPath();
            p.MoveTo(f.P(31f, 48f));
            p.LineTo(f.P(31f, 34f));
            ArcPoints(p, f, 50f, 34f, 19f, 180f, 360f, false);
            p.LineTo(f.P(69f, 48f));
            p.Stroke();
            RoundRect(p, f, 18f, 44f, 64f, 50f, 11f, c);
            if (c.a > 0.5f)
            {
                var hole = UiPalette.A(UiPalette.Ink, 0.35f);
                Circle(p, f, 50f, 63f, 7f, hole);
                RoundRect(p, f, 46.5f, 63f, 7f, 16f, 3f, hole);
            }
        }

        static void Gear(Painter2D p, Frame f, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            const int steps = 64;
            for (int k = 0; k < steps; k++)
            {
                float a = (k + 0.5f) * Mathf.PI * 2f / steps;
                float rad = (k / 4) % 2 == 0 ? 46f : 35f;
                var pt = f.P(50f + Mathf.Cos(a) * rad, 50f + Mathf.Sin(a) * rad);
                if (k == 0) p.MoveTo(pt);
                else p.LineTo(pt);
            }
            p.ClosePath();
            // The hole is a second sub-path, cut out by the even-odd rule.
            p.MoveTo(f.P(66f, 50f));
            ArcPoints(p, f, 50f, 50f, 16f, 0f, 360f, true);
            p.ClosePath();
            p.Fill(FillRule.OddEven);
        }

        static void Pause(Painter2D p, Frame f, Color c)
        {
            RoundRect(p, f, 24f, 16f, 19f, 68f, 7f, c);
            RoundRect(p, f, 57f, 16f, 19f, 68f, 7f, c);
        }

        static void Undo(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(13f);
            p.BeginPath();
            p.MoveTo(f.P(34f, 38f));
            p.LineTo(f.P(56f, 38f));
            p.BezierCurveTo(f.P(75f, 38f), f.P(87f, 50f), f.P(87f, 63f));
            p.BezierCurveTo(f.P(87f, 77f), f.P(75f, 88f), f.P(58f, 88f));
            p.LineTo(f.P(42f, 88f));
            p.Stroke();
            Poly(p, c, f.P(10f, 38f), f.P(40f, 14f), f.P(40f, 62f));
        }

        static void Restart(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(12f);
            p.BeginPath();
            ArcPoints(p, f, 50f, 54f, 30f, -40f, 230f, true);
            p.Stroke();
            // Arrowhead at the start of the arc, pointing back along it.
            float a = -40f * Mathf.Deg2Rad;
            var tip = new Vector2(50f + Mathf.Cos(a) * 30f, 54f + Mathf.Sin(a) * 30f);
            var dir = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            var normal = new Vector2(-dir.y, dir.x);
            var head = tip + dir * 15f;
            var b1 = tip - dir * 4f + normal * 14f;
            var b2 = tip - dir * 4f - normal * 14f;
            Poly(p, c, f.P(head.x, head.y), f.P(b1.x, b1.y), f.P(b2.x, b2.y));
        }

        static void Crane(Painter2D p, Frame f, Color c)
        {
            RoundRect(p, f, 24f, 4f, 52f, 15f, 6f, c);
            p.strokeColor = c;
            p.lineWidth = f.L(8f);
            p.BeginPath();
            p.MoveTo(f.P(50f, 18f));
            p.LineTo(f.P(50f, 46f));
            p.Stroke();
            Circle(p, f, 50f, 46f, 7f, c);
            p.lineWidth = f.L(11f);
            p.BeginPath();
            p.MoveTo(f.P(50f, 50f));
            ArcPoints(p, f, 50f, 68f, 18f, -90f, 200f, false);
            p.Stroke();
            Poly(p, c, f.P(24f, 56f), f.P(40f, 60f), f.P(30f, 72f));
        }

        static void PlusSlot(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(9f);
            RoundRectPath(p, f, 12f, 12f, 76f, 76f, 18f);
            p.Stroke();
            p.lineWidth = f.L(12f);
            Line(p, f, 50f, 32f, 50f, 68f);
            Line(p, f, 32f, 50f, 68f, 50f);
        }

        static void Speaker(Painter2D p, Frame f, Color c, bool on)
        {
            Poly(p, c, f.P(10f, 36f), f.P(28f, 36f), f.P(52f, 14f), f.P(52f, 86f), f.P(28f, 64f), f.P(10f, 64f));
            p.strokeColor = c;
            p.lineWidth = f.L(8f);
            if (on)
            {
                p.BeginPath();
                ArcPoints(p, f, 52f, 50f, 17f, -50f, 50f, true);
                p.Stroke();
                p.BeginPath();
                ArcPoints(p, f, 52f, 50f, 32f, -50f, 50f, true);
                p.Stroke();
            }
            else
            {
                p.lineWidth = f.L(10f);
                Line(p, f, 64f, 36f, 88f, 64f);
                Line(p, f, 88f, 36f, 64f, 64f);
            }
        }

        static void Music(Painter2D p, Frame f, Color c)
        {
            Circle(p, f, 28f, 76f, 13f, c);
            Circle(p, f, 72f, 68f, 13f, c);
            RoundRect(p, f, 34f, 22f, 8f, 56f, 3f, c);
            RoundRect(p, f, 78f, 12f, 8f, 58f, 3f, c);
            Poly(p, c, f.P(34f, 22f), f.P(86f, 10f), f.P(86f, 26f), f.P(34f, 38f));
        }

        static void Phone(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(8f);
            RoundRectPath(p, f, 32f, 12f, 36f, 76f, 10f);
            p.Stroke();
            RoundRect(p, f, 44f, 74f, 12f, 6f, 3f, c);
            p.lineWidth = f.L(6f);
            p.BeginPath();
            p.MoveTo(f.P(20f, 30f));
            p.LineTo(f.P(12f, 40f));
            p.LineTo(f.P(20f, 50f));
            p.LineTo(f.P(12f, 60f));
            p.LineTo(f.P(20f, 70f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(f.P(80f, 30f));
            p.LineTo(f.P(88f, 40f));
            p.LineTo(f.P(80f, 50f));
            p.LineTo(f.P(88f, 60f));
            p.LineTo(f.P(80f, 70f));
            p.Stroke();
        }

        static void Check(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(15f);
            p.BeginPath();
            p.MoveTo(f.P(16f, 52f));
            p.LineTo(f.P(40f, 76f));
            p.LineTo(f.P(86f, 26f));
            p.Stroke();
        }

        static void Close(Painter2D p, Frame f, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(15f);
            Line(p, f, 24f, 24f, 76f, 76f);
            Line(p, f, 76f, 24f, 24f, 76f);
        }

        static void House(Painter2D p, Frame f, Color c)
        {
            Poly(p, c, f.P(50f, 8f), f.P(94f, 48f), f.P(80f, 48f), f.P(80f, 90f), f.P(20f, 90f), f.P(20f, 48f), f.P(6f, 48f));
            RoundRect(p, f, 42f, 62f, 16f, 28f, 4f, UiPalette.A(UiPalette.Ink, c.a * 0.3f));
        }

        static void PlayTriangle(Painter2D p, Frame f, Color c) =>
            Poly(p, c, f.P(26f, 12f), f.P(86f, 50f), f.P(26f, 88f));

        static void Chevron(Painter2D p, Frame f, Color c, bool left)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(13f);
            p.BeginPath();
            if (left)
            {
                p.MoveTo(f.P(62f, 18f));
                p.LineTo(f.P(30f, 50f));
                p.LineTo(f.P(62f, 82f));
            }
            else
            {
                p.MoveTo(f.P(38f, 18f));
                p.LineTo(f.P(70f, 50f));
                p.LineTo(f.P(38f, 82f));
            }
            p.Stroke();
        }

        static void MapPin(Painter2D p, Frame f, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(f.P(50f, 94f));
            p.BezierCurveTo(f.P(34f, 72f), f.P(16f, 56f), f.P(16f, 38f));
            p.BezierCurveTo(f.P(16f, 18f), f.P(32f, 4f), f.P(50f, 4f));
            p.BezierCurveTo(f.P(68f, 4f), f.P(84f, 18f), f.P(84f, 38f));
            p.BezierCurveTo(f.P(84f, 56f), f.P(66f, 72f), f.P(50f, 94f));
            p.ClosePath();
            p.MoveTo(f.P(63f, 38f));
            ArcPoints(p, f, 50f, 38f, 13f, 0f, 360f, true);
            p.ClosePath();
            p.Fill(FillRule.OddEven);
        }

        /// <summary>A side-on flatbed truck facing right: the journey marker.</summary>
        static void Truck(Painter2D p, Frame f)
        {
            var body = UiPalette.Primary;
            var cab = UiPalette.PrimaryPress;
            // Bed with two parcels.
            RoundRect(p, f, 4f, 52f, 62f, 18f, 4f, body);
            RoundRect(p, f, 10f, 32f, 22f, 20f, 4f, UiPalette.Gold);
            RoundRect(p, f, 36f, 32f, 22f, 20f, 4f, UiPalette.GoldDeep);
            // Cab with a window.
            RoundRect(p, f, 64f, 30f, 32f, 40f, 8f, cab);
            RoundRect(p, f, 72f, 36f, 18f, 13f, 3f, UiPalette.Surface);
            // Wheels.
            Circle(p, f, 22f, 74f, 11f, UiPalette.Ink);
            Circle(p, f, 78f, 74f, 11f, UiPalette.Ink);
            Circle(p, f, 22f, 74f, 4f, UiPalette.Ink3);
            Circle(p, f, 78f, 74f, 4f, UiPalette.Ink3);
        }

        /// <summary>A red "off" slash across a toggle icon.</summary>
        static void Slash(Painter2D p, Frame f)
        {
            p.strokeColor = UiPalette.Danger;
            p.lineWidth = f.L(10f);
            Line(p, f, 12f, 12f, 88f, 88f);
        }

        // ---- Primitives ---------------------------------------------------------------------------

        static void Circle(Painter2D p, Frame f, float cx, float cy, float r, Color c)
        {
            p.fillColor = c;
            p.BeginPath();
            p.Arc(f.P(cx, cy), f.L(r), new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.ClosePath();
            p.Fill();
        }

        static void ArcStroke(Painter2D p, Frame f, float cx, float cy, float r, float a0, float a1, float width, Color c)
        {
            p.strokeColor = c;
            p.lineWidth = f.L(width);
            p.BeginPath();
            ArcPoints(p, f, cx, cy, r, a0, a1, true);
            p.Stroke();
        }

        /// <summary>
        /// Appends points along an arc (degrees, y down, so increasing angles turn clockwise on
        /// screen). <paramref name="moveFirst"/> starts a new sub-path at the first point.
        /// </summary>
        static void ArcPoints(Painter2D p, Frame f, float cx, float cy, float r, float a0, float a1, bool moveFirst)
        {
            int steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(a1 - a0) / 8f));
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)steps) * Mathf.Deg2Rad;
                var pt = f.P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                if (i == 0 && moveFirst) p.MoveTo(pt);
                else p.LineTo(pt);
            }
        }

        static void RoundRect(Painter2D p, Frame f, float x, float y, float w, float h, float r, Color c)
        {
            p.fillColor = c;
            RoundRectPath(p, f, x, y, w, h, r);
            p.Fill();
        }

        static void RoundRectPath(Painter2D p, Frame f, float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w * 0.5f, h * 0.5f);
            p.BeginPath();
            p.MoveTo(f.P(x + r, y));
            p.LineTo(f.P(x + w - r, y));
            p.ArcTo(f.P(x + w, y), f.P(x + w, y + r), f.L(r));
            p.LineTo(f.P(x + w, y + h - r));
            p.ArcTo(f.P(x + w, y + h), f.P(x + w - r, y + h), f.L(r));
            p.LineTo(f.P(x + r, y + h));
            p.ArcTo(f.P(x, y + h), f.P(x, y + h - r), f.L(r));
            p.LineTo(f.P(x, y + r));
            p.ArcTo(f.P(x, y), f.P(x + r, y), f.L(r));
            p.ClosePath();
        }

        static void Poly(Painter2D p, Color c, params Vector2[] points)
        {
            p.fillColor = c;
            p.BeginPath();
            p.MoveTo(points[0]);
            for (int i = 1; i < points.Length; i++) p.LineTo(points[i]);
            p.ClosePath();
            p.Fill();
        }

        static void Line(Painter2D p, Frame f, float x0, float y0, float x1, float y1)
        {
            p.BeginPath();
            p.MoveTo(f.P(x0, y0));
            p.LineTo(f.P(x1, y1));
            p.Stroke();
        }

    }
}
