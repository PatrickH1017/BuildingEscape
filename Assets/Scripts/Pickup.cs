using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Marks an object as something a player can pick up into the backpack (E) or carry (Q).
    /// Every item has a weight; the backpack only holds so much.
    /// </summary>
    public class Pickup : MonoBehaviour
    {
        [Min(0.1f)] public float weight = 1f;
        [Tooltip("Shown in the inventory. Empty = use the loot name / object name.")]
        public string label;

        /// <summary>True while a player is carrying it in their hands (Q).</summary>
        [Tooltip("Cash it counts for when dropped in the elevator (0 = never banked, e.g. weapons). Loot uses its own value.")]
        public int bankValue;

        public bool IsHeld { get; set; }

        /// <summary>Network id (same on every machine for things the floor generator made). -1 = not registered.</summary>
        public int NetId { get; private set; } = Net.NetIds.None;

        void Awake()
        {
            NetId = Net.NetIds.Register(this);
            // On a remote client every pickup is a puppet: the host simulates it and streams its position.
            if (Net.NetRole.IsRemoteClient && TryGetComponent(out Rigidbody rb)) rb.isKinematic = true;
        }

        void OnDestroy() => Net.NetIds.Unregister(NetId, this);

        /// <summary>Cargo that rides the elevator is registered again on the new floor.</summary>
        public void RenewNetId()
        {
            Net.NetIds.Unregister(NetId, this);
            NetId = Net.NetIds.Register(this);
        }

        public HeavyLoot Loot => TryGetComponent(out HeavyLoot l) ? l : null;
        public string Label => !string.IsNullOrEmpty(label) ? label : (Loot ? Loot.displayName : name);
        /// <summary>Cash value (0 for plain items like mugs).</summary>
        public int Value => Loot ? Loot.CurrentValue : bankValue;

        // ------------------------------------------------------------------ finding pickups near a player
        // Asked every frame by the local player's backpack, so nothing here allocates: the buffers are shared (the game
        // is single threaded and none of these calls can reach another one).

        /// <summary>Room for every collider within reach. A crowded office floor puts well over 64 of them inside a 3 m ball.</summary>
        const int OverlapCapacity = 256;
        static readonly Collider[] overlap = new Collider[OverlapCapacity];
        static readonly HashSet<Pickup> seen = new HashSet<Pickup>();
        static readonly List<Pickup> found = new List<Pickup>(32);
        static readonly List<Collider> ownColliders = new List<Collider>(16);

        /// <summary>Nearest free pickup in front of / around <paramref name="who"/>, or null.</summary>
        public static Pickup FindNearest(Transform who, float radius = 2.2f)
        {
            ScanNear(who, radius, found);
            var pos = who.position;
            var forward = who.forward;
            Pickup best = null; float bestScore = float.MaxValue;
            for (int i = 0; i < found.Count; i++)
            {
                var p = found[i];
                float d = Vector3.Distance(pos, p.ClosestPointTo(pos));
                float facing = Vector3.Dot(forward, (p.transform.position - pos).normalized);
                float score = d - 0.6f * facing;        // close counts most, then whatever the player is facing
                if (score < bestScore) { bestScore = score; best = p; }
            }
            found.Clear();
            return best;
        }

        /// <summary>Fills <paramref name="into"/> (cleared first) with each free pickup within reach, each one once.</summary>
        public static void ScanNear(Transform who, float radius, List<Pickup> into) =>
            ScanAround(who.position + who.forward * 0.7f + Vector3.up * 0.2f, radius, into);

        /// <summary>Fills <paramref name="into"/> (cleared first) with each free pickup within <paramref name="radius"/> of a point, each one once.</summary>
        public static void ScanAround(Vector3 centre, float radius, List<Pickup> into)
        {
            into.Clear();
            int n = Physics.OverlapSphereNonAlloc(centre, radius, overlap, ~0, QueryTriggerInteraction.Ignore);
            seen.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = overlap[i].GetComponentInParent<Pickup>();
                if (!p || p.IsHeld || !p.isActiveAndEnabled || !seen.Add(p)) continue;
                var l = p.Loot;
                if (l && l.Holders > 0) continue; // someone is carrying it
                into.Add(p);
            }
            System.Array.Clear(overlap, 0, n);          // do not keep destroyed colliders alive
        }

        /// <summary>How far, in metres, the nearest point of this item's solid colliders is from <paramref name="pos"/>.</summary>
        public float DistanceFrom(Vector3 pos) => Vector3.Distance(pos, ClosestPointTo(pos));

        /// <summary>The world box around all of this item's solid colliders (what the crosshair aims at). False if it has none.</summary>
        public bool TryGetBounds(out Bounds box)
        {
            box = default;
            bool any = false;
            GetComponentsInChildren(ownColliders);
            for (int i = 0; i < ownColliders.Count; i++)
            {
                var c = ownColliders[i];
                if (c.isTrigger || !c.enabled) continue;
                if (!any) { box = c.bounds; any = true; }
                else box.Encapsulate(c.bounds);
            }
            ownColliders.Clear();
            return any;
        }

        /// <summary>The point of this item's solid colliders nearest to <paramref name="pos"/> (its centre when it has none).</summary>
        Vector3 ClosestPointTo(Vector3 pos)
        {
            var best = transform.position; float bd = float.MaxValue;
            GetComponentsInChildren(ownColliders);
            for (int i = 0; i < ownColliders.Count; i++)
            {
                var c = ownColliders[i];
                if (c.isTrigger) continue;
                var q = c.bounds.ClosestPoint(pos);
                float d = (q - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = q; }
            }
            ownColliders.Clear();
            return best;
        }
    }
}
