using CargoJam.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// First-session teaching: on level 1 a hand bobs over a good parcel (following it every frame)
    /// until the first move; levels 2 and 3 show a one-line tip. When a cargo kind debuts, the hand
    /// points at what to do with it (<see cref="BeginCargo"/>).
    /// </summary>
    public sealed class Tutorial
    {
        const float HandSize = 150f;
        // Where the fingertip is inside the hand icon (fractions of its size).
        const float TipX = 0.41f, TipY = 0.04f;
        /// <summary>How far past a lid's edge the fingertip rests when pointing at cargo (panel units).</summary>
        const float EdgeGap = 2f;

        static readonly string[] Tips =
        {
            null,
            "Blocked parcels can't move. Clear a path first.",
            "No matching truck? Parcels wait in the dock. Keep a slot or two free.",
        };

        readonly UiContext ctx;
        readonly Icon hand;
        readonly VisualElement tip;
        readonly Label tipLabel;
        bool handActive;
        bool handShown;
        /// <summary>The cargo kind the hand is teaching, or null for the level 1 hint.</summary>
        CargoKind? cargoKind;
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
            var badge = Ui.El(tip, "cj-tip__badge");
            Ui.Text(badge, "Tip", "cj-tip__badge-label");
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
                tipTween = Anim.SlideIn(tip, 0f, -24f, Anim.Panel, 0.7f);
            }
        }

        /// <summary>
        /// Points the hand at what teaches a cargo kind on its debut level (the controller picks the cell,
        /// see <c>GameController.TryGetCargoHintCell</c>) until the first move. Bollards have nothing to
        /// tap, so they get no hand.
        /// </summary>
        public void BeginCargo(CargoKind kind)
        {
            Stop();
            if (kind == CargoKind.Bollard) return;
            cargoKind = kind;
            handActive = true;
            handFrom = Time.unscaledTime + 0.4f;
        }

        /// <summary>The board changed (a move happened): the hand's job is done; tips linger briefly.</summary>
        public void OnMove()
        {
            if (handActive)
            {
                handActive = false;
                handShown = false;
                cargoKind = null;
                Anim.Fade(hand, 1f, 0f, 0.2f, 0f, () => { if (!handActive) Ui.Show(hand, false); });
            }
            if (tipActive)
            {
                tipActive = false;
                tipTween?.Cancel();
                tipTween = UiTween.Delay(2.5f, () => Anim.Fade(tip, 1f, 0f, Anim.Small, 0f, () => Ui.Show(tip, false)));
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
            cargoKind = null;
            tipActive = false;
            tipTween?.Cancel();
            Ui.Show(hand, false);
            Ui.Show(tip, false);
        }

        bool TryGetTarget(GameController c, out Cell cell) =>
            cargoKind.HasValue ? c.TryGetCargoHintCell(cargoKind.Value, out cell) : c.TryGetHintCell(out cell);

        /// <summary>Follows the hint parcel; call every frame.</summary>
        public void Update()
        {
            if (!handActive) return;
            var c = ctx.Controller;
            Cell cell = default;
            if (Time.unscaledTime < handFrom || !c.InputEnabled || !TryGetTarget(c, out cell))
            {
                SetHandShown(false);
                return;
            }
            SetHandShown(true);
            float time = Time.unscaledTime - handFrom;
            // A calm tap motion: ease down onto the parcel, rest, lift away.
            float cycle = (time / 1.3f) % 1f;
            float lift = cycle < 0.35f ? 1f - Easing.OutQuint(cycle / 0.35f)
                : cycle < 0.55f ? 0f
                : Easing.InOutSine((cycle - 0.55f) / 0.45f);
            float scale = 1f - (1f - lift) * 0.05f;
            if (!cargoKind.HasValue)
            {
                var p = ctx.Space.ScreenToPanel(c.CellToScreen(cell));
                Anim.SetTranslate(hand, p.x - HandSize * TipX, p.y - HandSize * TipY + 10f + lift * 30f);
                Anim.SetScale(hand, scale);
                return;
            }
            // Teaching cargo, the hand must not hide what it teaches: it touches the lid's front edge and
            // hangs below it, or, when that would cover the lesson, reaches down (mirrored) from above.
            bool above = c.CargoHintFromAbove;
            var edge = ctx.Space.ScreenToPanel(c.LidEdgeToScreen(cell, !above));
            if (above)
            {
                Anim.SetTranslate(hand, edge.x - HandSize * TipX, edge.y - HandSize * (1f - TipY) - EdgeGap - lift * 30f);
                Anim.SetScale(hand, scale, -scale);
            }
            else
            {
                Anim.SetTranslate(hand, edge.x - HandSize * TipX, edge.y - HandSize * TipY + EdgeGap + lift * 30f);
                Anim.SetScale(hand, scale);
            }
        }
    }
}
