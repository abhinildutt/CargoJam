using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// Design v3 type for the FX overlay: Nunito Black, no outline, and the soft text shadow used on
    /// glass and over the 3D scene (0 2px 6px rgba(0,0,0,0.35)). Falls back to the panel's default
    /// font when the asset is missing.
    /// </summary>
    public static class FxType
    {
        const string BlackPath = "UI/Fonts/Nunito-Black SDF";

        public static readonly TextShadow SoftShadow = new TextShadow
        {
            offset = new Vector2(0f, 2f),
            blurRadius = 6f,
            color = new Color(0f, 0f, 0f, 0.35f),
        };

        static FontAsset black;
        static bool loaded;

        /// <summary>Applies Nunito Black with the soft shadow and no outline.</summary>
        public static void Display(VisualElement e)
        {
            var s = e.style;
            var font = Black();
            if (font != null) s.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
            s.unityFontStyleAndWeight = FontStyle.Normal;
            s.unityTextOutlineWidth = 0f;
            s.textShadow = SoftShadow;
        }

        static FontAsset Black()
        {
            if (loaded) return black;
            loaded = true;
            black = Resources.Load<FontAsset>(BlackPath);
            if (black == null) Debug.LogWarning($"FxType: Resources/{BlackPath} missing; overlay text uses the default font.");
            return black;
        }
    }
}
