using CargoJam.Game.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoJam.EditorTools
{
    /// <summary>
    /// Editor-only "Cargo Jam" menu for playtesting from a clean start: reset saved progress now, or
    /// automatically every time Play is pressed (on by default), or jump straight into Main from level 1.
    /// Sound, music and haptics settings are kept.
    /// </summary>
    [InitializeOnLoad]
    static class CargoJamDevMenu
    {
        const string ResetOnPlayPref = "CargoJam.ResetProgressOnPlay";
        const string ResetOnPlayItem = "Cargo Jam/Reset Progress On Every Play";
        const string MainScene = "Assets/Scenes/Main.unity";
        /// <summary>GameFlow's first-launch splash flag, cleared too so a reset really starts from scratch.</summary>
        const string SplashSeenKey = "cj.splashSeen";

        static CargoJamDevMenu()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode && ResetOnPlay) ResetProgress();
            };
        }

        static bool ResetOnPlay
        {
            get => EditorPrefs.GetBool(ResetOnPlayPref, true);
            set => EditorPrefs.SetBool(ResetOnPlayPref, value);
        }

        [MenuItem("Cargo Jam/Play From Level 1", priority = 0)]
        static void PlayFromStart()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainScene);
            ResetProgress();
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Cargo Jam/Reset Progress Now", priority = 1)]
        static void ResetNow()
        {
            ResetProgress();
            Debug.Log("Cargo Jam: progress reset. The next Play starts at level 1 with a fresh profile.");
        }

        [MenuItem(ResetOnPlayItem, priority = 20)]
        static void ToggleResetOnPlay() => ResetOnPlay = !ResetOnPlay;

        [MenuItem(ResetOnPlayItem, true)]
        static bool ToggleResetOnPlayValidate()
        {
            Menu.SetChecked(ResetOnPlayItem, ResetOnPlay);
            return true;
        }

        static void ResetProgress()
        {
            ProfileStore.Reset();
            PlayerPrefs.DeleteKey(SplashSeenKey);
            PlayerPrefs.Save();
        }
    }
}
