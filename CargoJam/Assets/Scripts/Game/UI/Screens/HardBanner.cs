using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>The red "HARD LEVEL · x2 coins" ribbon that sweeps across at the start of a hard level.</summary>
    public sealed class HardBanner
    {
        readonly VisualElement band;
        readonly VisualElement inner;
        UiTween sequence;

        public HardBanner(VisualElement overlay)
        {
            band = Ui.El(overlay, "cj-hard-banner");
            inner = Ui.El(band, "cj-hard-banner__inner");
            Ui.Icon(inner, IconKind.Flame, "cj-hard-banner__icon");
            var col = Ui.El(inner, "cj-hard-banner__col");
            Ui.Text(col, "HARD LEVEL", "cj-hard-banner__title cj-txt");
            Ui.Text(col, "Double coins if you win!", "cj-hard-banner__sub cj-txt");
            Ui.Icon(inner, IconKind.Flame, "cj-hard-banner__icon");
            Ui.Show(band, false);
        }

        public void Play(float delay = 0.25f)
        {
            sequence?.Cancel();
            Ui.Show(band, true);
            const float travel = 1400f;
            Anim.SetTranslate(inner, -travel, 0f);
            Anim.SetScale(band, 1f, 0f);
            UiFeedback.Warning();
            // Band opens, text whooshes in, holds, whooshes out the other side, band closes.
            sequence = UiTween.Run(2.1f, t =>
            {
                float time = t * 2.1f;
                float open = Mathf.Clamp01(time / 0.2f) * (1f - Mathf.Clamp01((time - 1.9f) / 0.2f));
                Anim.SetScale(band, 1f, Easing.OutBack(open));
                float x;
                if (time < 0.5f) x = Mathf.LerpUnclamped(-travel, 0f, Easing.OutBack(Mathf.Clamp01((time - 0.1f) / 0.4f)));
                else if (time < 1.5f) x = Mathf.Lerp(0f, 30f, (time - 0.5f));
                else x = Mathf.Lerp(30f, travel, Easing.InCubic(Mathf.Clamp01((time - 1.5f) / 0.4f)));
                Anim.SetTranslate(inner, x, 0f);
            }, null, delay, () => Ui.Show(band, false));
        }

        public void Stop()
        {
            sequence?.Cancel();
            Ui.Show(band, false);
        }
    }
}
