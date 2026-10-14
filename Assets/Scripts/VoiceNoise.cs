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
        public float threshold = 0.15f;
        public float maxRadius = 18f;
        public float emitInterval = 0.25f;

        float level, nextEmit;
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
            if (level < threshold || Time.time < nextEmit) return;
            nextEmit = Time.time + emitInterval;
            NoiseBus.Emit(transform.position, Mathf.Lerp(2f, maxRadius, level), gameObject);
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
