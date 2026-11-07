using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Inline transform helpers and the canned animations every screen shares. Design v3 motion:
    /// small transitions 220 ms and panels 320 ms on OutQuint with no overshoot; the reward variants
    /// (480 ms, OutBack 1.2) are for coins, chest, stamp and tier-up only; at most one idle
    /// <see cref="Breathe"/> per screen.
    /// </summary>
    public static class Anim
    {
        public const float Small = 0.22f;
        public const float Panel = 0.32f;
        public const float Reward = 0.48f;

        public static void SetScale(VisualElement e, float s) => e.style.scale = new StyleScale(new Scale(new Vector2(s, s)));

        public static void SetScale(VisualElement e, float x, float y) => e.style.scale = new StyleScale(new Scale(new Vector2(x, y)));

        public static void SetTranslate(VisualElement e, float x, float y) => e.style.translate = new StyleTranslate(new Translate(x, y));

        public static void SetRotate(VisualElement e, float degrees) =>
            e.style.rotate = new StyleRotate(new Rotate(new Angle(degrees, AngleUnit.Degree)));

        public static void SetOpacity(VisualElement e, float a) => e.style.opacity = a;

        /// <summary>Hands the transform back to USS.</summary>
        public static void ClearTransform(VisualElement e)
        {
            e.style.scale = StyleKeyword.Null;
            e.style.translate = StyleKeyword.Null;
            e.style.rotate = StyleKeyword.Null;
        }

        /// <summary>Fades in while settling from 96% scale. No overshoot.</summary>
        public static UiTween PopIn(VisualElement e, float delay = 0f, float duration = Small, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetScale(e, Mathf.Lerp(0.96f, 1f, t));
                SetOpacity(e, t);
            }, Easing.OutQuint, delay, done);
        }

        /// <summary>The reward pop: grows from small with a gentle overshoot (OutBack 1.2).</summary>
        public static UiTween PopInReward(VisualElement e, float delay = 0f, float duration = Reward, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetScale(e, Mathf.LerpUnclamped(0.6f, 1f, Easing.OutBack(t)));
                SetOpacity(e, Mathf.Clamp01(t * 3f));
            }, null, delay, done);
        }

        /// <summary>Fades out while shrinking slightly.</summary>
        public static UiTween PopOut(VisualElement e, float duration = 0.18f, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetScale(e, Mathf.Lerp(1f, 0.96f, t));
                SetOpacity(e, 1f - t);
            }, Easing.OutCubic, 0f, done);
        }

        public static UiTween Fade(VisualElement e, float from, float to, float duration, float delay = 0f, Action done = null)
        {
            return UiTween.Run(duration, t => SetOpacity(e, Mathf.Lerp(from, to, t)), Easing.OutCubic, delay, done);
        }

        /// <summary>A quick swell and settle, for "this changed" feedback.</summary>
        public static UiTween Punch(VisualElement e, float amount = 0.12f, float duration = 0.26f, float delay = 0f)
        {
            return UiTween.Run(duration, t => SetScale(e, 1f + amount * Mathf.Sin(t * Mathf.PI)),
                null, delay, () => SetScale(e, 1f));
        }

        /// <summary>A short squash (wide and low) and recover, for a counter taking a hit.</summary>
        public static UiTween Squash(VisualElement e, float amount = 0.08f, float duration = 0.16f)
        {
            return UiTween.Run(duration, t =>
            {
                float k = Mathf.Sin(t * Mathf.PI) * amount;
                SetScale(e, 1f + k, 1f - k);
            }, null, 0f, () => SetScale(e, 1f));
        }

        /// <summary>A horizontal "no" shake.</summary>
        public static UiTween Shake(VisualElement e, float amplitude = 14f, float duration = 0.32f)
        {
            return UiTween.Run(duration, t => SetTranslate(e, Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t), 0f),
                null, 0f, () => e.style.translate = StyleKeyword.Null);
        }

        /// <summary>
        /// The one idle animation a screen may have: a slow 2% breathe on the main call to action.
        /// Cancel the returned loop when the screen hides.
        /// </summary>
        public static UiTween Breathe(VisualElement e, float amount = 0.02f, float period = 2.4f)
        {
            return UiTween.Loop(time => SetScale(e, 1f + amount * (0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2f / period))));
        }

        /// <summary>Slides in from an offset to rest (OutQuint, no overshoot).</summary>
        public static UiTween SlideIn(VisualElement e, float fromX, float fromY, float duration = Panel, float delay = 0f, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetTranslate(e, Mathf.Lerp(fromX, 0f, t), Mathf.Lerp(fromY, 0f, t));
                SetOpacity(e, Mathf.Clamp01(t * 2f));
            }, Easing.OutQuint, delay, done);
        }

        /// <summary>Slides out to an offset and fades.</summary>
        public static UiTween SlideOut(VisualElement e, float toX, float toY, float duration = Small, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetTranslate(e, toX * t, toY * t);
                SetOpacity(e, 1f - t);
            }, Easing.InQuad, 0f, done);
        }

        /// <summary>Rolls a label's number from one value to another.</summary>
        public static UiTween CountUp(Label label, int from, int to, float duration, float delay = 0f,
            string prefix = "", Action<int> onStep = null)
        {
            int last = int.MinValue;
            return UiTween.Run(duration, t =>
            {
                int v = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
                if (v == last) return;
                last = v;
                label.text = prefix + Format(v);
                onStep?.Invoke(v);
            }, Easing.OutCubic, delay);
        }

        /// <summary>Thousands separators, culture-independent ("1,240").</summary>
        public static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
