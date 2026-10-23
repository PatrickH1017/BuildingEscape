using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A melee weapon item (crowbar, bat, hatchet): the crowbar, baseball bat and fire axe of the Synty Battle Royale pack (the owner added it, 2026-10-18; the weapon catalog says which models, how they are turned
    /// and where the hand closes on them). It is a normal Pickup (weighs something, goes in the backpack); equip it in the inventory (Tab)
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
            /// <summary>Health taken off a training dummy (and a rival in the PVP room) per hit.</summary>
            public float damage;
            /// <summary>
            /// Health taken off a guard per hit (the owner, 2026-10-22): a base guard has 100 HP (<see cref="GuardHealthRules.BaseHealth"/>), so the crowbar's 15 takes seven hits, the bat's 25 four, the hatchet's 35 three. Crowbar &lt; bat &lt; hatchet.
            /// The same on every hit, whatever the number of guards about; change the numbers in <see cref="GuardHealthRules"/>.
            /// </summary>
            public float guardDamage;
        }

        // length = how long the weapon is in metres when held; stun in seconds; knock = shove on hit.
        public static readonly Spec[] Specs =
        {
            new Spec { name = "Crowbar",      weight = 3f, length = 0.85f, stun = 4f, knock = 5f, cooldown = 0.9f, noise = 16f, damage = 14f, guardDamage = GuardHealthRules.CrowbarDamage },
            new Spec { name = "Baseball Bat", weight = 2f, length = 1.00f, stun = 3f, knock = 9f, cooldown = 0.8f, noise = 14f, damage = 11f, guardDamage = GuardHealthRules.BatDamage },
            new Spec { name = "Hatchet",      weight = 3f, length = 0.70f, stun = 6f, knock = 4f, cooldown = 1.0f, noise = 18f, damage = 22f, guardDamage = GuardHealthRules.HatchetDamage },
        };

        /// <summary>How far up the handle (m, as held) the hand closes, from the end opposite the head: a hand's width, so that a little of the handle shows below the fist (crowbar, bat, hatchet).</summary>
        public static readonly float[] GripFromEnd = { 0.16f, 0.14f, 0.12f };

        public Kind kind;
        public Spec Data => Specs[(int)kind];

        static WeaponCatalog catalog;
        public static WeaponCatalog Catalog => catalog ? catalog : (catalog = Resources.Load<WeaponCatalog>("WeaponCatalog"));
        public static bool Available => Catalog && Catalog.crowbar && Catalog.bat && Catalog.hatchet;

        public static GameObject PrefabOf(Kind k) => k == Kind.Crowbar ? Catalog.crowbar : k == Kind.Bat ? Catalog.bat : Catalog.hatchet;

        /// <summary>How the catalog says the model must be turned so that it runs along +Y with its head up.</summary>
        public static Quaternion TurnOf(Kind k) => Quaternion.Euler(k == Kind.Crowbar ? Catalog.crowbarTurn : k == Kind.Bat ? Catalog.batTurn : Catalog.hatchetTurn);

        static Vector3 GripOf(Kind k) => k == Kind.Crowbar ? Catalog.crowbarGrip : k == Kind.Bat ? Catalog.batGrip : Catalog.hatchetGrip;

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

        // ------------------------------------------------------------------ the model

        /// <summary>Where a weapon's parts are once its model is turned the way the game holds it (head up along +Y), and what follows from that. Measured once per weapon, from the prefab.</summary>
        public struct Layout
        {
            public bool valid;
            /// <summary>Scale that makes the weapon as long as its spec says (the packs use different units).</summary>
            public float scale;
            /// <summary>The turn that takes the model into the game's frame.</summary>
            public Quaternion turn;
            /// <summary>The model's box around its own origin, in the game's frame, before scaling.</summary>
            public Bounds bounds;
            /// <summary>Where the hand closes round it (on the handle's own axis): in the game's frame around the model's origin, before scaling.</summary>
            public Vector3 grip;
        }

        static Layout[] layouts = new Layout[Specs.Length];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { layouts = new Layout[Specs.Length]; }

        /// <summary>The measurements of the model (made the first time, from a copy of the prefab that is gone again at once).</summary>
        public static Layout LayoutOf(Kind k)
        {
            var l = layouts[(int)k];
            if (l.valid) return l;
            var turn = TurnOf(k);
            l.turn = turn;
            l.scale = 1f;
            l.bounds = new Bounds(Vector3.zero, Vector3.one * 0.1f);
            if (WeaponCatalog.Measure(PrefabOf(k), turn, null, out var b))
            {
                l.bounds = b;
                float len = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (len > 0.0001f) l.scale = Specs[(int)k].length / len;
            }
            var grip = GripOf(k);
            l.grip = grip != Vector3.zero ? turn * grip : new Vector3(0f, l.bounds.min.y + GripFromEnd[(int)k] / l.scale, 0f);
            l.valid = true;
            layouts[(int)k] = l;
            return l;
        }

        static void StripColliders(GameObject go)
        {
            // (switched off first: a mesh collider that is not convex must not sit on a rigid body even for the rest of this frame)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) { c.enabled = false; Object.Destroy(c); }
        }

        /// <summary>A weapon lying around as a pickup item.</summary>
        public static GameObject Create(Kind k, Vector3 pos, Transform parent, float yaw = float.NaN)
        {
            var spec = Specs[(int)k];
            var lay = LayoutOf(k);
            var root = new GameObject(spec.name);
            root.transform.SetParent(parent, true);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, float.IsNaN(yaw) ? Random.Range(0f, 360f) : yaw, 90f); // lying on its side

            var model = Object.Instantiate(PrefabOf(k), root.transform);
            StripColliders(model);
            model.transform.localRotation = lay.turn;
            model.transform.localScale = Vector3.one * lay.scale;
            model.transform.localPosition = -(lay.bounds.center * lay.scale);        // the middle of the box at the item's origin (the pack's own origin can be anywhere on the weapon)

            var bc = root.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = lay.bounds.size * lay.scale;
            root.AddComponent<Rigidbody>().mass = 2f;
            var p = root.AddComponent<Pickup>();
            p.weight = spec.weight; p.label = spec.name;
            root.AddComponent<MeleeWeapon>().kind = k;
            return root;
        }

        /// <summary>The weapon model parented into a hand bone (visual only).</summary>
        public static GameObject BuildHeld(Kind k, Transform hand)
        {
            var lay = LayoutOf(k);
            var go = Object.Instantiate(PrefabOf(k));
            go.name = Specs[(int)k].name + "_Held";
            StripColliders(go);
            go.transform.SetParent(hand, false);
            var hold = HoldRotation;
            go.transform.localRotation = hold * lay.turn;
            go.transform.localScale = Vector3.one * lay.scale / Mathf.Max(0.01f, hand.lossyScale.y);
            // the palm sits a little along the hand from the wrist; the point of the handle the fingers close round goes there
            var palm = hand.position + hand.TransformDirection(Vector3.left) * 0.07f;
            go.transform.position = palm - hand.rotation * (hold * (lay.grip * lay.scale));
            return go;
        }
    }
}
