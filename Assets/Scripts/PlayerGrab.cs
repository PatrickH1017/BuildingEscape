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
        [Tooltip("How hard a carried thing is held back from overshooting its hand (the spring is damped about as much as it can be without being sluggish).")]
        public float carryDamping = 12f;
        /// <summary>The most a carried thing is accelerated towards its hand (m/s^2): a far-off piece is not shot at its carrier.</summary>
        public const float MaxCarryAcceleration = 70f;
        public float throwForce = 9f;
        public KeyCode grabKey = KeyCode.Q; // E now puts things in the backpack
        public KeyCode throwKey = KeyCode.Mouse0;

        public Rigidbody Held { get; private set; }
        HeavyLoot heldLoot;
        Vector3 lastTarget;
        bool haveLastTarget;

        /// <summary>Where this player holds what they carry: the hand point in front of them.</summary>
        public Vector3 HandPoint => hand ? hand.position : transform.position + transform.forward;

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
            UpkeepPlayerDrag();
            if (!Net.NetPlayer.IsLocalPlayer(wob)) return; // teammates are driven by their own machine
            if (Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || ElevatorPanel.Open || OutpostUi.Open) return;
            if (wob.InCustody) return; // pinned or busted
            if (Input.GetKeyDown(grabKey)) { if (Held) AskRelease(false); else if (IsDragging) AskDropBody(); else TryGrab(); }
            if (Held && Input.GetKeyDown(throwKey)) AskRelease(true);
        }

        void FixedUpdate()
        {
            if (!Held) { haveLastTarget = false; return; }
            if (Net.NetRole.IsRemoteClient) return; // the host moves what is carried
            if (heldLoot && !heldLoot.IsLiftable)
            {
                // Not enough hands: you just drag your feet. It stays where it is (and falls if it was lifted).
                Held.useGravity = true;
                haveLastTarget = false;
                return;
            }
            // A spring to the hand that also follows the hand's own motion (so a piece does not trail behind a walking carrier or swing like a
            // pendulum) and is damped about as much as it can be without being sluggish. Nothing pulls it down while it is carried (it would sag
            // below the hand by the spring's stretch). Two or three carriers add up: the piece goes to the middle of their hands.
            var target = HandPoint;
            var targetVelocity = haveLastTarget ? Vector3.ClampMagnitude((target - lastTarget) / Time.fixedDeltaTime, 8f) : Vector3.zero;
            lastTarget = target; haveLastTarget = true;
            Held.useGravity = false;
            var acceleration = (target - Held.position) * carryForce + (targetVelocity - Held.linearVelocity) * carryDamping;
            Held.AddForce(Vector3.ClampMagnitude(acceleration, MaxCarryAcceleration), ForceMode.Acceleration);
        }

        void TryGrab()
        {
            Rigidbody rb = null;
            var aimed = Crosshair.Aim;                       // what the crosshair is on first, then whatever is nearest
            var p = aimed.Kind == AimKind.Pickup ? aimed.Pickup : Pickup.FindNearest(transform, reach);
            if (p) p.TryGetComponent(out rb);
            if (!rb && !p && TryGrabBody()) return;                     // (nothing to pick up: perhaps a body to drag)
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
            Held.useGravity = true;
            if (heldLoot) heldLoot.RemoveHolder(this, thrown);
            if (thrown && (!heldLoot || heldLoot.Holders == 0))
            {
                if (Net.NetRole.Simulates) Held.AddForce(impulse, ForceMode.VelocityChange);
                if (Held.TryGetComponent(out Throwable thrownItem)) thrownItem.OnThrown(gameObject);
            }
            if (heldPickup) heldPickup.IsHeld = heldLoot && heldLoot.Holders > 0;
            Held = null; heldLoot = null; heldPickup = null;
        }

        // ------------------------------------------------------------------ dragging a body (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)")

        DraggableBody dragging;

        /// <summary>The body of an eliminated guard this player drags, or null (<see cref="DraggableBody"/>).</summary>
        public DraggableBody Dragging => dragging != null && wob != null && dragging.Dragger == wob ? dragging : null;

        /// <summary>The grab key with nothing to pick up: take hold of the nearest body in reach, if there is one (a networked run asks the host). True when there was one.</summary>
        public bool TryGrabBody()
        {
            if (!wob) TryGetComponent(out wob);
            if (!wob || wob.InCustody || Held || IsDragging) return false;
            var body = DraggableBody.Nearest(transform, reach);
            if (body != null)
            {
                if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDragBody(body.Guard);
                else ApplyDragBody(body);
                return true;
            }
            var friend = BustedBodyNear();                               // (no guard's body in reach: perhaps a busted teammate's)
            if (friend == null) return false;
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDragPlayerBody(friend);
            else ApplyDragPlayerBody(friend);
            return true;
        }

        WobblyPlayer draggingPlayer;

        /// <summary>The busted teammate whose body this player drags, or null (<see cref="WobblyPlayer.DraggedBy"/>).</summary>
        public WobblyPlayer DraggingPlayer => draggingPlayer != null && wob != null && draggingPlayer.DraggedBy == wob ? draggingPlayer : null;

        /// <summary>Does this player drag a body now (a guard's or a teammate's)?</summary>
        public bool IsDragging => Dragging != null || DraggingPlayer != null;

        /// <summary>The nearest busted teammate within reach (flat distance, <see cref="DraggableBodyRules.GrabReach"/>) whom nobody drags yet, or null.</summary>
        WobblyPlayer BustedBodyNear()
        {
            WobblyPlayer best = null; float bestD = float.MaxValue;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p || p == wob || p.Custody != CustodyState.Busted || p.DraggedBy != null || p.DraggedByGuard != null) continue;
                var d = p.transform.position - transform.position; d.y = 0f;
                if (!DraggableBodyRules.CanGrab(d.magnitude, false) || d.magnitude >= bestD) continue;
                best = p; bestD = d.magnitude;
            }
            return best;
        }

        /// <summary>This player now drags the body of this busted teammate (offline: at once; a networked run: the host said so, every machine applies it).</summary>
        public void ApplyDragPlayerBody(WobblyPlayer friend)
        {
            if (!friend || friend == wob || IsDragging) return;
            if (!wob) TryGetComponent(out wob);
            if (!wob) return;
            draggingPlayer = friend;
            friend.DraggedBy = wob;
            wob.DragFactor = DraggableBodyRules.DraggerSpeedShare;
        }

        /// <summary>Every frame, on every machine: a teammate who is not busted any more (bought back) is let go of; on the dragger's own machine, one who is too far away is too.</summary>
        void UpkeepPlayerDrag()
        {
            if (draggingPlayer == null) return;
            if (!draggingPlayer || draggingPlayer.Custody != CustodyState.Busted || wob.InCustody) { ApplyDropBody(); return; }
            if (!Net.NetPlayer.IsLocalPlayer(wob)) return;
            var d = draggingPlayer.transform.position - transform.position; d.y = 0f;
            if (DraggableBodyRules.MustLetGo(d.magnitude)) AskDropBody();
        }

        void AskDropBody()
        {
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestDropBody();
            else ApplyDropBody();
        }

        /// <summary>This player now drags the body (offline: at once; a networked run: the host said so, every machine applies it).</summary>
        public void ApplyDragBody(DraggableBody body)
        {
            if (!body || Dragging != null) return;
            if (!wob) TryGetComponent(out wob);
            if (!wob) return;
            dragging = body;
            body.Begin(wob);
        }

        /// <summary>This player lets go of the body he drags.</summary>
        public void ApplyDropBody()
        {
            if (dragging != null) dragging.End();
            dragging = null;
            if (draggingPlayer != null)
            {
                if (draggingPlayer && draggingPlayer.DraggedBy == wob) draggingPlayer.DraggedBy = null;
                draggingPlayer = null;
                if (wob) wob.DragFactor = 1f;
            }
        }

        void OnDisable() { Release(false); ApplyDropBody(); }
    }
}
