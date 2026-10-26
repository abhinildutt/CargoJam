using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// What the UI tells the game about screen space: where the board may be drawn, and which presses
    /// belong to the UI rather than the board. Implemented by the debug IMGUI <see cref="Hud"/> and by
    /// the real UI.
    /// </summary>
    public interface IHudLayout
    {
        /// <summary>The 0..1 viewport rect the camera fits the level into.</summary>
        Rect GameViewport { get; }

        /// <summary>True when a press at this screen position (origin bottom-left) is the UI's.</summary>
        bool Captures(Vector2 screenPosition);
    }

    public enum LandingSpot
    {
        Truck,
        Dock,
        /// <summary>Back on its yard cell (Undo).</summary>
        Yard,
    }

    /// <summary>Everything the FX layer may decorate for one level, handed over right after it is built.</summary>
    public sealed class LevelViews
    {
        public int LevelIndex;
        public bool Hard;
        public Transform Root;
        public Camera Camera;
        public Layout Layout;
        public MaterialBank Materials;
        public Tweener Tweener;
        public DockView Dock;
        public IReadOnlyList<ParcelView> Parcels;
        public IReadOnlyList<TruckView> Trucks;
    }

    /// <summary>
    /// Sound, haptics, particles and other decoration, driven by the controller. Times are absolute
    /// (Time.time) and may be in the future: the controller schedules animations ahead and tells the
    /// FX layer when each moment will land. Implemented by CargoJam.Game.Feel.FeelDirector.
    /// </summary>
    public interface ILevelFx
    {
        /// <summary>A level was just built (views in their resting positions). The FX layer may play an intro.</summary>
        /// <returns>Seconds until the board should accept input.</returns>
        float LevelBuilt(LevelViews views);

        void ParcelPicked(ParcelView parcel, bool lifted);
        /// <param name="combo">Loads in a row including this one (0 when it went to the dock or yard).</param>
        void ParcelLanded(ParcelView parcel, LandingSpot spot, float at, int combo);
        void TruckFull(TruckView truck, float departAt);
        void TruckArriving(TruckView truck, float arriveAt);
        void TapRejected(ParcelView parcel, bool dockFull);
        void DockFillChanged(int count, int capacity, float at);
        void ParcelRevealed(ParcelView parcel, float at);
        void ParcelUnlocked(ParcelView parcel, float at);
        void BoosterUsed(CargoJam.Meta.BoosterType booster, float at);
        void Win(float at, Vector3 center);
        void Fail(float at, IReadOnlyList<ParcelView> needed, TruckView waiting);
        void LevelCleared();
    }
}
