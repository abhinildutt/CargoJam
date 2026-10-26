using CargoJam.Game.Presentation;
using CargoJam.Game.Presentation.Fx;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>Colored paper flakes for celebrations, drawn from the shared <see cref="ParticlePool"/>.</summary>
    public static class Confetti
    {
        static Material[] palette;
        static MaterialBank paletteSource;

        /// <summary>A burst of <paramref name="count"/> flakes in every parcel color plus gold and white.</summary>
        public static void Burst(ParticlePool pool, Vector3 origin, int count, MaterialBank materials, int colorCount,
            FxMaterials fx, float speed = 9f)
        {
            if (palette == null || paletteSource != materials)
            {
                // Built once per MaterialBank so bursts reuse the same array.
                paletteSource = materials;
                palette = new Material[colorCount + 2];
                for (int i = 0; i < colorCount; i++) palette[i] = materials.Parcel(i, true);
                palette[colorCount] = fx.Gold;
                palette[colorCount + 1] = fx.White;
            }
            pool.Emit(origin, BurstSpec.Confetti(count, speed), palette, palette.Length);
        }
    }
}
