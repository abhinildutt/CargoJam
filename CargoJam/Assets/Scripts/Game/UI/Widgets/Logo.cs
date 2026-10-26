using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The "CARGO JAM" title: chunky outlined letters in the six crate colours, each slightly tilted
    /// like a stacked crate, riding a gentle wave. Drops in letter by letter when shown.
    /// </summary>
    public sealed class Logo : VisualElement
    {
        // Crate colours in ParcelColors order (R B G Y P O), matching the 3D parcels.
        static readonly Color[] Palette =
        {
            new Color(0.93f, 0.27f, 0.27f),
            new Color(0.24f, 0.52f, 0.96f),
            new Color(0.30f, 0.78f, 0.36f),
            new Color(0.98f, 0.80f, 0.20f),
            new Color(0.64f, 0.38f, 0.92f),
            new Color(1.00f, 0.56f, 0.16f),
        };

        static readonly float[] Tilts = { -6f, 4f, -3f, 5f, -4f, 3f, -5f, 4f };

        readonly Label[] letters;
        UiTween wave;

        public Logo()
        {
            AddToClassList("cj-logo");
            pickingMode = PickingMode.Ignore;
            var top = Ui.El(this, "cj-logo__row");
            var bottom = Ui.El(this, "cj-logo__row");
            letters = new Label[8];
            int n = 0;
            foreach (char c in "CARGO") letters[n] = MakeLetter(top, c, n++, "cj-logo__letter");
            foreach (char c in "JAM") letters[n] = MakeLetter(bottom, c, n++, "cj-logo__letter cj-logo__letter--big");
        }

        Label MakeLetter(VisualElement row, char c, int index, string classes)
        {
            var label = Ui.Text(row, c.ToString(), classes);
            label.style.color = Palette[(index * 5 + 1) % Palette.Length];
            Anim.SetRotate(label, Tilts[index % Tilts.Length]);
            return label;
        }

        /// <summary>Letters drop in one by one with a bounce, then the idle wave starts.</summary>
        public void PlayIntro()
        {
            Stop();
            for (int i = 0; i < letters.Length; i++)
            {
                var letter = letters[i];
                float tilt = Tilts[i % Tilts.Length];
                Anim.SetOpacity(letter, 0f);
                Anim.SetTranslate(letter, 0f, -160f);
                UiTween.Run(0.5f, t =>
                {
                    Anim.SetOpacity(letter, Mathf.Clamp01(t * 3f));
                    Anim.SetTranslate(letter, 0f, -160f * (1f - t));
                }, Easing.OutBack, 0.05f + i * 0.06f, () => Anim.SetRotate(letter, tilt));
            }
            UiTween.Delay(0.6f + letters.Length * 0.06f, StartWave);
        }

        void StartWave()
        {
            wave?.Cancel();
            wave = UiTween.Loop(time =>
            {
                for (int i = 0; i < letters.Length; i++)
                {
                    float y = Mathf.Sin(time * 2.4f - i * 0.55f) * 7f;
                    Anim.SetTranslate(letters[i], 0f, y);
                }
            });
        }

        public void Stop()
        {
            wave?.Cancel();
            wave = null;
        }
    }
}
