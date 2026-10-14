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
        public KeyCode grabKey = KeyCode.Q; // E now puts things in the backpack
        public KeyCode throwKey = KeyCode.Mouse0;

        public Rigidbody Held { get; private set; }
        HeavyLoot heldLoot;

        Pickup heldPickup;

        void Awake() { grabKey = KeyCode.Q; } // overrides the old value saved in the scene

        void Update()
        {
            if (Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open) return;
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
            Rigidbody rb = null;
            var p = Pickup.FindNearest(transform, reach);
            if (p) p.TryGetComponent(out rb);
            if (!rb)
            {
                var origin = transform.position + Vector3.up * 0.3f;
                if (!Physics.SphereCast(origin, 0.6f, transform.forward, out var hit, reach, grabbable)) return;
                rb = hit.rigidbody;
                if (!rb || rb.GetComponent<WobblyPlayer>()) return;
                p = rb.GetComponent<Pickup>();
            }
            Held = rb;
            heldPickup = p;
            if (heldPickup) heldPickup.IsHeld = true;
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
            if (heldPickup) heldPickup.IsHeld = heldLoot != null && heldLoot.Holders > 0;
            Held = null; heldLoot = null; heldPickup = null;
        }

        void OnDisable() { Release(false); }
    }
}
