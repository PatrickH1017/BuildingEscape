using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// One remote speaker. Decoded samples are queued here and played through an AudioSource by writing straight
    /// into the audio stream (OnAudioFilterRead), resampled to the output rate. With a position the voice is 3D
    /// (it comes from the teammate's character); without one (the lobby) it is plain stereo.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceVoice : MonoBehaviour
    {
        public ulong speakerId;
        public int sampleRate = 16000;

        // ring of mono samples at sampleRate; written on the main thread, read on the audio thread
        float[] ring = new float[16000 * 2];
        int write, read;               // absolute counters; (write - read) = queued samples
        readonly object gate = new();
        double frac;
        int outRate = 48000;
        float lastSample;

        // for tests / HUD
        public long samplesQueued, samplesPlayed, packets;
        public float lastLevel;
        public float lastPacketTime;

        AudioSource source;

        public bool Playing => source != null && source.isPlaying;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            outRate = AudioSettings.outputSampleRate;
            // a looping 1-sample clip keeps the source (and its filter callback) alive
            var clip = AudioClip.Create("voice", 1, 1, outRate, false);
            clip.SetData(new[] { 0f }, 0);
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.minDistance = 2.5f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
        }

        public void SetSpatial(bool spatial, float volume)
        {
            source.spatialBlend = spatial ? 1f : 0f;
            source.volume = volume;
        }

        public void Push(float[] pcm, int count, int rate)
        {
            sampleRate = rate;
            float peak = 0f;
            lock (gate)
            {
                // too much queued (a stall): drop the oldest so latency does not build up
                int queued = write - read;
                if (queued + count > ring.Length - 1)
                    read = write - Mathf.Max(0, ring.Length / 4);
                for (int i = 0; i < count; i++)
                {
                    float v = pcm[i];
                    ring[write % ring.Length] = v;
                    write++;
                    float a = Mathf.Abs(v);
                    if (a > peak) peak = a;
                }
            }
            lastLevel = peak;
            samplesQueued += count;
            packets++;
            lastPacketTime = Time.unscaledTime;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            double step = (double)sampleRate / outRate;
            int frames = data.Length / channels;
            lock (gate)
            {
                for (int f = 0; f < frames; f++)
                {
                    int queued = write - read;
                    float s = 0f;
                    if (queued > 1)
                    {
                        float a = ring[read % ring.Length];
                        float b = ring[(read + 1) % ring.Length];
                        s = Mathf.Lerp(a, b, (float)frac);
                        frac += step;
                        while (frac >= 1.0) { frac -= 1.0; read++; samplesPlayed++; }
                    }
                    else s = lastSample * 0.9f; // run dry: fade instead of clicking
                    lastSample = s;
                    for (int c = 0; c < channels; c++) data[f * channels + c] = s;
                }
            }
        }
    }
}