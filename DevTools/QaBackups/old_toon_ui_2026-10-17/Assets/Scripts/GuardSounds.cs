using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What an officer sounds like besides his footsteps: the keys on his belt jingle as he moves (faster and louder when he runs),
    /// and his walkie-talkie crackles with static every now and then. When he radios for backup the radio squelches.
    /// Purely local sound, nothing is sent over the network: every machine hears the guard it sees (a remote client's guards are
    /// puppets whose position and state arrive from the host, which is all this needs). Added to every guard by GuardVisual.
    /// </summary>
    [RequireComponent(typeof(GuardAI))]
    public sealed class GuardSounds : MonoBehaviour
    {
        [Tooltip("Metres walked between key jingles, walking and running (about every 3 s when walking, 1.3 s when running).")]
        public float walkStride = 7f, runStride = 6f;
        [Tooltip("Seconds between radio static bursts: the first burst comes after a shorter random wait.")]
        public Vector2 staticEvery = new Vector2(7f, 24f);
        public Vector2 firstStaticAfter = new Vector2(4f, 18f);
        public float keysVolume = 0.5f, staticVolume = 0.45f, callVolume = 0.7f;
        [Tooltip("How far away each sound can be heard, in metres (keys are quiet, a radio carries further).")]
        public float keysRange = 16f, staticRange = 22f;

        const float RunSpeed = 3.6f;      // above this an officer counts as running (walk 2.2, investigate 3, chase 4.6)

        GuardAI ai;
        SynthRandom rng;
        Vector3 last;
        float walked, nextStride, nextStatic;
        bool wasCalling;

        void Start()
        {
            ai = GetComponent<GuardAI>();
            // every officer gets his own sequence, the same on every machine for the same guard
            rng = new SynthRandom(unchecked(ai.NetId * 7919 + 31));
            last = transform.position;
            nextStride = 1f;
            nextStatic = Time.time + rng.Range(firstStaticAfter.x, firstStaticAfter.y);
            wasCalling = ai.IsCalling;
        }

        void Update()
        {
            if (ai == null) return;
            Keys();
            Radio();
        }

        void Keys()
        {
            var p = transform.position;
            var d = p - last; last = p; d.y = 0f;
            float step = d.magnitude;
            if (step > 2.5f) { walked = 0f; return; }                    // teleported (a new floor)
            if (ai.Dead || ai.State == GuardState.Stunned) { walked = 0f; return; }
            walked += step;
            if (walked < nextStride) return;
            walked = 0f;
            bool running = ai.CurrentSpeed > RunSpeed;
            nextStride = (running ? runStride : walkStride) * rng.Range(0.75f, 1.3f);   // never a metronome
            GameAudio.PlaySound(running ? SoundId.KeysRun : SoundId.Keys, p + Vector3.up * 0.9f,
                                keysVolume * rng.Range(0.8f, 1f) * (running ? 1.2f : 1f), rng.Range(0.92f, 1.08f), true, keysRange, 2f, rng.Int(64));
        }

        void Radio()
        {
            bool calling = ai.IsCalling;
            if (calling && !wasCalling && !ai.Dead)                      // he just keyed the radio to call for backup
            {
                GameAudio.PlaySound(SoundId.RadioCall, transform.position + Vector3.up * 1.4f, callVolume, rng.Range(0.95f, 1.05f), true, staticRange + 6f, 3f, rng.Int(64));
                nextStatic = Mathf.Max(nextStatic, Time.time + 4f);
            }
            wasCalling = calling;

            if (Time.time < nextStatic) return;
            nextStatic = Time.time + rng.Range(staticEvery.x, staticEvery.y);
            if (ai.Dead || calling) return;
            GameAudio.PlaySound(SoundId.RadioStatic, transform.position + Vector3.up * 1.4f, staticVolume * rng.Range(0.7f, 1f), rng.Range(0.9f, 1.1f), true, staticRange, 2.5f, rng.Int(64));
        }
    }
}
