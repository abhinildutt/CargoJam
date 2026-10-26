using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The chunky candy button: a rounded pill with a darker bottom edge and a glossy shine. It
    /// presses down to 0.94 and bounces back on release. Variants and sizes are USS classes
    /// (cj-btn--primary/secondary/danger/coin/white, cj-btn--big/medium/small/round).
    /// </summary>
    public sealed class CandyButton : VisualElement
    {
        const float PressedScale = 0.94f;

        readonly VisualElement content;
        Label label;
        Icon icon;
        bool pressed;
        int pointerId = -1;
        UiTween scaleTween;
        float currentScale = 1f;
        bool greyed;

        /// <summary>Fired on a completed tap (press and release inside), greyed or not.</summary>
        public event Action Clicked;

        /// <summary>When false the button ignores presses entirely.</summary>
        public bool Interactable { get; set; } = true;

        /// <summary>Skip the default click sound (for buttons that play their own).</summary>
        public bool Silent { get; set; }

        public CandyButton(string text, string classes, IconKind? iconKind = null)
        {
            AddToClassList("cj-btn");
            Ui.AddClasses(this, classes);
            pickingMode = PickingMode.Position;

            Ui.El(this, "cj-btn__shine");
            content = Ui.El(this, "cj-btn__content");
            if (iconKind.HasValue) icon = Ui.Icon(content, iconKind.Value, "cj-btn__icon");
            if (text != null) label = Ui.Text(content, text, "cj-btn__label cj-txt");

            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerUpEvent>(OnUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => Release(false));
        }

        public Label Label => label;
        public Icon Icon => icon;
        /// <summary>The row holding icon and label; screens may add extra children (price pills).</summary>
        public VisualElement Content => content;

        public string Text
        {
            get => label?.text;
            set
            {
                if (label == null) label = Ui.Text(content, value, "cj-btn__label cj-txt");
                else label.text = value;
            }
        }

        /// <summary>Greyed buttons still report taps, so screens can shake them and explain why.</summary>
        public bool Greyed
        {
            get => greyed;
            set
            {
                greyed = value;
                EnableInClassList("cj-btn--greyed", value);
            }
        }

        void OnDown(PointerDownEvent e)
        {
            if (!Interactable || pressed) return;
            pressed = true;
            pointerId = e.pointerId;
            this.CapturePointer(pointerId);
            UiFeedback.Press();
            ScaleTo(PressedScale, 0.07f, Easing.OutCubic);
            EnableInClassList("cj-btn--pressed", true);
            e.StopPropagation();
        }

        void OnUp(PointerUpEvent e)
        {
            if (!pressed || e.pointerId != pointerId) return;
            bool inside = ContainsPoint(this.WorldToLocal(e.position));
            Release(inside);
            e.StopPropagation();
        }

        void Release(bool click)
        {
            if (!pressed) return;
            pressed = false;
            if (this.HasPointerCapture(pointerId)) this.ReleasePointer(pointerId);
            EnableInClassList("cj-btn--pressed", false);
            ScaleTo(1f, 0.32f, Easing.OutBackStrong);
            if (!click) return;
            if (!Silent && !greyed) UiFeedback.Click();
            Clicked?.Invoke();
        }

        void ScaleTo(float target, float duration, Func<float, float> ease)
        {
            scaleTween?.Cancel();
            float from = currentScale;
            scaleTween = UiTween.Run(duration, t =>
            {
                currentScale = Mathf.LerpUnclamped(from, target, t);
                Anim.SetScale(this, currentScale);
            }, ease);
        }
    }
}
