using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What an eliminated guard leaves behind (the owner, 2026-10-23: "Eliminated guards should drop 0-3 bullets with 0 being more like then 1 then 2 then 3 being the least likely" and "A player should have to run over the eliminated guard to get the
    /// dropped bullets"): 0 to 3 bullets of his gun's kind (an unarmed guard's are pistol bullets; the odds are <see cref="GuardDropRules"/>), brass cartridges lying where he fell. They are taken by RUNNING OVER them
    /// (<see cref="AmmoRunOver"/>), never with the crosshair and E, and only by a player who carries a gun of their kind. The host (or an offline game) rolls and makes them and tells the clients (<c>RunSync.BroadcastSpawnClip</c>), who make the
    /// same ones with the host's network id. (Before 2026-10-23 an armed guard left one whole clip, taken with E: the owner, 2026-10-19, "guards should drop a fair amount of bullets per guard".)
    /// </summary>
    public static class GuardDrops
    {
        static readonly Vector3 BoxSize = new Vector3(0.38f, 0.05f, 0.28f);
        static Material brass, tip;

        /// <summary>Piles of bullets made by guards going down, on this machine, and the bullets in them (for the tests).</summary>
        public static int Dropped { get; private set; }
        public static int DroppedBullets { get; private set; }

        /// <summary>Tests: the roll (0 to 99) that the next guard to go down uses instead of a random one; -1 = random.</summary>
        public static int TestRoll = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Dropped = 0; DroppedBullets = 0; TestRoll = -1; brass = null; tip = null; }

        /// <summary>The guard went down for good (host / offline only): the roll says how many bullets he leaves (often none). The boss and puppets drop none.</summary>
        public static void OnDown(GuardAI g)
        {
            if (g == null || g.IsPuppet || g.IsBoss) return;
            int bullets = GuardDropRules.Bullets(TestRoll >= 0 ? TestRoll : Random.Range(0, 100));
            if (bullets <= 0) return;
            var kind = g.Loadout.Armed ? g.Loadout.Kind : RangedWeapon.Kind.Pew;          // (an unarmed guard's bullets are pistol bullets)
            var pos = g.transform.position + Vector3.up * 0.4f + g.transform.forward * 0.3f;
            var go = Create(kind, bullets, pos, ParentOnShow());
            if (go && go.TryGetComponent(out Pickup pk)) Net.RunSync.Instance?.BroadcastSpawnClip(pk.NetId, pos, (byte)kind, (byte)bullets);
        }

        /// <summary>A client: the host made some bullets; make the same ones (the id is the host's).</summary>
        public static void SpawnFromNet(int id, Vector3 pos, RangedWeapon.Kind kind, int rounds)
        {
            Net.NetIds.PendingId = id;
            Create(kind, rounds, pos, ParentOnShow());
            Net.NetIds.PendingId = Net.NetIds.None;
        }

        static Transform ParentOnShow()
        {
            var gen = Object.FindFirstObjectByType<FloorGenerator>();
            return gen ? gen.Current : null;
        }

        static Material Plain(ref Material cache, Color colour, float gloss)
        {
            if (cache) return cache;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Sprites/Default");
            cache = new Material(sh) { name = "GuardDrops", color = colour };
            if (cache.HasProperty("_Glossiness")) cache.SetFloat("_Glossiness", gloss);
            if (cache.HasProperty("_Metallic")) cache.SetFloat("_Metallic", 0.6f);
            return cache;
        }

        /// <summary>
        /// <paramref name="bullets"/> bullets for a gun of this kind at <paramref name="pos"/>: a real item (a rigid body that falls to the floor) holding a few brass cartridges lying about, one for each bullet. It is no item for the pack and cannot be aimed
        /// at (<see cref="Pickup.runOverOnly"/>): <see cref="AmmoRunOver"/> gives the bullets to a player who runs over them.
        /// </summary>
        public static GameObject Create(RangedWeapon.Kind kind, int bullets, Vector3 pos, Transform parent)
        {
            bullets = Mathf.Clamp(bullets, 1, GuardDropRules.MaxBullets);
            string label = bullets == 1 ? "Bullet" : "Bullets";
            var go = new GameObject(label);
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            for (int i = 0; i < bullets; i++)
            {
                // a cartridge lying on its side: a brass case and its darker tip (a rifle's and a pistol's alike: only how many matters)
                float x = (i - (bullets - 1) * 0.5f) * 0.11f, z = Random.Range(-0.04f, 0.04f);
                var holder = new GameObject("Cartridge").transform;
                holder.SetParent(go.transform, false);
                holder.localPosition = new Vector3(x, 0.012f, z);
                holder.localRotation = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f);
                Cylinder(holder, "Case", new Vector3(0f, 0f, 0f), 0.03f, 0.07f, Plain(ref brass, new Color(0.88f, 0.70f, 0.20f), 0.55f));
                Cylinder(holder, "Tip", new Vector3(0f, 0f, 0.052f), 0.024f, 0.036f, Plain(ref tip, new Color(0.62f, 0.34f, 0.16f), 0.4f));
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, BoxSize.y * 0.5f, 0f);
            box.size = BoxSize;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.1f;
            rb.isKinematic = Net.NetRole.IsRemoteClient;           // (the host simulates it; the others follow its snapshots)
            var pk = go.AddComponent<Pickup>();                    // (registers its network id now)
            pk.weight = 0.1f; pk.label = label;
            pk.runOverOnly = true;
            var ammo = go.AddComponent<AmmoClip>();
            ammo.rounds = bullets;
            ammo.kind = kind;
            go.AddComponent<AmmoRunOver>();
            Dropped++; DroppedBullets += bullets;
            return go;
        }

        /// <summary>A cylinder lying along the holder's z axis (the primitive stands along y: turned onto its side), without its collider.</summary>
        static void Cylinder(Transform parent, string partName, Vector3 localPos, float diameter, float length, Material mat)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = partName;
            var col = c.GetComponent<Collider>(); if (col) Object.Destroy(col);
            c.transform.SetParent(parent, false);
            c.transform.localPosition = localPos;
            c.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            c.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);          // (the primitive is 2 m tall: half the length as its scale)
            var r = c.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
