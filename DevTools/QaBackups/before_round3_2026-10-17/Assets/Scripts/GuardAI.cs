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
        [Tooltip("A chasing guard keeps after a player for as long as the player is in his cone of vision. Out of it for this many seconds in a row and he gives up.")]
        public float loseSightSeconds = GuardRules.LoseSightSeconds;
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

        GuardState state = GuardState.Patrol;
        public GuardState State { get => state; private set => state = value; }

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

        /// <summary>
        /// Does a guard in this state walk about by himself, so that the doors open for him when he walks into one? (Calling, pinning
        /// somebody, knocked out: no, he stands still.) Whatever the reason he is walking: on patrol, going to look at a noise or at
        /// the place he lost the player, running at the player as backup, or chasing.
        /// </summary>
        public static bool UsesDoors(GuardState state) =>
            state == GuardState.Patrol || state == GuardState.Investigate || state == GuardState.Chase;

        /// <summary>
        /// Where he is walking, for the doors (a door opens for a guard who is about to go through it): fills <paramref name="xs"/> /
        /// <paramref name="zs"/> with his position followed by the corners of the rest of his path, and returns how many points that is,
        /// or 0 when he is standing still, knocked out or busy standing (calling, pinning). <paramref name="speed"/> is how fast he goes.
        /// The host reads its agent's path (so a door opens even for a guard about to turn into it); a remote client's puppet has
        /// only the way it faces and the speed the host streamed, and is taken to keep walking straight on.
        /// </summary>
        public int GetRoute(float[] xs, float[] zs, out float speed)
        {
            speed = 0f;
            if (Dead || !UsesDoors(state)) return 0;
            var pos = transform.position;
            xs[0] = pos.x; zs[0] = pos.z;
            if (IsPuppet)
            {
                if (netSpeed < 0.3f) return 0;
                var f = transform.forward; f.y = 0f;
                if (f.sqrMagnitude < 0.0025f) return 0;
                f.Normalize();
                speed = Mathf.Max(netSpeed, MinLookSpeed);                    // (a puppet that has only just set off is still slow)
                xs[1] = pos.x + f.x * StraightAhead; zs[1] = pos.z + f.z * StraightAhead;
                return 2;
            }
            if (!agent || !agent.enabled || !agent.isOnNavMesh || agent.isStopped) return 0;
            var want = agent.desiredVelocity; want.y = 0f;
            if (want.sqrMagnitude < 0.0025f) return 0;                          // not going anywhere right now
            speed = agent.speed;                                                // (the speed he is going to have, not the one he has yet)
            int n = 1;
            if (agent.hasPath)
            {
                if (cornerBuffer == null) cornerBuffer = new Vector3[xs.Length];
                int c = agent.path.GetCornersNonAlloc(cornerBuffer);            // (the doors ask about ten times a second, only for guards on the move)
                int leg = NearestLeg(cornerBuffer, c, pos);                     // (the list may still hold corners he has passed)
                for (int i = leg + 1; i < c && n < xs.Length; i++) { xs[n] = cornerBuffer[i].x; zs[n] = cornerBuffer[i].z; n++; }
            }
            if (n == 1)
            {
                want.Normalize();                                               // no path to go by: straight on the way he wants to go
                xs[1] = pos.x + want.x * StraightAhead; zs[1] = pos.z + want.z * StraightAhead;
                n = 2;
            }
            return n;
        }

        /// <summary>The slowest a walking puppet is taken to walk, for looking ahead to a door (m/s).</summary>
        const float MinLookSpeed = 1.5f;
        /// <summary>How far a guard whose path is not known is taken to keep walking straight on (m).</summary>
        const float StraightAhead = 12f;
        Vector3[] cornerBuffer;

        /// <summary>The leg (from corner i to i + 1) of a path that is nearest to p (flat); -1 if the path has fewer than two corners.</summary>
        static int NearestLeg(Vector3[] c, int count, Vector3 p)
        {
            int best = -1; float bestSq = float.MaxValue;
            for (int i = 0; i + 1 < count; i++)
            {
                float ax = c[i].x, az = c[i].z, bx = c[i + 1].x - ax, bz = c[i + 1].z - az;
                float len2 = bx * bx + bz * bz;
                float t = len2 > 1e-6f ? Mathf.Clamp01(((p.x - ax) * bx + (p.z - az) * bz) / len2) : 0f;
                float qx = ax + bx * t - p.x, qz = az + bz * t - p.z;
                float sq = qx * qx + qz * qz;
                if (sq < bestSq - 1e-4f) { bestSq = sq; best = i; }
            }
            return best;
        }

        /// <summary>Client: take the state the host sent.</summary>
        public void NetApply(GuardState state, bool dead, bool isCalling, float progress, float speed, bool armed = false)
        {
            State = state; Dead = dead; calling = isCalling; netProgress = progress; netSpeed = speed;
            // the host says whether he carries a gun: this machine shows it (and plays the shots the host announces)
            if (armed && gun == null) gameObject.AddComponent<GuardGun>();
            else if (!armed && gun != null) gun.Remove();
        }

        // ---- the gun (see GuardGun and ArmedGuards)
        GuardGun gun;
        /// <summary>His gun, or null when he is not armed.</summary>
        public GuardGun Gun => gun;
        /// <summary>He carries a gun.</summary>
        public bool Armed => gun != null && gun.Armed;
        internal void AttachGun(GuardGun g) { gun = g; }
        internal void DetachGun(GuardGun g) { if (gun == g) gun = null; }

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
            if (noticeGate != null && !noticeGate(n.Position)) return;      // deaf to anything outside the room he guards
            float dist = Vector3.Distance(transform.position, n.Position);
            if (dist > n.Loudness * hearingMultiplier) return;
            noiseTarget = n.Position;
            State = GuardState.Investigate;
            agent.speed = investigateSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0;
        }

        /// <summary>
        /// A weapon hit: stuns like Stun, and the hitsToKill-th hit leaves the guard down for good. A melee hit counts as one hit; a bullet
        /// counts as <paramref name="weight"/> (the pew: five of the ten).
        /// </summary>
        public void WeaponHit(float stunSeconds, GameObject by = null, int weight = 1)
        {
            if (IsPuppet) { Net.RunSync.Instance?.RequestGuardHit(this); return; } // the host decides what a hit does
            WeaponHitHost(stunSeconds, by, weight);
        }

        /// <summary>The real effect of a weapon hit: offline, or on the host (after it checked a client's request).</summary>
        public void WeaponHitHost(float stunSeconds, GameObject by = null, int weight = 1)
        {
            if (Dead) return;
            calling = false; callTimer = 0f; // a weapon hit cuts the radio call
            ReleaseHold(true);               // hit while holding someone down: they get free
            Hits += Mathf.Max(1, weight);
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

        /// <summary>
        /// The chase (after the backup call): run at the player; touching them pins them to the ground. He keeps after somebody for as long
        /// as a free player is in his cone of vision. Lose them for <see cref="loseSightSeconds"/> in a row and he gives up
        /// (<see cref="GuardRules.PursuitOver"/>).
        /// </summary>
        void UpdateChase()
        {
            // the player he was after got pinned by someone else, or arrested: go for another free player in view
            if (target && target.Custody != CustodyState.Free) target = FindVisibleFreePlayer();
            if (!target) { LoseChase(); return; }

            // in view: the target, or (if he got away from the cone) any other free player who is in it
            bool seen = CanSee(target);
            if (!seen)
            {
                var other = FindVisibleFreePlayer();
                if (other) { target = other; seen = true; }
            }
            if (seen) lastSeenPos = target.transform.position;
            if (GuardRules.PursuitOver(ref unseenFor, seen, Time.deltaTime, loseSightSeconds)) { LoseChase(); return; }

            // an armed guard stops and shoots while he sees a free player who is in range and not within arm's reach (then he grabs him)
            if (gun != null && gun.Armed)
            {
                float flat = FlatDistance(target.transform);
                if (flat > catchRadius * 1.3f && gun.Engage(target, seen, flat))
                {
                    agent.isStopped = true;
                    FaceTarget();
                    return;
                }
            }

            // (out of view he still runs at where the player is until the time is up)
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(target.transform.position);
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

        // ------------------------------------------------------------------ a guard on duty (the outpost's)

        /// <summary>
        /// Set for a guard who is blind and deaf to anything the gate says no to (a position outside the room he guards): he sees and hears
        /// only what is inside it. A player who steps out of the room drops the call he was making (the guard no longer sees them) and a
        /// chase that leaves the room runs out after <see cref="loseSightSeconds"/>, like any other that loses its target.
        /// </summary>
        public System.Func<Vector3, bool> noticeGate;
        /// <summary>A guard on duty at one spot: after any excursion he walks back to it and stands there looking the way <see cref="postForward"/> says.</summary>
        public bool stationary;
        public Vector3 postForward = Vector3.forward;

        /// <summary>Puts the guard on duty where he stands, facing <paramref name="forward"/> (a flat direction).</summary>
        public void StandGuard(Vector3 forward)
        {
            forward.y = 0f;
            postForward = forward.sqrMagnitude > 0.01f ? forward.normalized : transform.forward;
            stationary = true;
            transform.rotation = Quaternion.LookRotation(postForward);
        }

        void UpdatePatrol()
        {
            if (roam == null && (patrolPoints == null || patrolPoints.Length == 0)) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                if (stationary) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(postForward), 3f * Time.deltaTime);
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
            if (noticeGate != null && !noticeGate(p.transform.position)) return false;      // outside the room he guards: he does not see it
            var eye = transform.position + Vector3.up * 1.6f;
            var to = p.transform.position + Vector3.up * 0.9f - eye;
            float range = p.IsHidden ? hiddenSightRange : sightRange;
            // the cone is the fan he shows on the floor (GuardVision): flat, so standing close to him does not hide you from him
            var flat = p.transform.position - transform.position;
            var fwd = transform.forward;
            if (!GuardRules.InCone(fwd.x, fwd.z, flat.x, flat.z, sightAngle, range)) return false;
            // trigger volumes (hide spots, the elevator zone...) must not block the view, or standing in one would make you invisible
            if (Physics.Raycast(eye, to.normalized, out var hit, range, sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<WobblyPlayer>() == p;
            return true;
        }
    }
}
