using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    /// <summary>
    /// The body of an eliminated guard can be taken hold of with the grab key (Q) and dragged along behind the player, to hide it (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)"; the guards
    /// look for bodies: <see cref="GuardAI"/> ScanForBodies). Added by <see cref="GuardAI"/> to every guard that is down for good, on every machine, next to <see cref="GuardCorpse"/>. Only the machine that decides (the host, or an offline game) moves the
    /// body; the others see it move through the guard snapshots, and are told who drags it (<c>RunSync.BodyDragRpc</c>) so that the dragger walks slower and the body does not sink away under his hands (<see cref="GuardCorpse"/> waits while a body is dragged).
    /// Let go with Q again, or by being too far from it (<see cref="DraggableBodyRules.LetGoDistance"/>), pinned, busted or gone. The body is held behind the player and kept on the floor plan: every step is cut short where the NavMesh ends
    /// (a wall), so a body is never pulled through one.
    /// </summary>
    public sealed class DraggableBody : MonoBehaviour
    {
        /// <summary>Every body that can be dragged on this machine (for the tests and the search).</summary>
        public static readonly List<DraggableBody> All = new List<DraggableBody>();

        GuardAI guard;
        GuardCorpse corpse;
        float heightOverFloor;

        /// <summary>The guard whose body this is.</summary>
        public GuardAI Guard => guard;

        /// <summary>The player who drags it, or null.</summary>
        public WobblyPlayer Dragger { get; private set; }

        public bool IsDragged => Dragger != null;

        /// <summary>Can somebody take hold of it now: nobody drags it, it is not the boss's, and it has not begun to sink away?</summary>
        public bool CanBeDragged
        {
            get
            {
                if (IsDragged || guard == null || guard.IsBoss) return false;
                if (corpse == null) corpse = GetComponent<GuardCorpse>();
                return corpse == null || !corpse.Sinking;
            }
        }

        void Awake() { guard = GetComponent<GuardAI>(); }
        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); End(); }

        /// <summary>The nearest body within reach (flat distance, <see cref="DraggableBodyRules.GrabReach"/>) in front of or beside the player, that nobody drags, or null.</summary>
        public static DraggableBody Nearest(Transform who, float reach)
        {
            DraggableBody best = null; float bestScore = float.MaxValue;
            for (int i = 0; i < All.Count; i++)
            {
                var b = All[i];
                if (!b || !b.CanBeDragged) continue;
                var d = b.transform.position - who.position; d.y = 0f;
                if (!DraggableBodyRules.CanGrab(d.magnitude, false) || d.magnitude > reach) continue;
                float facing = d.sqrMagnitude > 1e-4f ? Vector3.Dot(who.forward, d.normalized) : 1f;
                float score = d.magnitude - 0.6f * facing;                          // (close counts most, then what he faces: as for items)
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }

        /// <summary>The body is taken hold of by this player (every machine applies it, the host's or an offline game's before the others).</summary>
        public void Begin(WobblyPlayer by)
        {
            if (!by || IsDragged) return;
            Dragger = by;
            by.DragFactor = DraggableBodyRules.DraggerSpeedShare;
            if (Net.NetRole.Simulates)
            {
                var agent = GetComponent<NavMeshAgent>();
                if (agent) agent.enabled = false;                                    // (an agent would pull the body back to where it thinks it is)
                heightOverFloor = FloorOffset(transform.position);
            }
        }

        /// <summary>The body is let go (every machine applies it).</summary>
        public void End()
        {
            if (Dragger) Dragger.DragFactor = 1f;
            Dragger = null;
        }

        /// <summary>How high the body's middle is over the floor plan under it (m).</summary>
        static float FloorOffset(Vector3 p) => NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas) ? p.y - hit.position.y : 0f;

        void Update()
        {
            if (Dragger == null) return;
            if (!Dragger.isActiveAndEnabled || Dragger.InCustody) { End(); return; }
            if (!Net.NetRole.Simulates) return;                                      // (a puppet: the host's snapshots move it)
            var here = transform.position; var player = Dragger.transform.position;
            var gap = player - here; gap.y = 0f;
            if (DraggableBodyRules.MustLetGo(gap.magnitude)) { Net.RunSync.Instance?.HostEndDrag(this); if (Dragger) End(); return; }
            var fwd = Dragger.transform.forward;
            DraggableBodyRules.HoldSpot(player.x, player.z, fwd.x, fwd.z, out float tx, out float tz);
            DraggableBodyRules.Step(here.x, here.z, tx, tz, Time.deltaTime, out float nx, out float nz);
            // stay on the floor plan: a step that would cross an edge of it (a wall) stops there
            if (NavMesh.SamplePosition(new Vector3(here.x, here.y, here.z), out var onMesh, 3f, NavMesh.AllAreas))
            {
                var to = new Vector3(nx, onMesh.position.y, nz);
                if (NavMesh.Raycast(onMesh.position, to, out var edge, NavMesh.AllAreas)) to = new Vector3(edge.position.x, onMesh.position.y, edge.position.z);
                if (NavMesh.SamplePosition(to, out var ok, 1f, NavMesh.AllAreas)) to = new Vector3(to.x, ok.position.y, to.z);
                transform.position = new Vector3(to.x, to.y + heightOverFloor, to.z);
            }
            // lie with the head away from the dragger: the body trails behind him along the line he pulls it
            var pull = player - transform.position; pull.y = 0f;
            if (pull.sqrMagnitude > 0.04f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-pull.normalized), 6f * Time.deltaTime);
        }
    }
}
