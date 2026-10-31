using System;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Small stateless DSP helpers shared by <see cref="SfxSynth"/> and <see cref="MusicSynth"/>.
    /// Oscillator phases are in cycles (1 = one period).
    /// </summary>
    public static class Dsp
    {
        const int TableSize = 2048;

        /// <summary>One sine period plus a guard entry, for <see cref="FastSine"/>.</summary>
        static readonly float[] SineTable = BuildSineTable();

        static float[] BuildSineTable()
        {
            var table = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++) table[i] = (float)Math.Sin(2.0 * Math.PI * i / TableSize);
            return table;
        }

        /// <summary>Exact sine of a phase in cycles.</summary>
        public static float Sine(float cycles) => (float)Math.Sin(cycles * 2.0 * Math.PI);

        /// <summary>
        /// Table sine (linear interpolation, error below -110 dB) for a phase already wrapped to
        /// [0, 1). Several times cheaper than <see cref="Sine"/>; used by the music renderer.
        /// </summary>
        public static float FastSine(float phase)
        {
            float x = phase * TableSize;
            int i = (int)x;
            float a = SineTable[i];
            return a + (SineTable[i + 1] - a) * (x - i);
        }

        /// <summary>Advances a phase by <paramref name="step"/> cycles (below 1), keeping it in [0, 1).</summary>
        public static float Advance(float phase, float step)
        {
            phase += step;
            return phase >= 1f ? phase - 1f : phase;
        }

        /// <summary>Phase (in cycles) of a sine sweeping from f0 to f1 over d seconds.</summary>
        public static float Sweep(float t, float d, float f0, float f1) => f0 * t + (f1 - f0) * t * t / (2f * d);

        public static float Saw(float cycles) => 2f * (cycles - (float)Math.Floor(cycles + 0.5f));
        public static float Triangle(float cycles) => 2f * Math.Abs(Saw(cycles)) - 1f;
        public static float Soft(float x) => (float)Math.Tanh(x);
        public static float Decay(float t, float rate) => (float)Math.Exp(-t * rate);

        /// <summary>Linear attack and release around a flat middle.</summary>
        public static float Envelope(float t, float d, float attack, float release)
        {
            if (t < attack) return t / attack;
            if (t > d - release) return Math.Max(0f, (d - t) / release);
            return 1f;
        }

        /// <summary>
        /// Deterministic hash noise in [-1, 1] for an integer index (a sample number, plus any salt),
        /// so synthesized sounds are identical every launch and need no generator state.
        /// </summary>
        public static float Noise(uint index)
        {
            uint x = index * 747796405u + 2891336453u;
            x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
            x = (x >> 22) ^ x;
            return x / (float)uint.MaxValue * 2f - 1f;
        }
    }
}
