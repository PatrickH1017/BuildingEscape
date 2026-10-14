using UnityEngine;

namespace BuildingEscape
{
    /// <summary>Plays one-shot sound effects and picks the right ones for melee hits.</summary>
    public static class GameAudio
    {
        static AudioCatalog catalog;
        public static AudioCatalog Catalog => catalog ? catalog : (catalog = Resources.Load<AudioCatalog>("AudioCatalog"));

        public static T Pick<T>(T[] a) where T : class => (a == null || a.Length == 0) ? null : a[Random.Range(0, a.Length)];

        /// <summary>One-shot sound at a position. spatial=false plays it flat (own footsteps, UI).</summary>
        public static void Play(AudioClip clip, Vector3 pos, float volume = 1f, float pitch = 1f, bool spatial = true)
        {
            if (!clip) return;
            var go = new GameObject("sfx");
            go.transform.position = pos;
            var s = go.AddComponent<AudioSource>();
            s.clip = clip; s.volume = volume; s.pitch = pitch;
            s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 2.5f; s.maxDistance = 30f;
            s.Play();
            Object.Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
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
    }
}
