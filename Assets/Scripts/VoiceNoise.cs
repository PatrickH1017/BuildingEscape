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
        [Tooltip("Normalised 0-1 level above which speech makes noise.")]
        public float hearThreshold = 0.08f;
        [Tooltip("Radius, in metres, at which a guard hears a quiet voice at the threshold...")]
        public float quietRadius = 7f;
        [Tooltip("...and a shout. Talking normally lands in between, so a chatty team is a loud team.")]
        public float shoutRadius = 26f;
        public float emitInterval = 0.25f;

        float level, nextEmit;

        /// <summary>The level currently being turned into noise (0 when silent).</summary>
        public float Level => level;
        /// <summary>The hearing radius a given level carries.</summary>
        public float RadiusFor(float normalised) => Mathf.Lerp(quietRadius, shoutRadius, Mathf.Clamp01(normalised));
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
            if (level < hearThreshold || Time.time < nextEmit) return;
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
