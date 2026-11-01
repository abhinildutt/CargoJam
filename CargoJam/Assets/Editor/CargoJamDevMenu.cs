using System;
using CargoJam.Core;
using CargoJam.Game.Meta;
using CargoJam.Meta;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CargoJam.EditorTools
{
    /// <summary>
    /// Editor-only "Cargo Jam" menu for playtesting from a clean start: reset saved progress now, or
    /// automatically every time Play is pressed (on by default), or jump straight into Main from level 1
    /// or any other level (presets for each cargo debut). Sound, music and haptics settings are kept.
    /// </summary>
    [InitializeOnLoad]
    static class CargoJamDevMenu
    {
        const string ResetOnPlayPref = "CargoJam.ResetProgressOnPlay";
        const string ResetOnPlayItem = "Cargo Jam/Reset Progress On Every Play";
        const string PlayFromItem = "Cargo Jam/Play From Level…/";
        const string MainScene = "Assets/Scenes/Main.unity";
        /// <summary>GameFlow's first-launch splash flag, cleared too so a reset really starts from scratch.</summary>
        const string SplashSeenKey = "cj.splashSeen";
        /// <summary>The 0-based level the next Play starts at (set by the Play From Level items; -1 = none).</summary>
        const string StartLevelKey = "CargoJam.PlayFromLevel";

        /// <summary>Where each cargo kind debuts (level numbers, design v4 section 4).</summary>
        static readonly (int number, CargoKind kind)[] Debuts =
        {
            (5, CargoKind.Bollard),
            (7, CargoKind.Frost),
            (12, CargoKind.Mystery),
            (17, CargoKind.Express),
            (22, CargoKind.Lock),
            (27, CargoKind.Strap),
            (42, CargoKind.Gate),
        };

        /// <summary>A Play From request made during Play mode, run once the editor is back in Edit mode.</summary>
        static Action afterExit;

        static CargoJamDevMenu()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && afterExit != null)
                {
                    var action = afterExit;
                    afterExit = null;
                    // Next tick, so the editor has finished tearing Play mode down before a scene opens.
                    NextUpdate(action);
                    return;
                }
                if (state != PlayModeStateChange.ExitingEditMode) return;
                int start = SessionState.GetInt(StartLevelKey, -1);
                SessionState.EraseInt(StartLevelKey);
                // Runs after any reset-on-play, so the chosen level survives it.
                if (ResetOnPlay || start >= 0) ResetProgress();
                if (start >= 0) StartProfileAt(start);
            };
        }

        static bool ResetOnPlay
        {
            get => EditorPrefs.GetBool(ResetOnPlayPref, true);
            set => EditorPrefs.SetBool(ResetOnPlayPref, value);
        }

        /// <summary>
        /// Runs <paramref name="action"/> once on the next editor update. Unlike delayCall, the update loop
        /// keeps ticking while the editor is in the background (as it is when driven from the command line).
        /// </summary>
        static void NextUpdate(Action action)
        {
            EditorApplication.CallbackFunction once = null;
            once = () =>
            {
                EditorApplication.update -= once;
                action();
            };
            EditorApplication.update += once;
        }

        /// <summary>
        /// Runs <paramref name="action"/> in Edit mode: now, or after leaving Play mode (saving or opening
        /// a scene throws while playing, and leaving Play mode only happens at the end of the frame).
        /// A newer request made while leaving replaces an older one.
        /// </summary>
        static void InEditMode(Action action)
        {
            if (!EditorApplication.isPlaying)
            {
                action();
                return;
            }
            afterExit = action;
            EditorApplication.isPlaying = false;
        }

        [MenuItem("Cargo Jam/Play From Level 1", priority = 0)]
        static void PlayFromStart() => InEditMode(PlayFromStartNow);

        static void PlayFromStartNow()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainScene);
            ResetProgress();
            EditorApplication.isPlaying = true;
        }

        [MenuItem(PlayFromItem + "Choose Level…", priority = 1)]
        static void ChooseLevel() => PlayFromLevelWindow.Open();

        [MenuItem(PlayFromItem + "Level 5 (Bollard)", priority = 20)] static void Play5() => PlayFromLevel(5);
        [MenuItem(PlayFromItem + "Level 7 (Frozen crate)", priority = 21)] static void Play7() => PlayFromLevel(7);
        [MenuItem(PlayFromItem + "Level 12 (Mystery parcel)", priority = 22)] static void Play12() => PlayFromLevel(12);
        [MenuItem(PlayFromItem + "Level 17 (Express parcel)", priority = 23)] static void Play17() => PlayFromLevel(17);
        [MenuItem(PlayFromItem + "Level 22 (Padlocked parcel)", priority = 24)] static void Play22() => PlayFromLevel(22);
        [MenuItem(PlayFromItem + "Level 27 (Strapped pair)", priority = 25)] static void Play27() => PlayFromLevel(27);
        [MenuItem(PlayFromItem + "Level 42 (Customs gate)", priority = 26)] static void Play42() => PlayFromLevel(42);

        /// <summary>
        /// Plays Main from level <paramref name="number"/> (1-based) with a fresh profile, as if the player
        /// had just got there: boosters unlocked so far, cargo that debuted earlier already met (so the
        /// level's own debut card shows), the previous destination's arrival already seen. Like
        /// "Play From Level 1", this replaces the saved progress.
        /// </summary>
        public static void PlayFromLevel(int number) => InEditMode(() => PlayFromLevelNow(number));

        static void PlayFromLevelNow(int number)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MainScene);
            SessionState.SetInt(StartLevelKey, Mathf.Max(1, number) - 1);
            EditorApplication.isPlaying = true;
            // If Play mode is refused (compile errors, say), drop the request: left behind, it would
            // wipe the save on the next ordinary Play.
            NextUpdate(() =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode) SessionState.EraseInt(StartLevelKey);
            });
        }

        [MenuItem("Cargo Jam/Reset Progress Now", priority = 2)]
        static void ResetNow()
        {
            ResetProgress();
            Debug.Log("Cargo Jam: progress reset. The next Play starts at level 1 with a fresh profile.");
        }

        [MenuItem(ResetOnPlayItem, priority = 40)]
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

        /// <summary>Saves a fresh profile that starts at the 0-based <paramref name="index"/> (see <see cref="PlayFromLevel"/>).</summary>
        static void StartProfileAt(int index)
        {
            var data = ProfileStore.Load();
            data.levelIndex = index;
            data.lastArrivalShown = index > 0 ? Journey.LegFor(index - 1) : -1;
            data.cargoSeen = ProfileMigration.ResizeCargoSeen(data.cargoSeen);
            foreach (var (number, kind) in Debuts)
                if (number - 1 < index) data.cargoSeen[(int)kind] = true;
            ProfileStore.Save(data);
            // The IMGUI debug HUD (no UI in the scene) reads the old level key.
            CargoJam.Game.Meta.Progress.LevelIndex = index;
            PlayerPrefs.SetInt(SplashSeenKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"Cargo Jam: playing from level {index + 1} with a fresh profile.");
        }
    }

    /// <summary>The "Choose Level…" prompt: a level number and a Play button.</summary>
    sealed class PlayFromLevelWindow : EditorWindow
    {
        const string LastNumberPref = "CargoJam.PlayFromLevel.Last";
        int number;
        bool focused;

        public static void Open()
        {
            var window = GetWindow<PlayFromLevelWindow>(true, "Play From Level", true);
            window.number = EditorPrefs.GetInt(LastNumberPref, 1);
            window.focused = false;
            window.minSize = window.maxSize = new Vector2(260f, 64f);
        }

        void OnGUI()
        {
            // Read Return before the number field can use the event up.
            var e = Event.current;
            bool enter = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("Number");
            number = Mathf.Max(1, EditorGUILayout.IntField("Level number", number));
            if (!focused)
            {
                EditorGUI.FocusTextInControl("Number");
                focused = true;
            }
            if (!GUILayout.Button("Play") && !enter) return;
            int chosen = number;
            EditorPrefs.SetInt(LastNumberPref, chosen);
            Close();
            // Opening a scene and entering Play mode from inside OnGUI upsets the layout; do it next tick.
            EditorApplication.delayCall += () => CargoJamDevMenu.PlayFromLevel(chosen);
        }
    }
}
