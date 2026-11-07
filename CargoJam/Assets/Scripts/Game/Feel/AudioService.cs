using System.Collections.Generic;
using CargoJam.Game.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    public enum Sfx
    {
        Pick,
        Load,
        Hold,
        Nope,
        Horn,
        Arrive,
        Danger,
        Win,
        Fail,
        Click,
        /// <summary>Soft tick (intro drops).</summary>
        Tick,
        /// <summary>High twinkle (truck full, reveal).</summary>
        Sparkle,
        /// <summary>Undo booster.</summary>
        Whoosh,
        /// <summary>Extra Slot booster, pads popping in.</summary>
        Pop,
        /// <summary>Two bell notes (Extra Slot).</summary>
        Chime,
        /// <summary>Crane booster motor.</summary>
        Whir,
        /// <summary>Mystery tarp coming off, truck dust.</summary>
        Puff,
        /// <summary>Padlock click-clack.</summary>
        Unlock,
        /// <summary>Combo callout sting (pitched up per tier).</summary>
        Callout,
        /// <summary>Postcard stamp landing on the arrival card: a soft papery thump.</summary>
        Stamp,
        /// <summary>Distant rumble after a storm's lightning flash.</summary>
        Thunder,
    }

    /// <summary>Plays one-shot effects from a small pool of sources so overlapping sounds don't cut each other off.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int Voices = 14;

        // Major scale steps: consecutive loads climb do-re-mi and top out an octave up.
        static readonly int[] ComboSteps = { 0, 2, 4, 5, 7, 9, 11, 12 };

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        readonly AudioSource[] sources = new AudioSource[Voices];
        int next;

        void Awake()
        {
            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx))) clips[sfx] = SfxSynth.Make(sfx);
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }

        /// <summary>Plays a one-shot at a pitch and volume (silent when sound is off).</summary>
        public void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (!Settings.SoundOn) return;
            var source = sources[next];
            next = (next + 1) % Voices;
            source.pitch = pitch;
            source.PlayOneShot(clips[sfx], volume);
        }

        /// <summary>
        /// Plays with a random pitch within ±<paramref name="variance"/>, so frequent sounds don't
        /// grate. Never use it for the combo load, whose pitch steps are musical.
        /// </summary>
        public void PlayVaried(Sfx sfx, float pitch = 1f, float volume = 1f, float variance = 0.04f) =>
            Play(sfx, pitch * Random.Range(1f - variance, 1f + variance), volume);

        /// <summary>Pitch for the nth load in a combo streak (0-based).</summary>
        public static float ComboPitch(int streak)
        {
            int step = ComboSteps[Mathf.Clamp(streak, 0, ComboSteps.Length - 1)];
            return Mathf.Pow(2f, step / 12f);
        }
    }
}
