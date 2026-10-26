using System;
using CargoJam.Game.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The pause menu (Sound / Music / Haptics toggles, Resume, Restart, Home) and, without the
    /// level buttons, the home screen's settings popup.
    /// </summary>
    public sealed class PausePopup : Popup
    {
        readonly VisualElement levelButtons;
        readonly Label restartWarning;
        Action onResume;

        public event Action RestartClicked;
        public event Action HomeClicked;

        public PausePopup(UiContext ctx) : base(ctx, "Paused", "cj-ribbon--secondary", true)
        {
            var toggles = Ui.El(Body, "cj-card cj-settings");
            AddToggle(toggles, "Sound", IconKind.Sound, IconKind.SoundOff, Settings.SoundOn, v => Settings.SoundOn = v);
            AddToggle(toggles, "Music", IconKind.Music, IconKind.MusicOff, Settings.MusicOn, v =>
            {
                Settings.MusicOn = v;
                (ctx.Controller.Fx as Feel.FeelDirector)?.Music?.Refresh();
            });
            AddToggle(toggles, "Haptics", IconKind.Haptics, IconKind.HapticsOff, Settings.HapticsOn, v =>
            {
                Settings.HapticsOn = v;
                if (v) UiFeedback.Press();
            });

            levelButtons = Ui.El(Body, "cj-pause__buttons");
            var resume = new CandyButton("Resume", "cj-btn--primary cj-btn--big", IconKind.Play);
            resume.Clicked += () => Close(TakeResume());
            levelButtons.Add(resume);

            var row = Ui.El(levelButtons, "cj-pause__row");
            var restart = new CandyButton("Restart", "cj-btn--secondary cj-btn--medium cj-pause__half", IconKind.Restart);
            restart.Clicked += () => RestartClicked?.Invoke();
            row.Add(restart);
            var home = new CandyButton("Home", "cj-btn--secondary cj-btn--medium cj-pause__half", IconKind.Home);
            home.Clicked += () => HomeClicked?.Invoke();
            row.Add(home);
            restartWarning = Ui.Text(levelButtons, "", "cj-fail__warning");
        }

        /// <summary>Opens as the in-level pause menu.</summary>
        /// <param name="streakAtRisk">Win streak lost by restarting or leaving (0 for none).</param>
        /// <param name="resume">Runs after the popup closes via Resume or X.</param>
        public void ShowPause(int streakAtRisk, Action resume)
        {
            Title = "Paused";
            onResume = resume;
            Ui.Show(levelButtons, true);
            restartWarning.text = streakAtRisk > 0 ? UiText.StreakLoss(streakAtRisk) : "";
            Ui.Show(restartWarning, streakAtRisk > 0);
            OpenPopup();
        }

        /// <summary>Opens as the settings popup (toggles only).</summary>
        public void ShowSettings(Action closed)
        {
            Title = "Settings";
            onResume = closed;
            Ui.Show(levelButtons, false);
            OpenPopup();
        }

        protected override void OnCloseButton() => Close(TakeResume());

        Action TakeResume()
        {
            var r = onResume;
            onResume = null;
            return r;
        }

        static void AddToggle(VisualElement parent, string name, IconKind on, IconKind off, bool value, Action<bool> set)
        {
            var row = Ui.El(parent, "cj-settings__row");
            var icon = Ui.Icon(row, value ? on : off, "cj-settings__icon");
            icon.Tint = IconPainter.Hex(0x4A90F0);
            Ui.Text(row, name, "cj-settings__label");
            Ui.El(row, "cj-grow");
            var toggle = new ToggleSwitch(value);
            toggle.Changed += v =>
            {
                set(v);
                icon.Kind = v ? on : off;
                Anim.Punch(icon, 0.2f);
            };
            row.Add(toggle);
        }
    }
}
