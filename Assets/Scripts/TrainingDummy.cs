using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A practice dummy in the loading dock: whack it with a weapon and the health bar over its head drops. When it
    /// is knocked down it lies there a few seconds and then stands up full again; left alone it slowly heals.
    ///
    /// The host (or an offline game) decides the damage, from the weapon the player really holds. Teammates see the
    /// same health, mirrored through RunSync.
    /// </summary>
    public class TrainingDummy : MonoBehaviour
    {
        public static TrainingDummy Current { get; private set; }

        public float maxHealth = 100f;
        [Tooltip("Seconds it stays knocked down.")]
        public float resetSeconds = 3f;
        public float regenDelay = 5f, regenPerSecond = 15f;

        public float Health { get; private set; }
        public bool Down { get; private set; }
        public int Knockdowns { get; private set; }
        public float Fraction => Mathf.Clamp01(Health / Mathf.Max(1f, maxHealth));

        Transform body, bar, fill;
        Renderer fillRenderer;
        float sway, flash, lastHitTime, downUntil, tilt;
        Vector3 swayAxis = Vector3.right;
        static Material barMat;

        void Awake() { Health = maxHealth; }
        void OnEnable() { Current = this; }
        void OnDisable() { if (Current == this) Current = null; }

        // ------------------------------------------------------------------ building

        /// <summary>Makes a dummy standing at <paramref name="pos"/>: a base, a post, a sandbag body, a head and a target.</summary>
        public static TrainingDummy Create(Transform parent, Vector3 pos, float yaw, Material cloth, Material wood, Material target)
        {
            var root = new GameObject("TrainingDummy");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var d = root.AddComponent<TrainingDummy>();

            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 0.95f, 0f); cap.height = 1.9f; cap.radius = 0.5f;

            Part(root.transform, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.06f, 0f), new Vector3(1.1f, 0.06f, 1.1f), wood);
            var body = new GameObject("Body").transform;     // everything above the base sways about its foot
            body.SetParent(root.transform, false);
            d.body = body;
            Part(body, PrimitiveType.Cylinder, "Post", new Vector3(0f, 0.75f, 0f), new Vector3(0.14f, 0.7f, 0.14f), wood);
            Part(body, PrimitiveType.Capsule, "Torso", new Vector3(0f, 1.2f, 0f), new Vector3(0.62f, 0.5f, 0.5f), cloth);
            Part(body, PrimitiveType.Cube, "Arms", new Vector3(0f, 1.38f, 0f), new Vector3(1.15f, 0.12f, 0.12f), wood);
            Part(body, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.78f, 0f), new Vector3(0.34f, 0.34f, 0.34f), cloth);
            // a target on the chest: three rings facing +Z
            Part(body, PrimitiveType.Cylinder, "TargetOuter", new Vector3(0f, 1.2f, 0.24f), new Vector3(0.5f, 0.012f, 0.5f), target, new Vector3(90f, 0f, 0f));
            Part(body, PrimitiveType.Cylinder, "TargetMid", new Vector3(0f, 1.2f, 0.255f), new Vector3(0.32f, 0.012f, 0.32f), cloth, new Vector3(90f, 0f, 0f));
            Part(body, PrimitiveType.Cylinder, "TargetInner", new Vector3(0f, 1.2f, 0.27f), new Vector3(0.14f, 0.012f, 0.14f), target, new Vector3(90f, 0f, 0f));

            d.BuildBar(root.transform);
            return d;
        }

        static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            if (mat) g.GetComponent<Renderer>().sharedMaterial = mat;
            var col = g.GetComponent<Collider>();
            if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        }

        void BuildBar(Transform parent)
        {
            var sh = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (!barMat) barMat = new Material(sh) { name = "DummyBar" };
            bar = new GameObject("HealthBar").transform;
            bar.SetParent(parent, false);
            bar.localPosition = new Vector3(0f, 2.45f, 0f);
            Quad(bar, "Frame", new Vector3(0f, 0f, 0.002f), new Vector3(1.28f, 0.2f, 1f), new Color(0.05f, 0.05f, 0.06f, 1f));
            Quad(bar, "Back", new Vector3(0f, 0f, 0.001f), new Vector3(1.2f, 0.12f, 1f), new Color(0.35f, 0.08f, 0.08f, 1f));
            var f = Quad(bar, "Fill", new Vector3(-0.6f, 0f, 0f), new Vector3(1.2f, 0.12f, 1f), Color.green);
            fill = f.transform;
            fillRenderer = f.GetComponent<Renderer>();
        }

        GameObject Quad(Transform parent, string name, Vector3 pos, Vector3 scale, Color colour)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            var col = q.GetComponent<Collider>();
            if (col) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = barMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var pb = new MaterialPropertyBlock();
            pb.SetColor("_Color", colour);
            pb.SetColor("_RendererColor", colour);
            r.SetPropertyBlock(pb);
            return q;
        }

        // ------------------------------------------------------------------ damage

        /// <summary>
        /// A swing connected. Called on the machine of the player who swung: offline or on the host it applies the
        /// damage; on a client it asks the host (and shows the wobble straight away).
        /// </summary>
        public void Hit(float damage, Vector3 from)
        {
            if (Down) return;
            Wobble(from);
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestDummyHit(damage); return; }
            ApplyDamage(damage, from);
        }

        /// <summary>The real effect (offline, or on the host): health goes down, and at zero it falls.</summary>
        public void ApplyDamage(float damage, Vector3 from)
        {
            if (Down || damage <= 0f) return;
            Health = Mathf.Max(0f, Health - damage);
            lastHitTime = Time.time;
            flash = 1f;
            Wobble(from);
            if (Health <= 0f)
            {
                Down = true;
                Knockdowns++;
                downUntil = Time.time + resetSeconds;
            }
        }

        void Wobble(Vector3 from)
        {
            var away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
            away.Normalize();
            // rotate about the axis across the push direction so the top leans away from the swing
            swayAxis = Vector3.Cross(Vector3.up, away);
            sway = 1f;
            flash = 1f;
        }

        // ------------------------------------------------------------------ per frame

        void Update()
        {
            float dt = Time.deltaTime;
            if (Net.NetRole.IsRemoteClient)
            {
                // a teammate's machine: the host's health
                float h = Net.RunSync.Instance ? Net.RunSync.Instance.DummyHealth : -1f;
                if (h >= 0f)
                {
                    if (h < Health - 0.01f) { Wobble(transform.position - transform.forward); }
                    Health = h;
                    Down = h <= 0f;
                }
            }
            else
            {
                if (Down && Time.time >= downUntil) { Down = false; Health = maxHealth; }
                else if (!Down && Health < maxHealth && Time.time - lastHitTime > regenDelay)
                    Health = Mathf.Min(maxHealth, Health + regenPerSecond * dt);
                Net.RunSync.Instance?.HostSetDummyHealth(Health);
            }
            Animate(dt);
        }

        void Animate(float dt)
        {
            sway = Mathf.MoveTowards(sway, 0f, dt * 1.6f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 4f);
            tilt = Mathf.MoveTowards(tilt, Down ? 82f : 0f, dt * (Down ? 360f : 140f));
            if (body)
            {
                float wobble = sway * 14f * Mathf.Sin(Time.time * 20f);
                body.localRotation = Quaternion.AngleAxis(tilt + wobble, swayAxis);
            }
            if (bar)
            {
                var cam = Camera.main;
                if (cam) bar.rotation = cam.transform.rotation; // always readable
                bar.gameObject.SetActive(!Down || tilt < 60f);
            }
            if (fill)
            {
                float f = Fraction;
                fill.localScale = new Vector3(Mathf.Max(0.001f, 1.2f * f), 0.12f, 1f);
                fill.localPosition = new Vector3(-0.6f + 0.6f * f, 0f, 0f);
                var c = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.85f, 0.25f), f);
                c = Color.Lerp(c, Color.white, flash * 0.7f);
                var pb = new MaterialPropertyBlock();
                pb.SetColor("_Color", c);
                pb.SetColor("_RendererColor", c);
                fillRenderer.SetPropertyBlock(pb);
            }
        }
    }
}
