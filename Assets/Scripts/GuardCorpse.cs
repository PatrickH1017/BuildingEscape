using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A guard who is down for good lies there for <see cref="GuardRules.CorpseSeconds"/>, then sinks away and is gone (the owner, 2026-10-18: "Guards bodies should de-spawn").
    /// Every machine does it by itself from the flag it has (the host's from the blow, a puppet's from the snapshot), so it needs no message; a machine that joins late sees a body for its own
    /// full time. Added by <see cref="GuardAI"/> when it finds the guard dead. The boss never goes.
    /// </summary>
    public sealed class GuardCorpse : MonoBehaviour
    {
        /// <summary>Bodies that have gone since the game started (for the tests), and a test hook that shortens the wait (a negative value = the real one).</summary>
        public static int Gone;
        public static float WaitOverride = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Gone = 0; WaitOverride = -1f; }

        float since;
        Vector3 start;
        bool began;

        void Update()
        {
            since += Time.deltaTime;
            float wait = WaitOverride >= 0f ? WaitOverride : GuardRules.CorpseSeconds;
            float sink = GuardRules.CorpseSink(since, wait);
            if (sink <= 0f) return;
            if (!began) { began = true; start = transform.position; Release(); }
            transform.position = start + Vector3.down * (0.45f * sink);
            if (sink >= 1f) { Gone++; Destroy(gameObject); }
        }

        /// <summary>He sinks through the floor: nothing holds him up or blocks anybody on his way down.</summary>
        void Release()
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent) agent.enabled = false;
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        }
    }
}
