#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CargoJam.Game.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    public enum Haptic
    {
        /// <summary>A parcel was picked up (and UI presses): a light tick.</summary>
        Tap,
        /// <summary>A held parcel jumped into a truck; small UI steps.</summary>
        Tick,
        /// <summary>A truck filled up and is leaving: a medium thud (stronger along a departure ladder).</summary>
        TruckFull,
        /// <summary>A tap that did nothing (blocked, or dock full).</summary>
        Nope,
        /// <summary>One holding slot left.</summary>
        Warning,
        Win,
        Fail,
        /// <summary>A new destination's postcard stamp lands.</summary>
        Arrival,
    }

    /// <summary>
    /// Haptic feedback. iOS uses UIKit's feedback generators through Plugins/iOS/CargoJamHaptics.mm.
    /// Android and the Editor are silent for now. Rapid repeats are thinned out: within
    /// <see cref="MinGap"/> of the last pulse, only a stronger kind gets through, so a chain of loads
    /// or callouts never buzzes continuously.
    /// </summary>
    public static class Haptics
    {
        /// <summary>Seconds (unscaled) during which a pulse of equal or lower rank is dropped.</summary>
        const float MinGap = 0.07f;

        static float lastAt = -1f;
        static int lastRank;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CargoJam_HapticsPrepare();
        [DllImport("__Internal")] static extern void CargoJam_HapticsImpact(int style, float intensity);
        [DllImport("__Internal")] static extern void CargoJam_HapticsNotify(int type);
        [DllImport("__Internal")] static extern void CargoJam_HapticsSelection();
#endif

        public static void Prepare()
        {
#if UNITY_IOS && !UNITY_EDITOR
            CargoJam_HapticsPrepare();
#endif
        }

        public static void Play(Haptic haptic) => Play(haptic, 1f);

        /// <param name="strength">0..1 scale for impact kinds (ignored by notifications and ticks).</param>
        public static void Play(Haptic haptic, float strength)
        {
            if (!Settings.HapticsOn) return;
            float now = Time.unscaledTime;
            int rank = Rank(haptic);
            if (lastAt >= 0f && now - lastAt < MinGap && rank <= lastRank) return;
            lastAt = now;
            lastRank = rank;
            strength = Mathf.Clamp01(strength);
#if UNITY_IOS && !UNITY_EDITOR
            // Impact styles: 0 light, 1 medium, 2 heavy, 3 soft, 4 rigid.
            switch (haptic)
            {
                case Haptic.Tap: CargoJam_HapticsImpact(0, 0.55f * strength); break;
                case Haptic.Tick: CargoJam_HapticsSelection(); break;
                case Haptic.TruckFull: CargoJam_HapticsImpact(1, Mathf.Lerp(0.45f, 1f, strength)); break;
                case Haptic.Nope: CargoJam_HapticsImpact(3, 0.6f); break;
                case Haptic.Warning: CargoJam_HapticsNotify(1); break;
                case Haptic.Win: CargoJam_HapticsNotify(0); break;
                case Haptic.Fail: CargoJam_HapticsNotify(2); break;
                case Haptic.Arrival: CargoJam_HapticsNotify(0); break;
            }
#endif
        }

        static int Rank(Haptic haptic) => haptic switch
        {
            Haptic.Tap => 0,
            Haptic.Tick => 0,
            Haptic.Nope => 1,
            Haptic.TruckFull => 2,
            Haptic.Warning => 3,
            _ => 4,
        };
    }
}
