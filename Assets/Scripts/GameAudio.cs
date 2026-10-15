using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Plays one-shot sound effects and picks the right ones for melee hits. Sounds play on a fixed pool of AudioSources that is
    /// built once, so playing a sound never creates or destroys an object (footsteps, keys and radios add up to dozens a second).
    /// </summary>
    public static class GameAudio
    {
        public const float DefaultMinDistance = 2.5f, DefaultMaxDistance = 30f;

        const int PoolSize = 32;
        const float MinStealAge = 0.15f;     // a sound this young is never cut off to make room

        static AudioCatalog catalog;
        static float nextCatalogSearch;
        static AudioSource[] pool;
        static float[] startedAt;
        static Transform poolRoot;
        static AudioListener listener;
        static float nextListenerSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()   // the editor may keep statics between play sessions (domain reload off)
        {
            catalog = null; nextCatalogSearch = 0f;
            pool = null; startedAt = null; poolRoot = null;
            listener = null; nextListenerSearch = 0f;
        }

        /// <summary>The sound catalog, or null while there is none (looked for again every few seconds).</summary>
        public static AudioCatalog Catalog
        {
            get
            {
                if (!catalog && Time.realtimeSinceStartup >= nextCatalogSearch)
                {
                    nextCatalogSearch = Time.realtimeSinceStartup + 5f;
                    catalog = Resources.Load<AudioCatalog>("AudioCatalog");
                }
                return catalog;
            }
        }

        public static T Pick<T>(T[] a) where T : class => (a == null || a.Length == 0) ? null : a[Random.Range(0, a.Length)];

        /// <summary>
        /// One-shot sound at a position. spatial=false plays it flat (own footsteps, UI). A spatial sound that is further from the
        /// listener than <paramref name="maxDistance"/> is not played at all. Returns the source it plays on (null when it was skipped).
        /// </summary>
        public static AudioSource Play(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, bool spatial = true,
                                       float maxDistance = DefaultMaxDistance, float minDistance = DefaultMinDistance)
        {
            if (!clip || volume <= 0.001f) return null;
            if (spatial && !InEarshot(pos, maxDistance)) return null;
            var s = Acquire();
            if (!s) return null;
            s.transform.position = pos;
            s.clip = clip; s.volume = volume; s.pitch = pitch;
            s.spatialBlend = spatial ? 1f : 0f;
            s.minDistance = minDistance; s.maxDistance = Mathf.Max(maxDistance, minDistance + 0.1f);
            s.Play();
            return s;
        }

        /// <summary>A door / elevator / officer sound (a random variant, or the one asked for).</summary>
        public static AudioSource PlaySound(SoundId id, Vector3 pos, float volume = 1f, float pitch = 1f, bool spatial = true,
                                            float maxDistance = DefaultMaxDistance, float minDistance = DefaultMinDistance, int variant = -1)
        {
            if (variant < 0) variant = Random.Range(0, SoundBank.Variants(id));
            return Play(SoundBank.Get(id, variant), pos, volume, pitch, spatial, maxDistance, minDistance);
        }

        public static void Whoosh(Vector3 pos)
        {
            var c = Catalog; if (c == null) return;
            Play(Pick(c.whoosh), pos, 0.6f, Random.Range(0.95f, 1.1f), false);
        }

        /// <summary>A weapon landing on a guard. A killing blow is heavier: finisher plus bone / gore layer.</summary>
        public static void MeleeHit(MeleeWeapon.Kind kind, bool killed, Vector3 pos)
        {
            var c = Catalog; if (c == null) return;
            bool bat = kind == MeleeWeapon.Kind.Bat, crow = kind == MeleeWeapon.Kind.Crowbar;
            if (!killed)
            {
                Play(Pick(bat ? c.hitBat : crow ? c.hitCrowbar : c.hitHatchet), pos, 1f, Random.Range(0.95f, 1.05f));
                return;
            }
            Play(Pick(bat ? c.killBat : crow ? c.killCrowbar : c.killHatchet), pos, 1f, 0.95f);
            Play(Pick(kind == MeleeWeapon.Kind.Hatchet ? c.layerGore : c.layerBone), pos, 0.9f, 1f);
        }

        // ------------------------------------------------------------------ pool

        /// <summary>A source that is free; else the one that has been playing longest (unless it only just started).</summary>
        static AudioSource Acquire()
        {
            if (!poolRoot) BuildPool();
            float now = Time.realtimeSinceStartup;
            int oldest = -1; float oldestStart = float.MaxValue;
            for (int i = 0; i < pool.Length; i++)
            {
                var s = pool[i];
                if (!s) continue;
                if (!s.isPlaying) { startedAt[i] = now; return s; }
                if (startedAt[i] < oldestStart) { oldestStart = startedAt[i]; oldest = i; }
            }
            if (oldest < 0 || now - oldestStart < MinStealAge) return null;   // a burst of sounds: the newest one is dropped
            startedAt[oldest] = now;
            return pool[oldest];
        }

        static void BuildPool()
        {
            var root = new GameObject("GameAudio (sound pool)");
            Object.DontDestroyOnLoad(root);
            poolRoot = root.transform;
            pool = new AudioSource[PoolSize];
            startedAt = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var child = new GameObject("voice " + i);
                child.transform.SetParent(poolRoot, false);
                var s = child.AddComponent<AudioSource>();
                s.playOnAwake = false; s.loop = false;
                s.dopplerLevel = 0f;                       // no pitch warble when the camera moves
                s.rolloffMode = AudioRolloffMode.Linear;
                pool[i] = s;
            }
        }

        /// <summary>False when the sound is further from the listener than it can be heard (it would only waste a voice).</summary>
        static bool InEarshot(Vector3 pos, float maxDistance)
        {
            if (!listener && Time.realtimeSinceStartup >= nextListenerSearch)
            {
                nextListenerSearch = Time.realtimeSinceStartup + 1f;
                listener = Object.FindFirstObjectByType<AudioListener>();
            }
            if (!listener) return true;
            return (listener.transform.position - pos).sqrMagnitude <= maxDistance * maxDistance;
        }
    }
}
