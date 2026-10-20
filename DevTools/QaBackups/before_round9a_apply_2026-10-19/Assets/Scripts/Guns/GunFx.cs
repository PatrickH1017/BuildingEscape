using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What a gunshot looks like, for the player's pew and the armed guards' alike: a flash at the muzzle (a glowing quad and a light that
    /// lasts a few frames), a tracer from the muzzle to where the bullet landed, a spark where it hit something, and the brass the pack's
    /// bullet model makes when it is thrown out of the gun. Everything is pooled (a fixed number of objects made once), so firing never
    /// creates or destroys one. Purely visual: it reads nothing and decides nothing, so a client can play it for a shot the host fired.
    /// </summary>
    public sealed class GunFx : MonoBehaviour
    {
        const int FlashSlots = 6, TracerSlots = 10, SparkSlots = 8, CasingSlots = 14;
        const float FlashSeconds = 0.055f, TracerSeconds = 0.09f, SparkSeconds = 0.09f, CasingSeconds = 3.5f;
        /// <summary>The Synty cartridge is 3.2 cm long and the gun is scaled down to about three quarters; the case is thrown out a little bigger than that (3.8 cm) so that it can be seen on the floor.</summary>
        const float CasingScale = 1.2f;

        public static GunFx Instance { get; private set; }

        // counters for the tests
        public int FlashesShown, TracersShown, SparksShown, CasingsShown;

        sealed class Flash { public Transform t; public Renderer r; public Light light; public float until; }
        sealed class Tracer { public LineRenderer line; public float from, until; }
        sealed class Spark { public Transform t; public Renderer r; public float until; }
        sealed class Casing { public Transform t; public Vector3 velocity, spin; public float until; public bool landed; }

        Flash[] flashes;
        Tracer[] tracers;
        Spark[] sparks;
        Casing[] casings;
        Material flashMat, lineMat, sparkMat;
        Texture2D soft;
        Camera cam;
        GameObject casingPrefab;
        int nextFlash, nextTracer, nextSpark, nextCasing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        /// <summary>The effects object (made the first time anybody fires).</summary>
        public static GunFx Get()
        {
            if (Instance) return Instance;
            var go = new GameObject("GunFx");
            DontDestroyOnLoad(go);
            return go.AddComponent<GunFx>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (flashMat) Destroy(flashMat);
            if (lineMat) Destroy(lineMat);
            if (sparkMat) Destroy(sparkMat);
            if (soft) Destroy(soft);
        }

        // ------------------------------------------------------------------ building the pools

        static Shader Sprite() => Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent") ?? Shader.Find("Standard");

        Texture2D SoftDisc()
        {
            const int s = 32;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { name = "GunFx soft disc", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = (x + 0.5f) / s * 2f - 1f, dy = (y + 0.5f) / s * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return t;
        }

        void Build()
        {
            var sh = Sprite();
            soft = SoftDisc();
            flashMat = new Material(sh) { name = "GunFx flash", mainTexture = soft, color = new Color(1f, 0.85f, 0.45f, 1f) };
            sparkMat = new Material(sh) { name = "GunFx spark", mainTexture = soft, color = new Color(1f, 0.9f, 0.6f, 1f) };
            lineMat = new Material(sh) { name = "GunFx tracer", color = Color.white };

            flashes = new Flash[FlashSlots];
            for (int i = 0; i < FlashSlots; i++)
            {
                var q = MakeQuad("flash " + i, flashMat, 0.34f);
                var lg = new GameObject("light").AddComponent<Light>();
                lg.transform.SetParent(q.transform, false);
                lg.type = LightType.Point; lg.range = 7f; lg.intensity = 3f; lg.color = new Color(1f, 0.82f, 0.5f);
                lg.shadows = LightShadows.None; lg.enabled = false;
                flashes[i] = new Flash { t = q.transform, r = q.GetComponent<Renderer>(), light = lg };
                q.SetActive(false);
            }
            tracers = new Tracer[TracerSlots];
            for (int i = 0; i < TracerSlots; i++)
            {
                var go = new GameObject("tracer " + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = lineMat;
                lr.positionCount = 2; lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                lr.numCapVertices = 2;
                go.SetActive(false);
                tracers[i] = new Tracer { line = lr };
            }
            sparks = new Spark[SparkSlots];
            for (int i = 0; i < SparkSlots; i++)
            {
                var q = MakeQuad("spark " + i, sparkMat, 0.16f);
                sparks[i] = new Spark { t = q.transform, r = q.GetComponent<Renderer>() };
                q.SetActive(false);
            }
            casings = new Casing[CasingSlots];
            var cat = RangedWeapon.Catalog;
            casingPrefab = cat ? cat.pewBullet : null;
            for (int i = 0; i < CasingSlots; i++)
            {
                Transform t = null;
                if (casingPrefab)
                {
                    var c = Instantiate(casingPrefab, transform);
                    foreach (var col in c.GetComponentsInChildren<Collider>(true)) Destroy(col);
                    foreach (var rr in c.GetComponentsInChildren<Renderer>(true)) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    c.name = "casing " + i;
                    c.transform.localScale = Vector3.one * CasingScale;
                    c.SetActive(false);
                    t = c.transform;
                }
                casings[i] = new Casing { t = t };
            }
        }

        GameObject MakeQuad(string name, Material mat, float size)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            var col = q.GetComponent<Collider>(); if (col) Destroy(col);
            q.transform.SetParent(transform, false);
            q.transform.localScale = Vector3.one * size;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return q;
        }

        // ------------------------------------------------------------------ one shot

        /// <summary>
        /// A shot: the flash at <paramref name="muzzle"/>, a tracer to <paramref name="end"/>, a spark there when the bullet hit something
        /// (<paramref name="impact"/>), and a case thrown out to the right of the gun - from <paramref name="casingFrom"/>, the slide's ejection port, when the gun has one (with a silencer on, the muzzle is the far end of it).
        /// </summary>
        public void Shot(Vector3 muzzle, Vector3 end, bool impact, Vector3 right, bool silenced = false, Vector3? casingFrom = null)
        {
            float now = Time.time;

            var f = flashes[nextFlash++ % FlashSlots];
            f.t.gameObject.SetActive(true);
            f.t.position = muzzle;
            f.t.localScale = Vector3.one * Random.Range(0.4f, 0.6f) * (silenced ? 0.4f : 1f);        // (a silenced shot: a small flash that does not light the room)
            f.light.enabled = !silenced;
            f.until = now + FlashSeconds;
            FlashesShown++;

            var t = tracers[nextTracer++ % TracerSlots];
            t.line.gameObject.SetActive(true);
            t.line.SetPosition(0, muzzle);
            t.line.SetPosition(1, end);
            t.from = now; t.until = now + TracerSeconds;
            TracersShown++;

            if (impact)
            {
                var s = sparks[nextSpark++ % SparkSlots];
                s.t.gameObject.SetActive(true);
                s.t.position = end;
                s.until = now + SparkSeconds;
                SparksShown++;
            }

            var c = casings[nextCasing++ % CasingSlots];
            if (c.t)
            {
                c.t.gameObject.SetActive(true);
                c.t.position = casingFrom ?? (muzzle - (end - muzzle).normalized * 0.05f);
                c.t.rotation = Random.rotation;
                c.velocity = right.normalized * Random.Range(1.6f, 2.6f) + Vector3.up * Random.Range(1.2f, 2.2f);
                c.spin = Random.insideUnitSphere * 900f;
                c.until = now + CasingSeconds;
                c.landed = false;
                CasingsShown++;
            }
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            float now = Time.time, dt = Time.deltaTime;
            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;

            for (int i = 0; i < flashes.Length; i++)
            {
                var f = flashes[i];
                if (!f.t.gameObject.activeSelf) continue;
                if (now >= f.until) { f.light.enabled = false; f.t.gameObject.SetActive(false); continue; }
                float k = (f.until - now) / FlashSeconds;
                if (cam) f.t.rotation = cam.transform.rotation;
                f.light.intensity = 3f * k;
            }
            for (int i = 0; i < tracers.Length; i++)
            {
                var t = tracers[i];
                if (!t.line.gameObject.activeSelf) continue;
                if (now >= t.until) { t.line.gameObject.SetActive(false); continue; }
                float k = 1f - (now - t.from) / (t.until - t.from);
                t.line.startWidth = 0.045f * k; t.line.endWidth = 0.012f * k;
                t.line.startColor = new Color(1f, 0.9f, 0.55f, 0.95f * k);
                t.line.endColor = new Color(1f, 0.75f, 0.3f, 0.45f * k);
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                var s = sparks[i];
                if (!s.t.gameObject.activeSelf) continue;
                if (now >= s.until) { s.t.gameObject.SetActive(false); continue; }
                if (cam) s.t.rotation = cam.transform.rotation;
                s.t.localScale = Vector3.one * Mathf.Lerp(0.05f, 0.2f, 1f - (s.until - now) / SparkSeconds);
            }
            for (int i = 0; i < casings.Length; i++)
            {
                var c = casings[i];
                if (!c.t || !c.t.gameObject.activeSelf) continue;
                if (now >= c.until) { c.t.gameObject.SetActive(false); continue; }
                if (c.landed) continue;
                var from = c.t.position;
                c.velocity += Physics.gravity * dt;
                var step = c.velocity * dt;
                if (Physics.Raycast(from, step.normalized, out var hit, step.magnitude + 0.01f, ~0, QueryTriggerInteraction.Ignore))
                {
                    c.landed = true;
                    c.t.position = hit.point + hit.normal * 0.012f;
                    c.t.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Random.onUnitSphere, hit.normal).normalized + hit.normal * 0.001f, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                    continue;
                }
                c.t.position = from + step;
                c.t.Rotate(c.spin * dt, Space.Self);
            }
        }

        /// <summary>For the tests: is any effect showing right now?</summary>
        public bool AnythingShowing()
        {
            foreach (var f in flashes) if (f.t.gameObject.activeSelf) return true;
            foreach (var t in tracers) if (t.line.gameObject.activeSelf) return true;
            return false;
        }
    }
}
