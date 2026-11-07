using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// The "Cargo Jam" wordmark for the launch title: the name in white over a road, and a red
    /// truck carrying three parcels. Letters and truck are separate images (tools/brand/make_logo.py)
    /// so the truck can drive in. Plays once; no idle motion.
    /// </summary>
    public sealed class Logo : VisualElement
    {
        /// <summary>How far left of its spot the truck starts, off screen (panel units).</summary>
        const float TruckRunUp = 900f;
        const float TruckDrive = 0.6f;

        readonly VisualElement letters;
        readonly VisualElement truck;

        public Logo()
        {
            AddToClassList("cj-logo");
            pickingMode = PickingMode.Ignore;
            letters = Ui.El(this, "cj-logo__letters");
            truck = Ui.El(this, "cj-logo__truck");
        }

        /// <summary>The name and road rise in, then the truck drives in from the left and stops.</summary>
        public void PlayIntro()
        {
            Anim.SlideIn(letters, 0f, 40f, Anim.Panel);
            Anim.SetTranslate(truck, -TruckRunUp, 0f);
            UiTween.Run(TruckDrive, t => Anim.SetTranslate(truck, Mathf.Lerp(-TruckRunUp, 0f, t), 0f),
                Easing.OutQuint, 0.18f);
        }
    }
}
