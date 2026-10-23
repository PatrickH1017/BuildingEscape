using BuildingEscape.EditorTools;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the 2026-10-17 changes of the Employees Take Revenge rename session: the basement gate, the hallway doors' glass, the new
    /// loot, carrying, the loot scanner, the security-office signs and the TV cutscene. Called from <c>NetDevTest.PolicyTest</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        /// <summary>Like the plain check, with a few numbers after the verdict (the log line still says PASS or FAIL, and the numbers are for the person reading it).</summary>
        static void Check(string name, bool ok, string detail) => Debug.Log($"[NetTest] policy {name}: {(ok ? "PASS" : "FAIL")} {detail}");

        public static void EtrRulesTest()
        {
            GateTest();
            SecuritySignTest();
            ValuableCatalogTest();
            ValuablePlanTest();
            CarryRoomTest();
            ScanRulesTest();
            CarLootRulesTest();
        }

        // ------------------------------------------------------------------ the valuables

        static void ValuableCatalogTest()
        {
            bool order = Valuables.All.Length == 13;
            for (int i = 0; i < Valuables.All.Length; i++)
            {
                var d = Valuables.All[i];
                order &= (int)d.kind == i && d.value >= 100 && d.share > 0 && d.weight > 0f && d.weight < 3f && !string.IsNullOrEmpty(d.label) && d.width > 0f && d.depth > 0f;
            }
            Check("loot: the catalog lists 13 kinds in the order of the enum, each with a name, a value, a weight and a share", order);
            Check("loot: the guard outposts' three valuables are the first three kinds", (int)OutpostValuable.Envelope == (int)ValuableKind.Envelope && (int)OutpostValuable.Watch == (int)ValuableKind.Watch
                && (int)OutpostValuable.Chips == (int)ValuableKind.Chips && Valuables.Of(1).label == OutpostLoot.ValuableName(1) && Valuables.Of(2).label == OutpostLoot.ValuableName(2) && Valuables.Of(0).label == OutpostLoot.ValuableName(0));
            int roomy = 0; foreach (var d in Valuables.All) if (d.Roomy) roomy++;
            Check("loot: only the laptop needs a roomy spot, and everything else fits a small one", roomy == 1 && Valuables.Of(ValuableKind.Laptop).Roomy);
            Check("loot: the watches and phones are there (a watch, a phone, a wallet, a laptop, jewellery, cash)", Valuables.Of(ValuableKind.Watch).label == "Gold Watch" && Valuables.Of(ValuableKind.Phone).label == "Smartphone"
                && Valuables.Of(ValuableKind.Wallet).label == "Wallet" && Valuables.Of(ValuableKind.Laptop).label == "Laptop" && Valuables.Of(ValuableKind.Jewelry).label == "Jewelry Box" && Valuables.Of(ValuableKind.Cash).label == "Cash Stack");

            bool steps = true, rises = true; int last = 0;
            foreach (var d in Valuables.All)
                for (int floor = 1; floor <= 12; floor++)
                    for (double u = 0.0; u <= 1.0; u += 0.25)
                    {
                        int v = Valuables.ValueOf(d, floor, u);
                        steps &= v % 5 == 0 && v >= 5 && v >= d.value * Valuables.Scale(floor) * 0.79 && v <= d.value * Valuables.Scale(floor) * 1.31;
                    }
            for (int floor = 1; floor <= 12; floor++) { int v = Valuables.ValueOf(Valuables.Of(ValuableKind.Phone), floor, 0.5); rises &= v >= last; last = v; }
            Check("loot: a value is in $5 steps, within 0.8 to 1.3 of the catalog's times the floor's scale, and a phone is worth more on every higher floor", steps && rises);
            Check("loot: the floors' scale is the quota's (1 on floor 1, more every floor)", System.Math.Abs(Valuables.Scale(1) - 1.0) < 1e-9 && Valuables.Scale(2) > 1.2 && Valuables.Scale(4) > 2.0 && Valuables.Scale(12) > Valuables.Scale(4) && Valuables.Scale(0) == 1.0);
            Check("loot: a floor holds 67 valuables on floor 1, 76 on floor 4, 100 on floor 12", Valuables.Count(1) == 67 && Valuables.Count(4) == 76 && Valuables.Count(12) == 100);

            // picking a kind
            bool noLaptopOnSmall = true; int laptops = 0, conferenceLaptops = 0, officeLaptops = 0; var seen = new System.Collections.Generic.HashSet<ValuableKind>();
            for (int i = 0; i < 2000; i++)
            {
                double u = (i + 0.5) / 2000.0;
                noLaptopOnSmall &= Valuables.Pick("office", false, u).kind != ValuableKind.Laptop;
                if (Valuables.Pick("office", true, u).kind == ValuableKind.Laptop) { laptops++; officeLaptops++; }
                if (Valuables.Pick("conference", true, u).kind == ValuableKind.Laptop) conferenceLaptops++;
                seen.Add(Valuables.Pick(null, true, u).kind);
            }
            Check("loot: a small spot never gets a laptop; a roomy one can", noLaptopOnSmall && laptops > 0);
            Check("loot: a conference table has more laptops than an office desk", conferenceLaptops > officeLaptops);
            Check("loot: every kind can come up", seen.Count == 13);
            double a = Valuables.Unit(5, 1, 2, 3), b = Valuables.Unit(5, 1, 2, 3), c = Valuables.Unit(6, 1, 2, 3);
            Check("loot: the numbers that decide kinds and values depend on the seed only (the same on every machine), and spread over 0 to 1", a == b && a != c && a >= 0.0 && a < 1.0);
        }

        /// <summary>The floors as the planner makes them (8 x 5 cells): what the plan puts on the desks.</summary>
        static void ValuablePlanTest()
        {
            const int cols = 8, rows = 5;
            int floors = 0, tooFew = 0, outside = 0, onItems = 0, crowded = 0, outpostHost = 0, differ = 0, ratioLow = 0, ratioHigh = 0, fewKinds = 0, cellOver = 0, wrongHeight = 0;
            double coverage = 0, minCoverage = 1.0; int totalSpots = 0, totalPlanned = 0;
            foreach (var (floor, seed) in new[] { (1, 4101), (2, 777), (3, 1234), (4, 9172), (5, 55), (6, 31337), (8, 999), (9, 2288), (12, 515), (1, 7), (2, 8675309), (4, 424242) })
            {
                var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
                var layout = OfficePlanner.Build(shape.PlanFor(floor, seed, 18f, 3.5f, 777));
                var plan = ValuablePlan.Plan(layout, seed, floor, 18f, cols, rows);
                var again = ValuablePlan.Plan(layout, seed, floor, 18f, cols, rows);
                floors++;
                totalSpots += ValuablePlan.FindSpots(layout, seed).Count; totalPlanned += plan.Count;
                if (plan.Count != again.Count) differ++;
                else for (int i = 0; i < plan.Count; i++) if (plan[i].spot.x != again[i].spot.x || plan[i].def.kind != again[i].def.kind || plan[i].value != again[i].value) { differ++; break; }
                if (plan.Count < 45) tooFew++;
                var kinds = new System.Collections.Generic.HashSet<ValuableKind>();
                long worth = 0;
                var perCell = new System.Collections.Generic.Dictionary<int, int>();
                var roomsWith = new System.Collections.Generic.HashSet<int>();
                foreach (var pv in plan)
                {
                    var s = pv.spot; kinds.Add(pv.def.kind); worth += pv.value;
                    if (!s.host.rc.Contains(s.x, s.z)) outside++;
                    if (System.Math.Abs(s.y - (s.host.y + s.host.h)) > 1e-4f) wrongHeight++;
                    if (s.host.room != null && s.host.room.StartsWith("outpost")) outpostHost++;
                    var mine = Rc.Centered(s.x, s.z, 0.25f, 0.25f);
                    foreach (var o in layout.pieces)
                        if (o.onTop && !o.mount && o.host == s.host && System.Math.Abs(o.y - s.y) < 0.08f && o.rc.Overlaps(mine, 0.002f)) { onItems++; break; }
                    foreach (var o in plan)
                        if (!ReferenceEquals(o, pv) && o.spot.host == s.host && System.Math.Abs(o.spot.x - s.x) < 0.5f && System.Math.Abs(o.spot.z - s.z) < 0.5f) { crowded++; break; }
                    int cell = System.Math.Max(0, System.Math.Min(cols - 1, (int)(s.x / 18f))) + cols * System.Math.Max(0, System.Math.Min(rows - 1, (int)(s.z / 18f)));
                    perCell.TryGetValue(cell, out int n); perCell[cell] = n + 1;
                    if (s.roomIndex >= 0) roomsWith.Add(s.roomIndex);
                    if (pv.def.Roomy && !s.roomy) wrongHeight++;
                }
                foreach (var kv in perCell) if (kv.Value > LootPlan.CellCap) cellOver++;
                if (kinds.Count < 8) fewKinds++;
                double ratio = worth / (TowerRules.QuotaFor(floor) / TowerRules.QuotaMultiplier);          // (against the quota the loot is calibrated on: the real quota is QuotaMultiplier times higher)
                if (ratio < 5.0 || worth / (double)TowerRules.QuotaFor(floor) < 2.0) ratioLow++;
                if (ratio > 12.0) ratioHigh++;
                int numbered = 0;
                for (int i = 0; i < layout.rooms.Count; i++) if (layout.rooms[i].kind == 0 && layout.rooms[i].doors.Count > 0) numbered++;
                double cov = numbered > 0 ? roomsWith.Count / (double)numbered : 1.0;
                coverage += cov; minCoverage = System.Math.Min(minCoverage, cov);
                UnityEngine.Debug.Log($"[NetTest] loot plan floor {floor} seed {seed}: {plan.Count} valuables of {ValuablePlan.FindSpots(layout, seed).Count} spots, {kinds.Count} kinds, worth {worth} (x{ratio:0.0} of the quota), {roomsWith.Count} of {numbered} numbered rooms have one");
            }
            Check($"loot plan: every floor has at least 45 valuables on its desks ({totalPlanned} in {floors} floors, {totalSpots} spots)", tooFew == 0);
            Check("loot plan: each lies inside the desk or table it is on, at the height of its top", outside == 0 && wrongHeight == 0);
            Check("loot plan: none lies on top of something already there, none within half a metre of another on the same surface", onItems == 0 && crowded == 0);
            Check("loot plan: none in the guard outpost (it has its own things), at most four to a cell", outpostHost == 0 && cellOver == 0);
            Check("loot plan: the same layout and seed give the same valuables", differ == 0);
            Check("loot plan: at least 8 kinds on every floor", fewKinds == 0);
            Check("loot plan: the desks alone hold 5 to 12 times the quota (the treasures come on top)", ratioLow == 0 && ratioHigh == 0, $"({ratioLow} floors too poor, {ratioHigh} too rich)");
            Check($"loot plan: most numbered rooms hold one (on average {coverage / floors * 100.0:0}% of them, at least {minCoverage * 100.0:0}%)", coverage / floors >= 0.45 && minCoverage >= 0.30);
        }

        // ------------------------------------------------------------------ room to carry

        static void CarryRoomTest()
        {
            // a made-up floor: an empty one, then one with the thing walled in
            var empty = new Layout();
            var slot = new Slot { x = 20f, z = 20f, y = 0f, yaw = 0f, kind = "server" };
            Check("carry room: on an empty floor there is room for two or three people to carry anything", LootPlan.RoomForCarry(empty, slot, 2) && LootPlan.RoomForCarry(empty, slot, 3) && LootPlan.RoomForCarry(empty, slot, 1));
            var tight = new Layout();
            // four walls 0.6 m from the thing's middle
            tight.walls.Add(new WallBox { rc = new Rc(19.1f, 18.5f, 19.4f, 21.5f), y0 = 0f, y1 = 3.5f });
            tight.walls.Add(new WallBox { rc = new Rc(20.6f, 18.5f, 20.9f, 21.5f), y0 = 0f, y1 = 3.5f });
            tight.walls.Add(new WallBox { rc = new Rc(18.5f, 19.1f, 21.5f, 19.4f), y0 = 0f, y1 = 3.5f });
            tight.walls.Add(new WallBox { rc = new Rc(18.5f, 20.6f, 21.5f, 20.9f), y0 = 0f, y1 = 3.5f });
            Check("carry room: a thing walled in so that nobody can stand beside it is refused for two or three, never for one", !LootPlan.RoomForCarry(tight, slot, 2) && !LootPlan.RoomForCarry(tight, slot, 3) && LootPlan.RoomForCarry(tight, slot, 1));
            // a counter against a wall: free in front of it, or a desk blocking the front
            var counter = new Piece { x = 20f, z = 20f, rc = new Rc(19.5f, 19.7f, 20.5f, 20.3f), h = 0.95f, solid = true, yaw = 180f };
            var open = new Layout(); open.pieces.Add(counter);
            open.walls.Add(new WallBox { rc = new Rc(18f, 20.3f, 22f, 20.5f), y0 = 0f, y1 = 3.5f });          // the wall behind it
            var onCounter = new Slot { x = 20f, y = 0.95f, z = 20f, yaw = 0f, kind = "espresso", host = counter };
            Check("carry room: a machine on a counter with free floor in front of it can be carried by two", LootPlan.RoomForCarry(open, onCounter, 2));
            var blockedFront = new Layout(); blockedFront.pieces.Add(counter);
            blockedFront.walls.Add(new WallBox { rc = new Rc(18f, 20.3f, 22f, 20.5f), y0 = 0f, y1 = 3.5f });
            blockedFront.walls.Add(new WallBox { rc = new Rc(18f, 18.2f, 22f, 18.4f), y0 = 0f, y1 = 3.5f });
            blockedFront.walls.Add(new WallBox { rc = new Rc(17.8f, 18.2f, 18.0f, 20.5f), y0 = 0f, y1 = 3.5f });
            blockedFront.walls.Add(new WallBox { rc = new Rc(22.0f, 18.2f, 22.2f, 20.5f), y0 = 0f, y1 = 3.5f });
            Check("carry room: ... but not when it is shut in a narrow room with the counter against its back wall", !LootPlan.RoomForCarry(blockedFront, onCounter, 2));
            // the free radius itself
            var oneBox = new Layout(); oneBox.pieces.Add(new Piece { rc = new Rc(0f, 0f, 1f, 1f), h = 0.9f, solid = true }); oneBox.pieces.Add(new Piece { rc = new Rc(3f, 0f, 3.4f, 1f), h = 0.3f, solid = true });
            oneBox.walls.Add(new WallBox { rc = new Rc(5f, -3f, 5.2f, 3f), y0 = 0f, y1 = 3.5f });
            oneBox.walls.Add(new WallBox { rc = new Rc(-3f, 2.45f, 3f, 2.65f), y0 = 2.45f, y1 = 3.5f });         // (a beam over a doorway: nobody has to walk round it)
            Check("carry room: the free radius is the distance to the nearest solid thing or wall, a beam over a doorway is not one, a knee-high thing is not either",
                System.Math.Abs(LootPlan.FreeRadius(oneBox, 3f, 0.5f, 9f) - 2f) < 1e-4 && System.Math.Abs(LootPlan.FreeRadius(oneBox, 3.5f, 0.5f, 1f) - 1f) < 1e-4 && System.Math.Abs(LootPlan.FreeRadius(oneBox, 0.5f, 0.5f, 9f)) < 1e-4);

            // on the real floors: the one-person treasures are never refused, and some heavy ones are
            const int cols = 8, rows = 5;
            int floors = 0, heavy = 0, roomy = 0, light = 0, lightRefused = 0;
            foreach (var (floor, seed) in new[] { (1, 4101), (2, 777), (3, 1234), (4, 9172), (6, 31337), (9, 2288), (12, 515), (1, 7), (2, 8675309), (4, 424242) })
            {
                var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
                var layout = OfficePlanner.Build(shape.PlanFor(floor, seed, 18f, 3.5f, 777));
                floors++;
                foreach (var s in layout.bigSlots)
                {
                    int lifters = s.kind == "safe" ? 3 : s.kind == "espresso" || s.kind == "server" ? 2 : 1;
                    bool ok = LootPlan.RoomForCarry(layout, s, lifters);
                    if (lifters == 1) { light++; if (!ok) lightRefused++; }
                    else { heavy++; if (ok) roomy++; }
                }
            }
            UnityEngine.Debug.Log($"[NetTest] carry room: {floors} floors, {heavy} heavy treasure spots of which {roomy} have room, {light} one-person spots");
            Check("carry room: a one-person treasure is never refused", lightRefused == 0);
            Check("carry room: on the real floors some heavy treasure spots have room and some do not (the rule is neither empty nor blind)", heavy > 0 && roomy > 0 && roomy < heavy, $"({roomy} of {heavy})");
        }

        // ------------------------------------------------------------------ loot stays in the elevator car (the owner, 2026-10-22)

        static void CarLootRulesTest()
        {
            Check("car: a piece's middle is kept inside the wall by its own half width and a margin (never closer than a metre to the middle)", System.Math.Abs(CarLootRules.Limit(0.7f) - 3.0f) < 1e-4f && CarLootRules.Limit(10f) == 1f && CarLootRules.Limit(0.5f, 3.6f) > 2.9f - 1e-4f);
            Check("car: what would be past the limit is brought back to it, on either side and round any middle; what is inside is left alone", CarLootRules.Keep(4.6f, 0f, 3f) == 3f && CarLootRules.Keep(-4f, 0f, 3f) == -3f && CarLootRules.Keep(1f, 0f, 3f) == 1f && CarLootRules.Keep(14f, 10f, 3f) == 13f && CarLootRules.Keep(6f, 10f, 3f) == 7f);
            Check("car: a piece going out past the limit loses that speed, one coming back in or lying inside keeps it",
                CarLootRules.AfterWall(9f, 4.6f, 0f, 3f) == 0f && CarLootRules.AfterWall(-9f, -4f, 0f, 3f) == 0f && CarLootRules.AfterWall(-2f, 4.6f, 0f, 3f) == -2f && CarLootRules.AfterWall(5f, 1f, 0f, 3f) == 5f && CarLootRules.AfterWall(9f, 14f, 10f, 3f) == 0f);
            Check("car: a piece let go of in the doorway a moment ago is caught, one set down long ago or never is not",
                CarLootRules.CatchInSill(0.5f) && CarLootRules.CatchInSill(0f) && CarLootRules.CatchInSill(CarLootRules.SetDownWindow) && !CarLootRules.CatchInSill(CarLootRules.SetDownWindow + 0.1f) && !CarLootRules.CatchInSill(-1f) && !CarLootRules.CatchInSill(1000f));
            Check("car: the doorway strip starts at the car's zone and reaches past the outer face of the wall (4.2 m), and the inside is 4 less half a wall", CarLootRules.SillFrom == 3.6f && CarLootRules.SillTo > 4.2f && CarLootRules.InnerHalf == 4f - 0.4f / 2f && CarLootRules.SillHalfWidth >= 1.5f);
        }

        // ------------------------------------------------------------------ the loot scanner

        static void ScanRulesTest()
        {
            Check("scan: the pulse leaves at 28 m/s and no pulse goes further than 30 m", LootScanRules.RadiusAt(0f) == 0f && LootScanRules.RadiusAt(-1f) == 0f && LootScanRules.RadiusAt(1f) == LootScanRules.PulseSpeed && LootScanRules.MaxReach == 30f);
            var room = new Rc(10f, 20f, 18f, 26f);
            float reach = LootScanRules.Reach(11f, 21f, room);
            Check("scan: the pulse goes as far as the farthest corner of the room it was sent from (and no further than the limit)", System.Math.Abs(reach - Mathf.Sqrt(7f * 7f + 5f * 5f)) < 1e-4f
                && System.Math.Abs(LootScanRules.Reach(14f, 23f, room) - 5f) < 1e-4f && LootScanRules.Reach(0f, 0f, new Rc(-100f, -100f, 100f, 100f)) == LootScanRules.MaxReach);
            Check("scan: a thing is marked once the pulse has reached it", LootScanRules.Reached(10f, 10f) && LootScanRules.Reached(9f, 10f) && !LootScanRules.Reached(10.1f, 10f));
            Check("scan: only the room the player is in is scanned (never the room next door, never no room)", LootScanRules.SameRoom(7, 7) && !LootScanRules.SameRoom(7, 8) && !LootScanRules.SameRoom(7, -1) && !LootScanRules.SameRoom(-1, -1) && !LootScanRules.SameRoom(-1, 3));
            Check("scan: a little more than the room (the owner, 2026-10-22) - what lies within ExtraReach m of the player is scanned too, in another room or in none, and nothing further",
                LootScanRules.ExtraReach > 0f && LootScanRules.ExtraReach <= 4f && LootScanRules.InScan(true, 25f) && LootScanRules.InScan(false, 0.5f) && LootScanRules.InScan(false, LootScanRules.ExtraReach)
                && !LootScanRules.InScan(false, LootScanRules.ExtraReach + 0.1f) && !LootScanRules.InScan(false, 30f) && !LootScanRules.InScan(false, -1f));
            var office = new RoomSpot { index = 3, label = "114", kind = 0, theme = "office" };
            Check("scan: the room is named by its number, or as the open area, the lobby or the hallway", LootScanRules.RoomTitle(office) == "ROOM 114" && LootScanRules.RoomTitle(new RoomSpot { index = 4, kind = 1, theme = "open" }) == "THE OPEN AREA"
                && LootScanRules.RoomTitle(new RoomSpot { index = 5, kind = 2, theme = "lobby" }) == "THE LOBBY" && LootScanRules.RoomTitle(new RoomSpot { index = 6, kind = 2, theme = "hall" }) == "THE HALLWAY"
                && LootScanRules.RoomTitle(new RoomSpot { index = -1 }) == "NO ROOM" && office.Valid && !new RoomSpot { index = -1 }.Valid);
            Check("scan: mugs ($25) are not marked, valuables are", !LootScanRules.Worth(25) && !LootScanRules.Worth(59) && LootScanRules.Worth(60) && LootScanRules.Worth(120) && LootScanRules.Worth(1700));
            bool fades = LootScanRules.Alpha(0f) == 0f && LootScanRules.Alpha(0.01f) == 1f && LootScanRules.Alpha(3f) == 1f && LootScanRules.Alpha(LootScanRules.MarkSeconds) == 0f && LootScanRules.Alpha(LootScanRules.MarkSeconds + 1f) == 0f;
            float mid = LootScanRules.Alpha(LootScanRules.MarkSeconds - LootScanRules.FadeSeconds * 0.5f);
            Check("scan: a mark is fully there for a few seconds, then fades out over the last 1.4 s and is gone at 6", fades && mid > 0.45f && mid < 0.55f);
            float peak = 0f; for (float t = 0f; t <= 0.25f; t += 0.01f) peak = Mathf.Max(peak, LootScanRules.Pop(t));
            Check("scan: a mark pops up (0 at first, a little over 1 on the way, 1 at the end)", LootScanRules.Pop(0f) == 0f && peak > 1.02f && peak < 1.3f && System.Math.Abs(LootScanRules.Pop(0.3f) - 1f) < 1e-5f);
            Check("scan: the tiers follow the quota ($150 small, $420 good, $735 great, $1,260 and a safe's worth a jackpot on floor 1)", LootScanRules.Tier(150, 4300) == 0 && LootScanRules.Tier(420, 4300) == 1
                && LootScanRules.Tier(735, 4300) == 2 && LootScanRules.Tier(1260, 4300) == 3 && LootScanRules.Tier(3000, 4300) == 3 && LootScanRules.Tier(150, 9400) == 0 && LootScanRules.Tier(420, 9400) == 0);
            Check("scan: no scanning at the docks, with a screen open, pinned or busted, or during the cinematic", !LootScanRules.CanScan(0, false, false, false) && LootScanRules.CanScan(1, false, false, false)
                && LootScanRules.CanScan(12, false, false, false) && !LootScanRules.CanScan(3, true, false, false) && !LootScanRules.CanScan(3, false, true, false) && !LootScanRules.CanScan(3, false, false, true));
            Check("scan: holding the key sends a pulse at once, then another every 2.4 s", !LootScanRules.NextPulse(false, -1f) && LootScanRules.NextPulse(true, -1f) && !LootScanRules.NextPulse(true, 1f)
                && !LootScanRules.NextPulse(true, 2.39f) && LootScanRules.NextPulse(true, 2.4f) && !LootScanRules.NextPulse(false, 9f));

            // which room a point is in, on the real floors: the smallest that holds it; every valuable on a desk is in a room; the guard outpost is its own room inside its lobby
            const int cols = 8, rows = 5;
            int floors = 0, valuables = 0, inRoom = 0, doorway = 0, outpostOk = 0, outposts = 0, wrongHost = 0;
            foreach (var (floor, seed) in new[] { (1, 4101), (2, 777), (4, 9172), (6, 31337), (12, 515) })
            {
                var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
                var layout = OfficePlanner.Build(shape.PlanFor(floor, seed, 18f, 3.5f, 777));
                floors++;
                foreach (var pv in ValuablePlan.Plan(layout, seed, floor, 18f, cols, rows))
                {
                    valuables++;
                    int r = LootPlan.RoomAround(layout, pv.spot.x, pv.spot.z);
                    if (r >= 0) inRoom++;
                    // it is in the same room as its own desk
                    if (r >= 0 && LootPlan.RoomAround(layout, pv.spot.host.x, pv.spot.host.z) != r) wrongHost++;
                }
                if (layout.outpost != null)
                {
                    outposts++;
                    var b = layout.outpost.bounds;
                    int inside = LootPlan.RoomAround(layout, b.Cx, b.Cz);
                    bool isOutpost = inside >= 0 && layout.rooms[inside].theme == "outpost";
                    // a step outside the outpost's door is not the outpost (it is the corridor or lobby around it)
                    var d = layout.outpost.door;
                    int outside = LootPlan.RoomAround(layout, d.Cx + layout.outpost.frontX * 1.5f, d.Cz + layout.outpost.frontZ * 1.5f);
                    if (isOutpost && outside != inside) outpostOk++;
                }
                // a point in a doorway (inside the wall) still belongs to a room next to it
                foreach (var rm in layout.rooms)
                    if (rm.doors.Count > 0) { var d = rm.doors[0]; if (LootPlan.RoomAround(layout, d.Cx, d.Cz) >= 0) doorway++; break; }
            }
            Check($"scan: every valuable on a desk is in a room ({inRoom} of {valuables} in {floors} floors), the same room as its desk", inRoom == valuables && wrongHost == 0);
            Check("scan: the guard outpost is a room of its own (inside its cell's lobby), and a step outside its door is another room", outposts > 0 && outpostOk == outposts, $"({outpostOk} of {outposts})");
            Check("scan: a point in a doorway belongs to a room next to it", doorway == floors);
            bool listed = false;
            for (int g = 0; g < KeybindScreen.GroupCount; g++) for (int i = 0; i < KeybindScreen.EntryCount(g); i++) if (KeybindScreen.KeyAt(g, i) == "Z") listed = KeybindScreen.LabelAt(g, i).Contains("scan");
            Check("scan: the key is Z (nothing else uses it) and the Keybinds screen and the How to Play page list it", LootScanner.ScanKey == KeyCode.Z && listed
                && System.Array.Exists(HowToPlayContent.Controls, c => c.key.StartsWith("Z") && c.action.Contains("Scan")));
        }
        // ------------------------------------------------------------------ the "SECURITY OFFICE" signs

        static void SecuritySignTest()
        {
            const int cols = 8, rows = 5, n = cols * rows;
            int floors = 0, withOutpost = 0, tooFew = 0, badText = 0, wrongWay = 0, inOutpostCell = 0, differ = 0, clash = 0, total = 0, tooMany = 0;
            foreach (var (floor, seed) in new[] { (1, 4101), (2, 777), (3, 1234), (4, 9172), (5, 55), (6, 31337), (7, 999), (9, 2288), (12, 515), (1, 7), (2, 8675309), (4, 424242) })
            {
                var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
                var layout = OfficePlanner.Build(shape.PlanFor(floor, seed, 18f, 3.5f, 777));
                var signs = DirectionSigns.Plan(layout, floor, cols, rows, shape.openE, shape.openN, 18f, 3.5f, seed);
                var again = DirectionSigns.Plan(layout, floor, cols, rows, shape.openE, shape.openN, 18f, 3.5f, seed);
                floors++;
                if (signs.Count != again.Count) differ++;
                else for (int i = 0; i < signs.Count; i++)
                    if (signs[i].x != again[i].x || signs[i].z != again[i].z || signs[i].line1 != again[i].line1 || signs[i].security != again[i].security) { differ++; break; }

                int target = DirectionSigns.OutpostCell(layout, 18f, cols, rows);
                if (target < 0) continue;
                withOutpost++;
                var dist = DirectionSigns.Distances(cols, rows, shape.openE, shape.openN);
                int security = 0;
                foreach (var s in signs)
                {
                    if (!s.security) continue;
                    security++;
                    if (s.line1 != DirectionSigns.SecurityTitle || s.line2 != DirectionSigns.SecurityRange) badText++;
                    int from = s.cellX + s.cellY * cols;
                    int sx = s.cellX + s.dirX, sy = s.cellY + s.dirZ, next = sx + sy * cols;
                    // the arrow leads along an open doorway to a cell one step closer to the outpost
                    bool open = DirectionSigns.IsOpen(cols, rows, shape.openE, shape.openN, s.cellX, s.cellY, sx, sy);
                    if (!open || dist[next * n + target] != dist[from * n + target] - 1 || s.toCellX + s.toCellY * cols != target || s.steps != dist[from * n + target]) wrongWay++;
                    if (from == target) inOutpostCell++;
                    foreach (var o in signs)
                        if (!ReferenceEquals(o, s) && DirectionSigns.Footprint(o, DirectionSigns.ClearDepth).Overlaps(DirectionSigns.Footprint(s, DirectionSigns.ClearDepth))
                            && System.Math.Abs(o.y - s.y) < (o.height + s.height) * 0.5f) clash++;
                }
                total += security;
                if (security < 3) tooFew++;
                if (security > DirectionSigns.SecurityAreas(n)) tooMany++;
            }
            Check($"security signs: {floors} floors planned twice give the same signs", differ == 0);
            Check($"security signs: every floor with a guard outpost ({withOutpost} of {floors}) has at least three ({total} in all)", withOutpost >= floors - 2 && tooFew == 0 && total >= 3 * withOutpost);
            Check("security signs: never more than one for each cell they are spread over", tooMany == 0);
            Check("security signs: they say SECURITY over OFFICE", badText == 0 && DirectionSigns.SecurityTitle == "SECURITY" && DirectionSigns.SecurityRange == "OFFICE");
            Check("security signs: each arrow leads through an open doorway to a cell one step nearer the outpost, and the sign knows how far it is", wrongWay == 0);
            Check("security signs: none in the outpost's own cell, none on top of another sign", inOutpostCell == 0 && clash == 0);
            Check("security signs: the lettering exists in the dot font", DotText.Has('S') && DotText.Has('E') && DotText.Has('C') && DotText.Has('U') && DotText.Has('R') && DotText.Has('I')
                && DotText.Has('T') && DotText.Has('Y') && DotText.Has('O') && DotText.Has('F') && DotText.Columns("SECURITY") == 31 && DotText.Columns("OFFICE") == 23);
            Check("security signs: 3 cells on a small floor, 5 on 8 x 5, never more than 6", DirectionSigns.SecurityAreas(12) == 3 && DirectionSigns.SecurityAreas(40) == 5 && DirectionSigns.SecurityAreas(200) == 6);
        }

        // ------------------------------------------------------------------ the way back to the basement

        static void GateTest()
        {
            bool table = true;
            for (int level = 0; level <= 13; level++)
            {
                bool office = level >= 1 && level <= 12;
                table &= (TowerRules.DownFrom(level, true) == 0) == office;      // open on a finished office floor, nowhere else
                table &= TowerRules.DownFrom(level, false) == -1;                  // shut on every floor whose quota is not met
            }
            Check("gate: the way back to the basement is shut on every unfinished floor and open on every finished office floor (not from the docks or the boss floor)", table);
            string shut = ElevatorExtraction.WhyNot(0, 2, -1, -1);
            Check("gate: the panel says why the docks are shut (the quota), not 'no way down'", shut.Contains("quota") && shut.Contains("Docks"));
            Check("gate: ... and says the old thing when the way is open", ElevatorExtraction.WhyNot(0, 2, -1, 0) == "Going down always leads to the Loading Docks");
            Check("gate: ... and says something else when asked about a higher floor", ElevatorExtraction.WhyNot(1, 0, -1, -1) != shut);
            Check("gate: finishing a floor does not change where up leads", TowerRules.UpFrom(1, true, 1) == 2 && TowerRules.UpFrom(1, false, 1) == -1 && TowerRules.UpFrom(3, true, 4) == -1);
        }
    }
}
