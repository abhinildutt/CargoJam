using CargoJam.Game.Feel;
using UnityEngine;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Sound and haptics for UI moments. Kept in one place so a change to the FX stream's
    /// <see cref="Sfx"/> or <see cref="Haptic"/> names only touches this file.
    /// </summary>
    public static class UiFeedback
    {
        /// <summary>The game's audio service (null until the controller's FX layer exists).</summary>
        public static AudioService Audio { get; set; }

        public static void Press() => Haptics.Play(Haptic.Tap);

        public static void Click() => Audio?.Play(Sfx.Click);

        public static void Nope()
        {
            Audio?.Play(Sfx.Nope, 1f, 0.8f);
            Haptics.Play(Haptic.Nope);
        }

        /// <summary>One coin landing in the counter; the pitch climbs with each coin of a fly-in.</summary>
        public static void CoinTick(int index)
        {
            Audio?.Play(Sfx.Click, Mathf.Min(1f + index * 0.06f, 2f), 0.7f);
            if (index % 3 == 0) Haptics.Play(Haptic.Tick);
        }

        public static void Reward()
        {
            Audio?.Play(Sfx.Win, 1.15f, 0.8f);
            Haptics.Play(Haptic.Win);
        }

        public static void Whoosh() => Audio?.Play(Sfx.Arrive, 1.3f, 0.6f);

        public static void Warning() => Haptics.Play(Haptic.Warning);
    }
}
