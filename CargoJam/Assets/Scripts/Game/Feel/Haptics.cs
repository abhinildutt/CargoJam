#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using CargoJam.Game.Meta;

namespace CargoJam.Game.Feel
{
    public enum Haptic
    {
        /// <summary>A parcel was picked up.</summary>
        Tap,
        /// <summary>A held parcel jumped into a truck.</summary>
        Tick,
        /// <summary>A truck filled up and is leaving.</summary>
        TruckFull,
        /// <summary>A tap that did nothing (blocked, or dock full).</summary>
        Nope,
        /// <summary>One holding slot left.</summary>
        Warning,
        Win,
        Fail,
    }

    /// <summary>
    /// Haptic feedback. iOS uses UIKit's feedback generators through Plugins/iOS/CargoJamHaptics.mm.
    /// Android and the Editor are silent for now.
    /// </summary>
    public static class Haptics
    {
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

        public static void Play(Haptic haptic)
        {
            if (!Settings.HapticsOn) return;
#if UNITY_IOS && !UNITY_EDITOR
            switch (haptic)
            {
                case Haptic.Tap: CargoJam_HapticsImpact(0, 0.8f); break;
                case Haptic.Tick: CargoJam_HapticsSelection(); break;
                case Haptic.TruckFull: CargoJam_HapticsImpact(1, 1f); break;
                case Haptic.Nope: CargoJam_HapticsImpact(3, 0.6f); break;
                case Haptic.Warning: CargoJam_HapticsNotify(1); break;
                case Haptic.Win: CargoJam_HapticsNotify(0); break;
                case Haptic.Fail: CargoJam_HapticsNotify(2); break;
            }
#endif
        }
    }
}
