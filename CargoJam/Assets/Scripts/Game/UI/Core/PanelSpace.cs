using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Converts between screen pixels (origin bottom-left) and panel units (origin top-left, scaled
    /// by the PanelSettings), and reports the safe area in panel units.
    /// </summary>
    public sealed class PanelSpace
    {
        readonly VisualElement root;
        readonly PanelSettings settings;

        public PanelSpace(VisualElement root, PanelSettings settings)
        {
            this.root = root;
            this.settings = settings;
        }

        /// <summary>Panel units per screen pixel.</summary>
        public float UnitsPerPixel
        {
            get
            {
                // The laid-out panel size is the ground truth; before the first layout, use the scale formula.
                var tree = root?.panel?.visualTree;
                if (tree != null && ScreenInfo.Width > 0)
                {
                    float w = tree.layout.width;
                    if (w > 0f && !float.IsNaN(w)) return w / ScreenInfo.Width;
                }
                return FormulaUnitsPerPixel();
            }
        }

        /// <summary>The full panel size in panel units.</summary>
        public Vector2 PanelSize => new Vector2(ScreenInfo.Width, ScreenInfo.Height) * UnitsPerPixel;

        public Vector2 ScreenToPanel(Vector2 screen)
        {
            float k = UnitsPerPixel;
            return new Vector2(screen.x * k, (ScreenInfo.Height - screen.y) * k);
        }

        /// <summary>Safe-area insets in panel units: left, top, right, bottom.</summary>
        public Vector4 SafeInsets
        {
            get
            {
                var safe = ScreenInfo.SafeArea;
                float k = UnitsPerPixel;
                return new Vector4(
                    safe.xMin * k,
                    (ScreenInfo.Height - safe.yMax) * k,
                    (ScreenInfo.Width - safe.xMax) * k,
                    safe.yMin * k);
            }
        }

        /// <summary>Mirrors PanelSettings' ScaleWithScreenSize / MatchWidthOrHeight math.</summary>
        float FormulaUnitsPerPixel()
        {
            if (settings == null || ScreenInfo.Width <= 0 || ScreenInfo.Height <= 0) return 1f;
            if (settings.scaleMode != PanelScaleMode.ScaleWithScreenSize)
                return settings.scale > 0f ? 1f / settings.scale : 1f;
            var reference = settings.referenceResolution;
            if (reference.x <= 0 || reference.y <= 0) return 1f;
            float logW = Mathf.Log(ScreenInfo.Width / (float)reference.x, 2f);
            float logH = Mathf.Log(ScreenInfo.Height / (float)reference.y, 2f);
            float scale = Mathf.Pow(2f, Mathf.Lerp(logW, logH, settings.match));
            return scale > 0f ? 1f / scale : 1f;
        }
    }
}
