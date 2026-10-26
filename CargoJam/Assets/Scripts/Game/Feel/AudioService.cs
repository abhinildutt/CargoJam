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
    }

    /// <summary>Plays one-shot effects from a small pool of sources so overlapping sounds don't cut each other off.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int Voices = 10;

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

        public void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (!Settings.SoundOn) return;
            var source = sources[next];
            next = (next + 1) % Voices;
            source.pitch = pitch;
            source.PlayOneShot(clips[sfx], volume);
        }

        /// <summary>Pitch for the nth load in a combo streak (0-based).</summary>
        public static float ComboPitch(int streak)
        {
            int step = ComboSteps[Mathf.Clamp(streak, 0, ComboSteps.Length - 1)];
            return Mathf.Pow(2f, step / 12f);
        }
    }
}
