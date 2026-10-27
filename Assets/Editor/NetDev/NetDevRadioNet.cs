using System.Collections;
using System.Globalization;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The dock radio in a session (two machines: <c>qa_mp.ps1 -Runs 'RadioNet:host,c1'</c>): the whole team hears the same station, the same song at the same place (the host publishes the station, when it was put on and the seed;
    /// every machine works the song and the second out from the shared clock); the host changes the station and the client follows; the client clicks a station at the radio and the host (and so everybody) changes to it; a client
    /// who asks from across the docks is not listened to.
    /// </summary>
    public partial class NetDevRunner
    {
        int radioNetFails;

        void RadioNetCheck(string name, bool ok, string detail = "")
        {
            if (!ok) radioNetFails++;
            Log($"[RadioNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>What this machine's radio plays: the clip's name and the second in it, as a signal's text.</summary>
        static string RadioNow()
        {
            var r = DockRadio.Current;
            return r != null && r.ClipNow != null ? r.ClipNow.name + "|" + r.Speaker.time.ToString("0.00", CultureInfo.InvariantCulture) + "|" + RadioState.Station : "none|0|-1";
        }

        IEnumerator RadioNetHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            yield return WaitFor(() => DockRadio.Current != null && DockRadio.Current.Playing, 30, "the radio playing");
            RadioNetCheck("host: the docks' radio plays a station", DockRadio.Current != null && DockRadio.Current.Playing && RadioRules.ValidStation(RadioState.Station), $"(station {RadioState.Station}, '{DockRadio.Current?.TitleNow}')");
            RadioState.Set(1);
            yield return new WaitForSeconds(2.5f);
            Signal("h_station1", RadioNow());
            yield return WaitSig("c_checked1", 60);

            // ---- the client asks for station 3 from across the docks (refused), then at the radio (done)
            Signal("h_ready_far");
            yield return WaitSig("c_asked_far", 60);
            yield return new WaitForSeconds(1.5f);
            RadioNetCheck("host: a client who asks from across the docks is not listened to", RadioState.Station == 1, $"(station {RadioState.Station})");
            Signal("h_far_done");
            yield return WaitSig("c_asked_near", 60);
            yield return WaitFor(() => RadioState.Station == 2, 10, "the client's station");
            RadioNetCheck("host: a client at the radio changes the station for everybody", RadioState.Station == 2, $"(station {RadioState.Station})");
            yield return new WaitForSeconds(2.5f);
            Signal("h_station3", RadioNow());
            // ---- the client turns it up a step and switches it off
            yield return WaitSig("c_power", 60);
            yield return WaitFor(() => !RadioState.On && RadioState.Volume == RadioRules.DefaultVolume + 1, 10, "the client's volume and power");
            yield return new WaitForSeconds(0.5f);
            RadioNetCheck("host: the client's VOL + and OFF reach here: the radio is a step louder and off", RadioState.Volume == RadioRules.DefaultVolume + 1 && !RadioState.On && !DockRadio.Current.Playing, $"(volume {RadioState.Volume}, on {RadioState.On})");
            Signal("h_power_ok");
            yield return WaitSig("c_done", 60);
            Log($"[RadioNet] DONE fails={radioNetFails}");
            Quit(radioNetFails == 0 ? 0 : 1);
        }

        IEnumerator RadioNetClientScenario()
        {
            var net = NetSession.Instance;
            float deadline = Time.realtimeSinceStartup + 150f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.JoinLocal("127.0.0.1");
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(2f);
            }
            yield return FloorReady(0, 120);

            // ---- the host put station 2 on: the same song, at the same place (within 1.5 s)
            yield return WaitSig("h_station1", 120);
            var host = ReadSig("h_station1").Split('|');
            var mine = RadioNow().Split('|');
            float hostAt = float.Parse(host[1], CultureInfo.InvariantCulture), myAt = float.Parse(mine[1], CultureInfo.InvariantCulture);
            RadioNetCheck("client: the host's station plays here too - the same song, at the same place (the shared clock)", mine[2] == "1" && mine[0] == host[0] && Mathf.Abs(myAt - hostAt) < 1.5f,
                $"(host '{host[0]}' at {hostAt:0.0} s, here '{mine[0]}' at {myAt:0.0} s, station {mine[2]})");
            Signal("c_checked1");

            // ---- from across the docks: not listened to; at the radio: done
            yield return WaitSig("h_ready_far", 60);
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            var radio = DockRadio.Current;
            wob.TeleportTo(radio.transform.position + radio.Forward * 14f + Vector3.up * 0.6f, wob.transform.rotation);
            yield return new WaitForSeconds(1.0f);
            RadioState.Request(2);
            Signal("c_asked_far");
            yield return WaitSig("h_far_done", 60);
            RadioNetCheck("client: from across the docks the station stays", RadioState.Station == 1, $"(station {RadioState.Station})");
            var stand = radio.transform.position + radio.Forward * 1.3f;
            wob.TeleportTo(new Vector3(stand.x, 1.1f, stand.z), wob.transform.rotation);
            yield return new WaitForSeconds(1.5f);
            RadioState.Request(2);
            Signal("c_asked_near");
            yield return WaitSig("h_station3", 60);
            host = ReadSig("h_station3").Split('|');
            mine = RadioNow().Split('|');
            hostAt = float.Parse(host[1], CultureInfo.InvariantCulture); myAt = float.Parse(mine[1], CultureInfo.InvariantCulture);
            RadioNetCheck("client: at the radio his click changed the station here and at the host - the same song at the same place", mine[2] == "2" && mine[0] == host[0] && Mathf.Abs(myAt - hostAt) < 1.5f,
                $"(host '{host[0]}' at {hostAt:0.0} s, here '{mine[0]}' at {myAt:0.0} s, station {mine[2]})");
            RadioState.Request(RadioRules.ActionVolumeUp);
            RadioState.Request(RadioRules.ActionPower);
            Signal("c_power");
            yield return WaitSig("h_power_ok", 60);
            yield return WaitFor(() => !RadioState.On, 10, "off here");
            yield return new WaitForSeconds(0.5f);
            RadioNetCheck("client: the radio is off here too, and a step louder for when it comes back", !RadioState.On && !DockRadio.Current.Playing && RadioState.Volume == RadioRules.DefaultVolume + 1, $"(on {RadioState.On}, volume {RadioState.Volume})");
            Signal("c_done");
            Log($"[RadioNet] DONE fails={radioNetFails}");
            Quit(radioNetFails == 0 ? 0 : 1);
        }
    }
}
