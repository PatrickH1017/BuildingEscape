using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// One remote speaker. Decoded samples are queued here and played through an AudioSource by writing straight
    /// into the audio stream (OnAudioFilterRead), resampled to the output rate.
    ///
    /// How loud the voice is, and from which side, is decided HERE, from <see cref="VoiceRules"/> (<see cref="SetMix"/>): the engine's own 3D
    /// falloff and panning never reach a stream that is written inside OnAudioFilterRead (measured on 2026-10-17: the same loudness at 4, 11 and
    /// 21 m, so a teammate could be heard across a floor), so the source is plain 2D at full volume and the distance and side are applied to the samples.
    /// In the lobby (no bodies) the mix is simply full volume in the middle.
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

        // the mix: set on the main thread (a float write is atomic), read on the audio thread, which glides towards it so that a change is never a click
        float targetL, targetR, curL, curR, smooth = 0.002f;
        float outPeak, outPeakL, outPeakR;

        // for tests / HUD
        public long samplesQueued, samplesPlayed, packets;
        public float lastLevel;
        public float lastPacketTime;

        /// <summary>The gain this voice is being played at (1 = full, 0 = out of range).</summary>
        public float Gain { get; private set; }
        /// <summary>The loudest sample this voice wrote to the output lately, after its mix (what a listener really gets from it).</summary>
        public float OutPeak => outPeak;
        /// <summary>The same per ear: a voice to the right is louder in the right ear.</summary>
        public float OutPeakLeft => outPeakL;
        public float OutPeakRight => outPeakR;

        AudioSource source;
        AudioClip keepAlive;

        public bool Playing => source != null && source.isPlaying;

        void Awake()
        {
            source = GetComponent<AudioSource>();
            outRate = AudioSettings.outputSampleRate;
            smooth = 1f - Mathf.Exp(-1f / (0.012f * outRate));     // about 12 ms to follow a change of the mix
            // a looping 1-sample clip keeps the source (and its filter callback) alive
            keepAlive = AudioClip.Create("voice", 1, 1, outRate, false);
            keepAlive.SetData(new[] { 0f }, 0);
            source.clip = keepAlive;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;      // (plain 2D: the distance and the side are in the samples, see the class comment)
            source.volume = 1f;
            source.Play();
        }

        void OnDestroy() { if (keepAlive) Destroy(keepAlive); }   // an AudioClip is not freed with the object that plays it

        /// <summary>Sets how loud the voice is (<paramref name="gain"/>, 1 = full, 0 = silent) and from which side it comes (<paramref name="pan"/>, -1 left .. 1 right).</summary>
        public void SetMix(float gain, float pan)
        {
            Gain = gain;
            targetL = VoiceRules.LeftGain(gain, pan);
            targetR = VoiceRules.RightGain(gain, pan);
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
            float tl = targetL, tr = targetR, peakL = 0f, peakR = 0f;
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
                    curL += (tl - curL) * smooth;
                    curR += (tr - curR) * smooth;
                    float l = s * curL, r = s * curR;
                    if (channels == 1) data[f] = 0.5f * (l + r);
                    else
                    {
                        int i = f * channels;
                        data[i] = l;
                        data[i + 1] = r;
                        for (int c = 2; c < channels; c++) data[i + c] = 0f;   // (the voice stays in the front pair)
                    }
                    float al = Mathf.Abs(l), ar = Mathf.Abs(r);
                    if (al > peakL) peakL = al;
                    if (ar > peakR) peakR = ar;
                }
            }
            outPeakL = peakL; outPeakR = peakR;
            outPeak = Mathf.Max(peakL, peakR);
        }
    }
}
