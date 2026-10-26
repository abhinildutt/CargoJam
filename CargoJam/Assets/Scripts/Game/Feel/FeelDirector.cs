using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Presentation;
using CargoJam.Game.Presentation.Fx;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// The game's <see cref="ILevelFx"/>: turns controller moments into sound, music, haptics,
    /// particles and screen overlays. Owned by the FX stream; the controller only calls the interface.
    /// </summary>
    public sealed class FeelDirector : MonoBehaviour, ILevelFx
    {
        const int ParticleCapacity = 260;

        AudioService sfx;
        MusicPlayer music;
        ParticlePool particles;
        FxOverlay overlay;
        FxMaterials fxMaterials;
        DockFx dockFx;
        ComboCallouts callouts;
        LevelViews views;

        /// <summary>One-shot sound effects (also used by the debug HUD).</summary>
        public AudioService Audio => sfx;

        /// <summary>The background loop; call <see cref="MusicPlayer.Refresh"/> after toggling music.</summary>
        public MusicPlayer Music => music;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioService>();
            var musicGo = new GameObject("Music");
            musicGo.transform.SetParent(transform, false);
            music = musicGo.AddComponent<MusicPlayer>();
            Haptics.Prepare();

            fxMaterials = new FxMaterials();
            // Top level, so a scaled or moved controller object never distorts world-space particles.
            particles = ParticlePool.Create(null, ParticleCapacity, fxMaterials.White);
            overlay = FxOverlay.Create(transform);
            dockFx = new DockFx(fxMaterials, overlay);
            callouts = new ComboCallouts(sfx, particles, overlay, fxMaterials);
        }

        void OnDestroy()
        {
            if (particles != null) Destroy(particles.gameObject);
            dockFx?.Destroy();
        }

        // ---- Level lifecycle ----------------------------------------------------------------------

        public float LevelBuilt(LevelViews levelViews)
        {
            views = levelViews;
            Theme.Apply(views.LevelIndex, views.Materials, views.Camera);
            overlay.Camera = views.Camera;
            dockFx.Begin(views);
            callouts.Begin(views);

            sfx.Play(Sfx.Arrive, 1.1f, 0.8f);
            // HARD levels open with a red edge flash, so the tension starts before the first tap.
            if (views.Hard) overlay.Flash(new Color(0.95f, 0.33f, 0.29f), 0.9f);
            return LevelIntro.Play(views,
                (row, rows) => sfx.PlayVaried(Sfx.Tick, 0.9f + 0.45f * row / Mathf.Max(1, rows), 0.45f),
                truck =>
                {
                    if (truck) particles.Emit(truck.transform.position + new Vector3(-0.9f, 0.15f, 0f), BurstSpec.Dust(6), fxMaterials.Dust);
                });
        }

        public void LevelCleared()
        {
            views = null;
            overlay.ClearAll();
            particles.Clear();
            dockFx.End();
            music.ClearDuck();
        }

        // ---- Moves --------------------------------------------------------------------------------

        public void ParcelPicked(ParcelView parcel, bool lifted)
        {
            sfx.PlayVaried(Sfx.Pick);
            Haptics.Play(Haptic.Tap);
        }

        public void ParcelLanded(ParcelView parcel, LandingSpot spot, float at, int combo)
        {
            if (views == null) return;
            var tw = views.Tweener;
            switch (spot)
            {
                case LandingSpot.Truck:
                {
                    // Exact pitch: consecutive loads climb a major scale.
                    float pitch = AudioService.ComboPitch(combo - 1);
                    var mat = views.Materials.Parcel(parcel.Parcel.Color, true);
                    tw.At(at, () =>
                    {
                        sfx.Play(Sfx.Load, pitch);
                        if (parcel) particles.Emit(parcel.transform.position + Vector3.up * 0.15f, BurstSpec.Sparkle(4), mat);
                    });
                    dockFx.Left(parcel, at - Timing.HopToTruck);
                    callouts.Loaded(combo, at);
                    break;
                }
                case LandingSpot.Dock:
                    tw.At(at, () => sfx.PlayVaried(Sfx.Hold));
                    dockFx.Landed(parcel, at);
                    break;
                default:
                    tw.At(at, () =>
                    {
                        sfx.PlayVaried(Sfx.Hold, 1.2f);
                        if (parcel) particles.Emit(parcel.transform.position + Vector3.down * 0.25f, BurstSpec.Dust(5), fxMaterials.Dust);
                    });
                    dockFx.Left(parcel, at - Timing.HopToDock * 1.4f);
                    break;
            }
        }

        public void TruckFull(TruckView truck, float departAt)
        {
            if (views == null) return;
            var tw = views.Tweener;
            var palette = fxMaterials.SparklePalette;
            var cab = views.Materials.TruckCab(truck.Truck.Color);
            // The last parcel lands about DepartDelay before departure: celebrate right then.
            tw.At(departAt - Timing.DepartDelay, () =>
            {
                if (!truck) return;
                var bed = truck.transform.position + new Vector3(-0.4f, 0.9f, 0f);
                particles.Emit(bed, BurstSpec.Sparkle(14), palette, palette.Length);
                particles.Emit(bed, BurstSpec.Sparkle(6), cab);
                overlay.ShowCheck(truck.transform, new Vector3(-0.3f, 2.0f, 0f));
                sfx.Play(Sfx.Sparkle, 1f, 0.7f);
            });
            tw.At(departAt - 0.05f, () =>
            {
                sfx.Play(Sfx.Horn);
                Haptics.Play(Haptic.TruckFull);
            });
            // Dust kicks up behind the rear wheels as it pulls away.
            tw.At(departAt + 0.08f, () =>
            {
                if (!truck) return;
                particles.Emit(truck.transform.position + new Vector3(-1.2f, 0.15f, 0f), BurstSpec.Dust(10), fxMaterials.Dust);
                sfx.Play(Sfx.Puff, 0.8f, 0.5f);
            });
        }

        public void TruckArriving(TruckView truck, float arriveAt)
        {
            if (views == null) return;
            views.Tweener.At(arriveAt - Timing.QueueShift * 0.7f, () => sfx.Play(Sfx.Arrive));
        }

        public void TapRejected(ParcelView parcel, bool dockFull)
        {
            sfx.PlayVaried(Sfx.Nope, 1f, 1f, 0.03f);
            if (dockFull) sfx.Play(Sfx.Danger, 1f, 0.7f);
            Haptics.Play(Haptic.Nope);
        }

        public void DockFillChanged(int count, int capacity, float at)
        {
            if (views == null) return;
            dockFx.SetCapacity(capacity);
            if (count < capacity - 1) return;
            views.Tweener.At(at, () =>
            {
                sfx.Play(Sfx.Danger, count >= capacity ? 0.8f : 1f);
                Haptics.Play(Haptic.Warning);
            });
        }

        // ---- Blockers and boosters ----------------------------------------------------------------

        public void ParcelRevealed(ParcelView parcel, float at)
        {
            if (views == null) return;
            var tarp = views.Materials.Tarp;
            var palette = fxMaterials.SparklePalette;
            // Mid-flip, when the real color shows.
            views.Tweener.At(at + 0.15f, () =>
            {
                if (!parcel) return;
                var top = parcel.transform.position + Vector3.up * 0.35f;
                particles.Emit(top, BurstSpec.Dust(8), tarp);
                particles.Emit(top, BurstSpec.Sparkle(8), palette, palette.Length);
                sfx.Play(Sfx.Puff);
                sfx.Play(Sfx.Sparkle, 1.15f, 0.45f);
            });
        }

        public void ParcelUnlocked(ParcelView parcel, float at)
        {
            if (views == null) return;
            views.Tweener.At(at, () =>
            {
                if (!parcel) return;
                var lockPoint = parcel.transform.position + Vector3.up * 0.6f;
                particles.Emit(lockPoint, BurstSpec.Sparks(14), fxMaterials.Gold);
                particles.Emit(lockPoint, BurstSpec.Sparkle(5), fxMaterials.Glint);
                sfx.Play(Sfx.Unlock);
                Haptics.Play(Haptic.Tick);
            });
        }

        public void BoosterUsed(BoosterType booster, float at)
        {
            if (views == null) return;
            var tw = views.Tweener;
            tw.At(at, () => sfx.Play(Sfx.Click, 1f, 0.6f));
            switch (booster)
            {
                case BoosterType.Undo:
                    tw.At(at, () => sfx.Play(Sfx.Whoosh));
                    break;
                case BoosterType.ExtraSlot:
                    overlay.SetFailMood(false);
                    dockFx.SlotAdded();
                    tw.At(at, () => sfx.Play(Sfx.Pop));
                    tw.At(at + 0.1f, () => sfx.Play(Sfx.Chime, 1f, 0.8f));
                    tw.At(at + 0.18f, () =>
                    {
                        if (dockFx.TryNewestPad(out var pad))
                            particles.Emit(pad + Vector3.up * 0.2f, BurstSpec.Sparkle(14), fxMaterials.SparklePalette, 3);
                    });
                    break;
                default:
                    tw.At(at, () => sfx.Play(Sfx.Whir));
                    break;
            }
        }

        // ---- Win and fail -------------------------------------------------------------------------

        public void Win(float at, Vector3 center)
        {
            if (views == null) return;
            var tw = views.Tweener;
            var materials = views.Materials;
            dockFx.ClearDanger();
            music.Duck(at, 2.4f);
            tw.At(at, () =>
            {
                Confetti.Burst(particles, center + Vector3.up * 1.5f, 60, materials, ParcelColors.Count, fxMaterials);
                overlay.Flash(new Color(1f, 0.86f, 0.4f), 0.7f);
                sfx.Play(Sfx.Win);
                Haptics.Play(Haptic.Win);
            });
            // Follow-up bursts left, right and high, so the celebration rolls instead of popping once.
            WinBurst(tw, at + 0.25f, center + new Vector3(-2.2f, 1.2f, 0.6f), materials, 0.95f);
            WinBurst(tw, at + 0.45f, center + new Vector3(2.2f, 1.2f, 0.6f), materials, 1.1f);
            WinBurst(tw, at + 0.7f, center + new Vector3(0f, 2.4f, 1.2f), materials, 1.25f);
        }

        void WinBurst(Tweener tw, float at, Vector3 point, MaterialBank materials, float pitch)
        {
            tw.At(at, () =>
            {
                Confetti.Burst(particles, point, 34, materials, ParcelColors.Count, fxMaterials, 7f);
                particles.Emit(point, BurstSpec.Sparkle(10), fxMaterials.SparklePalette, 3);
                sfx.Play(Sfx.Sparkle, pitch, 0.5f);
            });
        }

        public void Fail(float at, IReadOnlyList<ParcelView> needed, TruckView waiting)
        {
            if (views == null) return;
            music.Duck(at, 2.2f);
            views.Tweener.At(at, () =>
            {
                overlay.SetFailMood(true);
                sfx.Play(Sfx.Horn, 0.85f);
                sfx.Play(Sfx.Fail);
                Haptics.Play(Haptic.Fail);
            });
        }
    }
}
