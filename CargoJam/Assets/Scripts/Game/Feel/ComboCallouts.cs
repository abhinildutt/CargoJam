using CargoJam.Game.Presentation;
using CargoJam.Game.Presentation.Fx;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// "Nice!" / "Great!" / "Awesome!" / "Unstoppable!" at 3 / 5 / 8 / 12 loads in a row (and every 4
    /// after that), each tier bigger, with a colored burst and a sting pitched up per tier.
    /// </summary>
    public sealed class ComboCallouts
    {
        static readonly int[] Thresholds = { 3, 5, 8, 12 };
        static readonly string[] Words = { "Nice!", "Great!", "Awesome!", "Unstoppable!" };
        static readonly Color[] Colors =
        {
            new Color(0.23f, 0.79f, 0.36f),
            new Color(0.29f, 0.56f, 0.94f),
            new Color(0.70f, 0.42f, 0.98f),
            new Color(1f, 0.78f, 0.24f),
        };
        static readonly float[] FontSizes = { 92f, 104f, 116f, 130f };
        /// <summary>Parcel color index used for each tier's burst (green, blue, purple, yellow).</summary>
        static readonly int[] BurstColor = { 2, 1, 4, 3 };
        /// <summary>Root, major second, major third, fifth.</summary>
        static readonly float[] Pitches = { 1f, 1.1225f, 1.2599f, 1.4983f };
        const int RepeatEvery = 4;

        readonly AudioService sfx;
        readonly ParticlePool particles;
        readonly FxOverlay overlay;
        readonly FxMaterials fxMaterials;
        LevelViews views;

        public ComboCallouts(AudioService sfx, ParticlePool particles, FxOverlay overlay, FxMaterials fxMaterials)
        {
            this.sfx = sfx;
            this.particles = particles;
            this.overlay = overlay;
            this.fxMaterials = fxMaterials;
        }

        /// <summary>Starts a new level.</summary>
        public void Begin(LevelViews levelViews) => views = levelViews;

        /// <summary>The callout tier for a combo count, or -1 for none.</summary>
        public static int TierFor(int combo)
        {
            for (int i = Thresholds.Length - 1; i >= 0; i--)
                if (combo == Thresholds[i]) return i;
            int top = Thresholds[Thresholds.Length - 1];
            return combo > top && (combo - top) % RepeatEvery == 0 ? Thresholds.Length - 1 : -1;
        }

        /// <summary>A load landed at <paramref name="at"/>, making <paramref name="combo"/> in a row.</summary>
        public void Loaded(int combo, float at)
        {
            if (views == null) return;
            int tier = TierFor(combo);
            if (tier < 0) return;

            // In front of the bay, between dock and truck, so it rises through the truck and stays on the board.
            var anchor = views.Layout.Bay + new Vector3(-0.3f, 1.2f, -0.9f);
            var burst = views.Materials.Parcel(BurstColor[tier], true);
            var palette = fxMaterials.SparklePalette;
            string word = Words[tier];
            var color = Colors[tier];
            float size = FontSizes[tier];
            float pitch = Pitches[tier];
            views.Tweener.At(at + 0.04f, () =>
            {
                overlay.ShowCallout(word, color, size, anchor);
                particles.Emit(anchor, BurstSpec.Sparkle(10 + tier * 6), burst);
                particles.Emit(anchor, BurstSpec.Sparkle(4 + tier * 3), palette, palette.Length);
                sfx.Play(Sfx.Callout, pitch, 0.8f);
                if (tier >= 2) sfx.Play(Sfx.Sparkle, pitch, 0.5f);
                Haptics.Play(tier >= 2 ? Haptic.TruckFull : Haptic.Tick);
            });
        }
    }
}
