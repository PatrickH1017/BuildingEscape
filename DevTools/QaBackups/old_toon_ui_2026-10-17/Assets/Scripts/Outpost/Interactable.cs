using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A thing in the world that E works when the crosshair is on it and the player is close enough: the keypad beside the guard
    /// outpost's door, a floor safe, a note on a desk. <see cref="Crosshair"/> asks <see cref="Aimed"/> which one the player is pointing
    /// at (right next to items, doors and the dealer), shows <see cref="Prompt"/> under the reticle and calls <see cref="Use"/> when E is
    /// pressed. These things never move, so the box the crosshair aims at is worked out once by whoever builds them.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>How close (metres from the player's body to the nearest point of the box) a player has to be to work it.</summary>
        public const float DefaultReach = 2.4f;
        /// <summary>The box counts a hair bigger than it is, so a small keypad or a note is not hard to hit with a thin ray.</summary>
        public const float AimPad = 0.08f;

        static readonly List<Interactable> all = new List<Interactable>();

        /// <summary>Everything that can be worked on the floor that is on show.</summary>
        public static IReadOnlyList<Interactable> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { all.Clear(); }

        /// <summary>How close a player has to be (see <see cref="DefaultReach"/>).</summary>
        public float reach = DefaultReach;

        /// <summary>The world box the crosshair aims at.</summary>
        public Bounds AimBox { get; protected set; }

        protected virtual void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        protected virtual void OnDisable() { all.Remove(this); }

        /// <summary>Can it be worked right now? An open safe cannot (what is inside it is picked up instead), nor can a lock that is open.</summary>
        public virtual bool Available => true;

        /// <summary>What the prompt under the reticle says ("[E] Hack the lock").</summary>
        public abstract string Prompt { get; }

        /// <summary>The player pressed E on it (already checked: close enough, crosshair on it, not in custody, no screen up).</summary>
        public abstract void Use(WobblyPlayer who);

        /// <summary>How far the nearest point of the box is from a position.</summary>
        public float DistanceFrom(Vector3 position) => Mathf.Sqrt(AimBox.SqrDistance(position));

        /// <summary>
        /// The thing a ray points at: the one that is available, within reach of the player at <paramref name="body"/>, and whose box the
        /// ray enters first within <paramref name="maxT"/> metres. <paramref name="enter"/> is where along the ray it goes in. A thing
        /// that is aimed at but too far away is not returned; <paramref name="farEnter"/> says where the ray meets the nearest such thing
        /// (infinity if none). The caller checks walls in the way.
        /// </summary>
        public static Interactable Aimed(Vector3 origin, Vector3 direction, float maxT, Vector3 body, out float enter, out float farEnter)
        {
            enter = farEnter = float.PositiveInfinity;
            Interactable best = null;
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (!a || !a.Available) continue;
                var b = a.AimBox;
                if (!AimMath.RayBox(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z,
                                    b.min.x - AimPad, b.min.y - AimPad, b.min.z - AimPad, b.max.x + AimPad, b.max.y + AimPad, b.max.z + AimPad,
                                    maxT, out float t, out _)) continue;
                if (a.DistanceFrom(body) > a.reach) { if (t < farEnter) farEnter = t; continue; }
                if (t < enter) { enter = t; best = a; }
            }
            return best;
        }

        /// <summary>Sets the aim box from the renderers and colliders under this object (the part that is solid or seen).</summary>
        protected void FitAimBoxToChildren()
        {
            bool any = false;
            Bounds box = default;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }
            AimBox = any ? box : new Bounds(transform.position, Vector3.one * 0.3f);
        }
    }
}
