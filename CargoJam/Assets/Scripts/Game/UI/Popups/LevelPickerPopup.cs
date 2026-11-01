using System;
using CargoJam.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Testing aid, in the editor and development builds only (<see cref="Enabled"/>): opened from the
    /// home screen's "Levels" button, it picks any level with −10/−1/+1/+10 steps or a tap on a cargo
    /// debut, then plays it as if the player had just got there (see <c>Progression.JumpTo</c>).
    /// </summary>
    public sealed class LevelPickerPopup : Popup
    {
        /// <summary>Where each cargo kind debuts (1-based level numbers, design v5: one a level, 4 to 10).</summary>
        public static readonly (int number, CargoKind kind)[] Debuts =
        {
            (4, CargoKind.Bollard),
            (5, CargoKind.Frost),
            (6, CargoKind.Mystery),
            (7, CargoKind.Lock),
            (8, CargoKind.Strap),
            (9, CargoKind.Express),
            (10, CargoKind.Gate),
        };

        /// <summary>Whether the picker exists in this build: the editor and development builds.</summary>
        public static bool Enabled => Application.isEditor || Debug.isDebugBuild;

        readonly Label number;
        readonly Label detail;
        int level = 1;
        int levelCount = 1;
        Action<int> play;

        public LevelPickerPopup(UiContext ctx) : base(ctx, "Play a level", PopupKind.Card, true)
        {
            number = Ui.Text(Body, "1", "cj-t-display cj-c-ink cj-picker__number");
            detail = Ui.Text(Body, "", "cj-popup__text cj-picker__detail");

            var steps = Ui.El(Body, "cj-pause__row cj-picker__steps");
            foreach (int step in new[] { -10, -1, 1, 10 })
            {
                var button = new CandyButton(step > 0 ? "+" + step : "−" + -step, "cj-btn--quiet cj-btn--s cj-pause__half");
                button.Clicked += () => Set(level + step);
                steps.Add(button);
            }

            Ui.Text(Body, "New cargo", "cj-t-label cj-c-ink2 cj-picker__caption");
            var grid = Ui.El(Body, "cj-picker__grid");
            foreach (var (n, kind) in Debuts)
            {
                var chip = new CandyButton($"{n} {UiText.CargoName(kind)}", "cj-btn--quiet cj-btn--s cj-picker__chip", UiText.CargoIcon(kind));
                chip.Icon.Tint = UiPalette.Ink;
                chip.Clicked += () => Set(n);
                grid.Add(chip);
            }

            var actions = Ui.El(Body, "cj-popup__actions");
            var go = new CandyButton("Play", "cj-btn--primary cj-btn--l cj-btn--wide");
            go.Clicked += () =>
            {
                var after = play;
                int chosen = level;
                play = null;
                Close(() => after?.Invoke(chosen - 1));
            };
            actions.Add(go);
        }

        /// <summary>
        /// Opens on the current level (0-based <paramref name="currentIndex"/>);
        /// <paramref name="playAction"/> gets the chosen 0-based level once the popup has closed.
        /// </summary>
        public void Show(int currentIndex, int count, Action<int> playAction)
        {
            play = playAction;
            levelCount = Mathf.Max(1, count);
            Set(currentIndex + 1);
            OpenPopup();
        }

        void Set(int value)
        {
            level = Mathf.Clamp(value, 1, levelCount);
            number.text = $"Level {level}";
            string cargo = null;
            foreach (var (n, kind) in Debuts)
                if (n == level) cargo = UiText.CargoName(kind);
            string hard = Ctx.Controller != null && Ctx.Controller.IsHardLevel(level - 1) ? "HARD · " : "";
            detail.text = hard + (cargo != null ? $"{cargo} debuts here" : $"of {levelCount}");
        }
    }
}
