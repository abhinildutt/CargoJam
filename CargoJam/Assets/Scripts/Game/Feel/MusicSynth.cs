using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Generates the background loop: 8 bars at 90 BPM (about 21 s), a soft detuned pad on
    /// C – Am – F – G with a plucky C-major-pentatonic arpeggio on top. Mono, 22.05 kHz (~0.9 MB).
    /// Notes that ring past the end wrap around to the start, so the loop is seamless. Rendering is
    /// spread over frames so it never hitches startup.
    /// </summary>
    public static class MusicSynth
    {
        const int Rate = 22050;
        const float Bpm = 90f;
        const int Bars = 8;
        const int BeatsPerBar = 4;
        /// <summary>Sample writes per frame while rendering (a few ms on a mid-range phone).</summary>
        const int Budget = 60000;

        enum Voice { Pad, Pluck }

        struct Note
        {
            public int Start;
            public int Length;
            public float Freq;
            public float Gain;
            public Voice Voice;
        }

        // Chords, two bars each: pad notes (MIDI) and the pentatonic notes the arpeggio walks.
        static readonly int[][] PadChords =
        {
            new[] { 48, 55, 64 }, // C
            new[] { 45, 52, 60 }, // Am
            new[] { 41, 57, 60 }, // F
            new[] { 43, 55, 62 }, // G
        };
        static readonly int[][] ArpNotes =
        {
            new[] { 72, 76, 79, 81 }, // C E G A
            new[] { 69, 72, 76, 79 }, // A C E G
            new[] { 69, 72, 74, 76 }, // A C D E
            new[] { 67, 69, 74, 79 }, // G A D G
        };
        // Eighth-note patterns (index into the chord's arp notes, +4 = an octave up, -1 = rest).
        static readonly int[] PatternA = { 0, 1, 2, 3, 4, 3, 2, 1 };
        static readonly int[] PatternB = { 2, -1, 3, 4, -1, 5, 3, -1 };

        /// <summary>Length of the loop in seconds.</summary>
        public static float LoopSeconds => Bars * BeatsPerBar * 60f / Bpm;

        /// <summary>Renders the loop over several frames, then hands the clip to <paramref name="done"/>.</summary>
        public static IEnumerator Render(Action<AudioClip> done)
        {
            float beat = 60f / Bpm;
            int total = Mathf.RoundToInt(LoopSeconds * Rate);
            var notes = Compose(beat);
            var data = new float[total];

            int written = 0;
            foreach (var note in notes)
            {
                for (int i = 0; i < note.Length; i++)
                {
                    data[(note.Start + i) % total] += Sample(note, i);
                    if (++written >= Budget)
                    {
                        written = 0;
                        yield return null;
                    }
                }
            }

            float peak = 0.0001f;
            for (int i = 0; i < total; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.9f / peak;
            for (int i = 0; i < total; i++) data[i] *= gain;

            var clip = AudioClip.Create("music", total, 1, Rate, false);
            clip.SetData(data, 0);
            done(clip);
        }

        static List<Note> Compose(float beat)
        {
            var notes = new List<Note>(96);
            float chordSeconds = 2 * BeatsPerBar * beat;
            float padRelease = 1.2f;
            for (int c = 0; c < PadChords.Length; c++)
            {
                float start = c * chordSeconds;
                foreach (int midi in PadChords[c])
                    notes.Add(new Note
                    {
                        Start = Samples(start), Length = Samples(chordSeconds + padRelease),
                        Freq = Hz(midi), Gain = 0.11f, Voice = Voice.Pad,
                    });

                for (int bar = 0; bar < 2; bar++)
                {
                    // The second bar of each chord leaves gaps so the line breathes.
                    var pattern = bar == 0 ? PatternA : PatternB;
                    for (int e = 0; e < 8; e++)
                    {
                        int idx = pattern[e];
                        if (idx < 0) continue;
                        var set = ArpNotes[c];
                        int midi = set[idx % set.Length] + 12 * (idx / set.Length);
                        float t = start + (bar * BeatsPerBar + e * 0.5f) * beat;
                        // Downbeats a touch louder, for a lilt.
                        float accent = e % 2 == 0 ? 1f : 0.75f;
                        notes.Add(new Note
                        {
                            Start = Samples(t), Length = Samples(0.9f),
                            Freq = Hz(midi), Gain = 0.16f * accent, Voice = Voice.Pluck,
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
            float env = Mathf.Min(1f, t * 300f) * Mathf.Exp(-t * 6.5f) * Mathf.Clamp01((d - t) * 20f);
            float tone = Mathf.Sin(twoPi * n.Freq * t) + 0.3f * Mathf.Sin(twoPi * n.Freq * 2f * t) * Mathf.Exp(-t * 14f);
            return tone * env * n.Gain;
        }

        static int Samples(float seconds) => Mathf.RoundToInt(seconds * Rate);

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}
