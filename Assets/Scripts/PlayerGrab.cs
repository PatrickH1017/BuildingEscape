using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Grab/carry. Light items follow the hand; HeavyLoot only moves when enough
    /// players hold it, and then it is pulled toward the average of the holders' hand points.
    /// </summary>
    public class PlayerGrab : MonoBehaviour
    {
        public Transform hand;
        public float reach = 2.2f;
        public LayerMask grabbable = ~0;
        public float carryForce = 40f;
        public float throwForce = 9f;
        public KeyCode grabKey = KeyCode.E;
        public KeyCode throwKey = KeyCode.Mouse0;

        public Rigidbody Held { get; private set; }
        HeavyLoot heldLoot;

        void Update()
        {
            if (Input.GetKeyDown(grabKey)) { if (Held) Release(false); else TryGrab(); }
            if (Held && Input.GetKeyDown(throwKey)) Release(true);
        }

        void FixedUpdate()
        {
            if (!Held) return;
            if (heldLoot && !heldLoot.IsLiftable)
            {
                // Not enough hands: you just drag your feet. Slow the owner down instead.
                return;
            }
            var target = hand ? hand.position : transform.position + transform.forward;
            Vector3 toTarget = target - Held.position;
            Held.AddForce(toTarget * carryForce - Held.linearVelocity * 4f, ForceMode.Acceleration);
        }

        void TryGrab()
        {
            var origin = hand ? hand.position : transform.position + Vector3.up;
            if (!Physics.SphereCast(origin, 0.4f, transform.forward, out var hit, reach, grabbable)) return;
            var rb = hit.rigidbody;
            if (!rb) return;
            Held = rb;
            heldLoot = rb.GetComponent<HeavyLoot>();
            heldLoot?.AddHolder(this);
        }

        public void Release(bool thrown)
        {
            if (!Held) return;
            heldLoot?.RemoveHolder(this);
            if (thrown && (heldLoot == null || heldLoot.Holders == 0))
            {
                var t = Held.GetComponent<Throwable>();
                Held.AddForce((transform.forward + Vector3.up * 0.25f) * throwForce * (t ? t.throwMultiplier : 1f),
                              ForceMode.VelocityChange);
                t?.OnThrown(gameObject);
            }
            Held = null; heldLoot = null;
        }

        void OnDisable() { Release(false); }
    }
}
