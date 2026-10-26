using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// First-session teaching: on level 1 a hand bobs over a good parcel (following it every frame)
    /// until the first move; levels 2 and 3 show a one-line tip.
    /// </summary>
    public sealed class Tutorial
    {
        const float HandSize = 150f;
        // Where the fingertip is inside the hand icon (fractions of its size).
        const float TipX = 0.41f, TipY = 0.04f;

        static readonly string[] Tips =
        {
            null,
            "Blocked parcels can't move. Clear a path!",
            "No matching truck? Parcels wait in the dock. Don't let it fill up!",
        };

        readonly UiContext ctx;
        readonly Icon hand;
        readonly VisualElement tip;
        readonly Label tipLabel;
        bool handActive;
        bool handShown;
        bool tipActive;
        float handFrom;
        UiTween tipTween;

        public Tutorial(UiContext ctx, VisualElement overlaySafe)
        {
            this.ctx = ctx;
            hand = Ui.Icon(ctx.OverlayLayer, IconKind.Hand, "cj-hand");
            hand.style.width = HandSize;
            hand.style.height = HandSize;
            Ui.Show(hand, false);

            tip = Ui.El(overlaySafe, "cj-tip");
            Ui.Icon(tip, IconKind.Star, "cj-tip__icon");
            tipLabel = Ui.Text(tip, "", "cj-tip__label");
            Ui.Show(tip, false);
        }

        /// <summary>Sets up teaching for the level that just started (0-based index).</summary>
        public void Begin(int levelIndex)
        {
            Stop();
            if (levelIndex == 0)
            {
                handActive = true;
                // Let the intro land before pointing.
                handFrom = Time.unscaledTime + 0.9f;
            }
            else if (levelIndex < Tips.Length && Tips[levelIndex] != null)
            {
                tipActive = true;
                tipLabel.text = Tips[levelIndex];
                Ui.Show(tip, true);
                tipTween = Anim.PopIn(tip, 0.7f, 0.45f);
            }
        }

        /// <summary>The board changed (a move happened): the hand's job is done; tips linger briefly.</summary>
        public void OnMove()
        {
            if (handActive)
            {
                handActive = false;
                handShown = false;
                Anim.Fade(hand, 1f, 0f, 0.2f, 0f, () => { if (!handActive) Ui.Show(hand, false); });
            }
            if (tipActive)
            {
                tipActive = false;
                tipTween?.Cancel();
                tipTween = UiTween.Delay(2.5f, () => Anim.PopOut(tip, 0.2f, () => Ui.Show(tip, false)));
            }
        }

        void SetHandShown(bool on)
        {
            if (handShown == on) return;
            handShown = on;
            Ui.Show(hand, on);
            if (on) Anim.Fade(hand, 0f, 1f, 0.2f);
        }

        public void Stop()
        {
            handActive = false;
            handShown = false;
            tipActive = false;
            tipTween?.Cancel();
            Ui.Show(hand, false);
            Ui.Show(tip, false);
        }

        /// <summary>Follows the hint parcel; call every frame.</summary>
        public void Update()
        {
            if (!handActive) return;
            var c = ctx.Controller;
            if (Time.unscaledTime < handFrom || !c.InputEnabled || !c.TryGetHintCell(out var cell))
            {
                SetHandShown(false);
                return;
            }
            SetHandShown(true);
            var p = ctx.Space.ScreenToPanel(c.CellToScreen(cell));
            float time = Time.unscaledTime - handFrom;
            float bob = Mathf.Abs(Mathf.Sin(time * Mathf.PI * 1.3f));
            Anim.SetTranslate(hand, p.x - HandSize * TipX, p.y - HandSize * TipY + 12f + bob * 34f);
            // Squash a little at the bottom of each bob, like a tap.
            Anim.SetScale(hand, 1f - (1f - bob) * 0.08f);
        }
    }
}
