using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    /// <summary>
    /// The officer drags an arrested player by the head to the guards' outpost (the owner's restraint, drag and rescue clips, 2026-10-28). The pin ran out and the player is Busted: the officer who held him down
    /// (<see cref="HeistManager.BustPlayer"/>) takes hold of his head and walks him to the outpost at <see cref="RestraintRules.GuardDragSpeed"/>. The captive struggles by holding a button (<see cref="WobblyPlayer.StruggleHeld"/>): the officer slows to
    /// <see cref="RestraintRules.StruggleSlow"/> of his speed, the captive's stamina runs down (<see cref="StruggleMeter"/>), and an officer who has been pulled against for <see cref="RestraintRules.ShoveAfterSeconds"/> in a row stops for
    /// <see cref="RestraintRules.ShoveStopSeconds"/> to shove him (the stamina is full again afterwards). A teammate can shoot or hit the officer: he lets go where he is (<see cref="DropCaptive"/>) and the captive lies there hogtied, for a teammate to free
    /// (<see cref="RestraintRules.FreeSeconds"/> holding the button). Host / offline decides all of it; every machine shows it from <see cref="WobblyPlayer.DraggedByGuard"/> and the struggle numbers the host publishes.
    /// </summary>
    public partial class GuardAI
    {
        /// <summary>The arrested player this officer drags by the head to the outpost, or null (host / offline; a puppet's is found through <see cref="WobblyPlayer.DraggedByGuard"/>).</summary>
        public WobblyPlayer Dragging { get; private set; }
        readonly StruggleMeter struggle = new StruggleMeter();
        float shoveUntil;
        Vector3 dragTo;
        /// <summary>The officer has stopped to shove the captive who struggles (for the tests).</summary>
        public bool DragShoving => Dragging != null && Time.time < shoveUntil;
        /// <summary>The captive's struggle stamina (host / offline; for the tests).</summary>
        public StruggleMeter Struggle => struggle;
        /// <summary>How many times he has shoved a captive (for the tests and the other machines).</summary>
        public int Shoves { get; private set; }
        /// <summary>Where he is taking the captive (for the tests).</summary>
        public Vector3 DragTarget => dragTo;
        /// <summary>How many captives he has brought to the outpost (for the tests).</summary>
        public int Delivered { get; private set; }
        /// <summary>How many captives he has had to let go of because he was hit (for the tests).</summary>
        public int Dropped { get; private set; }

        /// <summary>Where an officer takes a captive: the middle of the guards' outpost on this floor (a point of the floor plan, on the navigation mesh); false when the floor has none (the captive is left lying).</summary>
        bool DragDestination(out Vector3 to)
        {
            to = default;
            var gen = FindFirstObjectByType<FloorGenerator>();
            if (!gen || !gen.TryGetOutpostRoom(out var room)) return false;
            var from = transform.position;
            if (!NavMesh.SamplePosition(new Vector3(room.Cx, from.y, room.Cz), out var hit, 8f, NavMesh.AllAreas)) return false;
            to = hit.position;
            return true;
        }

        /// <summary>
        /// The pin ran out and the player is arrested: the officer who held him down drags him by the head to the outpost (host / offline). False when he cannot (he is a puppet, down, stunned, the floor has no outpost,
        /// the captive is dragged by somebody already): the officer then lets go of him as before.
        /// </summary>
        public bool BeginDrag(WobblyPlayer p)
        {
            if (IsPuppet || Dead || !p || !agent || !agent.isOnNavMesh || State == GuardState.Stunned) return false;
            if (p.Custody != CustodyState.Busted || p.DraggedByGuard != null || p.DraggedBy != null) return false;
            if (!DragDestination(out var to)) return false;
            holding = null;
            Dragging = p; p.DraggedByGuard = this; p.PinnedBy = null;
            struggle.Reset();
            var np = p.GetComponent<Net.NetPlayer>();
            if (np && np.IsSpawned) np.HostSetDragger(this);
            PublishStruggle(p);
            dragTo = to;
            State = GuardState.Dragging;
            calling = false; callTimer = 0f; target = null;
            agent.isStopped = false; agent.speed = RestraintRules.GuardSpeed(false);
            agent.SetDestination(to);
            Debug.Log($"{name} drags {p.name} to the outpost");
            return true;
        }

        void PublishStruggle(WobblyPlayer p)
        {
            p.StruggleActive = struggle.Active; p.StruggleStamina = struggle.Stamina;
            var np = p.GetComponent<Net.NetPlayer>();
            if (np && np.IsSpawned) np.HostSetStruggle(struggle.Active, struggle.Stamina, p.ShoveCount);
        }

        void UpdateDragging()
        {
            var p = Dragging;
            if (!p || p.Custody != CustodyState.Busted || p.DraggedByGuard != this) { EndDrag(false); return; }
            bool shoving = Time.time < shoveUntil;
            struggle.Step(Time.deltaTime, p.StruggleHeld && !shoving);
            if (!shoving && struggle.ShoveDue)
            {
                struggle.Shoved();
                shoveUntil = Time.time + RestraintRules.ShoveStopSeconds;
                Shoves++; p.ShoveCount++;                       // (the player's own count only goes up: a machine that sees it change knows he was shoved)
                shoving = true;
            }
            agent.isStopped = shoving;
            agent.speed = RestraintRules.GuardSpeed(struggle.Active);
            PublishStruggle(p);
            var left = dragTo - transform.position; left.y = 0f;
            bool there = left.magnitude < RestraintRules.DragArrive;
            bool stuck = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.3f && agent.velocity.sqrMagnitude < 0.01f;          // (a locked door or a wall: he cannot get any further)
            if (!shoving && (there || stuck)) EndDrag(there);
        }

        /// <summary>He lets go of the captive (he arrived, or cannot go on, or the captive is gone) and goes back to his round.</summary>
        void EndDrag(bool delivered)
        {
            var p = Dragging;
            Dragging = null;
            if (p)
            {
                if (p.DraggedByGuard == this) p.DraggedByGuard = null;
                p.StruggleActive = false; p.StruggleStamina = 1f;
                var np = p.GetComponent<Net.NetPlayer>();
                if (np && np.IsSpawned) { np.HostSetDragger(null); np.HostSetStruggle(false, 1f, p.ShoveCount); }
                if (delivered) Delivered++;
            }
            struggle.Reset();
            if (State == GuardState.Dragging && !Dead)
            {
                State = GuardState.Patrol; waitUntil = 0f;
                agent.isStopped = false; agent.speed = walkSpeed;
                GoNextPatrol();
            }
        }

        /// <summary>He is hit, stunned or knocked out while he drags a captive (<see cref="ReleaseHold"/>): the captive is let go where he is and lies there hogtied (the drop is shown on every machine, see <see cref="WobblyPlayer.DropCount"/>).</summary>
        void DropCaptive()
        {
            var p = Dragging;
            if (p)
            {
                Dropped++;
                p.NoteDrop(this, p.DropCount + 1);
                var np = p.GetComponent<Net.NetPlayer>();
                if (np && np.IsSpawned) np.HostSetDrop(this, p.DropCount);
            }
            EndDrag(false);
        }
    }
}
