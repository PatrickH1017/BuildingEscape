using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Grab/carry. Light items follow the hand; HeavyLoot only moves when enough
    /// players hold it, and then it is pulled toward the average of the holders' hand points.
    /// In a networked run the host decides who holds what (RunSync) and moves the object; every machine
    /// applies the same grab / release so the carry state matches everywhere.
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
            if (!Net.NetPlayer.IsLocalPlayer(GetComponent<WobblyPlayer>())) return; // teammates are driven by their own machine
            if (Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open) return;
            if (Input.GetKeyDown(grabKey)) { if (Held) AskRelease(false); else TryGrab(); }
            if (Held && Input.GetKeyDown(throwKey)) AskRelease(true);
        }

        void FixedUpdate()
        {
            if (!Held) return;
            if (Net.NetRole.IsRemoteClient) return; // the host moves what is carried
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
            if (Net.NetRole.Synced)
            {
                if (p) Net.RunSync.Instance.RequestGrab(p); // the host checks and tells everyone
                return;
            }
            Attach(rb, p);
        }

        void Attach(Rigidbody rb, Pickup p)
        {
            Held = rb;
            heldPickup = p;
            if (heldPickup) heldPickup.IsHeld = true;
            heldLoot = rb.GetComponent<HeavyLoot>();
            heldLoot?.AddHolder(this);
        }

        /// <summary>The host said this player now holds the item (every machine applies it).</summary>
        public void ApplyGrab(Pickup p)
        {
            if (!p || Held || !p.TryGetComponent(out Rigidbody rb)) return;
            Attach(rb, p);
        }

        void AskRelease(bool thrown)
        {
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestRelease(thrown);
            else Release(thrown);
        }

        /// <summary>Where the throw goes (the host calculates it from the player's facing).</summary>
        public Vector3 ThrowImpulse()
        {
            var t = Held ? Held.GetComponent<Throwable>() : null;
            return (transform.forward + Vector3.up * 0.25f) * throwForce * (t ? t.throwMultiplier : 1f);
        }

        /// <summary>Let go now, on this machine only (offline play, or tidy-up when the player goes away).</summary>
        public void Release(bool thrown)
        {
            if (!Held) return;
            ApplyRelease(thrown, thrown ? ThrowImpulse() : Vector3.zero);
        }

        /// <summary>The host said this player let go (and with what impulse, if thrown). Every machine applies it.</summary>
        public void ApplyRelease(bool thrown, Vector3 impulse)
        {
            if (!Held) return;
            heldLoot?.RemoveHolder(this);
            if (thrown && (heldLoot == null || heldLoot.Holders == 0))
            {
                if (Net.NetRole.Simulates) Held.AddForce(impulse, ForceMode.VelocityChange);
                Held.GetComponent<Throwable>()?.OnThrown(gameObject);
            }
            if (heldPickup) heldPickup.IsHeld = heldLoot != null && heldLoot.Holders > 0;
            Held = null; heldLoot = null; heldPickup = null;
        }

        void OnDisable() { Release(false); }
    }
}