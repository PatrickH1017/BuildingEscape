using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    public enum GuardState { Patrol, Investigate, Chase, Stunned, Calling }

    /// <summary>
    /// Security guard / robot: patrols waypoints, investigates noise, chases players it sees.
    /// Hidden players are only caught at very short range.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardAI : MonoBehaviour
    {
        public Transform[] patrolPoints;
        /// <summary>If set, the guard wanders to random points from this instead of walking patrolPoints.</summary>
        public System.Func<Vector3> roam;
        public float walkSpeed = 2.2f, investigateSpeed = 3f, chaseSpeed = 4.6f;
        public float sightRange = 14f, sightAngle = 65f, hiddenSightRange = 1.5f;
        public float hearingMultiplier = 1f;
        public float catchRadius = 1.2f;
        public float investigateWait = 3f;
        public float loseSightSeconds = 4f;
        public LayerMask sightBlockers = ~0;
        [Tooltip("Melee hits it takes to knock a guard out for good.")]
        public int hitsToKill = 10;

        /// <summary>Melee hits taken so far.</summary>
        public int Hits { get; private set; }
        /// <summary>Knocked out for good: lies on the ground and never gets up.</summary>
        public bool Dead { get; private set; }

        [Tooltip("Seconds from spotting a player until the guard has called for backup (you are busted).")]
        public float callSeconds = 5f;

        /// <summary>All live guards (the backup indicator draws one ring per calling guard).</summary>
        public static readonly System.Collections.Generic.List<GuardAI> All = new();

        public GuardState State { get; private set; } = GuardState.Patrol;
        /// <summary>True while the guard is radioing for backup. Only a weapon hit stops it.</summary>
        public bool IsCalling => calling;
        /// <summary>0..1: how far the backup call has got.</summary>
        public float CallProgress => calling ? Mathf.Clamp01(callTimer / Mathf.Max(0.1f, callSeconds)) : 0f;
        bool calling; float callTimer;

        NavMeshAgent agent;
        int patrolIndex;
        float waitUntil, stunUntil, lastSeen;
        Vector3 noiseTarget;
        WobblyPlayer target;

        void Awake() => agent = GetComponent<NavMeshAgent>();
        void OnEnable() { NoiseBus.OnNoise += OnNoise; if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { NoiseBus.OnNoise -= OnNoise; All.Remove(this); }
        Vector3? pendingDispatch;
        bool started;

        void Start()
        {
            started = true;
            if (pendingDispatch.HasValue) { var p = pendingDispatch.Value; pendingDispatch = null; Dispatch(p); }
            else GoNextPatrol();
        }

        /// <summary>Backup: run to a position (then look around and go back to patrolling).</summary>
        public void Dispatch(Vector3 pos)
        {
            if (!started) { pendingDispatch = pos; return; }
            noiseTarget = pos;
            State = GuardState.Investigate;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(pos);
            waitUntil = 0;
        }

        void OnNoise(NoiseEvent n)
        {
            if (State == GuardState.Stunned || State == GuardState.Chase || State == GuardState.Calling) return;
            float dist = Vector3.Distance(transform.position, n.Position);
            if (dist > n.Loudness * hearingMultiplier) return;
            noiseTarget = n.Position;
            State = GuardState.Investigate;
            agent.speed = investigateSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0;
        }

        /// <summary>A weapon hit: stuns like Stun, and the hitsToKill-th hit leaves the guard down for good.</summary>
        public void WeaponHit(float stunSeconds, GameObject by = null)
        {
            if (Dead) return;
            calling = false; callTimer = 0f; // a weapon hit cuts the radio call
            Hits++;
            if (Hits >= hitsToKill)
            {
                Dead = true;
                State = GuardState.Stunned;
                stunUntil = float.MaxValue;
                agent.isStopped = true;
                Debug.Log($"{name} is down for good after {Hits} hits.");
                return;
            }
            Stun(stunSeconds, by);
        }

        public void Stun(float seconds, GameObject by = null)
        {
            if (Dead) return;
            State = GuardState.Stunned;
            stunUntil = Time.time + seconds;
            agent.isStopped = true;
            if (by) noiseTarget = by.transform.position;
        }

        void Update()
        {
            TickCall();
            switch (State)
            {
                case GuardState.Stunned:
                    if (Time.time >= stunUntil)
                    {
                        if (calling) { agent.isStopped = false; State = GuardState.Calling; return; } // still on the radio
                        agent.isStopped = false;
                        State = GuardState.Investigate;
                        agent.speed = investigateSpeed;
                        agent.SetDestination(noiseTarget);
                    }
                    return;
                case GuardState.Patrol: UpdatePatrol(); break;
                case GuardState.Investigate: UpdateInvestigate(); break;
                case GuardState.Calling: ChargeWhileCalling(); break;
            }
            if (State != GuardState.Chase && State != GuardState.Calling) ScanForPlayers();
        }

        void StartCall(WobblyPlayer p)
        {
            target = p;
            calling = true; callTimer = 0f;
            State = GuardState.Calling;
            agent.isStopped = false; // he keeps charging while he radios
            Debug.Log($"{name} spotted a player and is calling for backup ({callSeconds:0} s)");
        }

        void TickCall()
        {
            if (!calling || Dead) return;
            callTimer += Time.deltaTime;
            if (callTimer >= callSeconds)
            {
                calling = false;
                HeistManager.Instance?.CallBackup(this);
                if (State == GuardState.Calling)
                {
                    // call made: back to looking for the player (if they are still in sight, he calls again)
                    agent.isStopped = false;
                    State = GuardState.Investigate;
                    noiseTarget = target ? target.transform.position : transform.position;
                    agent.speed = investigateSpeed;
                    agent.SetDestination(noiseTarget);
                    waitUntil = 0;
                }
            }
        }

        /// <summary>While he radios for backup the guard runs straight at the player; catching them busts the team.</summary>
        void ChargeWhileCalling()
        {
            if (!target) { FaceTarget(); return; }
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(target.transform.position);
            if (Vector3.Distance(transform.position, target.transform.position) <= catchRadius)
                HeistManager.Instance?.Bust(target);
        }

        void FaceTarget()
        {
            if (!target) return;
            var to = target.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
        }

        void UpdatePatrol()
        {
            if (roam == null && (patrolPoints == null || patrolPoints.Length == 0)) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                if (waitUntil == 0) waitUntil = Time.time + 1.2f;
                if (Time.time >= waitUntil) { waitUntil = 0; GoNextPatrol(); }
            }
        }

        void GoNextPatrol()
        {
            if (roam != null)
            {
                // backup guards wander freely around the floor instead of walking a fixed route
                agent.isStopped = false;
                agent.speed = walkSpeed;
                agent.SetDestination(roam());
                return;
            }
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            agent.speed = walkSpeed;
            agent.SetDestination(patrolPoints[patrolIndex].position);
            patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }

        void UpdateInvestigate()
        {
            if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.3f) return;
            if (waitUntil == 0) waitUntil = Time.time + investigateWait;
            if (Time.time >= waitUntil) { waitUntil = 0; State = GuardState.Patrol; GoNextPatrol(); }
        }

        void ScanForPlayers()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (CanSee(p)) { StartCall(p); return; }
            }
        }

        bool CanSee(WobblyPlayer p)
        {
            var eye = transform.position + Vector3.up * 1.6f;
            var to = p.transform.position + Vector3.up * 0.9f - eye;
            float range = p.IsHidden ? hiddenSightRange : sightRange;
            if (to.magnitude > range) return false;
            if (Vector3.Angle(transform.forward, to) > sightAngle) return false;
            if (Physics.Raycast(eye, to.normalized, out var hit, range, sightBlockers))
                return hit.collider.GetComponentInParent<WobblyPlayer>() == p;
            return true;
        }
    }
}
