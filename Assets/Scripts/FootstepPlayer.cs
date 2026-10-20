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

        /// <summary>How far a teammate's footsteps carry, and the distance inside which they are at full volume (the owner, 2026-10-18: "footsteps volume should have a radius"): a player hears his own steps flat, a teammate's only from where the teammate walks.</summary>
        public const float TeammateRange = 16f, TeammateNear = 2.5f;

        /// <summary>Is a step played flat (the step of the player the camera follows) or in 3D (a guard's, a teammate's: heard near, silent far away)?</summary>
        public static bool IsFlat(bool guard, bool ownPlayer) => !guard && ownPlayer;

        GuardAI ai;
        WobblyPlayer player;
        Backpack pack;
        bool guard;
        Vector3 last;
        float acc;

        void Start()
        {
            ai = GetComponent<GuardAI>();
            player = GetComponent<WobblyPlayer>();
            pack = GetComponent<Backpack>();
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
            // a crouched step is soft and a little lower
            bool soft = player && player.IsCrouching;
            bool flat = IsFlat(guard, pack == null || pack.Viewed);
            GameAudio.Play(clip, transform.position + Vector3.down * 0.9f, soft ? volume * 0.3f : volume,
                soft ? Random.Range(0.85f, 0.95f) : Random.Range(0.92f, 1.08f), !flat,
                guard ? GameAudio.DefaultMaxDistance : TeammateRange, guard ? GameAudio.DefaultMinDistance : TeammateNear);
        }
    }
}
