using System.Collections;
using System.Globalization;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch K, T5 + T12, of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63): the two things that cross the network and had only been tried on one machine. Two machines on the loopback, the host and one client.
    /// T5, in the docks: the client buys the walkie talkie at the Vendor (the host takes the money and every machine puts it in his pack), it is not held out by itself, held out it turns his radio on on EVERY machine (<c>VoiceChat.RadioOf</c> answers from the pack every
    /// machine mirrors) and shows in his hand on the host's screen too, put away it is off again. T12, on floor 1: the host puts a guard down, the client takes hold of the body (the host checks and tells everybody), walks away - the body trails him on the host and, through the
    /// snapshots, on the client's own screen - and lets go. Run: <c>qa_mp_bd.ps1 -Method DragNet</c>; both logs end with <c>[Asks1023K] ... DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23kFails;

        void A23K(string name, bool ok, string detail = "")
        {
            if (!ok) a23kFails++;
            Log($"[Asks1023K] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static string PkV(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "{0:0.###},{1:0.###},{2:0.###}", v.x, v.y, v.z);

        static Vector3 UnPkV(string s)
        {
            var p = s.Split(',');
            return new Vector3(float.Parse(p[0], CultureInfo.InvariantCulture), float.Parse(p[1], CultureInfo.InvariantCulture), float.Parse(p[2], CultureInfo.InvariantCulture));
        }

        static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

        // ------------------------------------------------------------------ the host

        IEnumerator DragNetHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.Roster.Count >= 2, 200, "a client");
            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance; var tm = TowerManager.Instance; var sync = RunSync.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            yield return WaitFor(() => sync != null && sync.EveryoneHere, 60, "every player");
            var clientPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            A23K("(test) the client's player is here", clientPlayer != null);
            if (clientPlayer == null) { Quit(1); yield break; }
            var cpack = clientPlayer.GetComponent<Backpack>();
            var clientWob = clientPlayer.GetComponent<WobblyPlayer>();

            // ---- T5 in the docks: the Vendor, the pack, the hand, the radio
            hm.TrySpend(hm.Cash); hm.AddCash(5000);
            Signal("h_ready");
            yield return WaitSig("c_bought", 120);
            yield return new WaitForSeconds(0.5f);
            var item = cpack.Items.FirstOrDefault(i => i && i.TryGetComponent(out WalkieTalkie _));
            A23K("T5 host: the client's pack holds the walkie talkie, the team paid its price, and he does not hold it out by himself", item != null && cpack.Equipped == null && hm.Cash == 5000 - WalkieRules.Price, $"(cash {hm.Cash}, equipped {(cpack.Equipped ? cpack.Equipped.Label : "nothing")})");
            A23K("T5 host: while it is in the pack the radio is off for him (the voice chat asks the pack)", VoiceChat.RadioOf != null && !VoiceChat.RadioOf(clientPlayer.OwnerClientId));
            Signal("h_bought_ok");
            yield return WaitSig("c_equipped", 90);
            yield return new WaitForSeconds(0.6f);
            var hw = clientPlayer.GetComponent<HeldWalkie>();
            A23K("T5 host: he holds it out: it is the item in his hand here too and the radio is on for him", item != null && cpack.Equipped == item && WalkieTalkie.HoldsOut(cpack) && VoiceChat.RadioOf(clientPlayer.OwnerClientId));
            A23K("T5 host: and it shows in his hand on this machine", hw != null && hw.Model != null);
            Signal("h_equipped_ok");
            yield return WaitSig("c_put_away", 90);
            yield return new WaitForSeconds(0.6f);
            A23K("T5 host: put away, the radio is off again and the model is gone from his hand", !WalkieTalkie.HoldsOut(cpack) && !VoiceChat.RadioOf(clientPlayer.OwnerClientId) && (hw == null || hw.Model == null));
            Signal("h_put_away_ok");

            // ---- T12 on floor 1: a guard is put down, the client drags his body
            tm.RideTo(1);
            yield return WaitFor(() => tm.Loading || hm.Floor == 1, 20, "the ride to floor 1");
            yield return FloorReady(1, 180);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(2f);
            GuardCorpse.WaitOverride = 40f;
            var victim = GuardAI.All.FirstOrDefault(g => g && !g.Dead && !g.IsPuppet && !g.IsBoss && g.NetId >= 0);
            A23K("(test) floor 1 has a guard to put down", victim != null);
            if (victim == null) { Quit(1); yield break; }
            Eliminate(victim);
            yield return null; yield return null;
            var body = victim.GetComponent<DraggableBody>();
            A23K("T12 host: the guard who is down for good has a body that can be dragged", victim.Dead && body != null && body.CanBeDragged);
            var bodyAt = victim.transform.position;
            bool open = OpenSpot(bodyAt + Vector3.up * 0.9f, 6f, out var far, true);
            A23K("(test) an open spot 6 m from the body", open);
            if (!open || body == null) { Quit(1); yield break; }
            var dir = new Vector3(far.x - bodyAt.x, 0f, far.z - bodyAt.z).normalized;
            var near = bodyAt + dir * 1.0f;
            Signal("h_body", victim.NetId + ";" + PkV(bodyAt) + ";" + PkV(near) + ";" + PkV(far));
            yield return WaitSig("c_grabbed", 120);
            yield return new WaitForSeconds(0.5f);
            A23K("T12 host: the client took hold of the body: it is his to drag, he is slower, and nobody else drags it", body.IsDragged && body.Dragger == clientWob && Mathf.Approximately(clientWob.DragFactor, DraggableBodyRules.DraggerSpeedShare), $"(dragger {(body.Dragger ? body.Dragger.name : "nobody")}, his speed x{clientWob.DragFactor:0.0})");
            Signal("h_grabbed_ok");
            yield return WaitSig("c_walked", 120);
            yield return new WaitForSeconds(0.5f);
            float gap = FlatDistance(body.transform.position, clientWob.transform.position);
            A23K("T12 host: the body trailed him over six metres: it is within a couple of metres of him and far from where it lay", gap > 0.6f && gap < 2.6f && FlatDistance(body.transform.position, bodyAt) > 3f, $"(gap {gap:0.00} m, moved {FlatDistance(body.transform.position, bodyAt):0.0} m)");
            A23K("T12 host: it is still there (it waits while it is dragged)", body != null && victim.Dead);
            Signal("h_walked_ok");
            yield return WaitSig("c_dropped", 90);
            yield return new WaitForSeconds(0.5f);
            A23K("T12 host: he let go: nobody drags the body and he is as fast as before", !body.IsDragged && Mathf.Approximately(clientWob.DragFactor, 1f));
            Signal("h_dropped_ok");
            GuardCorpse.WaitOverride = -1f;
            Log($"[Asks1023K] host DONE fails={a23kFails}");
            Log("DONE dragnethost");
            Quit(a23kFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ the client

        IEnumerator DragNetClientScenario()
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
            yield return WaitSig("h_ready", 150);
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm.Cash >= 5000, 30, "the team's cash");
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            var rb = wob.GetComponent<Rigidbody>();
            var pack = MyPack;

            // ---- T5: stand at the counter, buy it, hold it out, put it away
            var vendor = Vendor.Current;
            var zone = vendor.GetComponent<Collider>();
            wob.TeleportTo(zone.bounds.center + Vector3.up * 0.2f, wob.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.5f);
            RunSync.Instance.RequestBuy(Vendor.WalkieIndex);
            yield return WaitFor(() => pack.Items.Any(i => i && i.TryGetComponent(out WalkieTalkie _)), 20, "the walkie talkie in the pack");
            var item = pack.Items.First(i => i && i.TryGetComponent(out WalkieTalkie _));
            A23K("T5 client: bought at the Vendor: it is in the pack and not held out by itself, the radio is off", pack.Equipped == null && !WalkieTalkie.HoldsOut(pack) && !VoiceChat.RadioOf(me.OwnerClientId), $"(equipped {(pack.Equipped ? pack.Equipped.Label : "nothing")})");
            Signal("c_bought");
            yield return WaitSig("h_bought_ok", 60);
            pack.SetEquipped(item);                                                     // (the pack's EQUIP button)
            yield return WaitFor(() => pack.Equipped == item, 15, "the walkie talkie held out");
            yield return new WaitForSeconds(0.6f);
            var hw = me.GetComponent<HeldWalkie>();
            A23K("T5 client: held out: the radio is on for him and the model is in his hand", WalkieTalkie.HoldsOut(pack) && VoiceChat.RadioOf(me.OwnerClientId) && hw != null && hw.Model != null);
            Signal("c_equipped");
            yield return WaitSig("h_equipped_ok", 60);
            pack.SetEquipped(null);
            yield return WaitFor(() => pack.Equipped == null, 15, "the walkie talkie put away");
            yield return new WaitForSeconds(0.6f);
            A23K("T5 client: put away: the radio is off and the model is gone from his hand", !WalkieTalkie.HoldsOut(pack) && !VoiceChat.RadioOf(me.OwnerClientId) && (hw == null || hw.Model == null));
            Signal("c_put_away");
            yield return WaitSig("h_put_away_ok", 60);

            // ---- T12: the body of a guard
            yield return WaitSig("h_body", 300);
            yield return FloorReady(1, 240);
            GuardCorpse.WaitOverride = 40f;
            var info = ReadSig("h_body").Split(';');
            int bodyId = int.Parse(info[0], CultureInfo.InvariantCulture);
            var bodyAt = UnPkV(info[1]); var near = UnPkV(info[2]); var far = UnPkV(info[3]);
            var dir = new Vector3(far.x - bodyAt.x, 0f, far.z - bodyAt.z).normalized;
            yield return new WaitForSeconds(1.0f);
            var guard = NetIds.Get<GuardAI>(bodyId);
            A23K("(test) the dead guard is here as a puppet with a body that can be dragged", guard != null && guard.IsPuppet && guard.Dead && guard.GetComponent<DraggableBody>() != null);
            wob.TeleportTo(new Vector3(near.x, far.y + 1.1f, near.z), Quaternion.LookRotation(dir));
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.5f);
            var pg = me.GetComponent<PlayerGrab>();
            bool took = pg.TryGrabBody();
            yield return WaitFor(() => pg.Dragging != null, 15, "the body taken hold of");
            A23K("T12 client: the grab key takes hold of the body (the host agreed) and he walks slower", took && pg.Dragging != null && pg.Dragging.Guard == guard && Mathf.Approximately(wob.DragFactor, DraggableBodyRules.DraggerSpeedShare), $"(speed x{wob.DragFactor:0.0})");
            Signal("c_grabbed");
            yield return WaitSig("h_grabbed_ok", 60);
            var from = new Vector3(near.x, 0f, near.z); var to = new Vector3(far.x, 0f, far.z);
            for (int i = 1; i <= 25; i++)
            {
                var p = Vector3.Lerp(from, to, i / 25f);
                wob.TeleportTo(new Vector3(p.x, wob.transform.position.y, p.z), Quaternion.LookRotation(dir));
                rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(2.5f);
            float gap = FlatDistance(guard.transform.position, wob.transform.position);
            A23K("T12 client: the body, which the host moves and the snapshots show here, trails him: within a couple of metres of him and far from where it lay", gap > 0.6f && gap < 3.0f && FlatDistance(guard.transform.position, bodyAt) > 3f, $"(gap {gap:0.00} m, moved {FlatDistance(guard.transform.position, bodyAt):0.0} m)");
            Signal("c_walked");
            yield return WaitSig("h_walked_ok", 60);
            RunSync.Instance.RequestDropBody();
            yield return WaitFor(() => pg.Dragging == null, 15, "the body let go");
            yield return new WaitForSeconds(0.3f);
            A23K("T12 client: let go: he is as fast as before and the body is no longer dragged", Mathf.Approximately(wob.DragFactor, 1f) && !guard.GetComponent<DraggableBody>().IsDragged);
            Signal("c_dropped");
            yield return WaitSig("h_dropped_ok", 60);
            GuardCorpse.WaitOverride = -1f;
            Log($"[Asks1023K] client DONE fails={a23kFails}");
            Log("DONE dragnetclient");
            Quit(a23kFails == 0 ? 0 : 1);
        }
    }
}
