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
            OnNoise?.Invoke(new NoiseEvent(position, loudness, source));
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
