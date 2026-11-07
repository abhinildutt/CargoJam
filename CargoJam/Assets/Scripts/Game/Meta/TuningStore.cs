using System.Collections.Generic;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>
    /// Keeps the last remote-config values (<see cref="Tuning"/>) in PlayerPrefs, so a launch uses
    /// what the previous session fetched even before (or without) a network response. A remote-config
    /// backend calls <see cref="Store"/> with each fetched set; the game calls <see cref="ApplyCached"/>
    /// once at launch, before the profile loads, because the starting coins are tunable.
    /// </summary>
    public static class TuningStore
    {
        const string Key = "cargojam.tuning";

        /// <summary>Applies the cached overrides (none on a fresh install).</summary>
        public static void ApplyCached()
        {
            var values = Tuning.Deserialize(PlayerPrefs.GetString(Key, ""));
            Tuning.Apply(values);
            if (values.Count > 0 && Debug.isDebugBuild)
                Debug.Log($"TuningStore: applied {values.Count} cached remote values.");
        }

        /// <summary>Applies <paramref name="values"/> now and caches them for the next launch.</summary>
        public static void Store(IEnumerable<KeyValuePair<string, string>> values)
        {
            Tuning.Apply(values);
            PlayerPrefs.SetString(Key, Tuning.Serialize(Tuning.Overrides));
            PlayerPrefs.Save();
        }

        /// <summary>Forgets the cached values and returns to the shipped defaults (debug).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Tuning.Reset();
        }
    }
}
