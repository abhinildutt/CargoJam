using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>Player preferences, kept in PlayerPrefs.</summary>
    public static class Settings
    {
        const string SoundKey = "cargojam.sound";
        const string HapticsKey = "cargojam.haptics";
        const string MusicKey = "cargojam.music";

        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set => Save(SoundKey, value);
        }

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set => Save(MusicKey, value);
        }

        public static bool HapticsOn
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set => Save(HapticsKey, value);
        }

        static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
