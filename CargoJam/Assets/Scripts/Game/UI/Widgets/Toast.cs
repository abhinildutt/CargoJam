using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// A short glass message pill under the top bar ("Tap any parcel to lift it", "Unlocks at level 6").
    /// Timed messages fade on their own; sticky ones stay until <see cref="Hide"/>.
    /// </summary>
    public sealed class Toast
    {
        readonly VisualElement pill;
        readonly Label label;
        UiTween life, anim;
        bool visible;

        public Toast(VisualElement parent)
        {
            pill = Ui.El(parent, "cj-toast cj-glass");
            label = Ui.Text(pill, "", "cj-toast__label cj-on-glass");
            Ui.Show(pill, false);
        }

        public void Show(string text, float seconds = 2.2f)
        {
            ShowInternal(text);
            life = UiTween.Delay(seconds, Hide);
        }

        public void ShowSticky(string text) => ShowInternal(text);

        public void Hide()
        {
            life?.Cancel();
            if (!visible) return;
            visible = false;
            anim?.Cancel();
            anim = Anim.SlideOut(pill, 0f, -24f, 0.2f, () => Ui.Show(pill, false));
        }

        void ShowInternal(string text)
        {
            life?.Cancel();
            anim?.Cancel();
            label.text = text;
            Ui.Show(pill, true);
            bool wasVisible = visible;
            visible = true;
            if (wasVisible)
            {
                Anim.SetTranslate(pill, 0f, 0f);
                Anim.SetOpacity(pill, 1f);
                anim = Anim.Punch(pill, 0.04f);
            }
            else
            {
                anim = Anim.SlideIn(pill, 0f, -32f, Anim.Small);
            }
        }
    }
}
