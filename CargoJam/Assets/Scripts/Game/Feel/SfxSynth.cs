using System;
using UnityEngine;
using static CargoJam.Game.Feel.Dsp;

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
                case Sfx.Tick: return Clip("tick", 0.045f, (t, d) =>
                    (Sine(t * 1500f) * Decay(t, 85f) * 0.6f + Noise(t) * Decay(t, 400f) * 0.25f) * 0.35f);
                case Sfx.Sparkle: return Pings("sparkle", new[] { 96, 100, 103, 108 }, 0.045f, 0.22f);
                case Sfx.Whoosh: return Whoosh();
                case Sfx.Pop: return Clip("pop", 0.1f, (t, d) => Sine(Sweep(t, d, 320f, 980f)) * Envelope(t, d, 0.003f, 0.06f) * 0.5f);
                case Sfx.Chime: return Pings("chime", new[] { 88, 95 }, 0.08f, 0.32f);
                case Sfx.Whir: return Whir();
                case Sfx.Puff: return Puff();
                case Sfx.Unlock: return Clip("unlock", 0.22f, Unlock);
                case Sfx.Callout: return Notes("callout", new[] { 79, 84, 88 }, 0.06f, 0.3f, 0.3f);
                case Sfx.Stamp: return Clip("stamp", 0.2f, Stamp);
                case Sfx.Thunder: return Thunder();
                case Sfx.IceCrack: return Clip("ice-crack", 0.16f, IceCrack);
                case Sfx.IceShatter: return Clip("ice-shatter", 0.5f, IceShatter);
                case Sfx.ExpressTick: return Clip("express-tick", 0.09f, ExpressTick);
                case Sfx.StrapSnap: return Clip("strap-snap", 0.22f, StrapSnap);
                case Sfx.GateTick: return Clip("gate-tick", 0.09f, GateTick);
                case Sfx.GateOpen: return Clip("gate-open", 0.55f, GateOpen);
                default: throw new ArgumentOutOfRangeException(nameof(sfx));
            }
        }

        // A rubber stamp pressed onto card: a dull low thump with a short papery slap on top.
        static float Stamp(float t, float d)
        {
            float body = Sine(Sweep(t, d, 150f, 80f)) * Decay(t, 26f);
            float slap = Noise(t) * Decay(t, 90f) * 0.45f;
            return (body + slap) * Mathf.Min(1f, t * 600f) * 0.55f;
        }

        // Ice giving way: a crackle of three glassy ticks over a short brittle ping.
        static float IceCrack(float t, float d)
        {
            float crackle = 0f;
            for (int i = 0; i < 3; i++)
            {
                float local = t - i * 0.022f;
                if (local < 0f) break;
                crackle += Noise(t) * Sine(local * 3400f) * Decay(local, 220f) * (1f - 0.25f * i);
            }
            float ping = (Sine(t * 2650f) + 0.5f * Sine(t * 4130f)) * Decay(t, 32f) * 0.22f;
            return (crackle * 0.55f + ping) * Mathf.Min(1f, t * 2000f) * 0.7f;
        }

        // The last of the ice bursting: a bright noisy break, a low knock and scattered tinkles falling away.
        static float IceShatter(float t, float d)
        {
            // Differenced noise: a high-passed, glassy hiss.
            uint n = (uint)(t * Rate);
            float burst = (Dsp.Noise(n) - Dsp.Noise(n - 1u)) * 0.5f * Decay(t, 11f);
            float knock = Sine(Sweep(t, d, 260f, 140f)) * Decay(t, 30f) * 0.35f;
            float tinkle = 0f;
            for (int i = 0; i < 5; i++)
            {
                float local = t - 0.035f - i * 0.055f;
                if (local < 0f) break;
                float f = 2200f + 430f * ((i * 3) % 5);
                tinkle += (Sine(local * f) + 0.3f * Sine(local * f * 2.76f)) * Decay(local, 26f) * (0.32f - 0.04f * i);
            }
            return (burst * 0.9f + knock + tinkle) * Mathf.Min(1f, t * 1500f) * 0.6f;
        }

        // A crisp clock tick with a short tonal blip, so pitching it up reads as urgency.
        static float ExpressTick(float t, float d)
        {
            float click = Noise(t) * Decay(t, 600f) * 0.35f;
            float blip = (Sine(t * 1320f) + 0.35f * Soft(Sine(t * 1320f) * 4f)) * Decay(t, 48f) * 0.4f;
            return (click + blip) * Mathf.Min(1f, t * 3000f) * 0.75f;
        }

        // A taut woven band letting go: a fast downward twang with a papery slap.
        static float StrapSnap(float t, float d)
        {
            float twang = Soft(Sine(Sweep(t, 0.12f, 900f, 120f)) * 2.2f) * Decay(t, 20f) * 0.35f;
            float slap = Noise(t) * Decay(t, 70f) * 0.5f;
            float thud = Sine(t * 110f) * Decay(t, 28f) * 0.25f;
            return (twang + slap + thud) * Mathf.Min(1f, t * 2500f) * 0.6f;
        }

        // A mechanical counter flipping: two tight clacks and a small metallic tick.
        static float GateTick(float t, float d)
        {
            float second = t - 0.028f;
            float clack = Noise(t) * Decay(t, 380f) * 0.5f + Sine(t * 1900f) * Decay(t, 140f) * 0.3f;
            if (second > 0f) clack += Noise(second) * Decay(second, 420f) * 0.35f + Sine(second * 2500f) * Decay(second, 120f) * 0.25f;
            return clack * 0.6f;
        }

        // The boom swinging up: a short rising ratchet, then a bright two-note ding.
        static float GateOpen(float t, float d)
        {
            float ratchet = 0f;
            if (t < 0.2f)
            {
                float tooth = (t * 60f) % 1f;
                ratchet = Noise(t) * Decay(tooth / 60f, 500f) * 0.35f * Envelope(t, 0.2f, 0.02f, 0.05f);
            }
            float ding = 0f;
            for (int i = 0; i < 2; i++)
            {
                float local = t - 0.17f - i * 0.09f;
                if (local < 0f) break;
                float f = i == 0 ? 1318.5f : 1975.5f;
                ding += (Sine(local * f) + 0.25f * Sine(local * f * 2.76f)) * Decay(local, 9f) * Mathf.Min(1f, local * 800f) * 0.28f;
            }
            return ratchet + ding;
        }

        // Far-off thunder: heavily low-passed noise that swells and rolls away, with a slow wobble.
        static AudioClip Thunder()
        {
            float lp = 0f, lp2 = 0f;
            return Clip("thunder", 1.8f, (t, d) =>
            {
                lp += 0.02f * (Noise(t) - lp);
                lp2 += 0.05f * (lp - lp2);
                float swell = Mathf.Min(1f, t / 0.12f) * Decay(t, 1.9f);
                float roll = 0.7f + 0.3f * Sine(t * 3.1f);
                return lp2 * swell * roll * 7.5f;
            });
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

        /// <summary>Bell-like sine pings (with an inharmonic partial), each left ringing.</summary>
        static AudioClip Pings(string name, int[] midi, float step, float gain)
        {
            float length = step * (midi.Length - 1) + 0.35f;
            return Clip(name, length, (t, d) =>
            {
                float sum = 0f;
                for (int i = 0; i < midi.Length; i++)
                {
                    float local = t - i * step;
                    if (local < 0f) break;
                    float f = 440f * Mathf.Pow(2f, (midi[i] - 69) / 12f);
                    sum += (Sine(local * f) + 0.25f * Sine(local * f * 2.76f)) * Decay(local, 14f) * Mathf.Min(1f, local * 800f);
                }
                return sum * gain;
            });
        }

        // Noise through a one-pole low-pass whose cutoff swells and falls: air rushing past.
        static AudioClip Whoosh()
        {
            float lp = 0f;
            return Clip("whoosh", 0.32f, (t, d) =>
            {
                float k = t / d;
                float bell = Mathf.Sin(k * Mathf.PI);
                float a = 0.02f + 0.18f * bell;
                lp += a * (Noise(t) - lp);
                return lp * bell * bell * 1.1f;
            });
        }

        // A small motor spinning up: a soft saw rising in pitch with a fast flutter.
        static AudioClip Whir()
        {
            return Clip("whir", 0.55f, (t, d) =>
            {
                float flutter = 0.75f + 0.25f * Sine(t * 32f);
                float tone = Soft(Saw(Sweep(t, d, 110f, 330f)) * 1.4f) * 0.6f + Sine(Sweep(t, d, 220f, 660f)) * 0.25f;
                return tone * flutter * Envelope(t, d, 0.06f, 0.18f) * 0.16f;
            });
        }

        // Low, dull burst of air: a tarp flicked off.
        static AudioClip Puff()
        {
            float lp = 0f;
            return Clip("puff", 0.22f, (t, d) =>
            {
                lp += 0.08f * (Noise(t) - lp);
                return lp * Decay(t, 16f) * Mathf.Min(1f, t * 300f) * 2.2f;
            });
        }

        // Click-clack of a padlock: two bright clicks and a little metallic ring.
        static float Unlock(float t, float d)
        {
            float second = t - 0.07f;
            float click = Noise(t) * Decay(t, 260f) * 0.5f + Sine(t * 3100f) * Decay(t, 120f) * 0.3f;
            if (second > 0f)
                click += Noise(second) * Decay(second, 240f) * 0.6f + Sine(second * 2350f) * Decay(second, 90f) * 0.35f;
            float ring = second > 0f ? Sine(second * 1760f) * Decay(second, 18f) * 0.12f : 0f;
            return (click + ring) * 0.55f;
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

        // Deterministic hash noise per sample, so the clips sound the same every launch.
        static float Noise(float t) => Dsp.Noise((uint)(t * Rate));
    }
}
