using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Turns microphone loudness into NoiseBus events (proximity-chat "guards can hear you").
    /// Feed SetLevel() from whatever voice backend is used (Vivox/Dissonance/Steam voice);
    /// if no backend is wired, enable useLocalMicrophone to sample the default mic directly.
    /// </summary>
    public class VoiceNoise : MonoBehaviour
    {
        public bool useLocalMicrophone;
        [Tooltip("Normalised 0-1 loudness below which speech makes no noise at all.")]
        public float minLevel = 0.01f;
        [Tooltip("How far a guard hears the quietest voice that is still above the microphone gate, in metres.")]
        public float radiusAtGate = 3f;
        [Tooltip("How far a guard hears a shout, in metres. In between, the louder you speak the further it carries.")]
        public float radiusAtShout = 30f;
        public float emitInterval = 0.25f;

        /// <summary>A shout, in dB relative to full scale (RMS). Quieter speech maps linearly in dB from the mic gate up to here.</summary>
        public const float ShoutDb = -14f;

        float level, nextEmit;

        /// <summary>The level currently being turned into noise (0 when silent).</summary>
        public float Level => level;

        /// <summary>
        /// Loudness 0..1 from a microphone RMS (0..1 samples): 0 at the microphone gate, 1 at a shout, linear in decibels
        /// in between (each doubling of volume is +6 dB), so every step louder reaches a bit further.
        /// </summary>
        public static float LevelFromRms(float rms, float gate)
        {
            if (rms <= 1e-6f || gate <= 1e-6f) return 0f;
            float db = 20f * Mathf.Log10(rms), gateDb = 20f * Mathf.Log10(gate);
            return Mathf.Clamp01(Mathf.InverseLerp(gateDb, ShoutDb, db));
        }

        /// <summary>The hearing radius, in metres, a loudness (0..1) carries.</summary>
        public float RadiusFor(float normalised) => Mathf.Lerp(radiusAtGate, radiusAtShout, Mathf.Clamp01(normalised));

        AudioClip mic; string device; int lastPos;
        readonly float[] buf = new float[256];

        public void SetLevel(float normalised) => level = Mathf.Clamp01(normalised);

        void Start()
        {
            if (!useLocalMicrophone || Microphone.devices.Length == 0) return;
            device = Microphone.devices[0];
            mic = Microphone.Start(device, true, 1, 16000);
        }

        void Update()
        {
            if (mic != null) SampleMic();
            if (level < minLevel || Time.time < nextEmit) return;
            nextEmit = Time.time + emitInterval;
            NoiseBus.Emit(transform.position, RadiusFor(level), gameObject);
        }

        void SampleMic()
        {
            int pos = Microphone.GetPosition(device) - buf.Length;
            if (pos < 0) return;
            mic.GetData(buf, pos);
            float sum = 0; foreach (var s in buf) sum += s * s;
            level = Mathf.Clamp01(Mathf.Sqrt(sum / buf.Length) * 6f);
        }

        void OnDestroy() { if (device != null) Microphone.End(device); }
    }
}
