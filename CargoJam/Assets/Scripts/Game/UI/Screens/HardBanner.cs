using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The calm notice at the start of a hard level: a glass card under the top bar with a HARD tag,
    /// "Hard level" and "Double coins for this one". It settles in, holds, and fades away.
    /// </summary>
    public sealed class HardBanner
    {
        const float Hold = 1.8f;

        readonly VisualElement card;
        UiTween sequence;

        public HardBanner(VisualElement overlaySafe)
        {
            card = Ui.El(overlaySafe, "cj-hard-banner cj-glass");
            var tag = Ui.El(card, "cj-tag cj-hard-banner__tag");
            Ui.Text(tag, "HARD", "cj-tag__label");
            var col = Ui.El(card, "cj-hard-banner__col");
            Ui.Text(col, "Hard level", "cj-t-heading cj-on-glass");
            Ui.Text(col, "Double coins for this one", "cj-t-caption cj-on-glass-2");
            Ui.Show(card, false);
        }

        public void Play(float delay = 0.3f)
        {
            sequence?.Cancel();
            Ui.Show(card, true);
            Anim.SetOpacity(card, 0f);
            sequence = UiTween.Delay(delay, () =>
            {
                UiFeedback.Warning();
                sequence = Anim.SlideIn(card, 0f, -30f, Anim.Panel, 0f, () =>
                    sequence = Anim.Fade(card, 1f, 0f, Anim.Panel, Hold, () => Ui.Show(card, false)));
            });
        }

        public void Stop()
        {
            sequence?.Cancel();
            Ui.Show(card, false);
        }
    }
}
