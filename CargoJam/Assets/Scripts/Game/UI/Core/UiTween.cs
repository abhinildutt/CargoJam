using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Easing curves over t in 0..1. Design v3: OutQuint for everyday motion (no overshoot);
    /// <see cref="OutBack"/> (overshoot 1.2) is reserved for reward moments.
    /// </summary>
    public static class Easing
    {
        public static float Linear(float t) => t;

        public static float OutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Fast start, long soft settle, no overshoot: presses, panels, sheets.</summary>
        public static float OutQuint(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u * u * u;
        }

        public static float InCubic(float t) => t * t * t;

        public static float InQuad(float t) => t * t;

        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;

        public static float InOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;

        /// <summary>A gentle overshoot (1.2) for reward moments only: coins, chest, stamp, tier-up.</summary>
        public static float OutBack(float t) => Back(t, 1.2f);

        static float Back(float t, float c1)
        {
            float c3 = c1 + 1f, u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }

    /// <summary>
    /// A small time-based tween driven by the UI root's scheduler (unscaled time). Every tween runs
    /// on one always-attached <see cref="Clock"/> element, so animating an element that is being
    /// removed from the tree never stalls its completion callback.
    /// </summary>
    public sealed class UiTween
    {
        const long TickMs = 16;

        /// <summary>The element whose scheduler drives every tween (the UI root).</summary>
        public static VisualElement Clock { get; set; }

        readonly float start;
        readonly float duration;
        readonly Action<float> step;
        readonly Func<float, float> ease;
        readonly Action done;
        IVisualElementScheduledItem item;

        /// <summary>True once the tween finished or was cancelled.</summary>
        public bool Finished { get; private set; }

        UiTween(float delay, float duration, Action<float> step, Func<float, float> ease, Action done)
        {
            start = Time.unscaledTime + Mathf.Max(0f, delay);
            this.duration = Mathf.Max(0.0001f, duration);
            this.step = step;
            this.ease = ease ?? Easing.Linear;
            this.done = done;
        }

        /// <summary>
        /// Runs <paramref name="step"/> with the eased progress every frame for <paramref name="duration"/>
        /// seconds. The starting value is applied at once (even with a delay) so nothing flashes in
        /// its final state for a frame.
        /// </summary>
        public static UiTween Run(float duration, Action<float> step, Func<float, float> ease = null,
            float delay = 0f, Action done = null)
        {
            var tween = new UiTween(delay, duration, step, ease, done);
            tween.step?.Invoke(tween.ease(0f));
            if (Clock == null)
            {
                tween.Complete();
                return tween;
            }
            tween.item = Clock.schedule.Execute(tween.Tick).Every(TickMs);
            return tween;
        }

        /// <summary>Calls <paramref name="action"/> after a delay in seconds (unscaled).</summary>
        public static UiTween Delay(float seconds, Action action) => Run(seconds, null, null, 0f, action);

        /// <summary>
        /// Runs a callback every frame until cancelled; the argument is seconds since it started.
        /// Used for idle loops (pulses, bobbing hands, spinning rays).
        /// </summary>
        public static UiTween Loop(Action<float> step) => Run(float.MaxValue, null, null, 0f, null).Become(step);

        UiTween Become(Action<float> loopStep)
        {
            loopTick = loopStep;
            return this;
        }

        Action<float> loopTick;

        void Tick()
        {
            if (Finished) return;
            float elapsed = Time.unscaledTime - start;
            if (elapsed < 0f) return;
            if (loopTick != null)
            {
                loopTick(elapsed);
                return;
            }
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Complete();
                return;
            }
            step?.Invoke(ease(t));
        }

        void Complete()
        {
            if (Finished) return;
            Finished = true;
            item?.Pause();
            step?.Invoke(ease(1f));
            done?.Invoke();
        }

        /// <summary>Stops the tween where it is, without calling its completion callback.</summary>
        public void Cancel()
        {
            if (Finished) return;
            Finished = true;
            item?.Pause();
        }

        /// <summary>Jumps to the end state and runs the completion callback.</summary>
        public void Skip() => Complete();
    }
}
