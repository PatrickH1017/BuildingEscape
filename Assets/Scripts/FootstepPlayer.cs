using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Plays a footstep every stride of walking. Players hear their own steps flat; guards' steps are 3D
    /// so you can hear them coming. Added automatically to players (WobblyPlayer) and guards (GuardVisual).
    /// </summary>
    public class FootstepPlayer : MonoBehaviour
    {
        public float stride = 1.5f;
        public float volume = 0.45f;

        GuardAI ai;
        bool guard;
        Vector3 last;
        float acc;

        void Start()
        {
            ai = GetComponent<GuardAI>();
            guard = ai != null;
            if (guard) { stride = 1.3f; volume = 0.8f; }
            last = transform.position;
        }

        void Update()
        {
            var p = transform.position;
            var d = p - last; last = p; d.y = 0f;
            // Physics moves the player in steps, so per-frame speed is spiky: just add up distance travelled.
            if (d.magnitude > 2.5f) { acc = 0f; return; }       // teleported (new floor)
            if (guard && ai.State == GuardState.Stunned) { acc = 0f; return; }
            if (!guard && !Grounded()) return;
            acc += d.magnitude;
            if (acc >= stride) { acc -= stride; Step(); }
        }

        bool Grounded() => Physics.Raycast(transform.position, Vector3.down, 2.2f, ~0, QueryTriggerInteraction.Ignore);

        void Step()
        {
            var cat = GameAudio.Catalog;
            if (cat == null) return;
            var clip = GameAudio.Pick(guard ? cat.stepsGuard : cat.stepsPlayer);
            GameAudio.Play(clip, transform.position + Vector3.down * 0.9f, volume, Random.Range(0.92f, 1.08f), guard);
        }
    }
}
