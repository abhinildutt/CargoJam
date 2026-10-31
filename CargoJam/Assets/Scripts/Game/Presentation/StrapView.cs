using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// The woven band and buckle over a strapped pair's lids, one mesh, parented to the member in the
    /// later row so it rides along when the pair leaves the yard.
    /// </summary>
    public sealed class StrapView : MonoBehaviour
    {
        /// <summary>Index into <see cref="LevelDefinition.Straps"/>.</summary>
        public int Index { get; private set; }

        public ParcelView A { get; private set; }
        public ParcelView B { get; private set; }

        /// <summary>False once the Crane cut it (until Undo restores it).</summary>
        public bool Intact { get; private set; } = true;

        /// <param name="index">The strap's index in <see cref="LevelDefinition.Straps"/>.</param>
        /// <param name="a">The parcel at <see cref="ParcelStrap.A"/>.</param>
        /// <param name="b">The parcel at <see cref="ParcelStrap.B"/>.</param>
        public static StrapView Create(int index, ParcelView a, ParcelView b, MaterialBank materials)
        {
            var owner = b.Parcel.Cell.Y >= a.Parcel.Cell.Y ? b : a;
            var go = new GameObject($"Strap {index}");
            go.transform.SetParent(owner.transform, false);

            var view = go.AddComponent<StrapView>();
            view.Index = index;
            view.A = a;
            view.B = b;
            return view;
        }

        /// <summary>The Crane cut it: the band snaps away from <paramref name="at"/>.</summary>
        public void Snap(float at)
        {
            Intact = false;
        }

        /// <summary>Undo put the pair back: the band reappears from <paramref name="at"/>.</summary>
        public void Restore(float at)
        {
            Intact = true;
        }
    }
}
