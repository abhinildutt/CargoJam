using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// A full-screen, non-blocking overlay for things that fly: coins into the counter, reward icons,
    /// and spark bursts. Positions are panel coordinates (the layer sits at the panel origin).
    /// </summary>
    public sealed class FlyLayer
    {
        static readonly Color[] SparkColors =
        {
            new Color(1f, 0.78f, 0.24f), new Color(1f, 1f, 1f), new Color(0.23f, 0.79f, 0.36f),
            new Color(0.29f, 0.56f, 0.94f), new Color(0.95f, 0.33f, 0.29f),
        };

        readonly VisualElement layer;
        readonly System.Random random = new System.Random();

        public FlyLayer(VisualElement layer)
        {
            this.layer = layer;
        }

        /// <summary>
        /// Coins burst out of <paramref name="from"/> and stream into <paramref name="counter"/>,
        /// ticking it up as each lands. The counter should be frozen by the caller beforehand.
        /// </summary>
        public void Coins(Vector2 from, CoinCounter counter, int total, int count = 10, float delay = 0f, Action done = null)
        {
            if (total <= 0 || counter == null)
            {
                done?.Invoke();
                return;
            }
            count = Mathf.Clamp(count, 1, Mathf.Max(1, total));
            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                int worth = total * (i + 1) / count - total * i / count;
                Fly(IconKind.Coin, from, counter.Target, 72f, delay + i * 0.055f, () =>
                {
                    counter.AddShown(worth);
                    UiFeedback.CoinTick(index);
                    if (++landed == count) done?.Invoke();
                });
            }
        }

        /// <summary>One icon pops out of a point, drifts, then swoops into a target element.</summary>
        public void Fly(IconKind kind, Vector2 from, VisualElement target, float size, float delay, Action landed)
        {
            var icon = new Icon(kind);
            icon.AddToClassList("cj-flyer");
            icon.style.width = size;
            icon.style.height = size;
            icon.style.opacity = 0f;
            layer.Add(icon);

            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float spread = 60f + (float)random.NextDouble() * 90f;
            var scatter = from + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spread;
            Vector2 half = Vector2.one * size * 0.5f;

            // Phase 1: burst outward. Phase 2: curve into the target (re-read in case it moved).
            UiTween.Run(0.28f, t =>
            {
                var p = Vector2.LerpUnclamped(from, scatter, t);
                Place(icon, p - half);
                Anim.SetScale(icon, Mathf.Lerp(0.3f, 1.1f, t));
                icon.style.opacity = 1f;
            }, Easing.OutCubic, delay, () =>
            {
                var end = target != null && target.panel != null ? target.worldBound.center : from;
                var control = new Vector2(Mathf.Lerp(scatter.x, end.x, 0.2f), Mathf.Min(scatter.y, end.y) - 120f);
                UiTween.Run(0.5f, t =>
                {
                    float u = 1f - t;
                    var p = u * u * scatter + 2f * u * t * control + t * t * end;
                    Place(icon, p - half);
                    Anim.SetScale(icon, Mathf.Lerp(1.1f, 0.65f, t));
                }, Easing.InCubic, 0f, () =>
                {
                    icon.RemoveFromHierarchy();
                    if (target != null) Anim.Punch(target, 0.3f, 0.2f);
                    landed?.Invoke();
                });
            });
        }

        /// <summary>A ring of sparks flying out and fading, for chest bursts and claims.</summary>
        public void Burst(Vector2 at, int count = 22, float distance = 260f, float delay = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                var spark = new VisualElement { pickingMode = PickingMode.Ignore };
                spark.AddToClassList("cj-spark");
                float s = 16f + (float)random.NextDouble() * 22f;
                spark.style.width = s;
                spark.style.height = s;
                spark.style.backgroundColor = SparkColors[i % SparkColors.Length];
                spark.style.opacity = 0f;
                layer.Add(spark);

                float angle = (i + (float)random.NextDouble() * 0.6f) / count * Mathf.PI * 2f;
                float dist = distance * (0.55f + (float)random.NextDouble() * 0.6f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                UiTween.Run(0.7f, t =>
                {
                    // A little gravity pulls the sparks down as they fade.
                    var p = at + dir * dist * Easing.OutCubic(t) + Vector2.up * 140f * t * t;
                    Place(spark, p - Vector2.one * s * 0.5f);
                    spark.style.opacity = 1f - t;
                    Anim.SetScale(spark, 1f - t * 0.5f);
                }, null, delay, spark.RemoveFromHierarchy);
            }
        }

        static void Place(VisualElement e, Vector2 topLeft) => Anim.SetTranslate(e, topLeft.x, topLeft.y);
    }
}
