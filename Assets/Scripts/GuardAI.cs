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
        [Tooltip("Down a straight line of sight (a hallway) he sees this many times as far as usual, in a narrow beam of 'longSightAngle' degrees each side of the way he faces (GuardRules.RangeAt).")]
        public float longSightFactor = GuardRules.LongSightFactor, longSightAngle = GuardRules.LongSightHalfAngle;
        public float hearingMultiplier = 1f;
        public float catchRadius = 1.6f;
        public float investigateWait = 3f;
        [Tooltip("A chasing guard keeps after a player for as long as the player is in his cone of vision. Out of it for this many seconds in a row and he gives up.")]
        public float loseSightSeconds = GuardRules.LoseSightSeconds;
        public LayerMask sightBlockers = ~0;
        [Tooltip("The damage it takes to put a guard down for good, in hits of 10 HP each (GuardHealthRules.HpPerHit): 10 hits = 100 HP for a base guard. A bullet does its gun's guardHits (less through a shield); a melee blow takes its weapon's damage (crowbar 15, bat 25, hatchet 35 HP).")]
        public int hitsToKill = GuardHealthRules.BaseHitsToKill;

        /// <summary>What he carries and wears (<see cref="GuardLoadout"/>): set when he is made (the host) or from the host's snapshot (a puppet); GuardVisual puts the vest and the gun on him.</summary>
        public GuardLoadout Loadout { get; private set; } = GuardLoadout.Unarmed;
        /// <summary>The shield he wears (none, light, medium, heavy): it takes a share of every bullet (<see cref="GuardLoadouts.BulletShare"/>).</summary>
        public ShieldTier Shield => Loadout.Shield;
        /// <summary>Wears a shield of any tier.</summary>
        public bool Armoured => Loadout.Shield != ShieldTier.None;

        /// <summary>Damage taken so far, in hits (a shield takes a share of each bullet, so it need not be a whole number).</summary>
        public float Damage { get; private set; }
        /// <summary>The damage taken so far, in whole hits.</summary>
        public int Hits => Mathf.RoundToInt(Damage);
        /// <summary>His full health in HP (the owner, 2026-10-22): 100 for a base guard (hitsToKill x 10 HP, see <see cref="GuardHealthRules"/>); the boss's thirty hits are 300. The host's (or the offline game's) number: a client's guard is a puppet.</summary>
        public float MaxHealth => GuardHealthRules.MaxHealth(hitsToKill);
        /// <summary>His health now in HP, never below 0: <see cref="MaxHealth"/> less the damage he has taken (bullets and blows take from the same pool). At 0 he is <see cref="Dead"/>.</summary>
        public float Health => GuardHealthRules.HealthLeft(Damage, hitsToKill);
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

        /// <summary>True while the guard is radioing for backup. A melee blow or a punch cuts it, so does being put down for good; a bullet does not (it starts one: <see cref="BeginShotCall"/>).</summary>
        public bool IsCalling => calling;
        /// <summary>0..1: how far the backup call has got.</summary>
        public float CallProgress => !calling ? 0f : IsPuppet ? netProgress : Mathf.Clamp01(callTimer / Mathf.Max(0.1f, callSeconds));
        bool calling; float callTimer;
        float unseenFor; Vector3 lastSeenPos;

        // ---- a guard who was knocked over radios for backup when he gets up (the owner, 2026-10-20: "Guards that are hit should call for backup upon getting up")
        bool callOnGetUp, callAnyway;
        WobblyPlayer hitBy;
        /// <summary>A backup officer (spawned by a radio call or the alarm): he notices a player in his cone much faster (<see cref="GuardRules.BackupSpotFactor"/>; the owner, 2026-10-20). Set by the floor when it spawns the reinforcements.</summary>
        public bool IsBackup;
        /// <summary>How long a player who stays <paramref name="distance"/> metres away has to stay in his cone before he notices, that is, how long the whole notice meter takes to fill at that distance (a backup officer needs a quarter of it; see <see cref="NoticeProgress"/>).</summary>
        public float SpotSecondsAt(float distance) => GuardRules.SpotSeconds(distance, spotBase, spotPerMetre) * (IsBackup ? GuardRules.BackupSpotFactor : 1f);

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

        /// <summary>Client: take the state the host sent (<paramref name="loadoutPacked"/>: his gun's kind and his shield, see <see cref="GuardLoadout.Pack"/>; 0 = not told).</summary>
        public void NetApply(GuardState state, bool dead, bool isCalling, float progress, float speed, bool armed = false, bool eatingNow = false, byte loadoutPacked = 0)
        {
            State = state; Dead = dead; calling = isCalling; netProgress = progress; netSpeed = speed; eating = eatingNow;
            // the host says what he carries and wears: this machine shows it (and plays the shots the host announces)
            var told = loadoutPacked != 0 ? GuardLoadout.Unpack(loadoutPacked, armed) : new GuardLoadout(armed, Loadout.Kind, Loadout.Shield);
            if (told != Loadout) Loadout = told;
            SyncGun();
        }

        /// <summary>The loadout he is made with (host / offline); GuardVisual notices the change and builds the vest and the gun.</summary>
        public void SetLoadout(GuardLoadout loadout) { Loadout = loadout; }

        /// <summary>Makes his gun match his loadout: the right kind in his hand when he is armed, none when he is not (a gun of another kind is taken away and the right one given).</summary>
        public void SyncGun()
        {
            if (Loadout.Armed)
            {
                if (gun != null && gun.kind != Loadout.Kind) gun.Remove();
                if (gun == null || !gun.Armed)
                {
                    var g = gameObject.AddComponent<GuardGun>();
                    g.Configure(Loadout.Kind);
                }
            }
            else if (gun != null) gun.Remove();
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
        /// <summary>The player this officer is after now (host only: a puppet knows nothing of it), or null. The developer spectator's overlay shows it (RunSync.DevView.cs).</summary>
        public WobblyPlayer Target => target;

        NavMeshAgent agent;
        int patrolIndex;
        float waitUntil, stunUntil, lastSeen;
        Vector3 noiseTarget;
        WobblyPlayer target;
        WobblyPlayer grabFor; float grabHeld;          // (the player he is trying to grab and how long he has held on: CustodyRules.GrabSeconds pins)

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

        /// <summary>
        /// The real effect of a legacy weapon hit: offline, or on the host. It stuns him and counts <paramref name="weight"/> hits (a shield takes no share of it): the tests use it to put a guard down. In the game a melee blow is
        /// <see cref="MeleeHitHost"/> and a bullet <see cref="BulletHitHost"/> (the owner, 2026-10-19: bullets damage, melee knocks over).
        /// </summary>
        public void WeaponHitHost(float stunSeconds, GameObject by = null, int weight = 1)
        {
            if (Dead) return;
            CutRadioAndRelease();
            Damage += Mathf.Max(1, weight);
            if (CheckDown()) return;
            Stun(stunSeconds, by);
        }

        /// <summary>
        /// A melee blow (the owner, 2026-10-19: "Melee weapons knock over a guard one shot (depending on the tier of the weapon determines on how long guard are knocked down)"): he is knocked over for <paramref name="stunSeconds"/>
        /// (the weapon's: bat 3 s, crowbar 4 s, hatchet 6 s), it cuts his radio call and frees a player he holds down. Since 2026-10-22 ("melee weapons are not killing guards ... a health based system") it also takes
        /// <paramref name="damage"/> HP off his health (the weapon's <see cref="MeleeWeapon.Spec.guardDamage"/>: crowbar 15, bat 25, hatchet 35 of a base guard's 100): the blow that takes his last HP puts him down for good instead of
        /// knocking him over. (The boss keeps his old count - a blow is one of his thirty hits - until his floor is redone.) A client's blow is a request: the host decides, and takes the damage from the weapon the client holds.
        /// </summary>
        public void MeleeHit(float stunSeconds, GameObject by = null, float damage = 0f)
        {
            if (IsPuppet) { Net.RunSync.Instance?.RequestGuardHit(this); return; }
            MeleeHitHost(stunSeconds, by, damage);
        }

        /// <summary>
        /// The real effect of a melee blow: offline, or on the host (after it checked a client's request). It takes <paramref name="damage"/> HP off the guard once (the caller says it once per blow and guard: a swing finds each guard once);
        /// a guard who is down takes nothing more. 0 (the default) is a blow that only knocks over. How many guards are about does not matter: the same blow takes the same off every guard.
        /// </summary>
        public void MeleeHitHost(float stunSeconds, GameObject by = null, float damage = 0f)
        {
            if (Dead) return;                                  // (down for good: no more damage, no second fall, no second drop of the key)
            CutRadioAndRelease();
            Damage = IsBoss ? Damage + 1f : GuardHealthRules.DamageAfterBlow(Damage, damage);
            if (CheckDown()) return;                           // (the blow that takes his last HP puts him down for good: he is not knocked over)
            Stun(stunSeconds, by);
        }

        /// <summary>
        /// A bullet (the owner, 2026-10-19: "Bullets should not knock down, bullets should eliminate and depending on the bullet determines how much health a guard loses"): it takes <paramref name="bulletHits"/> hits of his health
        /// (<see cref="hitsToKill"/>; the gun's <c>guardHits</c>) less what his shield takes (<see cref="GuardLoadouts.DamageTaken"/>; a piercing round takes it all), and it never knocks him over. It frees a player he holds down. A shot officer who is still on his feet
        /// starts to radio for backup (the owner, 2026-10-21: "officers should start calling for backup after being shot and if a player eliminates them in time the call will be cancelled"): see <see cref="BeginShotCall"/>; the bullet that puts him down
        /// cancels the call (<see cref="CheckDown"/>). Host / offline only (a client's shot is decided by the host).
        /// </summary>
        public void BulletHitHost(float bulletHits, bool piercing = false, GameObject by = null)
        {
            if (Dead) return;
            ReleaseHold(true);                       // (hit while holding somebody down: they get free; a radio call he has going is NOT cut by a bullet any more)
            Damage += GuardLoadouts.DamageTaken(bulletHits, Shield, piercing);
            if (CheckDown()) return;                 // (down for good: the call he had going is cancelled with him)
            if (BeginShotCall(by)) return;           // (he stands and radios for backup: no need to send him to look at where the shot came from)
            TurnToShooter(by);
        }

        /// <summary>Did a bullet that hit him at this height (world y) hit his head (the owner, 2026-10-27: "headshots should deal more damage than body shots")? The top fifth of his body, from his feet (<see cref="BulletDamageRules.HeadFrom"/>), times his scale.</summary>
        public bool IsHeadHit(float hitY)
        {
            float s = Mathf.Abs(transform.lossyScale.y);
            return BulletDamageRules.IsHeadshot(hitY, transform.position.y - GuardRules.FeetBelowMiddle * s, GuardRules.BodyHeight * s);
        }

        /// <summary>The dev menu's "alarm off" (the owner, 2026-10-27): he gives up whatever he was doing about the players - a chase, an investigation, a radio call - and goes back to his round (host / offline). A body on the floor is left alone.</summary>
        public void DevCalm()
        {
            if (Dead || IsPuppet) return;
            ReleaseHold(true);
            calling = false; callTimer = 0f; callAnyway = false; callOnGetUp = false;
            target = null; hitBy = null; noticed.Clear();
            if (State != GuardState.Stunned) { State = GuardState.Patrol; waitUntil = 0f; GoNextPatrol(); }
        }

        void CutRadioAndRelease()
        {
            calling = false; callTimer = 0f; // a weapon hit cuts the radio call
            ReleaseHold(true);               // hit while holding someone down: they get free
        }

        /// <summary>Has his damage reached his health? Then he is down for good: he drops the key if he has it and the clip of his gun, and the boss's floor is won.</summary>
        bool CheckDown()
        {
            if (Dead) return true;                                          // (already down: the fall, the drops and the boss's win happen once)
            if (!GuardHealthRules.IsDown(Damage, hitsToKill)) return false;
            Dead = true;
            calling = false; callTimer = 0f; callAnyway = false; callOnGetUp = false;      // (eliminated before his call was through: no backup, the ring over his head goes - the owner, 2026-10-21)
            State = GuardState.Stunned;
            stunUntil = float.MaxValue;
            agent.isStopped = true;
            Debug.Log($"{name} is down for good after {Damage:0.#} hits (0 of {MaxHealth:0} HP left).");
            if (carriesKey)
            {
                carriesKey = false;
                var gen = FindFirstObjectByType<FloorGenerator>();
                if (gen) gen.DropKey(transform.position + Vector3.up * 0.5f);
            }
            GuardDrops.OnDown(this);         // (the owner, 2026-10-19: "guards should drop a fair amount of bullets per guard"; since 2026-10-23: 0 to 3 bullets, taken by running over them)
            HeistManager.Instance?.GuardEliminated(this);       // (the owner, 2026-10-23: 15 seconds later a new guard comes out of a stairwell)
            if (IsBoss) HeistManager.Instance?.BossDefeated();
            return true;
        }

        /// <summary>A bullet that did not put him down: when he is not busy with a player (a chase, a knock-over) he goes to where it came from, to look.</summary>
        void TurnToShooter(GameObject by)
        {
            if (!by || IsPuppet || Dead) return;
            if (State == GuardState.Chase || State == GuardState.Stunned) return;
            noiseTarget = by.transform.position;
            State = GuardState.Investigate;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0f;
        }

        /// <summary>
        /// A bullet that did not put him down starts a radio call for backup: he stops where he is and radios for <see cref="callSeconds"/> (the same ring over his head as a sighting), whether or not he sees anybody; when the call is through the
        /// officers are sent and he goes after whoever shot him (<see cref="AfterGetUpCall"/>). Putting him down before that cancels the call (<see cref="CheckDown"/>); a melee blow or a punch cuts it. A bullet that hits a guard who is already radioing
        /// neither starts it over nor stops it. A guard who is after somebody already (a chase begins with a call, so his backup is on the way), one who is knocked over (he radios on getting up), the boss and one who is not on the floor start no call.
        /// True when he is radioing now (the caller then does not send him to look at where the shot came from).
        /// </summary>
        bool BeginShotCall(GameObject by)
        {
            if (Dead || IsBoss || IsPuppet) return false;
            var shooter = by ? by.GetComponentInParent<WobblyPlayer>() : null;
            if (calling)
            {
                callAnyway = true;                                       // (shot while he watches somebody: the call no longer needs them in view)
                if (shooter) { hitBy = shooter; target = shooter; lastSeenPos = shooter.transform.position; }
                if (by) noiseTarget = by.transform.position;
                return true;
            }
            if (State == GuardState.Chase || State == GuardState.Stunned) return false;
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
            hitBy = shooter;
            target = shooter;
            noiseTarget = by ? by.transform.position : transform.position;
            if (shooter) lastSeenPos = shooter.transform.position;
            calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = true;
            eating = false;
            State = GuardState.Calling;
            agent.isStopped = true;
            Debug.Log($"{name} was shot and is calling for backup");
            return true;
        }

        /// <summary>A punch landed (<see cref="PunchRules"/>): the host decides what it does (a client's guard is a puppet and asks it to).</summary>
        public void PunchHit(float stunSeconds, GameObject by = null)
        {
            if (IsPuppet) { Net.RunSync.Instance?.RequestGuardHit(this); return; }
            PunchHost(stunSeconds, by);
        }

        /// <summary>
        /// The real effect of a punch (offline, or on the host after it checked a client's request): the radio call is cut, a player he holds down gets free and he is stunned for a moment, but the blow does not count towards
        /// knocking him out (only weapons and bullets do).
        /// </summary>
        public void PunchHost(float stunSeconds, GameObject by = null)
        {
            if (Dead) return;
            calling = false; callTimer = 0f;
            ReleaseHold(true);
            Stun(stunSeconds, by);
        }

        public void Stun(float seconds, GameObject by = null)
        {
            if (Dead || IsPuppet) return;
            ReleaseHold(true);               // stunned (melee or a thrown item) while holding someone down: they get free
            State = GuardState.Stunned;
            stunUntil = Time.time + seconds;
            agent.isStopped = true;
            if (by)
            {
                noiseTarget = by.transform.position;
                hitBy = by.GetComponentInParent<WobblyPlayer>();
                if (!IsBoss) callOnGetUp = true;                     // (knocked over by somebody: when he gets up he radios for backup)
            }
        }

        GuardCorpse corpse;
        DraggableBody draggable;

        // ---- a donut break (the owner, 2026-10-18: "Crazy chances that a guard (not the armed guards) will randomly stop what they are doing to eat a donut")
        bool eating;
        /// <summary>He has stopped his round to eat a donut (a puppet: what the host says).</summary>
        public bool Eating => eating;
        /// <summary>A test hook: the roll (0 = always, 1 = never) for a donut break at a patrol stop; a negative value is a real dice roll.</summary>
        public static float DonutRollOverride = -1f;

        // ---- the PA (the owner, 2026-10-18: "If a guard is in range of the loud speaker noise they should rush to the outpost")
        float paUntil;
        /// <summary>He is rushing to the outpost (or waiting at its door) because the loudspeakers are on and he is within earshot of one.</summary>
        public bool PaAlerted => !Dead && Time.time < paUntil;

        /// <summary>
        /// The loudspeakers are on and he can hear one (<see cref="PaRules"/>): he drops what he is doing (a radio call, a donut) and runs to <paramref name="target"/>, the outpost's door; every call renews it.
        /// Not an officer who is holding a player down, chasing one, or down himself. Host / offline only. A locked door stops him there like anybody.
        /// </summary>
        public void PaAlert(Vector3 target)
        {
            if (IsPuppet || Dead || State == GuardState.Pinning || State == GuardState.Stunned || State == GuardState.Chase || !agent || !agent.isOnNavMesh) return;
            eating = false;
            if (calling) { calling = false; callTimer = 0f; }
            bool fresh = !(State == GuardState.Investigate && Time.time < paUntil);
            paUntil = Time.time + PaRules.AlertSeconds;
            if (!fresh) return;
            State = GuardState.Investigate;
            noiseTarget = target;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(target);
            waitUntil = 0f;
        }

        /// <summary>A guard who is down for good lies there for a while and then goes (<see cref="GuardCorpse"/>); every machine does it by itself from the flag it has (the host's, a puppet's from the snapshot).</summary>
        bool bodySettled;

        /// <summary>
        /// A guard who is down for good lies where he fell and nobody has to walk round him (the owner, 2026-10-21: "A eliminated officer should remain on the ground but can now be walked through"): every collider of his body goes off - the standing
        /// capsule would stay in the way as an invisible pillar, and soak up the bullets and blows meant for somebody behind him - and the body stays where it lies until <see cref="GuardCorpse"/> takes it away.
        /// Every machine does it by itself from the flag it has (the host's from the blow, a puppet's from the snapshot).
        /// </summary>
        void SettleBody()
        {
            bodySettled = true;
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c) c.enabled = false;
        }

        void LateUpdate()
        {
            if (!Dead) return;
            if (!bodySettled) SettleBody();
            if (!IsBoss && corpse == null) corpse = gameObject.AddComponent<GuardCorpse>();
            if (!IsBoss && draggable == null) draggable = gameObject.AddComponent<DraggableBody>();          // (the owner, 2026-10-23: a body can be dragged away to hide it)
        }

        void Update()
        {
            if (IsPuppet) return;
            if (eating && (Dead || State != GuardState.Patrol)) eating = false;        // (a noise, a sighting or a blow ends the break)
            TickCall();
            if (State != GuardState.Chase) grabHeld = 0f;          // (the grab only runs in a chase)
            switch (State)
            {
                case GuardState.Stunned:
                    if (Time.time >= stunUntil)
                    {
                        if (calling) { agent.isStopped = true; State = GuardState.Calling; return; } // still watching
                        if (callOnGetUp) { callOnGetUp = false; BeginGetUpCall(); return; }
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
            if (State == GuardState.Patrol || State == GuardState.Investigate) ScanForBodies();
        }

        void StartCall(WobblyPlayer p)
        {
            target = p;
            calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = false;
            lastSeenPos = p.transform.position;
            State = GuardState.Calling;
            agent.isStopped = true; // he stops and looks at what he saw
            Debug.Log($"{name} spotted something: watching for {callSeconds:0.#} s before he calls for backup and chases");
        }

        void TickCall()
        {
            if (!calling || Dead) return;
            // the player has to stay in view for the whole call: slip out of sight and it is dropped
            if (State == GuardState.Calling && !callAnyway && !StillSees()) { CancelCall(); return; }
            callTimer += Time.deltaTime;
            if (callTimer >= callSeconds)
            {
                calling = false;
                HeistManager.Instance?.CallBackup(this, target && target.Custody == CustodyState.Free ? target : null);          // (he marks the player he spotted, or the one who shot him, and nobody else; the call for a body marks nobody)
                if (State == GuardState.Calling)
                {
                    if (callAnyway) { callAnyway = false; AfterGetUpCall(); return; }
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

        /// <summary>
        /// He was knocked over and is on his feet again: he radios for backup where he stands, for <see cref="callSeconds"/> and whether or not he sees anybody (the owner, 2026-10-20: "Guards that are hit should call for backup upon
        /// getting up"). It is the call of a sighting (the ring over his head, another blow cuts it); at the end the officers are sent to him (<see cref="AfterGetUpCall"/>).
        /// </summary>
        void BeginGetUpCall()
        {
            calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = true;
            target = hitBy;
            if (hitBy) lastSeenPos = hitBy.transform.position;
            State = GuardState.Calling;
            agent.isStopped = true;
            Debug.Log($"{name} got up and is calling for backup");
        }

        /// <summary>The call he made on getting up is through (backup is on the way): he goes after the player who hit him, or to where the blow came from.</summary>
        void AfterGetUpCall()
        {
            if (hitBy && hitBy.Custody == CustodyState.Free)
            {
                target = hitBy;
                State = GuardState.Chase;
                unseenFor = 0f;
                agent.isStopped = false;
                agent.speed = chaseSpeed;
                agent.SetDestination(hitBy.transform.position);
            }
            else
            {
                State = GuardState.Investigate;
                agent.isStopped = false;
                agent.speed = investigateSpeed;
                agent.SetDestination(noiseTarget);
                waitUntil = 0;
            }
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
            ScanForPlayers(true);          // (the owner, 2026-10-24: a second player he notices while he chases is marked as well; the others are not his business)
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
            TryGrab(target);
        }

        /// <summary>
        /// Can he lay hands on this player? A wall, a shut door or any other solid thing that is fixed in the building between the two of them says no (the owner, 2026-10-18: "Players can be pinned through walls at times":
        /// catching used to be a flat distance only, so an officer on the other side of a thin partition could pin somebody through it). The line runs from his chest to theirs; things that move (a crate, another body) do not count.
        /// </summary>
        public bool HasReach(WobblyPlayer p)
        {
            if (!p) return false;
            var from = transform.position + Vector3.up * 0.9f;
            var to = p.transform.position + Vector3.up * 0.9f;
            var d = to - from;
            float dist = d.magnitude;
            if (dist < 0.3f) return true;
            var hits = Physics.RaycastAll(from, d / dist, dist, sightBlockers, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                var c = h.collider;
                if (!c || c.transform.IsChildOf(transform) || c.transform.IsChildOf(p.transform)) continue;
                if (c.attachedRigidbody != null) continue;                                                    // (a crate, a body: it moves, it is not a wall)
                return false;
            }
            return true;
        }

        /// <summary>Is the player within his arm's reach, with nothing solid between them? Then he pins them (host / offline) and starts to hold them down. True when he got hold of them.</summary>
        public bool TryCatch(WobblyPlayer p)
        {
            if (!p || FlatDistance(p.transform) > catchRadius || !HasReach(p)) return false;
            if (HeistManager.Instance == null || !HeistManager.Instance.Pin(p, this)) return false;
            BeginHold(p);
            return true;
        }

        /// <summary>
        /// The grab (the owner, 2026-10-23: "Guards should not pin so quickly in melee range, it should take 2 seconds of being next to them"): while the player is within his arm's reach (the catch radius, nothing solid between)
        /// a clock fills and he turns to face them; it drains twice as fast when they get away, and when it holds <see cref="CustodyRules.GrabSeconds"/> he pins them (<see cref="TryCatch"/>). True when he got hold of them.
        /// </summary>
        bool TryGrab(WobblyPlayer p)
        {
            if (p != grabFor) { grabFor = p; grabHeld = 0f; }
            bool inReach = p && FlatDistance(p.transform) <= catchRadius && HasReach(p);
            grabHeld = CustodyRules.GrabAfter(grabHeld, inReach, Time.deltaTime);
            if (inReach) FaceTarget();
            if (!CustodyRules.GrabDone(grabHeld)) return false;
            grabHeld = 0f;
            GrabsDone++;
            return TryCatch(p);
        }

        /// <summary>Tests: how long the guard has held on to the player he is grabbing (0 to <see cref="CustodyRules.GrabSeconds"/>), and how many times the clock has run out (the pin itself can be refused: god mode, a player just freed).</summary>
        public float GrabHeld => grabHeld;
        public int GrabsDone { get; private set; }

        WobblyPlayer FindVisibleFreePlayer()
        {
            var hm = HeistManager.Instance;
            foreach (var p in WobblyPlayerRegistry.All)
                if (p && p.Custody == CustodyState.Free && (hm == null || hm.IsMarked(p)) && CanSee(p)) return p;          // (a MARKED player: one who was spotted; the owner, 2026-10-24)
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

        float stuckFor;

        void UpdatePatrol()
        {
            if (roam == null && (patrolPoints == null || patrolPoints.Length == 0)) return;
            // a door locked from the security computer can stop a patrol: after 3 s of getting nowhere he gives up on that leg
            if (!agent.pathPending && agent.remainingDistance > agent.stoppingDistance + 1.5f && agent.velocity.sqrMagnitude < 0.01f) stuckFor += Time.deltaTime; else stuckFor = 0f;
            if (stuckFor > 3f) { stuckFor = 0f; GoNextPatrol(); return; }
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                if (stationary) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(postForward), 3f * Time.deltaTime);
                if (waitUntil == 0)
                {
                    waitUntil = Time.time + 1.2f;
                    // a donut break: now and then an unarmed guard stops at his stop to eat one
                    if (GuardRules.DonutBreak(DonutRollOverride >= 0f ? DonutRollOverride : Random.value, Armed, IsBoss, stationary)) { waitUntil = Time.time + GuardRules.DonutSeconds; eating = true; }
                }
                if (Time.time >= waitUntil) { waitUntil = 0; eating = false; GoNextPatrol(); }
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

        [Tooltip("Spotting takes time, and the further away a player is the longer. A guard's notice of a player is a meter: while the player is in his sight it fills by 1 / (spotBase + spotPerMetre x metres) of the meter every second (14 m = about 1.8 s for the whole meter, 40 m = about 5 s), so the closer the player the faster, whichever way he got there; when it is full the guard has noticed him. Out of his sight the meter holds for a moment (GuardRules.NoticeHoldSeconds) and then fades twice as fast as it filled (GuardRules.NoticeStep).")]
        public float spotBase = GuardRules.SpotBase, spotPerMetre = GuardRules.SpotPerMetre;

        /// <summary>One guard's notice of one player: how full the meter is (0 to 1) and how long the player has been out of his sight in a row.</summary>
        sealed class NoticeMeter { public float progress, unseenFor; }
        readonly System.Collections.Generic.Dictionary<WobblyPlayer, NoticeMeter> noticed = new System.Collections.Generic.Dictionary<WobblyPlayer, NoticeMeter>();
        readonly System.Collections.Generic.List<WobblyPlayer> noticeKeys = new System.Collections.Generic.List<WobblyPlayer>();

        /// <summary>How far this guard's notice of a player has got: 0 (nothing) to 1 (he has noticed him: his radio call begins and the meter starts again from 0).</summary>
        public float NoticeProgress(WobblyPlayer p) => p && noticed.TryGetValue(p, out var m) ? m.progress : 0f;

        /// <summary>A test hook: he forgets everything he had noticed.</summary>
        public void ForgetNotices() => noticed.Clear();

        /// <summary>
        /// Looks for players (every frame, host / offline, while he is not calling, chasing or holding somebody down) and keeps his notice meter of each (<see cref="GuardRules.NoticeStep"/>): it fills while <see cref="CanSee"/> says the player is in his
        /// sight, the faster the closer the player is (the distance on the floor, the one the cone is measured in), and when it is full he starts his radio call (<see cref="StartCall"/>). Each guard keeps his own meters and nothing here depends on how many
        /// other guards there are.
        /// </summary>
        void ScanForPlayers(bool chasing = false)
        {
            float dt = Time.deltaTime;
            float factor = IsBackup ? GuardRules.BackupSpotFactor : 1f;
            float far = sightRange * Mathf.Max(1f, longSightFactor);
            var hm = HeistManager.Instance;
            if (chasing && hm == null) return;
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (!p) continue;
                if (chasing && (hm.IsMarked(p) || p.Custody != CustodyState.Free)) continue;          // (chasing: only a player who is not marked yet can be noticed now)
                bool inSight = CanSee(p);
                if (!noticed.TryGetValue(p, out var meter))
                {
                    if (!inSight) continue;                              // (nothing noticed, so nothing to fade)
                    meter = new NoticeMeter();
                    noticed[p] = meter;
                }
                var flat = p.transform.position - transform.position; flat.y = 0f;
                float distance = Mathf.Min(flat.magnitude, far);         // (a player who got away is measured no further than he could be seen from)
                meter.progress = GuardRules.NoticeStep(meter.progress, ref meter.unseenFor, inSight, dt, distance, spotBase, spotPerMetre, factor);
                if (meter.progress >= 1f)
                {
                    noticed.Remove(p);
                    if (chasing) { hm.MarkSpotted(p); continue; }          // (he is after somebody already: the second player is marked, nothing else changes)
                    StartCall(p);
                    return;
                }
            }
            if (noticed.Count > 0)
            {
                noticeKeys.Clear();
                foreach (var kv in noticed) if (!kv.Key || kv.Value.progress <= 0f) noticeKeys.Add(kv.Key);
                foreach (var k in noticeKeys) noticed.Remove(k);
            }
        }

        /// <summary>True on the body of an eliminated guard that has already raised the alarm: each body raises it once, whoever sees it first (<see cref="ScanForBodies"/>).</summary>
        public bool BodyAlarmRaised { get; private set; }
        float nextBodyScan;

        /// <summary>
        /// He looks for the bodies of eliminated guards (the owner, 2026-10-23: "Guards can see other guards eliminated bodies and get alerted"): a few times a second (host / offline) while he is on his round or looking into something. A body he sees - inside his
        /// cone (<see cref="GuardRules.InVision"/>, the same one as for a player) with nothing in the way (<see cref="GuardSight"/>) - makes him stop, look at it and radio for backup like for a sighting (<see cref="BeginBodyCall"/>). A guard on duty in the outpost
        /// sees only what is inside his room (<see cref="noticeGate"/>), and the boss does not look.
        /// </summary>
        void ScanForBodies()
        {
            if (IsPuppet || Dead || IsBoss || Time.time < nextBodyScan) return;
            nextBodyScan = Time.time + GuardRules.BodyScanSeconds;
            float far = sightRange * Mathf.Max(1f, longSightFactor);
            var fwd = transform.forward;
            for (int i = 0; i < All.Count; i++)
            {
                var other = All[i];
                if (!other || other == this || !other.Dead || other.BodyAlarmRaised) continue;
                var at = other.transform.position;
                if (noticeGate != null && !noticeGate(at)) continue;
                var flat = at - transform.position;
                if (!GuardRules.InVision(fwd.x, fwd.z, flat.x, flat.z, sightAngle, sightRange, far, longSightAngle)) continue;
                var eye = EyePoint;
                float feet = at.y - GuardRules.FeetBelowMiddle * Mathf.Abs(other.transform.lossyScale.y);
                if (sightLine == null) sightLine = LineIsClear;
                bool seen = GuardRules.SeesBody(eye.x, eye.y, eye.z, at.x, feet, at.z, GuardRules.BodyHeightLying, sightLine);
                if (!GuardRules.BodyAlarms(seen, other.BodyAlarmRaised)) continue;
                BeginBodyCall(other);
                return;
            }
        }

        /// <summary>He has seen the body of an eliminated guard: he stops, faces it and radios for backup as for a sighting - for <see cref="callSeconds"/>, whether or not he sees a player - and when the call is through he goes to look at the body (<see cref="AfterGetUpCall"/>).</summary>
        void BeginBodyCall(GuardAI body)
        {
            body.BodyAlarmRaised = true;                                 // (one alarm per body)
            eating = false;
            calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = true; callOnGetUp = false;
            target = null; hitBy = null;
            noiseTarget = body.transform.position;
            lastSeenPos = noiseTarget;
            State = GuardState.Calling;
            agent.isStopped = true;
            var to = noiseTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(to);
            Debug.Log($"{name} saw the body of {body.name}: he is calling for backup");
        }

        /// <summary>
        /// Where his eyes are: <see cref="GuardRules.EyeAboveMiddle"/> above the middle of his body (times his scale). Every ray of his sight starts here, the ones that cut the cone short on the floor (<see cref="GuardVision"/>) and the ones in
        /// <see cref="CanSee"/>.
        /// </summary>
        public Vector3 EyePoint => transform.position + Vector3.up * (GuardRules.EyeAboveMiddle * Mathf.Abs(transform.lossyScale.y));

        /// <summary>
        /// Is this player in his sight? THE one rule, and it is what the cone on the floor shows (the owner, 2026-10-22): the player is inside the cone (<see cref="GuardRules.InVision"/>: the same angle, range and long beam the cone is drawn with),
        /// and nothing that stays where it is stands in the way (<see cref="HasClearView"/>). Pinned or arrested players are not what he is looking for, and one who hides is seen only from close by (<see cref="hiddenSightRange"/>).
        /// </summary>
        bool CanSee(WobblyPlayer p)
        {
            if (p.Custody != CustodyState.Free) return false; // pinned or arrested players are not what he is looking for
            if (PlayerBuffs.IsInvisible(p)) return false;     // the "invisible" consumable (the owner, 2026-10-25): nothing of him is seen, so his notice meter never fills and no chase starts
            if (noticeGate != null && !noticeGate(p.transform.position)) return false;      // outside the room he guards: he does not see it
            float range = p.IsHidden ? hiddenSightRange : sightRange;
            float far = p.IsHidden ? range : range * Mathf.Max(1f, longSightFactor);       // (down a straight line of sight he sees much further)
            // the cone is the fan he shows on the floor (GuardVision): flat, so standing close to him does not hide you from him
            var flat = p.transform.position - transform.position;
            var fwd = transform.forward;
            if (!GuardRules.InVision(fwd.x, fwd.z, flat.x, flat.z, sightAngle, range, far, longSightAngle)) return false;
            return HasClearView(p);
        }

        readonly RaycastHit[] sightHits = new RaycastHit[64];
        GuardRules.SightLine sightLine;

        /// <summary>
        /// Is there a clear view from his eyes to this player (<see cref="GuardRules.SeesBody"/>)? The lines run to his head, chest and knees, worked out from where his feet are and how tall he is (lower when he crouches), and END at the player: the
        /// first thing a line meets does not have to be him, which is what used to make a crouching player, or one with a crate, another player or the wall behind him on the line, invisible to a guard whose cone he stood in. Only things that stay
        /// where they are stop a line (<see cref="GuardSight.Blocks"/>), the same things that cut the cone short.
        /// </summary>
        bool HasClearView(WobblyPlayer p)
        {
            var eye = EyePoint;
            var at = p.transform.position;
            float scale = Mathf.Abs(p.transform.lossyScale.y);
            float height = GuardRules.BodyHeight * scale * (p.IsCrouching ? Mathf.Clamp(p.crouchHeight, 0.3f, 1f) : 1f);
            float feet = at.y - GuardRules.FeetBelowMiddle * scale;
            if (sightLine == null) sightLine = LineIsClear;
            return GuardRules.SeesBody(eye.x, eye.y, eye.z, at.x, feet, at.z, height, sightLine);
        }

        bool LineIsClear(float ax, float ay, float az, float bx, float by, float bz) =>
            GuardSight.LineClear(new Vector3(ax, ay, az), new Vector3(bx, by, bz), sightBlockers, transform, sightHits);
    }

    /// <summary>
    /// What stops a guard's sight: one rule for the cone he shows on the floor (<see cref="GuardVision"/>) and for the check that decides what he sees (<see cref="GuardAI"/>), so that they cannot disagree (the owner, 2026-10-22). Walls, doors and
    /// furniture that stays where it is stop it. Anything with a rigidbody does not - a loose crate, the loot somebody carries, a player - and neither do the guard's own body and the other officers: guards queueing up in a corridor must not blind each other
    /// (a chase would be given up and a radio call dropped depending on how many of them there are). Trigger volumes (hide spots, the elevator zone) are never in the way: standing in one must not make you invisible.
    /// </summary>
    public static class GuardSight
    {
        /// <summary>Does this collider stop the sight of <paramref name="guard"/>?</summary>
        public static bool Blocks(Collider c, Transform guard)
        {
            if (!c || c.attachedRigidbody != null) return false;                 // (loose things and people move: they are not walls)
            if (guard && c.transform.IsChildOf(guard)) return false;             // (his own body)
            return c.GetComponentInParent<GuardAI>() == null;                    // (nor are the other officers)
        }

        /// <summary>Is the straight line from <paramref name="from"/> to <paramref name="to"/> free of anything that stops <paramref name="guard"/>'s sight? <paramref name="mask"/> is his <see cref="GuardAI.sightBlockers"/>; <paramref name="hits"/> a buffer to cast into.</summary>
        public static bool LineClear(Vector3 from, Vector3 to, int mask, Transform guard, RaycastHit[] hits)
        {
            var d = to - from;
            float dist = d.magnitude;
            if (dist < 0.05f) return true;
            var dir = d / dist;
            int count = Physics.RaycastNonAlloc(from, dir, hits, dist, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (Blocks(hits[i].collider, guard)) return false;
            if (count < hits.Length) return true;
            // (the list was full, so a blocker may have been left off it: walk the line one hit at a time, nearest first)
            var at = from; float left = dist;
            for (int step = 0; step < 64 && left > 0.02f; step++)
            {
                if (!Physics.Raycast(at, dir, out var hit, left, mask, QueryTriggerInteraction.Ignore)) return true;
                if (Blocks(hit.collider, guard)) return false;
                float moved = hit.distance + 0.02f;
                at += dir * moved; left -= moved;
            }
            return true;
        }
    }
}
