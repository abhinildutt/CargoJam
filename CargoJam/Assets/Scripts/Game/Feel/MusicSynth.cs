using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Generates the background loops: one 8-bar style per journey destination (its own chord
    /// progression, tempo, arpeggio patterns and pluck), each a soft detuned pad with a plucky
    /// arpeggio on top. A dark variant (storm finales) drops the pad's root an octave, takes the arp
    /// down, thins it to the sparse pattern and slows the tempo a touch. Mono, 22.05 kHz (~19–25 s,
    /// 0.8–1.1 MB). Notes that ring past the end wrap around to the start, so every loop is seamless.
    /// Rendering is spread over frames with a per-frame sample budget so it never hitches.
    /// </summary>
    public static class MusicSynth
    {
        const int Rate = 22050;
        const int Bars = 8;
        const int BeatsPerBar = 4;

        /// <summary>Number of destination styles (style = journey stop).</summary>
        public const int StyleCount = 6;

        enum Voice { Pad, Pluck }

        struct Note
        {
            public int Start;
            public int Length;
            public float Freq;
            public float Gain;
            public Voice Voice;
            public float Decay;
        }

        sealed class Style
        {
            public float Bpm;
            /// <summary>Four chords, two bars each: pad notes (MIDI).</summary>
            public int[][] Pads;
            /// <summary>The notes the arpeggio walks for each chord.</summary>
            public int[][] Arps;
            /// <summary>Eighth-note patterns (index into the arp notes, +4 = an octave up, -1 = rest).</summary>
            public int[] PatternA, PatternB;
            /// <summary>Pluck decay rate (higher is shorter).</summary>
            public float Decay = 6.5f;
            public float ArpGain = 0.16f;
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
            },
            // 1 Farmlands: G – D – Em – C, a touch quicker and bouncier.
            new Style
            {
                Bpm = 98f,
                Pads = new[] { new[] { 43, 55, 59 }, new[] { 38, 54, 57 }, new[] { 40, 55, 59 }, new[] { 36, 55, 64 } },
                Arps = new[] { new[] { 67, 71, 74, 79 }, new[] { 62, 66, 69, 74 }, new[] { 64, 67, 71, 76 }, new[] { 64, 67, 72, 76 } },
                PatternA = new[] { 0, 2, 1, 3, 0, 2, 4, 2 },
                PatternB = new[] { 0, -1, 2, 3, -1, 2, 4, -1 },
                Decay = 7.5f,
            },
            // 2 Canyon Highway: Am – G – F – E, slow and dusty, lots of air between notes.
            new Style
            {
                Bpm = 84f,
                Pads = new[] { new[] { 45, 52, 60 }, new[] { 43, 50, 59 }, new[] { 41, 48, 57 }, new[] { 40, 52, 56 } },
                Arps = new[] { new[] { 69, 72, 76, 81 }, new[] { 67, 71, 74, 79 }, new[] { 65, 69, 72, 77 }, new[] { 64, 68, 71, 76 } },
                PatternA = new[] { 0, -1, 2, -1, 1, -1, 3, 2 },
                PatternB = new[] { 0, -1, -1, 2, -1, 1, -1, -1 },
                Decay = 5f,
                ArpGain = 0.18f,
            },
            // 3 Pine Ridge: F – C – Dm – Bb, an unhurried folk walk.
            new Style
            {
                Bpm = 80f,
                Pads = new[] { new[] { 41, 57, 60 }, new[] { 36, 55, 64 }, new[] { 38, 57, 65 }, new[] { 34, 53, 62 } },
                Arps = new[] { new[] { 65, 69, 72, 77 }, new[] { 64, 67, 72, 76 }, new[] { 62, 65, 69, 74 }, new[] { 62, 65, 70, 74 } },
                PatternA = new[] { 0, 2, 1, 3, 2, 4, 3, 1 },
                PatternB = new[] { 0, -1, 2, -1, 3, -1, -1, -1 },
                Decay = 5.5f,
            },
            // 4 Metro City: Fmaj7 – Em7 – Dm7 – Cmaj7, lo-fi and syncopated.
            new Style
            {
                Bpm = 100f,
                Pads = new[] { new[] { 41, 57, 64 }, new[] { 40, 55, 62 }, new[] { 38, 53, 60 }, new[] { 36, 55, 59 } },
                Arps = new[] { new[] { 69, 72, 76, 77 }, new[] { 67, 71, 74, 76 }, new[] { 65, 69, 72, 74 }, new[] { 64, 67, 71, 72 } },
                PatternA = new[] { 0, -1, 1, 2, -1, 3, -1, 2 },
                PatternB = new[] { -1, 2, -1, 1, 3, -1, 4, -1 },
                Decay = 9f,
                ArpGain = 0.17f,
            },
            // 5 Frost Port: Em – C – G – D, slow with a high, bell-like arp.
            new Style
            {
                Bpm = 76f,
                Pads = new[] { new[] { 40, 55, 59 }, new[] { 36, 55, 64 }, new[] { 43, 55, 62 }, new[] { 38, 54, 62 } },
                Arps = new[] { new[] { 76, 79, 83, 88 }, new[] { 76, 79, 84, 88 }, new[] { 74, 79, 83, 86 }, new[] { 74, 78, 81, 86 } },
                PatternA = new[] { 0, 2, 4, 2, 1, 3, 5, 3 },
                PatternB = new[] { 0, -1, 4, -1, 2, -1, -1, -1 },
                Decay = 3.8f,
                ArpGain = 0.12f,
            },
        };

        /// <summary>Dark (storm) variants play this much slower.</summary>
        const float DarkTempo = 0.94f;

        static Style StyleFor(int style) => Styles[((style % StyleCount) + StyleCount) % StyleCount];

        static float Bpm(int style, bool dark) => StyleFor(style).Bpm * (dark ? DarkTempo : 1f);

        /// <summary>Length of a loop in seconds.</summary>
        public static float LoopSeconds(int style, bool dark) => Bars * BeatsPerBar * 60f / Bpm(style, dark);

        /// <summary>
        /// Renders a loop over several frames (at most <paramref name="budget"/> sample writes per
        /// frame), then hands the clip to <paramref name="done"/>.
        /// </summary>
        public static IEnumerator Render(int style, bool dark, int budget, Action<AudioClip> done)
        {
            float beat = 60f / Bpm(style, dark);
            int total = Mathf.RoundToInt(LoopSeconds(style, dark) * Rate);
            var notes = Compose(StyleFor(style), dark, beat);
            var data = new float[total];

            int written = 0;
            foreach (var note in notes)
            {
                for (int i = 0; i < note.Length; i++)
                {
                    data[(note.Start + i) % total] += Sample(note, i);
                    if (++written >= budget)
                    {
                        written = 0;
                        yield return null;
                    }
                }
            }

            float peak = 0.0001f;
            for (int i = 0; i < total; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = (dark ? 0.8f : 0.9f) / peak;
            for (int i = 0; i < total; i++) data[i] *= gain;

            var clip = AudioClip.Create(dark ? $"music-{style}-storm" : $"music-{style}", total, 1, Rate, false);
            clip.SetData(data, 0);
            done(clip);
        }

        static List<Note> Compose(Style style, bool dark, float beat)
        {
            var notes = new List<Note>(96);
            float chordSeconds = 2 * BeatsPerBar * beat;
            float padRelease = 1.2f;
            for (int c = 0; c < style.Pads.Length; c++)
            {
                float start = c * chordSeconds;
                var pad = style.Pads[c];
                for (int n = 0; n < pad.Length; n++)
                {
                    // Dark: the root drops an octave for weight (not below A1, where it would only be mud).
                    bool drop = dark && n == 0 && pad[n] - 12 >= 33;
                    int midi = drop ? pad[n] - 12 : pad[n];
                    notes.Add(new Note
                    {
                        Start = Samples(start), Length = Samples(chordSeconds + padRelease),
                        Freq = Hz(midi), Gain = drop ? 0.14f : 0.11f, Voice = Voice.Pad,
                    });
                }

                for (int bar = 0; bar < 2; bar++)
                {
                    // The second bar of each chord leaves gaps so the line breathes; dark keeps it sparse throughout.
                    var pattern = bar == 0 && !dark ? style.PatternA : style.PatternB;
                    for (int e = 0; e < 8; e++)
                    {
                        int idx = pattern[e];
                        if (idx < 0) continue;
                        var set = style.Arps[c];
                        int midi = set[idx % set.Length] + 12 * (idx / set.Length) - (dark ? 12 : 0);
                        float t = start + (bar * BeatsPerBar + e * 0.5f) * beat;
                        // Downbeats a touch louder, for a lilt.
                        float accent = e % 2 == 0 ? 1f : 0.75f;
                        notes.Add(new Note
                        {
                            Start = Samples(t), Length = Samples(0.9f),
                            Freq = Hz(midi), Gain = style.ArpGain * accent * (dark ? 0.8f : 1f), Voice = Voice.Pluck,
                            Decay = style.Decay * (dark ? 0.8f : 1f),
                        });
                    }
                }
            }
            return notes;
        }

        static float Sample(in Note n, int i)
        {
            float t = i / (float)Rate;
            float d = n.Length / (float)Rate;
            float twoPi = Mathf.PI * 2f;
            if (n.Voice == Voice.Pad)
            {
                // Two slightly detuned sines beat slowly; a quiet octave adds air.
                float s = Mathf.Sin(twoPi * n.Freq * 0.997f * t) + Mathf.Sin(twoPi * n.Freq * 1.003f * t)
                          + 0.18f * Mathf.Sin(twoPi * n.Freq * 2f * t);
                float attack = Mathf.Clamp01(t / 0.7f);
                float release = Mathf.Clamp01((d - t) / 1.2f);
                return s * 0.5f * attack * release * n.Gain;
            }
            float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * n.Decay) * Mathf.Clamp01((d - t) * 20f);
            float tone = Mathf.Sin(twoPi * n.Freq * t) + 0.3f * Mathf.Sin(twoPi * n.Freq * 2f * t) * Mathf.Exp(-t * 14f);
            return tone * env * n.Gain;
        }

        static int Samples(float seconds) => Mathf.RoundToInt(seconds * Rate);

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
