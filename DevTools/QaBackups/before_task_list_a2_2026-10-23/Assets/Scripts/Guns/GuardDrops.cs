using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What an armed officer leaves when he goes down for good (the owner, 2026-10-19: "guards should drop a fair amount of bullets per guard"): a clip of his gun's ammunition, lying where he fell. It is the same clip as a safe's or the
    /// dealer's (<see cref="AmmoClip"/>): the crosshair on it and E, and its rounds go into the gun of that kind the taker carries, up to three clips; nobody who carries that gun leaves it lying. The host (or an offline game) makes it and
    /// tells the clients (<c>RunSync.BroadcastSpawnClip</c>), who make the same one with the host's network id.
    /// </summary>
    public static class GuardDrops
    {
        static readonly Vector3 ClipSize = new Vector3(0.12f, 0.03f, 0.065f);          // (a plain box when the gun's model has no magazine part: the shotgun's shells)
        static Material plain, brass;

        /// <summary>Clips made by guards going down, on this machine (for the tests).</summary>
        public static int Dropped { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Dropped = 0; plain = null; brass = null; }

        /// <summary>The guard went down for good (host / offline only): his gun's clip drops. Unarmed guards, the boss and puppets drop none.</summary>
        public static void OnDown(GuardAI g)
        {
            if (g == null || g.IsPuppet || g.IsBoss || !g.Loadout.Armed) return;
            var kind = g.Loadout.Kind;
            int rounds = GuardLoadouts.DropRounds(kind);
            var pos = g.transform.position + Vector3.up * 0.4f + g.transform.forward * 0.3f;
            var go = Create(kind, rounds, pos, ParentOnShow());
            if (go && go.TryGetComponent(out Pickup pk)) Net.RunSync.Instance?.BroadcastSpawnClip(pk.NetId, pos, (byte)kind, (byte)rounds);
        }

        /// <summary>A client: the host made a clip; make the same one (the id is the host's).</summary>
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

        static Material Plain(ref Material cache, Color colour)
        {
            if (cache) return cache;
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Sprites/Default");
            cache = new Material(sh) { name = "GuardDrops", color = colour };
            return cache;
        }

        /// <summary>A clip of <paramref name="rounds"/> rounds for a gun of this kind at <paramref name="pos"/>: a real item (a rigid body that falls and is picked up).</summary>
        public static GameObject Create(RangedWeapon.Kind kind, int rounds, Vector3 pos, Transform parent)
        {
            var spec = RangedWeapon.Specs[(int)kind];
            string label = kind == RangedWeapon.Kind.Pew ? "Pistol clip" : spec.ammoName;
            var go = new GameObject(label);
            if (parent) go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var model = RangedWeapon.CreateClipModel(go.transform, out var size, kind);
            bool real = model != null;
            if (!real)
            {
                size = ClipSize;
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(go.transform, false);
                body.transform.localScale = ClipSize;
                body.GetComponent<Renderer>().sharedMaterial = Plain(ref plain, new Color(0.18f, 0.18f, 0.2f));
                var col = body.GetComponent<Collider>(); if (col) Object.Destroy(col);
                var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tip.name = "Rounds";
                tip.transform.SetParent(go.transform, false);
                tip.transform.localPosition = new Vector3(0.012f, ClipSize.y * 0.5f + 0.003f, 0f);
                tip.transform.localScale = new Vector3(ClipSize.x * 0.72f, 0.008f, ClipSize.z * 0.6f);
                tip.GetComponent<Renderer>().sharedMaterial = Plain(ref brass, new Color(0.85f, 0.68f, 0.2f));
                var col2 = tip.GetComponent<Collider>(); if (col2) Object.Destroy(col2);
            }
            var box = go.AddComponent<BoxCollider>();
            box.center = real ? Vector3.zero : new Vector3(0f, 0.004f, 0f);
            box.size = real ? size + new Vector3(0.004f, 0.004f, 0.004f) : ClipSize + new Vector3(0f, 0.008f, 0f);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.15f;
            rb.isKinematic = Net.NetRole.IsRemoteClient;           // (the host simulates it; the others follow its snapshots)
            var pk = go.AddComponent<Pickup>();                    // (registers its network id now)
            pk.weight = 0.2f; pk.label = label;
            var ammo = go.AddComponent<AmmoClip>();
            ammo.rounds = rounds;
            ammo.kind = kind;
            Dropped++;
            return go;
        }
    }
}
