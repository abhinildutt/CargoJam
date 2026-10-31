using CargoJam.Game.Presentation.Fx;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Win confetti, drawn from the shared <see cref="ParticlePool"/>: a modest number of large cards
    /// in the v3 reward palette (gold, gold-deep, cream, primary green) that float down slowly.
    /// </summary>
    public static class Confetti
    {
        public static void Burst(ParticlePool pool, Vector3 origin, int count, FxMaterials fx, float speed = 7f)
        {
            var palette = fx.ConfettiPalette;
            pool.Emit(origin, BurstSpec.Ribbons(count, speed), palette, palette.Length);
        }
    }
}
