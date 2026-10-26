using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Presentation;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// The game's <see cref="ILevelFx"/>: turns controller moments into sound, haptics and particles.
    /// Owned by the FX stream; the controller only calls the interface.
    /// </summary>
    public sealed class FeelDirector : MonoBehaviour, ILevelFx
    {
        AudioService sfx;
        LevelViews views;

        public AudioService Audio => sfx;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioService>();
            Haptics.Prepare();
        }

        public float LevelBuilt(LevelViews levelViews)
        {
            views = levelViews;
            return 0f;
        }

        public void ParcelPicked(ParcelView parcel, bool lifted)
        {
            sfx.Play(Sfx.Pick);
            Haptics.Play(Haptic.Tap);
        }

        public void ParcelLanded(ParcelView parcel, LandingSpot spot, float at, int combo)
        {
            switch (spot)
            {
                case LandingSpot.Truck:
                    float pitch = AudioService.ComboPitch(combo - 1);
                    views.Tweener.At(at, () => sfx.Play(Sfx.Load, pitch));
                    break;
                case LandingSpot.Dock:
                    views.Tweener.At(at, () => sfx.Play(Sfx.Hold));
                    break;
                default:
                    views.Tweener.At(at, () => sfx.Play(Sfx.Hold, 1.2f));
                    break;
            }
        }

        public void TruckFull(TruckView truck, float departAt)
        {
            views.Tweener.At(departAt - 0.05f, () =>
            {
                sfx.Play(Sfx.Horn);
                Haptics.Play(Haptic.TruckFull);
            });
        }

        public void TruckArriving(TruckView truck, float arriveAt)
        {
            views.Tweener.At(arriveAt - Timing.QueueShift * 0.7f, () => sfx.Play(Sfx.Arrive));
        }

        public void TapRejected(ParcelView parcel, bool dockFull)
        {
            sfx.Play(Sfx.Nope);
            if (dockFull) sfx.Play(Sfx.Danger, 1f, 0.7f);
            Haptics.Play(Haptic.Nope);
        }

        public void DockFillChanged(int count, int capacity, float at)
        {
            if (count < capacity - 1) return;
            views.Tweener.At(at, () =>
            {
                sfx.Play(Sfx.Danger, count >= capacity ? 0.8f : 1f);
                Haptics.Play(Haptic.Warning);
            });
        }

        public void ParcelRevealed(ParcelView parcel, float at) { }

        public void ParcelUnlocked(ParcelView parcel, float at) { }

        public void BoosterUsed(BoosterType booster, float at)
        {
            views.Tweener.At(at, () => sfx.Play(Sfx.Click));
        }

        public void Win(float at, Vector3 center)
        {
            views.Tweener.At(at, () =>
            {
                Confetti.Burst(views.Root, center + Vector3.up * 1.5f, 60, views.Materials, ParcelColors.Count);
                sfx.Play(Sfx.Win);
                Haptics.Play(Haptic.Win);
            });
        }

        public void Fail(float at, IReadOnlyList<ParcelView> needed, TruckView waiting)
        {
            views.Tweener.At(at, () =>
            {
                sfx.Play(Sfx.Horn, 0.85f);
                sfx.Play(Sfx.Fail);
                Haptics.Play(Haptic.Fail);
            });
        }

        public void LevelCleared() => views = null;
    }
}
