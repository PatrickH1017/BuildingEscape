using UnityEngine;

namespace BuildingEscape
{
    /// <summary>What a bullet hit.</summary>
    public struct Impact
    {
        public bool hit;
        public Vector3 point, normal;
        public float distance;
        public Collider collider;
        public GuardAI guard;
        public TrainingDummy dummy;
        public WobblyPlayer player;
        public Rigidbody body;

        public static Impact Miss(Vector3 end) => new Impact { hit = false, point = end, normal = Vector3.up };
    }

    /// <summary>
    /// Where a bullet goes: the first solid thing along a ray. The shooter's own body never counts, loose items lying about do not stop a
    /// bullet, and who else a bullet can hit depends on who fired it: a player's bullet passes through his teammates and hits guards and
    /// the training dummy; a guard's bullet passes through other guards and hits players. Walls, doors and furniture stop all of them.
    /// </summary>
    public static class Ballistics
    {
        static readonly RaycastHit[] hits = new RaycastHit[48];

        sealed class ByDistance : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly ByDistance Instance = new ByDistance();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        /// <param name="shooter">The body that fired (its colliders are skipped).</param>
        /// <param name="hitsPlayers">A guard's bullet: a player stops it (a free one; somebody already held down or arrested is passed through).</param>
        public static Impact Trace(Vector3 origin, Vector3 direction, float range, Transform shooter, bool hitsPlayers)
        {
            direction.Normalize();
            int n = Physics.RaycastNonAlloc(origin, direction, hits, range, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, 0, n, ByDistance.Instance);
            var result = Impact.Miss(origin + direction * range);
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                var c = h.collider;
                if (!c) continue;
                if (shooter && c.transform.IsChildOf(shooter)) continue;

                var player = c.GetComponentInParent<WobblyPlayer>();
                if (player != null)
                {
                    if (!hitsPlayers || player.InCustody) continue;       // teammates are not in the way; a body on the floor is passed over
                    result = Fill(h, c); result.player = player;
                    break;
                }
                var guard = c.GetComponentInParent<GuardAI>();
                if (guard != null && hitsPlayers) continue;               // a guard's bullet goes past his colleagues
                if (guard == null && c.GetComponentInParent<Pickup>() != null) continue;   // a mug on a desk does not stop a bullet

                result = Fill(h, c);
                result.guard = guard;
                result.dummy = c.GetComponentInParent<TrainingDummy>();
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic && guard == null) result.body = c.attachedRigidbody;
                break;
            }
            System.Array.Clear(hits, 0, n);
            return result;
        }

        static Impact Fill(RaycastHit h, Collider c) => new Impact { hit = true, point = h.point, normal = h.normal, distance = h.distance, collider = c };
    }
}
