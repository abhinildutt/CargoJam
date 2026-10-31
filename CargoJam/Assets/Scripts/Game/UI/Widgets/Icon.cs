using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    public enum IconKind
    {
        Coin,
        Flame,
        Lock,
        Gear,
        Pause,
        Undo,
        Crane,
        PlusSlot,
        Chest,
        ChestOpen,
        Hand,
        Sound,
        SoundOff,
        Music,
        MusicOff,
        Haptics,
        HapticsOff,
        Check,
        Close,
        Home,
        Restart,
        Gift,
        Star,
        Rays,
        Play,
        Back,
        ChevronRight,
        Truck,
        Map,
    }

    /// <summary>
    /// A crisp vector icon drawn with Painter2D (no textures). Single-colour icons use
    /// <see cref="Tint"/> (white by default; set ink on light surfaces); coin, flame, chest, gift,
    /// star, truck and hand carry their own palette.
    /// </summary>
    public sealed class Icon : VisualElement
    {
        IconKind kind;
        Color tint = UiPalette.OnGlass;

        public Icon(IconKind kind)
        {
            this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("cj-icon");
            generateVisualContent += Draw;
        }

        public IconKind Kind
        {
            get => kind;
            set
            {
                if (kind == value) return;
                kind = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Fill colour of single-colour icons.</summary>
        public Color Tint
        {
            get => tint;
            set
            {
                tint = value;
                MarkDirtyRepaint();
            }
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f || float.IsNaN(rect.width)) return;
            IconPainter.Draw(context.painter2D, kind, rect, tint);
        }
    }
}
