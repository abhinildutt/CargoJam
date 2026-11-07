using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The "Cargo Jam" wordmark for the launch splash: the name in display type over a row of six
    /// parcel-colored tiles (the game's only saturated colors). Rises in once; no idle motion.
    /// </summary>
    public sealed class Logo : VisualElement
    {
        readonly Label word;
        readonly VisualElement[] tiles = new VisualElement[6];

        public Logo()
        {
            AddToClassList("cj-logo");
            pickingMode = PickingMode.Ignore;
            word = Ui.Text(this, "Cargo Jam", "cj-logo__word cj-on-glass");
            var row = Ui.El(this, "cj-logo__tiles");
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = Ui.El(row, "cj-logo__tile");
                tiles[i].style.backgroundColor = UiPalette.Parcels[i % UiPalette.Parcels.Length];
            }
        }

        /// <summary>The word rises in, then the tiles drop into place one by one.</summary>
        public void PlayIntro()
        {
            Anim.SlideIn(word, 0f, 40f, Anim.Panel);
            for (int i = 0; i < tiles.Length; i++)
            {
                var tile = tiles[i];
                Anim.SetOpacity(tile, 0f);
                UiTween.Run(Anim.Small, t =>
                {
                    Anim.SetOpacity(tile, t);
                    Anim.SetTranslate(tile, 0f, Mathf.Lerp(-28f, 0f, t));
                }, Easing.OutQuint, 0.18f + i * 0.05f);
            }
        }
    }
}
