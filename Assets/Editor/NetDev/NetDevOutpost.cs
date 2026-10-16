using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check (offline, Greybox scene) of the guard outposts: the locked door and its keypad, the one-way window, the quick hack
    /// (a clean one, a wrong key, a slow one, walking away), the alarm and the backup it calls, the notes and the journal, and the floor
    /// safe (code, lock-out, crowbar). The hack is played by feeding the same <see cref="HackSession"/> the panel runs (a batch run has no
    /// keyboard), so it covers everything but the key reading itself.
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

            // a run seed whose floor 1 has a safe (the same pure function the game uses)
            int seed = 1;
            while (!SafeRules.HasSafe(seed, 1)) seed++;
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

            // ================= the hack
            var at = new Vector3(ctl.KeypadPosition.x, 1.1f, ctl.KeypadPosition.z) + outward * 1.0f;
            void GoTo(Vector3 p) { player.transform.position = p; rb.linearVelocity = Vector3.zero; }
            GoTo(at);
            yield return new WaitForSeconds(0.3f);
            int guardsBefore = GuardAI.All.Count;

            // walking away backs out with no alarm
            HackPanel.Begin(ctl, player);
            OutpostCheck("hack: E on the keypad starts the hack and the screen is up", HackPanel.Running && OutpostUi.Current == OutpostUi.Kind.Hack && LiveHack() != null);
            GoTo(at + outward * (OutpostController.HackReach + 2f));
            yield return new WaitForSeconds(0.3f);
            OutpostCheck("hack: stepping away ends it with no alarm", !HackPanel.Running && !OutpostUi.Open && !ctl.AlarmOn && !ctl.Unlocked && !hm.BackupCalled);
            GoTo(at);
            yield return new WaitForSeconds(0.3f);

            // a wrong key sets the alarm off and calls backup
            HackPanel.Begin(ctl, player);
            yield return WaitForPrompt();
            var s1 = LiveHack();
            OutpostCheck("hack: after the short wait the first key is asked for", s1 != null && s1.Phase == HackPhase.Prompt && "WASD".IndexOf(s1.Expected) >= 0);
            if (s1 != null && s1.Phase == HackPhase.Prompt)
            {
                yield return null;
                s1.Press(WrongKeyFor(s1.Expected), Time.unscaledTime);
                yield return null; yield return null;
                OutpostCheck("hack: a wrong key sets the alarm off", ctl.AlarmOn && !ctl.Unlocked && !HackPanel.Running && !OutpostUi.Open);
                OutpostCheck("hack: the alarm calls backup, as a spotting guard does", hm.BackupCalled && hm.BackupActive);
                yield return new WaitForSeconds(1.5f);
                OutpostCheck("hack: two officers were sent", GuardAI.All.Count >= guardsBefore + 1, $"({guardsBefore} -> {GuardAI.All.Count} officers)");
                OutpostCheck("hack: the lock is jammed while the alarm sounds", ctl.Jammed && ctl.LockPrompt != null && ctl.LockPrompt.Contains("jammed"));
                HackPanel.Begin(ctl, player);
                OutpostCheck("hack: no new hack can be started while it is jammed", !HackPanel.Running);
                OutpostCheck("hack: the door is still locked", door.Locked && door.IsShut);
            }
            yield return new WaitForSeconds(OutpostController.AlarmSeconds + 1f);
            OutpostCheck("hack: the alarm stops by itself", !ctl.AlarmOn && ctl.LockPrompt != null && ctl.LockPrompt.Contains("Hack"));

            // too slow loses too
            GoTo(at);
            HackPanel.Begin(ctl, player);
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
            HackPanel.Begin(ctl, player);
            yield return WaitForPrompt();
            var s3 = LiveHack();
            int pressed = 0;
            while (s3 != null && !s3.Finished && pressed < HackSession.Steps)
            {
                yield return null;                                       // (a key cannot be pressed in the frame it appears)
                if (s3.Phase != HackPhase.Prompt) break;
                s3.Press(s3.Expected, Time.unscaledTime);
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

            // ================= the note and the journal
            {
                NoteProp note = null;
                foreach (var i in Interactable.All) if (i is NoteProp n) { note = n; break; }
                OutpostCheck("note: floor 1's outpost holds the note about floor 1's safe", note != null && note.safeFloor == 1 && note.outpostFloor == 1, note ? $"(about floor {note.safeFloor})" : "");
                if (note != null)
                {
                    OutpostCheck("note: it gives the code exactly when the rules say so", note.givesCode == SafeRules.NoteGivesCode(seed, 1));
                    GoTo(note.AimBox.center + Vector3.up * 0.4f);
                    int before = SafeIntel.Entries.Count;
                    note.Use(player);
                    yield return null;
                    OutpostCheck("note: reading it brings the note screen up", OutpostUi.Current == OutpostUi.Kind.Note);
                    SafeIntel.Entry e;
                    bool known = SafeIntel.TryGet(1, out e);
                    string room = gen.SafeRoomLabel(1);
                    OutpostCheck("note: the journal learns the safe's room", known && !string.IsNullOrEmpty(room) && e.room == room && SafeIntel.Entries.Count == before + 1, $"(room {room})");
                    OutpostCheck("note: and the code if the note gave it", known && (note.givesCode ? e.code == SafeRules.Code(seed, 1) : e.code == 0));
                    OutpostUi.Hide(OutpostUi.Kind.Note);
                    note.Use(player);
                    OutpostUi.Hide(OutpostUi.Kind.Note);
                    OutpostCheck("note: reading it again learns nothing new", SafeIntel.Entries.Count == before + 1);
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
                OutpostCheck("safe: it holds half the floor's quota in cash bundles", total == SafeRules.Cash(1) && total == 2150 && cash.Count == safe.BundleCount && cash.Count == SafeRules.Bundles(1), $"(${total} in {cash.Count} bundles, quota {TowerRules.QuotaFor(1)})");
                OutpostCheck("safe: the bundles are dormant while it is shut", dormant == cash.Count);
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
            else Log("[Outpost] SKIP crowbar: no weapons set up (Overtime Escape > Setup Weapons)");

            // ================= a floor without a safe still has its outpost, and the boss floor keeps the old red car
            {
                int plain = 2;
                while (SafeRules.HasSafe(seed, plain)) plain++;
                yield return LoadLevel(hm, plain);
                OutpostCheck($"floor {plain} (no safe): an outpost, no safe", OutpostController.Active != null && FloorSafe.Active == null && OutpostController.Active.Safe == null);

                yield return LoadLevel(hm, TowerRules.BossFloor);
                OutpostCheck("boss floor: the old red guest elevator, no outpost", OutpostController.Active == null && gen.Current.Find("GuestElevator") != null);
            }

            Log($"[Outpost] DONE fails={outpostFails}");
            Quit(outpostFails == 0 ? 0 : 1);
        }

        static bool AnyKeypadAvailable()
        {
            foreach (var i in Interactable.All) if (i is KeypadProp && i.Available) return true;
            return false;
        }
    }
}
