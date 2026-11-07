using System;
using UnityEngine;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// The level-start animation. The views arrive in their resting places; each one is moved to an
    /// offset and tweened back to exactly where it was: trucks roll in from the left with a soft
    /// overshoot and a brake dip, parcels drop in row by row (back row first) with a squash, and the
    /// dock pads pop up.
    /// </summary>
    public static class LevelIntro
    {
        /// <summary>Seconds after the level is built until the board takes input.</summary>
        public const float InputDelay = 0.45f;

        const float DropHeight = 5.5f;
        const float DropTime = 0.26f;
        const float DropStart = 0.06f;
        const float RowStagger = 0.025f;
        /// <summary>Tall boards compress the stagger so the front row still lands near <see cref="InputDelay"/>.</summary>
        const float MaxRowSpread = 0.15f;
        const float Squash = 0.18f;

        const float TruckOffset = 11f;
        const float TruckTime = 0.6f;
        const float TruckStagger = 0.07f;

        const float PadStart = 0.08f;
        const float PadStagger = 0.035f;

        static readonly Vector3 SquashAmount = new Vector3(0.16f, -0.24f, 0.16f);

        /// <summary>Parked out of shot while a delayed intro waits (above the camera, far up the road).</summary>
        const float ParkHeight = 60f;
        const float ParkDistance = 40f;

        /// <summary>
        /// Plays the intro. <paramref name="rowLanded"/> gets (row, rowCount) as each row touches down;
        /// <paramref name="leadTruckParked"/> fires when the first truck reaches the bay. With a
        /// <paramref name="delay"/> (the arrival reveal pans the camera first) the board waits out of
        /// shot and drops in as the camera settles.
        /// </summary>
        /// <returns>Seconds until input should be allowed, including <paramref name="delay"/>.</returns>
        public static float Play(LevelViews views, Action<int, int> rowLanded, Action<TruckView> leadTruckParked,
            float delay = 0f)
        {
            var tw = views.Tweener;
            delay = Mathf.Max(0f, delay);
            float now = tw.Now + delay;

            int rows = 0;
            foreach (var pv in views.Parcels) rows = Mathf.Max(rows, pv.Parcel.Cell.Y + 1);
            float stagger = rows > 1 ? Mathf.Min(RowStagger, MaxRowSpread / (rows - 1)) : 0f;

            foreach (var pv in views.Parcels)
            {
                // A hair of per-parcel jitter so a row doesn't land as one slab.
                float start = now + DropStart + pv.Parcel.Cell.Y * stagger + UnityEngine.Random.Range(0f, 0.012f);
                Drop(tw, pv, start, delay > 0f);
            }
            if (rowLanded != null)
            {
                for (int row = 0; row < rows; row++)
                {
                    int r = row;
                    tw.At(now + DropStart + row * stagger + DropTime, () => rowLanded(r, rows));
                }
            }

            for (int i = 0; i < views.Trucks.Count; i++)
            {
                var tv = views.Trucks[i];
                float arrive = RollIn(tw, tv, now + i * TruckStagger, delay > 0f);
                // The controller schedules loads and queue shifts after these; keep them honest.
                tv.MoveFreeAt = Mathf.Max(tv.MoveFreeAt, arrive);
                if (i == 0)
                {
                    tv.BayReadyAt = Mathf.Max(tv.BayReadyAt, arrive);
                    if (leadTruckParked != null) tw.At(arrive - 0.05f, () => leadTruckParked(tv));
                }
            }

            var dock = views.Dock;
            if (dock != null)
                for (int slot = 0; slot < dock.SlotCount; slot++)
                    dock.PopIn(tw, now + PadStart + slot * PadStagger, slot);

            return delay + InputDelay;
        }

        static void Drop(Tweener tw, ParcelView pv, float start, bool park)
        {
            var tr = pv.transform;
            var rest = tr.position;
            var from = rest + Vector3.up * DropHeight;
            tr.position = park ? rest + Vector3.up * ParkHeight : from;
            // Every step checks InYard: if the parcel is tapped mid-drop the controller owns it from then on.
            tw.Run(start, DropTime,
                t => { if (pv && pv.InYard) tr.position = Vector3.LerpUnclamped(from, rest, Ease.InCubic(t)); },
                null,
                () =>
                {
                    if (!pv || !pv.InYard) return;
                    tr.position = rest;
                    SquashInYard(tw, pv, tw.Now, Squash);
                });
        }

        /// <summary>A squash on the parcel that stops the moment it leaves the yard.</summary>
        public static void SquashInYard(Tweener tw, ParcelView pv, float start, float duration)
        {
            var tr = pv.transform;
            var rest = Layout.ParcelSize;
            tw.Run(start, duration,
                t =>
                {
                    if (!pv || !pv.InYard) return;
                    float k = Mathf.Sin(t * Mathf.PI) * (1f - t);
                    tr.localScale = rest + Vector3.Scale(rest, SquashAmount) * k;
                },
                null,
                () => { if (pv && pv.InYard) tr.localScale = rest; });
        }

        static float RollIn(Tweener tw, TruckView tv, float start, bool park)
        {
            var tr = tv.transform;
            var rest = tr.position;
            var from = rest + Vector3.left * TruckOffset;
            tr.position = park ? rest + Vector3.left * ParkDistance : from;
            float arrive = start + TruckTime;
            tw.Run(start, TruckTime,
                t => { if (tr) tr.position = Vector3.LerpUnclamped(from, rest, SoftBack(t)); },
                null,
                () => { if (tr) tr.position = rest; });

            // Nose dips as it brakes, then the body settles with a squash.
            var body = tv.Body;
            tw.Run(arrive - 0.18f, 0.45f,
                t => { if (body) body.localRotation = Quaternion.Euler(0f, 0f, -5f * Mathf.Sin(t * Mathf.PI) * (1f - t)); },
                null,
                () => { if (body) body.localRotation = Quaternion.identity; });
            tw.Punch(body, Vector3.one, new Vector3(0.06f, -0.12f, 0.06f), arrive - 0.04f, 0.25f);
            return arrive;
        }

        /// <summary>Like <see cref="Ease.OutBack"/> with a gentler (~4%) overshoot.</summary>
        static float SoftBack(float t)
        {
            const float c1 = 1.1f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
