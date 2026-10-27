using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The empty magazine a pistol reload drops (the dock worker's Blender reload, the owner, 2026-10-27): a copy of the gun's magazine that leaves the grip, tumbles, lies flat on the floor and is gone a few seconds later.
    /// Only for the picture: no collider, no network id, nothing can pick it up (every machine that shows the reload drops its own).
    /// </summary>
    public sealed class DroppedMagazine : MonoBehaviour
    {
        // as the Blender file drops it: out of the grip at 0.8 m/s, tumbling at 7 rad/s once clear of it
        const float Speed = 0.8f, Spin = 7f, SpinAfter = 0.08f, Gravity = 9.81f, LieFor = 6f, MaxDrop = 4f;
        static readonly RaycastHit[] hits = new RaycastHit[8];
        Vector3 velocity, spinAxis;
        Transform owner;
        float age, halfThick;
        bool landed;

        /// <summary>Drops a copy of <paramref name="magazine"/> where it is, out along <paramref name="eject"/>, tumbling about <paramref name="spinAxis"/>; the body holding the gun (the root of the magazine) is not floor to it.</summary>
        public static void Drop(Transform magazine, Vector3 eject, Vector3 spinAxis)
        {
            if (!magazine) return;
            var copy = Instantiate(magazine.gameObject, magazine.position, magazine.rotation);
            copy.name = "DroppedMagazine";
            copy.transform.localScale = magazine.lossyScale;
            copy.SetActive(true);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var d = copy.AddComponent<DroppedMagazine>();
            d.velocity = eject * Speed;
            d.spinAxis = spinAxis.sqrMagnitude > 1e-4f ? spinAxis.normalized : Vector3.right;
            d.owner = magazine.root;
            var mf = copy.GetComponent<MeshFilter>();
            d.halfThick = mf && mf.sharedMesh ? mf.sharedMesh.bounds.extents.x * copy.transform.lossyScale.x : 0.01f;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age > LieFor) { Destroy(gameObject); return; }
            if (landed) return;
            float dt = Time.deltaTime;
            velocity += Vector3.down * (Gravity * dt);
            var step = velocity * dt;
            // (the floor is looked for from where the magazine is, before the step: a ray that starts inside the floor would not see it, and a fast step would go through it)
            if (Floor(out float floorY) && transform.position.y + step.y - halfThick <= floorY) { transform.position += new Vector3(step.x, 0f, step.z); Land(floorY); return; }
            transform.position += step;
            if (age > SpinAfter) transform.rotation = Quaternion.AngleAxis(Spin * Mathf.Rad2Deg * dt, spinAxis) * transform.rotation;
        }

        /// <summary>The height of the first solid thing under the magazine that is not the body that dropped it.</summary>
        bool Floor(out float y)
        {
            y = 0f;
            int n = Physics.RaycastNonAlloc(transform.position + Vector3.up * 0.05f, Vector3.down, hits, MaxDrop, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (owner && hits[i].transform.IsChildOf(owner)) continue;
                if (hits[i].distance < best) { best = hits[i].distance; y = hits[i].point.y; }
            }
            return best < float.MaxValue;
        }

        /// <summary>On the floor: on its flat side (its thin axis up), its long way kept.</summary>
        void Land(float floorY)
        {
            landed = true;
            var thin = transform.right;
            transform.rotation = Quaternion.FromToRotation(thin, Vector3.Dot(thin, Vector3.up) >= 0f ? Vector3.up : Vector3.down) * transform.rotation;
            var p = transform.position;
            transform.position = new Vector3(p.x, floorY + halfThick, p.z);
        }
    }
}
