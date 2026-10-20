using BuildingEscape.Net;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// Proximity voice (owner, 2026-10-17: players hear each other only from a certain distance; walkie-talkies, bought at the vendor, will cover
        /// distance): the volume by distance, the routes (air / radio / none), which packets the host sends on, and the panning.
        /// </summary>
        public static void VoiceRulesTest()
        {
            Check("voice: full volume up to 4 m, silence from 20 m on (and for a distance that is not a number)",
                VoiceRules.Gain(0f) == 1f && VoiceRules.Gain(VoiceRules.FullVolumeDistance) == 1f && VoiceRules.Gain(VoiceRules.MaxDistance) == 0f
                && VoiceRules.Gain(500f) == 0f && VoiceRules.Gain(float.NaN) == 0f && VoiceRules.FullVolumeDistance == 4f && VoiceRules.MaxDistance == 20f);
            bool falling = true; float prev = 2f;
            for (float d = 0f; d <= 30f; d += 0.25f) { float g = VoiceRules.Gain(d); falling &= g <= prev + 1e-6f && g >= 0f && g <= 1f; prev = g; }
            Check("voice: the volume only ever falls with distance, always between 0 and 1 (0 to 30 m)", falling);
            Check("voice: half way between full volume and silence a voice is a quarter as loud",
                System.Math.Abs(VoiceRules.Gain((VoiceRules.FullVolumeDistance + VoiceRules.MaxDistance) * 0.5f) - 0.25f) < 1e-5f);
            Check("voice: nobody is heard across a floor (an office cell is 18 m: from 20 m, so 36 m and 90 m, silence)",
                VoiceRules.Gain(36f) == 0f && VoiceRules.Gain(90f) == 0f && VoiceRules.AudibleLimit < 18.5f && VoiceRules.AudibleLimit > 17f);
            Check("voice: audible exactly while the volume is above the audible gain",
                VoiceRules.Audible(10f) && VoiceRules.Audible(VoiceRules.AudibleLimit - 0.1f) && !VoiceRules.Audible(VoiceRules.AudibleLimit + 0.1f));

            Check("voice: close = by the air, far = nothing when nobody carries a radio",
                VoiceRules.RouteFor(3f, false, false) == VoiceRules.Route.Near && VoiceRules.RouteFor(30f, false, false) == VoiceRules.Route.None);
            Check("voice: a radio needs BOTH sides (walkie-talkies, later): both = any distance, only one = nothing",
                VoiceRules.RouteFor(300f, true, true) == VoiceRules.Route.Radio && VoiceRules.RouteFor(300f, true, false) == VoiceRules.Route.None
                && VoiceRules.RouteFor(300f, false, true) == VoiceRules.Route.None);
            Check("voice: standing beside a radio holder is still heard by the air, at full volume",
                VoiceRules.RouteFor(2f, true, true) == VoiceRules.Route.Near && VoiceRules.GainFor(VoiceRules.Route.Near, 2f) == 1f);
            Check("voice: the lobby keeps its old volume (0.8: no bodies there, so no distance)", VoiceRules.LobbyGain == 0.8f);
            Check("voice: the gain of a route (air by distance, radio fixed, none silent)",
                VoiceRules.GainFor(VoiceRules.Route.Radio, 500f) == VoiceRules.RadioGain && VoiceRules.GainFor(VoiceRules.Route.None, 1f) == 0f
                && VoiceRules.GainFor(VoiceRules.Route.Near, 12f) == VoiceRules.Gain(12f) && VoiceRules.RadioGain > 0f && VoiceRules.RadioGain <= 1f);

            Check("voice: the host sends a voice up to the range plus a margin of 4 m, then stops",
                VoiceRules.ShouldRelay(0f, false, false) && VoiceRules.ShouldRelay(23.9f, false, false) && !VoiceRules.ShouldRelay(24.1f, false, false) && VoiceRules.RelayMargin == 4f);
            bool everythingHeardIsSent = true;
            for (float d = 0f; d <= 40f; d += 0.25f) if (VoiceRules.Audible(d) && !VoiceRules.ShouldRelay(d, false, false)) everythingHeardIsSent = false;
            Check("voice: everything that can be heard is also sent (the host never cuts a voice short)", everythingHeardIsSent);
            Check("voice: the host sends everything between two radios and nothing far to one without",
                VoiceRules.ShouldRelay(500f, true, true) && !VoiceRules.ShouldRelay(500f, true, false) && !VoiceRules.ShouldRelay(500f, false, true));

            Check("voice: a voice fully to one side pans fully (1 right, -1 left), one beside you less (half way at 1.5 m), a number that is not one is the middle",
                VoiceRules.PanFor(1f, 10f) == 1f && VoiceRules.PanFor(-1f, 10f) == -1f && System.Math.Abs(VoiceRules.PanFor(1f, 1.5f) - 0.5f) < 1e-5f
                && VoiceRules.PanFor(0f, 10f) == 0f && VoiceRules.PanFor(float.NaN, 10f) == 0f && VoiceRules.PanFor(5f, 10f) == 1f);
            Check("voice: only the ear away from the voice gets quieter (right = left ear down to 30 %, left = right ear down, middle = both full)",
                VoiceRules.LeftGain(1f, 0f) == 1f && VoiceRules.RightGain(1f, 0f) == 1f
                && System.Math.Abs(VoiceRules.LeftGain(1f, 1f) - 0.3f) < 1e-5f && VoiceRules.RightGain(1f, 1f) == 1f
                && System.Math.Abs(VoiceRules.RightGain(1f, -1f) - 0.3f) < 1e-5f && VoiceRules.LeftGain(1f, -1f) == 1f
                && System.Math.Abs(VoiceRules.LeftGain(0.5f, 1f) - 0.15f) < 1e-5f);
        }
    }
}
