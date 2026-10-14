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

        public HeavyLoot Loot => TryGetComponent(out HeavyLoot l) ? l : null;
        public string Label => !string.IsNullOrEmpty(label) ? label : (Loot ? Loot.displayName : name);
        /// <summary>Cash value (0 for plain items like mugs).</summary>
        public int Value => Loot ? Loot.CurrentValue : bankValue;

        static readonly Collider[] buf = new Collider[64];

        /// <summary>Nearest free pickup in front of / around <paramref name="who"/>, or null.</summary>
        public static Pickup FindNearest(Transform who, float radius = 2.2f)
        {
            Pickup best = null; float bestScore = float.MaxValue;
            ScanNear(who, radius, p =>
            {
                float d = Vector3.Distance(who.position, p.ClosestPointTo(who.position));
                float facing = Vector3.Dot(who.forward, (p.transform.position - who.position).normalized);
                float score = d - 0.6f * facing;
                if (score < bestScore) { bestScore = score; best = p; }
            });
            return best;
        }

        /// <summary>Calls <paramref name="visit"/> once for each free pickup within reach.</summary>
        public static void ScanNear(Transform who, float radius, System.Action<Pickup> visit)
        {
            var centre = who.position + who.forward * 0.7f + Vector3.up * 0.2f;
            int n = Physics.OverlapSphereNonAlloc(centre, radius, buf, ~0, QueryTriggerInteraction.Ignore);
            var seen = new System.Collections.Generic.HashSet<Pickup>();
            for (int i = 0; i < n; i++)
            {
                var p = buf[i].GetComponentInParent<Pickup>();
                if (!p || p.IsHeld || !p.isActiveAndEnabled || !seen.Add(p)) continue;
                var l = p.Loot;
                if (l && l.Holders > 0) continue; // someone is carrying it
                visit(p);
            }
        }

        Vector3 ClosestPointTo(Vector3 pos)
        {
            var best = transform.position; float bd = float.MaxValue;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger) continue;
                var q = c.bounds.ClosestPoint(pos);
                float d = (q - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = q; }
            }
            return best;
        }
    }
}
