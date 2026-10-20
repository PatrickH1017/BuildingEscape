using System.Collections;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The demo's key step over the network. The pew lies INSIDE floor 4's guard outpost and the outpost's front door is locked, so a
    /// player has to hack the lock; in a team that is usually the client. <c>OutpostNet</c> (two processes:
    /// <c>runmp.ps1 -Method OutpostNet -Roles host,c1 -Players 2 -RunId on1</c>) plays it the way a player does, with the client doing the
    /// hacking: the crosshair on the keypad, E, then W / A / S / D through the panel's own Update (<see cref="HackPanel.TestKeyDown"/> stands in
    /// for the keyboard, which a batch run has not). A wrong key (the host must set the alarm off and call backup, and the client must see
    /// the siren and the jammed lock), then four right keys (the host must open the lock and the door on both machines), then the client
    /// takes the pew from the desk (the host sees it in his pack and in his hand).
    /// </summary>
    public partial class NetDevRunner
    {
        int outpostNetFails;

        void OutNetCheck(string name, bool ok, string detail = "")
        {
            if (!ok) outpostNetFails++;
            Log($"[OutpostNet] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        /// <summary>A ray through the outpost's doorway at chest height: stopped while the door is shut, free while it is open.</summary>
        static bool OutpostDoorwayBlocked(OutpostController ctl) =>
            Physics.Raycast(ctl.Door.Centre + ctl.Outward * 2f + Vector3.up * 1.2f, -ctl.Outward, 4f, ~0, QueryTriggerInteraction.Ignore);

        IEnumerator OutpostNetHostScenario()
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
            var tm = TowerManager.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            Signal("h_ready0");
            yield return WaitSig("c_ready0", 150);

            tm.GoToLevel(OutpostLoot.PewFloor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);

            var gen = FindFirstObjectByType<FloorGenerator>();
            var ctl = OutpostController.Active;
            OutNetCheck("host: floor 4 has its outpost, the front door locked and shut", ctl != null && ctl.Door != null && ctl.Door.Locked && ctl.Door.IsShut && OutpostDoorwayBlocked(ctl) && !ctl.Unlocked && !ctl.AlarmOn);
            var pews = PewPickups();
            var pew = pews.Count == 1 ? pews[0] : null;
            OutNetCheck("host: the pew lies inside the outpost", pew != null && gen.TryGetOutpostRoom(out var room) && room.Grow(0.4f).Contains(pew.transform.position.x, pew.transform.position.z), $"({pews.Count})");
            if (ctl == null || pew == null) { Log($"[OutpostNet] host DONE fails={outpostNetFails}"); Quit(1); yield break; }
            Signal("h_floor4", pew.NetId.ToString());
            yield return WaitSig("c_sees", 120);

            // ---- the client's first try: a wrong key. The host decides: alarm, jammed lock, backup.
            int guardsBefore = GuardAI.All.Count(g => g && !g.IsPuppet);
            yield return WaitSig("c_wrong", 120);
            yield return new WaitForSeconds(1.5f);
            OutNetCheck("host: the client's wrong key set the alarm off here, the lock still shut", ctl.AlarmOn && ctl.Jammed && !ctl.Unlocked && ctl.Door.Locked && ctl.Door.IsShut);
            int guardsNow = GuardAI.All.Count(g => g && !g.IsPuppet);
            OutNetCheck("host: and backup was called: two officers came", hm.BackupCalled && guardsNow >= guardsBefore + 2, $"({guardsBefore} -> {guardsNow} officers)");
            Signal("h_alarm_ok");

            // ---- the client's second try: four right keys. The host opens the lock and the door.
            yield return WaitSig("c_right", 150);
            yield return new WaitForSeconds(1.5f);
            OutNetCheck("host: the client's four right keys opened the lock here (unlocked, no alarm, the state bit set)", ctl.Unlocked && !ctl.AlarmOn && !ctl.Door.Locked && (ctl.Bits & 1) != 0 && (ctl.Bits & 2) == 0);
            OutNetCheck("host: the door slid open and stays open", !ctl.Door.IsShut && !ctl.Door.BlockerEnabled && !OutpostDoorwayBlocked(ctl));
            Signal("h_unlocked_ok");

            // ---- the client goes in and takes the pew: the host sees it in his pack and in his hand
            yield return WaitSig("c_pew", 120);
            var clientPlayer = NetPlayer.All.FirstOrDefault(p => !p.IsOwner);
            var cpack = clientPlayer ? clientPlayer.GetComponent<Backpack>() : null;
            OutNetCheck("host: the client's pack holds the pew and has it equipped", cpack != null && cpack.Contains(pew) && cpack.Equipped == pew);
            yield return new WaitForSeconds(0.5f);
            var cgc = clientPlayer ? clientPlayer.GetComponent<GunController>() : null;
            OutNetCheck("host: the client's character has the pew in his hand here", cgc != null && cgc.Gun != null && cgc.Held != null);
            Signal("h_pew_ok");
            yield return WaitSig("c_done", 90);
            Log($"[OutpostNet] host DONE fails={outpostNetFails}");
            Log("DONE outpostnethost");
            Quit(outpostNetFails == 0 ? 0 : 1);
        }

        IEnumerator OutpostNetClientScenario()
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
            yield return WaitSig("h_ready0", 90);
            RunSync.SyncChecked = false;
            Signal("c_ready0");
            yield return WaitSig("h_floor4", 150);
            var hm = HeistManager.Instance;
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            yield return WaitFor(() => RunSync.SyncChecked, 60, "floor checked against the host");
            yield return new WaitForSeconds(3f);

            var ctl = OutpostController.Active;
            int pewId = int.Parse(ReadSig("h_floor4").Trim());
            var pew = NetIds.Get<Pickup>(pewId);
            OutNetCheck("client: the outpost is built here too, the front door locked and shut", ctl != null && ctl.Door != null && ctl.Door.Locked && ctl.Door.IsShut && OutpostDoorwayBlocked(ctl) && !ctl.Unlocked && !ctl.AlarmOn);
            OutNetCheck("client: the pew is here with the host's id", pew != null && pew.GetComponent<RangedWeapon>() != null);
            KeypadProp keypad = null;
            foreach (var i in Interactable.All) if (i is KeypadProp kp && i.Available) { keypad = kp; break; }
            OutNetCheck("client: there is one keypad to work", keypad != null);
            if (ctl == null || pew == null || keypad == null) { Signal("c_sees"); Log($"[OutpostNet] client DONE fails={outpostNetFails}"); Quit(1); yield break; }
            HackPanel.TestKeyDown = null;
            Signal("c_sees");

            // ---- stand in front of the keypad (the host's copy of this player has to arrive: it only takes a hack from within 6.5 m)
            var me = NetPlayer.Local;
            var wob = me.GetComponent<WobblyPlayer>();
            var rb = wob.GetComponent<Rigidbody>();
            var pack = MyPack;
            var at = new Vector3(ctl.KeypadPosition.x, 1.1f, ctl.KeypadPosition.z) + ctl.Outward * 1.0f;
            wob.TeleportTo(at, wob.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(2.0f);
            var eye = at + ctl.Outward * 2.6f + Vector3.up * 0.3f;
            var aimed = Crosshair.Resolve(eye, keypad.AimBox.center - eye, wob);
            OutNetCheck("client: the crosshair on the keypad targets it", aimed.Kind == AimKind.Interact && aimed.Interactable == keypad, $"({aimed.Kind})");

            // ---- a wrong key: the client only sends its result; the host's verdict (alarm, jammed lock) comes back
            keypad.Use(wob);
            OutNetCheck("client: E on the keypad starts the hack", HackPanel.Running && OutpostUi.Current == OutpostUi.Kind.Hack);
            yield return WaitForPrompt();
            var s1 = LiveHack();
            if (s1 != null && s1.Phase == HackPhase.Prompt)
            {
                yield return null;
                yield return Tap(KeyOf(WrongKeyFor(s1.Expected)));
            }
            OutNetCheck("client: the wrong key ended the hack and let go of the player", !HackPanel.Running && !OutpostUi.Open);
            yield return WaitFor(() => ctl.AlarmOn, 15, "the host's alarm");
            OutNetCheck("client: the host's verdict arrived: the alarm sounds and the lock is jammed", ctl.AlarmOn && ctl.Jammed && !ctl.Unlocked && ctl.Door.Locked);
            keypad.Use(wob);
            OutNetCheck("client: no new hack can be started while it is jammed", !HackPanel.Running);
            Signal("c_wrong");
            yield return WaitSig("h_alarm_ok", 60);
            yield return WaitFor(() => !ctl.AlarmOn, OutpostController.AlarmSeconds + 15f, "the alarm to stop");
            OutNetCheck("client: the alarm stopped by itself and the lock can be tried again", !ctl.AlarmOn && ctl.LockPrompt != null && ctl.LockPrompt.Contains("Hack"));

            // ---- four right keys in time
            keypad.Use(wob);
            yield return WaitForPrompt();
            var s3 = LiveHack();
            int pressed = 0;
            while (s3 != null && !s3.Finished && s3.Phase == HackPhase.Prompt && pressed < HackSession.Steps)
            {
                yield return Tap(KeyOf(s3.Expected));
                pressed++;
            }
            OutNetCheck("client: four right keys in time won the hack", s3 != null && s3.Phase == HackPhase.Won && pressed == HackSession.Steps, $"(phase {(s3 != null ? s3.Phase.ToString() : "none")}, {pressed} keys)");
            yield return WaitFor(() => ctl.Unlocked, 15, "the host to open the lock");
            OutNetCheck("client: the host's verdict arrived: the lock is open, no alarm", ctl.Unlocked && !ctl.AlarmOn && !ctl.Door.Locked && !OutpostUi.Open);
            yield return new WaitForSeconds(1.2f);
            OutNetCheck("client: the door slid open and stays open", !ctl.Door.IsShut && !ctl.Door.BlockerEnabled && !OutpostDoorwayBlocked(ctl));
            OutNetCheck("client: the keypad can not be worked any more", !keypad.Available);
            Signal("c_right");
            yield return WaitSig("h_unlocked_ok", 60);

            // ---- into the room: take the pew
            wob.TeleportTo(pew.transform.position + new Vector3(0.6f, 1.1f, 0f), wob.transform.rotation);
            rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.2f);
            pack.TryStore(pew);
            yield return WaitFor(() => pack.Contains(pew) && pack.Equipped == pew, 15, "the pew in the pack");
            yield return new WaitForSeconds(0.6f);
            var gc = me.GetComponent<GunController>();
            OutNetCheck("client: the pew is in the pack, equipped, and in his hand", pack.Contains(pew) && pack.Equipped == pew && gc != null && gc.Gun != null && gc.Held != null);
            Signal("c_pew");
            yield return WaitSig("h_pew_ok", 60);
            Signal("c_done");
            Log($"[OutpostNet] client DONE fails={outpostNetFails}");
            Log("DONE outpostnetclient");
            Quit(outpostNetFails == 0 ? 0 : 1);
        }
    }
}
