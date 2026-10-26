using CargoJam.Game.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Plays the generated <see cref="MusicSynth"/> loop quietly, fading with the Music setting
    /// (polled twice a second, or immediately via <see cref="Refresh"/>) and ducking under stingers.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>Full music volume; kept low so sound effects stay on top.</summary>
        public const float Volume = 0.22f;
        const float DuckLevel = 0.35f;
        const float PollInterval = 0.5f;

        AudioSource source;
        bool wanted;
        bool paused;
        float nextPoll;
        float fade;
        float duck = 1f;
        float duckFrom = -1f, duckUntil = -1f;

        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            // Behind the effects when the voice budget is tight.
            source.priority = 200;
            wanted = Settings.MusicOn;
            StartCoroutine(MusicSynth.Render(clip =>
            {
                source.clip = clip;
                if (wanted) source.Play();
            }));
        }

        /// <summary>Re-reads <see cref="Settings.MusicOn"/> now (call after the toggle changes).</summary>
        public void Refresh()
        {
            nextPoll = Time.unscaledTime + PollInterval;
            wanted = Settings.MusicOn;
            if (!wanted || source.clip == null || source.isPlaying) return;
            if (paused) source.UnPause();
            else source.Play();
            paused = false;
        }

        /// <summary>Dips the music from <paramref name="at"/> (Time.time) for <paramref name="seconds"/>, for a stinger.</summary>
        public void Duck(float at, float seconds)
        {
            duckFrom = at;
            duckUntil = at + seconds;
        }

        /// <summary>Stops any duck at once (level teardown).</summary>
        public void ClearDuck() => duckFrom = duckUntil = -1f;

        void Update()
        {
            if (Time.unscaledTime >= nextPoll) Refresh();
            if (source.clip == null) return;

            float dt = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, dt * (wanted ? 0.6f : 2.5f));
            float now = Time.time;
            bool ducking = now >= duckFrom && now < duckUntil;
            duck = Mathf.MoveTowards(duck, ducking ? DuckLevel : 1f, dt * (ducking ? 4f : 0.9f));

            source.volume = Volume * fade * duck;
            // Paused rather than left playing silently, so a disabled toggle costs nothing.
            if (fade <= 0f && source.isPlaying)
            {
                source.Pause();
                paused = true;
            }
        }
    }
}
