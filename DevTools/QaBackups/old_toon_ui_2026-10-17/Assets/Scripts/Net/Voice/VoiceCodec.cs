using System;
using Steamworks;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>Where compressed voice packets come from (the local microphone).</summary>
    public interface IVoiceInput : IDisposable
    {
        /// <summary>Codec id sent with every packet so receivers pick the matching decoder.</summary>
        byte Codec { get; }
        /// <summary>Start / stop capturing (stopping lets the microphone go).</summary>
        void SetActive(bool on);
        /// <summary>Next packet, or false when none is ready. Packets are small enough for one network message.</summary>
        bool TryRead(byte[] buffer, out int length);
    }

    /// <summary>Turns a packet back into mono float samples.</summary>
    public interface IVoiceDecoder
    {
        int SampleRate { get; }
        /// <summary>Decodes into <paramref name="pcm"/>; returns the number of samples (0 if the packet was unusable).</summary>
        int Decode(byte[] data, int length, float[] pcm);
    }

    public static class VoiceCodecs
    {
        public const byte Steam = 0;
        public const byte Pcm16 = 1;

        public static IVoiceDecoder DecoderFor(byte codec)
        {
            switch (codec)
            {
                case Steam: return SteamVoiceDecoder.Instance;
                case Pcm16: return Pcm16Decoder.Instance;
                default: return null;
            }
        }
    }

    // ------------------------------------------------------------------ Steam voice (Opus, ~3-6 KB/s)

    public sealed class SteamVoiceInput : IVoiceInput
    {
        bool recording;
        public byte Codec => VoiceCodecs.Steam;

        public void SetActive(bool on)
        {
            if (on == recording) return;
            recording = on;
            var steam = SteamService.Instance;
            if (steam == null || !steam.Ready) return;       // (SteamService shuts Steam down in OnApplicationQuit, before VoiceChat is destroyed: nothing left to stop)
            if (on) SteamUser.StartVoiceRecording(); else SteamUser.StopVoiceRecording();
        }

        public bool TryRead(byte[] buffer, out int length)
        {
            length = 0;
            if (!recording) return false;
            if (SteamUser.GetAvailableVoice(out uint available) != EVoiceResult.k_EVoiceResultOK || available == 0) return false;
            if (SteamUser.GetVoice(true, buffer, (uint)buffer.Length, out uint written) != EVoiceResult.k_EVoiceResultOK || written == 0) return false;
            length = (int)written;
            return true;
        }

        public void Dispose() { SetActive(false); }
    }

    public sealed class SteamVoiceDecoder : IVoiceDecoder
    {
        public static readonly SteamVoiceDecoder Instance = new();
        readonly byte[] raw = new byte[48000 * 2];

        public int SampleRate => (int)SteamUser.GetVoiceOptimalSampleRate();

        public int Decode(byte[] data, int length, float[] pcm)
        {
            var r = SteamUser.DecompressVoice(data, (uint)length, raw, (uint)raw.Length, out uint written, (uint)SampleRate);
            if (r != EVoiceResult.k_EVoiceResultOK) return 0;
            int n = Mathf.Min((int)written / 2, pcm.Length);
            for (int i = 0; i < n; i++) pcm[i] = BitConverter.ToInt16(raw, i * 2) / 32768f;
            return n;
        }
    }

    // ------------------------------------------------------------------ plain PCM (fallback without Steam, and tests)

    /// <summary>Raw 16 kHz 16-bit mono: ~32 KB/s, so only a fallback. Cut into 20 ms packets (640 bytes).</summary>
    public sealed class MicrophoneVoiceInput : IVoiceInput
    {
        public const int Rate = 16000;
        public const int FrameSamples = 320;
        public byte Codec => VoiceCodecs.Pcm16;

        string device;
        AudioClip clip;
        int readPos;

        public void SetActive(bool on)
        {
            if (on && clip == null)
            {
                if (Microphone.devices.Length == 0) return;
                device = Microphone.devices[0];
                clip = Microphone.Start(device, true, 1, Rate);
                readPos = 0;
            }
            else if (!on && clip != null)
            {
                Microphone.End(device);
                clip = null;
            }
        }

        readonly float[] frame = new float[FrameSamples];

        public bool TryRead(byte[] buffer, out int length)
        {
            length = 0;
            if (clip == null) return false;
            int pos = Microphone.GetPosition(device);
            int available = (pos - readPos + clip.samples) % clip.samples;
            if (available < FrameSamples) return false;
            clip.GetData(frame, readPos);
            readPos = (readPos + FrameSamples) % clip.samples;
            return Pcm16Decoder.Encode(frame, FrameSamples, buffer, out length);
        }

        public void Dispose() { SetActive(false); }
    }

    public sealed class Pcm16Decoder : IVoiceDecoder
    {
        public static readonly Pcm16Decoder Instance = new();
        public int SampleRate => MicrophoneVoiceInput.Rate;

        public int Decode(byte[] data, int length, float[] pcm)
        {
            int n = Mathf.Min(length / 2, pcm.Length);
            for (int i = 0; i < n; i++) pcm[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
            return n;
        }

        public static bool Encode(float[] samples, int count, byte[] buffer, out int length)
        {
            length = count * 2;
            if (length > buffer.Length) return false;
            for (int i = 0; i < count; i++)
            {
                short s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
                buffer[i * 2] = (byte)(s & 0xFF);
                buffer[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return true;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>Dev only: a steady sine tone as a stand-in microphone, so voice can be tested without a person.</summary>
    public sealed class ToneVoiceInput : IVoiceInput
    {
        readonly float frequency, amplitude;
        float phase, nextFrame;
        bool on;
        public byte Codec => VoiceCodecs.Pcm16;

        public ToneVoiceInput(float frequency = 440f, float amplitude = 0.4f) { this.frequency = frequency; this.amplitude = amplitude; }

        public void SetActive(bool value) { on = value; nextFrame = Time.unscaledTime; }

        readonly float[] frame = new float[MicrophoneVoiceInput.FrameSamples];

        public bool TryRead(byte[] buffer, out int length)
        {
            length = 0;
            if (!on || Time.unscaledTime < nextFrame) return false;
            nextFrame += MicrophoneVoiceInput.FrameSamples / (float)MicrophoneVoiceInput.Rate;
            if (nextFrame < Time.unscaledTime - 0.1f) nextFrame = Time.unscaledTime;
            float step = 2f * Mathf.PI * frequency / MicrophoneVoiceInput.Rate;
            for (int i = 0; i < frame.Length; i++) { frame[i] = Mathf.Sin(phase) * amplitude; phase += step; }
            return Pcm16Decoder.Encode(frame, frame.Length, buffer, out length);
        }

        public void Dispose() { }
    }
#endif
}