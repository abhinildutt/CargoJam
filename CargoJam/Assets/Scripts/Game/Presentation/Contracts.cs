using System.Collections.Generic;
using CargoJam.Core;
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

    /// <summary>
    /// What the UI tells the game about the home screen: where the 3D diorama should be framed.
    /// Implemented by the real UI (GameFlow); without it the controller uses its own default.
    /// </summary>
    public interface IMenuLayout
    {
        /// <summary>The 0..1 viewport rect (origin bottom-left) the menu diorama is fitted into.</summary>
        Rect MenuViewport { get; }
    }

    public enum LandingSpot
    {
        Truck,
        Dock,
        /// <summary>Back on its yard cell (Undo).</summary>
        Yard,
    }

    /// <summary>
    /// Everything the FX layer may decorate for one level, handed over right after it is built. The
    /// controller keeps <see cref="Dock"/> and <see cref="Layout"/> current when the dock grows.
    /// </summary>
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
        /// <summary>Customs gates, indexed like <see cref="LevelDefinition.Gates"/>.</summary>
        public IReadOnlyList<GateView> Gates;
        /// <summary>Strap bands, indexed like <see cref="LevelDefinition.Straps"/>.</summary>
        public IReadOnlyList<StrapView> Straps;
        /// <summary>The level after this one is a HARD level (lets the music prefetch its cue).</summary>
        public bool NextHard;
        /// <summary>The persistent scenery (reveal points for the arrival pan, the current biome).</summary>
        public WorldEnvironment Environment;
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
        /// <summary>The holding dock's fill or capacity changed (holds report at landing time).</summary>
        void DockFillChanged(int count, int capacity, float at);
        void ParcelRevealed(ParcelView parcel, float at);
        void ParcelUnlocked(ParcelView parcel, float at);
        void BoosterUsed(CargoJam.Meta.BoosterType booster, float at);
        void Win(float at, Vector3 center);

        /// <summary>
        /// Called right after <see cref="LevelBuilt"/> (and when the menu shows) with what the
        /// environment now shows. On an arrival the FX layer plays the destination reveal.
        /// </summary>
        /// <returns>Extra seconds the board should wait for input (the reveal's length), else 0.</returns>
        float EnvironmentChanged(EnvironmentInfo info);

        /// <summary>
        /// The win sequence's world side, scheduled from the last truck's departure: slow-down,
        /// sparkle and the moment the UI's win card should rise.
        /// </summary>
        /// <param name="at">Time.time when the last truck starts to leave.</param>
        /// <param name="lastTruck">Where it leaves from.</param>
        void WinSequence(float at, Vector3 lastTruck);

        /// <summary>A frozen crate's countdown dropped; <paramref name="left"/> moves remain until it thaws (0 = thawed).</summary>
        void FrostCracked(ParcelView parcel, int left, float at);
        /// <summary>An express parcel's count went down to <paramref name="left"/>.</summary>
        void ExpressTicked(ParcelView parcel, int left, float at);
        /// <summary>The Crane cut a strap.</summary>
        void StrapSnapped(StrapView strap, float at);
        /// <summary>A load counted a gate down to <paramref name="left"/>.</summary>
        void GateProgress(GateView gate, int left, float at);
        void GateOpened(GateView gate, float at);
        /// <summary>The board became tense (dock one short of full, or an express parcel nearly out) or relaxed.</summary>
        void TensionChanged(bool tense, float at);

        /// <param name="reason">Why the level was lost.</param>
        /// <param name="needed">Parcels the waiting truck needed (pulsing), possibly empty.</param>
        /// <param name="waiting">The truck at the bay, or null.</param>
        void Fail(float at, FailReason reason, IReadOnlyList<ParcelView> needed, TruckView waiting);
        void LevelCleared();
    }
}
