using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>An on/off pill switch with a sliding knob.</summary>
    public sealed class ToggleSwitch : VisualElement
    {
        const float Travel = 70f;

        readonly VisualElement knob;
        bool value;
        float knobX;
        UiTween tween;

        /// <summary>Fired with the new value after a tap.</summary>
        public event Action<bool> Changed;

        public ToggleSwitch(bool initial)
        {
            AddToClassList("cj-switch");
            pickingMode = PickingMode.Position;
            knob = Ui.El(this, "cj-switch__knob");
            SetValue(initial, false);
            RegisterCallback<PointerUpEvent>(e =>
            {
                SetValue(!value, true);
                UiFeedback.Press();
                UiFeedback.Click();
                Changed?.Invoke(value);
                e.StopPropagation();
            });
        }

        public bool Value => value;

        public void SetValue(bool on, bool animate)
        {
            value = on;
            EnableInClassList("cj-switch--on", on);
            tween?.Cancel();
            float from = knobX, to = on ? Travel : 0f;
            if (!animate)
            {
                knobX = to;
                Anim.SetTranslate(knob, to, 0f);
                return;
            }
            tween = UiTween.Run(0.25f, t =>
            {
                knobX = Mathf.LerpUnclamped(from, to, t);
                Anim.SetTranslate(knob, knobX, 0f);
            }, Easing.OutBack);
        }
    }
}
