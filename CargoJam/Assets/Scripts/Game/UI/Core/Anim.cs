using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>Inline transform helpers and the canned animations every screen shares.</summary>
    public static class Anim
    {
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

        /// <summary>Scales up from small with an overshoot and fades in.</summary>
        public static UiTween PopIn(VisualElement e, float delay = 0f, float duration = 0.38f, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetScale(e, Mathf.LerpUnclamped(0.4f, 1f, Easing.OutBack(t)));
                SetOpacity(e, Mathf.Clamp01(t * 3f));
            }, null, delay, done);
        }

        /// <summary>Shrinks a little and fades out.</summary>
        public static UiTween PopOut(VisualElement e, float duration = 0.18f, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                SetScale(e, Mathf.LerpUnclamped(1f, 0.7f, Easing.InBack(t)));
                SetOpacity(e, 1f - t);
            }, null, 0f, done);
        }

        public static UiTween Fade(VisualElement e, float from, float to, float duration, float delay = 0f, Action done = null)
        {
            return UiTween.Run(duration, t => SetOpacity(e, Mathf.Lerp(from, to, t)), Easing.OutCubic, delay, done);
        }

        /// <summary>A quick swell and settle, for "this changed" feedback.</summary>
        public static UiTween Punch(VisualElement e, float amount = 0.25f, float duration = 0.28f, float delay = 0f)
        {
            return UiTween.Run(duration, t => SetScale(e, 1f + amount * Mathf.Sin(t * Mathf.PI) * (1f - t * 0.3f)),
                null, delay, () => SetScale(e, 1f));
        }

        /// <summary>A horizontal "no" shake.</summary>
        public static UiTween Shake(VisualElement e, float amplitude = 18f, float duration = 0.4f)
        {
            return UiTween.Run(duration, t => SetTranslate(e, Mathf.Sin(t * Mathf.PI * 8f) * amplitude * (1f - t), 0f),
                null, 0f, () => e.style.translate = StyleKeyword.Null);
        }

        /// <summary>A gentle idle breathing scale, for the main call to action. Cancel the returned loop.</summary>
        public static UiTween Pulse(VisualElement e, float amount = 0.045f, float period = 1.1f)
        {
            return UiTween.Loop(time => SetScale(e, 1f + amount * (0.5f - 0.5f * Mathf.Cos(time * Mathf.PI * 2f / period))));
        }

        /// <summary>Slides in from an offset to rest with an overshoot.</summary>
        public static UiTween SlideIn(VisualElement e, float fromX, float fromY, float duration = 0.4f, float delay = 0f)
        {
            return UiTween.Run(duration, t =>
            {
                float k = Easing.OutBack(t);
                SetTranslate(e, Mathf.LerpUnclamped(fromX, 0f, k), Mathf.LerpUnclamped(fromY, 0f, k));
                SetOpacity(e, Mathf.Clamp01(t * 2.5f));
            }, null, delay);
        }

        /// <summary>Slides out to an offset and fades.</summary>
        public static UiTween SlideOut(VisualElement e, float toX, float toY, float duration = 0.25f, Action done = null)
        {
            return UiTween.Run(duration, t =>
            {
                float k = Easing.InCubic(t);
                SetTranslate(e, toX * k, toY * k);
                SetOpacity(e, 1f - t);
            }, null, 0f, done);
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
