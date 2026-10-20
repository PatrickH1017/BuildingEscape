using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode, with graphics: the 2026-10-17 changes of the rename session looked at on real floors - the windows in the main hallway doors, the
    /// "SECURITY OFFICE" signs, the small valuables on the desks (a contact sheet of all thirteen kinds and close-ups on real desks) and the checks behind the pictures
    /// (no furniture is loot, the kinds vary, every wide door has its windows). Run: <c>NetDevHarness.Etr</c> (pictures in %TEMP%\be_shot_etr_*.png).
    /// </summary>
    public partial class NetDevRunner
    {
        int etrFails;

        void EtrCheck(string name, bool ok, string detail = "")
        {
            if (!ok) etrFails++;
            Log($"[Etr] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static readonly string[] FurnitureWords = { "monitor", "printer", "copy machine", "water cooler", "keyboard", "crate", "briefcase", "microwave", "coffee machine", "projector", "desk phone", "notepad", "roll of tape", "croissant", "takeaway" };

        // ------------------------------------------------------------------ the loot scanner (needs a real Editor window: IMGUI is not drawn in batch mode)

        /// <summary>
        /// Hold Z in a room: a pulse sweeps that room and marks the valuables lying in it - and none lying in the rooms next to it - and the top of the screen names the room and
        /// what is in it; another room, another set of marks; a hallway says what it holds; nothing is marked at the docks; the marks fade. Pictures of the pulse and of the marks
        /// (%TEMP%\be_ui_etr_scan_*.png). Run in a non-batch Editor on the hidden desktop: <c>NetDevHarness.EtrScan</c>.
        /// </summary>
        Camera etrShotCam;

        /// <summary>A picture of the Game view (the HUD and the scan's marks included) from a corner of a room, high up, looking at its middle.</summary>
        IEnumerator RoomShot(RoomSpot r, string name)
        {
            if (!etrShotCam)
            {
                etrShotCam = new GameObject("EtrShotCam").AddComponent<Camera>();
                etrShotCam.fieldOfView = 78f; etrShotCam.nearClipPlane = 0.05f; etrShotCam.enabled = false;
            }
            var main = Camera.main;
            etrShotCam.transform.position = new Vector3(r.bounds.x0 + 0.6f, 2.5f, r.bounds.z0 + 0.6f);
            etrShotCam.transform.LookAt(new Vector3(r.bounds.Cx, 0.75f, r.bounds.Cz));
            if (main) main.enabled = false;
            etrShotCam.enabled = true; etrShotCam.tag = "MainCamera";
            yield return null;
            yield return Snap(name);
            etrShotCam.enabled = false; etrShotCam.tag = "Untagged";
            if (main) main.enabled = true;
        }

        IEnumerator EtrScanScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) UnityEditor.EditorWindow.GetWindow(gameView).maximized = true;
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            yield return new WaitForSeconds(1f);
            var scanner = LootScanner.Instance;
            EtrCheck("scan: the scanner is part of every run", scanner != null);

            // the docks: nothing to scan
            LootScanner.TestHold = true;
            yield return new WaitForSeconds(0.5f);
            EtrCheck("scan: at the Loading Docks holding the key scans nothing", !LootScanner.Scanning && scanner.MarkCount == 0 && scanner.Pulses == 0);
            LootScanner.TestHold = false;

            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1, 90, "floor 1");
            yield return new WaitForSeconds(1.0f);

            Collider carBox = ElevatorExtraction.Current ? ElevatorExtraction.Current.GetComponent<Collider>() : null;
            System.Func<Pickup, bool> scannable = p => p && p.isActiveAndEnabled && !p.IsHeld && FloorChecklist.Classify(p) >= 0 && !(carBox && carBox.bounds.Contains(p.transform.position));
            var all = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(scannable).ToList();
            // (the scan marks the shut safes of the room as well, the owner, 2026-10-19: they glow red)
            System.Func<int, int> safesIn = idx => FloorSafe.OnShow.Count(s => s && !s.IsOpen && gen.RoomSpotAt(s.transform.position).index == idx) + (GunSafeProp.Active && !GunSafeProp.Active.IsOpen && gen.RoomSpotAt(GunSafeProp.Active.transform.position).index == idx ? 1 : 0);
            // rooms by what they hold: a numbered room with several things for the main test, another one for the move, a hallway
            var byRoom = all.GroupBy(p => gen.RoomSpotAt(p.transform.position).index).Where(g => g.Key >= 0).OrderByDescending(g => g.Count()).ToList();
            var first = byRoom.First(g => gen.RoomSpotAt(g.First().transform.position).kind == 0 && !string.IsNullOrEmpty(gen.RoomSpotAt(g.First().transform.position).label));
            var second = byRoom.First(g => g.Key != first.Key && gen.RoomSpotAt(g.First().transform.position).kind == 0 && Vector3.Distance(g.First().transform.position, first.First().transform.position) < 40f);
            var roomA = gen.RoomSpotAt(first.First().transform.position);
            var roomB = gen.RoomSpotAt(second.First().transform.position);
            Log($"[Etr] scan: room {roomA.label} holds {first.Count()} things, room {roomB.label} holds {second.Count()}");

            // ---- stand in room A and hold the key
            player.TeleportTo(new Vector3(roomA.bounds.Cx, 1.1f, roomA.bounds.Cz), Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(1.0f);
            EtrCheck("scan: the player is in room A", gen.RoomSpotAt(player.transform.position).index == roomA.index, $"(in room {gen.RoomSpotAt(player.transform.position).label}, wanted {roomA.label})");
            var inA = all.Where(p => gen.RoomSpotAt(p.transform.position).index == roomA.index).ToList();
            var nextDoor = all.Where(p => gen.RoomSpotAt(p.transform.position).index != roomA.index && Vector3.Distance(p.transform.position, player.transform.position) < LootScanRules.MaxReach).ToList();
            Log($"[Etr] scan: standing at {player.transform.position}: {inA.Count} things in the room, {nextDoor.Count} more within {LootScanRules.MaxReach} m in other rooms");

            LootScanner.TestHold = true;
            yield return new WaitForSeconds(0.06f);
            yield return RoomShot(roomA, "etr_scan_pulse");
            yield return new WaitForSeconds(1.2f);
            yield return RoomShot(roomA, "etr_scan_marks");
            EtrCheck("scan: holding the key scans (a pulse has left)", LootScanner.Scanning && scanner.Pulses >= 1 && scanner.Room.index == roomA.index);
            EtrCheck("scan: every valuable in the room is marked", inA.Count >= 2 && inA.All(p => scanner.IsMarked(p)), $"({inA.Count(p => scanner.IsMarked(p))} of {inA.Count} marked)");
            EtrCheck("scan: not one in any other room is, however near", nextDoor.Count >= 1 && !nextDoor.Any(p => scanner.IsMarked(p)) && scanner.MarkCount == inA.Count + safesIn(roomA.index), $"({scanner.MarkCount} marks, {inA.Count} in the room, {nextDoor.Count(p => scanner.IsMarked(p))} of {nextDoor.Count} others)");
            EtrCheck("scan: the top of the screen names the room and counts what is in it", scanner.StatusLine.Contains("ROOM " + roomA.label) && scanner.StatusLine.Contains(inA.Count(p => FloorChecklist.Classify(p) == (int)ScanKind.Valuable || FloorChecklist.Classify(p) == (int)ScanKind.Cash) + " valuable"), $"('{scanner.StatusLine}')");

            // ---- walk into room B: the old marks go and the new room's things are marked
            player.TeleportTo(new Vector3(roomB.bounds.Cx, 1.1f, roomB.bounds.Cz), Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(0.45f);
            EtrCheck("scan: in another room the old room's marks are gone at once", !inA.Any(p => scanner.IsMarked(p)));
            yield return new WaitForSeconds(2.4f);
            var inB = all.Where(p => gen.RoomSpotAt(p.transform.position).index == roomB.index).ToList();
            EtrCheck("scan: ... and the new room's things are marked, only those", scanner.Room.index == roomB.index && inB.All(p => scanner.IsMarked(p)) && scanner.MarkCount == inB.Count + safesIn(roomB.index) && !inA.Any(p => scanner.IsMarked(p)), $"({scanner.MarkCount} marks, {inB.Count} in room {roomB.label})");
            EtrCheck("scan: while the key is held another pulse leaves every couple of seconds", scanner.Pulses >= 2, $"({scanner.Pulses} pulses)");

            // ---- a hallway: it says what it holds (here, nothing)
            var hall = gen.RoomSpotAt(Vector3.zero);
            Vector3 hallPos = Vector3.zero; bool found = false;
            for (int cx = 0; cx < 8 && !found; cx++)
                for (int cz = 0; cz < 5 && !found; cz++)
                {
                    var p = new Vector3((cx + 0.5f) * gen.cellSize, 1.1f, (cz + 0.5f) * gen.cellSize);
                    var spot = gen.RoomSpotAt(p);
                    if (spot.Valid && spot.kind == 2 && spot.theme == "hub" && all.All(x => gen.RoomSpotAt(x.transform.position).index != spot.index)) { hall = spot; hallPos = p; found = true; }
                }
            EtrCheck("scan: a hallway to try it in", found);
            if (found)
            {
                player.TeleportTo(hallPos, Quaternion.identity);
                yield return new WaitForSeconds(0.6f);
                yield return new WaitForSeconds(2.4f);
                EtrCheck("scan: in a hallway it says so and finds nothing there", scanner.Room.index == hall.index && scanner.MarkCount == 0 && scanner.StatusLine.Contains("THE HALLWAY") && scanner.StatusLine.Contains("nothing to steal"), $"('{scanner.StatusLine}')");
                yield return RoomShot(hall, "etr_scan_hall");
            }

            // ---- let go: the marks fade out
            player.TeleportTo(new Vector3(roomA.bounds.Cx, 1.1f, roomA.bounds.Cz), Quaternion.Euler(0f, 90f, 0f));
            yield return new WaitForSeconds(1.6f);
            EtrCheck("scan: back in room A it is marked again", inA.All(p => scanner.IsMarked(p)));
            LootScanner.TestHold = false;
            yield return new WaitForSeconds(0.2f);
            EtrCheck("scan: letting go stops scanning", !LootScanner.Scanning);
            yield return new WaitForSeconds(LootScanRules.MarkSeconds + 0.6f);
            EtrCheck("scan: the marks fade away six seconds after the last pulse", scanner.MarkCount == 0, $"({scanner.MarkCount} left)");

            // ---- a thing taken (into a pack) loses its mark
            LootScanner.TestHold = true;
            yield return new WaitForSeconds(1.2f);
            var anyPick = inA.FirstOrDefault(p => p && p.Loot == null);
            int before = scanner.MarkCount;
            if (anyPick) anyPick.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.3f);
            EtrCheck("scan: something taken (out of the world, into a pack) loses its mark", anyPick != null && scanner.MarkCount == before - 1, $"({before} -> {scanner.MarkCount})");
            if (anyPick) anyPick.gameObject.SetActive(true);
            LootScanner.TestHold = false;
            Log($"[Etr] DONE scan fails={etrFails}");
            Quit(etrFails == 0 ? 0 : 1);
        }
        // ------------------------------------------------------------------ the TV cutscene (needs a real Editor window: IMGUI is not drawn in batch mode)

        /// <summary>
        /// The run-start cutscene in an offline run: the TV on the docks' wall, the workers watching it (the player's character and a coworker), the camera in the docks in every beat, the
        /// workers' line at the end, and everything put back afterwards. One picture in the middle of every beat (%TEMP%\be_intro_tv_*.png). Run in a non-batch Editor on the hidden desktop:
        /// <c>NetDevHarness.IntroTv</c>.
        /// </summary>
        IEnumerator EtrTvScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) UnityEditor.EditorWindow.GetWindow(gameView).maximized = true;
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            var gen = FindFirstObjectByType<FloorGenerator>();
            yield return WaitFor(() => IntroCinematic.Playing, 90, "the Intro");
            var intro = IntroCinematic.Current;
            var tv = DockTv.Current;
            EtrCheck("tv: the docks have a TV on the wall", tv != null && intro.HasTv);
            if (tv == null) { Log($"[Etr] DONE tv fails={etrFails}"); Quit(1); yield break; }
            // where the TV is: on the south wall near the south-east corner, high up, facing into the room
            var spot = gen.DockTvPoint;
            EtrCheck("tv: it hangs on the south wall of the break room, facing into the room (the old TV of the main room is gone)", Mathf.Abs(tv.Origin.x - spot.x) < 0.01f && gen.BreakRoom.Contains(new Vector2(spot.x, spot.z + 0.5f)) && spot.z < 0.3f && tv.Forward == Vector3.forward && FindObjectsByType<DockTv>(FindObjectsSortMode.None).Length == 1);
            var screenTop = tv.Point(0f, DockTv.CentreHeight + DockTv.ScreenHeight * 0.5f, 0f);
            EtrCheck("tv: the screen is up on the wall, above head height at its lower edge", tv.Point(0f, DockTv.CentreHeight - DockTv.ScreenHeight * 0.5f, 0f).y > 1.6f && screenTop.y < 3.2f);
            EtrCheck("tv: the space in front of it is clear of storage (no boxes or cabinets within 4 m of the screen)",
                !FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r => r && r.enabled && r.name != "Floor" && r.bounds.size.y > 0.3f && r.bounds.size.y < 2.6f && !r.transform.IsChildOf(tv.transform) && !r.transform.IsChildOf(intro.transform)
                    && r.bounds.center.y < 2f && Mathf.Abs(r.bounds.center.x - tv.Origin.x) < 3.4f && r.bounds.center.z > 0.6f && r.bounds.center.z < 4.4f && r.bounds.size.x < 6f && r.gameObject.name != "Wall"
                    && (r.name == "box" || r.name.StartsWith("cabinet") || r.name.StartsWith("shelf") || r.name == "bookshelf" || r.name == "kshelve")));

            // the workers: the player's character and a coworker, in front of the TV, facing it
            EtrCheck("tv: two workers stand in the docks - the player's character and a coworker, who is the one that speaks", intro.WorkerCount == 2 && intro.WorkerLabel(intro.SpeakerAt) == IntroContent.CoworkerLabel && intro.WorkerLabel(intro.ListenerAt) == IntroContent.PlayerLabel(0));
            bool toonWorkers = intro.WorkerCount > 0;
            for (int i = 0; i < intro.WorkerCount; i++) toonWorkers &= intro.WorkerIsToon(i);
            EtrCheck("tv: the workers are Tiny Toon people, not the colourful Party characters", toonWorkers, $"({intro.WorkerCount} workers)");
            bool placed = true;
            for (int i = 0; i < intro.WorkerCount; i++)
            {
                var f = intro.WorkerFoot(i);
                placed &= f.x > spot.x - 3f && f.x < spot.x + 3f && f.z > 1.7f && f.z < 3.2f && Mathf.Abs(f.y) < 0.01f;
                placed &= Mathf.Abs(Mathf.DeltaAngle(intro.WorkerYaw(i), 180f)) < 20f;           // (they face the wall: south)
            }
            EtrCheck("tv: they stand 2 to 2.5 m from the wall, within 3 m of the TV's middle, and look at it", placed);
            EtrCheck("tv: the players' own bodies are not in the picture (hidden for the Intro)", WobblyPlayerRegistry.All.All(p => p.GetComponentsInChildren<Renderer>().All(r => !r.enabled)));

            // one picture in the middle of every beat; the camera is always inside the docks (never behind a wall)
            bool camOk = true, tvProgram = true;
            var beats = IntroRules.Beats;
            for (int i = 0; i < beats.Length; i++)
            {
                float at = (beats[i].start + beats[i].end) * 0.5f;
                if (i == IntroRules.BeatAt(IntroRules.JetStart)) at = IntroRules.JetStart + 1.5f;
                intro.TestSeek(at);
                yield return null; yield return null;
                var c = intro.DockCamera.transform.position;
                camOk &= c.x > 0.3f && c.x < 35.5f && c.z > 0.3f && c.z < 17.5f && c.y > 0.5f && c.y < 3.4f;
                tvProgram &= beats[i].shot == IntroShot.None ? !tv.OnProgram : tv.OnProgram;
                yield return Snap($"intro_tv_{i}_{beats[i].dock}");
            }
            EtrCheck("tv: the camera stays inside the docks in every beat", camOk);
            EtrCheck("tv: the TV shows the boss during the monologue and static after it", tvProgram);
            // the worker's line: the two of them turn to each other
            intro.TestSeek(31.5f);
            yield return new WaitForSeconds(0.8f);
            int s = intro.SpeakerAt, l = intro.ListenerAt;
            var toL = intro.WorkerFoot(l) - intro.WorkerFoot(s); toL.y = 0f;
            float want = Quaternion.LookRotation(toL, Vector3.up).eulerAngles.y;
            EtrCheck("tv: when they speak, the speaker turns to the one they talk to, and that one turns back", Mathf.Abs(Mathf.DeltaAngle(intro.WorkerYaw(s), want)) < 8f && Mathf.Abs(Mathf.DeltaAngle(intro.WorkerYaw(l), want + 180f)) < 8f, $"(speaker {intro.WorkerYaw(s):0}, listener {intro.WorkerYaw(l):0}, wanted {want:0})");
            // skip it: everything is put back
            LootScanner.TestHold = false;
            IntroCinematic.TestHold = true;
            yield return new WaitForSeconds(1.4f);
            IntroCinematic.TestHold = false;
            yield return new WaitForSeconds(0.4f);
            EtrCheck("tv: skipped, the Intro is over and the TV is back on standby", !IntroCinematic.Playing && DockTv.Current != null && !DockTv.Current.OnProgram);
            EtrCheck("tv: ... the players' bodies are back, the workers are gone and no cutscene camera is left", WobblyPlayerRegistry.All.Any(p => p.GetComponentsInChildren<Renderer>().Any(r => r.enabled))
                && GameObject.Find("IntroWorker_" + IntroContent.CoworkerLabel) == null && GameObject.Find("IntroCamera") == null && GameObject.Find("IntroOfficeCamera") == null && GameObject.Find("BossOffice") == null);
            yield return Snap("intro_tv_after");
            Log($"[Etr] DONE tv fails={etrFails}");
            Quit(etrFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ carrying together (no graphics needed)

        static GameObject YardBox(Transform yard, string name, Vector3 at, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(yard, false);
            go.transform.localPosition = at; go.transform.localScale = size;
            return go;
        }

        PlayerGrab YardCarrier(Transform yard, string name, Vector3 at)
        {
            var g = new GameObject(name);
            g.transform.SetParent(yard, false); g.transform.position = at;
            var hand = new GameObject("Hand").transform;
            hand.SetParent(g.transform, false); hand.localPosition = new Vector3(0f, 1.1f, 0.5f);
            var pg = g.AddComponent<PlayerGrab>();
            pg.hand = hand;
            return pg;
        }

        /// <summary>
        /// The 2026-10-17 carrying changes without people: two dummy carriers (a PlayerGrab with a hand, no body) hold an espresso machine (two lifters) in a yard far
        /// from the map, walk it about, drive it into a wall, let go of it, throw it and drop it out of the world. Run: <c>NetDevHarness.EtrCarry</c> (no graphics).
        /// </summary>
        IEnumerator EtrCarryScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1, 90, "floor 1");
            yield return new WaitForSeconds(0.5f);

            var yard = new GameObject("CarryYard").transform;
            yard.position = new Vector3(-400f, 0f, -400f);
            YardBox(yard, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(90f, 1f, 40f));
            YardBox(yard, "Wall", new Vector3(12f, 1.5f, 0f), new Vector3(0.4f, 3f, 14f));
            var a = YardCarrier(yard, "CarrierA", yard.position + new Vector3(0f, 0f, 0f));
            var b = YardCarrier(yard, "CarrierB", yard.position + new Vector3(1.2f, 0f, 0f));

            var go = gen.SpawnLoot(1, yard.position + new Vector3(0.6f, 0.6f, 2.5f), 1);      // the espresso machine: two lifters
            var loot = go.GetComponent<HeavyLoot>();
            var rb = go.GetComponent<Rigidbody>();
            var pick = go.GetComponent<Pickup>();
            EtrCheck("carry: the espresso machine needs two people", loot != null && loot.lifters == 2 && rb != null && pick != null);
            yield return new WaitForSeconds(1.0f);
            EtrCheck("carry: it was left alone when it fell on the ground (no damage)", loot.Integrity >= loot.maxIntegrity - 0.01f && !loot.Broken, $"(integrity {loot.Integrity:0})");

            // one carrier cannot lift it
            a.ApplyGrab(pick);
            yield return new WaitForSeconds(0.8f);
            EtrCheck("carry: one person alone cannot lift it", !loot.IsLiftable && rb.position.y < 1.0f && loot.Holders == 1, $"(y {rb.position.y - yard.position.y:0.00})");
            b.ApplyGrab(pick);
            yield return new WaitForSeconds(1.4f);
            Vector3 Mean() => (a.HandPoint + b.HandPoint) * 0.5f;
            EtrCheck("carry: two people lift it to the middle of their hands, with nothing pulling it down", loot.IsLiftable && Vector3.Distance(rb.position, Mean()) < 0.35f && !rb.useGravity, $"(off by {Vector3.Distance(rb.position, Mean()):0.00} m, gravity {rb.useGravity})");

            // walk: 9 m along the yard at 4 m/s; it does not trail, swing or get fast
            float worst = 0f, fastest = 0f;
            for (float t = 0f; t < 2.3f; t += Time.deltaTime)
            {
                a.transform.position += Vector3.right * 4f * Time.deltaTime; b.transform.position += Vector3.right * 4f * Time.deltaTime;
                worst = Mathf.Max(worst, Vector3.Distance(rb.position, Mean())); fastest = Mathf.Max(fastest, rb.linearVelocity.magnitude);
                yield return null;
            }
            yield return new WaitForSeconds(0.3f);
            EtrCheck("carry: walked at 4 m/s, it stayed close to its carriers' hands all the way (no trailing, no swinging)", worst < 0.9f && Vector3.Distance(rb.position, Mean()) < 0.4f, $"(worst {worst:0.00} m, now {Vector3.Distance(rb.position, Mean()):0.00} m, fastest {fastest:0.0} m/s)");
            EtrCheck("carry: carried, it never goes faster than the limit and takes no damage", fastest <= HeavyLoot.MaxCarrySpeed + 0.5f && loot.Integrity >= loot.maxIntegrity - 0.01f);

            // into a wall at 5 m/s: nothing breaks, nothing tunnels (the dummy carriers walk through it, a real pair could not), and when the carriers are through and far
            // away the piece is brought to them
            float wallX = yard.position.x + 12f;
            float maxX = float.MinValue; bool brokeInWall = false;
            for (float t = 0f; t < 3.4f; t += Time.deltaTime)
            {
                a.transform.position += Vector3.right * 5f * Time.deltaTime; b.transform.position += Vector3.right * 5f * Time.deltaTime;
                if (t < 1.2f) maxX = Mathf.Max(maxX, rb.position.x);
                brokeInWall |= loot.Broken || loot.Integrity < loot.maxIntegrity - 0.01f;
                yield return null;
            }
            yield return new WaitForSeconds(0.4f);
            EtrCheck("carry: driven into a wall at 5 m/s it takes no damage and does not go through", !brokeInWall && maxX < wallX + 0.1f, $"(furthest x {maxX - yard.position.x:0.0} of the wall at 12, integrity {loot.Integrity:0})");
            EtrCheck("carry: once its carriers are through and far away the stuck piece is brought to them", !loot.Broken && Vector3.Distance(rb.position, Mean()) < 1.6f, $"(now {Vector3.Distance(rb.position, Mean()):0.00} m from the middle of their hands)");

            // let go: it falls to the ground and is not hurt by the landing
            a.Release(false); b.Release(false);
            yield return new WaitForSeconds(0.2f);
            EtrCheck("carry: let go of, it is not held and gravity is back", loot.Holders == 0 && rb.useGravity);
            yield return new WaitForSeconds(1.6f);
            EtrCheck("carry: set down, the landing did no damage", loot.Integrity >= loot.maxIntegrity - 0.01f && !loot.Broken, $"(integrity {loot.Integrity:0})");

            // a person bumping it never hurts it (the real player is stood next to it and the piece is thrown at them at 8 m/s)
            var player = WobblyPlayerRegistry.All[0];
            player.TeleportTo(rb.position + new Vector3(0f, 0.2f, 3f), Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            rb.linearVelocity = (player.transform.position - rb.position).normalized * 8f;
            yield return new WaitForSeconds(0.8f);
            EtrCheck("carry: a person bumped by it, or bumping it, does not hurt it", loot.Integrity >= loot.maxIntegrity - 0.01f && !loot.Broken, $"(integrity {loot.Integrity:0})");
            player.TeleportTo(gen.transform.position + new Vector3(0f, 2f, 0f), Quaternion.identity);

            // thrown at a wall, a free piece IS hurt - by at most one knock's worth - and a second knock shatters it, loudly
            rb.position = new Vector3(yard.position.x + 8f, 1.0f, yard.position.z); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.2f);
            float before = loot.Integrity;
            rb.linearVelocity = new Vector3(14f, 0f, 0f);
            yield return new WaitForSeconds(0.9f);
            EtrCheck("carry: a free piece that hits a wall hard is hurt, but one knock never takes more than 45", loot.Integrity < before - 1f && before - loot.Integrity <= HeavyLoot.MaxHit + 0.5f && !loot.Broken, $"(integrity {before:0} -> {loot.Integrity:0})");
            rb.position = new Vector3(yard.position.x + 8f, 1.0f, yard.position.z); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.0f);
            rb.linearVelocity = new Vector3(14f, 0f, 0f);
            yield return new WaitForSeconds(0.9f);
            int guard = 0;
            while (!loot.Broken && guard++ < 4)
            {
                rb.position = new Vector3(yard.position.x + 8f, 1.0f, yard.position.z); rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.8f);
                rb.linearVelocity = new Vector3(14f, 0f, 0f);
                yield return new WaitForSeconds(0.8f);
            }
            EtrCheck("carry: knocked about enough it shatters, and everybody sees the burst and reads about it", loot == null || loot.Broken || go == null);
            yield return new WaitForSeconds(0.2f);
            EtrCheck("carry: ... the shards are there", FindObjectsByType<LootShards>(FindObjectsSortMode.None).Length >= 1);
            EtrCheck("carry: ... and the banner says it shattered", hm.BackupMessage != null && hm.BackupMessage.Contains("SHATTERED"), $"('{hm.BackupMessage}')");

            // a piece that falls out of the world is put back where it last stood
            var go2 = gen.SpawnLoot(0, yard.position + new Vector3(-10f, 0.5f, 3f), 1);                    // the golden stapler (one lifter)
            var loot2 = go2.GetComponent<HeavyLoot>(); var rb2 = go2.GetComponent<Rigidbody>();
            yield return new WaitForSeconds(1.8f);
            var resting = rb2.position;
            rb2.position = resting + new Vector3(0f, -40f, 0f); rb2.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.6f);
            EtrCheck("carry: a piece that fell out of the world is back where it last stood", rb2.position.y > -1f && Vector3.Distance(rb2.position, resting) < 1.0f, $"(at {rb2.position - yard.position}, was {resting - yard.position})");
            EtrCheck("carry: ... and the banner says so", hm.BackupMessage != null && hm.BackupMessage.Contains("fell out"), $"('{hm.BackupMessage}')");
            Destroy(yard.gameObject);
            Log($"[Etr] DONE carry fails={etrFails}");
            Quit(etrFails == 0 ? 0 : 1);
        }

        IEnumerator EtrScenario()
        {
            var hm = HeistManager.Instance;
            var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var cam = new GameObject("EtrCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.17f, 0.2f);
            cam.fieldOfView = 62f; cam.nearClipPlane = 0.03f; cam.enabled = false;

            // ---- the thirteen valuables on a plain table
            var stage = new GameObject("EtrStage").transform;
            stage.position = new Vector3(-300f, 0f, -300f);
            var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.transform.SetParent(stage, false);
            table.transform.localPosition = new Vector3(0f, 0.8f, 0f); table.transform.localScale = new Vector3(2.9f, 0.04f, 1.5f);
            table.GetComponent<Renderer>().material.color = new Color(0.45f, 0.36f, 0.28f);
            var lamp = new GameObject("EtrLamp").AddComponent<Light>();
            lamp.transform.SetParent(stage, false); lamp.transform.localPosition = new Vector3(0.3f, 2.6f, -0.6f);
            lamp.type = LightType.Point; lamp.range = 9f; lamp.intensity = 2.2f; lamp.color = new Color(1f, 0.96f, 0.88f);
            var build = typeof(FloorGenerator).GetMethod("BuildValuable", BindingFlags.NonPublic | BindingFlags.Instance);
            EtrCheck("the generator builds a valuable from a kind, a value, a spot and a yaw", build != null);
            int made = 0;
            foreach (var def in Valuables.All)
            {
                int col = (int)def.kind % 5, row = (int)def.kind / 5;
                var at = stage.position + new Vector3(-1.1f + col * 0.55f, 0.835f, -0.45f + row * 0.45f);
                var go = (GameObject)build.Invoke(gen, new object[] { def.kind, Valuables.ValueOf(def, 1, 0.5), at, 12f + 7f * (int)def.kind, stage });
                if (go && go.TryGetComponent(out Pickup pk) && pk.bankValue > 0 && pk.Label == def.label && go.TryGetComponent(out Throwable _) && go.TryGetComponent(out Rigidbody _) && go.TryGetComponent(out Collider _)) made++;
            }
            EtrCheck("all 13 kinds are made as pack items with a label, a value, a body and a collider", made == Valuables.All.Length, $"({made} of {Valuables.All.Length})");
            yield return new WaitForSeconds(0.6f);
            cam.transform.position = stage.position + new Vector3(0.2f, 2.35f, -2.15f);
            cam.transform.LookAt(stage.position + new Vector3(0f, 0.8f, -0.02f));
            ShotCam(cam, "etr_contact_sheet");
            cam.transform.position = stage.position + new Vector3(-0.9f, 1.25f, -0.75f);
            cam.transform.LookAt(stage.position + new Vector3(-0.85f, 0.82f, -0.38f));
            ShotCam(cam, "etr_contact_left");
            cam.transform.position = stage.position + new Vector3(0.7f, 1.2f, -0.7f);
            cam.transform.LookAt(stage.position + new Vector3(0.7f, 0.82f, -0.1f));
            ShotCam(cam, "etr_contact_right");
            Destroy(stage.gameObject);

            // ---- real floors
            foreach (int floor in new[] { 1, 2, 3, 4 })
            {
                tm.GoToLevel(floor);
                yield return new WaitUntil(() => tm.Loading || hm.Floor == floor);
                yield return WaitFor(() => !tm.Loading && hm.Floor == floor, 90, $"floor {floor}");
                yield return new WaitForSeconds(0.8f);
                string tag = $"floor {floor}";

                // the doors: the wide ones have their windows, the narrow ones (a room's) have none
                var dm = FindFirstObjectByType<DoorManager>();
                int wide = 0, wideDressed = 0, narrowPanes = 0, narrow = 0;
                SlidingDoor firstWide = null;
                foreach (var d in dm.Doors)
                {
                    int panes = (d.LowLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane") + d.HighLeaf.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Pane"));
                    if (d.Wide) { wide++; if (panes == 12) wideDressed++; if (firstWide == null) firstWide = d; }
                    else { narrow++; if (panes != 0 && panes != 16 && panes != 4) narrowPanes++; }
                }
                EtrCheck($"{tag}: every wide hallway door has its windows (2 leaves x 2 faces x 2 windows and a push bar)", wide == wideDressed, $"({wideDressed} of {wide} wide doors; {narrow} room doors)");
                EtrCheck($"{tag}: the room doors are plain (the outpost's own door keeps its wired glass, the doors of offices with a glass front are glass: 4 big panes)", narrowPanes == 0);
                if (floor == 1 && firstWide != null)
                {
                    var c = firstWide.Centre; var fwd = firstWide.AlongX ? Vector3.forward : Vector3.right;
                    cam.transform.position = c + fwd * 4.6f + Vector3.up * 1.6f; cam.transform.LookAt(c + Vector3.up * 1.4f);
                    ShotCam(cam, "etr_hall_door_front");
                    cam.transform.position = c - fwd * 4.6f + Vector3.up * 1.6f; cam.transform.LookAt(c + Vector3.up * 1.4f);
                    ShotCam(cam, "etr_hall_door_back");
                    cam.transform.position = c + fwd * 2.2f + Vector3.up * 1.7f; cam.transform.LookAt(c + Vector3.up * 1.5f);
                    ShotCam(cam, "etr_hall_door_close");
                }

                // the signs
                var signs = gen.PlanSigns();
                int security = signs.Count(s => s.security);
                EtrCheck($"{tag}: at least three security-office signs and some room signs on the walls", security >= 3 && signs.Count - security >= 1, $"({security} security, {signs.Count - security} room signs)");
                var holder = GameObject.Find("DirectionSigns");
                EtrCheck($"{tag}: the signs are one mesh with six materials", holder != null && holder.GetComponent<MeshRenderer>().sharedMaterials.Length == 6);
                if (floor == 1)
                {
                    var sec = signs.First(s => s.security);
                    var n = new Vector3(sec.nx, 0f, sec.nz);
                    cam.transform.position = new Vector3(sec.x, 1.55f, sec.z) + n * 3.4f; cam.transform.LookAt(new Vector3(sec.x, sec.y - 0.15f, sec.z));
                    ShotCam(cam, "etr_sign_security");
                    var rm = signs.FirstOrDefault(s => !s.security);
                    if (rm != null)
                    {
                        n = new Vector3(rm.nx, 0f, rm.nz);
                        cam.transform.position = new Vector3(rm.x, 1.55f, rm.z) + n * 3.4f; cam.transform.LookAt(new Vector3(rm.x, rm.y - 0.15f, rm.z));
                        ShotCam(cam, "etr_sign_rooms");
                    }
                }

                // the loot: nothing that is furniture, a variety of kinds, everything priced
                var loot = FindObjectsByType<Pickup>(FindObjectsSortMode.None).Where(p => p && p.isActiveAndEnabled && p.Value > 0 && !p.IsHeld).ToList();
                var furniture = loot.Where(p => FurnitureWords.Any(w => p.Label.ToLowerInvariant().Contains(w) && !p.Label.ToLowerInvariant().Contains("golden"))).ToList();
                // (the mugs count $25 each; the labels of the valuables and the treasures are known)
                var kinds = loot.Select(p => p.Label).Distinct().Where(l => Valuables.All.Any(v => v.label == l)).ToList();
                EtrCheck($"{tag}: nothing that is furniture is loot", furniture.Count == 0, furniture.Count > 0 ? "(" + string.Join(", ", furniture.Select(p => p.Label).Distinct().Take(5)) + ")" : $"({loot.Count} things worth money)");
                EtrCheck($"{tag}: the valuables come in at least 9 kinds", kinds.Count >= 9, $"({kinds.Count}: {string.Join(", ", kinds)})");
                int valuables = loot.Count(p => Valuables.All.Any(v => v.label == p.Label));
                EtrCheck($"{tag}: the floor holds its valuables (about {Valuables.Count(floor)}) and a few big treasures", valuables >= 40 && loot.Count(p => p.Loot != null) >= 3, $"({valuables} valuables, {loot.Count(p => p.Loot != null)} treasures)");
                if (floor == 1)
                {
                    // close-ups on real desks: the three nearest valuables to the middle of the first cell with several
                    var shown = loot.Where(p => Valuables.All.Any(v => v.label == p.Label)).OrderBy(p => (p.transform.position - new Vector3(27f, 0f, 27f)).sqrMagnitude).Take(3).ToList();
                    for (int k = 0; k < shown.Count; k++)
                    {
                        var p = shown[k].transform.position;
                        cam.transform.position = p + new Vector3(0.55f, 0.55f, -0.65f); cam.transform.LookAt(p + Vector3.up * 0.02f);
                        ShotCam(cam, "etr_desk_close" + k);
                    }
                    if (shown.Count > 0)
                    {
                        var p = shown[0].transform.position;
                        cam.transform.position = p + new Vector3(1.6f, 1.25f, -1.9f); cam.transform.LookAt(p);
                        ShotCam(cam, "etr_desk_room");
                    }
                }
            }
            Log($"[Etr] DONE fails={etrFails}");
            Quit(etrFails == 0 ? 0 : 1);
        }
    }
}
