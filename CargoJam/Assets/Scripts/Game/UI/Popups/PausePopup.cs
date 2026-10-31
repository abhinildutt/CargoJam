using System;
using CargoJam.Game.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The pause card (Sound / Music / Haptics toggles, Resume, Restart, Home) and, without the
    /// level buttons, the home screen's settings card.
    /// </summary>
    public sealed class PausePopup : Popup
    {
        readonly VisualElement levelButtons;
        readonly Label restartWarning;
        Action onResume;

        public event Action RestartClicked;
        public event Action HomeClicked;

        public PausePopup(UiContext ctx) : base(ctx, "Paused", PopupKind.Card, true)
        {
            var toggles = Ui.El(Body, "cj-inset cj-settings");
            AddToggle(toggles, "Sound", IconKind.Sound, IconKind.SoundOff, Settings.SoundOn, v => Settings.SoundOn = v);
            Ui.El(toggles, "cj-settings__rule");
            AddToggle(toggles, "Music", IconKind.Music, IconKind.MusicOff, Settings.MusicOn, v =>
            {
                Settings.MusicOn = v;
                (ctx.Controller.Fx as Feel.FeelDirector)?.Music?.Refresh();
            });
            Ui.El(toggles, "cj-settings__rule");
            AddToggle(toggles, "Haptics", IconKind.Haptics, IconKind.HapticsOff, Settings.HapticsOn, v =>
            {
                Settings.HapticsOn = v;
                if (v) UiFeedback.Press();
            });

            levelButtons = Ui.El(Body, "cj-popup__actions");
            var resume = new CandyButton("Resume", "cj-btn--primary cj-btn--l cj-btn--wide");
            resume.Clicked += () => Close(TakeResume());
            levelButtons.Add(resume);

            var row = Ui.El(levelButtons, "cj-pause__row");
            var restart = new CandyButton("Restart", "cj-btn--quiet cj-btn--m cj-pause__half", IconKind.Restart);
            restart.Icon.Tint = UiPalette.Ink;
            restart.Clicked += () => RestartClicked?.Invoke();
            row.Add(restart);
            var home = new CandyButton("Home", "cj-btn--quiet cj-btn--m cj-pause__half", IconKind.Home);
            home.Icon.Tint = UiPalette.Ink;
            home.Clicked += () => HomeClicked?.Invoke();
            row.Add(home);
            restartWarning = Ui.Text(levelButtons, "", "cj-popup__note");
        }

        /// <summary>Opens as the in-level pause card.</summary>
        /// <param name="streakWarning">"Streak will drop to ×1.5" when restarting or leaving drops a tier, else null.</param>
        /// <param name="resume">Runs after the popup closes via Resume or ✕.</param>
        public void ShowPause(string streakWarning, Action resume)
        {
            Title = "Paused";
            onResume = resume;
            Ui.Show(levelButtons, true);
            restartWarning.text = streakWarning ?? "";
            Ui.Show(restartWarning, !string.IsNullOrEmpty(streakWarning));
            OpenPopup();
        }

        /// <summary>Opens as the settings card (toggles only).</summary>
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
            icon.Tint = UiPalette.Ink2;
            Ui.Text(row, name, "cj-t-heading cj-c-ink cj-settings__label");
            Ui.El(row, "cj-grow");
            var toggle = new ToggleSwitch(value);
            toggle.Changed += v =>
            {
                set(v);
                icon.Kind = v ? on : off;
            };
            row.Add(toggle);
        }
    }
}
