using CargoJam.Meta;

namespace CargoJam.Game.Meta
{
    /// <summary>
    /// Loads and saves the player's <see cref="ProfileData"/> (PlayerPrefs + JsonUtility).
    /// CONTRACT for stream B; the UI stream calls these. Placeholder until stream B lands.
    /// </summary>
    public static class ProfileStore
    {
        /// <summary>The saved profile, or a fresh one (migrating the old debug level index if present).</summary>
        public static ProfileData Load() => new ProfileData();

        public static void Save(ProfileData data) { }

        /// <summary>Deletes the saved profile (debug "reset progress").</summary>
        public static void Reset() { }
    }
}
