using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape
{
    public enum GuardState { Patrol, Investigate, Chase, Stunned }

    /// <summary>
    /// Security guard / robot: patrols waypoints, investigates noise, chases players it sees.
    /// Hidden players are only caught at very short range.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class GuardAI : MonoBehaviour
    {
        public Transform[] patrolPoints;
        public float walkSpeed = 2.2f, investigateSpeed = 3f, chaseSpeed = 4.6f;
        public float sightRange = 14f, sightAngle = 65f, hiddenSightRange = 1.5f;
        public float hearingMultiplier = 1f;
        public float catchRadius = 1.2f;
        public float investigateWait = 3f;
        public float loseSightSeconds = 4f;
        public LayerMask sightBlockers = ~0;

        public GuardState State { get; private set; } = GuardState.Patrol;

        NavMeshAgent agent;
        int patrolIndex;
        float waitUntil, stunUntil, lastSeen;
        Vector3 noiseTarget;
        WobblyPlayer target;

        void Awake() => agent = GetComponent<NavMeshAgent>();
        void OnEnable() => NoiseBus.OnNoise += OnNoise;
        void OnDisable() => NoiseBus.OnNoise -= OnNoise;
        void Start() { GoNextPatrol(); }

        void OnNoise(NoiseEvent n)
        {
            if (State == GuardState.Stunned || State == GuardState.Chase) return;
            float dist = Vector3.Distance(transform.position, n.Position);
            if (dist > n.Loudness * hearingMultiplier) return;
            noiseTarget = n.Position;
            State = GuardState.Investigate;
            agent.speed = investigateSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0;
        }

        public void Stun(float seconds, GameObject by = null)
        {
            State = GuardState.Stunned;
            stunUntil = Time.time + seconds;
            agent.isStopped = true;
            if (by) noiseTarget = by.transform.position;
        }

        void Update()
        {
            switch (State)
            {
                case GuardState.Stunned:
                    if (Time.time >= stunUntil)
                    {
                        agent.isStopped = false;
                        State = GuardState.Investigate;
                        agent.speed = investigateSpeed;
                        agent.SetDestination(noiseTarget);
                    }
                    return;
                case GuardState.Patrol: UpdatePatrol(); break;
                case GuardState.Investigate: UpdateInvestigate(); break;
                case GuardState.Chase: UpdateChase(); break;
            }
            if (State != GuardState.Chase) ScanForPlayers();
        }

        void UpdatePatrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                if (waitUntil == 0) waitUntil = Time.time + 1.2f;
                if (Time.time >= waitUntil) { waitUntil = 0; GoNextPatrol(); }
            }
        }

        void GoNextPatrol()
        {
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

        void UpdateChase()
        {
            if (!target) { State = GuardState.Patrol; GoNextPatrol(); return; }
            agent.speed = chaseSpeed;
            agent.SetDestination(target.transform.position);
            if (CanSee(target)) lastSeen = Time.time;
            if (Vector3.Distance(transform.position, target.transform.position) <= catchRadius)
                HeistManager.Instance?.NotifyPlayerCaught(target);
            if (Time.time - lastSeen > loseSightSeconds)
            {
                noiseTarget = target.transform.position;
                target = null; State = GuardState.Investigate; agent.speed = investigateSpeed;
                agent.SetDestination(noiseTarget);
            }
        }

        void ScanForPlayers()
        {
            foreach (var p in WobblyPlayerRegistry.All)
            {
                if (CanSee(p)) { target = p; lastSeen = Time.time; State = GuardState.Chase; return; }
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
