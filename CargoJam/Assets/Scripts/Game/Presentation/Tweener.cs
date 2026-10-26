using System;
using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    public static class Ease
    {
        public static float Linear(float t) => t;
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }

    /// <summary>
    /// Minimal time-scheduled tweens. Every tween has an absolute start time, so the controller can
    /// line animations up against each other (a parcel lands, then its truck leaves) while the rules
    /// have already moved on. Start values are captured when a tween begins, not when it is queued.
    /// </summary>
    public sealed class Tweener : MonoBehaviour
    {
        sealed class Tween
        {
            public float Start;
            public float Duration;
            public Action OnBegin;
            public Action<float> Step;
            public Action OnDone;
            public bool Begun;
            public bool Finished;
        }

        readonly List<Tween> active = new List<Tween>();

        public float Now => Time.time;

        public void Run(float start, float duration, Action<float> step, Action onBegin = null, Action onDone = null)
        {
            active.Add(new Tween { Start = start, Duration = duration, Step = step, OnBegin = onBegin, OnDone = onDone });
        }

        /// <summary>Runs <paramref name="action"/> once at <paramref name="time"/>.</summary>
        public void At(float time, Action action) => Run(time, 0f, null, null, action);

        public void Clear() => active.Clear();

        void Update()
        {
            float now = Time.time;
            // Callbacks may queue more tweens; those are picked up in this same pass.
            for (int i = 0; i < active.Count; i++)
            {
                var tw = active[i];
                if (tw.Finished || now < tw.Start) continue;
                if (!tw.Begun)
                {
                    tw.Begun = true;
                    tw.OnBegin?.Invoke();
                }
                float t = tw.Duration <= 0f ? 1f : Mathf.Clamp01((now - tw.Start) / tw.Duration);
                tw.Step?.Invoke(t);
                if (t >= 1f)
                {
                    tw.Finished = true;
                    tw.OnDone?.Invoke();
                }
            }
            active.RemoveAll(tw => tw.Finished);
        }

        // ---- Transform helpers -------------------------------------------------------------

        public void MoveTo(Transform tr, Vector3 to, float start, float duration, Func<float, float> ease, Action onDone = null)
        {
            Vector3 from = default;
            Run(start, duration,
                t => { if (tr) tr.position = Vector3.LerpUnclamped(from, to, ease(t)); },
                () => { if (tr) from = tr.position; },
                onDone);
        }

        /// <summary>Slides along a polyline at constant speed, easing out at the end.</summary>
        public void MoveAlong(Transform tr, IReadOnlyList<Vector3> points, float start, float duration)
        {
            var pts = new List<Vector3>(points.Count + 1);
            float[] lengths = null;
            float total = 0f;
            Run(start, duration,
                t =>
                {
                    if (!tr || pts.Count < 2) return;
                    float d = Ease.OutCubic(t) * total;
                    for (int i = 1; i < pts.Count; i++)
                    {
                        if (d <= lengths[i] || i == pts.Count - 1)
                        {
                            float seg = lengths[i] - lengths[i - 1];
                            float k = seg > 0f ? (d - lengths[i - 1]) / seg : 1f;
                            tr.position = Vector3.Lerp(pts[i - 1], pts[i], k);
                            return;
                        }
                    }
                },
                () =>
                {
                    if (!tr) return;
                    pts.Add(tr.position);
                    pts.AddRange(points);
                    lengths = new float[pts.Count];
                    for (int i = 1; i < pts.Count; i++)
                    {
                        total += Vector3.Distance(pts[i - 1], pts[i]);
                        lengths[i] = total;
                    }
                });
        }

        /// <summary>A hop to <paramref name="to"/> with a parabolic height, scaling along the way.</summary>
        public void Arc(Transform tr, Vector3 to, float height, Vector3 scaleTo, float start, float duration, Action onDone = null)
        {
            Vector3 from = default, scaleFrom = default;
            Run(start, duration,
                t =>
                {
                    if (!tr) return;
                    float e = Ease.InOutQuad(t);
                    var p = Vector3.LerpUnclamped(from, to, e);
                    p.y += 4f * height * t * (1f - t);
                    tr.position = p;
                    tr.localScale = Vector3.LerpUnclamped(scaleFrom, scaleTo, e);
                },
                () =>
                {
                    if (!tr) return;
                    from = tr.position;
                    scaleFrom = tr.localScale;
                },
                onDone);
        }

        /// <summary>
        /// A quick squash-and-recover around <paramref name="rest"/>. Taking the rest scale explicitly
        /// means overlapping punches always settle back to it instead of drifting.
        /// </summary>
        public void Punch(Transform tr, Vector3 rest, Vector3 amount, float start, float duration)
        {
            Run(start, duration,
                t =>
                {
                    if (!tr) return;
                    float k = Mathf.Sin(t * Mathf.PI) * (1f - t);
                    tr.localScale = rest + Vector3.Scale(rest, amount) * k;
                },
                null,
                () => { if (tr) tr.localScale = rest; });
        }

        /// <summary>A side-to-side wobble around <paramref name="origin"/>, ending there.</summary>
        public void Shake(Transform tr, Vector3 origin, float amplitude, float start, float duration)
        {
            Run(start, duration,
                t =>
                {
                    if (!tr) return;
                    tr.position = origin + Vector3.right * (Mathf.Sin(t * Mathf.PI * 6f) * amplitude * (1f - t));
                },
                null,
                () => { if (tr) tr.position = origin; });
        }
    }
}
