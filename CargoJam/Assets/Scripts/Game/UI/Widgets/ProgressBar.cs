using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>A slim rounded track whose fill glides between values (cj-bar--gold for rewards).</summary>
    public sealed class ProgressBar : VisualElement
    {
        readonly VisualElement fill;
        float fraction;
        UiTween tween;

        public ProgressBar(string classes = null)
        {
            AddToClassList("cj-bar");
            Ui.AddClasses(this, classes);
            pickingMode = PickingMode.Ignore;
            fill = Ui.El(this, "cj-bar__fill");
            Apply(0f);
        }

        public float Fraction => fraction;

        /// <summary>Sets progress as value/max.</summary>
        public void Set(int value, int max, bool animate, float delay = 0f, float duration = 0.6f) =>
            SetFraction(max > 0 ? value / (float)max : 0f, animate, delay, duration);

        public void SetFraction(float to, bool animate, float delay = 0f, float duration = 0.6f)
        {
            to = Mathf.Clamp01(to);
            tween?.Cancel();
            if (!animate)
            {
                Apply(to);
                return;
            }
            float from = fraction;
            tween = UiTween.Run(duration, t => Apply(Mathf.Lerp(from, to, t)), Easing.OutQuint, delay);
        }

        /// <summary>Jumps an in-flight fill to its end.</summary>
        public void Finish() => tween?.Skip();

        void Apply(float f)
        {
            fraction = f;
            fill.style.width = Length.Percent(f * 100f);
            fill.style.display = f <= 0.001f ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
