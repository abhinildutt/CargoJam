using System;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Placeholder sound effects synthesized at startup, so the greybox has audio without any asset
    /// files. Replaced by recorded or commissioned sounds in the art and audio pass.
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 44100;

        public static AudioClip Make(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Pick: return Clip("pick", 0.06f, (t, d) => Sine(Sweep(t, d, 950f, 600f)) * Decay(t, 45f) * 0.5f);
                case Sfx.Load: return Clip("load", 0.16f, Load);
                case Sfx.Hold: return Clip("hold", 0.09f, (t, d) =>
                    (Sine(t * 540f) + 0.4f * Sine(t * 1080f)) * Decay(t, 50f) * 0.45f);
                case Sfx.Nope: return Clip("nope", 0.16f, (t, d) =>
                    Soft(Sine(Sweep(t, d, 230f, 150f)) * 2.5f) * Decay(t, 18f) * 0.45f);
                case Sfx.Horn: return Clip("horn", 0.28f, Horn);
                case Sfx.Arrive: return Clip("arrive", 0.3f, (t, d) =>
                    Soft(Saw(t * 70f) * 1.5f) * Envelope(t, d, 0.05f, 0.15f) * 0.16f);
                case Sfx.Danger: return Clip("danger", 0.05f, (t, d) => Sine(t * 1760f) * Decay(t, 90f) * 0.35f);
                case Sfx.Win: return Notes("win", new[] { 72, 76, 79, 84 }, 0.11f, 0.45f, 0.4f);
                case Sfx.Fail: return Notes("fail", new[] { 67, 63, 60 }, 0.16f, 0.45f, 0.38f);
                case Sfx.Click: return Clip("click", 0.03f, (t, d) => Sine(t * 1200f) * Decay(t, 120f) * 0.3f);
                default: throw new ArgumentOutOfRangeException(nameof(sfx));
            }
        }

        // A low thunk with a short noisy attack, like a box dropping onto a truck bed.
        static float Load(float t, float d)
        {
            float body = Sine(Sweep(t, d, 190f, 110f)) * Decay(t, 22f);
            float knock = Noise(t) * Decay(t, 160f) * 0.35f;
            float ring = Sine(t * 660f) * Decay(t, 40f) * 0.18f;
            return (body + knock + ring) * 0.6f;
        }

        // Two detuned square-ish tones: a toy truck horn.
        static float Horn(float t, float d)
        {
            float tone = Soft(Sine(t * 370f) * 3f) + Soft(Sine(t * 466f) * 3f);
            return tone * Envelope(t, d, 0.01f, 0.06f) * 0.16f;
        }

        /// <summary>A triangle-wave arpeggio of MIDI notes, the last one held.</summary>
        static AudioClip Notes(string name, int[] midi, float step, float tail, float gain)
        {
            float length = step * (midi.Length - 1) + tail;
            return Clip(name, length, (t, d) =>
            {
                int i = Mathf.Min((int)(t / step), midi.Length - 1);
                float local = t - i * step;
                float len = i == midi.Length - 1 ? tail : step;
                float f = 440f * Mathf.Pow(2f, (midi[i] - 69) / 12f);
                return Triangle(t * f) * Envelope(local, len, 0.005f, len * 0.6f) * gain;
            });
        }

        static AudioClip Clip(string name, float seconds, Func<float, float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Clamp(wave(t, seconds), -1f, 1f);
            }
            // A few milliseconds of fade-out so clips never end on a click.
            int fade = Mathf.Min(n, Rate / 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;

            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Phase (in cycles) of a sine sweeping from f0 to f1 over d seconds.</summary>
        static float Sweep(float t, float d, float f0, float f1) => f0 * t + (f1 - f0) * t * t / (2f * d);

        static float Sine(float cycles) => Mathf.Sin(cycles * 2f * Mathf.PI);
        static float Saw(float cycles) => 2f * (cycles - Mathf.Floor(cycles + 0.5f));
        static float Triangle(float cycles) => 2f * Mathf.Abs(Saw(cycles)) - 1f;
        static float Soft(float x) => (float)Math.Tanh(x);
        static float Decay(float t, float rate) => Mathf.Exp(-t * rate);

        static float Envelope(float t, float d, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > d - release) return Mathf.Max(0f, (d - t) / release);
            return 1f;
        }

        // Deterministic hash noise, so the clips sound the same every launch.
        static float Noise(float t)
        {
            uint x = (uint)(t * Rate) * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return x / (float)uint.MaxValue * 2f - 1f;
        }
    }
}
