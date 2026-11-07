using System;
using System.Collections.Generic;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The Journey screen: a vertical scrolling road (Painter2D) winding up past six postcard tiles,
    /// one per destination. Reached stops show their illustration, name and (when earned) the gold
    /// stamp; locked stops show a silhouette and "Level 31". A truck marker sits on the road in
    /// proportion to progress and drives forward when the screen opens after wins.
    /// </summary>
    public sealed class JourneyScreen
    {
        // Layout of the scroll content, in panel units. The column is centered in the panel.
        const float ColumnWidth = 960f;
        const float Spacing = 700f;
        const float TopPad = 520f;
        const float BottomPad = 420f;
        const float TileWidth = 540f;
        const float TileHeight = 440f;
        const float MarkerSize = 150f;

        readonly UiContext ctx;
        readonly VisualElement root;
        readonly ScrollView scroll;
        readonly VisualElement column;
        readonly JourneyRoad road;
        readonly VisualElement marker;
        readonly Label subtitle;
        readonly Tile[] tiles = new Tile[Journey.StopCount];
        UiTween drive;
        bool visible;

        public event Action BackClicked;

        sealed class Tile
        {
            public VisualElement Root;
            public PostcardArt Art;
            public Label Name;
            public Label Caption;
            public PostcardStamp Stamp;
        }

        public JourneyScreen(UiContext ctx, VisualElement layer)
        {
            this.ctx = ctx;
            root = Ui.El(layer, "cj-fill cj-journey", true);

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("cj-fill");
            scroll.AddToClassList("cj-journey__scroll");
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            root.Add(scroll);

            column = Ui.El(scroll.contentContainer, "cj-journey__column");
            column.style.width = ColumnWidth;
            column.style.height = ContentHeight;

            road = new JourneyRoad();
            road.AddToClassList("cj-fill");
            column.Add(road);
            var anchors = new Vector2[Journey.StopCount];
            for (int i = 0; i < anchors.Length; i++) anchors[i] = Anchor(i);
            road.SetAnchors(anchors, ContentHeight);

            for (int i = 0; i < tiles.Length; i++) tiles[i] = BuildTile(i);

            marker = Ui.El(column, "cj-journey__marker");
            Ui.El(marker, "cj-elevation");
            var disc = Ui.El(marker, "cj-journey__marker-disc");
            Ui.Icon(disc, IconKind.Truck, "cj-journey__marker-icon");

            // Header over the scroll: a solid band that fades into the map.
            var header = Ui.El(root, "cj-journey__header");
            var band = Ui.El(header, "cj-journey__band cj-safe-top");
            var row = Ui.El(band, "cj-journey__row");
            row.style.height = LevelHud.TopBarHeight;
            var back = new CandyButton(null, "cj-btn--quiet cj-btn--icon cj-journey__back", IconKind.Back);
            back.Icon.Tint = UiPalette.Ink;
            back.Clicked += () => BackClicked?.Invoke();
            row.Add(back);
            var titles = Ui.El(row, "cj-journey__titles");
            Ui.Text(titles, "Journey", "cj-t-title cj-c-ink");
            subtitle = Ui.Text(titles, "", "cj-t-caption cj-c-ink2");
            var fade = new GradientFill(UiPalette.Surface2, UiPalette.A(UiPalette.Surface2, 0f));
            fade.AddToClassList("cj-journey__fade");
            header.Add(fade);

            Ui.Show(layer, false);
            Ui.Show(root, false);
        }

        public bool Visible => visible;

        static float ContentHeight => TopPad + BottomPad + Spacing * (Journey.StopCount - 1);

        /// <summary>Where the road passes stop <paramref name="i"/> (stop 0 at the bottom; alternating sides).</summary>
        static Vector2 Anchor(int i) =>
            new Vector2(i % 2 == 0 ? 770f : 190f, ContentHeight - BottomPad - i * Spacing);

        Tile BuildTile(int i)
        {
            var a = Anchor(i);
            var t = new Tile { Root = Ui.El(column, "cj-postcard") };
            t.Root.style.width = TileWidth;
            t.Root.style.height = TileHeight;
            t.Root.style.left = i % 2 == 0 ? 40f : ColumnWidth - 40f - TileWidth;
            t.Root.style.top = a.y - TileHeight * 0.5f;
            Ui.El(t.Root, "cj-elevation");
            var card = Ui.El(t.Root, "cj-postcard__card");
            var frame = Ui.El(card, "cj-postcard__frame");
            t.Art = new PostcardArt();
            frame.Add(t.Art);
            var text = Ui.El(card, "cj-postcard__text");
            t.Name = Ui.Text(text, "", "cj-t-heading cj-c-ink");
            t.Caption = Ui.Text(text, "", "cj-t-caption cj-c-ink3");
            t.Stamp = new PostcardStamp();
            t.Stamp.AddToClassList("cj-postcard__stamp");
            t.Root.Add(t.Stamp);
            return t;
        }

        /// <summary>Progress along the journey in stops (0..StopCount): stop + step / LevelsPerStop.</summary>
        static float ProgressOf(int levelIndex) =>
            Journey.StopFor(levelIndex) + Journey.StepInStop(levelIndex) / (float)Journey.LevelsPerStop;

        /// <summary>
        /// Opens the screen. <paramref name="fromLevelIndex"/> is where the player was when the screen
        /// was last seen; when it is behind the current level the truck drives forward.
        /// </summary>
        public void Show(int fromLevelIndex)
        {
            var p = ctx.Progression;
            int index = p.LevelIndex;
            int stopNow = Journey.StopFor(index);
            bool lapDone = Journey.LegFor(index) >= Journey.StopCount;
            subtitle.text = $"{p.PostcardCount} of {Journey.StopCount} postcards";

            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                bool reached = lapDone || i <= stopNow || p.HasPostcard(i);
                t.Art.Set(i, !reached);
                t.Root.EnableInClassList("cj-postcard--locked", !reached);
                t.Root.EnableInClassList("cj-postcard--current", i == stopNow);
                t.Name.text = reached ? Journey.NameOf(i) : $"Level {UiText.FirstLevelOf(i, index)}";
                int first = UiText.FirstLevelOf(i, index);
                t.Caption.text = reached ? $"Levels {first}–{first + Journey.LevelsPerStop - 1}" : "Locked";
                Ui.Show(t.Stamp, p.HasPostcard(i));
                Anim.SetRotate(t.Stamp, i % 2 == 0 ? -12f : 10f);
            }

            float to = ProgressOf(index);
            float from = fromLevelIndex >= 0 && fromLevelIndex < index ? ProgressOf(fromLevelIndex) : to;
            if (from > to) from = to; // looped past the last stop: no drive backwards
            road.SetProgress(from);
            PlaceMarker(from);

            if (!visible)
            {
                visible = true;
                Ui.Show(root.parent, true);
                Ui.Show(root, true);
                Anim.SlideIn(root, 0f, 120f, Anim.Panel);
            }

            // Center the truck once the scroll view knows its size, then drive.
            scroll.schedule.Execute(() =>
            {
                float view = scroll.contentViewport.layout.height;
                if (float.IsNaN(view) || view <= 0f) view = ctx.Space.PanelSize.y;
                float y = road.PointAt(to).y - view * 0.55f;
                scroll.scrollOffset = new Vector2(0f, Mathf.Max(0f, y));
                Drive(from, to);
            }).StartingIn(30);
        }

        void Drive(float from, float to)
        {
            drive?.Cancel();
            if (to - from < 0.001f) return;
            float duration = Mathf.Clamp(0.6f + (to - from) * 0.8f, 0.6f, 1.8f);
            drive = UiTween.Run(duration, t =>
            {
                float progress = Mathf.Lerp(from, to, t);
                PlaceMarker(progress);
                road.SetProgress(progress);
            }, Easing.InOutCubic, 0.35f, () => Anim.Punch(marker, 0.08f));
        }

        void PlaceMarker(float progress)
        {
            var pt = road.PointAt(progress);
            marker.style.left = pt.x - MarkerSize * 0.5f;
            marker.style.top = pt.y - MarkerSize * 0.5f;
        }

        public void Hide(Action done = null)
        {
            if (!visible)
            {
                done?.Invoke();
                return;
            }
            visible = false;
            drive?.Skip();
            Anim.SlideOut(root, 0f, 120f, Anim.Small, () =>
            {
                if (!visible)
                {
                    Ui.Show(root, false);
                    Ui.Show(root.parent, false);
                    Anim.ClearTransform(root);
                    Anim.SetOpacity(root, 1f);
                }
                done?.Invoke();
            });
        }
    }

    /// <summary>
    /// The winding road behind the postcards: a wide band with a dashed center line. The stretch
    /// already driven gets a solid line in the primary color.
    /// </summary>
    public sealed class JourneyRoad : VisualElement
    {
        const float RoadWidth = 84f;
        const int Samples = 32;

        readonly List<Vector2> points = new List<Vector2>();
        float progress;

        public JourneyRoad()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        /// <summary>Builds the path through the stop anchors, extending past the first and last stops.</summary>
        public void SetAnchors(Vector2[] anchors, float height)
        {
            points.Clear();
            int n = anchors.Length;
            // Segment k runs from anchor k-1 to anchor k; segment 0 enters from below, segment n leaves above.
            var start = new Vector2(anchors[0].x, height + 40f);
            var end = new Vector2(anchors[n - 1].x < 480f ? 770f : 190f, anchors[n - 1].y - 700f);
            for (int k = 0; k <= n; k++)
            {
                var a = k == 0 ? start : anchors[k - 1];
                var b = k == n ? end : anchors[k];
                float half = (a.y - b.y) * 0.5f;
                var c1 = new Vector2(a.x, a.y - half);
                var c2 = new Vector2(b.x, b.y + half);
                for (int s = k == 0 ? 0 : 1; s <= Samples; s++)
                {
                    float t = s / (float)Samples;
                    float u = 1f - t;
                    points.Add(u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b);
                }
            }
            MarkDirtyRepaint();
        }

        /// <summary>The point on the road at a journey progress (0 = stop 0 .. StopCount = past the last stop).</summary>
        public Vector2 PointAt(float stopProgress)
        {
            if (points.Count == 0) return Vector2.zero;
            // Sample index: segment 0 (from below) is skipped; stop k sits at the end of segment k.
            float f = (stopProgress + 1f) * Samples;
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, points.Count - 1);
            int j = Mathf.Min(i + 1, points.Count - 1);
            return Vector2.Lerp(points[i], points[j], f - Mathf.Floor(f));
        }

        public void SetProgress(float stopProgress)
        {
            progress = stopProgress;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            if (points.Count < 2) return;
            var p = context.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;

            // Verge, then the road surface.
            Stroke(p, 0, points.Count - 1, RoadWidth + 20f, UiPalette.A(UiPalette.Ink, 0.06f));
            Stroke(p, 0, points.Count - 1, RoadWidth, UiPalette.Surface);

            // Driven stretch: a solid center line.
            int driven = Mathf.Clamp(Mathf.RoundToInt((progress + 1f) * Samples), 0, points.Count - 1);
            if (driven > 0) Stroke(p, 0, driven, 12f, UiPalette.Primary);

            // The rest: a dashed center line.
            p.strokeColor = UiPalette.A(UiPalette.Ink, 0.16f);
            p.lineWidth = 8f;
            const float dash = 26f, gap = 26f;
            float carried = 0f;
            bool on = true;
            for (int i = Mathf.Max(driven, 0); i < points.Count - 1; i++)
            {
                var a = points[i];
                var b = points[i + 1];
                float len = Vector2.Distance(a, b);
                float pos = 0f;
                while (pos < len)
                {
                    float left = (on ? dash : gap) - carried;
                    float step = Mathf.Min(left, len - pos);
                    if (on)
                    {
                        p.BeginPath();
                        p.MoveTo(Vector2.Lerp(a, b, pos / len));
                        p.LineTo(Vector2.Lerp(a, b, (pos + step) / len));
                        p.Stroke();
                    }
                    pos += step;
                    carried += step;
                    if (carried >= (on ? dash : gap) - 0.001f)
                    {
                        carried = 0f;
                        on = !on;
                    }
                }
            }
        }

        void Stroke(Painter2D p, int from, int to, float width, Color color)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.BeginPath();
            p.MoveTo(points[from]);
            for (int i = from + 1; i <= to; i++) p.LineTo(points[i]);
            p.Stroke();
        }
    }
}
