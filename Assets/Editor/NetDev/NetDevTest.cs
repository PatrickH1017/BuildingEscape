using BuildingEscape.EditorTools;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Batch-mode entry points used to verify the networking work.</summary>
    public static class NetDevTest
    {
        public static void BuildAll()
        {
            GreyboxBuilder.Build();
            NetSceneBuilder.Build();
        }

        /// <summary>Pre-sets the define Steamworks.NET adds on first load, so it cannot recompile mid-Play in tests.</summary>
        public static void SetDefines()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            string cur = UnityEditor.PlayerSettings.GetScriptingDefineSymbols(target);
            if (!cur.Contains("STEAMWORKS_NET"))
                UnityEditor.PlayerSettings.SetScriptingDefineSymbols(target, string.IsNullOrEmpty(cur) ? "STEAMWORKS_NET" : cur + ";STEAMWORKS_NET");
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("[NetTest] defines: " + UnityEditor.PlayerSettings.GetScriptingDefineSymbols(target));
        }

        public static void PolicyTest()
        {
            RulesTest();
            AppearanceTest();
            VoiceCurveTest();
            CustodyTest();
            RoomNumberTest();
            LightingTest();
            ElevatorLookTest();
            AimMathTest();
            DoorOpenersTest();
            string v = NetConst.BuildVersion;
            Check("allow", JoinPolicy.Evaluate(false, 1, v, v, true) == JoinVerdict.Allow);
            Check("allow 4th (3 connected)", JoinPolicy.Evaluate(false, 3, v, v, true) == JoinVerdict.Allow);
            Check("full at 4", JoinPolicy.Evaluate(false, 4, v, v, true) == JoinVerdict.Full);
            Check("run started", JoinPolicy.Evaluate(true, 1, v, v, true) == JoinVerdict.RunStarted);
            Check("version", JoinPolicy.Evaluate(false, 1, "x", v, true) == JoinVerdict.WrongVersion);
            Check("not member", JoinPolicy.Evaluate(false, 1, v, v, false) == JoinVerdict.NotInLobby);
        }

        public static void RulesTest()
        {
            Check("13 floors + docks", TowerRules.OfficeFloors == 13 && TowerRules.Levels == 14);
            Check("group ends are 3,6,9,12", TowerRules.IsGroupEnd(3) && TowerRules.IsGroupEnd(6) && TowerRules.IsGroupEnd(9) && TowerRules.IsGroupEnd(12)
                && !TowerRules.IsGroupEnd(1) && !TowerRules.IsGroupEnd(2) && !TowerRules.IsGroupEnd(13) && !TowerRules.IsGroupEnd(0));
            Check("up needs the quota", TowerRules.UpFrom(1, false, 1) == -1 && TowerRules.UpFrom(1, true, 1) == 2);
            Check("no up from a group end", TowerRules.UpFrom(3, true, 4) == -1 && TowerRules.UpFrom(12, true, 13) == -1);
            Check("docks resume at the next floor", TowerRules.UpFrom(0, false, 4) == 4);
            Check("down goes to the docks", TowerRules.DownFrom(5) == 0 && TowerRules.DownFrom(0) == -1 && TowerRules.DownFrom(13) == -1);
            Check("no up from the boss floor", TowerRules.UpFrom(13, true, 13) == -1);
            Check("resume after leaving", TowerRules.ResumeAfterLeaving(3, true) == 4 && TowerRules.ResumeAfterLeaving(2, false) == 2);
            Check("labels", TowerRules.Label(0) == "Loading Docks" && TowerRules.Label(7) == "Floor 7" && TowerRules.Label(13).StartsWith("Floor 13"));
        }

        public static void AppearanceTest()
        {
            var a = PlayerAppearance.ForSlot(2);
            string text = a.Serialize();
            Check("appearance round trip", PlayerAppearance.TryParse(text, out var b) && b.Serialize() == text);
            Check("appearance default slot 2 is Bubble pink + chef hat", a.hat == 2 && a.body.r == 255);
            Check("rejects empty", !PlayerAppearance.TryParse("", out _));
            Check("rejects garbage", !PlayerAppearance.TryParse("hello-world", out _));
            Check("rejects bad hex", !PlayerAppearance.TryParse("ZZZZZZ-1-0-100-100", out _));
            Check("rejects too long", !PlayerAppearance.TryParse(new string('1', 80), out _));
            Check("clamps face / hat / build", PlayerAppearance.TryParse("FFFFFF-9-9-300-1", out var c)
                && c.face == 3 && c.hat == PlayerAppearance.HatNames.Length - 1
                && c.height == PlayerAppearance.MaxBuild && c.width == PlayerAppearance.MinBuild);
            var rng = new System.Random(7);
            bool allOk = true;
            for (int i = 0; i < 50; i++) allOk &= PlayerAppearance.TryParse(PlayerAppearance.Random(rng).Serialize(), out _);
            Check("50 random looks all parse", allOk);
            Check("look builds", PlayerAppearance.ForSlot(0).ToLook("x").name == "x");

            // Tiny Toon people: a sixth part names the character; old five-part strings stay Party looks
            var toon = PlayerAppearance.ForSlot(1);
            toon.model = "male_a";
            string ts = toon.Serialize();
            Check("toon model round trip", PlayerAppearance.TryParse(ts, out var t) && t.IsToon && t.model == "male_a" && t.Serialize() == ts);
            Check("toon look carries the model", t.ToLook("x").model == "male_a" && PlayerAppearance.ForSlot(0).ToLook("x").model == null);
            Check("old five-part string is a Party look", PlayerAppearance.TryParse("40E6B2-1-2-100-100", out var old) && !old.IsToon && old.model == null);
            Check("rejects bad model ids", !PlayerAppearance.TryParse("40E6B2-1-2-100-100-Male-A", out _)
                && !PlayerAppearance.TryParse("40E6B2-1-2-100-100-bad id", out _)
                && !PlayerAppearance.TryParse("40E6B2-1-2-100-100-" + new string('a', 30), out _)
                && !PlayerAppearance.TryParse("40E6B2-1-2-100-100-", out _));
            var longest = PlayerAppearance.ForSlot(0); longest.model = new string('a', 24);
            Check("longest legal toon string fits the network field", longest.Serialize().Length <= 61
                && PlayerAppearance.TryParse(longest.Serialize(), out _));
            Check("unknown model id still parses (friend on a newer build)", PlayerAppearance.TryParse("40E6B2-1-2-100-100-zz_new_guy", out var unk) && unk.model == "zz_new_guy");
            Check("ids", CharacterCatalog.ValidId("male_a") && !CharacterCatalog.ValidId("Male") && !CharacterCatalog.ValidId("a-b") && !CharacterCatalog.ValidId(""));
        }

        /// <summary>The voice loudness -> guard hearing distance curve.</summary>
        public static void VoiceCurveTest()
        {
            const float gate = 0.012f;
            Check("voice: silence and the gate are level 0", VoiceNoise.LevelFromRms(0f, gate) == 0f && VoiceNoise.LevelFromRms(gate, gate) == 0f && VoiceNoise.LevelFromRms(gate * 0.5f, gate) == 0f);
            float shoutRms = Mathf.Pow(10f, VoiceNoise.ShoutDb / 20f);
            Check("voice: a shout is level 1, louder stays 1", Mathf.Approximately(VoiceNoise.LevelFromRms(shoutRms, gate), 1f) && VoiceNoise.LevelFromRms(1f, gate) == 1f);
            bool rising = true; float prev = -1f;
            for (float rms = gate * 1.1f; rms < shoutRms * 0.95f; rms *= 1.25f)
            {
                float l = VoiceNoise.LevelFromRms(rms, gate);
                rising &= l > prev; prev = l;
            }
            Check("voice: every step louder is a higher level", rising);
            // 6 dB (twice the amplitude) is always the same step of the range
            float a = VoiceNoise.LevelFromRms(0.02f, gate), b = VoiceNoise.LevelFromRms(0.04f, gate), c = VoiceNoise.LevelFromRms(0.08f, gate);
            Check("voice: doubling the volume adds the same distance each time", Mathf.Abs((b - a) - (c - b)) < 0.001f);
            var go = new GameObject("vn"); var vn = go.AddComponent<VoiceNoise>();
            float r0 = vn.RadiusFor(0f), r1 = vn.RadiusFor(0.25f), r2 = vn.RadiusFor(0.5f), r3 = vn.RadiusFor(1f);
            Check("voice: the louder you speak the further guards hear (3 m ... 30 m)", r0 < r1 && r1 < r2 && r2 < r3 && Mathf.Approximately(r0, 3f) && Mathf.Approximately(r3, 30f));
            Object.DestroyImmediate(go);
        }
        /// <summary>The pinned / busted / backup / buy-back rules (CustodyRules, BackupClock) and the player's custody state.</summary>
        public static void CustodyTest()
        {
            Check("custody: a pin lasts 12 s and backup repeats every 30 s", CustodyRules.PinSeconds == 12f && CustodyRules.BackupIntervalSeconds == 30f);
            Check("custody: only a free, non-immune player can be pinned",
                CustodyRules.CanPin(CustodyState.Free, 10f, 0f) && !CustodyRules.CanPin(CustodyState.Pinned, 10f, 0f)
                && !CustodyRules.CanPin(CustodyState.Busted, 10f, 0f) && !CustodyRules.CanPin(CustodyState.Free, 11f, 13f));
            Check("custody: the team is busted when nobody is free", CustodyRules.TeamDown(0, 3) && !CustodyRules.TeamDown(1, 3) && CustodyRules.TeamDown(0, 1) && !CustodyRules.TeamDown(0, 0));
            float left = CustodyRules.PinSeconds; int ticks = 0;
            while (left > 0f) { left = CustodyRules.PinLeftAfter(left, 0.5f); ticks++; }
            Check("custody: a pinned player is busted after 12 s", ticks == 24 && CustodyRules.ShownSeconds(12f) == 12 && CustodyRules.ShownSeconds(0.01f) == 1);
            Check("custody: buy-back costs more the higher the floor", CustodyRules.BuyBackPrice(1) == 350 && CustodyRules.BuyBackPrice(12) == 900 && CustodyRules.CanAffordBuyBack(350, 1) && !CustodyRules.CanAffordBuyBack(349, 1));
            Check("custody: the vendor's lines", CustodyRules.VendorLine(true) == "One of your friends got got. You're lucky I have someone on the inside" && CustodyRules.VendorLine(false).StartsWith("Glad you made it"));

            var clock = new BackupClock();
            bool offFirst = !clock.Active && !clock.Tick(100f);
            clock.Start();
            int fired = 0;
            for (int i = 0; i < 950; i++) if (clock.Tick(0.1f)) fired++;
            clock.Start(); // a second call does not restart it
            Check("custody: after backup is called another officer comes every 30 s", offFirst && fired == 3 && clock.Repeats == 3);

            // the player's own state
            var go = new GameObject("custodyPlayer"); go.AddComponent<Rigidbody>().isKinematic = true; go.AddComponent<CapsuleCollider>();   // kinematic: no physics in an edit-mode test
            var wp = go.AddComponent<WobblyPlayer>();
            wp.usePartyCharacter = false;
            wp.ApplyCustody(CustodyState.Pinned, 12f, 0);
            bool pinned = wp.Custody == CustodyState.Pinned && wp.InCustody && Mathf.Approximately(wp.PinLeft, 12f);
            wp.IsHidden = true; wp.ApplyCustody(CustodyState.Busted, 0f, 4);
            bool busted = wp.Custody == CustodyState.Busted && wp.BustedOnLevel == 4 && !wp.IsHidden;
            wp.ApplyCustody(CustodyState.Free, 0f, 0);
            Check("custody: a player goes free -> pinned -> busted -> free", pinned && busted && wp.Custody == CustodyState.Free && !wp.InCustody);
            Object.DestroyImmediate(go);
        }
        /// <summary>The room numbers: floor digit first, 01-99 per round, a plate over every numbered room's doorway facing away from the room.</summary>
        public static void RoomNumberTest()
        {
            Check("rooms: floor 1 starts at 101, floor 2 at 201, floor 12 at 1201",
                RoomNumbers.Label(1, 1) == "101" && RoomNumbers.Label(2, 1) == "201" && RoomNumbers.Label(12, 5) == "1205");
            Check("rooms: a big floor goes round again with a letter", RoomNumbers.Label(3, 99) == "399" && RoomNumbers.Label(3, 100) == "301A" && RoomNumbers.Label(3, 199) == "301B");
            const int cols = 4, rows = 3;
            var input = new PlanInput { seed = 7, floor = 2, cols = cols, rows = rows, openE = new bool[cols, rows], openN = new bool[cols, rows], guestC = cols - 1, guestR = rows - 1, guestDirX = 1 };
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++) { input.openE[c, r] = c < cols - 1; input.openN[c, r] = r < rows - 1; }
            var layout = OfficePlanner.Build(input);
            var plates = RoomNumbers.Plates(layout, 2);
            bool allGood = plates.Count > 0;
            foreach (var p in plates) allGood &= p.label.StartsWith("2") && Mathf.Abs(p.nx) + Mathf.Abs(p.nz) == 1f;
            Check("rooms: every plate of floor 2 says 2xx and faces along an axis", allGood);
            bool onBeams = plates.Count > 0;
            foreach (var p in plates)
            {
                bool found = false;
                foreach (var w in layout.walls)
                {
                    if (w.kind != "header") continue;
                    float face = p.nx != 0f ? (p.nx > 0f ? w.rc.x1 : w.rc.x0) : (p.nz > 0f ? w.rc.z1 : w.rc.z0);
                    float at = p.nx != 0f ? p.x : p.z, along = p.nx != 0f ? p.z : p.x;
                    float lo = p.nx != 0f ? w.rc.z0 : w.rc.x0, hi = p.nx != 0f ? w.rc.z1 : w.rc.x1;
                    if (Mathf.Abs(face - at) < 0.001f && along > lo && along < hi) { found = true; break; }
                }
                onBeams &= found;
            }
            Check("rooms: every plate hangs on the outer face of a doorway beam (not buried in it)", onBeams);
            Check("rooms: the docks and the boss floor have no plates", RoomNumbers.Plates(layout, 0).Count == 0 && RoomNumbers.Plates(null, 2).Count == 0);
        }
        /// <summary>
        /// The after-hours planner (ceiling light panels, which are lit, where the real lights stand) on realistic floors of several sizes:
        /// deterministic, panels inside their room and clear of the elevator shafts, the lobby lit, light in every 18 m cell, a sensible
        /// share on, and a panel mesh whose faces point the right way.
        /// </summary>
        public static void LightingTest()
        {
            bool all = true, deterministic = true, inside = true, clearOfShafts = true, noOverlap = true, carsLit = true, lobbyLit = true,
                 everyCell = true, shareOk = true, sourcesOk = true;
            int panelsTotal = 0;
            foreach (var (cols, rows, floor) in new[] { (3, 2, 1), (5, 4, 2), (8, 5, 9), (8, 5, 12), (9, 6, 4) })
            {
                for (int s = 0; s < 3; s++)
                {
                    int seed = 1000 + floor * 7919 + s * 131;
                    var pin = new PlanInput { seed = seed, floor = floor, cols = cols, rows = rows, openE = new bool[cols, rows], openN = new bool[cols, rows],
                        elevC = 0, elevR = 0, elevDoorEast = true, hiddenHall = floor == 12, guestC = cols - 1, guestR = rows - 1, guestDirX = 1, cafe = floor == 1 };
                    for (int c = 0; c < cols; c++)
                        for (int r = 0; r < rows; r++) { pin.openE[c, r] = c < cols - 1; pin.openN[c, r] = r < rows - 1; }
                    var layout = OfficePlanner.Build(pin);
                    var input = new AfterHours.Input { layout = layout, floor = floor, seed = seed, footprint = new Rc(0f, 0f, cols * 18f, rows * 18f) };
                    var shaft = new Rc(4.8f, 4.8f, 13.2f, 13.2f);
                    input.solids.Add(shaft);
                    if (floor == 12) input.solids.Add(new Rc(0f, 4.8f, 5.2f, 13.2f));
                    float gx = (cols - 0.5f) * 18f, gz = (rows - 0.5f) * 18f;
                    input.solids.Add(new Rc(gx - 2.8f, gz - 2.8f, gx + 2.8f, gz + 2.8f));
                    input.cars.Add(new Rc(5.2f, 5.2f, 12.8f, 12.8f));
                    input.cars.Add(new Rc(gx - 2.4f, gz - 2.4f, gx + 2.4f, gz + 2.4f));
                    var plan = AfterHours.Build(input);
                    var again = AfterHours.Build(input);
                    panelsTotal += plan.panels.Count;

                    deterministic &= plan.panels.Count == again.panels.Count && plan.sources.Count == again.sources.Count && plan.LitPanels == again.LitPanels;
                    foreach (var p in plan.panels)
                    {
                        if (p.room < 0) continue;
                        var b = layout.rooms[p.room].bounds;
                        var f = AfterHours.FrameRect(p);
                        inside &= f.x0 >= b.x0 + AfterHours.WallMargin - 1e-4f && f.x1 <= b.x1 - AfterHours.WallMargin + 1e-4f
                               && f.z0 >= b.z0 + AfterHours.WallMargin - 1e-4f && f.z1 <= b.z1 - AfterHours.WallMargin + 1e-4f;
                        foreach (var so in input.solids) clearOfShafts &= !f.Overlaps(so);
                    }
                    for (int i = 0; i < plan.panels.Count && noOverlap; i++)
                        for (int j = i + 1; j < plan.panels.Count; j++)
                            if (plan.panels[i].room != -2 && plan.panels[j].room != -2
                                && AfterHours.FrameRect(plan.panels[i]).Overlaps(AfterHours.FrameRect(plan.panels[j]))) { noOverlap = false; break; }
                    int litCars = 0, lobby = 0, lobbyOn = 0;
                    var litCells = new System.Collections.Generic.HashSet<long>();
                    foreach (var p in plan.panels)
                    {
                        if (p.room == -2 && p.lit) litCars++;
                        if (p.room >= 0 && layout.rooms[p.room].theme == "lobby") { lobby++; if (p.lit) lobbyOn++; }
                        if (p.lit && p.room >= 0) litCells.Add((long)(p.x / 18f) * 1000 + (long)(p.z / 18f));
                    }
                    carsLit &= litCars == input.cars.Count;
                    lobbyLit &= lobby >= 8 && lobbyOn == lobby;
                    for (int c = 0; c < cols; c++)
                        for (int r = 0; r < rows; r++) everyCell &= litCells.Contains(c * 1000L + r);
                    shareOk &= plan.LitShare >= 0.30f && plan.LitShare <= 0.80f;
                    sourcesOk &= plan.sources.Count >= 6 && plan.sources.Count <= plan.LitPanels;
                    foreach (var so in plan.sources)
                    {
                        bool onLit = false;
                        foreach (var p in plan.panels) if (p.lit && p.x == so.x && p.z == so.z) { onLit = true; break; }
                        sourcesOk &= onLit;
                    }
                }
            }
            all = deterministic && inside && clearOfShafts && noOverlap && carsLit && lobbyLit && everyCell && shareOk && sourcesOk;
            Check($"lighting: planned {panelsTotal} panels over 15 floors", panelsTotal > 500);
            Check("lighting: the same seed gives the same lights", deterministic);
            Check("lighting: every panel lies inside its room, clear of the walls", inside);
            Check("lighting: no panel is over an elevator shaft or the hidden hallway walls", clearOfShafts);
            Check("lighting: no two panels overlap", noOverlap);
            Check("lighting: every elevator car has a lit panel", carsLit);
            Check("lighting: the lobby is lit all round", lobbyLit);
            Check("lighting: every 18 m cell has a lit panel", everyCell);
            Check("lighting: 30-80 % of the panels are on", shareOk);
            Check("lighting: the real lights stand on lit panels, and are not more than the lit panels", sourcesOk);

            // an open floor (the docks, the boss floor): everything lit
            var open = new AfterHours.Input { layout = null, floor = 0, seed = 5, footprint = new Rc(0f, 0f, 36f, 18f), allLit = true };
            open.solids.Add(new Rc(4.8f, 4.8f, 13.2f, 13.2f)); open.cars.Add(new Rc(5.2f, 5.2f, 12.8f, 12.8f));
            var op = AfterHours.Build(open);
            bool allOn = op.panels.Count >= 6;
            foreach (var p in op.panels) allOn &= p.lit;
            Check("lighting: an open floor is lit everywhere", allOn && op.sources.Count >= 4);

            // litScale darkens and brightens
            var lay = OfficePlanner.Build(new PlanInput { seed = 4242, floor = 5, cols = 8, rows = 5, openE = Open(8, 5, true), openN = Open(8, 5, false), guestC = 7, guestR = 4, guestDirX = 1 });
            float Share(float k) { var i = new AfterHours.Input { layout = lay, floor = 5, seed = 4242, footprint = new Rc(0f, 0f, 144f, 90f), litScale = k }; return AfterHours.Build(i).LitShare; }
            Check("lighting: litScale orders the lit share", Share(0.4f) <= Share(1f) && Share(1f) <= Share(1.8f));

            // the panel mesh: right counts and every triangle faces along its normal
            var g = new PanelGeometry();
            foreach (var (alongX, lit, cool) in new[] { (true, false, false), (false, true, false), (true, true, true), (false, false, true) })
                g.Add(new AfterHours.Panel { x = 3, z = -2, alongX = alongX, lit = lit, cool = cool }, 3.5f);
            bool winding = g.VertexCount == 4 * 6 * 4;
            int tris = 0;
            for (int m = 0; m < 4; m++)
            {
                var t = g.triangles[m];
                winding &= t.Count % 3 == 0;
                for (int k = 0; k + 2 < t.Count; k += 3)
                {
                    tris++;
                    int a = t[k], b = t[k + 1], c = t[k + 2];
                    var A = new Vector3(g.vertices[a * 3], g.vertices[a * 3 + 1], g.vertices[a * 3 + 2]);
                    var B = new Vector3(g.vertices[b * 3], g.vertices[b * 3 + 1], g.vertices[b * 3 + 2]);
                    var C = new Vector3(g.vertices[c * 3], g.vertices[c * 3 + 1], g.vertices[c * 3 + 2]);
                    var n = new Vector3(g.normals[a * 3], g.normals[a * 3 + 1], g.normals[a * 3 + 2]);
                    var cross = Vector3.Cross(B - A, C - A);
                    winding &= cross.sqrMagnitude > 1e-10f && Vector3.Dot(cross.normalized, n) > 0.999f;
                }
            }
            winding &= tris == 4 * 6 * 2;
            winding &= g.triangles[PanelGeometry.Off].Count == 12 && g.triangles[PanelGeometry.OnWarm].Count == 6 && g.triangles[PanelGeometry.OnCool].Count == 6;
            Check("lighting: the panel mesh has whole, outward-facing triangles in the right materials", winding);
        }

        /// <summary>
        /// The elevator's interior plan (wood panels, steel trim, mirror), its mesh, the wood texture and the mirror arithmetic, for all four
        /// door layouts of the service elevator (front door east or north, with and without the floor-12 back door).
        /// </summary>
        public static void ElevatorLookTest()
        {
            const float H = ElevatorLook.Half, dh = ElevatorLook.DoorHalf, ceilB = ElevatorLook.Height - ElevatorLook.CeilingThickness;
            var layouts = new[]
            {
                (ElevatorLook.Side.East, -1, ElevatorLook.Side.North), (ElevatorLook.Side.East, (int)ElevatorLook.Side.West, ElevatorLook.Side.North),
                (ElevatorLook.Side.North, -1, ElevatorLook.Side.East), (ElevatorLook.Side.North, (int)ElevatorLook.Side.South, ElevatorLook.Side.East)
            };
            bool mirrorOk = true, doorwaysOk = true, railOk = true, shallowOk = true, meshOk = true, lampsOk = true, deterministic = true;
            foreach (var (front, back, panel) in layouts)
            {
                var plan = ElevatorLook.Build(new ElevatorLook.Input { front = front, back = back, panelWall = panel, seed = 5 });
                var again = ElevatorLook.Build(new ElevatorLook.Input { front = front, back = back, panelWall = panel, seed = 5 });
                deterministic &= plan.boxes.Count == again.boxes.Count && plan.boxes.Count > 40;

                var m = plan.mirror;
                ElevatorLook.Inward(m != null ? m.wall : front, out float nx, out float nz);
                mirrorOk &= m != null && m.wall == ElevatorLook.Opposite(panel) && m.wall != front && (int)m.wall != back && m.nx == nx && m.nz == nz && m.width > 6.5f;

                // the doorways are open (only the sill is in them), and nothing but the rail stands more than 11 cm out of a wall
                var doors = new System.Collections.Generic.List<ElevatorLook.Side> { front };
                if (back >= 0) doors.Add((ElevatorLook.Side)back);
                foreach (var b in plan.boxes)
                {
                    if (b.name == "Sill" || b.name == "Carpet" || b.name.StartsWith("Ceiling") || b.name == "DoorFrame") continue;
                    foreach (var s in doors)
                    {
                        float x0 = s == ElevatorLook.Side.East ? H - 0.1f : s == ElevatorLook.Side.West ? -H - 0.4f : -dh;
                        float x1 = s == ElevatorLook.Side.East ? H + 0.4f : s == ElevatorLook.Side.West ? -H + 0.1f : dh;
                        float z0 = s == ElevatorLook.Side.North ? H - 0.1f : s == ElevatorLook.Side.South ? -H - 0.4f : -dh;
                        float z1 = s == ElevatorLook.Side.North ? H + 0.4f : s == ElevatorLook.Side.South ? -H + 0.1f : dh;
                        bool hit = b.x0 < x1 - 1e-4f && b.x1 > x0 + 1e-4f && b.z0 < z1 - 1e-4f && b.z1 > z0 + 1e-4f && b.y0 < ceilB && b.y1 > 0.06f;
                        doorwaysOk &= !hit;
                    }
                    float depth = Mathf.Min(Mathf.Min(H - b.x0, b.x1 + H), Mathf.Min(H - b.z0, b.z1 + H));
                    shallowOk &= depth <= 0.1101f;
                    if (b.name == "Rail" || b.name == "Band")
                    {
                        bool onPanelWall = panel == ElevatorLook.Side.North ? b.z0 > H - 0.2f : b.x0 > H - 0.2f;
                        float end = panel == ElevatorLook.Side.North ? b.x1 : b.z1;
                        if (onPanelWall) railOk &= end <= Mathf.Min(ElevatorLook.PanelAlong - ElevatorLook.PanelHalfWidth, ElevatorLook.GoAlong - ElevatorLook.GoHalfWidth) - 0.05f;
                    }
                }

                var parts = CarMesh.From(plan).parts;
                int tris = 0;
                for (int k = 0; k < parts.Length; k++)
                {
                    var p = parts[k];
                    meshOk &= p.VertexCount > 0 && p.normals.Count == p.vertices.Count && p.uvs.Count == p.VertexCount * 2 && p.triangles.Count % 3 == 0;
                    for (int t = 0; t + 2 < p.triangles.Count; t += 3)
                    {
                        tris++;
                        int a = p.triangles[t], b2 = p.triangles[t + 1], c = p.triangles[t + 2];
                        var A = new Vector3(p.vertices[a * 3], p.vertices[a * 3 + 1], p.vertices[a * 3 + 2]);
                        var B = new Vector3(p.vertices[b2 * 3], p.vertices[b2 * 3 + 1], p.vertices[b2 * 3 + 2]);
                        var C = new Vector3(p.vertices[c * 3], p.vertices[c * 3 + 1], p.vertices[c * 3 + 2]);
                        var n = new Vector3(p.normals[a * 3], p.normals[a * 3 + 1], p.normals[a * 3 + 2]);
                        var cross = Vector3.Cross(B - A, C - A);
                        meshOk &= cross.sqrMagnitude > 1e-12f && Vector3.Dot(cross.normalized, n) > 0.999f;
                    }
                }
                meshOk &= tris == plan.boxes.Count * 12 + plan.discs.Count * CarMesh.DiscSides;
                lampsOk &= plan.lamps.Count == 4 && plan.discs.Count == 18;
            }
            Check("elevator: the plan is the same every time", deterministic);
            Check("elevator: the mirror is on the wall opposite the floor panel, facing in, never over a door", mirrorOk);
            Check("elevator: the doorways stay clear", doorwaysOk);
            Check("elevator: the rail stops before the floor panel and the GO button", railOk);
            Check("elevator: the trim stands no more than 11 cm out of the walls", shallowOk);
            Check("elevator: the mesh has whole triangles that face outward, in every material", meshOk);
            Check("elevator: nine downlights and four ceiling lamps", lampsOk);

            var px = ProceduralWood.Pixels(ProceduralWood.Width, ProceduralWood.Height, 11);
            double sum = 0;
            for (int i = 0; i < px.Length; i += 4) sum += 0.30 * px[i] + 0.59 * px[i + 1] + 0.11 * px[i + 2];
            double mean = sum / (px.Length / 4);
            Check("elevator: the wood texture is orange-brown, neither black nor white", px.Length == ProceduralWood.Width * ProceduralWood.Height * 4 && mean > 90 && mean < 190);

            var r = MirrorMath.Reflection(0f, 0f, 1f, -2f);                // the plane z = 2
            MirrorMath.Transform(r, 1f, 3f, 5f, out float ox, out float oy, out float oz);
            Check("elevator: reflecting in the mirror plane flips the distance", Mathf.Abs(ox - 1f) < 1e-4f && Mathf.Abs(oy - 3f) < 1e-4f && Mathf.Abs(oz - (-1f)) < 1e-4f);
            MirrorMath.TextureSize(1920, 1080, 0.6f, out int tw, out int th);
            Check("elevator: the mirror picture is a share of the screen", tw == 1152 && th == 648);
        }

        static bool[,] Open(int cols, int rows, bool east)
        {
            var a = new bool[cols, rows];
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++) a[c, r] = east ? c < cols - 1 : r < rows - 1;
            return a;
        }

        /// <summary>Builds the character catalog from the Tiny Toon pack and checks it (needs the pack imported).</summary>
        public static void ToonSetup()
        {
            UnityEditor.AssetDatabase.Refresh();
            CharacterCatalogBuilder.BuildNow(true);
            var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterCatalog>("Assets/Resources/CharacterCatalog.asset");
            Check("catalog asset exists", cat != null);
            if (!cat) return;
            Check("catalog has an atlas", cat.atlas != null);
            Check("catalog lists characters", cat.characters.Length >= 4);
            foreach (var c in cat.characters)
                Debug.Log($"[NetTest] catalog character id={c.id} label={c.label} role={c.role} clips={c.clips} prefab={(c.prefab ? c.prefab.name : "NONE")}");
            Check("three players to pick from", cat.Players().Count >= 3);
            Check("a guard character", cat.FirstWithRole(ToonRole.Guard) != null);
            foreach (var (n, s) in new[] { ("male", cat.male), ("female", cat.female), ("zombie", cat.zombie) })
                Debug.Log($"[NetTest] clipset {n}: idle={s.idle != null} walk={s.walk != null} run={s.run != null} death={s.death != null} attack={s.attack != null} pistolIdle={s.pistolIdle != null} pistolRun={s.pistolRun != null} pistolShoot={s.pistolShoot != null}");
            Check("male clips complete", cat.male.idle && cat.male.walk && cat.male.run && cat.male.death && cat.male.attack);
            Check("female clips complete", cat.female.idle && cat.female.walk && cat.female.run && cat.female.death && cat.female.attack);
            Check("male pistol clips (armed guards)", cat.male.pistolIdle && cat.male.pistolRun && cat.male.pistolShoot);
            foreach (var clip in new[] { cat.male.idle, cat.male.walk, cat.male.run, cat.male.death, cat.male.attack })
                if (clip) Debug.Log($"[NetTest] clip {clip.name}: length={clip.length:0.00}s humanMotion={clip.humanMotion} looping={clip.isLooping}");
        }

        /// <summary>
        /// Guards and doors: which officers walk through doors (and so open them), when a walk counts as going into a doorway, what a guard
        /// sees (the cone) and how long a chase lasts. The same checks run outside Unity (scratchpad guard_test).
        /// </summary>
        public static void DoorOpenersTest()
        {
            // who walks about by himself
            Check("doors: patrolling, investigating and chasing officers use doors", GuardAI.UsesDoors(GuardState.Patrol) && GuardAI.UsesDoors(GuardState.Investigate) && GuardAI.UsesDoors(GuardState.Chase));
            Check("doors: officers who stand (calling, pinning, stunned) do not", !GuardAI.UsesDoors(GuardState.Calling) && !GuardAI.UsesDoors(GuardState.Pinning) && !GuardAI.UsesDoors(GuardState.Stunned));

            // a doorway in a wall along x at z = 0, gap 2 m (HalfGap 1): the zone is the gap (+0.25) and 0.5 m each side of the wall's middle
            const float reach = 1.25f, zone = SlidingDoor.WalkZone;
            bool Enters(float px, float pz, float dx, float dz, float len) => DoorGeometry.WalkEnters(px, pz, dx, dz, len, 0f, 0f, true, reach, zone);
            Check("doors: a walk straight at the doorway goes in", Enters(0f, -3f, 0f, 1f, 2.6f));
            Check("doors: ...but not when the look-ahead is too short to get there", !Enters(0f, -3f, 0f, 1f, 2.0f));
            Check("doors: walking away from the doorway does not", !Enters(0f, -1f, 0f, -1f, 6f));
            Check("doors: walking along the wall, 2 m away, does not", !Enters(-4f, -2f, 1f, 0f, 8f));
            Check("doors: walking along the wall as close as a guard can get (0.6 m) does not", !Enters(-4f, -0.6f, 1f, 0f, 8f));
            Check("doors: an oblique walk through the gap goes in", Enters(-2f, -2f, 0.7071f, 0.7071f, 3.5f));
            Check("doors: walking at the wall beside the doorway does not", !Enters(3f, -2f, 0f, 1f, 5f));
            Check("doors: standing in the doorway and walking on counts", Enters(0.2f, 0.1f, 0f, 1f, 1f));

            // a route that bends into the doorway after 4 m
            var xs = new float[8]; var zs = new float[8];
            xs[0] = -4f; zs[0] = -2f; xs[1] = 0.5f; zs[1] = -0.6f; xs[2] = 0.5f; zs[2] = 0.6f; xs[3] = 0.5f; zs[3] = 3f;
            Check("doors: a route that turns into the doorway is seen in time", DoorGeometry.RouteEnters(xs, zs, 4, 5f, 0f, 0f, true, reach, zone));
            Check("doors: ...and not before it is within the look-ahead", !DoorGeometry.RouteEnters(xs, zs, 4, 3f, 0f, 0f, true, reach, zone));
            xs[0] = -4f; zs[0] = -2f; xs[1] = 4f; zs[1] = -2f;
            Check("doors: a route straight past the doorway does not go in", !DoorGeometry.RouteEnters(xs, zs, 2, 20f, 0f, 0f, true, reach, zone));

            // what a guard sees: the fan he shows on the floor (65 degrees each side, 14 m)
            Check("guards: a player straight ahead is in the cone", GuardRules.InCone(0f, 1f, 0f, 5f, 65f, 14f));
            Check("guards: a player 70 degrees off is not", !GuardRules.InCone(0f, 1f, Mathf.Sin(70f * Mathf.Deg2Rad) * 5f, Mathf.Cos(70f * Mathf.Deg2Rad) * 5f, 65f, 14f));
            Check("guards: a player behind him is not", !GuardRules.InCone(0f, 1f, 0f, -5f, 65f, 14f));
            Check("guards: a player past his range is not", !GuardRules.InCone(0f, 1f, 0f, 15f, 65f, 14f));
            Check("guards: a player right on top of him is, whichever way he faces", GuardRules.InCone(0f, 1f, 0f, -0.3f, 65f, 14f));

            // the chase: as long as somebody is in view he keeps after him; 5 s out of view in a row and he gives up
            float unseen = 0f; bool over = false;
            for (int i = 0; i < 3600 && !over; i++) over = GuardRules.PursuitOver(ref unseen, true, 1f / 60f, GuardRules.LoseSightSeconds);
            Check("guards: a minute in view and the chase is still on", !over);
            unseen = 0f; over = false;
            for (int i = 0; i < 290; i++) over |= GuardRules.PursuitOver(ref unseen, false, 1f / 60f, GuardRules.LoseSightSeconds);
            Check("guards: 4.8 s out of view is not enough to give up", !over);
            over |= GuardRules.PursuitOver(ref unseen, true, 1f / 60f, GuardRules.LoseSightSeconds);
            for (int i = 0; i < 290; i++) over |= GuardRules.PursuitOver(ref unseen, false, 1f / 60f, GuardRules.LoseSightSeconds);
            Check("guards: a glimpse of the player starts the 5 s again", !over);
            for (int i = 0; i < 20; i++) over |= GuardRules.PursuitOver(ref unseen, false, 1f / 60f, GuardRules.LoseSightSeconds);
            Check("guards: 5 s out of view in a row and he gives up", over);
            Check("guards: the limit is 5 s", Mathf.Approximately(GuardRules.LoseSightSeconds, 5f));
        }

        /// <summary>The crosshair's arithmetic: rays against doorways, items and the dealer, which one E means, and the swing direction.</summary>
        public static void AimMathTest()
        {
            const float inf = float.PositiveInfinity;
            bool Near(float a, float b) => Mathf.Abs(a - b) < 1e-3f;

            // a door in a wall running along x at z = 10 (centre x = 4, gap 1.8, height 2.6, wall 0.3 thick)
            bool front = AimMath.RayDoorway(4f, 1.5f, 6f, 0f, 0f, 1f, 4f, 10f, true, 0.9f, 2.6f, 0.3f, 20f, out float en, out float ex);
            Check("aim: a ray through the middle of a doorway hits it", front && Near(en, 4f - 0.15f - AimMath.DoorPad) && ex > en);
            Check("aim: the wall beside the doorway is not the door", !AimMath.RayDoorway(6f, 1.5f, 6f, 0f, 0f, 1f, 4f, 10f, true, 0.9f, 2.6f, 0.3f, 20f, out _, out _));
            Check("aim: far over the door is not the door", !AimMath.RayDoorway(4f, 3.5f, 6f, 0f, 0f, 1f, 4f, 10f, true, 0.9f, 2.6f, 0.3f, 20f, out _, out _));
            Check("aim: a doorway in a wall along z is hit from the side", AimMath.RayDoorway(0f, 1.5f, 10f, 1f, 0f, 0f, 4f, 10f, false, 0.9f, 2.6f, 0.3f, 20f, out float e2, out _) && Near(e2, 4f - 0.15f - AimMath.DoorPad));
            Check("aim: a doorway farther than the ray is long is missed", !AimMath.RayDoorway(4f, 1.5f, -30f, 0f, 0f, 1f, 4f, 10f, true, 0.9f, 2.6f, 0.3f, 20f, out _, out _));

            Check("aim: a small item is hit", AimMath.RayItem(0f, 1f, -5f, 0f, 0f, 1f, -0.1f, 0.9f, -0.1f, 0.1f, 1.1f, 0.1f, 20f, out float ti) && Near(ti, 5f - 0.1f - AimMath.ItemPad));
            Check("aim: just beside a small item still hits it (padding)", AimMath.RayItem(0.15f, 1f, -5f, 0f, 0f, 1f, -0.1f, 0.9f, -0.1f, 0.1f, 1.1f, 0.1f, 20f, out _));
            Check("aim: well beside it misses", !AimMath.RayItem(0.3f, 1f, -5f, 0f, 0f, 1f, -0.1f, 0.9f, -0.1f, 0.1f, 1.1f, 0.1f, 20f, out _));

            Check("aim: the dealer (an upright cylinder) is hit head on", AimMath.RayCylinderY(0f, 1f, -5f, 0f, 0f, 1f, 0f, 0f, 0.5f, 0f, 2f, 20f, out float td) && Near(td, 4.5f));
            Check("aim: beside or over the dealer misses",
                !AimMath.RayCylinderY(0.6f, 1f, -5f, 0f, 0f, 1f, 0f, 0f, 0.5f, 0f, 2f, 20f, out _) && !AimMath.RayCylinderY(0f, 2.5f, -5f, 0f, 0f, 1f, 0f, 0f, 0.5f, 0f, 2f, 20f, out _));

            Check("aim: nothing under the crosshair is nothing", AimMath.Choose(inf, inf, inf, inf) == AimMath.Pick.None);
            Check("aim: an item in front of a door beats it", AimMath.Choose(2f, 3f, 3.3f, inf) == AimMath.Pick.Pickup);
            Check("aim: an item inside the doorway beats the door (a crate that keeps it open can be picked up)", AimMath.Choose(3.1f, 3f, 3.3f, inf) == AimMath.Pick.Pickup);
            Check("aim: an item far behind the door loses to it", AimMath.Choose(5f, 3f, 3.3f, inf) == AimMath.Pick.Door);
            Check("aim: the nearer of the dealer and a door wins", AimMath.Choose(inf, 3f, 3.3f, 2f) == AimMath.Pick.Dealer && AimMath.Choose(inf, 3f, 3.3f, 4f) == AimMath.Pick.Door);

            // the camera sits 2.6 behind and 0.45 to the right of the player, looking straight ahead: the swing leans a little right
            bool ok = AimMath.SwingDirection(0.45f, 1.35f, -2.6f, 0f, 0f, 1f, 0f, 1f, 0f, 2.4f, out float sx, out float sz);
            Check("aim: a swing leans towards the crosshair", ok && sx > 0.05f && sx < 0.4f && sz > 0.9f && Near(sx * sx + sz * sz, 1f));
            ok = AimMath.SwingDirection(0f, 1.35f, -2.6f, 0f, 0f, 1f, 0f, 1f, 0f, 2.4f, out sx, out sz);
            Check("aim: a swing along the player's own line goes straight ahead", ok && Near(sx, 0f) && Near(sz, 1f));
            Check("aim: the aim ray starts at the player's depth and ends past his reach", Near(AimMath.StartT(2.6f), 2.35f) && AimMath.StartT(0.1f) == 0f && AimMath.EndT(2.6f, 2.2f) > 2.6f + 2.2f);
        }

        static void Check(string name, bool ok) => Debug.Log($"[NetTest] policy {name}: {(ok ? "PASS" : "FAIL")}");
    }
}
