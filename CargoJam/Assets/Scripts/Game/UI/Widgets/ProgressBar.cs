using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>A rounded fill bar with a centred "x/y" label; the fill glides between values.</summary>
    public sealed class ProgressBar : VisualElement
    {
        readonly VisualElement fill;
        readonly Label label;
        float fraction;
        UiTween tween;

        public ProgressBar(string classes = null)
        {
            AddToClassList("cj-bar");
            Ui.AddClasses(this, classes);
            pickingMode = PickingMode.Ignore;
            fill = Ui.El(this, "cj-bar__fill");
            Ui.El(fill, "cj-bar__gloss");
            label = Ui.Text(this, "", "cj-bar__label cj-txt");
            Apply(0f);
        }

        public Label Label => label;

        /// <summary>Sets progress as value/max and the label text "value/max".</summary>
        public void Set(int value, int max, bool animate, float delay = 0f, float duration = 0.6f)
        {
            float to = max > 0 ? Mathf.Clamp01(value / (float)max) : 0f;
            label.text = $"{value}/{max}";
            tween?.Cancel();
            if (!animate)
            {
                Apply(to);
                return;
            }
            float from = fraction;
            tween = UiTween.Run(duration, t => Apply(Mathf.Lerp(from, to, t)), Easing.OutCubic, delay,
                () => { if (to > from) Anim.Punch(this, 0.06f); });
        }

        void Apply(float f)
        {
            fraction = f;
            fill.style.width = Length.Percent(f * 100f);
            fill.style.display = f <= 0.001f ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
