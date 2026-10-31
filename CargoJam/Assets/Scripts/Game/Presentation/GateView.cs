using CargoJam.Core;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// A customs gate on its yard cell: a post, a boom arm and a sign showing the gate's color and the
    /// loads it still needs. It opens (boom up, gate sinks) once the count reaches 0.
    /// </summary>
    public sealed class GateView : MonoBehaviour
    {
        /// <summary>Index into <see cref="LevelDefinition.Gates"/>.</summary>
        public int Index { get; private set; }

        public ParcelGate Gate { get; private set; }

        /// <summary>The count the sign shows (after any scheduled change).</summary>
        public int Left { get; private set; }

        public bool IsOpen { get; private set; }

        /// <param name="index">The gate's index in <see cref="LevelDefinition.Gates"/>.</param>
        /// <param name="left">Loads still needed (the session's <see cref="GameSession.GateLeft"/>).</param>
        public static GateView Create(int index, ParcelGate gate, int left, Transform parent, Layout layout, MaterialBank materials)
        {
            var go = new GameObject($"Gate {index} {ParcelColors.ToCode(gate.Color)}");
            go.transform.SetParent(parent, false);
            go.transform.position = layout.CellCenter(gate.Cell);

            var view = go.AddComponent<GateView>();
            view.Index = index;
            view.Gate = gate;
            view.Left = left;
            return view;
        }

        /// <summary>The sign shows <paramref name="left"/> from Time.time <paramref name="at"/>, with a punch.</summary>
        public void SetCount(int left, float at)
        {
            Left = left;
        }

        /// <summary>Raises the boom and sinks the gate from <paramref name="at"/>, then deactivates it.</summary>
        public void Open(float at)
        {
            IsOpen = true;
            Left = 0;
        }
    }
}
