using System;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>How a popup sits on screen.</summary>
    public enum PopupKind
    {
        /// <summary>A centered off-white card over a full scrim (pause, daily, chest, buy, unlock).</summary>
        Card,
        /// <summary>A card anchored above the bottom edge over a light scrim (win).</summary>
        BottomCard,
        /// <summary>An edge-to-edge bottom sheet, at most 45% of the screen, over a light scrim (fail).</summary>
        Sheet,
    }

    /// <summary>
    /// Base for every modal (design v3): an off-white card or sheet with a title in title type, no
    /// ribbon, and an optional quiet ✕. Cards fade and settle in; sheets slide up. Subclasses build
    /// their body once and refresh it in their own Show method.
    /// </summary>
    public abstract class Popup
    {
        const float SlideDistance = 700f;

        protected readonly UiContext Ctx;
        readonly PopupKind kind;
        readonly VisualElement root;
        readonly Label title;
        UiTween panelTween, scrimTween;

        protected VisualElement Root => root;
        protected VisualElement Scrim { get; }
        /// <summary>The positioned frame (panel plus its elevation underlay); this is what animates.</summary>
        protected VisualElement Frame { get; }
        protected VisualElement Panel { get; }
        protected Label TitleLabel => title;
        /// <summary>The column under the title that subclasses fill.</summary>
        protected VisualElement Body { get; }

        public bool IsOpen { get; private set; }

        protected Popup(UiContext ctx, string titleText, PopupKind kind = PopupKind.Card, bool closeButton = false)
        {
            Ctx = ctx;
            this.kind = kind;
            root = Ui.El(null, "cj-modal");
            Scrim = Ui.El(root, kind == PopupKind.Card ? "cj-scrim" : "cj-scrim cj-scrim--light", true);
            Frame = Ui.El(root, "cj-frame", true);
            Ui.El(Frame, "cj-elevation");
            Panel = Ui.El(Frame, "cj-panel");
            switch (kind)
            {
                case PopupKind.BottomCard:
                    root.AddToClassList("cj-modal--bottom");
                    Frame.AddToClassList("cj-frame--bottom");
                    break;
                case PopupKind.Sheet:
                    root.AddToClassList("cj-modal--sheet");
                    Frame.AddToClassList("cj-frame--sheet");
                    Ui.El(Panel, "cj-sheet__grabber");
                    break;
            }
            title = Ui.Text(Panel, titleText, "cj-panel__title");
            Body = Ui.El(Panel, "cj-panel__body");

            if (closeButton)
            {
                var close = new CandyButton(null, "cj-btn--close", IconKind.Close);
                close.Icon.Tint = UiPalette.Ink2;
                close.Clicked += OnCloseButton;
                Panel.Add(close);
            }
            Scrim.RegisterCallback<PointerDownEvent>(_ => OnScrimTapped());
        }

        protected string Title
        {
            set => title.text = value;
        }

        protected void OpenPopup()
        {
            if (IsOpen) return;
            IsOpen = true;
            Ctx.ModalOpened();
            Ctx.ModalLayer.Add(root);
            root.BringToFront();
            FitSafeArea();
            panelTween?.Cancel();
            scrimTween?.Cancel();
            scrimTween = Anim.Fade(Scrim, 0f, 1f, Anim.Small);
            panelTween = kind == PopupKind.Card
                ? Anim.PopIn(Frame, 0.02f, Anim.Panel)
                : Anim.SlideIn(Frame, 0f, SlideDistance, Anim.Panel);
            UiFeedback.Whoosh();
        }

        /// <summary>Animates the popup out, then removes it and runs <paramref name="after"/>.</summary>
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
            Action removed = () =>
            {
                if (!IsOpen) root.RemoveFromHierarchy();
                Anim.ClearTransform(Frame);
                after?.Invoke();
            };
            panelTween = kind == PopupKind.Card
                ? Anim.PopOut(Frame, 0.18f, removed)
                : Anim.SlideOut(Frame, 0f, SlideDistance * 0.6f, Anim.Small, removed);
        }

        /// <summary>Keeps sheets and bottom cards clear of the home indicator.</summary>
        void FitSafeArea()
        {
            var insets = Ctx.Space.SafeInsets;
            if (kind == PopupKind.Sheet) Panel.style.paddingBottom = 48f + insets.w;
            else if (kind == PopupKind.BottomCard) Frame.style.marginBottom = 56f + insets.w;
        }

        /// <summary>Called as the popup starts closing (stop loops here).</summary>
        protected virtual void OnClosing() { }

        protected virtual void OnCloseButton() => Close();

        /// <summary>A press on the scrim (outside the card). Ignored unless a popup opts in.</summary>
        protected virtual void OnScrimTapped() { }
    }
}
