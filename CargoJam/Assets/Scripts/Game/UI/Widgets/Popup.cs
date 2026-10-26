using System;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Base for every modal: a dimming scrim plus a cream panel with a title ribbon that pops in
    /// with an overshoot. Subclasses build their body once and refresh it in their own Show method.
    /// </summary>
    public abstract class Popup
    {
        protected readonly UiContext Ctx;
        readonly VisualElement root;
        readonly VisualElement ribbon;
        readonly Label title;
        UiTween panelTween, scrimTween;

        protected VisualElement Scrim { get; }
        protected VisualElement Panel { get; }
        /// <summary>The column under the ribbon that subclasses fill.</summary>
        protected VisualElement Body { get; }

        public bool IsOpen { get; private set; }

        protected Popup(UiContext ctx, string titleText, string ribbonClass, bool closeButton)
        {
            Ctx = ctx;
            root = Ui.El(null, "cj-modal");
            Scrim = Ui.El(root, "cj-scrim", true);
            Panel = Ui.El(root, "cj-panel", true);
            ribbon = Ui.El(Panel, "cj-ribbon " + ribbonClass);
            title = Ui.Text(ribbon, titleText, "cj-ribbon__label cj-txt");
            Body = Ui.El(Panel, "cj-panel__body");

            if (closeButton)
            {
                var close = new CandyButton(null, "cj-btn--danger cj-btn--close", IconKind.Close);
                close.Clicked += OnCloseButton;
                Panel.Add(close);
            }
        }

        protected string Title
        {
            set => title.text = value;
        }

        /// <summary>Swaps the ribbon colour class (e.g. danger for the fail panel).</summary>
        protected void SetRibbon(string ribbonClass)
        {
            ribbon.ClearClassList();
            ribbon.AddToClassList("cj-ribbon");
            ribbon.AddToClassList(ribbonClass);
        }

        protected void OpenPopup()
        {
            if (IsOpen) return;
            IsOpen = true;
            Ctx.ModalOpened();
            Ctx.ModalLayer.Add(root);
            root.BringToFront();
            panelTween?.Cancel();
            scrimTween?.Cancel();
            scrimTween = Anim.Fade(Scrim, 0f, 1f, 0.22f);
            panelTween = Anim.PopIn(Panel, 0.03f, 0.42f);
            UiFeedback.Whoosh();
        }

        /// <summary>Pops the panel out, then removes the popup and runs <paramref name="after"/>.</summary>
        public void Close(Action after = null)
        {
            if (!IsOpen)
            {
                after?.Invoke();
                return;
            }
            IsOpen = false;
            Ctx.ModalClosed();
            OnClosing();
            panelTween?.Cancel();
            scrimTween?.Cancel();
            scrimTween = Anim.Fade(Scrim, 1f, 0f, 0.2f);
            panelTween = Anim.PopOut(Panel, 0.18f, () =>
            {
                if (!IsOpen) root.RemoveFromHierarchy();
                after?.Invoke();
            });
        }

        /// <summary>Called as the popup starts closing (stop idle loops here).</summary>
        protected virtual void OnClosing() { }

        protected virtual void OnCloseButton() => Close();
    }
}
