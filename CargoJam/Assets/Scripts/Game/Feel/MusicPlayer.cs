using System.Collections;
using System.Collections.Generic;
using CargoJam.Game.Meta;
using UnityEngine;

namespace CargoJam.Game.Feel
{
    /// <summary>
    /// Plays the generated <see cref="MusicSynth"/> loop for the current destination quietly, fading
    /// with the Music setting (polled twice a second, or immediately via <see cref="Refresh"/>),
    /// ducking under stingers, and crossfading when the destination (or storm) changes.
    /// Loops render lazily: the requested one first, then the likely next one in the background at a
    /// low per-frame budget. At most <see cref="CacheLimit"/> clips are kept.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        /// <summary>Full music volume; kept low so sound effects stay on top.</summary>
        public const float Volume = 0.22f;
        const float DuckLevel = 0.35f;
        const float PollInterval = 0.5f;
        const float CrossfadeSeconds = 1.6f;
        const int CacheLimit = 3;
        /// <summary>Sample writes per frame for the wanted loop while nothing plays yet (a few ms on a mid-range phone).</summary>
        const int ForegroundBudget = 60000;
        /// <summary>Sample writes per frame otherwise (prefetches, or a missed prefetch while music plays).</summary>
        const int BackgroundBudget = 20000;
        /// <summary>If nobody asks for a track this long after start, play the first destination's.</summary>
        const float DefaultAfter = 1.5f;

        readonly AudioSource[] sources = new AudioSource[2];
        readonly int[] sourceKey = { -1, -1 };
        readonly float[] gains = new float[2];
        readonly Dictionary<int, AudioClip> cache = new Dictionary<int, AudioClip>();
        readonly List<int> recent = new List<int>(CacheLimit + 2);

        int active = -1;
        int desiredKey = -1;
        int prefetchKey = -1;
        bool rendering;
        bool wanted;
        float nextPoll;
        float fade;
        float duck = 1f;
        float duckFrom = -1f, duckUntil = -1f;

        void Awake()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
                // Behind the effects when the voice budget is tight.
                source.priority = 200;
                sources[i] = source;
            }
            wanted = Settings.MusicOn;
        }

        void OnDestroy()
        {
            foreach (var clip in cache.Values)
                if (clip != null) Destroy(clip);
            cache.Clear();
        }

        /// <summary>Plays the loop for journey stop <paramref name="stop"/> (dark for a storm), crossfading from the current one.</summary>
        public void PlayStop(int stop, bool dark)
        {
            int key = Key(stop, dark);
            desiredKey = key;
            // The next level is most likely this stop's storm finale, or the next stop after a storm.
            prefetchKey = dark ? Key(stop + 1, false) : Key(stop, true);
            bool playing = active >= 0 && sourceKey[active] == key;
            if (!playing && cache.TryGetValue(key, out var clip))
            {
                Touch(key);
                Crossfade(key, clip);
            }
            Pump();
        }

        /// <summary>Re-reads <see cref="Settings.MusicOn"/> now (call after the toggle changes).</summary>
        public void Refresh()
        {
            nextPoll = Time.unscaledTime + PollInterval;
            wanted = Settings.MusicOn;
            if (!wanted) return;
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.clip == null || source.isPlaying || (i != active && gains[i] <= 0f)) continue;
                source.UnPause();
                if (!source.isPlaying) source.Play();
            }
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
            if (desiredKey < 0 && Time.unscaledTime > DefaultAfter) PlayStop(0, false);
            if (Time.unscaledTime >= nextPoll) Refresh();
            if (active < 0) return;

            float dt = Time.unscaledDeltaTime;
            fade = Mathf.MoveTowards(fade, wanted ? 1f : 0f, dt * (wanted ? 0.6f : 2.5f));
            float now = Time.time;
            bool ducking = now >= duckFrom && now < duckUntil;
            duck = Mathf.MoveTowards(duck, ducking ? DuckLevel : 1f, dt * (ducking ? 4f : 0.9f));

            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.clip == null) continue;
                float step = dt / CrossfadeSeconds;
                gains[i] = Mathf.MoveTowards(gains[i], i == active ? 1f : 0f, step);
                if (i != active && gains[i] <= 0f)
                {
                    // Faded out: stop it and let its clip be evicted.
                    source.Stop();
                    source.clip = null;
                    sourceKey[i] = -1;
                    continue;
                }
                // Equal-power-ish crossfade.
                float g = Mathf.Sin(gains[i] * Mathf.PI * 0.5f);
                source.volume = Volume * fade * duck * g;
                // Paused rather than left playing silently, so a disabled toggle costs nothing.
                if (fade <= 0f && source.isPlaying) source.Pause();
            }
        }

        void Crossfade(int key, AudioClip clip)
        {
            int next = active < 0 ? 0 : 1 - active;
            var source = sources[next];
            source.Stop();
            source.clip = clip;
            sourceKey[next] = key;
            // The very first track starts at full gain (the setting fade-in covers it).
            bool first = active < 0 || sources[active].clip == null;
            gains[next] = first ? 1f : 0f;
            source.volume = 0f;
            active = next;
            if (wanted) source.Play();
        }

        void Pump()
        {
            if (rendering) return;
            int key = NextToRender();
            if (key < 0) return;
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
            while ((key = NextToRender()) >= 0)
            {
                // Full speed only while silent; with a loop already playing, a missed prefetch renders
                // at the gentle budget so it never costs frames during an arrival pan.
                bool silent = active < 0 || sources[active].clip == null;
                int budget = key == desiredKey && silent ? ForegroundBudget : BackgroundBudget;
                int k = key;
                AudioClip made = null;
                yield return MusicSynth.Render(Stop(k), Dark(k), budget, clip => made = clip);
                cache[k] = made;
                Touch(k);
                Evict();
                if (k == desiredKey && (active < 0 || sourceKey[active] != k)) Crossfade(k, made);
            }
            rendering = false;
        }

        void Touch(int key)
        {
            recent.Remove(key);
            recent.Add(key);
        }

        void Evict()
        {
            for (int i = 0; i < recent.Count && cache.Count > CacheLimit; i++)
            {
                int key = recent[i];
                if (key == desiredKey || key == prefetchKey || key == sourceKey[0] || key == sourceKey[1]) continue;
                if (cache.TryGetValue(key, out var clip) && clip != null) Destroy(clip);
                cache.Remove(key);
                recent.RemoveAt(i);
                i--;
            }
        }

        static int Key(int stop, bool dark)
        {
            int style = ((stop % MusicSynth.StyleCount) + MusicSynth.StyleCount) % MusicSynth.StyleCount;
            return style * 2 + (dark ? 1 : 0);
        }

        static int Stop(int key) => key / 2;
        static bool Dark(int key) => (key & 1) == 1;
    }
}
