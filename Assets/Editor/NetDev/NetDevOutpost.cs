using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check (offline, Greybox scene) of the guard outposts: the locked door and its keypad, the one-way window, the quick hack
    /// (a clean one, a wrong key, a slow one, walking away), the alarm and the backup it calls, the notes and the journal, and the floor
    /// safe (code, lock-out, crowbar). The hack is played the way a player plays it: the crosshair on the locked door or the keypad (the
    /// game's own <see cref="Crosshair.Resolve"/> ray), E (the keypad's / the controller's <c>Use</c>), then W, A, S, D and Esc through the
    /// panel's own Update (<see cref="HackPanel.TestKeyDown"/> stands in for the keyboard, which a batch run has not), so it covers everything
    /// but the operating system's key reading. (Feeding the <see cref="HackSession"/> directly does not work: the panel only reports a result
    /// it saw come about in its own Update, so a session finished from outside leaves the screen up and the alarm off.)
    /// Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.NetDevHarness.Outpost   (look for "[Outpost] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int outpostFails;

        void OutpostCheck(string name, bool ok, string detail = "")
        {
            if (!ok) outpostFails++;
            Log($"[Outpost] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static readonly System.Reflection.FieldInfo HackSessionField =
            typeof(HackPanel).GetField("session", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        static HackSession LiveHack() => HackPanel.Instance != null && HackSessionField != null ? HackSessionField.GetValue(HackPanel.Instance) as HackSession : null;

        /// <summary>Waits until the panel shows a key (or the hack ends first).</summary>
        IEnumerator WaitForPrompt()
        {
            float t = 0f;
            while (t < 3f)
            {
                var s = LiveHack();
                if (s == null || s.Phase != HackPhase.Intro) yield break;
                yield return null; t += Time.unscaledDeltaTime;
            }
        }

        static char WrongKeyFor(char expected)
        {
            foreach (char c in HackSession.KeyPool) if (c != expected) return c;
            return 'W';
        }

        /// <summary>The key a hack letter (W, A, S or D) stands for.</summary>
        static KeyCode KeyOf(char letter) => (KeyCode)System.Enum.Parse(typeof(KeyCode), letter.ToString());

        /// <summary>
        /// Presses a key the way a player does: the panel reads it in its own Update, in the one frame it is "down" (what
        /// <c>Input.GetKeyDown</c> would say), then it is let go again. Use as <c>yield return Tap(key)</c>.
        /// </summary>
        IEnumerator Tap(KeyCode key)
        {
            HackPanel.TestKeyDown = k => k == key;
            yield return null;
            HackPanel.TestKeyDown = null;
        }

        IEnumerator OutpostScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();
            var pack = player.GetComponent<Backpack>();

            // a run seed whose floor 1 has a safe (the same pure function the game uses) and whose weapon floors 3 and 7 each have a safe with a bit of cash and a safe with a spare clip, so that the tests can try them
            int seed = 1;
            while (!SafeRules.HasSafe(seed, 1) || !SideItemsOn(seed, 4) || !SideItemsOn(seed, 7) || !SafeRules.HasSideWatch(seed, 1, 0) || !WatchOn(seed, 4) || !WatchOn(seed, 7) || !FirstClipSafeHoldsGun(seed, 4) || !TallOn(seed, 7)) seed++;          // (and a gold watch in floor 1's safe and in a safe of floors 3 and 7)
            tm.SetIntelSeed(seed);
            OutpostCheck("setup: the safe seed is the one asked for", tm.IntelSeed == seed && SafeRules.HasSafe(tm.IntelSeed, 1), $"(seed {seed})");

            yield return LoadLevel(hm, 1);
            var floorRoot = gen.Current;
            var dm = floorRoot ? floorRoot.GetComponentInChildren<DoorManager>() : null;
            var ctl = OutpostController.Active;

            // ================= the outpost and its door
            OutpostCheck("outpost: floor 1 has a guard outpost", ctl != null && floorRoot.Find("Outpost") != null);
            if (ctl == null || dm == null || ctl.Door == null) { Log($"[Outpost] DONE fails={outpostFails}"); Quit(1); yield break; }
            var door = ctl.Door;
            var outward = ctl.Outward;
            OutpostCheck("outpost: the front door is locked and shut", door.Locked && door.IsShut && dm.IsLocked(ctl.DoorIndex), $"(door {ctl.DoorIndex})");
            OutpostCheck("outpost: nothing is unlocked, ringing or open at the start", !ctl.Unlocked && !ctl.AlarmOn && ctl.Bits == 0);

            int keypads = 0;
            foreach (var i in Interactable.All) if (i is KeypadProp && i.Available) keypads++;
            OutpostCheck("outpost: one keypad beside the door can be worked", keypads == 1, $"({keypads})");

            int cars = 0;
            foreach (var d in floorRoot.GetComponentsInChildren<ElevatorDoor>(true)) cars++;
            OutpostCheck("outpost: the backup's elevator is still there (service elevator + the red one)", cars >= 2, $"({cars} elevator doors)");

            bool Blocks() => Physics.Raycast(door.Centre + outward * 2f + Vector3.up * 1.2f, -outward, 4f, ~0, QueryTriggerInteraction.Ignore);
            OutpostCheck("outpost: a ray through the shut door is stopped", Blocks());

            dm.Use(ctl.DoorIndex, true);
            yield return new WaitForSeconds(1f);
            OutpostCheck("outpost: E cannot open the locked door", door.IsShut && door.BlockerEnabled && Blocks(), $"(open {door.Logic.Open:0.00})");

            // ================= the one-way window
            {
                var glass = floorRoot.Find("Outpost/OneWayGlass");
                var mirror = floorRoot.Find("Outpost/OneWayMirror");
                OutpostCheck("window: a pane and a mirror quad", glass != null && mirror != null && glass.GetComponent<BoxCollider>() != null);
                if (glass != null && mirror != null)
                {
                    var mid = glass.position;
                    bool hit = Physics.Raycast(mid + outward * 2.5f, -outward, out var h, 5f, ~0, QueryTriggerInteraction.Ignore);
                    OutpostCheck("window: nobody (and nothing thrown) passes through it", hit && h.collider.transform == glass, hit ? $"(hit {h.collider.name})" : "(hit nothing)");
                    var pm = mirror.GetComponent<PlanarMirror>();
                    if (pm != null)
                    {
                        OutpostCheck("window: it is a mirror seen from the corridor", pm.CanSee(mid + outward * 2.5f + Vector3.up * 0.2f));
                        OutpostCheck("window: and nothing at all from inside the room", !pm.CanSee(mid - outward * 2.5f + Vector3.up * 0.2f));
                        OutpostCheck("window: the mirror faces the corridor", Vector3.Dot(pm.normal, outward) > 0.99f);
                    }
                    else Log("[Outpost] SKIP window: the mirror camera (the mirror shader is missing, so the window is dark glass)");
                }
            }

            // ================= the hack (played through the panel's own Update, started the way E starts it)
            var at = new Vector3(ctl.KeypadPosition.x, 1.1f, ctl.KeypadPosition.z) + outward * 1.0f;
            void GoTo(Vector3 p) { player.TeleportTo(p, player.transform.rotation); rb.linearVelocity = Vector3.zero; }
            // the camera sits behind the player as CameraFollow puts it; "Look" is the crosshair on a point of the world (the game's own ray)
            Vector3 Eye() => player.transform.position + outward * 2.6f + Vector3.up * 0.3f;
            AimTarget Look(Vector3 point) { var o = Eye(); return Crosshair.Resolve(o, point - o, player); }
            KeypadProp keypad = null;
            foreach (var i in Interactable.All) if (i is KeypadProp kp && i.Available) { keypad = kp; break; }
            HackPanel.TestKeyDown = null;

            // E on the locked door (the crosshair on it) starts the hack; Esc gives it up with no alarm
            GoTo(new Vector3(door.Centre.x, 1.1f, door.Centre.z) + outward * 1.3f);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
            int guardsBefore = GuardAI.All.Count;
            var aimDoor = Look(door.Centre + Vector3.up * 1.2f);
            OutpostCheck("aim: the crosshair on the locked door targets the door (E hacks it)", aimDoor.Kind == AimKind.Door && aimDoor.Door == ctl.DoorIndex, $"({aimDoor.Kind}, door {aimDoor.Door})");
            ctl.UseLockedDoor(player);                                             // (what DoorManager does on E when the door under the crosshair is locked)
            OutpostCheck("hack: E on the locked door starts the hack and the screen is up", HackPanel.Running && OutpostUi.Current == OutpostUi.Kind.Hack && LiveHack() != null);
            yield return Tap(KeyCode.Escape);
            yield return null;
            OutpostCheck("hack: Esc gives it up with no alarm", !HackPanel.Running && !OutpostUi.Open && !ctl.AlarmOn && !ctl.Unlocked && !hm.BackupCalled);

            // E on the keypad (the crosshair on it) starts it too; walking away backs out with no alarm
            GoTo(at);
            yield return new WaitForFixedUpdate();
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
            var aimPad = keypad != null ? Look(keypad.AimBox.center) : AimTarget.Nothing;
            OutpostCheck("aim: the crosshair on the keypad targets it (E hacks the lock)", keypad != null && aimPad.Kind == AimKind.Interact && aimPad.Interactable == keypad, $"({aimPad.Kind})");
            if (keypad == null) { Log($"[Outpost] DONE fails={outpostFails}"); Quit(1); yield break; }
            keypad.Use(player);                                                    // (what Crosshair does on E when the keypad is under the crosshair)
            OutpostCheck("hack: E on the keypad starts the hack and the screen is up", HackPanel.Running && OutpostUi.Current == OutpostUi.Kind.Hack && LiveHack() != null);
            GoTo(at + outward * (OutpostController.HackReach + 2f));
            yield return new WaitForSeconds(0.3f);
            OutpostCheck("hack: stepping away ends it with no alarm", !HackPanel.Running && !OutpostUi.Open && !ctl.AlarmOn && !ctl.Unlocked && !hm.BackupCalled);
            GoTo(at);
            yield return new WaitForSeconds(0.3f);

            // a key pressed before the first one is shown counts for nothing; a wrong key then sets the alarm off and calls backup
            keypad.Use(player);
            yield return Tap(KeyCode.S);
            OutpostCheck("hack: a key pressed before the first prompt is ignored (nobody could have seen it)", HackPanel.Running && !ctl.AlarmOn);
            yield return WaitForPrompt();
            var s1 = LiveHack();
            OutpostCheck("hack: after the short wait the first key is asked for", s1 != null && s1.Phase == HackPhase.Prompt && "WASD".IndexOf(s1.Expected) >= 0);
            if (s1 != null && s1.Phase == HackPhase.Prompt)
            {
                yield return null;
                yield return Tap(KeyOf(WrongKeyFor(s1.Expected)));
                yield return null;
                OutpostCheck("hack: a wrong key sets the alarm off", ctl.AlarmOn && !ctl.Unlocked && !HackPanel.Running && !OutpostUi.Open);
                OutpostCheck("hack: the alarm calls backup, as a spotting guard does", hm.BackupCalled && hm.BackupActive);
                yield return new WaitForSeconds(1.5f);
                OutpostCheck("hack: the officers were sent (two, or as many as the cap of the floor leaves room for: 6 on floor 1)", GuardAI.All.Count >= guardsBefore + Mathf.Min(2, hm.GuardCap - guardsBefore) && GuardAI.All.Count > guardsBefore, $"({guardsBefore} -> {GuardAI.All.Count} officers, cap {hm.GuardCap})");
                OutpostCheck("hack: the lock is jammed while the alarm sounds", ctl.Jammed && ctl.LockPrompt != null && ctl.LockPrompt.Contains("jammed"));
                keypad.Use(player);
                OutpostCheck("hack: no new hack can be started while it is jammed", !HackPanel.Running);
                OutpostCheck("hack: the door is still locked", door.Locked && door.IsShut);
            }
            yield return new WaitForSeconds(OutpostController.AlarmSeconds + 1f);
            OutpostCheck("hack: the alarm stops by itself", !ctl.AlarmOn && ctl.LockPrompt != null && ctl.LockPrompt.Contains("Hack"));

            // ================= no alarm panel inside the room (removed at the owner's request: nothing calls the chasing guards off)
            {
                int panels = 0;
                foreach (var i in Interactable.All) if (i is AlarmPanelProp) panels++;
                OutpostCheck("panel: there is no alarm panel any more", panels == 0, $"({panels})");
                OutpostCheck("panel: the host would not accept a press on it anywhere (not at the world origin either)", !ctl.InPanelReach(Vector3.zero) && !ctl.InPanelReach(ctl.KeypadPosition) && !ctl.InPanelReach(ctl.PanelPosition));
            }

            // too slow loses too
            GoTo(at);
            keypad.Use(player);
            yield return WaitForPrompt();
            var s2 = LiveHack();
            OutpostCheck("hack: a second hack can be tried after the alarm", s2 != null && s2 != s1 && s2.Phase == HackPhase.Prompt);
            if (s2 != null && s2.Phase == HackPhase.Prompt)
            {
                yield return new WaitForSeconds(HackSession.Window + HackSession.Grace + 0.4f);
                OutpostCheck("hack: a key not pressed within the second loses", s2.Phase == HackPhase.Lost && s2.Reason == "Too slow" && ctl.AlarmOn);
            }
            yield return new WaitForSeconds(OutpostController.AlarmSeconds + 1f);

            // four right keys open the lock
            GoTo(at);
            keypad.Use(player);
            yield return WaitForPrompt();
            var s3 = LiveHack();
            int pressed = 0;
            while (s3 != null && !s3.Finished && s3.Phase == HackPhase.Prompt && pressed < HackSession.Steps)
            {
                yield return Tap(KeyOf(s3.Expected));                    // (the panel reads it in its next Update: never in the frame the key appears)
                pressed++;
            }
            yield return null; yield return null;
            OutpostCheck("hack: four right keys in time win", s3 != null && s3.Phase == HackPhase.Won && pressed == HackSession.Steps, $"(phase {(s3 != null ? s3.Phase.ToString() : "none")}, {pressed} keys)");
            OutpostCheck("hack: the lock is open and the player is free again", ctl.Unlocked && !door.Locked && !OutpostUi.Open && !ctl.AlarmOn);
            yield return new WaitForSeconds(1f);
            OutpostCheck("hack: the door slides open by itself and stays open", !door.IsShut && !door.BlockerEnabled && door.Latched && !Blocks(), $"(open {door.Logic.Open:0.00})");
            OutpostCheck("hack: the keypad can not be worked any more", !AnyKeypadAvailable());
            OutpostCheck("hack: the network state says unlocked", (ctl.Bits & 1) != 0 && (ctl.Bits & 2) == 0);
            dm.Use(ctl.DoorIndex, false);
            yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 0.5f);
            OutpostCheck("hack: afterwards it is an ordinary door (E closes it)", door.IsShut && !door.Locked);
            dm.Use(ctl.DoorIndex, true);
            yield return new WaitForSeconds(0.9f);
            OutpostCheck("hack: ...and opens again", !door.IsShut);


            // ================= the computer, its post-it and the journal
            {
                ComputerProp compo = null;
                foreach (var i in Interactable.All) if (i is ComputerProp c0) { compo = c0; break; }
                OutpostCheck("computer: floor 1's outpost has one, with the record of floor 1's safe", compo != null && compo.safeFloor == 1 && compo.outpostFloor == 1, compo ? $"(about floor {compo.safeFloor})" : "");
                if (compo != null)
                {
                    OutpostCheck("computer: the code is on record exactly when the rules say so", compo.givesCode == SafeRules.NoteGivesCode(seed, 1));
                    var lgn = ComputerRules.CredentialsFor(seed, 1);
                    OutpostCheck("computer: its log-in is the one the rules give (capitals and digits)", compo.Credentials.user == lgn.user && compo.Credentials.pass == lgn.pass && ComputerRules.IsPostItText(lgn.user) && ComputerRules.IsPostItText(lgn.pass));
                    int postItParts = 0;
                    foreach (var tp in compo.GetComponentsInChildren<Transform>(true)) if (tp.name.StartsWith("PostIt")) postItParts++;
                    OutpostCheck("computer: a post-it with four lines of lettering is stuck to the monitor", postItParts == 6, $"({postItParts} parts)");
                    GoTo(compo.AimBox.center + Vector3.up * 0.4f);
                    int before = SafeIntel.Entries.Count;
                    compo.Use(player);
                    yield return null;
                    OutpostCheck("computer: using it brings the log-in screen up, logged out", OutpostUi.Current == OutpostUi.Kind.Computer && ComputerScreen.Showing && !ComputerScreen.LoggedIn);
                    OutpostCheck("computer: a wrong password does not open it", !ComputerScreen.TryLogin(lgn.user, "NOTIT") && !ComputerScreen.LoggedIn && SafeIntel.Entries.Count == before);
                    OutpostCheck("computer: a wrong user name does not either", !ComputerScreen.TryLogin("NOBODY1", lgn.pass) && !ComputerScreen.LoggedIn);
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    OutpostCheck("computer: no tab can be clicked before logging in, and nothing is learned", ComputerScreen.Tab == ComputerScreen.TabNone && SafeIntel.Entries.Count == before);
                    OutpostCheck("computer: the post-it's words, typed in small letters, open it", ComputerScreen.TryLogin(lgn.user.ToLowerInvariant(), "  " + lgn.pass.ToLowerInvariant() + " ") && ComputerScreen.LoggedIn);
                    OutpostCheck("computer: logged in, the Nav tab opens first (the owner, 2026-10-23) and nothing is learned until the Safes tab is clicked", SafeIntel.Entries.Count == before && ComputerScreen.Tab == ComputerScreen.TabNav);
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    SafeIntel.Entry e;
                    bool known = SafeIntel.TryGet(1, out e);
                    string room = gen.SafeRoomLabel(1);
                    OutpostCheck("computer: the Safes tab lists the room, and the journal learns it", ComputerScreen.Tab == ComputerScreen.TabSafes && known && !string.IsNullOrEmpty(room) && e.room == room && SafeIntel.Entries.Count == before + 1, $"(room {room})");
                    OutpostCheck("computer: ... and the code if it is on record", known && (compo.givesCode ? e.code == SafeRules.Code(seed, 1) : e.code == 0));
                    OutpostUi.Hide(OutpostUi.Kind.Computer);
                    yield return null;
                    compo.Use(player);
                    yield return null;
                    OutpostCheck("computer: a player who has logged in stays logged in on this floor", ComputerScreen.Showing && ComputerScreen.LoggedIn);
                    ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                    OutpostCheck("computer: opening the tab again learns nothing new", SafeIntel.Entries.Count == before + 1);
                    OutpostUi.Hide(OutpostUi.Kind.Computer);
                    yield return null;
                    OutpostCheck("computer: closing it frees the player", !OutpostUi.Open && !ComputerScreen.Showing);
                }
            }

            // ================= the safe
            var safe = FloorSafe.Active;
            OutpostCheck("safe: floor 1 has its safe", safe != null && safe.Floor == 1 && safe.Code == SafeRules.Code(seed, 1));
            if (safe != null && pack != null)
            {
                int total = 0, dormant = 0;
                var cash = new List<Pickup>();
                foreach (var p in floorRoot.GetComponentsInChildren<Pickup>(true)) if (p.label == "Cash bundle") cash.Add(p);
                foreach (var p in cash) { total += p.bankValue; if (!p.isActiveAndEnabled) dormant++; }
                OutpostCheck("safe: it holds half the floor's quota in cash bundles", total == SafeRules.Cash(1) && total == TowerRules.QuotaFor(1) / 2 && cash.Count == safe.BundleCount && cash.Count == SafeRules.Bundles(1), $"(${total} in {cash.Count} bundles, quota {TowerRules.QuotaFor(1)})");
                OutpostCheck("safe: the bundles are dormant while it is shut", dormant == cash.Count);
                OutpostCheck("safe: it also holds a gold watch (the seed says so), switched off while the door is shut", safe.WatchCount == 1 && safe.Watches[0] != null && !safe.Watches[0].activeSelf, $"({safe.WatchCount} watches)");
                var found = new List<Pickup>();
                Pickup.ScanAround(safe.transform.position + Vector3.up * 0.2f, 1.2f, found);
                bool sawCash = false;
                foreach (var p in found) if (p.label == "Cash bundle") sawCash = true;
                OutpostCheck("safe: nobody can pick them up through the shut door", !sawCash);

                GoTo(new Vector3(safe.transform.position.x, 1.1f, safe.transform.position.z) + safe.transform.forward * 1.2f);
                yield return new WaitForSeconds(0.2f);
                int wrong = safe.Code == 1234 ? 4321 : 1234;
                OutpostCheck("safe: a wrong code is refused", safe.HostAttempt(false, wrong, pack) == SafeReply.Wrong);
                OutpostCheck("safe: and the keypad locks up for a moment", safe.HostAttempt(false, safe.Code, pack) == SafeReply.LockedOut);
                yield return new WaitForSeconds(1.7f);
                OutpostCheck("safe: without a crowbar prying gets nowhere", FloorSafe.FindCrowbar(pack) == null && safe.HostAttempt(true, 0, pack) == SafeReply.NoCrowbar);
                var reply = safe.HostAttempt(false, safe.Code, pack);
                OutpostCheck("safe: the right code opens it", reply == SafeReply.Opened);
                safe.ApplyOpen(false);
                yield return new WaitForSeconds(1f);
                int live = 0;
                foreach (var p in cash) if (p && p.isActiveAndEnabled) live++;
                OutpostCheck("safe: the bundles come to life", safe.IsOpen && live == cash.Count && (ctl.Bits & 4) != 0);
                found.Clear();
                Pickup.ScanAround(safe.transform.position + Vector3.up * 0.2f, 1.2f, found);
                sawCash = false;
                foreach (var p in found) if (p.label == "Cash bundle" && p.bankValue > 0) sawCash = true;
                OutpostCheck("safe: and can be picked up", sawCash);
                var wtW = safe.WatchCount > 0 ? safe.Watches[0] : null;
                var wtPk = wtW ? wtW.GetComponent<Pickup>() : null;
                OutpostCheck("safe: the gold watch comes to life with the door, lies in the hollow, and is worth what the rules say (the owner, 2026-10-18)",
                    wtPk != null && wtW.activeSelf && wtPk.isActiveAndEnabled && wtPk.Label == "Gold Watch" && wtPk.bankValue == SafeRules.SideWatchValue(1) && WatchInHollow(safe, wtPk.transform.position),
                    wtPk ? $"({wtPk.Label} ${wtPk.bankValue}, at {safe.transform.InverseTransformPoint(wtPk.transform.position)})" : "(no watch)");
            }

            // ================= a crowbar opens a safe with no code (35% of the time it breaks)
            if (MeleeWeapon.Available && pack != null)
            {
                yield return LoadLevel(hm, 1);                                   // (the same floor again: a fresh, shut safe)
                safe = FloorSafe.Active;
                OutpostCheck("crowbar: the floor is built again with its safe shut", safe != null && !safe.IsOpen);
                if (safe != null)
                {
                    var cb = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, pack.transform.position + Vector3.up, null);
                    cb.transform.SetParent(gen.Current, true);
                    bool stored = pack.TryStore(cb.GetComponent<Pickup>());
                    OutpostCheck("crowbar: the player holds one", stored && FloorSafe.FindCrowbar(pack) != null);
                    var r = safe.HostAttempt(true, 0, pack);
                    OutpostCheck("crowbar: prying always opens it", r == SafeReply.Opened || r == SafeReply.OpenedCrowbarBroke, $"({r})");
                    bool gone = FloorSafe.FindCrowbar(pack) == null;
                    OutpostCheck("crowbar: it is used up exactly when it broke", gone == (r == SafeReply.OpenedCrowbarBroke), $"(broke {r == SafeReply.OpenedCrowbarBroke}, gone {gone})");
                }
            }
            else Log("[Outpost] SKIP crowbar: no weapons set up (Employees Take Revenge > Setup Weapons)");

            // ================= every office floor has a safe (the owner, 2026-10-18), and the boss floor keeps the old red car
            {
                yield return LoadLevel(hm, 2);
                OutpostCheck("floor 2: an outpost with a safe of its own (a safe on every floor)", OutpostController.Active != null && FloorSafe.Active != null && OutpostController.Active.Safe != null);
                OutpostCheck("floor 2: one safe, with cash bundles and no gun", FloorSafe.OnShow.Count == 1 && SafeRules.SafeCount(seed, 2) == 1 && FloorSafe.Active != null && FloorSafe.Active.BundleCount > 0 && FloorSafe.Active.GunCount == 0);

                // ---- floor 3 and up: several safes with pistols in them (the owner, 2026-10-18)
                foreach (int wsFloor in new[] { 4, 7 })
                {
                    yield return LoadLevel(hm, wsFloor);
                    string wsTag = $"floor {wsFloor}:";
                    foreach (var wsG in GuardAI.All) if (wsG) { wsG.sightRange = 0.1f; wsG.hearingMultiplier = 0f; }          // (they are only to be looked at)
                    int wsWant = SafeRules.SafeCount(seed, wsFloor);
                    var wsSafes = new List<FloorSafe>(FloorSafe.OnShow);
                    var wsCtl = OutpostController.Active;
                    bool wsNumbered = wsSafes.Count == wsWant, wsCodes = true, wsApart = true, wsShut = true, wsRooms = true, wsGuns = true, wsSide = true;
                    var wsCodeSet = new HashSet<int>(); var wsRoomSet = new HashSet<string>();
                    for (int wsI = 0; wsI < wsSafes.Count; wsI++)
                    {
                        var wsS = wsSafes[wsI];
                        wsNumbered &= wsS.Index == wsI && wsS.Floor == wsFloor;
                        wsCodes &= wsS.Code == SafeRules.Code(seed, wsFloor, wsI);
                        wsApart &= wsCodeSet.Add(wsS.Code);
                        wsShut &= !wsS.IsOpen;
                        wsSide &= wsS.BundleCount == (SafeRules.HasSideCash(seed, wsFloor, wsI) ? 1 : 0) && wsS.ClipCount == (SafeRules.HasSideClip(seed, wsFloor, wsI) ? 1 : 0) && wsS.WatchCount == (SafeRules.HasSideWatch(seed, wsFloor, wsI) ? 1 : 0);
                        int wsItems = SafeRules.WeaponContents(seed, wsFloor, wsI).Length;
                        wsGuns &= wsS.GunCount == wsItems && wsItems <= 1;
                        string wsLabel = gen.SafeRoomLabel(wsFloor, wsI);
                        wsRooms &= !string.IsNullOrEmpty(wsLabel) && wsRoomSet.Add(wsLabel);
                    }
                    OutpostCheck($"{wsTag} {wsWant} safes, numbered from 0, all shut, with the codes the rules give and no two alike", wsNumbered && wsCodes && wsApart && wsShut, $"({wsSafes.Count} safes)");
                    OutpostCheck($"{wsTag} each safe stands in a numbered room of its own", wsRooms, $"(rooms {string.Join(",", wsRoomSet)})");
                    OutpostCheck($"{wsTag} each holds what the rules say: a gun, a piece of armour or nothing, never two", wsGuns);
                    OutpostCheck($"{wsTag} each also holds a bit of cash, a spare clip and a gold watch exactly when the rules say (each its own item)", wsSide);

                    int wsInSafe = 0, wsTotal = 0, wsExpected = 0;
                    bool wsBarrels = true, wsDormant = true;
                    foreach (var wsS in wsSafes) wsExpected += GunsInSafe(seed, wsFloor, wsS.Index);          // (a safe that is not the first of its floor may hold armour instead of a gun)
                    foreach (var wsGun in Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (!wsGun || !FloorSafe.HoldsGun(wsGun.gameObject)) continue;
                        wsTotal++;
                        wsDormant &= !wsGun.gameObject.activeSelf;
                        foreach (var wsS in wsSafes)
                        {
                            var wsLocal = wsS.transform.InverseTransformPoint(wsGun.transform.position);
                            if (Mathf.Abs(wsLocal.x) < 0.39f && wsLocal.y > 0.04f && wsLocal.y < (wsS.Tall ? 1.6f : 0.5f) && wsLocal.z > -0.30f && wsLocal.z < 0.25f)
                            {
                                wsInSafe++;
                                var wsLay = RangedWeapon.LayoutOf(wsGun.kind);
                                bool wsSlanted = wsLay.fullBounds.size.x * wsLay.scale > 0.46f;                // (a long gun lies slanted across the hollow, its barrel out through the open door)
                                wsBarrels &= wsS.Tall && (wsGun.kind == RangedWeapon.Kind.Shotgun || wsGun.kind == RangedWeapon.Kind.Sniper) ? Vector3.Dot(wsGun.transform.right, Vector3.up) > 0.95f : Vector3.Dot(wsGun.transform.right, wsS.transform.forward) > (wsSlanted ? 0.55f : 0.95f);          // (a shotgun or a sniper rifle stands upright in a vertical safe)
                                break;
                            }
                        }
                    }
                    OutpostCheck($"{wsTag} the pistols lie inside their safes with the barrels to the door, out of the world while the doors are shut", wsTotal == wsExpected && wsInSafe == wsTotal && wsTotal >= 1 && wsBarrels && wsDormant, $"({wsTotal} pistols, {wsInSafe} inside)");
                    { bool wsTallOk = true; foreach (var wsT in wsSafes) wsTallOk &= wsT.Tall == SafeRules.HoldsTallGun(seed, wsFloor, wsT.Index); OutpostCheck($"{wsTag} a safe is vertical (1.7 m) exactly when it holds a shotgun or a sniper rifle, the gun standing upright in it (the owner, 2026-10-19)", wsTallOk); }

                    // the computer's record of this floor: every safe, with room and code (and nothing about what is inside)
                    ComputerProp wsComputer = null;
                    foreach (var wsAny in Interactable.All) if (wsAny is ComputerProp wsCp) { wsComputer = wsCp; break; }
                    OutpostCheck($"{wsTag} the outpost has its computer", wsComputer != null);
                    if (wsComputer != null)
                    {
                        OutpostCheck($"{wsTag} the computer's record is about this floor's own safes", wsComputer.safeFloor == wsFloor && wsComputer.outpostFloor == wsFloor);
                        SafeIntel.Reset();
                        SafeIntel.Add(wsFloor == 4 ? 7 : 4, 0, 1, "999", 1111);                     // (something learned on another floor: the Safes tab and the journal must not list it)
                        GoTo(wsComputer.AimBox.center + Vector3.up * 0.4f);
                        wsComputer.Use(player);
                        yield return null;
                        bool wsIn = ComputerScreen.TryLogin(wsComputer.Credentials.user, wsComputer.Credentials.pass);
                        ComputerScreen.SelectTab(ComputerScreen.TabSafes);
                        var wsOnFloor = new List<SafeIntel.Entry>();
                        SafeIntel.OnFloor(wsFloor, wsOnFloor);
                        bool wsRecord = wsIn && wsOnFloor.Count == wsWant && SafeIntel.Entries.Count == wsWant + 1;
                        for (int wsK = 0; wsK < wsWant; wsK++)
                        {
                            bool wsKnown = SafeIntel.TryGet(wsFloor, wsK, out var wsE);
                            wsRecord &= wsKnown && wsE.count == wsWant && wsE.room == gen.SafeRoomLabel(wsFloor, wsK)
                                && wsE.code == (SafeRules.RecordGivesCode(seed, wsFloor, wsK) ? SafeRules.Code(seed, wsFloor, wsK) : 0);
                        }
                        OutpostCheck($"{wsTag} the Safes tab lists every safe with its room and the code when the record has it (and says nothing about what is inside)", wsRecord, $"({SafeIntel.Entries.Count} records)");
                        OutpostCheck($"{wsTag} the first safe's code is always on record", SafeIntel.CodeFor(wsFloor, 0) == SafeRules.Code(seed, wsFloor, 0));
                        OutpostUi.Hide(OutpostUi.Kind.Computer);
                        yield return null;
                    }

                    // each safe opens with its code (a wrong one is refused first), and the outpost's state bits say which are open
                    int[] wsBit = { 4, 16, 32, 64 };
                    bool wsWrong = true, wsOpened = true, wsBits = true;
                    for (int wsJ = 0; wsJ < wsSafes.Count; wsJ++)
                    {
                        var wsS2 = wsSafes[wsJ];
                        GoTo(new Vector3(wsS2.transform.position.x, 1.1f, wsS2.transform.position.z) + wsS2.transform.forward * 1.2f);
                        yield return new WaitForSeconds(0.15f);
                        if (wsJ == 0)
                        {
                            wsWrong &= wsS2.HostAttempt(false, wsS2.Code == 1234 ? 4321 : 1234, pack) == SafeReply.Wrong;
                            yield return new WaitForSeconds(1.7f);
                        }
                        wsOpened &= wsS2.HostAttempt(false, wsS2.Code, pack) == SafeReply.Opened;
                        wsS2.ApplyOpen(false);
                        yield return new WaitForSeconds(0.2f);
                        for (int wsB = 0; wsB < wsSafes.Count; wsB++) wsBits &= ((wsCtl.Bits & wsBit[wsB]) != 0) == (wsB <= wsJ);
                    }
                    OutpostCheck($"{wsTag} a wrong code is refused, the right code opens each safe, and the state bits follow (one bit for each safe)", wsWrong && wsOpened && wsBits);
                    bool wsLive = true; int wsLiveCount = 0;
                    foreach (var wsGun2 in Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (wsGun2 && FloorSafe.HoldsGun(wsGun2.gameObject)) { wsLive &= wsGun2.gameObject.activeSelf && wsGun2.isActiveAndEnabled; wsLiveCount++; }
                    OutpostCheck($"{wsTag} with the doors open every pistol is in the world", wsLive && wsLiveCount == wsExpected, $"({wsLiveCount} of {wsExpected})");
                    // ---- what else the safes hold (the owner, 2026-10-18): the pistol, the clip and the cash lie apart, and the crosshair picks each one on its own
                    {
                        int wsAimed = 0, wsAimWanted = 0, wsItemsWanted = 0;
                        foreach (var xCount in wsSafes) wsItemsWanted += xCount.GunCount + xCount.BundleCount + xCount.ClipCount + xCount.WatchCount;
                        var xNear = new List<Pickup>();
                        foreach (var xSafe in wsSafes)
                        {
                            var xStand = new Vector3(xSafe.transform.position.x, 1.1f, xSafe.transform.position.z) + xSafe.transform.forward * 1.2f;
                            GoTo(xStand);
                            yield return new WaitForSeconds(0.15f);
                            var xEye = xStand + xSafe.transform.forward * 1.6f + Vector3.up * 0.9f;                  // (behind and above the player, as the camera is)
                            Pickup.ScanAround(xSafe.transform.position + xSafe.transform.forward * 0.1f + Vector3.up * 0.15f, 0.6f, xNear);
                            foreach (var xItem in xNear)
                            {
                                var xl = xSafe.transform.InverseTransformPoint(xItem.transform.position);
                                if (Mathf.Abs(xl.x) > 0.39f || xl.y < 0.04f || xl.y > (xSafe.Tall ? 1.6f : 0.5f) || xl.z < -0.30f || xl.z > 0.26f) continue;           // (only what lies in the hollow)
                                if (!xItem.TryGetBounds(out var xBox)) continue;
                                wsAimWanted++;
                                var xAim = Crosshair.Resolve(xEye, xBox.center - xEye, player);
                                if (xAim.Kind == AimKind.Pickup && xAim.Pickup == xItem) wsAimed++;
                                else Log($"[Outpost] not aimed at: {xItem.Label} at {xl} in safe {xSafe.Index}; the crosshair found {xAim.Kind} {(xAim.Pickup ? xAim.Pickup.Label : "-")}");
                            }
                        }
                        OutpostCheck($"{wsTag} every item of every open safe (pistol, clip, cash, watch) is aimed at on its own: one crosshair, one item", wsAimWanted == wsItemsWanted && wsAimed == wsAimWanted, $"({wsAimed} of {wsAimWanted} aimed, {wsItemsWanted} expected)");
                    }

                    if (wsFloor == 4)
                    {
                        // ---- taking them: with no pistol in the pack a clip is refused; with one, the clip's rounds go into its reserve and the clip is gone; the cash goes into the pack as an item of its own
                        FloorSafe tSafe = null;
                        foreach (var tS in wsSafes) if (tS.ClipCount > 0) { tSafe = tS; break; }
                        Pickup tPew = null, tClip = null;
                        if (tSafe != null)
                        {
                            var tNear = new List<Pickup>();
                            Pickup.ScanAround(tSafe.transform.position + tSafe.transform.forward * 0.1f + Vector3.up * 0.15f, 0.6f, tNear);
                            foreach (var tp in tNear)
                            {
                                var tl = tSafe.transform.InverseTransformPoint(tp.transform.position);
                                if (Mathf.Abs(tl.x) > 0.39f || tl.y < 0.04f || tl.y > 0.5f || tl.z < -0.30f || tl.z > 0.26f) continue;
                                if (tp.Label == "Pew") tPew = tp; else if (tp.GetComponent<AmmoClip>() != null) tClip = tp;
                            }
                        }
                        OutpostCheck($"{wsTag} a safe with a spare clip has its pistol and the clip lying in it", tSafe != null && tPew != null && tClip != null);
                        if (tSafe != null && tPew != null && tClip != null)
                        {
                            bool tNoPew = Vendor.GunOf(pack) == null;
                            bool tRefused = !pack.TryStore(tClip);
                            yield return new WaitForSeconds(0.1f);
                            OutpostCheck($"{wsTag} with no pistol in the pack the clip is refused and stays where it lies", tNoPew && tRefused && tClip != null && tClip.isActiveAndEnabled);
                            bool tGotPew = pack.TryStore(tPew);
                            yield return new WaitForSeconds(0.2f);
                            var tGun = Vendor.GunOf(pack);
                            OutpostCheck($"{wsTag} a pistol found in a safe holds its one clip and no spare ones (the owner, 2026-10-19)", tGun != null && tGun.Magazine.Rounds == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine && tGun.Magazine.Reserve == 0, $"(rounds {(tGun != null ? tGun.Magazine.Rounds : -1)}, reserve {(tGun != null ? tGun.Magazine.Reserve : -1)})");
                            if (tGun != null) tGun.Magazine.SetReserve(8);
                            int tReserve0 = tGun != null ? tGun.Magazine.Reserve : -1;
                            int tRounds = tClip != null ? tClip.GetComponent<AmmoClip>().rounds : 0;
                            bool tTook = pack.TryStore(tClip);
                            yield return new WaitForSeconds(0.2f);
                            OutpostCheck($"{wsTag} with the pistol in the pack the clip's rounds go into its reserve and the clip is gone", tGotPew && tTook && tGun != null && tRounds == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine && tGun.Magazine.Reserve == tReserve0 + tRounds && !tClip,
                                $"(reserve {tReserve0} -> {(tGun != null ? tGun.Magazine.Reserve : -1)})");
                        }
                        // the bit of cash: from whichever safe has one (a safe with a pistol only has none, one with the clip may not have any)
                        Pickup tCash2 = null;
                        foreach (var tS2 in wsSafes)
                        {
                            if (tS2.BundleCount == 0) continue;
                            var tNear2 = new List<Pickup>();
                            Pickup.ScanAround(tS2.transform.position + tS2.transform.forward * 0.1f + Vector3.up * 0.15f, 0.6f, tNear2);
                            foreach (var tp2 in tNear2)
                            {
                                var tl2 = tS2.transform.InverseTransformPoint(tp2.transform.position);
                                if (Mathf.Abs(tl2.x) > 0.39f || tl2.y < 0.04f || tl2.y > 0.5f || tl2.z < -0.30f || tl2.z > 0.26f) continue;
                                if (tp2.Label == "Cash bundle") tCash2 = tp2;
                            }
                            if (tCash2 != null) break;
                        }
                        OutpostCheck($"{wsTag} a safe with a bit of cash has a cash bundle lying in it", tCash2 != null);
                        if (tCash2 != null)
                        {
                            bool tGotCash = pack.TryStore(tCash2);
                            yield return new WaitForSeconds(0.2f);
                            OutpostCheck($"{wsTag} the bit of cash goes into the pack on its own, worth what the rules say", tGotCash && pack.Contains(tCash2) && tCash2.Value == SafeRules.SideCash(wsFloor), $"(${(tCash2 ? tCash2.Value : 0)})");
                        }
                        // the gold watch, from whichever safe has one
                        Pickup tWatch = null;
                        foreach (var tS3 in wsSafes)
                        {
                            if (tS3.WatchCount == 0) continue;
                            var tNear3 = new List<Pickup>();
                            Pickup.ScanAround(tS3.transform.position + tS3.transform.forward * 0.1f + Vector3.up * 0.15f, 0.6f, tNear3);
                            foreach (var tp3 in tNear3) if (tp3.Label == "Gold Watch" && WatchInHollow(tS3, tp3.transform.position)) tWatch = tp3;
                            if (tWatch != null) break;
                        }
                        OutpostCheck($"{wsTag} a safe with a gold watch has it lying in the hollow", tWatch != null);
                        if (tWatch != null)
                        {
                            bool tGotWatch = pack.TryStore(tWatch);
                            yield return new WaitForSeconds(0.2f);
                            OutpostCheck($"{wsTag} the gold watch goes into the pack on its own, worth what the rules say", tGotWatch && pack.Contains(tWatch) && tWatch.Value == SafeRules.SideWatchValue(wsFloor), $"(${(tWatch ? tWatch.Value : 0)})");
                        }
                    }
                    else
                    {
                        var wsFound = new List<Pickup>();
                        Pickup.ScanAround(wsSafes[0].transform.position + Vector3.up * 0.2f, 1.2f, wsFound);
                        Pickup wsPew = null;
                        foreach (var wsP in wsFound) if (wsP.GetComponent<RangedWeapon>() != null) { wsPew = wsP; break; }
                        OutpostCheck($"{wsTag} a gun of the open safe can be picked up", wsPew != null);
                        if (wsPew != null && pack != null)
                        {
                            bool wsStored = pack.TryStore(wsPew);
                            yield return new WaitForSeconds(0.3f);
                            OutpostCheck($"{wsTag} ... and goes into the backpack", wsStored && pack.Contains(wsPew));
                        }
                    }

                    if (wsFloor == 7)
                    {
                        // built again (fresh, shut safes): the keypad route of the offline game and the bits a client gets from the host
                        yield return LoadLevel(hm, wsFloor);
                        foreach (var wsG3 in GuardAI.All) if (wsG3) { wsG3.sightRange = 0.1f; wsG3.hearingMultiplier = 0f; }
                        wsCtl = OutpostController.Active;
                        var wsAgain = new List<FloorSafe>(FloorSafe.OnShow);
                        bool wsFresh = wsAgain.Count == wsWant && wsCtl != null && (wsCtl.Bits & (4 | 16 | 32 | 64)) == 0;
                        foreach (var wsS3 in wsAgain) wsFresh &= !wsS3.IsOpen;
                        OutpostCheck($"{wsTag} built again, every safe is shut and the state bits are clear", wsFresh);
                        if (wsFresh)
                        {
                            GoTo(new Vector3(wsAgain[2].transform.position.x, 1.1f, wsAgain[2].transform.position.z) + wsAgain[2].transform.forward * 1.2f);
                            yield return new WaitForSeconds(0.15f);
                            wsAgain[2].SubmitCode(wsAgain[2].Code, player);                      // (offline the keypad's answer is judged here, as the host would)
                            yield return new WaitForSeconds(0.2f);
                            OutpostCheck($"{wsTag} the third safe opens from its keypad, the others stay shut, and its bit is set", wsAgain[2].IsOpen && !wsAgain[0].IsOpen && !wsAgain[1].IsOpen && !wsAgain[3].IsOpen && (wsCtl.Bits & (4 | 16 | 32 | 64)) == 32);
                            wsCtl.NetApply((byte)(16 | 64 | 32));                            // (what a client does when the host's state says the second, third and fourth safe are open)
                            yield return new WaitForSeconds(0.3f);
                            int wsLive3 = 0;
                            foreach (var wsGun3 in Object.FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None)) if (wsGun3 && FloorSafe.HoldsGun(wsGun3.gameObject)) wsLive3++;
                            OutpostCheck($"{wsTag} the host's bits open exactly the second, third and fourth safe, and their pistols come into the world", !wsAgain[0].IsOpen && wsAgain[1].IsOpen && wsAgain[2].IsOpen && wsAgain[3].IsOpen
                                && (wsCtl.Bits & (4 | 16 | 32 | 64)) == (16 | 32 | 64) && wsLive3 == GunsInSafe(seed, wsFloor, 1) + GunsInSafe(seed, wsFloor, 2) + GunsInSafe(seed, wsFloor, 3), $"({wsLive3} pistols live)");
                        }
                    }
                }

                yield return LoadLevel(hm, TowerRules.BossFloor);
                OutpostCheck("boss floor: the old red guest elevator, no outpost", OutpostController.Active == null && gen.Current.Find("GuestElevator") != null);
            }

            Log($"[Outpost] DONE fails={outpostFails}");
            Quit(outpostFails == 0 ? 0 : 1);
        }

        /// <summary>Do the safes of this floor hold at least one gold watch (so that a test can try it)?</summary>
        static bool WatchOn(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HasSideWatch(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Do the safes of this floor hold at least one gun (a safe holds one only by luck: the owner, 2026-10-19), so that a test can try it?</summary>
        static bool GunsOn(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HoldsGun(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Does the first safe of this floor that holds a spare clip also hold a gun (the test takes the pistol and the clip from that safe)? Floor 3 holds neither: the first weapon floor with guns in its safes is 4.</summary>
        static bool FirstClipSafeHoldsGun(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HasSideClip(runSeed, floor, i)) return SafeRules.HoldsGun(runSeed, floor, i);
            return false;
        }

        /// <summary>Does a safe of this floor hold a shotgun, so that its vertical safe can be tried?</summary>
        static bool TallOn(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HoldsTallGun(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Does this world position lie in the hollow of the safe?</summary>
        static bool WatchInHollow(FloorSafe s, Vector3 world)
        {
            var l = s.transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) < 0.39f && l.y > 0.04f && l.y < 0.5f && l.z > -0.30f && l.z < 0.26f;
        }

        /// <summary>Do the safes of this floor, between them, hold at least one bit of cash and at least one spare clip (so that a test can try both)?</summary>
        static bool SideItemsOn(int runSeed, int floor)
        {
            bool cash = false, clip = false;
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) { cash |= SafeRules.HasSideCash(runSeed, floor, i); clip |= SafeRules.HasSideClip(runSeed, floor, i); }
            return cash && clip;
        }

        static bool AnyKeypadAvailable()
        {
            foreach (var i in Interactable.All) if (i is KeypadProp && i.Available) return true;
            return false;
        }
    }
}
