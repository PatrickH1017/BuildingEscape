using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Play Mode check (offline, Greybox scene) of the officers' symbol bubbles and sounds, the music and the sliding doors (E opens, E closes).
    /// Run: Unity -batchmode -executeMethod BuildingEscape.EditorTools.NetDevHarness.Doors   (look for "[Doors] DONE fails=0").
    /// </summary>
    public partial class NetDevRunner
    {
        int doorFails;

        void DoorCheck(string name, bool ok, string detail = "")
        {
            if (!ok) doorFails++;
            Log($"[Doors] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator LoadLevel(HeistManager hm, int floor)
        {
            TowerManager.Instance.GoToLevel(floor);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == floor);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == floor, 90, $"floor {floor}");
            yield return new WaitForSeconds(1f);
        }

        IEnumerator DoorsScenario()
        {
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 60, "basement");
            var gen = FindFirstObjectByType<FloorGenerator>();
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var rb = player.GetComponent<Rigidbody>();

            // ================= music: the basement has none, floor 1 gets its own track
            var md = MusicDirector.Instance;
            DoorCheck("music: the director exists", md != null);
            if (md == null) { Quit(1); yield break; }
            DoorCheck("music: silence in the basement", md.Current == MusicDirector.Mood.None || md.Current == MusicDirector.Mood.Elevator, $"({md.Current})");

            yield return LoadLevel(hm, 1);
            int seed = TowerManager.Instance.baseSeed;
            yield return WaitFor(() => md.IntenseReady, 60, "the intense track of floor 1");
            string track1 = md.TrackName;
            DoorCheck("music: floor 1 has the track its seed says", track1 == MusicPlan.TrackFor(seed, 1).Name, $"({track1})");

            // ================= officers: sounds and symbol bubbles on every guard
            int guards = 0, withSounds = 0, withBubbles = 0;
            foreach (var g in GuardAI.All)
            {
                if (!g) continue;
                guards++;
                if (g.GetComponent<GuardSounds>()) withSounds++;
                if (g.GetComponent<GuardBubble>()) withBubbles++;
            }
            DoorCheck("officers: every guard has keys + radio and a bubble", guards > 0 && withSounds == guards && withBubbles == guards, $"({guards} guards)");
            float waited = 0f;
            while (waited < 30f && !AnyBubbleText()) { waited += Time.unscaledDeltaTime; yield return null; }
            string said = FirstBubbleText();
            bool symbolsOnly = !string.IsNullOrEmpty(said);
            if (symbolsOnly) foreach (char c in said) symbolsOnly &= SymbolSpeech.Symbols.IndexOf(c) >= 0;
            DoorCheck("officers: a guard cursed within 30 s, in symbols only", symbolsOnly, $"(\"{said}\" after {waited:0.0} s)");

            // ================= music: backup starts the intense track and the next floor changes it
            player.TeleportTo(gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 1.1f, 7f), player.transform.rotation); rb.linearVelocity = Vector3.zero;   // out of the elevator
            yield return new WaitForSeconds(6.5f);                                                                                       // (the ride grace time)
            DoorCheck("music: no intense music before backup is called", md.Current == MusicDirector.Mood.None && !md.IntensePlaying, $"({md.Current})");
            hm.CallBackup(GuardAI.All.Count > 0 ? GuardAI.All[0] : null);
            yield return new WaitForSeconds(1.5f);
            DoorCheck("music: calling backup starts the intense track", md.Current == MusicDirector.Mood.Intense && md.IntensePlaying, $"({md.Current}, playing {md.IntensePlaying})");
            yield return LoadLevel(hm, 2);
            yield return WaitFor(() => md.IntenseReady, 60, "the intense track of floor 2");
            for (float fade = 0f; md.IntensePlaying && fade < 6f; fade += Time.unscaledDeltaTime) yield return null;   // (arriving in the car, the old track fades out)
            DoorCheck("music: the new floor starts without it", md.Current != MusicDirector.Mood.Intense && !md.IntensePlaying, $"({md.Current})");
            DoorCheck("music: floor 2 has a different track", md.TrackName != track1 && md.TrackName == MusicPlan.TrackFor(seed, 2).Name, $"({track1} -> {md.TrackName})");

            // ================= doors (the guards stay out of the way)
            var allGuards = new List<GuardAI>(GuardAI.All);
            foreach (var g in allGuards) if (g) g.gameObject.SetActive(false);
            var floorRoot = gen.Current;
            var dm = floorRoot ? floorRoot.GetComponentInChildren<DoorManager>() : null;
            DoorCheck("doors: the floor has its doors", dm != null && dm.Count > 0, dm ? $"({dm.Count} doors)" : "");
            if (dm == null || dm.Count == 0) { Log($"[Doors] DONE fails={doorFails}"); Quit(1); yield break; }


            // ================= after hours: a ceiling, light panels, a dim night look, and only some lights on
            {
                var ah = floorRoot.Find("AfterHours");
                DoorCheck("night: the floor has its ceiling and light panels", ah != null && ah.Find("Ceiling") != null && ah.Find("Panels") != null);
                int tiles = ah && ah.Find("Ceiling") ? ah.Find("Ceiling").childCount : 0;
                DoorCheck("night: a ceiling tile over every cell", tiles == gen.CellColumns * gen.CellRows, $"({tiles} tiles for {gen.CellColumns}x{gen.CellRows} cells)");
                bool solid = true;
                if (ah && ah.Find("Ceiling")) foreach (Transform t in ah.Find("Ceiling")) solid &= t.GetComponent<Collider>() != null;
                DoorCheck("night: the ceiling has colliders (the camera cannot rise through it)", tiles > 0 && solid);
                var lamps = ah ? ah.GetComponent<FloorLights>() : null;
                DoorCheck("night: the lit areas have real lights", lamps != null && lamps.Count >= 6, lamps ? $"({lamps.Count} lights)" : "");
                if (lamps != null)
                {
                    lamps.Scan(player.transform.position);
                    yield return new WaitForSeconds(1f);
                    DoorCheck("night: no more lights are on than the budget", lamps.WantedOn <= lamps.maxActive && lamps.Enabled <= lamps.maxActive + 2, $"(wanted {lamps.WantedOn}, enabled {lamps.Enabled}, max {lamps.maxActive})");
                    DoorCheck("night: the lights near the player are on", lamps.WantedOn > 0, $"({lamps.WantedOn})");
                }
                DoorCheck("night: flat, dim ambient light (not the daylight skybox)", RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat
                    && RenderSettings.ambientLight.grayscale < 0.45f && RenderSettings.ambientLight.grayscale > 0.12f, $"({RenderSettings.ambientLight})");
                float maxY = 0f;
                if (floorRoot) foreach (var r in floorRoot.GetComponentsInChildren<Renderer>()) if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
                DoorCheck("night: nothing but the ceiling reaches above the walls", maxY < gen.wallHeight + 0.6f, $"(top at {maxY:0.00} m)");
            }


            // ================= the elevator car: wood, steel, carpet, a mirror that reflects the player
            {
                var car = floorRoot ? floorRoot.Find("ElevatorRoom/CarInterior") : null;
                int pieces = car ? car.childCount : 0;
                DoorCheck("car: the elevator has its wood, steel, carpet and ceiling", car != null && pieces >= 7, $"({pieces} parts)");
                bool wood = false, steel = false;
                if (car) foreach (Transform t in car)
                {
                    var rr = t.GetComponent<Renderer>();
                    if (!rr || !rr.sharedMaterial) continue;
                    wood |= rr.sharedMaterial.name == "CarWood" && rr.sharedMaterial.mainTexture != null;
                    steel |= rr.sharedMaterial.name == "CarSteel";
                }
                DoorCheck("car: the wall panels are textured wood and there is stainless steel trim", wood && steel);
                var doorPanel = floorRoot ? (floorRoot.Find("ElevatorRoom/DoorPanel_E_A") ? floorRoot.Find("ElevatorRoom/DoorPanel_E_A") : floorRoot.Find("ElevatorRoom/DoorPanel_N_A")) : null;
                DoorCheck("car: the elevator doors are stainless steel", doorPanel && doorPanel.GetComponent<Renderer>().sharedMaterial.name == "CarSteel");
                DoorCheck("car: nothing in the interior has a collider", car != null && car.GetComponentsInChildren<Collider>(true).Length == 0);
                var mq = car ? car.Find("CarMirror") : null;
                var pm = mq ? mq.GetComponent<PlanarMirror>() : null;
                var middle = gen.CellCentre(Vector2Int.zero);
                DoorCheck("car: a mirror (with its mirror camera script)", mq != null && pm != null);
                if (mq != null && pm != null)
                {
                    var eye = middle + Vector3.up * 1.7f;
                    DoorCheck("car: the mirror faces the middle of the car and the panel is on the opposite wall", Vector3.Dot(pm.normal, eye - mq.position) > 3f);
                    DoorCheck("car: somebody standing in the car sees the mirror", pm.CanSee(eye));
                    DoorCheck("car: nobody behind the glass sees it", !pm.CanSee(mq.position - pm.normal * 3f));
                    DoorCheck("car: nobody far away in the building sees it", !pm.CanSee(middle + Vector3.up * 1.7f + new Vector3(60f, 0f, 60f)));
                    var cam = Camera.main;
                    if (cam && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        int before = pm.RenderCount;
                        bool drew = pm.Draw(cam);
                        DoorCheck("car: the mirror camera draws a picture", drew && pm.RenderCount == before + 1 && pm.LastSize.x >= 64 && pm.LastSize.y >= 64, $"({pm.LastSize.x}x{pm.LastSize.y})");
                    }
                    else Log("[Doors] SKIP car: the mirror camera (no camera or no graphics device in this run)");
                }
                var lamps = floorRoot ? floorRoot.Find("AfterHours") : null;
                var fl = lamps ? lamps.GetComponent<FloorLights>() : null;
                int carLampCount = 0;
                if (lamps) foreach (var l in lamps.GetComponentsInChildren<Light>(true))
                    if (l.type == LightType.Point && Mathf.Abs(l.transform.position.x - middle.x) < 3f && Mathf.Abs(l.transform.position.z - middle.z) < 3f) carLampCount++;
                DoorCheck("car: four point lights in the car's ceiling", fl != null && carLampCount == 4, $"({carLampCount})");
            }

            int beams = 0;
            var partitions = floorRoot.Find("Interior/Partitions");
            // (the beam over an ordinary doorway is "DoorHeader"; over the guard outpost's locked front door it is "LockHeader")
            if (partitions) foreach (Transform t in partitions) if (t.name == "DoorHeader" || t.name == "LockHeader") beams++;
            DoorCheck("doors: one door under every doorway beam", dm.Count == beams, $"({dm.Count} doors, {beams} beams)");
            int shut = 0;
            foreach (var d in dm.Doors) if (d.IsShut && d.BlockerEnabled) shut++;
            DoorCheck("doors: they all start shut, with their colliders on", shut == dm.Count, $"({shut} of {dm.Count})");

            var door = dm.Doors[0];
            var across = door.AlongX ? Vector3.forward : Vector3.right;
            bool Blocks() => Physics.Raycast(door.Centre + across * 1.5f + Vector3.up * 1.2f, -across, 3f, ~0, QueryTriggerInteraction.Ignore);
            DoorCheck("doors: a shut door stops a ray through the doorway", Blocks());

            // walking up to a door does not open it any more: a player has to press E
            var atDoor = door.Centre + across * 1.8f + Vector3.up * 1.1f;
            var away = gen.CellCentre(Vector2Int.zero) + new Vector3(0f, 1.1f, 7f);
            player.TeleportTo(atDoor, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(1.2f);
            DoorCheck("doors: somebody walking up does not open it", door.IsShut && door.BlockerEnabled && Blocks(), $"(open {door.Logic.Open:0.00})");
            DoorCheck("doors: standing at it and facing it, the player means that door", door.UseScore(atDoor, -across, DoorManager.UseReach) < float.MaxValue);
            DoorCheck("doors: far away he means no door", door.UseScore(away, Vector3.forward, DoorManager.UseReach) == float.MaxValue);

            // E opens it (this is what DoorManager does when the key is pressed)
            dm.Use(0, !door.Logic.WantOpen);
            yield return new WaitForSeconds(0.9f);
            DoorCheck("doors: pressing E opens it", !door.IsShut && door.Logic.Open > 0.99f && !door.BlockerEnabled, $"(open {door.Logic.Open:0.00})");
            DoorCheck("doors: an open door lets a ray through", !Blocks());

            // and it stays open when he walks away
            player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(SlidingDoor.Linger + SlidingDoor.WideCloseSeconds + 1.0f);
            DoorCheck("doors: it stays open after he has gone", !door.IsShut && door.Logic.Open > 0.99f && door.Latched, $"(open {door.Logic.Open:0.00})");

            // E again closes it
            player.TeleportTo(atDoor, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.3f);
            dm.Use(0, !door.Logic.WantOpen);
            yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 0.4f);
            DoorCheck("doors: pressing E again closes it", door.IsShut && door.BlockerEnabled && Blocks() && !door.Latched, $"(open {door.Logic.Open:0.00})");

            // standing right beside the door (not in it) does not hold it open: E closes it at once
            dm.Use(0, true);
            yield return new WaitForSeconds(0.9f);
            player.TeleportTo(door.Centre + across * 0.8f + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.2f);
            dm.Use(0, false);
            yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 0.4f);
            DoorCheck("doors: E closes it at once with the player standing right beside it", door.IsShut && door.BlockerEnabled && Blocks(), $"(open {door.Logic.Open:0.00})");

            // he can stand in the doorway and ask: it does not shut on him, it closes when he has stepped out
            dm.Use(0, true);
            yield return new WaitForSeconds(0.9f);
            player.TeleportTo(door.Centre + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.2f);
            dm.Use(0, false);
            yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 1.0f);
            DoorCheck("doors: a door asked to close does not shut on the player in the doorway", !door.IsShut && !door.BlockerEnabled, $"(open {door.Logic.Open:0.00})");
            player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 1.0f);
            DoorCheck("doors: it closes once he has stepped out", door.IsShut && door.BlockerEnabled, $"(open {door.Logic.Open:0.00})");

            // something in the doorway keeps it open
            dm.Use(0, true);
            yield return new WaitForSeconds(0.9f);
            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "DoorTestCrate";
            crate.transform.position = door.Centre + Vector3.up * 0.7f;
            crate.transform.localScale = new Vector3(0.5f, 1.2f, 0.5f);
            dm.Use(0, false);
            yield return new WaitForSeconds(SlidingDoor.Linger + SlidingDoor.WideCloseSeconds + 1.5f);
            DoorCheck("doors: a crate in the doorway keeps the door open", !door.IsShut && !door.BlockerEnabled);
            Destroy(crate);
            yield return new WaitForSeconds(0.6f + SlidingDoor.WideCloseSeconds + 0.6f);
            DoorCheck("doors: it closes once the crate is gone", door.IsShut && door.BlockerEnabled);

            // ================= the crosshair: E works what the camera's middle points at
            {
                DoorCheck("aim: the crosshair is installed", Crosshair.Instance != null);
                dm.Use(0, false);
                yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 0.6f);
                var along = door.AlongX ? Vector3.right : Vector3.forward;
                player.TeleportTo(door.Centre + across * 1.2f + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                // the camera sits behind and above the player, as CameraFollow puts it; "Look" is the crosshair on a point of the world
                Vector3 Eye() => player.transform.position + across * 2.6f + Vector3.up * 0.3f;
                AimTarget Look(Vector3 at) { var o = Eye(); return Crosshair.Resolve(o, at - o, player); }
                var doorMid = door.Centre + Vector3.up * 1.2f;

                var a = Look(doorMid);
                DoorCheck("aim: the crosshair on a shut door targets it", a.Kind == AimKind.Door && a.Door == 0, $"({a.Kind}, door {a.Door})");
                a = Look(door.Centre + along * (door.HalfGap + 1.2f) + Vector3.up * 1.2f);
                DoorCheck("aim: the crosshair on the wall beside it does not", a.Kind != AimKind.Door, $"({a.Kind})");

                dm.Use(0, true);
                yield return new WaitForSeconds(0.9f);
                Physics.SyncTransforms();
                a = Look(doorMid);
                DoorCheck("aim: ...and on the same door standing open (nothing there to hit)", a.Kind == AimKind.Door && a.Door == 0, $"({a.Kind}, door {a.Door})");

                // an item in the doorway beats the door: a crate that keeps it open can be picked up
                var mug = new GameObject("AimTestMug");
                mug.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.3f, 0.3f);
                mug.AddComponent<Pickup>();
                mug.transform.position = door.Centre + Vector3.up * 0.9f;
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                a = Look(mug.transform.position);
                DoorCheck("aim: the crosshair on an item in the doorway targets the item", a.Kind == AimKind.Pickup && a.Pickup == mug.GetComponent<Pickup>(), $"({a.Kind})");
                Destroy(mug);
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();

                // an item in the open air in front of the door, on the line to it
                dm.Use(0, false);
                yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + 0.6f);
                var spot = Vector3.Lerp(Eye(), doorMid, 0.75f);
                var nut = new GameObject("AimTestNut");
                nut.AddComponent<BoxCollider>().size = new Vector3(0.12f, 0.12f, 0.12f);
                var nutPickup = nut.AddComponent<Pickup>();
                nut.transform.position = spot;
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                a = Look(doorMid);
                DoorCheck("aim: a small item on the line of sight in front of the door is the target (not the door)", a.Kind == AimKind.Pickup && a.Pickup == nutPickup, $"({a.Kind}, at {Vector3.Distance(spot, player.transform.position):0.0} m)");
                nut.transform.position = spot + Vector3.up * 0.5f + along * 0.5f;       // well off the line of sight
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                a = Look(doorMid);
                DoorCheck("aim: an item off to one side is not", a.Kind == AimKind.Door, $"({a.Kind})");

                // an item hiding behind the shut door cannot be reached through it
                nut.transform.position = door.Centre - across * 0.6f + Vector3.up * 1.2f;
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                a = Look(nut.transform.position);
                DoorCheck("aim: an item behind a shut door is not targeted through it", a.Kind == AimKind.Door, $"({a.Kind})");
                Destroy(nut);

                // Just out of reach: nothing is targeted and the reticle goes grey (TooFar). The aim ray ends 1.2 m past the player's reach
                // (AimMath.EndT), so that is as far away as "too far" can be told.
                player.TeleportTo(door.Centre + across * 3.0f + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                a = Look(doorMid);
                DoorCheck("aim: pointing at a door just out of reach targets nothing and says too far", a.Kind != AimKind.Door && a.TooFar, $"({a.Kind}, tooFar {a.TooFar})");
                // Much farther away the ray does not reach the door at all: nothing is targeted.
                player.TeleportTo(door.Centre + across * 5.5f + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return new WaitForFixedUpdate();
                Physics.SyncTransforms();
                a = Look(doorMid);
                DoorCheck("aim: pointing at a door from far away targets nothing", a.Kind != AimKind.Door, $"({a.Kind}, tooFar {a.TooFar})");

                // the swing goes the way the crosshair does: a flat unit vector, in front of the player
                var sw = Crosshair.SwingForward(player.transform);
                DoorCheck("aim: the swing direction is flat, one metre long, and in front of the player",
                    Mathf.Abs(sw.y) < 1e-3f && Mathf.Abs(sw.magnitude - 1f) < 1e-3f && Vector3.Dot(sw, player.transform.forward) > 0.5f, $"({sw}, facing {player.transform.forward})");
                player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(0.3f);
            }

            // ================= guard QA: a real officer and a real doorway
            yield return GuardDoorQa(dm, player, rb, allGuards, away);

            // the prompt must stay out of the way of the backpack
            DoorCheck("doors: the floor's door manager is the active one", DoorManager.Active == dm);

            Log($"[Doors] DONE fails={doorFails}");
            Quit(doorFails == 0 ? 0 : 1);
        }


        // ------------------------------------------------------------------ guards and doors

        const System.Reflection.BindingFlags Inst = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        /// <summary>What one walk of a real officer past or through a real doorway showed.</summary>
        sealed class GuardWalk
        {
            public bool Crossed, Arrived;
            public float MaxOpen, OpenAtTouch = -1f, SpeedAtTouch, Seconds;
            public float GapAtTouch(SlidingDoor d) => OpenAtTouch < 0f ? 0f : OpenAtTouch * 2f * d.HalfGap;
        }

        /// <summary>A doorway with floor on both sides and a straight walkable route through the middle of it, and one that runs past it.</summary>
        bool FindDoorRoutes(DoorManager dm, out int through, out Vector3 from, out Vector3 to, out Vector3 across, out int pass, out Vector3 passFrom, out Vector3 passTo)
        {
            through = pass = -1; from = to = across = passFrom = passTo = default;
            for (int i = 0; i < dm.Count && (through < 0 || pass < 0); i++)
            {
                var d = dm.Doors[i];
                var ax = d.AlongX ? Vector3.forward : Vector3.right;
                var along = d.AlongX ? Vector3.right : Vector3.forward;
                for (float reach = 4f; reach >= 2.5f && through < 0; reach -= 0.5f)
                {
                    if (!NavMesh.SamplePosition(d.Centre + ax * reach, out var a, 1.0f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.SamplePosition(d.Centre - ax * reach, out var b, 1.0f, NavMesh.AllAreas)) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    if (path.corners.Length > 4 || PathLength(path) > Vector3.Distance(a.position, b.position) + 1.2f) continue;      // (roughly straight)
                    through = i; from = a.position; to = b.position; across = ax;
                }
                if (pass >= 0) continue;
                foreach (float side in new[] { 1f, -1f })
                for (float dist = 2.4f; dist >= 1.6f && pass < 0; dist -= 0.4f)
                {
                    var wa = d.Centre + ax * (side * dist) - along * 3.5f;
                    var wb = d.Centre + ax * (side * dist) + along * 3.5f;
                    if (!NavMesh.SamplePosition(wa, out var a, 0.4f, NavMesh.AllAreas)) continue;
                    if (!NavMesh.SamplePosition(wb, out var b, 0.4f, NavMesh.AllAreas)) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    bool keepsOff = true;                                       // the route never comes near the wall's middle plane
                    foreach (var c in path.corners) if (Mathf.Abs(Vector3.Dot(c - d.Centre, ax)) < 1.0f) keepsOff = false;
                    if (!keepsOff || PathLength(path) > 7.0f + 0.6f) continue;
                    pass = i; passFrom = a.position; passTo = b.position;
                }
            }
            return through >= 0 && pass >= 0;
        }

        static float PathLength(NavMeshPath path)
        {
            float len = 0f; var c = path.corners;
            for (int i = 0; i + 1 < c.Length; i++) len += Vector3.Distance(c[i], c[i + 1]);
            return len;
        }

        /// <summary>
        /// Puts the officer at <paramref name="from"/> (standing, blind, the door shut), lets <paramref name="start"/> set him walking to
        /// <paramref name="to"/> in some state, and watches the door until he gets there. <paramref name="door"/> is the doorway under test.
        /// </summary>
        IEnumerator WalkOfficer(GuardWalk result, GuardAI guard, NavMeshAgent agent, SlidingDoor door, DoorManager dm, int doorIndex,
                                Vector3 from, Vector3 to, Vector3 across, System.Action start)
        {
            if (!door.IsShut) { dm.Use(doorIndex, false); yield return new WaitForSeconds(SlidingDoor.WideCloseSeconds + SlidingDoor.Linger + 0.5f); }
            guard.gameObject.SetActive(false); yield return null; guard.gameObject.SetActive(true);
            agent.Warp(from); agent.ResetPath(); agent.isStopped = false;
            yield return null; yield return null;
            start();
            float t = 0f, startSide = Vector3.Dot(from - door.Centre, across);
            while (t < 14f)
            {
                yield return null; t += Time.deltaTime;
                float side = Vector3.Dot(guard.transform.position - door.Centre, across);
                result.MaxOpen = Mathf.Max(result.MaxOpen, door.Logic.Open);
                if (result.OpenAtTouch < 0f && Mathf.Abs(side) < 0.56f && Mathf.Abs(startSide) > 1f) { result.OpenAtTouch = door.Logic.Open; result.SpeedAtTouch = guard.CurrentSpeed; }
                if (Mathf.Abs(side) < 0.4f || Mathf.Sign(side) != Mathf.Sign(startSide)) result.Crossed = true;
                var flat = guard.transform.position - to; flat.y = 0f;
                if (flat.magnitude < 0.8f) { result.Arrived = true; break; }
            }
            result.Seconds = t;
            guard.gameObject.SetActive(false);                          // (he stops before he gets to anybody)
        }

        /// <summary>
        /// QA of the officers and the doors with one real officer and real doorways: through a doorway on patrol, going to look at a noise,
        /// sent as backup and chasing (the door must be wide open before his body gets to it, and shut again afterwards), past a doorway on
        /// patrol and chasing (the door must not open), then how long a chase lasts (on while the player is in his cone, over after 5 s
        /// out of it). The officer is blind (sight range 0.01) except in the chase tests, so only the state under test drives him; the
        /// player stays far away.
        /// </summary>
        IEnumerator GuardDoorQa(DoorManager dm, WobblyPlayer player, Rigidbody rb, List<GuardAI> guards, Vector3 away)
        {
            GuardAI guard = null;
            foreach (var g in guards) if (g && !g.Dead) { guard = g; break; }
            DoorCheck("guards: the floor has an officer to test with", guard != null);
            if (guard == null) yield break;

            FindDoorRoutes(dm, out int idx, out var from, out var to, out var across, out int passIdx, out var passFrom, out var passTo);
            DoorCheck("guards: found a doorway an officer can walk straight through", idx >= 0);
            if (passIdx < 0) Log("[Doors] SKIP the walk-past tests: no route on this floor runs past a doorway 1.6-2.4 m from its wall");
            Log($"[Doors] guard QA: through door {idx} (from {from} to {to}), past door {passIdx}");
            var fState = typeof(GuardAI).GetField("state", Inst);
            var fTarget = typeof(GuardAI).GetField("target", Inst);
            var fUnseen = typeof(GuardAI).GetField("unseenFor", Inst);
            if (fState == null || fTarget == null || fUnseen == null) { Log("[Doors] SKIP guard QA (the guard fields changed)"); yield break; }
            if (idx < 0) yield break;

            var door = dm.Doors[idx];
            var agent = guard.GetComponent<NavMeshAgent>();
            float savedSight = guard.sightRange, savedCatch = guard.catchRadius;
            var savedBlockers = guard.sightBlockers;
            guard.sightRange = 0.01f;                                   // he sees nothing: only the state under test drives him
            guard.catchRadius = 0.1f;                                   // and catches nobody
            player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            guard.gameObject.SetActive(true);

            // ---- through a doorway, four ways
            var modes = new (string label, System.Action start)[]
            {
                ("on patrol", () => { fState.SetValue(guard, GuardState.Patrol); agent.speed = guard.walkSpeed; agent.SetDestination(to); }),
                ("going to look at a noise", () => NoiseBus.Emit(to, 400f)),
                ("sent as backup", () => guard.Dispatch(to)),
                ("chasing the player", () => { player.TeleportTo(to + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero; fTarget.SetValue(guard, player); fUnseen.SetValue(guard, 0f); fState.SetValue(guard, GuardState.Chase); agent.speed = guard.chaseSpeed; }),
            };
            foreach (var m in modes)
            {
                var w = new GuardWalk();
                yield return WalkOfficer(w, guard, agent, door, dm, idx, from, to, across, m.start);
                float gap = w.GapAtTouch(door);
                DoorCheck($"guards: an officer {m.label} gets through the doorway", w.Crossed && w.Arrived, $"(crossed {w.Crossed}, arrived {w.Arrived}, {w.Seconds:0.0} s)");
                DoorCheck($"guards: the door is wide open before an officer {m.label} reaches it", gap >= 1.2f,
                    $"(gap {gap:0.00} m of {2f * door.HalfGap:0.0} m, he walked at {w.SpeedAtTouch:0.0} m/s)");
                player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return new WaitForSeconds(SlidingDoor.Linger + SlidingDoor.WideCloseSeconds + 1.0f);
                DoorCheck($"guards: the door shuts behind him ({m.label})", door.IsShut && door.BlockerEnabled, $"(open {door.Logic.Open:0.00})");
            }

            // ---- along a wall past a doorway: nothing opens
            if (passIdx >= 0)
            {
                var pdoor = dm.Doors[passIdx];
                var pacross = pdoor.AlongX ? Vector3.forward : Vector3.right;
                var passModes = new (string label, System.Action start)[]
                {
                    ("on patrol", () => { fState.SetValue(guard, GuardState.Patrol); agent.speed = guard.walkSpeed; agent.SetDestination(passTo); }),
                    ("going to look at a noise", () => NoiseBus.Emit(passTo, 400f)),
                    ("sent as backup", () => guard.Dispatch(passTo)),
                    ("chasing the player", () => { player.TeleportTo(passTo + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero; fTarget.SetValue(guard, player); fUnseen.SetValue(guard, 0f); fState.SetValue(guard, GuardState.Chase); agent.speed = guard.chaseSpeed; }),
                };
                foreach (var m in passModes)
                {
                    var w = new GuardWalk();
                    yield return WalkOfficer(w, guard, agent, pdoor, dm, passIdx, passFrom, passTo, pacross, m.start);
                    DoorCheck($"guards: an officer {m.label} who walks past a doorway does not open it", w.Arrived && w.MaxOpen <= 0f,
                        $"(arrived {w.Arrived} after {w.Seconds:0.0} s, the door opened up to {w.MaxOpen:0.00})");
                    player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                }
            }

            // ---- how long a chase lasts: on for as long as the player is in his cone, over after 5 s out of it
            guard.gameObject.SetActive(false); yield return null; guard.gameObject.SetActive(true);
            agent.Warp(from); agent.ResetPath(); agent.isStopped = false;
            yield return null; yield return null;
            guard.sightRange = 40f; guard.sightBlockers = 0;           // only the cone decides what he sees (no wall in the way)
            fTarget.SetValue(guard, player); fUnseen.SetValue(guard, 0f); fState.SetValue(guard, GuardState.Chase); agent.speed = guard.chaseSpeed;
            float inView = 0f; bool stillChasing = true;
            while (inView < 7f)
            {
                var ahead = guard.transform.forward; ahead.y = 0f;
                player.TeleportTo(guard.transform.position + ahead.normalized * 8f + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return null; inView += Time.deltaTime;
                if (guard.State != GuardState.Chase) { stillChasing = false; break; }
            }
            DoorCheck("guards: a chasing officer keeps after the player for as long as he is in the cone (7 s)", stillChasing, $"(state {guard.State} after {inView:0.0} s)");

            guard.sightRange = 0.01f;                                   // the player is out of his cone...
            float outOfView = 0f;
            while (outOfView < 3.5f && guard.State == GuardState.Chase) { yield return null; outOfView += Time.deltaTime; }
            DoorCheck("guards: 3.5 s out of the cone and he is still after the player", guard.State == GuardState.Chase, $"(state {guard.State} after {outOfView:0.0} s)");
            guard.sightRange = 40f;                                     // ...a glimpse...
            for (int i = 0; i < 3; i++)
            {
                var ahead = guard.transform.forward; ahead.y = 0f;
                player.TeleportTo(guard.transform.position + ahead.normalized * 8f + Vector3.up * 1.1f, player.transform.rotation); rb.linearVelocity = Vector3.zero;
                yield return null;
            }
            guard.sightRange = 0.01f;                                   // ...and out of it again
            outOfView = 0f;
            while (outOfView < 8f && guard.State == GuardState.Chase) { yield return null; outOfView += Time.deltaTime; }
            DoorCheck("guards: a glimpse starts the 5 s again, and then he gives up after 5 s out of the cone", guard.State == GuardState.Investigate && outOfView > 4.7f && outOfView < 5.6f,
                $"(state {guard.State} after {outOfView:0.00} s)");

            // ---- put everything back
            guard.gameObject.SetActive(false);
            fState.SetValue(guard, GuardState.Patrol);
            guard.sightRange = savedSight; guard.catchRadius = savedCatch; guard.sightBlockers = savedBlockers;
            player.TeleportTo(away, player.transform.rotation); rb.linearVelocity = Vector3.zero;
            Log("[Doors] guard QA finished");
        }

        static bool AnyBubbleText() => !string.IsNullOrEmpty(FirstBubbleText());

        static string FirstBubbleText()
        {
            foreach (var b in GuardBubble.All) if (b && !string.IsNullOrEmpty(b.Text)) return b.Text;
            return null;
        }
    }
}
