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
        // (RaycastNonAlloc hands back the first hits it finds, not the nearest ones, once there are more colliders on the line than the array holds: across a long floor, through walls, desks and chairs, 48 was
        // not enough for a rifle's range and the guard at the end could be left out. 192 is more than any line of the building crosses.)
        static readonly RaycastHit[] hits = new RaycastHit[192];
        static readonly Collider[] inside = new Collider[16];

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
            if (!hitsPlayers)
            {
                // A ray never reports a collider it starts inside. The muzzle is only half a metre from the shooter and a guard's hitbox is a metre wide, so a guard who stands close (one who is pinning somebody,
                // say) had the muzzle INSIDE him and the bullet flew straight through (QA 2026-10-21, "the attack does not connect even though it definitely should"): such a bullet hits him where it starts.
                int k = Physics.OverlapSphereNonAlloc(origin, 0.02f, inside, ~0, QueryTriggerInteraction.Ignore);
                Impact atMuzzle = default;
                bool startedInside = false;
                for (int i = 0; i < k && !startedInside; i++)
                {
                    var c = inside[i];
                    if (!c || (shooter && c.transform.IsChildOf(shooter))) continue;
                    var g = c.GetComponentInParent<GuardAI>();
                    var d = g == null ? c.GetComponentInParent<TrainingDummy>() : null;
                    if ((g == null || g.Dead) && d == null) continue;
                    atMuzzle = new Impact { hit = true, point = origin, normal = -direction, distance = 0f, collider = c, guard = g, dummy = d };
                    startedInside = true;
                }
                System.Array.Clear(inside, 0, k);
                if (startedInside) return atMuzzle;
            }
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
                if (guard != null && guard.Dead) continue;                // a guard who is down for good lies on the floor: his standing hitbox must not stop a bullet meant for somebody behind him (like a player in custody)
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
