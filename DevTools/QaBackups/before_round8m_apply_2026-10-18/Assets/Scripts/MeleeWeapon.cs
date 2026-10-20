using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A melee weapon item (crowbar, bat, hatchet) from the Simple low poly melee weapon pack.
    /// It is a normal Pickup (weighs something, goes in the backpack); equip it in the inventory (Tab)
    /// and it appears in the player's right hand. LMB swings it with the slash animation.
    /// </summary>
    public class MeleeWeapon : MonoBehaviour
    {
        public enum Kind { Crowbar, Bat, Hatchet }

        /// <summary>What the network calls a punch (a weapon is its <see cref="Kind"/>, 0-2).</summary>
        public const byte PunchKind = 255;

        public struct Spec
        {
            public string name;
            public float weight, length, stun, knock, cooldown, noise;
            /// <summary>Health taken off a training dummy per hit (guards go down by hit count instead).</summary>
            public float damage;
        }

        // length = how long the weapon is in metres when held; stun in seconds; knock = shove on hit.
        public static readonly Spec[] Specs =
        {
            new Spec { name = "Crowbar",      weight = 3f, length = 0.85f, stun = 4f, knock = 5f, cooldown = 0.9f, noise = 16f, damage = 14f },
            new Spec { name = "Baseball Bat", weight = 2f, length = 1.00f, stun = 3f, knock = 9f, cooldown = 0.8f, noise = 14f, damage = 11f },
            new Spec { name = "Hatchet",      weight = 3f, length = 0.70f, stun = 6f, knock = 4f, cooldown = 1.0f, noise = 18f, damage = 22f },
        };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        static WeaponCatalog catalog;
        public static WeaponCatalog Catalog => catalog ? catalog : (catalog = Resources.Load<WeaponCatalog>("WeaponCatalog"));
        public static bool Available => Catalog && Catalog.crowbar && Catalog.bat && Catalog.hatchet;

        public static GameObject PrefabOf(Kind k) => k == Kind.Crowbar ? Catalog.crowbar : k == Kind.Bat ? Catalog.bat : Catalog.hatchet;

        // How the weapon sits in the right hand: the weapon's +Y (its head) points the way the sword in the
        // sword pack's sample character points, and the flat of the blade matches the sword's.
        static readonly Vector3 HeadInHand = new Vector3(-0.982f, 0.116f, -0.152f);
        static readonly Vector3 FlatInHand = new Vector3(0.190f, 0.668f, -0.719f);

        public static Quaternion HoldRotation
        {
            get
            {
                var y = HeadInHand.normalized;
                var x = (FlatInHand - y * Vector3.Dot(FlatInHand, y)).normalized;
                var z = Vector3.Cross(x, y);
                return Quaternion.LookRotation(z, y);
            }
        }

        /// <summary>Scale that makes the prefab as long as its spec length (the packs use different units).</summary>
        static float Fit(GameObject prefab, Kind k, out Bounds meshBounds)
        {
            meshBounds = new Bounds(Vector3.zero, Vector3.one);
            var mf = prefab.GetComponentInChildren<MeshFilter>(true);
            if (!mf || !mf.sharedMesh) return 1f;
            meshBounds = mf.sharedMesh.bounds;
            float len = Mathf.Max(meshBounds.size.x, meshBounds.size.y, meshBounds.size.z);
            return len > 0.0001f ? Specs[(int)k].length / len : 1f;
        }

        static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }

        /// <summary>A weapon lying around as a pickup item.</summary>
        public static GameObject Create(Kind k, Vector3 pos, Transform parent, float yaw = float.NaN)
        {
            var spec = Specs[(int)k];
            var prefab = PrefabOf(k);
            var root = new GameObject(spec.name);
            root.transform.SetParent(parent, true);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, float.IsNaN(yaw) ? Random.Range(0f, 360f) : yaw, 90f); // lying on its side

            var model = Object.Instantiate(prefab, root.transform);
            StripColliders(model);
            float f = Fit(prefab, k, out var mb);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * f;

            var bc = root.AddComponent<BoxCollider>();
            bc.center = mb.center * f;
            bc.size = mb.size * f;
            root.AddComponent<Rigidbody>().mass = 2f;
            var p = root.AddComponent<Pickup>();
            p.weight = spec.weight; p.label = spec.name;
            root.AddComponent<MeleeWeapon>().kind = k;
            return root;
        }

        /// <summary>The weapon model parented into a hand bone (visual only).</summary>
        public static GameObject BuildHeld(Kind k, Transform hand)
        {
            var prefab = PrefabOf(k);
            var go = Object.Instantiate(prefab);
            go.name = Specs[(int)k].name + "_Held";
            StripColliders(go);
            float f = Fit(prefab, k, out _);
            go.transform.SetParent(hand, false);
            go.transform.localRotation = HoldRotation;
            go.transform.localScale = Vector3.one * f / Mathf.Max(0.01f, hand.lossyScale.y);
            // the palm sits a little along the hand from the wrist
            go.transform.position = hand.position + hand.TransformDirection(Vector3.left) * 0.07f;
            return go;
        }
    }
}
