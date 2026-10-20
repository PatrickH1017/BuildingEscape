using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The blast of a player whom a shotgun or sniper officer hit (the owner, 2026-10-19: "they should explode in a manner with reference to R.E.P.O"; <see cref="HeistManager.Blast"/>). A cartoon one, made of plain solid shapes so that it
    /// shows in every build: a glowing orange ball that swells and shrinks away with a pale core, grey puffs of smoke that swell, rise and shrink, pieces of the player in their colour thrown out and falling, a flash of light and a boom
    /// (the shotgun's and the rifle's recordings, low). Purely visual: every machine plays it by itself when the host says so (<c>RunSync.BroadcastBlast</c>), it reads nothing and decides nothing.
    /// </summary>
    public sealed class BlastFx : MonoBehaviour
    {
        /// <summary>How long the effect lasts before it is gone (seconds).</summary>
        public const float Seconds = 3f;
        const int Pieces = 16, Puffs = 7;
        const float FireSeconds = 0.8f, CoreSeconds = 0.45f, PuffSeconds = 1.8f;

        /// <summary>Blasts played on this machine (for the tests).</summary>
        public static int Played { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Played = 0;

        sealed class Piece { public Transform t; public Vector3 velocity, spin; public bool landed; }
        sealed class Puff { public Transform t; public Vector3 drift; public float size, delay; }

        Transform fire, core;
        Material fireMat, coreMat, smokeMat, pieceMat, darkMat;
        Light flash;
        readonly List<Piece> pieces = new List<Piece>();
        readonly List<Puff> puffs = new List<Puff>();
        float born, groundY;

        /// <summary>Plays a blast at <paramref name="at"/>; the pieces are <paramref name="colour"/> (the player's).</summary>
        public static BlastFx Play(Vector3 at, Color colour)
        {
            var go = new GameObject("Blast");
            go.transform.position = at;
            var fx = go.AddComponent<BlastFx>();
            fx.Build(colour);
            Played++;
            GunSounds.Shot(at, false, false, RangedWeapon.SoundSet.Shotgun, 0.55f);
            GunSounds.Shot(at, false, false, RangedWeapon.SoundSet.Rifle, 0.6f);
            return fx;
        }

        static Shader PlainShader() => Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Sprites/Default");

        static Material Plain(string matName, Color colour, float glow = 0f)
        {
            var m = new Material(PlainShader()) { name = matName, color = colour };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0f);
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", colour * glow);
            }
            return m;
        }

        Transform Ball(string ballName, Material mat)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            s.name = ballName;
            var col = s.GetComponent<Collider>(); if (col) Destroy(col);
            s.transform.SetParent(transform, false);
            s.transform.localScale = Vector3.one * 0.01f;
            var r = s.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return s.transform;
        }

        void Build(Color colour)
        {
            born = Time.time;
            groundY = transform.position.y - 1f;
            if (Physics.Raycast(transform.position, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)) groundY = hit.point.y + 0.06f;

            fireMat = Plain("Blast fire", new Color(1f, 0.36f, 0.05f), 1.1f);
            coreMat = Plain("Blast core", new Color(1f, 0.85f, 0.4f), 1.6f);
            smokeMat = Plain("Blast smoke", new Color(0.46f, 0.45f, 0.44f), 0.25f);
            fire = Ball("fireball", fireMat);
            core = Ball("core", coreMat);

            var lg = new GameObject("light").AddComponent<Light>();
            lg.transform.SetParent(transform, false);
            lg.type = LightType.Point; lg.range = 14f; lg.intensity = 8f; lg.color = new Color(1f, 0.7f, 0.35f); lg.shadows = LightShadows.None;
            flash = lg;

            for (int i = 0; i < Puffs; i++)
            {
                var dir = Random.insideUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.6f + 0.25f;
                puffs.Add(new Puff { t = Ball("smoke " + i, smokeMat), drift = dir.normalized * Random.Range(0.7f, 1.7f), size = Random.Range(1.1f, 2.0f), delay = Random.Range(0.04f, 0.2f) });
            }

            // the pieces: boxes of the player's colour (and a few dark ones), thrown out and falling
            pieceMat = Plain("Blast piece", colour);
            darkMat = Plain("Blast dark piece", new Color(colour.r * 0.35f, colour.g * 0.35f, colour.b * 0.35f, 1f));
            for (int i = 0; i < Pieces; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = "piece " + i;
                var col = c.GetComponent<Collider>(); if (col) Destroy(col);
                c.transform.SetParent(transform, false);
                c.transform.localScale = new Vector3(Random.Range(0.08f, 0.2f), Random.Range(0.08f, 0.2f), Random.Range(0.08f, 0.2f));
                c.transform.rotation = Random.rotation;
                var r = c.GetComponent<Renderer>();
                r.sharedMaterial = i % 4 == 3 ? darkMat : pieceMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var dir = Random.onUnitSphere; dir.y = Mathf.Abs(dir.y) * 0.7f + 0.35f;
                pieces.Add(new Piece { t = c.transform, velocity = dir.normalized * Random.Range(3.5f, 8f), spin = Random.insideUnitSphere * 540f });
            }
        }

        /// <summary>0 at the start, up to 1 at the end of its <paramref name="seconds"/>.</summary>
        static float Fraction(float t, float seconds) => Mathf.Clamp01(t / seconds);

        /// <summary>Swells fast to full size and shrinks away to nothing at the end: 0 .. 1 .. 0.</summary>
        static float Swell(float t, float seconds, float growUntil = 0.4f)
        {
            float k = Fraction(t, seconds);
            if (k < growUntil) { float u = k / growUntil; return 1f - (1f - u) * (1f - u); }
            float v = (k - growUntil) / (1f - growUntil);
            return 1f - v * v;
        }

        void Update()
        {
            float t = Time.time - born;
            if (t >= Seconds) { Destroy(gameObject); return; }

            fire.localScale = Vector3.one * (3.4f * Swell(t, FireSeconds) + 0.01f);
            core.localScale = Vector3.one * (1.8f * Swell(t, CoreSeconds, 0.5f) + 0.01f);
            flash.intensity = 8f * Mathf.Clamp01(1f - t / 0.6f);
            flash.enabled = flash.intensity > 0.01f;

            foreach (var p in puffs)
            {
                float pt = t - p.delay;
                if (pt <= 0f || pt >= PuffSeconds) { p.t.localScale = Vector3.one * 0.01f; continue; }
                p.t.position = transform.position + p.drift * pt;
                p.t.localScale = Vector3.one * (p.size * Swell(pt, PuffSeconds, 0.45f) + 0.01f);
            }

            float dt = Time.deltaTime;
            foreach (var p in pieces)
            {
                if (p.landed) continue;
                p.velocity += Physics.gravity * dt;
                var pos = p.t.position + p.velocity * dt;
                if (pos.y <= groundY) { pos.y = groundY; p.landed = true; }
                p.t.position = pos;
                p.t.rotation = Quaternion.Euler(p.spin * dt) * p.t.rotation;
            }
            if (t > 2.4f)                                                                   // (the pieces shrink away at the end)
                foreach (var p in pieces) p.t.localScale *= 0.93f;
        }

        void OnDestroy()
        {
            if (fireMat) Destroy(fireMat);
            if (coreMat) Destroy(coreMat);
            if (smokeMat) Destroy(smokeMat);
            if (pieceMat) Destroy(pieceMat);
            if (darkMat) Destroy(darkMat);
        }
    }
}
