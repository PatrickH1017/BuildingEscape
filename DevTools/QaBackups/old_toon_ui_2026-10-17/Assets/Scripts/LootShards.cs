using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The burst of shards when a treasure shatters (what <see cref="HeavyLoot"/> does when it takes too many knocks and has no shatter prefab of its own): a few
    /// dozen little pieces of its colour thrown outwards that tumble, fall and fade, and a crash made from noise. Purely visual and local - no physics, nothing
    /// that touches a person - so it is made on every machine from the host's word that the piece broke. The owner, 2026-10-17: a carried piece "disappeared".
    /// </summary>
    public sealed class LootShards : MonoBehaviour
    {
        const int Count = 22;
        const float Life = 2.6f;

        struct Shard { public Transform t; public Vector3 v, spin; public float rest; }

        Shard[] shards;
        float born;
        float floorY;
        Material mat;
        Renderer[] renderers;

        static AudioClip crash;

        /// <summary>Bursts a piece at <paramref name="at"/>: <paramref name="size"/> is about how big it was (m across).</summary>
        public static void Burst(Vector3 at, Color colour, float size)
        {
            var go = new GameObject("LootShards");
            go.transform.position = at;
            go.AddComponent<LootShards>().Begin(at, colour, size);
            if (!crash) crash = MakeCrash();
            AudioSource.PlayClipAtPoint(crash, at, 0.9f);
        }

        void Begin(Vector3 at, Color colour, float size)
        {
            born = Time.time;
            floorY = 0f;                                                          // (every floor, and the docks, lie at y = 0)
            var sh = Shader.Find("Sprites/Default");
            mat = new Material(sh) { color = Color.white, hideFlags = HideFlags.HideAndDontSave };
            shards = new Shard[Count];
            renderers = new Renderer[Count];
            var rnd = new System.Random(Mathf.RoundToInt(at.x * 131f + at.z * 71f));
            float R() => (float)rnd.NextDouble();
            for (int i = 0; i < Count; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Cube);
                s.name = "Shard";
                var c = s.GetComponent<Collider>(); if (c) Destroy(c);
                s.transform.SetParent(transform, false);
                s.transform.localPosition = new Vector3((R() - 0.5f) * size * 0.4f, (R() - 0.5f) * size * 0.4f, (R() - 0.5f) * size * 0.4f);
                float k = 0.03f + R() * 0.05f;
                s.transform.localScale = new Vector3(k, k * (0.3f + R() * 0.7f), k * (0.6f + R() * 0.8f));
                s.transform.rotation = Random.rotation;
                var r = s.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                var block = new MaterialPropertyBlock();
                float shade = 0.65f + 0.45f * R();
                block.SetColor("_Color", new Color(Mathf.Clamp01(colour.r * shade), Mathf.Clamp01(colour.g * shade), Mathf.Clamp01(colour.b * shade), 1f));
                r.SetPropertyBlock(block);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i] = r;
                var dir = new Vector3(R() - 0.5f, R() * 0.8f + 0.2f, R() - 0.5f).normalized;
                shards[i] = new Shard { t = s.transform, v = dir * (1.2f + R() * 2.6f), spin = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f) * 720f, rest = -1f };
            }
        }

        void Update()
        {
            float age = Time.time - born;
            if (age > Life) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            float fade = Mathf.Clamp01((Life - age) / 0.8f);
            for (int i = 0; i < shards.Length; i++)
            {
                var s = shards[i];
                if (!s.t) continue;
                if (s.rest < 0f)
                {
                    s.v += Physics.gravity * dt;
                    s.t.position += s.v * dt;
                    s.t.Rotate(s.spin * dt, Space.Self);
                    if (s.t.position.y <= floorY + 0.02f && s.v.y < 0f)
                    {
                        // lands: a small hop or two, then lies still
                        var p = s.t.position; p.y = floorY + 0.02f; s.t.position = p;
                        s.v = new Vector3(s.v.x * 0.45f, -s.v.y * 0.35f, s.v.z * 0.45f);
                        s.spin *= 0.4f;
                        if (s.v.y < 0.5f) { s.rest = age; s.v = Vector3.zero; }
                    }
                }
                shards[i] = s;
                if (fade < 1f && renderers[i])
                {
                    var block = new MaterialPropertyBlock();
                    renderers[i].GetPropertyBlock(block);
                    var col = block.GetColor("_Color"); col.a = fade;
                    block.SetColor("_Color", col);
                    renderers[i].SetPropertyBlock(block);
                }
            }
        }

        void OnDestroy() { if (mat) Destroy(mat); }

        /// <summary>A short crash: a burst of noise that dies away, with a few bright pings in it.</summary>
        static AudioClip MakeCrash()
        {
            const int rate = 44100;
            int n = rate * 40 / 100;
            var d = new float[n];
            var rnd = new System.Random(11);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n, env = Mathf.Pow(1f - u, 3f);
                lp += ((float)(rnd.NextDouble() * 2.0 - 1.0) - lp) * 0.55f;
                float ping = Mathf.Sin(6.2831853f * (1800f + 900f * (i / 3000 % 3)) * (i / (float)rate)) * Mathf.Exp(-u * 18f) * 0.35f;
                d[i] = Mathf.Clamp(lp * env * 1.6f + ping, -1f, 1f);
            }
            var c = AudioClip.Create("LootCrash", n, 1, rate, false);
            c.SetData(d, 0);
            return c;
        }
    }
}
