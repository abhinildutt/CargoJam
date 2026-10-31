using System.Collections;
using System.Collections.Generic;
using CargoJam.Game.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Plays the generated <see cref="MusicSynth"/> loop for the current <see cref="MusicCue"/> quietly,
    /// fading with the Music setting (polled twice a second, or immediately via <see cref="Refresh"/>),
    /// ducking under stingers, and crossfading when the cue changes. A third source carries the cue's
    /// tension layer: it is started with <see cref="AudioSource.PlayScheduled"/> on the same DSP tick
    /// as the main loop (whose length is an exact multiple of it), so the two stay beat-locked, and
    /// <see cref="SetTension"/> only fades its volume.
    /// Loops render lazily, and never while music is off: the requested cue first, then the prefetch
    /// at a low per-frame budget. At most <see cref="CacheLimit"/> cues (main + tension clip, evicted
    /// together) and <see cref="CacheBudgetBytes"/> of clip data are kept; the playing, wanted and
    /// prefetched cues are never evicted.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>Full music volume; kept low so sound effects stay on top.</summary>
        public const float Volume = 0.22f;
        /// <summary>The tension layer's level relative to the main loop.</summary>
        const float TensionLevel = 0.8f;
        const float TensionInSeconds = 0.5f;
        const float TensionOutSeconds = 1.5f;
        const float DuckLevel = 0.35f;
        const float PollInterval = 0.5f;
        const float CrossfadeSeconds = 1.6f;
        const int CacheLimit = 4;
        /// <summary>Clip memory the cache may hold beyond the protected cues (float PCM, 4 bytes a sample).</summary>
        const long CacheBudgetBytes = 9L * 1024 * 1024;
        /// <summary>DSP lead for scheduled starts, so the main loop and its tension layer start on the same sample.</summary>
        const double ScheduleLead = 0.06;
        /// <summary>Sample writes per frame for the wanted loop while nothing is audible (a few ms on a mid-range phone).</summary>
        const int ForegroundBudget = 60000;
        /// <summary>Sample writes per frame otherwise (prefetches, or a missed prefetch while music plays).</summary>
        const int BackgroundBudget = 20000;
        /// <summary>If nobody asks for a track this long after start, play the first destination's.</summary>
        const float DefaultAfter = 1.5f;

        struct Cached
        {
            public AudioClip Main;
            public AudioClip Tension;
            public long Bytes;
        }

        readonly AudioSource[] sources = new AudioSource[2];
        readonly int[] sourceKey = { -1, -1 };
        readonly float[] gains = new float[2];
        readonly Dictionary<int, Cached> cache = new Dictionary<int, Cached>();
        readonly List<int> recent = new List<int>(CacheLimit + 3);
        AudioSource tensionSource;

        int active = -1;
        int desiredKey = -1;
        int prefetchKey = -1;
        bool rendering;
        bool wanted;
        float nextPoll;
        float fade;
        float duck = 1f;
        float duckFrom = -1f, duckUntil = -1f;
        bool tense;
        float tensionGain;

        void Awake()
        {
            for (int i = 0; i < sources.Length; i++) sources[i] = AddSource();
            tensionSource = AddSource();
            wanted = Settings.MusicOn;
        }

        AudioSource AddSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            // Behind the effects when the voice budget is tight.
            source.priority = 200;
            return source;
        }

        void OnDestroy()
        {
            foreach (var entry in cache.Values) Release(entry);
            cache.Clear();
        }

        /// <summary>
        /// Plays <paramref name="cue"/> (crossfading from the current loop) and renders
        /// <paramref name="prefetch"/> in the background so the next swap is instant.
        /// </summary>
        public void Play(MusicCue cue, MusicCue prefetch)
        {
            int key = cue.Key;
            desiredKey = key;
            prefetchKey = prefetch.Key;
            bool playing = active >= 0 && sourceKey[active] == key;
            if (!playing && cache.TryGetValue(key, out var entry))
            {
                Touch(key);
                Crossfade(key, entry);
            }
            Pump();
        }

        /// <summary>
        /// Fades the beat-locked tension layer in (0.5 s) or out (1.5 s). It follows the music fade,
        /// duck and crossfade like the main loop.
        /// </summary>
        public void SetTension(bool on) => tense = on;

        /// <summary>Re-reads <see cref="Settings.MusicOn"/> now (call after the toggle changes).</summary>
        public void Refresh()
        {
            nextPoll = Time.unscaledTime + PollInterval;
            wanted = Settings.MusicOn;
            if (!wanted) return;
            if (active >= 0 && sources[active].clip != null && !sources[active].isPlaying)
            {
                // Resuming: restart the loop and its layer together from the top so they stay locked.
                // A half-faded previous loop is simply dropped.
                StopSource(1 - active);
                StartActive();
            }
            Pump();
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
            if (desiredKey < 0 && Time.unscaledTime > DefaultAfter)
            {
                var first = new MusicCue(0, MusicVariant.Calm, 0);
                Play(first, first);
            }
            if (Time.unscaledTime >= nextPoll) Refresh();
            if (active < 0) return;

            float dt = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, dt * (wanted ? 0.6f : 2.5f));
            float now = Time.time;
            bool ducking = now >= duckFrom && now < duckUntil;
            duck = Mathf.MoveTowards(duck, ducking ? DuckLevel : 1f, dt * (ducking ? 4f : 0.9f));
            tensionGain = Mathf.MoveTowards(tensionGain, tense ? 1f : 0f, dt / (tense ? TensionInSeconds : TensionOutSeconds));

            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.clip == null) continue;
                float step = dt / CrossfadeSeconds;
                gains[i] = Mathf.MoveTowards(gains[i], i == active ? 1f : 0f, step);
                if (i != active && gains[i] <= 0f)
                {
                    // Faded out: stop it and let its clips be evicted.
                    StopSource(i);
                    Evict();
                    continue;
                }
                // Equal-power-ish crossfade.
                float g = Mathf.Sin(gains[i] * Mathf.PI * 0.5f);
                source.volume = Volume * fade * duck * g;
                // Paused rather than left playing silently, so a disabled toggle costs nothing.
                if (fade <= 0f && source.isPlaying) source.Pause();
            }

            // The layer rides on the active loop's crossfade; it keeps running silently when relaxed so
            // it never has to be re-synced.
            if (tensionSource.clip != null)
            {
                float g = Mathf.Sin(gains[active] * Mathf.PI * 0.5f);
                tensionSource.volume = Volume * TensionLevel * fade * duck * g * tensionGain;
                if (fade <= 0f && tensionSource.isPlaying) tensionSource.Pause();
            }
        }

        void Crossfade(int key, Cached entry)
        {
            int next = active < 0 ? 0 : 1 - active;
            var source = sources[next];
            source.Stop();
            source.clip = entry.Main;
            sourceKey[next] = key;
            // The very first track starts at full gain (the setting fade-in covers it).
            bool first = active < 0 || sources[active].clip == null;
            gains[next] = first ? 1f : 0f;
            source.volume = 0f;
            active = next;

            // The layer belongs to the incoming loop; any tension restarts its fade with it.
            tensionSource.Stop();
            tensionSource.clip = entry.Tension;
            tensionSource.volume = 0f;
            tensionGain = 0f;
            if (wanted) StartActive();
        }

        /// <summary>Starts the active loop and its tension layer on the same future DSP sample.</summary>
        void StartActive()
        {
            double at = AudioSettings.dspTime + ScheduleLead;
            var main = sources[active];
            main.Stop();
            main.PlayScheduled(at);
            if (tensionSource.clip == null) return;
            tensionSource.Stop();
            tensionSource.PlayScheduled(at);
        }

        void StopSource(int i)
        {
            var source = sources[i];
            source.Stop();
            source.clip = null;
            sourceKey[i] = -1;
            gains[i] = 0f;
        }

        void Pump()
        {
            if (rendering || !wanted) return;
            if (NextToRender() < 0) return;
            StartCoroutine(RenderLoop());
        }

        int NextToRender()
        {
            if (desiredKey >= 0 && !cache.ContainsKey(desiredKey)) return desiredKey;
            if (prefetchKey >= 0 && !cache.ContainsKey(prefetchKey)) return prefetchKey;
            return -1;
        }

        IEnumerator RenderLoop()
        {
            rendering = true;
            int key;
            // Music off: stop after the current render; Refresh restarts the pump when it comes back on.
            while (wanted && (key = NextToRender()) >= 0)
            {
                // Full speed only while silent; with a loop audible, a missed prefetch renders at the
                // gentle budget so it never costs frames during an arrival pan.
                bool silent = active < 0 || sources[active].clip == null || fade <= 0f;
                int budget = key == desiredKey && silent ? ForegroundBudget : BackgroundBudget;
                int k = key;
                AudioClip main = null, tension = null;
                yield return MusicSynth.Render(MusicCue.FromKey(k), budget, (m, t) =>
                {
                    main = m;
                    tension = t;
                });
                var entry = new Cached { Main = main, Tension = tension, Bytes = 4L * (main.samples + tension.samples) };
                cache[k] = entry;
                Touch(k);
                Evict();
                if (k == desiredKey && (active < 0 || sourceKey[active] != k)) Crossfade(k, entry);
            }
            rendering = false;
        }

        void Touch(int key)
        {
            recent.Remove(key);
            recent.Add(key);
        }

        /// <summary>Drops least-recently-used cues past the count or byte limit, never one in use or wanted.</summary>
        void Evict()
        {
            long bytes = 0;
            foreach (var entry in cache.Values) bytes += entry.Bytes;
            for (int i = 0; i < recent.Count && (cache.Count > CacheLimit || bytes > CacheBudgetBytes); i++)
            {
                int key = recent[i];
                if (key == desiredKey || key == prefetchKey || key == sourceKey[0] || key == sourceKey[1]) continue;
                if (cache.TryGetValue(key, out var entry))
                {
                    bytes -= entry.Bytes;
                    Release(entry);
                }
                cache.Remove(key);
                recent.RemoveAt(i);
                i--;
            }
        }

        static void Release(Cached entry)
        {
            if (entry.Main != null) Destroy(entry.Main);
            if (entry.Tension != null) Destroy(entry.Tension);
        }
    }
}
