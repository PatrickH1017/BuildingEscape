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
        WobblyPlayer wob;

        void Awake()
        {
            grabKey = KeyCode.Q; // overrides the old value saved in the scene
            TryGetComponent(out wob);
        }

        void Update()
        {
            if (!wob && !TryGetComponent(out wob)) return;
            if (!Net.NetPlayer.IsLocalPlayer(wob)) return; // teammates are driven by their own machine
            if (Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || ElevatorPanel.Open || OutpostUi.Open) return;
            if (wob.InCustody) return; // pinned or busted
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
            var aimed = Crosshair.Aim;                       // what the crosshair is on first, then whatever is nearest
            var p = aimed.Kind == AimKind.Pickup ? aimed.Pickup : Pickup.FindNearest(transform, reach);
            if (p) p.TryGetComponent(out rb);
            if (!rb)
            {
                var origin = transform.position + Vector3.up * 0.3f;
                if (!Physics.SphereCast(origin, 0.6f, transform.forward, out var hit, reach, grabbable)) return;
                rb = hit.rigidbody;
                if (!rb || rb.TryGetComponent<WobblyPlayer>(out _)) return;      // never pick up another player
                rb.TryGetComponent(out p);
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
            heldLoot = rb.TryGetComponent(out HeavyLoot loot) ? loot : null;
            if (heldLoot) heldLoot.AddHolder(this);
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
            float multiplier = Held && Held.TryGetComponent(out Throwable t) ? t.throwMultiplier : 1f;
            return (transform.forward + Vector3.up * 0.25f) * throwForce * multiplier;
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
            if (heldLoot) heldLoot.RemoveHolder(this);
            if (thrown && (!heldLoot || heldLoot.Holders == 0))
            {
                if (Net.NetRole.Simulates) Held.AddForce(impulse, ForceMode.VelocityChange);
                if (Held.TryGetComponent(out Throwable thrownItem)) thrownItem.OnThrown(gameObject);
            }
            if (heldPickup) heldPickup.IsHeld = heldLoot && heldLoot.Holders > 0;
            Held = null; heldLoot = null; heldPickup = null;
        }

        void OnDisable() { Release(false); }
    }
}
