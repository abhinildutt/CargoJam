using System;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>
    /// Loads and saves the player's <see cref="ProfileData"/> as JSON (JsonUtility) in PlayerPrefs.
    /// Never throws on bad data: a corrupt save is replaced by a fresh profile with a warning.
    /// Wrap the loaded data in a <see cref="Progression"/>, which repairs out-of-range values.
    /// </summary>
    public static class ProfileStore
    {
        /// <summary>PlayerPrefs key holding the profile JSON.</summary>
        public const string ProfileKey = "cargojam.profile";

        /// <summary>The pre-meta debug level index (<see cref="Progress"/>), migrated into a first profile.</summary>
        const string LegacyLevelKey = "cargojam.level";

        /// <summary>The saved profile, or a fresh one (migrating the old debug level index if present).</summary>
        public static ProfileData Load()
        {
            if (!PlayerPrefs.HasKey(ProfileKey))
            {
                var fresh = Fresh();
                Save(fresh);
                return fresh;
            }

            try
            {
                var data = JsonUtility.FromJson<ProfileData>(PlayerPrefs.GetString(ProfileKey, ""));
                if (data != null) return data;
                Debug.LogWarning("ProfileStore: saved profile was empty; starting a fresh one.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ProfileStore: saved profile is corrupt ({e.Message}); starting a fresh one.");
            }
            // Keep the broken save out of the way rather than retrying it on every launch.
            var replacement = Fresh();
            Save(replacement);
            return replacement;
        }

        /// <summary>Writes the profile to PlayerPrefs and flushes it to disk.</summary>
        public static void Save(ProfileData data)
        {
            if (data == null) return;
            PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Saves <paramref name="progression"/>'s data whenever it changes. Returns the handler so it
        /// can be unsubscribed (<c>progression.Changed -= handler</c>).
        /// </summary>
        public static Action SaveOnChange(Progression progression)
        {
            if (progression == null) throw new ArgumentNullException(nameof(progression));
            Action handler = () => Save(progression.Data);
            progression.Changed += handler;
            return handler;
        }

        /// <summary>Deletes the saved profile (debug "reset progress"), including the legacy level index.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.DeleteKey(LegacyLevelKey);
            PlayerPrefs.Save();
        }

        static ProfileData Fresh()
        {
            var data = new ProfileData();
            if (PlayerPrefs.HasKey(LegacyLevelKey))
                data.levelIndex = Math.Max(0, PlayerPrefs.GetInt(LegacyLevelKey, 0));
            return data;
        }
    }
}
