using System;

namespace BuildingEscape.Net
{
    /// <summary>
    /// How far a player's voice carries. The owner's rule (2026-10-17): players hear each other only from a certain distance; walkie-talkies, bought at
    /// the vendor later, will cover distance. Pure arithmetic, no Unity types (so it is tested with the other pure rules):
    ///  - the playback (<c>VoiceChat</c> / <c>VoiceVoice</c>) turns a speaker's distance into a volume with <see cref="GainFor"/> and a side with <see cref="PanFor"/>;
    ///  - the HOST decides who is sent a voice packet at all (<see cref="ShouldRelay"/>): nobody receives a voice from beyond its range;
    ///  - a radio changes only <see cref="RouteFor"/> (two players who both carry one hear each other at any distance), nothing else has to move.
    /// There are no positions in the lobby, so there everybody hears everybody (plain stereo); in a run a voice with no body is not heard.
    /// </summary>
    public static class VoiceRules
    {
        /// <summary>Within this many metres a voice is at full volume.</summary>
        public const float FullVolumeDistance = 4f;
        /// <summary>From this many metres on a voice is not heard at all (one office cell of a floor is 18 m).</summary>
        public const float MaxDistance = 20f;
        /// <summary>The host keeps sending a voice this much beyond <see cref="MaxDistance"/>: a position is a moment old, and a voice should fade out, not be cut off.</summary>
        public const float RelayMargin = 4f;
        /// <summary>A voice quieter than this (a gain, 1 = full) counts as silence: it is not decoded and does not show in the HUD.</summary>
        public const float AudibleGain = 0.01f;
        /// <summary>How much quieter the ear on the far side gets for a voice that is fully to one side (0 = no panning, 1 = silent in that ear).</summary>
        public const float PanDepth = 0.7f;
        /// <summary>The volume of everybody in the lobby (no bodies there, so no distance): what it was before the range existed.</summary>
        public const float LobbyGain = 0.8f;
        /// <summary>The volume of a voice that comes over a radio (a walkie-talkie), at any distance.</summary>
        public const float RadioGain = 0.85f;

        /// <summary>How a voice reaches a listener: not at all, by the air, or over a radio.</summary>
        public enum Route { None, Near, Radio }

        /// <summary>The volume of a voice heard by the air at this distance: 1 up to <see cref="FullVolumeDistance"/>, 0 from <see cref="MaxDistance"/> on, and in between a
        /// curve that falls quickly at first (half way it is a quarter).</summary>
        public static float Gain(float distance)
        {
            if (float.IsNaN(distance)) return 0f;
            if (distance <= FullVolumeDistance) return 1f;
            if (distance >= MaxDistance) return 0f;
            float g = 1f - (distance - FullVolumeDistance) / (MaxDistance - FullVolumeDistance);
            return g * g;
        }

        /// <summary>The farthest distance at which a voice is still louder than <see cref="AudibleGain"/> (about 18 m).</summary>
        public static readonly float AudibleLimit = FullVolumeDistance + (MaxDistance - FullVolumeDistance) * (1f - (float)Math.Sqrt(AudibleGain));

        public static bool Audible(float distance) => Gain(distance) >= AudibleGain;

        /// <summary>By the air when close enough to hear; over the radio when both carry one; else not at all. (Nobody carries a radio yet.)</summary>
        public static Route RouteFor(float distance, bool speakerHasRadio, bool listenerHasRadio)
        {
            if (Audible(distance)) return Route.Near;
            return speakerHasRadio && listenerHasRadio ? Route.Radio : Route.None;
        }

        public static float GainFor(Route route, float distance) =>
            route == Route.Near ? Gain(distance) : route == Route.Radio ? RadioGain : 0f;

        /// <summary>Host: is this speaker's packet worth sending to that listener? (Anything that can be heard, and a margin beyond it.)</summary>
        public static bool ShouldRelay(float distance, bool speakerHasRadio, bool listenerHasRadio) =>
            (speakerHasRadio && listenerHasRadio) || distance < MaxDistance + RelayMargin;

        /// <summary>-1 (voice at the left) .. 1 (at the right) from how far to the side the speaker is (the dot product of the direction to him with the listener's right,
        /// -1..1); a voice within 3 m is panned less, so one beside you is not thrown into one ear.</summary>
        public static float PanFor(float sideways, float distance)
        {
            float p = Math.Max(-1f, Math.Min(1f, sideways)) * Math.Min(1f, Math.Max(0f, distance) / 3f);
            return float.IsNaN(p) ? 0f : p;
        }

        /// <summary>The gain of the left ear for a voice at <paramref name="pan"/> (see <see cref="PanFor"/>): only the ear away from the voice gets quieter.</summary>
        public static float LeftGain(float gain, float pan) => gain * (1f - Math.Max(0f, pan) * PanDepth);

        public static float RightGain(float gain, float pan) => gain * (1f + Math.Min(0f, pan) * PanDepth);
    }
}
