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
        public const int MinCoins = 8;
        public const int MaxCoins = 15;
        /// <summary>Seconds between consecutive coins leaving.</summary>
        public const float CoinStagger = 0.04f;
        const float CoinFlight = 0.55f;
        const float CoinSize = 64f;

        readonly VisualElement layer;
        readonly System.Random random = new System.Random();

        public FlyLayer(VisualElement layer)
        {
            this.layer = layer;
        }

        /// <summary>How many coins a reward of <paramref name="total"/> flies (8..15, never more than the total).</summary>
        public static int CoinCountFor(int total) => Mathf.Min(total, Mathf.Clamp(total / 6, MinCoins, MaxCoins));

        /// <summary>Seconds from launch until the last of <paramref name="count"/> coins lands.</summary>
        public static float CoinsDuration(int count) => Mathf.Max(0, count - 1) * CoinStagger + CoinFlight + 0.12f;

        /// <summary>
        /// Coins leave <paramref name="from"/> one by one (~40 ms apart) and arc into
        /// <paramref name="counter"/> along curved paths. Each landing squashes the counter, adds the
        /// coin's worth and plays a tick with a rising pitch. The caller freezes the counter first.
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
                FlyCoin(from, counter.Target, delay + i * CoinStagger, () =>
                {
                    counter.Land(worth);
                    UiFeedback.CoinTick(index);
                    if (++landed == count) done?.Invoke();
                });
            }
        }

        void FlyCoin(Vector2 from, VisualElement target, float delay, Action landed)
        {
            var icon = NewFlyer(IconKind.Coin, CoinSize);
            Vector2 half = Vector2.one * CoinSize * 0.5f;
            var start = from + new Vector2(Rand(-40f, 40f), Rand(-20f, 20f));
            // A sideways bow so the stream fans out instead of travelling in one line.
            float bow = Rand(-260f, 260f);

            // Pop out of the card (reward overshoot), then arc into the target (re-read in case it moved).
            UiTween.Run(0.12f, t =>
            {
                Place(icon, start - half);
                Anim.SetScale(icon, Mathf.LerpUnclamped(0.3f, 1f, Easing.OutBack(t)));
                icon.style.opacity = Mathf.Clamp01(t * 2f);
            }, null, delay, () =>
            {
                var end = target != null && target.panel != null ? target.worldBound.center : start;
                var c1 = start + new Vector2(bow, -220f);
                var c2 = end + new Vector2(bow * 0.4f, 260f);
                UiTween.Run(CoinFlight, t =>
                {
                    float u = 1f - t;
                    var p = u * u * u * start + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * end;
                    Place(icon, p - half);
                    Anim.SetScale(icon, Mathf.Lerp(1f, 0.7f, t));
                }, Easing.InOutCubic, 0f, () =>
                {
                    icon.RemoveFromHierarchy();
                    landed?.Invoke();
                });
            });
        }

        /// <summary>One icon pops out of a point, drifts, then swoops into a target element.</summary>
        public void Fly(IconKind kind, Vector2 from, VisualElement target, float size, float delay, Action landed)
        {
            var icon = NewFlyer(kind, size);
            Vector2 half = Vector2.one * size * 0.5f;
            UiTween.Run(0.6f, t =>
            {
                var end = target != null && target.panel != null ? target.worldBound.center : from;
                var control = new Vector2(Mathf.Lerp(from.x, end.x, 0.2f), Mathf.Min(from.y, end.y) - 160f);
                float u = 1f - t;
                Place(icon, u * u * from + 2f * u * t * control + t * t * end - half);
                icon.style.opacity = Mathf.Clamp01(t * 4f);
                Anim.SetScale(icon, Mathf.Lerp(1f, 0.7f, t));
            }, Easing.InOutCubic, delay, () =>
            {
                icon.RemoveFromHierarchy();
                if (target != null) Anim.Punch(target, 0.12f, 0.2f);
                landed?.Invoke();
            });
        }

        /// <summary>A ring of gold and white sparks flying out and fading, for chest bursts and claims.</summary>
        public void Burst(Vector2 at, int count = 18, float distance = 240f, float delay = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                var spark = new VisualElement { pickingMode = PickingMode.Ignore };
                spark.AddToClassList("cj-spark");
                float s = 14f + Rand(0f, 18f);
                spark.style.width = s;
                spark.style.height = s;
                spark.style.backgroundColor = UiPalette.Sparks[i % UiPalette.Sparks.Length];
                spark.style.opacity = 0f;
                layer.Add(spark);

                float angle = (i + Rand(0f, 0.6f)) / count * Mathf.PI * 2f;
                float dist = distance * (0.55f + Rand(0f, 0.6f));
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                UiTween.Run(0.7f, t =>
                {
                    // A little gravity pulls the sparks down as they fade.
                    var p = at + dir * dist * Easing.OutQuint(t) + Vector2.up * 120f * t * t;
                    Place(spark, p - Vector2.one * s * 0.5f);
                    spark.style.opacity = 1f - t;
                    Anim.SetScale(spark, 1f - t * 0.5f);
                }, null, delay, spark.RemoveFromHierarchy);
            }
        }

        Icon NewFlyer(IconKind kind, float size)
        {
            var icon = new Icon(kind);
            icon.AddToClassList("cj-flyer");
            icon.style.width = size;
            icon.style.height = size;
            icon.style.opacity = 0f;
            layer.Add(icon);
            return icon;
        }

        float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);

        static void Place(VisualElement e, Vector2 topLeft) => Anim.SetTranslate(e, topLeft.x, topLeft.y);
    }
}
