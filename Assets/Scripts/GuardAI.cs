using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    public enum GuardState { Patrol, Investigate, Chase, Stunned, Calling, Pinning }

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
        public float catchRadius = 1.6f;
        public float investigateWait = 3f;
        public float loseSightSeconds = 4f;
        public LayerMask sightBlockers = ~0;
        [Tooltip("Melee hits it takes to knock a guard out for good.")]
        public int hitsToKill = 10;

        /// <summary>Melee hits taken so far.</summary>
        public int Hits { get; private set; }
        /// <summary>Knocked out for good: lies on the ground and never gets up.</summary>
        public bool Dead { get; private set; }

        [Tooltip("Seconds the guard stands and looks at what he saw. If the player is still in view after this he calls backup and chases.")]
        public float callSeconds = 2f;
        [Tooltip("How long the player may be out of view before the call is dropped (stops a flicker of the raycast from resetting it).")]
        public float loseSightGrace = 0.3f;

        /// <summary>All live guards (the backup indicator draws one ring per calling guard).</summary>
        public static readonly System.Collections.Generic.List<GuardAI> All = new();

        public GuardState State { get; private set; } = GuardState.Patrol;
        /// <summary>True while the guard is radioing for backup. Only a weapon hit stops it.</summary>
        public bool IsCalling => calling;
        /// <summary>0..1: how far the backup call has got.</summary>
        public float CallProgress => !calling ? 0f : IsPuppet ? netProgress : Mathf.Clamp01(callTimer / Mathf.Max(0.1f, callSeconds));
        bool calling; float callTimer;
        float unseenFor; Vector3 lastSeenPos;

        // ---- networking: on a remote client the guard is a puppet; the host runs the AI and streams its state
        /// <summary>Same on every machine.</summary>
        public int NetId { get; private set; } = Net.NetIds.None;
        public bool IsPuppet { get; private set; }
        float netProgress, netSpeed;

        /// <summary>The boss: tough, and knocking it out wins the run.</summary>
        public bool IsBoss;
        /// <summary>Drops the elevator key when knocked out for good (floor 12).</summary>
        public bool carriesKey;

        /// <summary>Metres per second, for the walk animation (the host reads its agent, a puppet the streamed value).</summary>
        public float CurrentSpeed => IsPuppet ? netSpeed : (agent && agent.enabled && agent.isOnNavMesh ? agent.velocity.magnitude : 0f);

        /// <summary>Client: take the state the host sent.</summary>
        public void NetApply(GuardState state, bool dead, bool isCalling, float progress, float speed)
        {
            State = state; Dead = dead; calling = isCalling; netProgress = progress; netSpeed = speed;
        }

        /// <summary>The player this officer is holding down (host only), or null.</summary>
        public WobblyPlayer Holding => holding;
        WobblyPlayer holding;

        NavMeshAgent agent;
        int patrolIndex;
        float waitUntil, stunUntil, lastSeen;
        Vector3 noiseTarget;
        WobblyPlayer target;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            NetId = Net.NetIds.Register(this);
            if (Net.NetRole.IsRemoteClient) { IsPuppet = true; agent.enabled = false; }
        }

        void OnDestroy() => Net.NetIds.Unregister(NetId, this);
        void OnEnable() { NoiseBus.OnNoise += OnNoise; if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { NoiseBus.OnNoise -= OnNoise; All.Remove(this); }
        Vector3? pendingDispatch;
        bool started;

        void Start()
        {
            started = true;
            if (IsPuppet) return;
            if (pendingDispatch.HasValue) { var p = pendingDispatch.Value; pendingDispatch = null; Dispatch(p); }
            else GoNextPatrol();
        }

        /// <summary>Backup: run to a position (then look around and go back to patrolling).</summary>
        public void Dispatch(Vector3 pos)
        {
            if (IsPuppet) return;
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
            if (IsPuppet) return;
            if (State == GuardState.Stunned || State == GuardState.Chase || State == GuardState.Calling || State == GuardState.Pinning) return;
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
            if (IsPuppet) { Net.RunSync.Instance?.RequestGuardHit(this); return; } // the host decides what a hit does
            WeaponHitHost(stunSeconds, by);
        }

        /// <summary>The real effect of a weapon hit: offline, or on the host (after it checked a client's request).</summary>
        public void WeaponHitHost(float stunSeconds, GameObject by = null)
        {
            if (Dead) return;
            calling = false; callTimer = 0f; // a weapon hit cuts the radio call
            ReleaseHold(true);               // hit while holding someone down: they get free
            Hits++;
            if (Hits >= hitsToKill)
            {
                Dead = true;
                State = GuardState.Stunned;
                stunUntil = float.MaxValue;
                agent.isStopped = true;
                Debug.Log($"{name} is down for good after {Hits} hits.");
                if (carriesKey)
                {
                    carriesKey = false;
                    var gen = FindFirstObjectByType<FloorGenerator>();
                    if (gen) gen.DropKey(transform.position + Vector3.up * 0.5f);
                }
                if (IsBoss) HeistManager.Instance?.BossDefeated();
                return;
            }
            Stun(stunSeconds, by);
        }

        public void Stun(float seconds, GameObject by = null)
        {
            if (Dead || IsPuppet) return;
            ReleaseHold(true);               // stunned (melee or a thrown item) while holding someone down: they get free
            State = GuardState.Stunned;
            stunUntil = Time.time + seconds;
            agent.isStopped = true;
            if (by) noiseTarget = by.transform.position;
        }

        void Update()
        {
            if (IsPuppet) return;
            TickCall();
            switch (State)
            {
                case GuardState.Stunned:
                    if (Time.time >= stunUntil)
                    {
                        if (calling) { agent.isStopped = true; State = GuardState.Calling; return; } // still watching
                        agent.isStopped = false;
                        State = GuardState.Investigate;
                        agent.speed = investigateSpeed;
                        agent.SetDestination(noiseTarget);
                    }
                    return;
                case GuardState.Patrol: UpdatePatrol(); break;
                case GuardState.Investigate: UpdateInvestigate(); break;
                case GuardState.Calling: WatchWhileCalling(); break;
                case GuardState.Chase: UpdateChase(); break;
                case GuardState.Pinning: UpdatePinning(); break;
            }
            if (State != GuardState.Chase && State != GuardState.Calling && State != GuardState.Pinning) ScanForPlayers();
        }

        void StartCall(WobblyPlayer p)
        {
            target = p;
            calling = true; callTimer = 0f; unseenFor = 0f;
            lastSeenPos = p.transform.position;
            State = GuardState.Calling;
            agent.isStopped = true; // he stops and looks at what he saw
            Debug.Log($"{name} spotted something: watching for {callSeconds:0.#} s before he calls for backup and chases");
        }

        void TickCall()
        {
            if (!calling || Dead) return;
            // the player has to stay in view for the whole call: slip out of sight and it is dropped
            if (State == GuardState.Calling && !StillSees()) { CancelCall(); return; }
            callTimer += Time.deltaTime;
            if (callTimer >= callSeconds)
            {
                calling = false;
                HeistManager.Instance?.CallBackup(this);
                if (State == GuardState.Calling)
                {
                    // he still saw the player after watching: backup is on the way and he starts the chase
                    State = GuardState.Chase;
                    unseenFor = 0f;
                    agent.isStopped = false;
                    agent.speed = chaseSpeed;
                    if (target) agent.SetDestination(target.transform.position);
                }
            }
        }

        /// <summary>True while any player is still in view (a short grace period covers a flickering line of sight).</summary>
        bool StillSees()
        {
            if (target && CanSee(target)) { unseenFor = 0f; lastSeenPos = target.transform.position; return true; }
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (p && CanSee(p)) { target = p; unseenFor = 0f; lastSeenPos = p.transform.position; return true; }
            }
            unseenFor += Time.deltaTime;
            return unseenFor < loseSightGrace;
        }

        /// <summary>The player got out of view in time: no backup. The guard goes to where he last saw them and looks around.</summary>
        void CancelCall()
        {
            calling = false; callTimer = 0f; unseenFor = 0f;
            State = GuardState.Investigate;
            noiseTarget = lastSeenPos;
            agent.isStopped = false;
            agent.speed = investigateSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0;
            Debug.Log($"{name} lost sight of the player: no backup called");
        }

        /// <summary>The first seconds after spotting: the guard stands still and looks at what he saw. No chasing yet.</summary>
        void WatchWhileCalling()
        {
            agent.isStopped = true;
            FaceTarget();
        }

        float FlatDistance(Transform t)
        {
            var d = t.position - transform.position; d.y = 0f;
            return d.magnitude;
        }

        /// <summary>The chase (after the backup call): run at the player; touching them pins them to the ground. Lose them for a while and he gives up.</summary>
        void UpdateChase()
        {
            // the player he was after got pinned by someone else, or arrested: go for another free player in view
            if (target && target.Custody != CustodyState.Free) target = FindVisibleFreePlayer();
            if (!target) { LoseChase(); return; }
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(target.transform.position);
            if (CanSee(target)) { lastSeenPos = target.transform.position; unseenFor = 0f; }
            else
            {
                unseenFor += Time.deltaTime;
                if (unseenFor >= loseSightSeconds) { LoseChase(); return; }
            }
            if (FlatDistance(target.transform) <= catchRadius && HeistManager.Instance != null && HeistManager.Instance.Pin(target, this))
                BeginHold(target);
        }

        WobblyPlayer FindVisibleFreePlayer()
        {
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Free && CanSee(p)) return p;
            return null;
        }

        /// <summary>The officer got hold of a player: he stops and kneels on them until they are freed or arrested.</summary>
        public void BeginHold(WobblyPlayer p)
        {
            holding = p;
            State = GuardState.Pinning;
            calling = false; callTimer = 0f;
            agent.isStopped = true;
            Debug.Log($"{name} pinned {p.name}");
        }

        void UpdatePinning()
        {
            agent.isStopped = true;
            // the player is no longer pinned (busted, freed, bought back): back to work
            if (!holding || holding.Custody != CustodyState.Pinned || holding.PinnedBy != this) { EndHold(); return; }
            var to = holding.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 8f * Time.deltaTime);
        }

        /// <summary>He is hit, stunned or knocked out while holding someone: they are freed (host).</summary>
        public void ReleaseHold(bool freeThem = false)
        {
            var p = holding;
            if (!p) return;
            holding = null;
            if (freeThem && p.PinnedBy == this) HeistManager.Instance?.FreePinned(p);
            if (State == GuardState.Pinning) EndHold();
        }

        void EndHold()
        {
            holding = null;
            if (Dead || State == GuardState.Stunned) return;
            // look around where it happened, then carry on patrolling
            State = GuardState.Investigate;
            noiseTarget = transform.position;
            agent.isStopped = false;
            agent.speed = investigateSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0;
        }

        void LoseChase()
        {
            State = GuardState.Investigate;
            noiseTarget = lastSeenPos == Vector3.zero ? transform.position : lastSeenPos;
            agent.isStopped = false;
            agent.speed = investigateSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0;
            unseenFor = 0f;
            Debug.Log($"{name} lost the player and gave up the chase");
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
            if (p.Custody != CustodyState.Free) return false; // pinned or arrested players are not what he is looking for
            var eye = transform.position + Vector3.up * 1.6f;
            var to = p.transform.position + Vector3.up * 0.9f - eye;
            float range = p.IsHidden ? hiddenSightRange : sightRange;
            if (to.magnitude > range) return false;
            if (Vector3.Angle(transform.forward, to) > sightAngle) return false;
            // trigger volumes (hide spots, the elevator zone...) must not block the view, or standing in one would make you invisible
            if (Physics.Raycast(eye, to.normalized, out var hit, range, sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<WobblyPlayer>() == p;
            return true;
        }
    }
}
