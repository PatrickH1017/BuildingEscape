using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A gun item: the pew pistol from the Guns pack. Like the melee weapons it is a normal Pickup (it weighs something and goes in the
    /// backpack) and is equipped in the inventory; unlike them it fires bullets and has a magazine that has to be reloaded.
    /// It lies in the guard outpost of floor 3 and the dealer sells it once that floor is finished (for money); the numbers below make it the strongest weapon in
    /// the game: one bullet counts as four of the ten hits it takes to knock a guard out (three bullets), a swing of the hatchet as one. It holds a clip of eight
    /// rounds and comes with two spare clips (three in all, the owner, 2026-10-18); more rounds are bought from the dealer.
    /// </summary>
    public class RangedWeapon : MonoBehaviour
    {
        public enum Kind { Pew }

        public struct Spec
        {
            public string name;
            /// <summary>Kilograms in the pack, and how long the gun is in metres when held (the pack models are not built to scale).</summary>
            public float weight, length;
            /// <summary>Health a bullet takes off a training dummy (a melee hit takes 11 to 22).</summary>
            public float damage;
            /// <summary>How many of a guard's hits to knock-out one bullet counts as (a melee hit counts as one).</summary>
            public int guardHits;
            /// <summary>Seconds a guard hit by a bullet is stunned, and the shove (m/s) a bullet gives a loose object.</summary>
            public float stun, knock;
            /// <summary>How far a gunshot is heard: guards within this many metres come to look.</summary>
            public float noise;
            public int magazine;
            /// <summary>The spare clips a new gun comes with (the clip in the gun is the first of the three).</summary>
            public int spareClips;
            public float reloadSeconds, fireInterval;
            /// <summary>How far a bullet flies (m) and the cone it is fired into (degrees).</summary>
            public float range, spread;
        }

        // The strongest weapon there is: damage / stun / knock / hits beat every melee weapon (see GunTest in NetDevTest).
        public static readonly Spec[] Specs =
        {
            new Spec
            {
                name = "Pew", weight = 1.2f, length = 0.30f,
                damage = 60f, guardHits = 4, stun = 8f, knock = 16f, noise = 45f,
                magazine = 8, spareClips = 2, reloadSeconds = 2.2f, fireInterval = 0.3f, range = 45f, spread = 1.2f,
            },
        };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        GunMagazine magazine;
        /// <summary>The rounds in this gun: the state travels with the item (drop it and pick it up again, it is as you left it).</summary>
        public GunMagazine Magazine => magazine ??= NewMagazine(Data);

        // (The elevator used to give every gun a full magazine again when a floor started. Since 2026-10-18 it does not: "more ammo should need to be purchased from the Vendor".)

        /// <summary>A new gun: a full clip in it and two spare ones (a gun that has been bought or found is loaded, the rest costs money).</summary>
        public static GunMagazine NewMagazine(Spec s)
        {
            var m = new GunMagazine(s.magazine, s.reloadSeconds, s.fireInterval);
            m.SetReserve(s.spareClips * s.magazine);
            return m;
        }

        public static WeaponCatalog Catalog => MeleeWeapon.Catalog;
        public static bool Available => Catalog && Catalog.pew;
        public static GameObject PrefabOf(Kind k) => Catalog.pew;

        /// <summary>True for any item that is a weapon (the backpack equips these).</summary>
        public static bool IsWeapon(Pickup p) => p && (p.TryGetComponent(out MeleeWeapon _) || p.TryGetComponent(out RangedWeapon _));

        // ------------------------------------------------------------------ the model

        /// <summary>Where things are on the pack's model, measured once from the prefab (its barrel points along +X, its top is +Y).</summary>
        public struct Layout
        {
            public bool valid;
            /// <summary>Model scale that makes the gun as long as the spec says.</summary>
            public float scale;
            /// <summary>The front of the barrel and the middle of the grip, in the prefab's own space.</summary>
            public Vector3 muzzle, grip;
            public Bounds bounds;
        }

        static Layout[] layouts = new Layout[Specs.Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { layouts = new Layout[Specs.Length]; }

        /// <summary>The measurements of the model (made the first time, from a copy of the prefab that is gone again at once).</summary>
        public static Layout LayoutOf(Kind k)
        {
            var l = layouts[(int)k];
            if (l.valid) return l;

            var spec = Specs[(int)k];
            var probe = Object.Instantiate(PrefabOf(k), new Vector3(0f, -4000f, 0f), Quaternion.identity);
            var origin = probe.transform.position;
            var rs = probe.GetComponentsInChildren<Renderer>();
            Bounds b = new Bounds(origin, new Vector3(0.14f, 0.09f, 0.014f));      // (what the pack's pistol measures, if it has no renderer)
            if (rs.Length > 0) { b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds); }
            Renderer slide = null;
            foreach (var r in rs) if (r.name.ToLowerInvariant().Contains("haut")) slide = r;      // "pewhaut": the slide, whose front is the muzzle
            var sb = slide ? slide.bounds : b;
            Object.Destroy(probe);

            b.center -= origin; sb.center -= origin;
            l.valid = true;
            l.bounds = b;
            l.scale = b.size.x > 0.001f ? spec.length / b.size.x : 1f;
            l.muzzle = new Vector3(sb.max.x, sb.center.y, sb.center.z);
            l.grip = new Vector3(b.min.x + 0.26f * b.size.x, b.center.y - 0.18f * b.size.y, b.center.z);
            layouts[(int)k] = l;
            return l;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }

        /// <summary>The gun lying around as a pickup item (on its side, like the melee weapons).</summary>
        public static GameObject Create(Kind k, Vector3 pos, Transform parent, float yaw = float.NaN)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var root = new GameObject(spec.name);
            root.transform.SetParent(parent, true);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, float.IsNaN(yaw) ? Random.Range(0f, 360f) : yaw, 0f);

            var model = Object.Instantiate(PrefabOf(k), root.transform);
            StripColliders(model);
            model.name = spec.name + "_Model";
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);                    // lying on its side: the thin way is up
            model.transform.localPosition = -(model.transform.localRotation * (lay.bounds.center * lay.scale));

            var size = lay.bounds.size * lay.scale;
            var bc = root.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = new Vector3(size.x, Mathf.Max(size.z, 0.03f), size.y);                  // (the side view's height is now along z)
            root.AddComponent<Rigidbody>().mass = 0.8f;
            var p = root.AddComponent<Pickup>();
            p.weight = spec.weight; p.label = spec.name;
            root.AddComponent<RangedWeapon>().kind = k;
            return root;
        }

        /// <summary>
        /// The gun held in a hand bone (visual only): a "grip" transform on the hand whose +Z is the barrel and +Y the top of the gun, the
        /// model on it, and the muzzle at the front of the barrel. <paramref name="grip"/> is what HeldGun aims.
        /// </summary>
        public static GameObject BuildHeld(Kind k, Transform hand, out Transform grip, out Transform muzzle)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);

            var root = new GameObject(spec.name + "_Held");
            grip = root.transform;
            grip.SetParent(hand, false);
            float inv = 1f / Mathf.Max(0.01f, hand.lossyScale.y);                              // the hand bone may be scaled
            grip.localScale = Vector3.one * inv;

            var model = Object.Instantiate(PrefabOf(k), grip, false);
            StripColliders(model);
            model.name = spec.name + "_Model";
            var turn = Quaternion.Euler(0f, -90f, 0f);                                         // the pack's +X (barrel) becomes the grip's +Z
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localRotation = turn;
            model.transform.localPosition = -(turn * (lay.grip * lay.scale));                  // the middle of the grip sits in the palm

            var m = new GameObject("Muzzle").transform;
            m.SetParent(grip, false);
            m.localPosition = model.transform.localPosition + turn * (lay.muzzle * lay.scale);
            m.localRotation = Quaternion.identity;
            muzzle = m;
            return root;
        }
    }
}
