using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Generates the background loops (design v4 §7): one style per journey destination (its own
    /// chord progression, tempo, arpeggio patterns, pluck, swing and drum mix), in four energy
    /// variants (<see cref="MusicVariant"/>) and two flavors. A loop is 16 bars, an A half and a B
    /// half: B walks the arp through PatternC/D, bars 8 and 16 carry drum and bass fills, and bar 16
    /// drops the arp for a breath before the loop comes round. Every event sits on an integer
    /// 16th-note grid (odd 16ths swung), so the loop is exactly 16 bars of samples and notes that ring
    /// past the end wrap to the start: every loop is seamless.
    ///
    /// Each loop comes with a 2-bar tension clip at the same tempo and swing (16th hats, a woodblock
    /// two octaves above the tonic, a sub pulse) that the player layers on beat-locked.
    ///
    /// Mono, 16 kHz, 35–51 s per loop (2.3–3.2 MB as float PCM). Rendering is spread over frames with
    /// a per-frame sample budget so it never hitches: composing, clearing, synthesis (the pad is
    /// rendered once for the A half and copied into the B half), normalization and the upload into the
    /// clip all run in budgeted slices, in one persistent buffer, so a render allocates no large garbage.
    /// </summary>
    public static class MusicSynth
    {
        /// <summary>
        /// Sample rate. 16 kHz keeps two resident cues (playing + prefetch, 7.3 MB at worst) inside the
        /// audio memory budget; the content is soft pads and plucks, and 8 kHz of air is enough for hats.
        /// </summary>
        public const int Rate = 16000;
        const int Bars = 16;
        const int HalfBars = Bars / 2;
        const int Steps = 16;
        const int TensionBars = 2;

        /// <summary>Number of destination styles (style = journey stop).</summary>
        public const int StyleCount = 6;

        /// <summary>Drive and Storm play this much faster.</summary>
        const float DriveTempo = 1.08f;
        /// <summary>Drive and Storm thin the pad under the rhythm section.</summary>
        const float DrivePad = 0.8f;
        /// <summary>Velocity of an 'o' (ghost) hit; 'x' is 1.</summary>
        const float GhostVelocity = 0.45f;
        const float PadRelease = 1.2f;
        /// <summary>The longest a pluck rings; a fast decay ends it sooner (4.6/decay is -40 dB).</summary>
        const float MaxPluck = 0.9f;

        // RMS loudness targets after mixing (peaks are held under 0.95 regardless).
        // (The v3 loops were peak-normalized and measured about 0.21 RMS.)
        const float CalmRms = 0.19f;
        // Busier loops carry more sub-bass, which a phone speaker drops: these targets keep the audible
        // band (above ~250 Hz) at or above the calm loops'.
        const float GrooveRms = 0.22f;
        const float DriveRms = 0.235f;
        const float TensionRms = 0.14f;
        const float MaxPeak = 0.95f;
        /// <summary>How far past the peak limit a loop may be driven into the soft knee (≈ +4 dB).</summary>
        const float ClipHeadroom = 1.6f;
        const float ClipKnee = 0.7f;

        enum Voice : byte { Pad, Pluck, Kick, Snare, Hat, Crash, Bass, Block, Sub }

        struct Note
        {
            public int Start;
            public int Length;
            public float Freq;
            public float Gain;
            public float Decay;
            public Voice Voice;
        }

        /// <summary>Oscillator and envelope state carried across render slices of one note.</summary>
        struct Osc
        {
            public float P1, P2, P3;
            public float E1, E2, E3;
            public float Last;
        }

        sealed class Style
        {
            public float Bpm;
            /// <summary>Four chords, two bars each: pad notes (MIDI), lowest first.</summary>
            public int[][] Pads;
            /// <summary>The notes the arpeggio walks for each chord.</summary>
            public int[][] Arps;
            /// <summary>
            /// Eighth-note patterns (index into the arp notes, +4 = an octave up, -1 = rest). A and B
            /// alternate bar by bar in the A half, C and D in the B half.
            /// </summary>
            public int[] PatternA, PatternB, PatternC, PatternD;
            /// <summary>Pluck decay rate (higher is shorter).</summary>
            public float Decay = 6.5f;
            public float ArpGain = 0.16f;
            /// <summary>How late odd 16ths land, as a fraction of a 16th.</summary>
            public float Swing;
            // Rhythm section mix.
            public float Kick = 0.42f, Snare = 0.28f, Hat = 0.14f, Bass = 0.2f;
        }

        /// <summary>A variant's rhythm section. Hit patterns are 16 steps of 'x' (accent), 'o' (ghost) or '.'.</summary>
        sealed class Rhythm
        {
            public string Kick, KickFill, Snare, SnareFill, Hat, HatB;
            /// <summary>Semitones above the chord root per 16th (an 8th-note long each); -1 is a rest.</summary>
            public int[] Bass, BassFill;
            /// <summary>How deep the pad ducks under each kick (0 = not at all).</summary>
            public float Pump;
            /// <summary>Crash on the downbeat of bar 1 (after the loop's fill) and/or bar 9.</summary>
            public bool CrashA, CrashB;
        }

        static readonly Style[] Styles =
        {
            // 0 Harbor Town: the original loop. C – Am – F – G, bright and easy.
            new Style
            {
                Bpm = 90f,
                Pads = new[] { new[] { 48, 55, 64 }, new[] { 45, 52, 60 }, new[] { 41, 57, 60 }, new[] { 43, 55, 62 } },
                Arps = new[] { new[] { 72, 76, 79, 81 }, new[] { 69, 72, 76, 79 }, new[] { 69, 72, 74, 76 }, new[] { 67, 69, 74, 79 } },
                PatternA = new[] { 0, 1, 2, 3, 4, 3, 2, 1 },
                PatternB = new[] { 2, -1, 3, 4, -1, 5, 3, -1 },
                PatternC = new[] { 0, 2, 4, 2, 1, 3, 5, 3 },
                PatternD = new[] { 4, -1, 3, -1, 2, 1, -1, -1 },
                Swing = 0.06f,
            },
            // 1 Farmlands: G – D – Em – C, a touch quicker and bouncier.
            new Style
            {
                Bpm = 98f,
                Pads = new[] { new[] { 43, 55, 59 }, new[] { 38, 54, 57 }, new[] { 40, 55, 59 }, new[] { 36, 55, 64 } },
                Arps = new[] { new[] { 67, 71, 74, 79 }, new[] { 62, 66, 69, 74 }, new[] { 64, 67, 71, 76 }, new[] { 64, 67, 72, 76 } },
                PatternA = new[] { 0, 2, 1, 3, 0, 2, 4, 2 },
                PatternB = new[] { 0, -1, 2, 3, -1, 2, 4, -1 },
                PatternC = new[] { 0, 4, 2, 4, 1, 4, 3, 4 },
                PatternD = new[] { 2, -1, 0, -1, 3, -1, 4, -1 },
                Decay = 7.5f,
                Swing = 0.10f,
                Snare = 0.3f,
            },
            // 2 Canyon Highway: Am – G – F – E, slow and dusty, lots of air between notes.
            new Style
            {
                Bpm = 84f,
                Pads = new[] { new[] { 45, 52, 60 }, new[] { 43, 50, 59 }, new[] { 41, 48, 57 }, new[] { 40, 52, 56 } },
                Arps = new[] { new[] { 69, 72, 76, 81 }, new[] { 67, 71, 74, 79 }, new[] { 65, 69, 72, 77 }, new[] { 64, 68, 71, 76 } },
                PatternA = new[] { 0, -1, 2, -1, 1, -1, 3, 2 },
                PatternB = new[] { 0, -1, -1, 2, -1, 1, -1, -1 },
                PatternC = new[] { 3, -1, 2, -1, 0, -1, 1, -1 },
                PatternD = new[] { 0, -1, -1, -1, 4, -1, 2, -1 },
                Decay = 5f,
                ArpGain = 0.18f,
                Swing = 0.08f,
                Kick = 0.46f, Hat = 0.12f, Bass = 0.22f,
            },
            // 3 Pine Ridge: F – C – Dm – Bb, an unhurried folk walk.
            new Style
            {
                Bpm = 80f,
                Pads = new[] { new[] { 41, 57, 60 }, new[] { 36, 55, 64 }, new[] { 38, 57, 65 }, new[] { 34, 53, 62 } },
                Arps = new[] { new[] { 65, 69, 72, 77 }, new[] { 64, 67, 72, 76 }, new[] { 62, 65, 69, 74 }, new[] { 62, 65, 70, 74 } },
                PatternA = new[] { 0, 2, 1, 3, 2, 4, 3, 1 },
                PatternB = new[] { 0, -1, 2, -1, 3, -1, -1, -1 },
                PatternC = new[] { 4, 3, 2, 1, 0, 1, 2, 3 },
                PatternD = new[] { 2, -1, 1, -1, 0, -1, -1, -1 },
                Decay = 5.5f,
                Swing = 0.05f,
                Snare = 0.25f,
            },
            // 4 Metro City: Fmaj7 – Em7 – Dm7 – Cmaj7, lo-fi and syncopated.
            new Style
            {
                Bpm = 100f,
                Pads = new[] { new[] { 41, 57, 64 }, new[] { 40, 55, 62 }, new[] { 38, 53, 60 }, new[] { 36, 55, 59 } },
                Arps = new[] { new[] { 69, 72, 76, 77 }, new[] { 67, 71, 74, 76 }, new[] { 65, 69, 72, 74 }, new[] { 64, 67, 71, 72 } },
                PatternA = new[] { 0, -1, 1, 2, -1, 3, -1, 2 },
                PatternB = new[] { -1, 2, -1, 1, 3, -1, 4, -1 },
                PatternC = new[] { 3, -1, -1, 2, -1, 1, 0, -1 },
                PatternD = new[] { 0, -1, 4, -1, -1, 3, -1, 2 },
                Decay = 9f,
                ArpGain = 0.17f,
                Swing = 0.15f,
                Hat = 0.16f,
            },
            // 5 Frost Port: Em – C – G – D, slow with a high, bell-like arp.
            new Style
            {
                Bpm = 76f,
                Pads = new[] { new[] { 40, 55, 59 }, new[] { 36, 55, 64 }, new[] { 43, 55, 62 }, new[] { 38, 54, 62 } },
                Arps = new[] { new[] { 76, 79, 83, 88 }, new[] { 76, 79, 84, 88 }, new[] { 74, 79, 83, 86 }, new[] { 74, 78, 81, 86 } },
                PatternA = new[] { 0, 2, 4, 2, 1, 3, 5, 3 },
                PatternB = new[] { 0, -1, 4, -1, 2, -1, -1, -1 },
                PatternC = new[] { 5, 3, 4, 2, 3, 1, 2, 0 },
                PatternD = new[] { 4, -1, 2, -1, 0, -1, -1, -1 },
                Decay = 3.8f,
                ArpGain = 0.12f,
                Kick = 0.38f, Hat = 0.13f,
            },
        };

        static readonly Rhythm Calm = new Rhythm
        {
            HatB = "..o...o...o...o.",
        };

        static readonly Rhythm Groove = new Rhythm
        {
            Kick = "x.....x.x.......",
            KickFill = "x.....x.x...x.x.",
            Snare = "....x.......x...",
            SnareFill = "....x.......xoxx",
            Hat = "..x...x...x...x.",
            // The B half lifts with ghost hats between the off-beats.
            HatB = "..x.o.x...x.o.x.",
            Bass = new[] { 0, -1, -1, 0, -1, -1, 12, -1, 0, -1, -1, 7, -1, -1, 12, -1 },
            BassFill = new[] { 0, -1, -1, 0, -1, -1, 12, -1, 0, -1, 7, -1, 12, -1, 7, -1 },
            Pump = 0.22f,
            CrashB = true,
        };

        static readonly Rhythm Drive = new Rhythm
        {
            Kick = "x...x...x...x...",
            KickFill = "x...x...x.x.xxxx",
            Snare = "....x.......x..o",
            SnareFill = "....x.......oxxx",
            Hat = "xoxoxoxoxoxoxoxo",
            HatB = "xoxoxoxoxoxoxoxo",
            Bass = new[] { 0, -1, 0, -1, 12, -1, 0, -1, 0, -1, 0, -1, 12, -1, 7, -1 },
            BassFill = new[] { 0, -1, 0, -1, 12, -1, 0, -1, 7, -1, 12, -1, 7, -1, 12, -1 },
            Pump = 0.4f,
            CrashA = true,
            CrashB = true,
        };

        static Style StyleFor(int style) => Styles[((style % StyleCount) + StyleCount) % StyleCount];

        static Rhythm RhythmFor(MusicVariant variant) => variant switch
        {
            MusicVariant.Groove => Groove,
            MusicVariant.Drive => Drive,
            MusicVariant.Storm => Drive,
            _ => Calm,
        };

        /// <summary>Samples per 16th note: an integer, so bars and loops are exact.</summary>
        static int StepSamples(in MusicCue cue)
        {
            float bpm = StyleFor(cue.Style).Bpm * (cue.Driving ? DriveTempo : 1f);
            return Mathf.RoundToInt(Rate * 15f / bpm);
        }

        /// <summary>Length of a loop in samples (exactly 16 bars).</summary>
        public static int LoopSamples(MusicCue cue) => StepSamples(cue) * Steps * Bars;

        /// <summary>Length of a cue's tension clip in samples (exactly 2 bars).</summary>
        public static int TensionSamples(MusicCue cue) => StepSamples(cue) * Steps * TensionBars;

        /// <summary>Length of a loop in seconds.</summary>
        public static float LoopSeconds(MusicCue cue) => LoopSamples(cue) / (float)Rate;

        /// <summary>The render buffer, sized once for the longest loop and shared by every render.</summary>
        static float[] buffer;
        /// <summary>One beat of samples, copied into the clip per <see cref="AudioClip.SetData(float[], int)"/> call.</summary>
        static float[] beat;

        /// <summary>The longest loop over every cue (the slowest style's calm loop), in samples.</summary>
        static int MaxLoopSamples()
        {
            int max = 0;
            for (int key = 0; key < MusicCue.KeyCount; key++) max = Math.Max(max, LoopSamples(MusicCue.FromKey(key)));
            return max;
        }

        /// <summary>
        /// Renders a cue's loop and its tension clip over several frames (about
        /// <paramref name="budget"/> sample writes per frame, never any work on the calling frame), then
        /// hands both clips to <paramref name="done"/> (main, tension). Renders share one buffer, so only
        /// one may run at a time (<see cref="MusicPlayer"/> renders strictly one after another).
        /// </summary>
        public static IEnumerator Render(MusicCue cue, int budget, Action<AudioClip, AudioClip> done)
        {
            // Nothing on the caller's frame (it may be the one that starts an arrival pan).
            yield return null;

            var style = StyleFor(cue.Style);
            var rhythm = RhythmFor(cue.Variant);
            int step = StepSamples(cue);
            int swing = Mathf.RoundToInt(style.Swing * step);
            int transpose = cue.Flavor == 1 ? 2 : 0;
            int total = step * Steps * Bars;
            int half = step * Steps * HalfBars;

            var pads = new List<Note>(16);
            var notes = new List<Note>(1024);
            var kicks = new List<int>(80);
            ComposePad(style, cue, step, transpose, pads);
            ComposeArp(style, cue, step, swing, transpose, notes);
            ComposeRhythm(style, rhythm, step, swing, transpose, notes, kicks);
            // The one large allocation, made once for the longest loop.
            if (buffer == null) buffer = new float[MaxLoopSamples()];
            if (beat == null || beat.Length != 4 * step) beat = new float[4 * step];
            yield return null;

            var data = buffer;
            int left = budget;
            // Clearing is a memset: a few samples' worth of budget per slice.
            for (int from = 0; from < total;)
            {
                int count = Math.Min(total - from, left * 8);
                Array.Clear(data, from, count);
                from += count;
                if ((left -= count / 8 + 1) <= 0)
                {
                    left = budget;
                    yield return null;
                }
            }

            // The pad repeats every half, so it is rendered once (wrapping within the A half)...
            foreach (var note in pads)
            {
                var osc = new Osc();
                for (int from = 0; from < note.Length;)
                {
                    int count = Math.Min(note.Length - from, left);
                    Synthesize(data, half, note, ref osc, from, count);
                    from += count;
                    if ((left -= count) <= 0)
                    {
                        left = budget;
                        yield return null;
                    }
                }
            }
            // ...then copied into the B half, and ducked under the kicks.
            for (int from = 0; from < half;)
            {
                int count = Math.Min(half - from, left * 4);
                Array.Copy(data, from, data, half + from, count);
                from += count;
                if ((left -= count / 4 + 1) <= 0)
                {
                    left = budget;
                    yield return null;
                }
            }
            if (rhythm.Pump > 0f && kicks.Count > 0)
            {
                var pump = new Pump(rhythm.Pump, kicks, total);
                if ((left -= Pump.WarmupSamples(total) / 2) <= 0)
                {
                    left = budget;
                    yield return null;
                }
                for (int from = 0; from < total;)
                {
                    int count = Math.Min(total - from, left * 2);
                    pump.Apply(data, from, count);
                    from += count;
                    if ((left -= count / 2 + 1) <= 0)
                    {
                        left = budget;
                        yield return null;
                    }
                }
            }

            foreach (var note in notes)
            {
                var osc = new Osc();
                for (int from = 0; from < note.Length;)
                {
                    int count = Math.Min(note.Length - from, left);
                    Synthesize(data, total, note, ref osc, from, count);
                    from += count;
                    if ((left -= count) <= 0)
                    {
                        left = budget;
                        yield return null;
                    }
                }
            }

            float target = cue.Variant == MusicVariant.Calm ? CalmRms : cue.Variant == MusicVariant.Groove ? GrooveRms : DriveRms;
            yield return Normalize(data, total, target, budget);
            var main = AudioClip.Create(cue.ToString(), total, 1, Rate, false);
            yield return Upload(main, data, total, budget);

            // The tension layer reuses the start of the buffer: 2 bars, a few slices.
            int length = step * Steps * TensionBars;
            Array.Clear(data, 0, length);
            notes.Clear();
            ComposeTension(style, step, swing, transpose, notes);
            left = budget - length / 8;
            foreach (var note in notes)
            {
                var osc = new Osc();
                Synthesize(data, length, note, ref osc, 0, note.Length);
                if ((left -= note.Length) <= 0)
                {
                    left = budget;
                    yield return null;
                }
            }
            yield return Normalize(data, length, TensionRms, budget);
            var layer = AudioClip.Create(cue + "-tension", length, 1, Rate, false);
            yield return Upload(layer, data, length, budget);

            done(main, layer);
        }

        /// <summary>
        /// Copies <paramref name="length"/> samples into <paramref name="clip"/> a beat at a time
        /// (<see cref="AudioClip.SetData(float[], int)"/> with an offset), within the frame budget.
        /// Loops and tension clips are whole numbers of beats.
        /// </summary>
        static IEnumerator Upload(AudioClip clip, float[] data, int length, int budget)
        {
            int chunk = beat.Length;
            int left = budget;
            for (int from = 0; from < length; from += chunk)
            {
                Array.Copy(data, from, beat, 0, chunk);
                clip.SetData(beat, from);
                if ((left -= chunk / 2) <= 0)
                {
                    left = budget;
                    yield return null;
                }
            }
        }

        // ---- Composition ----------------------------------------------------------------------------

        /// <summary>Sample offset of a 16th step (from the loop start): odd 16ths land late by the swing.</summary>
        static int At(int step16, int stepSamples, int swing) => step16 * stepSamples + ((step16 & 1) != 0 ? swing : 0);

        /// <summary>The A half's pad: four chords of two bars, each ringing into the next.</summary>
        static void ComposePad(Style style, MusicCue cue, int step, int transpose, List<Note> pads)
        {
            int chordSamples = 2 * Steps * step;
            float gain = cue.Driving ? DrivePad : 1f;
            for (int c = 0; c < style.Pads.Length; c++)
            {
                var pad = style.Pads[c];
                for (int n = 0; n < pad.Length; n++)
                {
                    // Dark: the root drops an octave for weight (not below A1, where it would only be mud).
                    bool drop = cue.Dark && n == 0 && pad[n] - 12 >= 33;
                    int midi = (drop ? pad[n] - 12 : pad[n]) + transpose;
                    pads.Add(new Note
                    {
                        Start = c * chordSamples, Length = chordSamples + Samples(PadRelease),
                        Freq = Hz(midi), Gain = (drop ? 0.14f : 0.11f) * gain, Voice = Voice.Pad,
                    });
                }
            }
        }

        /// <summary>The arpeggio over all 16 bars (none in bar 16), with a dotted-8th echo when driving.</summary>
        static void ComposeArp(Style style, MusicCue cue, int step, int swing, int transpose, List<Note> notes)
        {
            float decay = style.Decay * (cue.Dark ? 0.8f : 1f);
            int length = Samples(Mathf.Min(MaxPluck, 4.6f / decay));
            float gain = style.ArpGain * (cue.Dark ? 0.8f : 1f);
            int total = step * Steps * Bars;
            for (int bar = 0; bar < Bars - 1; bar++)
            {
                int c = bar % HalfBars / 2;
                bool second = bar >= HalfBars;
                // Flavor 1 swaps the halves' patterns.
                if (cue.Flavor == 1) second = !second;
                bool firstBarOfChord = bar % 2 == 0;
                // Dark keeps the sparse patterns throughout.
                int[] pattern = second
                    ? (firstBarOfChord && !cue.Dark ? style.PatternC : style.PatternD)
                    : (firstBarOfChord && !cue.Dark ? style.PatternA : style.PatternB);
                var set = style.Arps[c];
                for (int e = 0; e < 8; e++)
                {
                    int idx = pattern[e];
                    if (idx < 0) continue;
                    int midi = set[idx % set.Length] + 12 * (idx / set.Length) - (cue.Dark ? 12 : 0) + transpose;
                    int start = At(bar * Steps + e * 2, step, swing);
                    // Downbeats a touch louder, for a lilt.
                    float accent = e % 2 == 0 ? 1f : 0.75f;
                    var note = new Note
                    {
                        Start = start, Length = length, Freq = Hz(midi), Gain = gain * accent, Voice = Voice.Pluck,
                        Decay = decay,
                    };
                    notes.Add(note);
                    if (!cue.Driving) continue;
                    // A quiet dotted-8th echo: the arp turns into a running figure under the drums.
                    note.Start = At(bar * Steps + e * 2 + 3, step, swing) % total;
                    note.Gain *= 0.32f;
                    notes.Add(note);
                }
            }
        }

        /// <summary>Kick, snare, hats, crash and bass for the variant; collects kick starts for the pump.</summary>
        static void ComposeRhythm(Style style, Rhythm rhythm, int step, int swing, int transpose,
            List<Note> notes, List<int> kicks)
        {
            for (int bar = 0; bar < Bars; bar++)
            {
                bool fill = bar == HalfBars - 1 || bar == Bars - 1;
                bool b = bar >= HalfBars;
                int first = bar * Steps;
                Hits(fill && rhythm.KickFill != null ? rhythm.KickFill : rhythm.Kick, first, step, swing,
                    Voice.Kick, style.Kick, 0.25f, notes, kicks);
                Hits(fill && rhythm.SnareFill != null ? rhythm.SnareFill : rhythm.Snare, first, step, swing,
                    Voice.Snare, style.Snare, 0.18f, notes, null);
                Hits(b ? rhythm.HatB : rhythm.Hat, first, step, swing, Voice.Hat, style.Hat, 0.05f, notes, null);
                if ((bar == 0 && rhythm.CrashA) || (bar == HalfBars && rhythm.CrashB))
                    notes.Add(new Note { Start = first * step, Length = Samples(1.2f), Gain = style.Hat * 1.3f, Voice = Voice.Crash });

                var bass = fill && rhythm.BassFill != null ? rhythm.BassFill : rhythm.Bass;
                if (bass == null) continue;
                // The chord's root, lifted out of the mud so small speakers still carry its harmonics.
                int root = style.Pads[bar % HalfBars / 2][0];
                if (root < 40) root += 12;
                int eighth = 2 * step;
                for (int s = 0; s < Steps; s++)
                {
                    if (bass[s] < 0) continue;
                    float accent = s % 4 == 0 ? 1f : 0.8f;
                    notes.Add(new Note
                    {
                        Start = At(first + s, step, swing), Length = eighth, Freq = Hz(root + bass[s] + transpose),
                        Gain = style.Bass * accent, Voice = Voice.Bass,
                    });
                }
            }
        }

        static void Hits(string pattern, int firstStep, int step, int swing, Voice voice, float gain, float seconds,
            List<Note> notes, List<int> starts)
        {
            if (pattern == null) return;
            for (int s = 0; s < Steps; s++)
            {
                char ch = pattern[s];
                if (ch == '.') continue;
                int start = At(firstStep + s, step, swing);
                notes.Add(new Note
                {
                    Start = start, Length = Samples(seconds), Gain = gain * (ch == 'x' ? 1f : GhostVelocity), Voice = voice,
                });
                starts?.Add(start);
            }
        }

        /// <summary>
        /// Two bars of urgency: hats on every 16th, a tick-tock woodblock two octaves above the tonic
        /// and a sub pulse on the beat.
        /// </summary>
        static void ComposeTension(Style style, int step, int swing, int transpose, List<Note> notes)
        {
            int tonic = style.Pads[0][0] + transpose;
            for (int s = 0; s < Steps * TensionBars; s++)
            {
                int start = At(s, step, swing);
                notes.Add(new Note
                {
                    Start = start, Length = Samples(0.05f), Gain = (s % 2 == 0 ? 0.5f : 0.3f), Voice = Voice.Hat,
                });
                if (s % 4 == 0)
                {
                    notes.Add(new Note
                    {
                        Start = start, Length = Samples(0.09f), Freq = Hz(tonic + 24),
                        Gain = s % 8 == 0 ? 0.42f : 0.28f, Voice = Voice.Block,
                    });
                    notes.Add(new Note
                    {
                        Start = start, Length = Samples(0.3f), Freq = Hz(tonic < 36 ? tonic + 12 : tonic),
                        Gain = 0.5f, Voice = Voice.Sub,
                    });
                }
            }
        }

        // ---- Rendering ------------------------------------------------------------------------------

        /// <summary>
        /// Adds samples [from, from+count) of a note into <paramref name="data"/>, wrapping at
        /// <paramref name="wrap"/>. <paramref name="osc"/> carries phases and envelopes between slices,
        /// so a note can be split across frames without a seam.
        /// </summary>
        static void Synthesize(float[] data, int wrap, in Note n, ref Osc osc, int from, int count)
        {
            const float dt = 1f / Rate;
            float d = n.Length * dt;
            int idx = (n.Start + from) % wrap;
            int end = from + count;
            float f = n.Freq * dt;
            switch (n.Voice)
            {
                case Voice.Pad:
                {
                    // Two slightly detuned sines beat slowly; a quiet octave adds air.
                    float g = 0.5f * n.Gain;
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float s = Dsp.FastSine(osc.P1) + Dsp.FastSine(osc.P2) + 0.18f * Dsp.FastSine(osc.P3);
                        osc.P1 = Dsp.Advance(osc.P1, f * 0.997f);
                        osc.P2 = Dsp.Advance(osc.P2, f * 1.003f);
                        osc.P3 = Dsp.Advance(osc.P3, f * 2f);
                        float attack = t < 0.7f ? t / 0.7f : 1f;
                        float release = Mathf.Clamp01((d - t) / PadRelease);
                        data[idx] += s * attack * release * g;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Pluck:
                {
                    if (from == 0) osc.E1 = osc.E2 = 1f;
                    float k1 = Mathf.Exp(-n.Decay * dt), k2 = Mathf.Exp(-14f * dt);
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float env = Mathf.Min(1f, t * 300f) * osc.E1 * Mathf.Clamp01((d - t) * 20f);
                        float tone = Dsp.FastSine(osc.P1) + 0.3f * Dsp.FastSine(osc.P2) * osc.E2;
                        osc.P1 = Dsp.Advance(osc.P1, f);
                        osc.P2 = Dsp.Advance(osc.P2, f * 2f);
                        osc.E1 *= k1;
                        osc.E2 *= k2;
                        data[idx] += tone * env * n.Gain;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Kick:
                {
                    // A sine sweeping 120 → 45 Hz with a short beater click, so it reads on small speakers.
                    if (from == 0) osc.E1 = osc.E2 = osc.E3 = 1f;
                    float kPitch = Mathf.Exp(-18f * dt), kAmp = Mathf.Exp(-7f * dt), kClick = Mathf.Exp(-500f * dt);
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float body = Dsp.FastSine(osc.P1) * osc.E2;
                        osc.P1 = Dsp.Advance(osc.P1, (45f + 75f * osc.E1) * dt);
                        float click = Dsp.Noise((uint)(n.Start + i)) * osc.E3 * 0.45f;
                        osc.E1 *= kPitch;
                        osc.E2 *= kAmp;
                        osc.E3 *= kClick;
                        data[idx] += (body + click) * Mathf.Min(1f, t * 2000f) * Mathf.Clamp01((d - t) * 40f) * n.Gain;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Snare:
                {
                    // Hash noise (brightened by differencing) over a 180 Hz body.
                    if (from == 0) osc.E1 = osc.E2 = 1f;
                    float kNoise = Mathf.Exp(-20f * dt), kTone = Mathf.Exp(-28f * dt), step = 180f * dt;
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float x = Dsp.Noise((uint)(n.Start + i) ^ 0x5bd1e995u);
                        float noise = (0.6f * x + 0.4f * (x - osc.Last)) * osc.E1;
                        osc.Last = x;
                        float tone = Dsp.FastSine(osc.P1) * osc.E2 * 0.55f;
                        osc.P1 = Dsp.Advance(osc.P1, step);
                        osc.E1 *= kNoise;
                        osc.E2 *= kTone;
                        data[idx] += (noise * 0.75f + tone) * Mathf.Min(1f, t * 3000f) * Mathf.Clamp01((d - t) * 40f) * n.Gain;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Hat:
                case Voice.Crash:
                {
                    // High-passed noise (first difference): a short tick, or a long wash for the crash.
                    if (from == 0) osc.E1 = 1f;
                    float k = Mathf.Exp(-(n.Voice == Voice.Hat ? 75f : 3.2f) * dt);
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float x = Dsp.Noise((uint)(n.Start + i) ^ 0x2545f491u);
                        float hp = (x - osc.Last) * 0.5f;
                        osc.Last = x;
                        data[idx] += hp * osc.E1 * Mathf.Clamp01((d - t) * 60f) * n.Gain;
                        osc.E1 *= k;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Bass:
                {
                    // sin + 0.4·sin2 + 0.15·sin3: round, with enough upper partials for a phone speaker.
                    if (from == 0) osc.E1 = 1f;
                    float k = Mathf.Exp(-2.5f * dt);
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float s = Dsp.FastSine(osc.P1) + 0.4f * Dsp.FastSine(osc.P2) + 0.15f * Dsp.FastSine(osc.P3);
                        osc.P1 = Dsp.Advance(osc.P1, f);
                        osc.P2 = Dsp.Advance(osc.P2, f * 2f);
                        osc.P3 = Dsp.Advance(osc.P3, f * 3f);
                        float env = Mathf.Min(1f, t * 250f) * osc.E1 * Mathf.Clamp01((d - t) / 0.015f);
                        osc.E1 *= k;
                        data[idx] += s * env * n.Gain;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Block:
                {
                    // Woodblock: a fast-decaying sine with an inharmonic partial and a tiny click.
                    if (from == 0) osc.E1 = osc.E2 = osc.E3 = 1f;
                    float k1 = Mathf.Exp(-38f * dt), k2 = Mathf.Exp(-60f * dt), k3 = Mathf.Exp(-900f * dt);
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float s = Dsp.FastSine(osc.P1) * osc.E1 + 0.45f * Dsp.FastSine(osc.P2) * osc.E2
                                  + Dsp.Noise((uint)(n.Start + i)) * osc.E3 * 0.2f;
                        osc.P1 = Dsp.Advance(osc.P1, f);
                        osc.P2 = Dsp.Advance(osc.P2, f * 2.67f);
                        osc.E1 *= k1;
                        osc.E2 *= k2;
                        osc.E3 *= k3;
                        data[idx] += s * Mathf.Clamp01((d - t) * 60f) * n.Gain;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
                case Voice.Sub:
                {
                    if (from == 0) osc.E1 = 1f;
                    float k = Mathf.Exp(-7f * dt);
                    for (int i = from; i < end; i++)
                    {
                        float t = i * dt;
                        float s = Dsp.FastSine(osc.P1) + 0.35f * Dsp.FastSine(osc.P2);
                        osc.P1 = Dsp.Advance(osc.P1, f);
                        osc.P2 = Dsp.Advance(osc.P2, f * 2f);
                        data[idx] += s * Mathf.Min(1f, t * 200f) * osc.E1 * Mathf.Clamp01((d - t) * 30f) * n.Gain;
                        osc.E1 *= k;
                        if (++idx == wrap) idx = 0;
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Sidechain-style pump: the pad dips by <c>depth</c> under every kick and swells back, which
        /// makes the groove breathe. Smooth (6 ms attack, 110 ms release) and seamless at the wrap.
        /// </summary>
        struct Pump
        {
            readonly float depth;
            readonly List<int> kicks;
            readonly int total;
            readonly float attack, release;
            float level;
            int attackLeft;
            int next;

            public Pump(float depth, List<int> kicks, int total)
            {
                this.depth = depth;
                this.kicks = kicks;
                this.total = total;
                kicks.Sort();
                attack = 1f - Mathf.Exp(-1f / (0.0015f * Rate));
                release = Mathf.Exp(-1f / (0.11f * Rate));
                level = 0f;
                attackLeft = 0;
                next = 0;

                // Warm up over the loop's last two seconds so the state at sample 0 matches its end.
                int warm = WarmupSamples(total);
                int k = 0;
                while (k < kicks.Count && kicks[k] < total - warm) k++;
                for (int i = total - warm; i < total; i++)
                {
                    if (k < kicks.Count && kicks[k] == i)
                    {
                        attackLeft = Samples(0.006f);
                        k++;
                    }
                    Tick();
                }
            }

            /// <summary>Samples of loop tail the constructor runs through (charged to the frame budget).</summary>
            public static int WarmupSamples(int total) => Mathf.Min(total, Rate * 2);

            void Tick()
            {
                if (attackLeft > 0)
                {
                    level += (1f - level) * attack;
                    attackLeft--;
                }
                else level *= release;
            }

            public void Apply(float[] data, int from, int count)
            {
                int end = from + count;
                for (int i = from; i < end; i++)
                {
                    while (next < kicks.Count && kicks[next] <= i)
                    {
                        if (kicks[next] == i) attackLeft = Samples(0.006f);
                        next++;
                    }
                    Tick();
                    data[i] *= 1f - depth * level;
                }
            }
        }

        /// <summary>
        /// RMS-targeted normalization: gain = min(target/rms, <see cref="MaxPeak"/>/peak), so calm and
        /// busy loops sit at a similar loudness without clipping. Drum loops have a high crest factor
        /// and would otherwise be held quieter than the calm ones by their kick transients, so first
        /// the loop is driven up to <see cref="ClipHeadroom"/>× past its peak limit into a soft knee
        /// that rounds off only those transients. Works on the first <c>length</c> samples, spread over
        /// frames like the render.
        /// </summary>
        static IEnumerator Normalize(float[] data, int length, float targetRms, int budget)
        {
            int slice = budget * 4;
            double sum = 0.0;
            float peak = 0.0001f;
            for (int from = 0; from < length; from += slice)
            {
                Measure(data, from, Math.Min(length, from + slice), ref sum, ref peak);
                yield return null;
            }
            float drive = Mathf.Min(targetRms / Rms(sum, length), ClipHeadroom * MaxPeak / peak);

            // Drive into the knee and re-measure in the same pass.
            sum = 0.0;
            peak = 0.0001f;
            for (int from = 0; from < length; from += slice)
            {
                int end = Math.Min(length, from + slice);
                for (int i = from; i < end; i++) data[i] = SoftClip(data[i] * drive);
                Measure(data, from, end, ref sum, ref peak);
                yield return null;
            }
            float gain = Mathf.Min(targetRms / Rms(sum, length), MaxPeak / peak);
            for (int from = 0; from < length; from += slice)
            {
                int end = Math.Min(length, from + slice);
                for (int i = from; i < end; i++) data[i] *= gain;
                yield return null;
            }
        }

        static void Measure(float[] data, int from, int end, ref double sum, ref float peak)
        {
            for (int i = from; i < end; i++)
            {
                float x = data[i];
                sum += x * x;
                float a = x < 0f ? -x : x;
                if (a > peak) peak = a;
            }
        }

        static float Rms(double sum, int count) => Mathf.Max(1e-6f, (float)Math.Sqrt(sum / count));

        /// <summary>Linear below <see cref="ClipKnee"/>, then a tanh curve that never passes <see cref="MaxPeak"/>.</summary>
        static float SoftClip(float x)
        {
            float a = x < 0f ? -x : x;
            if (a <= ClipKnee) return x;
            const float range = MaxPeak - ClipKnee;
            float y = ClipKnee + range * (float)Math.Tanh((a - ClipKnee) / range);
            return x < 0f ? -y : y;
        }

        static int Samples(float seconds) => Mathf.RoundToInt(seconds * Rate);

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
