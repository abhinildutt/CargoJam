using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The home screen's journey strip: one dot per level of the current destination (done, the
    /// truck on the current one, the finale marked), "3 deliveries to Metro City" and a chevron. It is
    /// a glass button; tapping it opens the Journey screen.
    /// </summary>
    public sealed class JourneyStrip
    {
        readonly VisualElement[] dots = new VisualElement[Journey.LevelsPerStop];
        readonly Icon[] trucks = new Icon[Journey.LevelsPerStop];
        readonly Label caption;

        public CandyButton Button { get; }

        public JourneyStrip()
        {
            Button = new CandyButton(null, "cj-btn--glass cj-btn--wide cj-strip");
            var row = Ui.El(Button.Content, "cj-strip__dots");
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i] = Ui.El(row, "cj-strip__dot");
                trucks[i] = Ui.Icon(dots[i], IconKind.Truck, "cj-strip__truck");
            }
            caption = Ui.Text(Button.Content, "", "cj-strip__caption cj-on-glass");
            var chevron = Ui.Icon(Button.Content, IconKind.ChevronRight, "cj-strip__chevron");
            chevron.Tint = UiPalette.A(UiPalette.OnGlass, 0.8f);
        }

        /// <summary>Shows the destination of the level about to be played (0-based index).</summary>
        public void Set(int levelIndex)
        {
            int step = Journey.StepInStop(levelIndex);
            for (int i = 0; i < dots.Length; i++)
            {
                var dot = dots[i];
                dot.EnableInClassList("cj-strip__dot--done", i < step);
                dot.EnableInClassList("cj-strip__dot--current", i == step);
                dot.EnableInClassList("cj-strip__dot--finale", i > step && i == dots.Length - 1);
                Ui.Show(trucks[i], i == step);
            }
            caption.text = UiText.DeliveriesTo(levelIndex);
        }
    }
}
