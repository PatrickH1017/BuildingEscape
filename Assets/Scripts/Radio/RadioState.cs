using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What the dock radio is playing, the same for the whole team: the station (-1: not on yet), the moment the radio's clock started (on <see cref="Now"/>'s clock) and the seed its playlists are shuffled with. The host (or a solo
    /// game) decides it; in a session <see cref="Net.RunSync"/> publishes it and a client's click is a request to the host.
    /// Every station runs on that one clock for the whole run, whether it is listened to or not (the owner, 2026-10-27: "Every time I switch too a different station then go back it resets. I want it to be on a constant time loop so if
    /// I go back it picks up from where it was ... keep looping the enitire run that they are going so when the players come down they can hear different songs"): switching only changes which station is heard, and back from a floor
    /// the radio is further on in its playlists. Each station starts its loop at its own place (<see cref="StationStart"/>), so the three do not all begin with their first track.
    /// </summary>
    public static class RadioState
    {
        /// <summary>The station on (-1: none yet; the first radio built puts one on at random).</summary>
        public static int Station { get; private set; } = -1;
        /// <summary>When the radio's clock started (the first radio of the game, or the start of the session), in seconds of <see cref="Now"/>. It is not moved when the station changes.</summary>
        public static double Epoch { get; private set; }
        /// <summary>The seed every station's playlist is shuffled with.</summary>
        public static int Seed { get; private set; }
        /// <summary>Changes each time the station changes (for the screen and the tests).</summary>
        public static int Changes { get; private set; }
        /// <summary>The radio is switched on (its power button): off it is silent, and the stations play on unheard.</summary>
        public static bool On { get; private set; } = true;
        /// <summary>The radio's volume, 0..<see cref="RadioRules.VolumeSteps"/> (its volume buttons).</summary>
        public static int Volume { get; private set; } = RadioRules.DefaultVolume;

        static bool epochOnline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Station = -1; Epoch = 0; Seed = 0; Changes = 0; epochOnline = false; On = true; Volume = RadioRules.DefaultVolume; }

        static bool Online => Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening;

        /// <summary>The clock the radio runs on: the session's shared clock (the host's), or this machine's in a solo game.</summary>
        public static double Now => Online ? Unity.Netcode.NetworkManager.Singleton.ServerTime.Time : Time.unscaledTimeAsDouble;

        /// <summary>Seconds the radio's clock has run (a start on another clock - a solo game before a session - starts the clock again, once).</summary>
        public static double Running
        {
            get
            {
                if (epochOnline != Online && Station >= 0 && !Net.NetRole.IsRemoteClient) { Epoch = Now; epochOnline = Online; }
                return Now - Epoch;
            }
        }

        /// <summary>Where in its loop a station starts (seconds, from the seed: the three stations do not all begin with their first track; <see cref="RadioRules.Locate"/> takes it round the loop).</summary>
        public static double StationStart(int station) => RadioRules.StartOffset(Seed, station);

        /// <summary>How far a station is in its loop now (seconds; see <see cref="RadioRules.Locate"/>): the same clock for every station, listened to or not.</summary>
        public static double Elapsed(int station) => Running + StationStart(station);

        /// <summary>Puts a station on (offline and on the host; a client asks with <see cref="Request"/>). The first one starts the radio's clock; after that it only changes which station is heard.</summary>
        public static void Set(int station)
        {
            if (!RadioRules.ValidStation(station)) return;
            if (Seed == 0) Seed = Random.Range(1, int.MaxValue);
            if (Station < 0) { Epoch = Now; epochOnline = Online; }
            Station = station;
            Changes++;
        }

        /// <summary>The first radio: a station at random, so the docks have music from the start.</summary>
        public static void PutOnIfOff()
        {
            if (Station < 0 && !Net.NetRole.IsRemoteClient) Set(Random.Range(0, RadioRules.StationCount));
        }

        /// <summary>A click on a button (<see cref="RadioRules.ActionOf"/>: a station, the power, the volume): offline and on the host at once, a client asks the host (who checks the player is at the radio).</summary>
        public static void Request(int action)
        {
            if (!RadioRules.ValidAction(action)) return;
            if (Net.NetRole.IsRemoteClient) { Net.RunSync.Instance?.RequestRadio(action); return; }
            Apply(action);
        }

        /// <summary>Does what a button asks (offline and on the host): a station is put on (and the radio switched on), the power switches it off or on, the volume goes a step down or up.</summary>
        public static void Apply(int action)
        {
            if (RadioRules.ValidStation(action)) { Set(action); On = true; return; }
            if (action == RadioRules.ActionPower) { if (Station < 0) PutOnIfOff(); else On = !On; return; }
            if (action == RadioRules.ActionVolumeDown) Volume = RadioRules.StepVolume(Volume, -1);
            else if (action == RadioRules.ActionVolumeUp) Volume = RadioRules.StepVolume(Volume, 1);
        }

        /// <summary>A client: what the host published.</summary>
        public static void NetApply(int station, double epoch, int seed, bool on, int volume)
        {
            if (station != Station) Changes++;
            Station = station; Epoch = epoch; Seed = seed; epochOnline = true;
            On = on; Volume = RadioRules.StepVolume(volume, 0);
        }
    }
}
