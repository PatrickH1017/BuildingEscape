using System;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Global noise channel. Anything loud (voice, crashes, thrown mugs, loot breaking)
    /// publishes a NoiseEvent; guards subscribe and decide by distance + loudness.
    /// </summary>
    public static class NoiseBus
    {
        public static event Action<NoiseEvent> OnNoise;

        public static void Emit(Vector3 position, float loudness, GameObject source = null)
        {
            if (loudness <= 0f) return;
            // On a remote client the guards are puppets; the host hears it instead.
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestNoise(position, loudness); return; }
            OnNoise?.Invoke(new NoiseEvent(position, loudness, source));
        }

        /// <summary>Raises the noise on this machine only (the host, for a noise a client reported).</summary>
        public static void EmitLocal(Vector3 position, float loudness)
        {
            if (loudness > 0f) OnNoise?.Invoke(new NoiseEvent(position, loudness, null));
        }
    }

    public readonly struct NoiseEvent
    {
        public readonly Vector3 Position;
        /// <summary>Radius in metres at which a guard with default hearing notices it.</summary>
        public readonly float Loudness;
        public readonly GameObject Source;
        public NoiseEvent(Vector3 p, float l, GameObject s) { Position = p; Loudness = l; Source = s; }
    }
}
