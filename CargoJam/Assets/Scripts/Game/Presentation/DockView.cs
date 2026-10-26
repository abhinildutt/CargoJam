using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>The holding pads. They turn amber with one slot left and red when full.</summary>
    public sealed class DockView : MonoBehaviour
    {
        /// <summary>A pad's resting scale.</summary>
        public static readonly Vector3 PadScale = new Vector3(0.92f, 0.04f, 0.92f);

        MeshRenderer[] pads;
        MaterialBank materials;

        /// <summary>When each slot's previous parcel has lifted off, so a new one can land.</summary>
        public float[] SlotFreeAt { get; private set; }

        public static DockView Create(Transform parent, Layout layout, MaterialBank materials)
        {
            var go = new GameObject("Dock");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DockView>();
            view.materials = materials;
            view.pads = new MeshRenderer[layout.DockSlots];
            view.SlotFreeAt = new float[layout.DockSlots];
            for (int i = 0; i < layout.DockSlots; i++)
            {
                view.pads[i] = MaterialBank.Box($"Pad {i}", go.transform, layout.DockSlotCenter(i) + Vector3.up * 0.01f,
                    PadScale, materials.Pad).GetComponent<MeshRenderer>();
            }
            return view;
        }

        public void ShowFill(int count, int capacity)
        {
            var m = count >= capacity ? materials.PadDanger
                : count == capacity - 1 ? materials.PadWarn
                : materials.Pad;
            foreach (var pad in pads) pad.sharedMaterial = m;
        }

        public int SlotCount => pads.Length;

        /// <summary>The pad under a slot, for FX to decorate (don't change its scale permanently).</summary>
        public Transform Pad(int slot) => pads[slot].transform;

        /// <summary>A new slot pops in (Extra Slot booster).</summary>
        public void PopIn(Tweener tweener, float at, int slot)
        {
            if (slot < 0 || slot >= pads.Length) return;
            var pad = pads[slot].transform;
            pad.localScale = Vector3.zero;
            tweener.Run(at, 0.35f, t => { if (pad) pad.localScale = PadScale * Ease.OutBack(t); });
        }

        public void Flash(Tweener tweener)
        {
            foreach (var pad in pads)
                tweener.Punch(pad.transform, PadScale, new Vector3(0.12f, 0f, 0.12f), tweener.Now, 0.25f);
        }
    }
}
