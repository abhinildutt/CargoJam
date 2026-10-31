using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Presentation;
using CargoJam.Game.Presentation.Fx;
using CargoJam.Game.UI;
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

        // ---- Arrival reveal (design v3 §2): ≤2.4 s from build to input. ----
        /// <summary>The camera pan over the new scenery, ending on the gameplay pose.</summary>
        const float PanSeconds = 1.9f;
        /// <summary>The board drops in as the camera settles, so input opens right as the pan ends.</summary>
        const float RevealIntroDelay = PanSeconds - LevelIntro.InputDelay;

        // ---- Win slow-down (design v3 §3): eased to 0.4× around the last departure. ----
        const float SlowScale = 0.4f;
        /// <summary>Game seconds before the last truck's departure moment that the slow-down starts.</summary>
        const float SlowLead = 0.15f;
        // Real (unscaled) seconds: ease in, hold, ease out.
        const float SlowIn = 0.08f;
        const float SlowHold = 0.22f;
        const float SlowOut = 0.25f;

        // ---- Departure ladder: back-to-back trucks climb in pitch and haptic strength. ----
        const float LadderWindow = 2.5f;
        static readonly float[] LadderSemitones = { 0f, 2f, 4f, 7f, 9f, 12f };
        static readonly string[] LadderWords =
        {
            null, null, "Double delivery", "Triple delivery", "4 in a row", "5 in a row", "6 in a row", "7 in a row",
            "8 in a row", "9 in a row",
        };

        static readonly Color LightningColor = new Color(0.86f, 0.91f, 1f);

        AudioService sfx;
        MusicPlayer music;
        ParticlePool particles;
        AmbientLife ambient;
        FxOverlay overlay;
        FxMaterials fxMaterials;
        DockFx dockFx;
        ComboCallouts callouts;
        LevelViews views;
        readonly CameraPan pan = new CameraPan();

        bool introPending;
        int introFrame;
        float slowTriggerAt = -1f, slowRealStart = -1f;
        int ladder;
        float lastDepartAt = float.NegativeInfinity;
        float nextTrailAt;

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
            ambient = AmbientLife.Create(fxMaterials);
            ambient.Lightning = strength => overlay.Flash(LightningColor, 0.55f, strength);
            ambient.Thunder = () => sfx.Play(Sfx.Thunder, Random.Range(0.9f, 1.05f), 0.45f);
            overlay = FxOverlay.Create(transform);
            dockFx = new DockFx(fxMaterials, overlay);
            callouts = new ComboCallouts(sfx, particles, overlay, fxMaterials);
        }

        void OnDestroy()
        {
            ResetTimeScale();
            if (particles != null) Destroy(particles.gameObject);
            if (ambient != null) Destroy(ambient.gameObject);
            dockFx?.Destroy();
        }

        void Update()
        {
            // Safety net: if the environment call never came this frame, play the plain intro now.
            if (introPending && Time.frameCount != introFrame) PlayIntro(0f);
            StepSlowMotion();
        }

        // After GameController.Update (which may refit the camera on a resize), so the pan can see it.
        void LateUpdate() => pan.Tick();

        // ---- Level lifecycle ----------------------------------------------------------------------

        public float LevelBuilt(LevelViews levelViews)
        {
            views = levelViews;
            overlay.Camera = views.Camera;
            dockFx.Begin(views);
            callouts.Begin(views);
            ladder = 0;
            lastDepartAt = float.NegativeInfinity;

            // The drop-in waits for EnvironmentChanged (same frame), which may delay it behind the
            // arrival pan. The input delay returned here is the intro's own length either way.
            introPending = true;
            introFrame = Time.frameCount;
            return LevelIntro.InputDelay;
        }

        void PlayIntro(float delay)
        {
            introPending = false;
            if (views == null) return;
            var tw = views.Tweener;
            float start = tw.Now + delay;
            tw.At(start, () => sfx.Play(Sfx.Arrive, 1.1f, 0.8f));
            // HARD levels open with a soft red edge flash, so the tension starts before the first tap.
            if (views.Hard) tw.At(start, () => overlay.Flash(UiPalette.Danger, 0.9f, 0.6f));
            LevelIntro.Play(views,
                (row, rows) => sfx.PlayVaried(Sfx.Tick, 0.9f + 0.45f * row / Mathf.Max(1, rows), 0.45f),
                truck =>
                {
                    if (truck) particles.Emit(truck.transform.position + new Vector3(-0.9f, 0.15f, 0f), BurstSpec.Dust(6), fxMaterials.Dust);
                },
                delay);
        }

        public void LevelCleared()
        {
            pan.Cancel(true);
            ResetTimeScale();
            introPending = false;
            views = null;
            overlay.ClearAll();
            particles.Clear();
            dockFx.End();
            music.ClearDuck();
        }

        // ---- Environment (design v3): ambience, music, and the arrival reveal --------------------

        public float EnvironmentChanged(EnvironmentInfo info)
        {
            var cam = views != null ? views.Camera : Camera.main;
            if (overlay.Camera == null) overlay.Camera = cam;
            bool storm = info.Mood == Mood.Storm;

            // Same kind keeps its motes; a different one swaps cleanly. In a level the board stays clear.
            string biome = info.Biome != null ? info.Biome.Id : Journey.IdOf(info.Stop);
            var area = views != null ? views.Layout.ContentBounds : MenuDiorama.Bounds;
            ambient.Show(AmbientLife.KindFor(biome, info.Mood), storm, area, views != null, cam);
            music.PlayStop(info.Stop, storm);

            float extra = 0f;
            if (views != null && info.Arrival) extra = PlayArrival(info);
            if (introPending) PlayIntro(extra);
            return extra;
        }

        /// <summary>The pan, the destination card and the stamp. Returns the extra input delay.</summary>
        float PlayArrival(EnvironmentInfo info)
        {
            var tw = views.Tweener;
            var points = views.Environment != null ? views.Environment.RevealPoints : null;
            bool panned = pan.Begin(views.Camera, points, views.Layout.ContentBounds.center, PanSeconds);

            overlay.ShowArrival(Journey.NameOf(info.Stop));
            music.Duck(tw.Now + ArrivalCard.StampImpact - 0.1f, 1.1f);
            tw.At(tw.Now + ArrivalCard.StampImpact, () =>
            {
                sfx.Play(Sfx.Stamp);
                sfx.Play(Sfx.Chime, 1f, 0.75f);
                sfx.Play(Sfx.Sparkle, 0.9f, 0.35f);
                Haptics.Play(Haptic.Arrival);
            });
            // Without reveal points there is nothing to pan over: the card plays over the normal
            // intro and the board takes input as usual.
            return panned ? RevealIntroDelay : 0f;
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

            // Departure ladder: each truck leaving within LadderWindow of the last climbs a step.
            ladder = departAt - lastDepartAt <= LadderWindow ? ladder + 1 : 1;
            lastDepartAt = departAt;
            int rung = Mathf.Min(ladder - 1, LadderSemitones.Length - 1);
            float pitch = Mathf.Pow(2f, LadderSemitones[rung] / 12f);
            float strength = Mathf.Min(1f, 0.6f + 0.13f * rung);
            string word = ladder >= 2 ? LadderWords[Mathf.Min(ladder, LadderWords.Length - 1)] : null;

            // The last parcel lands about DepartDelay before departure: celebrate right then.
            tw.At(departAt - Timing.DepartDelay, () =>
            {
                if (!truck) return;
                var bed = truck.transform.position + new Vector3(-0.4f, 0.9f, 0f);
                particles.Emit(bed, BurstSpec.Sparkle(14), palette, palette.Length);
                particles.Emit(bed, BurstSpec.Sparkle(6), cab);
                overlay.ShowCheck(truck.transform, new Vector3(-0.3f, 2.0f, 0f));
                sfx.Play(Sfx.Sparkle, pitch, 0.7f);
            });
            tw.At(departAt - 0.05f, () =>
            {
                sfx.Play(Sfx.Horn, pitch);
                Haptics.Play(Haptic.TruckFull, strength);
            });
            if (word != null)
            {
                var anchor = new Vector3(0.6f, 2.4f, Layout.RoadZ + 0.4f);
                tw.At(departAt, () =>
                {
                    overlay.ShowCallout(word, UiPalette.OnGlass, 64f, anchor, false);
                    sfx.Play(Sfx.Chime, pitch, 0.35f);
                });
            }
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

        public void WinSequence(float at, Vector3 lastTruck)
        {
            if (views == null) return;
            // Slow-motion around the last departure; driven from Update in real time, so it can never
            // stretch itself, and reset unconditionally on teardown and fail.
            slowTriggerAt = at - SlowLead;
            slowRealStart = -1f;

            // A trail of glints behind the last truck as it pulls away.
            var truck = NearestTruck(lastTruck);
            if (truck == null) return;
            var palette = fxMaterials.SparklePalette;
            nextTrailAt = 0f;
            views.Tweener.Run(at - 0.25f, 0.6f, t =>
            {
                if (!truck || !truck.gameObject.activeInHierarchy) return;
                float now = Time.time;
                if (now < nextTrailAt) return;
                nextTrailAt = now + 0.035f;
                particles.Emit(truck.transform.position + new Vector3(-1.3f, 0.7f, 0f), BurstSpec.Trail(2), palette, palette.Length);
            });
        }

        TruckView NearestTruck(Vector3 point)
        {
            TruckView best = null;
            float bestDistance = float.MaxValue;
            foreach (var tv in views.Trucks)
            {
                if (!tv || !tv.gameObject.activeInHierarchy) continue;
                float d = (tv.transform.position - point).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = tv;
            }
            return best;
        }

        public void Win(float at, Vector3 center)
        {
            if (views == null) return;
            var tw = views.Tweener;
            dockFx.ClearDanger();
            music.Duck(at, 2.4f);
            tw.At(at, () =>
            {
                Confetti.Burst(particles, center + Vector3.up * 1.6f, 26, fxMaterials, 7.5f);
                overlay.Flash(UiPalette.Gold, 0.9f, 0.4f);
                sfx.Play(Sfx.Win);
                Haptics.Play(Haptic.Win);
            });
            // Two softer follow-ups left and right, so the celebration rolls instead of popping once.
            WinBurst(tw, at + 0.3f, center + new Vector3(-2f, 1.3f, 0.8f), 0.95f);
            WinBurst(tw, at + 0.55f, center + new Vector3(2f, 1.3f, 0.8f), 1.1f);
        }

        void WinBurst(Tweener tw, float at, Vector3 point, float pitch)
        {
            tw.At(at, () =>
            {
                Confetti.Burst(particles, point, 12, fxMaterials, 6f);
                particles.Emit(point, BurstSpec.Sparkle(6), fxMaterials.SparklePalette, 3);
                sfx.Play(Sfx.Sparkle, pitch, 0.4f);
            });
        }

        public void Fail(float at, IReadOnlyList<ParcelView> needed, TruckView waiting)
        {
            ResetTimeScale();
            if (views == null) return;
            overlay.ClearCallouts();
            music.Duck(at, 2.2f);
            views.Tweener.At(at, () =>
            {
                overlay.SetFailMood(true);
                sfx.Play(Sfx.Horn, 0.85f);
                sfx.Play(Sfx.Fail);
                Haptics.Play(Haptic.Fail);
            });
        }

        // ---- Time scale ---------------------------------------------------------------------------

        void StepSlowMotion()
        {
            if (slowTriggerAt >= 0f && Time.time >= slowTriggerAt)
            {
                slowTriggerAt = -1f;
                slowRealStart = Time.unscaledTime;
            }
            if (slowRealStart < 0f) return;

            float u = Time.unscaledTime - slowRealStart;
            float scale;
            if (u < SlowIn) scale = Mathf.Lerp(1f, SlowScale, Smooth(u / SlowIn));
            else if (u < SlowIn + SlowHold) scale = SlowScale;
            else if (u < SlowIn + SlowHold + SlowOut) scale = Mathf.Lerp(SlowScale, 1f, Smooth((u - SlowIn - SlowHold) / SlowOut));
            else
            {
                scale = 1f;
                slowRealStart = -1f;
            }
            Time.timeScale = scale;
        }

        void ResetTimeScale()
        {
            slowTriggerAt = -1f;
            slowRealStart = -1f;
            Time.timeScale = 1f;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
