using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BuildingEscape.Net;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The owner's restraint, drag and rescue clips (2026-10-28) over the network, scenario <c>RestraintNet</c>: two machines on the loopback, the host and one client, on floor 1. The host's officer pins the client's player, the pin runs out (the real <c>BustPlayer</c>) and the officer
    /// drags the client by the head: the client's own body follows him (its machine pulls it), both machines show the two halves of the drag clip, the client's held struggle button reaches the host (the officer slows, shoves) and the host's numbers come back (the strain, the shove), a hit officer
    /// lets go (the drop), the host frees the client by holding E beside him and then the client frees the host (the rescuer's target is the owner's own variable). Run: <c>qa_mp_bd3.ps1 -Method RestraintNet -Prefix m -Sync</c>; both logs end with <c>[RestraintNet] ... DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int rnFails;

        void RnCheck(string name, bool ok, string detail = "")
        {
            if (!ok) rnFails++;
            Log($"[RestraintNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        // ------------------------------------------------------------------ the host

        IEnumerator RestraintNetHostScenario()
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
            var hostPlayer = NetPlayer.All.FirstOrDefault(p => p.IsOwner);
            RnCheck("(test) both players are here", clientPlayer != null && hostPlayer != null);
            if (clientPlayer == null || hostPlayer == null) { Quit(1); yield break; }
            var cw = clientPlayer.GetComponent<WobblyPlayer>(); var hw = hostPlayer.GetComponent<WobblyPlayer>();
            var crv = cw.GetComponent<RestraintVisual>(); var hrv = hw.GetComponent<RestraintVisual>();

            tm.RideTo(1);
            yield return WaitFor(() => tm.Loading || hm.Floor == 1, 20, "the ride to floor 1");
            yield return FloorReady(1, 180);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(2f);
            var gen = FindFirstObjectByType<FloorGenerator>();
            RnCheck("(test) the floor has a guards' outpost", gen != null && gen.TryGetOutpostRoom(out var room));
            gen.TryGetOutpostRoom(out room);
            var middle = new Vector3(room.Cx, 0f, room.Cz);
            NavMesh.SamplePosition(middle, out var dest, 8f, NavMesh.AllAreas);
            var g = GuardAI.All.FirstOrDefault(x => x && !x.IsPuppet && !x.Dead && !x.IsBoss && !x.IsBackup && x.NetId >= 0 && x.name != "Guard_Outpost");
            RnCheck("(test) an officer to do it with", g != null);
            RnCheck("(test) a start point 10-14 m of walking from the outpost", RgStartPoint(dest.position, 10f, 14f, out var start));
            if (g == null) { Quit(1); yield break; }
            RgStartPoint(dest.position, 10f, 14f, out start);
            foreach (var other in GuardAI.All) { if (other && other != g && !other.IsPuppet) { var oa = other.GetComponent<NavMeshAgent>(); if (oa && oa.isOnNavMesh) oa.Warp(middle + new Vector3(0f, 0f, 45f)); } }
            var ga = g.GetComponent<NavMeshAgent>();
            ga.Warp(start);
            var dir = Vector3.ProjectOnPlane(dest.position - start, Vector3.up).normalized;
            g.transform.rotation = Quaternion.LookRotation(dir);
            Signal("h_scene", g.NetId + ";" + PkV(start) + ";" + PkV(dir));
            yield return WaitSig("c_there", 120);
            yield return new WaitForSeconds(0.6f);

            // ---- the officer pins the client; the pin runs out
            hm.godMode = false;
            bool pinned = hm.Pin(cw, g);
            hm.godMode = true;
            g.BeginHold(cw);
            yield return null;
            RnCheck("host: the officer pinned the client (everybody sees PINNED)", pinned && cw.Custody == CustodyState.Pinned && g.State == GuardState.Pinning, $"(pinned {pinned}, custody {cw.Custody}, officer {g.State})");
            Signal("h_pinned");
            yield return WaitSig("c_pinned_ok", 60);
            yield return new WaitForSeconds(2.8f);
            RnCheck("host: while he is pinned he is tied up (the client's puppet here shows the tie-up and then lies hogtied)", crv.Current == RestraintVisual.Look.Hogtied || crv.Current == RestraintVisual.Look.TieUp, $"(look {crv.Current})");
            typeof(HeistManager).GetMethod("BustPlayer", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hm, new object[] { cw });
            yield return new WaitForSeconds(0.6f);
            RnCheck("host: the pin ran out: the officer drags the client by the head (state Dragging) and the client's puppet shows his half of the clip", cw.Custody == CustodyState.Busted && g.State == GuardState.Dragging && g.Dragging == cw && cw.DraggedByGuard == g && crv.Current == RestraintVisual.Look.GuardDragDragged, $"(custody {cw.Custody}, officer {g.State}, look {crv.Current})");
            Signal("h_dragging");
            yield return WaitSig("c_dragging_ok", 60);
            var p0 = g.transform.position;
            yield return new WaitForSeconds(2.5f);
            float walked = FlatDistance(p0, g.transform.position);
            float gap = FlatDistance(g.transform.position, cw.transform.position);
            RnCheck("host: he walks the client towards the outpost at about the drag pace, and the client's own body (which the client's machine moves) trails him", walked > 2.2f && walked < 3.6f && gap < 2.6f, $"({walked / 2.5f:0.00} m/s, the client's body {gap:0.00} m behind him)");
            RnCheck("host: the officer shows his half of the clip", g.GetComponent<RestraintVisual>().Current == RestraintVisual.Look.GuardDragGuard);
            Signal("h_walked");

            // ---- the struggle: the client's button reaches the host, the host's numbers come back
            yield return WaitSig("c_struggling", 60);
            yield return new WaitForSeconds(0.8f);
            RnCheck("host: the client's held button arrived: he struggles, the officer is at half pace", cw.StruggleHeld && cw.StruggleActive && Mathf.Abs(ga.speed - RestraintRules.GuardSpeed(true)) < 0.01f && crv.Current == RestraintVisual.Look.GuardDragStruggleDragged, $"(held {cw.StruggleHeld}, active {cw.StruggleActive}, speed {ga.speed}, look {crv.Current})");
            yield return WaitFor(() => g.Shoves >= 1, 8f, "the shove");
            RnCheck("host: after 2.5 s of struggling the officer stopped and shoved him", g.Shoves == 1 && cw.ShoveCount == 1, $"(shoves {g.Shoves}, count {cw.ShoveCount})");
            Signal("h_shoved");
            yield return WaitSig("c_struggle_off", 60);
            yield return new WaitForSeconds(0.6f);
            RnCheck("host: the button let go: no struggle, full pace again", !cw.StruggleHeld && !cw.StruggleActive && Mathf.Abs(ga.speed - RestraintRules.GuardSpeed(false)) < 0.01f);

            // ---- a hit officer lets go
            g.BulletHitHost(2.5f, false, null);
            yield return null; yield return null;
            RnCheck("host: the officer is hit: he lets go (not Dragging, nobody dragged), the drop is on", g.Dragging == null && cw.DraggedByGuard == null && g.Dropped == 1 && cw.DroppedBy == g && cw.Custody == CustodyState.Busted && crv.Current == RestraintVisual.Look.GuardShotDragged, $"(officer {g.State}, drops {g.Dropped}, look {crv.Current})");
            Signal("h_dropped");
            yield return WaitSig("c_drop_ok", 60);
            yield return new WaitForSeconds(RestraintRules.DropSeconds + 0.4f);
            RnCheck("host: after the drop the client lies hogtied where he was let go", crv.Current == RestraintVisual.Look.Hogtied, $"(look {crv.Current})");

            // ---- the host frees the client: hold E beside him (the officer who was shot is alert and would chase the rescuers, which the god mode lets him: he is put away for this part)
            g.DevCalm();
            ga.Warp(middle + new Vector3(0f, 0f, 45f));
            g.enabled = false;
            yield return new WaitForSeconds(0.5f);
            var lie = clientPlayer.transform.position;
            hostPlayer.PlaceAt(lie + Quaternion.Euler(0f, cw.transform.eulerAngles.y, 0f) * new Vector3(1.4f, 0f, 0f) + Vector3.up * 0.2f, Quaternion.identity);
            yield return new WaitForSeconds(0.6f);
            var hrescue = hw.GetComponent<PlayerRescue>();
            RnCheck("host: beside the client the host has a captive to free", PlayerRescue.FindCaptive(hw) == cw && hrescue != null, $"(distance {FlatDistance(hw.transform.position, cw.transform.position):0.0} m)");
            Signal("h_rescue1");
            float t1 = Time.time;
            PlayerRescue.TestHold = true;
            yield return WaitFor(() => cw.Custody == CustodyState.Free, 9f, "the client to be freed");
            float took = Time.time - t1;
            PlayerRescue.TestHold = false;
            RnCheck("host: four seconds of holding frees the client", cw.Custody == CustodyState.Free && took > 3.6f && took < 5.2f && cw.FreeProgress == 0f, $"({took:0.00} s)");
            Signal("h_freed");
            yield return WaitSig("c_freed_ok", 60);

            // ---- the client frees the host
            typeof(HeistManager).GetMethod("SetCustody", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hm, new object[] { hw, CustodyState.Busted, 0f, 1 });
            yield return new WaitForSeconds(1.2f);
            RnCheck("(test) the host lies hogtied (busted)", hw.Custody == CustodyState.Busted && hrv.Current == RestraintVisual.Look.Hogtied, $"(look {hrv.Current})");
            Signal("h_selfpos", PkV(hw.transform.position) + ";" + hw.transform.eulerAngles.y.ToString("0.0", CultureInfo.InvariantCulture));
            float t2 = Time.time;
            for (float w = 0f; w < 14f && hw.Custody != CustodyState.Free; w += 2f)
            {
                yield return new WaitForSeconds(2f);
                Log($"[RestraintNet] host waits: the client's target {(cw.RescueTarget ? cw.RescueTarget.name : "none")}, my progress {hw.FreeProgress:0.00}, can be freed {PlayerRescue.CanBeFreed(hw)}, client {cw.Custody} at {cw.transform.position}, me {hw.Custody} at {hw.transform.position}, client's key {cw.GetComponent<NetPlayer>().IsOwner}");
            }
            float took2 = Time.time - t2;
            RnCheck("host: the client held E beside him (his target is the owner's own variable): the host is free again", hw.Custody == CustodyState.Free && hw.FreeProgress == 0f, $"({took2:0.0} s after he lay there)");
            Signal("h_freed_me");
            yield return WaitSig("c_done", 60);
            Log($"[RestraintNet] host DONE fails={rnFails}");
            Log("DONE restraintnethost");
            Quit(rnFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ the client

        IEnumerator RestraintNetClientScenario()
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
            var hm = HeistManager.Instance;
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            var rb = wob.GetComponent<Rigidbody>();
            var rv = wob.GetComponent<RestraintVisual>();
            yield return WaitSig("h_scene", 400);
            yield return FloorReady(1, 240);
            var info = ReadSig("h_scene").Split(';');
            int guardId = int.Parse(info[0], CultureInfo.InvariantCulture);
            var start = UnPkV(info[1]); var dir = UnPkV(info[2]);
            yield return new WaitForSeconds(1.0f);
            var guard = NetIds.Get<GuardAI>(guardId);
            RnCheck("(test) the officer is here as a puppet with a restraint visual", guard != null && guard.IsPuppet && guard.GetComponent<RestraintVisual>() != null);
            var hostPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var hostWob = hostPlayer ? hostPlayer.GetComponent<WobblyPlayer>() : null;
            wob.TeleportTo(new Vector3(start.x, start.y + 1.1f, start.z) - dir * 0.8f, Quaternion.LookRotation(dir));
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.0f);
            Signal("c_there");

            yield return WaitSig("h_pinned", 120);
            yield return WaitFor(() => wob.Custody == CustodyState.Pinned, 10f, "pinned on this machine");
            yield return new WaitForSeconds(0.5f);
            RnCheck("client: pinned: he is tied up here too, lying (the capsule laid down, the clip on)", wob.Custody == CustodyState.Pinned && wob.IsLying && (rv.Current == RestraintVisual.Look.TieUp || rv.Current == RestraintVisual.Look.Hogtied), $"(look {rv.Current}, lying {wob.IsLying})");
            Signal("c_pinned_ok");

            yield return WaitSig("h_dragging", 120);
            yield return WaitFor(() => wob.DraggedByGuard != null, 10f, "the dragging officer to reach this machine");
            yield return new WaitForSeconds(0.4f);
            RnCheck("client: the officer who drags him is known here (the host's variable) and the two halves of the clip show: his own, and the officer's", wob.Custody == CustodyState.Busted && wob.DraggedByGuard == guard && rv.Current == RestraintVisual.Look.GuardDragDragged && guard.GetComponent<RestraintVisual>().Current == RestraintVisual.Look.GuardDragGuard, $"(custody {wob.Custody}, look {rv.Current}, officer's {guard.GetComponent<RestraintVisual>().Current})");
            var b0 = wob.transform.position;
            Signal("c_dragging_ok");
            yield return WaitSig("h_walked", 60);
            float moved = FlatDistance(b0, wob.transform.position);
            float gap = FlatDistance(guard.transform.position, wob.transform.position);
            RnCheck("client: his own body follows the officer (this machine moves it): it went a good way and is within a couple of metres of him", moved > 2.0f && gap < 3.2f, $"(moved {moved:0.0} m, {gap:0.0} m from the officer)");

            // ---- the struggle
            wob.testStruggle = true;
            yield return WaitFor(() => wob.StruggleActive, 8f, "the struggle to be confirmed by the host");
            RnCheck("client: the held button reached the host and came back: he struggles (the struggle clip) and the stamina runs down", wob.StruggleHeld && wob.StruggleActive && wob.StruggleStamina < 1f && rv.Current == RestraintVisual.Look.GuardDragStruggleDragged, $"(active {wob.StruggleActive}, stamina {wob.StruggleStamina:0.00}, look {rv.Current})");
            Signal("c_struggling");
            yield return WaitSig("h_shoved", 60);
            yield return new WaitForSeconds(0.4f);
            RnCheck("client: the shove is known here (the count went up) and heard (a thud)", wob.ShoveCount == 1 && rv.Shoves >= 1, $"(count {wob.ShoveCount}, heard {rv.Shoves})");
            RnCheck("client: his strains and scuffs were heard on his own machine, and ties and marks too", rv.Strains >= 1 && rv.Scuffs >= 2, $"(strains {rv.Strains}, scuffs {rv.Scuffs})");
            wob.testStruggle = false;
            Signal("c_struggle_off");

            // ---- the drop
            yield return WaitSig("h_dropped", 60);
            yield return WaitFor(() => wob.DraggedByGuard == null, 8f, "the officer to let go on this machine");
            yield return new WaitForSeconds(0.3f);
            RnCheck("client: the officer let go: nobody drags him, the drop shows (his half and the officer's)", wob.DraggedByGuard == null && wob.DroppedBy == guard && rv.Current == RestraintVisual.Look.GuardShotDragged, $"(look {rv.Current}, dropped by {(wob.DroppedBy ? wob.DroppedBy.name : "nobody")})");
            Signal("c_drop_ok");

            // ---- freed by the host
            yield return WaitSig("h_rescue1", 60);
            yield return WaitFor(() => wob.FreeProgress > 0.1f, 8f, "the freeing to start");
            RnCheck("client: while the host holds E beside him the free-up clip runs on him (the progress comes from the host)", wob.Custody == CustodyState.Busted && rv.Current == RestraintVisual.Look.FreeUpFreed, $"(progress {wob.FreeProgress:0.00}, look {rv.Current})");
            yield return WaitFor(() => wob.Custody == CustodyState.Free, 10f, "to be freed");
            yield return new WaitForSeconds(0.5f);
            RnCheck("client: freed: on his feet (the capsule stands, no clip on him, the ties off)", wob.Custody == CustodyState.Free && !wob.IsLying && rv.Current == RestraintVisual.Look.None && wob.FreeProgress == 0f, $"(lying {wob.IsLying}, look {rv.Current})");
            Signal("c_freed_ok");

            // ---- the client frees the host
            yield return WaitSig("h_selfpos", 60);
            var hinfo = ReadSig("h_selfpos").Split(';');
            var hostAt = UnPkV(hinfo[0]);
            yield return WaitFor(() => hostWob != null && hostWob.Custody == CustodyState.Busted, 10f, "the host to lie there on this machine");
            wob.TeleportTo(hostAt + Vector3.right * 1.3f + Vector3.up * 0.3f, Quaternion.identity);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.8f);
            var crescue = wob.GetComponent<PlayerRescue>();
            RnCheck("client: beside the host he has a captive to free", PlayerRescue.FindCaptive(wob) == hostWob, $"(distance {FlatDistance(wob.transform.position, hostWob.transform.position):0.0} m)");
            PlayerRescue.TestHold = true;
            for (float w = 0f; w < 14f && hostWob.Custody != CustodyState.Free; w += 2f)
            {
                yield return new WaitForSeconds(2f);
                Log($"[RestraintNet] client waits: able {crescue.Able}, working {crescue.Working}, candidate {(crescue.Candidate ? crescue.Candidate.name : "none")}, my target {(wob.RescueTarget ? wob.RescueTarget.name : "none")}, locked {wob.inputLocked}, host {hostWob.Custody} progress {hostWob.FreeProgress:0.00}, me {wob.transform.position}, host {hostWob.transform.position}, menu {Crosshair.MenuOpen}, stumbling {wob.IsStumbling}, hidden {wob.IsHidden}");
            }
            PlayerRescue.TestHold = false;
            yield return new WaitForSeconds(0.4f);
            RnCheck("client: the host is free again, and the client can walk (he is not held)", hostWob.Custody == CustodyState.Free && !wob.inputLocked && wob.RescueTarget == null, $"(locked {wob.inputLocked})");
            Signal("c_done");
            yield return WaitSig("h_freed_me", 60);
            Log($"[RestraintNet] client DONE fails={rnFails}");
            Log("DONE restraintnetclient");
            Quit(rnFails == 0 ? 0 : 1);
        }
    }
}
