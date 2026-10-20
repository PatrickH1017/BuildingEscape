using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Proximity voice, measured the way a person hears it (owner, 2026-10-17: "players can only hear other players' voice from a certain distance"; before this a
    /// teammate could be heard across a floor): two machines in a run on floor 4, the host talks all the time (a tone stands in for the microphone) and stays
    /// where he is; the client stands at different distances from him and measures, for each, the loudest sample the host's voice wrote to the output
    /// (<c>VoiceVoice.OutPeak</c>, after the distance and the side are applied), the final mix at his AudioListener (<c>AudioListener.GetOutputData</c>) and how many voice
    /// packets reach him. Expected: close = loud, in between = <c>VoiceRules.Gain</c> of the distance, from 20 m on = silence, and beyond the range plus the host's margin not even a
    /// packet; then, with both machines saying they carry a radio (<c>VoiceChat.RadioOf</c>: the hook for the walkie-talkies), the same far voice is heard.
    /// Run: <c>qa_mp.ps1 -Runs 'VoiceRange:host,c1'</c> (one Unity process per role). Looks for "[VoiceRange] ... FAIL".
    /// </summary>
    public partial class NetDevRunner
    {
        int voiceRangeFails;

        void VoiceRangeCheck(string name, bool ok, string detail = "")
        {
            if (!ok) voiceRangeFails++;
            Log($"[VoiceRange] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static readonly float[] outBuf = new float[1024];

        /// <summary>The loudest RMS of the two output channels of the final mix right now.</summary>
        static float ListenerLevel()
        {
            float best = 0f;
            for (int ch = 0; ch < 2; ch++)
            {
                AudioListener.GetOutputData(outBuf, ch);
                double sum = 0;
                foreach (var s in outBuf) sum += s * s;
                best = Mathf.Max(best, Mathf.Sqrt((float)(sum / outBuf.Length)));
            }
            return best;
        }

        IEnumerator VoiceRangeHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            tm.RideTo(4);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 4);
            yield return FloorReady(4, 200);
            hm.godMode = true; hm.freezeTimer = true;
            foreach (var gg in GuardAI.All) if (gg) { gg.sightRange = 0.1f; gg.hearingMultiplier = 0f; }      // (they only stand about)
            yield return new WaitForSeconds(2f);

            var vc = VoiceChat.Instance;
            vc.UseTestInput(new ToneVoiceInput(440f));       // the host talks all the time
            var floorInfo = tm.CurrentInfo;
            Signal("h_ready", $"{floorInfo.cols},{floorInfo.rows}");       // (the client's own copy of the floor's size is not reliable)
            yield return WaitSig("c_far_done", 300);
            VoiceChat.RadioOf = _ => true;                    // (walkie-talkies: both say they carry one)
            Signal("h_radio");
            yield return WaitSig("c_done", 120);
            VoiceChat.RadioOf = null;
            Log($"[VoiceRange] host: sent={vc.PacketsSent} relayed={vc.PacketsRelayed} held back={vc.PacketsHeldBack} speaking={vc.Speaking}");
            VoiceRangeCheck("host: the host was talking", vc.PacketsSent > 100, $"(sent {vc.PacketsSent})");
            VoiceRangeCheck("host: the host held voice back from the player who was out of earshot", vc.PacketsHeldBack > 50, $"(held back {vc.PacketsHeldBack}, relayed {vc.PacketsRelayed})");
            Log($"[VoiceRange] host DONE fails={voiceRangeFails}");
            Log("DONE voicerangehost");
            Quit(voiceRangeFails == 0 ? 0 : 1);
        }

        struct VoiceReading { public float planned, body, peak, peakL, peakR, side, mix; public int packets; }

        IEnumerator VoiceRangeClientScenario()
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
            yield return FloorReady(4, 300);
            yield return WaitSig("h_ready", 120);
            yield return new WaitForSeconds(1.5f);

            var vc = VoiceChat.Instance;
            vc.Muted = true;                                  // only the host talks
            var hostPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            VoiceRangeCheck("client: the host's player is here", hostPlayer != null);
            if (hostPlayer == null) { Signal("c_far_done"); Signal("c_done"); Quit(1); yield break; }
            ulong hostId = hostPlayer.OwnerClientId;
            Vector3 at = hostPlayer.transform.position;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var dims = (ReadSig("h_ready") ?? "8,5").Split(',');
            int cols = int.Parse(dims[0]), rows = int.Parse(dims[1]);
            float cs = gen ? gen.cellSize : 18f;
            var min = Vector2.zero; var max = new Vector2(cols * cs, rows * cs);
            // the longest way from the host across the floor (towards the far corner)
            var corners = new[] { new Vector2(min.x, min.y), new Vector2(max.x, min.y), new Vector2(min.x, max.y), new Vector2(max.x, max.y) };
            Vector2 from2 = new Vector2(at.x, at.z);
            Vector2 far = corners.OrderByDescending(c => Vector2.Distance(c, from2)).First();
            Vector2 dir2 = (far - from2).normalized;
            float room = Vector2.Distance(far, from2) - 2f;
            Log($"[VoiceRange] floor {cols}x{rows} cells of {cs} m; the host stands at ({at.x:0.0}, {at.z:0.0}); {room:0} m of floor towards the far corner; range {VoiceRules.FullVolumeDistance:0}..{VoiceRules.MaxDistance:0} m, margin {VoiceRules.RelayMargin:0} m");

            var readings = new List<VoiceReading>();
            foreach (float d in new[] { 3f, 8f, 12f, 16f, 22f, 30f, 45f, 70f })
            {
                if (d > room) { Log($"[VoiceRange] {d:0} m does not fit on this floor: skipped"); continue; }
                var target = from2 + dir2 * d;
                Teleport(new Vector3(target.x, at.y, target.y));
                yield return new WaitForSeconds(1.2f);                 // (the move reaches the host; the voice follows the player)
                yield return Measure(hostId, d, readings, hostPlayer);
            }

            // ---- assertions on the air
            float nearPeak = readings.Where(r => r.body <= VoiceRules.FullVolumeDistance).Select(r => r.peak).DefaultIfEmpty(0f).Max();
            VoiceRangeCheck("client: a voice within 4 m is loud (a 0.4 tone: peak above 0.3)", nearPeak > 0.3f, $"(peak {nearPeak:0.000})");
            foreach (var r in readings.Where(r => r.body > VoiceRules.FullVolumeDistance && r.body < VoiceRules.MaxDistance))
            {
                float expected = VoiceRules.Gain(r.body);
                float ratio = nearPeak > 0f ? r.peak / nearPeak : -1f;
                VoiceRangeCheck($"client: a voice {r.body:0.0} m away is as loud as the rules say ({expected:0.00} of full)", Mathf.Abs(ratio - expected) <= 0.15f, $"(ratio {ratio:0.00})");
            }
            foreach (var r in readings.Where(r => r.body >= VoiceRules.MaxDistance))
            {
                VoiceRangeCheck($"client: a voice {r.body:0.0} m away is not heard at all", r.peak < 0.01f && r.mix < 0.05f, $"(voice peak {r.peak:0.0000}, final mix {r.mix:0.0000})");
            }
            foreach (var r in readings.Where(r => r.body >= VoiceRules.MaxDistance + VoiceRules.RelayMargin + 1f))
            {
                VoiceRangeCheck($"client: no voice packet even reaches him {r.body:0.0} m away (the host holds it back)", r.packets == 0, $"({r.packets} packets)");
            }
            bool nearOk = readings.Any(r => r.body <= VoiceRules.FullVolumeDistance && r.packets > 40);
            VoiceRangeCheck("client: close by, the packets do arrive", nearOk);
            VoiceRangeCheck("client: some distance was measured beyond the range", readings.Any(r => r.body >= VoiceRules.MaxDistance + VoiceRules.RelayMargin + 1f), $"({readings.Count} readings)");

            // ---- left and right: standing 4 m to the host's left and then to his right (the camera looks along +z, so the host is on the listener's right, then on his left)
            foreach (float dx in new[] { -4f, 4f })
            {
                Teleport(new Vector3(at.x + dx, at.y, at.z));
                yield return new WaitForSeconds(1.6f);
                var pan = new List<VoiceReading>();
                yield return Measure(hostId, 4f, pan, hostPlayer);
                var r = pan[0];
                bool right = r.side > 0f;
                VoiceRangeCheck($"client: with the host {(right ? "on his right" : "on his left")} ({r.side:+0.00;-0.00}) the {(right ? "right" : "left")} ear is the louder one",
                    Mathf.Abs(r.side) < 0.5f || (right ? r.peakR > r.peakL * 1.5f : r.peakL > r.peakR * 1.5f), $"(left {r.peakL:0.000}, right {r.peakR:0.000})");
            }

            // ---- a radio (the hook for the walkie-talkies): both machines say they carry one, and the voice at the farthest spot is heard
            Signal("c_far_done");
            yield return WaitSig("h_radio", 60);
            VoiceChat.RadioOf = _ => true;
            if (readings.Count > 0)                           // (back to the farthest spot)
            {
                var farTarget = from2 + dir2 * readings[readings.Count - 1].planned;
                Teleport(new Vector3(farTarget.x, at.y, farTarget.y));
            }
            yield return new WaitForSeconds(1.4f);
            var withRadio = new List<VoiceReading>();
            yield return Measure(hostId, readings.Count > 0 ? readings[readings.Count - 1].planned : 0f, withRadio, hostPlayer);
            if (withRadio.Count > 0)
            {
                var r = withRadio[0];
                VoiceRangeCheck($"client: over a radio the voice is heard {r.body:0.0} m away", r.body > VoiceRules.MaxDistance + VoiceRules.RelayMargin && r.peak > 0.25f && r.packets > 40, $"(voice peak {r.peak:0.000}, {r.packets} packets)");
            }
            VoiceChat.RadioOf = null;

            Signal("c_done");
            Log($"[VoiceRange] client DONE fails={voiceRangeFails}");
            Log("DONE voicerangeclient");
            Quit(voiceRangeFails == 0 ? 0 : 1);
        }

        /// <summary>1.5 s of listening to the host's voice from where the client stands now.</summary>
        IEnumerator Measure(ulong hostId, float planned, List<VoiceReading> into, NetPlayer hostPlayer)
        {
            var vc = VoiceChat.Instance;
            int rx0 = vc.PacketsReceived;
            float peak = 0f, peakL = 0f, peakR = 0f, mix = 0f, t = 0f;
            while (t < 1.5f)
            {
                var voice = vc.VoiceOf(hostId);
                if (voice) { peak = Mathf.Max(peak, voice.OutPeak); peakL = Mathf.Max(peakL, voice.OutPeakLeft); peakR = Mathf.Max(peakR, voice.OutPeakRight); }
                mix = Mathf.Max(mix, ListenerLevel());
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            var v = vc.VoiceOf(hostId);
            float body = Mine && hostPlayer ? Vector3.Distance(Mine.transform.position, hostPlayer.transform.position) : -1f;
            // which side the host is on, as the listener (the camera) sees it: the dot product of the direction to him with its right
            float side = 0f;
            var ears = FindFirstObjectByType<AudioListener>();
            if (ears && hostPlayer)
            {
                var dirTo = hostPlayer.transform.position - ears.transform.position; dirTo.y = 0f;
                var right = ears.transform.right; right.y = 0f;
                if (dirTo.sqrMagnitude > 0.01f && right.sqrMagnitude > 0.01f) side = Vector3.Dot(dirTo.normalized, right.normalized);
            }
            var reading = new VoiceReading { planned = planned, body = body, peak = peak, peakL = peakL, peakR = peakR, side = side, mix = mix, packets = vc.PacketsReceived - rx0 };
            into.Add(reading);
            Log($"[VoiceRange] planned {planned,3:0} m, bodies {body,5:0.0} m apart: voice peak {peak:0.000} (gain {(v ? v.Gain : 0f):0.00}; left ear {peakL:0.000}, right ear {peakR:0.000}; the host is {side:+0.00;-0.00} to the side), final mix {mix:0.000}, {reading.packets} packets in 1.5 s");
        }
    }
}
