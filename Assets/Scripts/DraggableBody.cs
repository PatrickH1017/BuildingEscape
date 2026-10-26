using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    /// <summary>
    /// The body of an eliminated guard can be taken hold of with the grab key (Q) and dragged along behind the player, to hide it (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)"; the guards
    /// look for bodies: <see cref="GuardAI"/> ScanForBodies). Added by <see cref="GuardAI"/> to every guard that is down for good, on every machine, next to <see cref="GuardCorpse"/>. Only the machine that decides (the host, or an offline game) moves the
    /// body; the others see it move through the guard snapshots, and are told who drags it (<c>RunSync.BodyDragRpc</c>) so that the dragger walks slower and the body does not sink away under his hands (<see cref="GuardCorpse"/> waits while a body is dragged).
    /// Let go with Q again, or by being too far from it (<see cref="DraggableBodyRules.LetGoDistance"/>), pinned, busted or gone. The body follows the path the player walked (DraggableBodyRules.Trail), so it goes through every doorway and round every corner he did,
    /// and is kept on the floor plan under its new place.
    /// </summary>
    public sealed class DraggableBody : MonoBehaviour
    {
        /// <summary>Every body that can be dragged on this machine (for the tests and the search).</summary>
        public static readonly List<DraggableBody> All = new List<DraggableBody>();

        GuardAI guard;
        GuardCorpse corpse;
        float heightOverFloor;
        readonly DraggableBodyRules.Trail trail = new DraggableBodyRules.Trail();

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
                trail.Begin(transform.position.x, transform.position.z, by.transform.position.x, by.transform.position.z);
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
            if (!Dragger.gameObject.activeInHierarchy || Dragger.InCustody) { End(); return; }          // (not isActiveAndEnabled: the WobblyPlayer of a player on another machine is switched off here, NetPlayer.OnNetworkSpawn)
            if (!Net.NetRole.Simulates) return;                                      // (a puppet: the host's snapshots move it)
            var here = transform.position; var player = Dragger.transform.position;
            var gap = player - here; gap.y = 0f;
            if (DraggableBodyRules.MustLetGo(gap.magnitude)) { Net.RunSync.Instance?.HostEndDrag(this); if (Dragger) End(); return; }
            trail.Add(player.x, player.z);                                           // (the body follows his steps: through every doorway and round every corner he went)
            trail.Advance(Time.deltaTime, out float nx, out float nz);
            // on the floor: the height of the floor plan under the new place (a place off it keeps the height the body had)
            float floorY = here.y - heightOverFloor;
            if (NavMesh.SamplePosition(new Vector3(nx, floorY, nz), out var ok, 1.5f, NavMesh.AllAreas)) floorY = ok.position.y;
            transform.position = new Vector3(nx, floorY + heightOverFloor, nz);
            // lie with the head away from the dragger: the body trails behind him along the line he pulls it
            var pull = player - transform.position; pull.y = 0f;
            if (pull.sqrMagnitude > 0.04f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-pull.normalized), 6f * Time.deltaTime);
        }
    }
}
