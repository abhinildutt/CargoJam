using UnityEngine;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// The handful of extra colors the FX need beyond <see cref="MaterialBank"/>, made once from the
    /// same URP Lit base material (opaque only, so no extra shader variants ship).
    /// </summary>
    public sealed class FxMaterials
    {
        /// <summary>Near-white sparkle.</summary>
        public Material White { get; }
        /// <summary>Pale warm yellow for sparkle mixes.</summary>
        public Material Glint { get; }
        /// <summary>Gold sparks (keys, unlocks, wins).</summary>
        public Material Gold { get; }
        /// <summary>Sandy dust for wheels and drops.</summary>
        public Material Dust { get; }
        /// <summary>Bright rim used for the dock pad glow.</summary>
        public Material PadGlow { get; }

        /// <summary>White, glint and gold: the default sparkle palette.</summary>
        public Material[] SparklePalette { get; }

        public FxMaterials()
        {
            var baseMaterial = Resources.Load<Material>("Materials/Base");
            White = Make(baseMaterial, new Color(1f, 1f, 0.97f));
            Glint = Make(baseMaterial, new Color(1f, 0.95f, 0.62f));
            Gold = Make(baseMaterial, new Color(1f, 0.78f, 0.22f));
            Dust = Make(baseMaterial, new Color(0.86f, 0.80f, 0.70f));
            PadGlow = Make(baseMaterial, new Color(1f, 0.97f, 0.80f));
            SparklePalette = new[] { White, Glint, Gold };
        }

        static Material Make(Material baseMaterial, Color c)
        {
            var m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            return m;
        }
    }
}
