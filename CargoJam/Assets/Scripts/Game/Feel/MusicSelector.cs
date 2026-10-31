using System;
using CargoJam.Game.Presentation;
using CargoJam.Meta;

namespace CargoJam.Game.Feel
{
    /// <summary>How much energy a loop has (design v4 §7). The base is calm; the others are the livelier swaps.</summary>
    public enum MusicVariant
    {
        /// <summary>Pad and pluck only (day levels and the menu); soft ghost hats in the B half.</summary>
        Calm = 0,
        /// <summary>Kick, snare, hats and a bouncing bass under the same harmony (dusk levels).</summary>
        Groove = 1,
        /// <summary>Four on the floor, 16th hats, an octave-pumping bass, 8% faster (HARD levels).</summary>
        Drive = 2,
        /// <summary>Drive's rhythm section under the dark harmony (HARD storm finales).</summary>
        Storm = 3,
    }

    /// <summary>One generated loop: a destination style, an energy variant and a flavor (1 = a whole step up, swapped patterns).</summary>
    public readonly struct MusicCue : IEquatable<MusicCue>
    {
        public const int VariantCount = 4;
        public const int FlavorCount = 2;
        /// <summary>Number of distinct keys (<see cref="Key"/> is 0..KeyCount-1).</summary>
        public const int KeyCount = MusicSynth.StyleCount * VariantCount * FlavorCount;

        /// <summary>The destination style, 0..<see cref="MusicSynth.StyleCount"/>-1.</summary>
        public readonly int Style;
        public readonly MusicVariant Variant;
        /// <summary>0 for the first lap of the journey, 1 for the second (levels 61–120), alternating after.</summary>
        public readonly int Flavor;

        public MusicCue(int style, MusicVariant variant, int flavor)
        {
            Style = ((style % MusicSynth.StyleCount) + MusicSynth.StyleCount) % MusicSynth.StyleCount;
            Variant = variant;
            Flavor = flavor & 1;
        }

        /// <summary>((style*4 + variant)*2 + flavor): unique per cue, used as the cache key.</summary>
        public int Key => (Style * VariantCount + (int)Variant) * FlavorCount + Flavor;

        public static MusicCue FromKey(int key) =>
            new MusicCue(key / (VariantCount * FlavorCount), (MusicVariant)(key / FlavorCount % VariantCount), key % FlavorCount);

        /// <summary>The storm finale's darker harmony (root down an octave, low sparse arp).</summary>
        public bool Dark => Variant == MusicVariant.Storm;

        /// <summary>Drive and Storm: the fast, four-on-the-floor rhythm section.</summary>
        public bool Driving => Variant >= MusicVariant.Drive;

        public bool Equals(MusicCue other) => Key == other.Key;
        public override bool Equals(object obj) => obj is MusicCue other && Equals(other);
        public override int GetHashCode() => Key;
        public override string ToString() => $"music-{Style}-{Variant}-{Flavor}";
    }

    /// <summary>
    /// Picks the loop for a level (design v4 §7): the destination's style, a variant from the level's
    /// mood and difficulty, and a flavor that changes on the journey's second lap. Also names the cue
    /// to prefetch for the level after, so swaps are instant in normal play.
    /// </summary>
    public static class MusicSelector
    {
        /// <summary>The loop for level <paramref name="levelIndex"/> (0-based progress index).</summary>
        public static MusicCue ForLevel(int levelIndex, bool hard)
        {
            var mood = Journey.MoodFor(levelIndex);
            MusicVariant variant;
            if (hard) variant = mood == Mood.Storm ? MusicVariant.Storm : MusicVariant.Drive;
            else variant = mood == Mood.Dusk ? MusicVariant.Groove : MusicVariant.Calm;
            return new MusicCue(Journey.StopFor(levelIndex), variant, FlavorFor(levelIndex));
        }

        /// <summary>The menu always plays its destination's calm loop.</summary>
        public static MusicCue ForMenu(int progressIndex) =>
            new MusicCue(Journey.StopFor(progressIndex), MusicVariant.Calm, FlavorFor(progressIndex));

        /// <summary>Flavor = (leg / stops) % 2: the second lap of the journey (L61+) sounds different.</summary>
        public static int FlavorFor(int levelIndex) => Journey.LegFor(levelIndex) / Journey.StopCount % 2;

        /// <summary>
        /// The cue to play now and the one to prefetch. In a level the prefetch is the next level's cue
        /// (<see cref="LevelViews.NextHard"/> says whether it is HARD); on the menu it is the cue of
        /// the level the player is about to start (finales are always HARD).
        /// </summary>
        public static void Choose(in EnvironmentInfo info, LevelViews views, out MusicCue play, out MusicCue prefetch)
        {
            if (views != null)
            {
                play = ForLevel(info.LevelIndex, views.Hard);
                prefetch = ForLevel(info.LevelIndex + 1, views.NextHard);
            }
            else
            {
                play = ForMenu(info.LevelIndex);
                prefetch = ForLevel(info.LevelIndex, Journey.MoodFor(info.LevelIndex) == Mood.Storm);
            }
        }
    }
}
