using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape.EditorTools
{
    /// <summary>Round 12 (2026-10-19 night), the pure rules: the darts game, the weapon bar, the first / third person view, the safes of floor 3.</summary>
    public static partial class NetDevTest
    {
        public static void Round12Test()
        {
            // ---- darts: where a dart lands, and what it scores
            var bull = DartsRules.Score(0f, 0f); var outer = DartsRules.Score(0.06f, 0f);
            Check("darts: the middle of the board is the bull for 50, the ring round it the outer bull for 25", bull.zone == DartsRules.Zone.Bull && bull.Score == 50 && outer.zone == DartsRules.Zone.OuterBull && outer.Score == 25);
            var single20 = DartsRules.Score(0f, 0.8f); var triple20 = DartsRules.Score(0f, 0.60f); var double20 = DartsRules.Score(0f, 0.98f); var out20 = DartsRules.Score(0f, 1.2f);
            Check("darts: straight up is 20: a single in the wide band (20), a triple in the narrow ring (60), a double on the rim (40), nothing outside the rim",
                single20.number == 20 && single20.Score == 20 && triple20.zone == DartsRules.Zone.Triple && triple20.Score == 60 && double20.zone == DartsRules.Zone.Double && double20.Score == 40 && !out20.OnBoard && out20.Score == 0);
            Check("darts: the numbers go clockwise as the thrower sees the board - 6 on the right, 3 at the bottom, 11 on the left, 5 and 1 next to the 20",
                DartsRules.Score(0.8f, 0f).number == 6 && DartsRules.Score(0f, -0.8f).number == 3 && DartsRules.Score(-0.8f, 0f).number == 11
                && DartsRules.Score(-0.8f * 0.17f, 0.8f).number == 5 && DartsRules.Score(0.8f * 0.17f, 0.8f).number == 1 && DartsRules.Ring.Length == 20);
            // 301: 301 -> 121 with six darts is not it; a bust sends the score back to the start of the turn; the last dart wins at exactly nothing
            var t20 = new DartsRules.Hit(DartsRules.Zone.Triple, 20); var t19 = new DartsRules.Hit(DartsRules.Zone.Triple, 19); var d20 = new DartsRules.Hit(DartsRules.Zone.Double, 20); var d12 = new DartsRules.Hit(DartsRules.Zone.Double, 12);
            var g = new DartsRules.Match(DartsRules.Mode.Game301);
            g.Throw(t20); bool oneDart = g.Remaining == 241 && g.DartsLeft == 2 && g.Turn == 1;
            g.Throw(t20); g.Throw(t20); bool turnOver = g.Remaining == 121 && g.DartsLeft == 3 && g.Turn == 2 && g.DartsThrown == 3;
            g.Throw(t20); g.Throw(t20); g.Throw(t20);
            bool bust = g.LastBust && g.Remaining == 121 && g.Turn == 3 && g.DartsLeft == 3 && !g.Won;
            g.Throw(d20); g.Throw(t19); g.Throw(d12);
            Check("darts 301: every dart takes its score off 301, three darts make a turn, a dart that would go below nothing is a bust (the turn's start score again), the dart that makes exactly nothing wins",
                oneDart && turnOver && bust && g.Won && g.Remaining == 0 && g.DartsThrown == 9, $"(remaining {g.Remaining}, darts {g.DartsThrown}, won {g.Won})");
            // Cricket: 20 to 15 and the bull, three marks each, marks beyond the third score points, all closed wins
            var c = new DartsRules.Match(DartsRules.Mode.Cricket);
            c.Throw(t20); bool closed20 = c.Closed(0) && c.Points == 0;
            c.Throw(new DartsRules.Hit(DartsRules.Zone.Single, 20)); bool points20 = c.Points == 20;
            c.Throw(new DartsRules.Hit(DartsRules.Zone.Single, 5)); bool otherNumber = c.marks.Sum() == 3 && c.Points == 20;      // (a 5 is no Cricket number)
            c.Throw(new DartsRules.Hit(DartsRules.Zone.Bull, 25)); c.Throw(new DartsRules.Hit(DartsRules.Zone.OuterBull, 25)); bool bullMarks = c.marks[6] == 3 && c.Points == 20;
            foreach (int n in new[] { 19, 18, 17, 16, 15 }) c.Throw(new DartsRules.Hit(DartsRules.Zone.Triple, n));
            Check("darts Cricket: 20 to 15 and the bull take three marks each (a double is two, a triple three, the outer bull one, the bull two), marks beyond the third score the number's points, a 5 counts for nothing, the game is won when all seven are closed",
                closed20 && points20 && otherNumber && bullMarks && c.AllClosed && c.Won && c.DartsThrown == 10, $"(marks {string.Join(",", c.marks)}, points {c.Points}, won {c.Won})");
            // ---- the scoreboard over the board and the throw line: what the wall says after each dart (the owner, 2026-10-19 night)
            var sbd = new DartsRules.Scoreboard();
            var blankLines = sbd.Lines();
            sbd.Apply(1, 0, 60, 1, 241, 0, false, false, DartsRules.Mode.Game301); var oneLines = sbd.Lines();
            sbd.Apply(1, 1, 19, 1, 222, 0, false, false, DartsRules.Mode.Game301); sbd.Apply(1, 2, 50, 1, 172, 0, false, false, DartsRules.Mode.Game301); var threeLines = sbd.Lines();
            sbd.Apply(1, 0, 0, 2, 172, 0, false, false, DartsRules.Mode.Game301); var nextTurnLines = sbd.Lines();
            sbd.Apply(2, 0, 20, 1, 281, 0, false, false, DartsRules.Mode.Game301); var otherLines = sbd.Lines();
            sbd.Apply(2, 1, 60, 1, 281, 0, true, false, DartsRules.Mode.Game301); var bustLines = sbd.Lines();
            sbd.Apply(2, 2, 25, 1, 0, 0, false, true, DartsRules.Mode.Game301); var wonLines = sbd.Lines();
            var cricketBoard = new DartsRules.Scoreboard(); cricketBoard.Apply(0, 0, 60, 1, 301, 40, false, false, DartsRules.Mode.Cricket); var cricketLines = cricketBoard.Lines();
            var badBoard = new DartsRules.Scoreboard();
            bool refusedNonsense = !badBoard.Apply(0, 3, 10, 1, 301, 0, false, false, DartsRules.Mode.Game301) && !badBoard.Apply(0, 0, 61, 1, 301, 0, false, false, DartsRules.Mode.Game301) && !badBoard.Apply(-1, 0, 10, 1, 301, 0, false, false, DartsRules.Mode.Game301) && badBoard.Slot < 0;
            bool drawable = true;
            foreach (var lineSet in new[] { blankLines, oneLines, threeLines, nextTurnLines, otherLines, bustLines, wonLines, cricketLines }) foreach (var line in lineSet) foreach (char ch in line) drawable &= DotText.Has(ch);
            Check("darts scoreboard: it starts blank (DARTS, three dashes), each dart shows its score in its place (a dash for the ones to come) with the thrower's name and what is left, a new turn or another player wipes the old darts",
                blankLines[0] == "DARTS" && blankLines[1] == "- - -" && blankLines[2] == "" && oneLines[0] == "PLAYER 2" && oneLines[1] == "60 - -" && oneLines[2] == "LEFT 241" && threeLines[1] == "60 19 50" && threeLines[2] == "LEFT 172"
                && nextTurnLines[1] == "0 - -" && otherLines[0] == "PLAYER 3" && otherLines[1] == "20 - -" && otherLines[2] == "LEFT 281",
                $"({string.Join(" | ", blankLines)} / {string.Join(" | ", oneLines)} / {string.Join(" | ", threeLines)} / {string.Join(" | ", nextTurnLines)} / {string.Join(" | ", otherLines)})");
            Check("darts scoreboard: a bust says BUST, a win says WON, Cricket shows the points, nonsense (a fourth dart, 61 points, nobody) is refused, and every letter on it can be drawn in dots",
                bustLines[2] == "BUST" && wonLines[2] == "WON" && cricketLines[2] == "PTS 40" && refusedNonsense && drawable,
                $"({string.Join(" | ", bustLines)} / {string.Join(" | ", wonLines)} / {string.Join(" | ", cricketLines)}, refused {refusedNonsense}, drawable {drawable})");
            Check("darts: the throw line is the regulation 2.37 m (7 ft 9 in) from the board; the throw-darts spot is round it: on the line, a step behind it and a step past it are in, too far back or too far to the side are out",
                Mathf.Approximately(DartsRules.ThrowDistance, 2.37f) && DartsRules.AtThrowLine(0f, 0f) && DartsRules.AtThrowLine(1.0f, 0f) && DartsRules.AtThrowLine(-0.4f, 0.5f) && !DartsRules.AtThrowLine(-0.7f, 0f) && !DartsRules.AtThrowLine(1.5f, 0f) && !DartsRules.AtThrowLine(0.5f, 1.0f));
            // the throw: how long the button is held, the speed, the drop-off, the spread
            Check("darts: the power is the time the button was held (full after 1.2 s, no more after it); a hard throw is faster, drops less over 4 m and is wilder than a soft one",
                DartsRules.Power(0.6f) > 0.49f && DartsRules.Power(0.6f) < 0.51f && DartsRules.Power(5f) == 1f && DartsRules.Power(-1f) == 0f && DartsRules.Speed(1f) > DartsRules.Speed(0.2f)
                && DartsRules.Drop(DartsRules.Speed(1f), 4f) < DartsRules.Drop(DartsRules.Speed(0.2f), 4f) && DartsRules.Drop(DartsRules.Speed(1f), 4f) > 0.1f && DartsRules.Spread(1f) > DartsRules.Spread(0.2f));

            // ---- the weapon bar: the first three weapons of the pack, in the order they came into it
            var bar = WeaponBarRules.Fill(new List<bool> { false, true, false, true, true, true });
            var oneWeapon = WeaponBarRules.Fill(new List<bool> { true }); var none = WeaponBarRules.Fill(new List<bool>());
            Check("weapon bar: three slots hold the first three weapons of the pack (the loot between them is skipped), the other slots are empty, the keys 1, 2, 3 pick the slots and no other key does",
                WeaponBarRules.Slots == 3 && bar[0] == 1 && bar[1] == 3 && bar[2] == 4 && oneWeapon[0] == 0 && oneWeapon[1] == -1 && oneWeapon[2] == -1 && none[0] == -1 && none[2] == -1
                && WeaponBarRules.SlotOfKey(1) == 0 && WeaponBarRules.SlotOfKey(3) == 2 && WeaponBarRules.SlotOfKey(0) == -1 && WeaponBarRules.SlotOfKey(4) == -1);

            // ---- first and third person: wheel up / Ctrl + Up = in, wheel down / Ctrl + Down = out, P = the other one; a glide of a quarter of a second
            bool target = ViewRules.Target(0f, 1f, false, false, false) == 1f && ViewRules.Target(1f, -1f, false, false, false) == 0f && ViewRules.Target(0f, 0f, true, false, false) == 1f
                && ViewRules.Target(1f, 0f, false, true, false) == 0f && ViewRules.Target(0f, 0f, false, false, true) == 1f && ViewRules.Target(1f, 0f, false, false, true) == 0f && ViewRules.Target(0.3f, 0f, false, false, false) == 0.3f;
            float z = 0f; for (int i = 0; i < 15; i++) z = ViewRules.Step(z, 1f, 0.01f);
            float zEnd = z; for (int i = 0; i < 20; i++) zEnd = ViewRules.Step(zEnd, 1f, 0.01f);
            Check("view: the wheel and Ctrl + Up / Down send the view to first or third person, P switches it, the glide takes a quarter of a second and the camera comes to the eyes (no distance) in first person",
                target && z > 0.5f && z < 0.7f && zEnd == 1f && ViewRules.Smooth(0f) == 0f && ViewRules.Smooth(1f) == 1f && ViewRules.IsFirstPerson(1f) && !ViewRules.IsFirstPerson(0.5f)
                && ViewRules.Distance(2.6f, 0f) == 2.6f && ViewRules.Distance(2.6f, 1f) == 0f && ViewRules.Seconds == 0.25f);

            // ---- floor 3: one pistol, in the outpost; the safes hold no weapon and no ammunition (armour, cash and watches stay)
            bool noGun = true, noClip = true, armourStays = false, cashStays = false, gunsLater = false, clipsLater = false;
            for (int sd = 0; sd < 400; sd++)
            {
                int rs = sd * 29 + 3;
                for (int i = 0, n = SafeRules.SafeCount(rs, 3); i < n; i++)
                {
                    noGun &= !SafeRules.HoldsGun(rs, 3, i) && !SafeRules.WeaponContents(rs, 3, i).Any(k => SafeRules.IsGunItem(k));
                    noClip &= !SafeRules.HasSideClip(rs, 3, i);
                    armourStays |= SafeRules.HoldsArmour(rs, 3, i); cashStays |= SafeRules.HasSideCash(rs, 3, i);
                }
                for (int i = 0, n = SafeRules.SafeCount(rs, 4); i < n; i++) { gunsLater |= SafeRules.HoldsGun(rs, 4, i); clipsLater |= SafeRules.HasSideClip(rs, 4, i); }
            }
            Check("safes: floor 3 holds no gun and no spare clip in any safe of any run (the one pistol of the floor is in the outpost's gun safe); armour and cash still turn up there and the safes of floor 4 still hold guns and clips (the owner, 2026-10-19 night)",
                noGun && noClip && armourStays && cashStays && gunsLater && clipsLater && !SafeRules.SafesHoldGuns(3) && SafeRules.SafesHoldGuns(4) && OutpostLoot.PewFloor == 3);
            Check("pistol: it carries twelve clips - 96 rounds, about 100 shots - and the other guns three (the owner, 2026-10-19 night)",
                RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].maxClips == 12 && RangedWeapon.NewMagazine(RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew]).MaxRounds == 96 && RangedWeapon.NewMagazine(RangedWeapon.Specs[(int)RangedWeapon.Kind.Smg]).MaxRounds == 72);
            // ---- the intro: the vendor's bay opens after the workers' line, in the last beat, a roller door's time before it ends
            var beats = IntroRules.Beats; int last = beats.Length - 1;
            Check("intro: the last beat is the vendor's bay - the roller door starts to roll up after the workers' line has ended and is all the way up before the picture fades",
                beats[last].dock == DockShot.Bay && IntroRules.BayOpensAt >= beats[last - 1].end && IntroRules.BayOpensAt + DockBayDoor.RollSeconds < beats[last].end - 0.5f && IntroRules.Total == beats[last].end);
        }
    }

    /// <summary>Round 12, in Play Mode: the doorways of the docks are clear, the dart board and its game, the weapon bar, the first / third person view.</summary>
    public partial class NetDevRunner
    {
        int r12Fails;

        void R12Check(string name, bool ok, string detail = "")
        {
            if (!ok) r12Fails++;
            Log($"[R12] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator Round12Scenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var gen = FindFirstObjectByType<FloorGenerator>();
            var player = WobblyPlayerRegistry.All[0];
            var pack = player.GetComponent<Backpack>();
            var rig = FindFirstObjectByType<CameraFollow>();
            bool gfx = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            yield return new WaitForSeconds(0.5f);

            // ---- the doorways of the break room and the PVP room are clear: no forklift, box or shelf stands in front of them (the owner, 2026-10-19 night: the forklift was across the PVP room's doorway)
            foreach (var (label, door) in new[] { ("break room", gen.BreakRoomDoor), ("PVP room", gen.PvpRoomDoor) })
            {
                var centre = new Vector3(door.x + 1.7f, 1.15f, door.z);                       // (from the wall's east face on: the wall's own boxes are not in the way)
                var blockers = Physics.OverlapBox(centre, new Vector3(1.5f, 0.8f, 0.7f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Where(c => c && !c.isTrigger && c.GetComponentInParent<WobblyPlayer>() == null && c.name != "Floor" && c.bounds.size.y < 3f).Select(c => c.name).Distinct().ToList();
                R12Check($"the {label}'s doorway is clear: nothing stands in the 3.2 m in front of it", blockers.Count == 0, $"(in the way: {string.Join(", ", blockers)})");
                var lifts = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Forklift").ToList();
                bool liftOk = true;
                foreach (var lt in lifts)
                {
                    var rs = lt.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    if (b.min.x < door.x + 4.6f && b.max.x > door.x && b.min.z < door.z + 2.4f && b.max.z > door.z - 2.4f) liftOk = false;
                }
                R12Check($"no forklift parks within 4.6 m of the {label}'s doorway", liftOk, $"({lifts.Count} forklifts)");
            }

            // ---- the PVP room is a warehouse now (the owner, 2026-10-19 night): an L with containers to hide behind, the annex joined to the hall, the way in from the doorway open
            {
                var annexRect = gen.PvpRoomAnnex; var hallRect = gen.PvpRoomArea; var blockRect = gen.PvpRoomBlock;
                var warehouse = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t && t.parent != null).ToList();
                bool InPvp(Vector3 p) => hallRect.Contains(new Vector2(p.x, p.z)) || annexRect.Contains(new Vector2(p.x, p.z)) || blockRect.Contains(new Vector2(p.x, p.z));
                var full = warehouse.Where(t => t.name == "container" && InPvp(t.position)).ToList();
                var small = warehouse.Where(t => t.name == "container_small" && InPvp(t.position)).ToList();
                float area = hallRect.width * hallRect.height + annexRect.width * annexRect.height + blockRect.width * blockRect.height;
                R12Check("the PVP room is a big warehouse (the owner: \"much larger\", not the break room's twin): the hall the whole way along the north wall, the deeper east part, the old pocket west of the elevator room, joined, 150 m2 and more (it was 42)",
                    hallRect.width > 20f && annexRect.width > 4f && annexRect.height > 7.5f && Mathf.Abs(annexRect.yMax - hallRect.yMin) < 0.3f && blockRect.width > 9f && Mathf.Abs(blockRect.yMax - hallRect.yMin) < 0.3f && area > 150f,
                    $"(hall {hallRect}, block {blockRect}, annex {annexRect}, {area:0} m2)");
                R12Check("two full-length shipping containers and two small ones stand in it, solid, as cover", full.Count == 2 && small.Count == 2 && full.All(t => t.GetComponentInChildren<Collider>() != null) && small.All(t => t.GetComponentInChildren<Collider>() != null),
                    $"({full.Count} full, {small.Count} small)");
                int cover = warehouse.Count(t => (t.name == "pallet_loaded" || t.name == "crate_large" || t.name == "barrel") && InPvp(t.position));
                R12Check("loaded pallets, crates and barrels give more cover (at least 8 pieces)", cover >= 8, $"({cover} pieces)");
                var joint = new Vector3(4.0f, 0f, hallRect.yMin);
                bool jointOpen = !Physics.CheckCapsule(joint + Vector3.up * 0.5f, joint + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var lane = new Vector3(gen.PvpRoomDoor.x - 1.4f, 0f, gen.PvpRoomDoor.z);
                bool laneOpen = !Physics.CheckCapsule(lane + Vector3.up * 0.5f, lane + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var middle = new Vector3((hallRect.xMin + hallRect.xMax) * 0.5f, 0f, (hallRect.yMin + hallRect.yMax) * 0.5f);
                bool middleOpen = !Physics.CheckCapsule(middle + Vector3.up * 1.1f, middle + Vector3.up * 1.7f, 1.0f, ~0, QueryTriggerInteraction.Ignore);          // (a capsule of 1 m radius: it starts above 1 m or it would reach into the floor)
                var blockJoint = new Vector3(19.0f, 0f, hallRect.yMin);
                bool blockJointOpen = !Physics.CheckCapsule(blockJoint + Vector3.up * 0.5f, blockJoint + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var cross = new Vector3(18.0f, 0f, 15.0f);
                bool crossOpen = !Physics.CheckCapsule(cross + Vector3.up * 0.5f, cross + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);       // (the dividing wall between the two cells of the docks has no north half: the hall runs through)
                R12Check("the hall opens into the annex and into the deeper east part (no wall: a player fits through the joints), the way in from the doorway is open and so is the middle of the hall (where the fighters start)",
                    jointOpen && laneOpen && middleOpen && blockJointOpen && crossOpen, $"(annex joint {jointOpen}, doorway lane {laneOpen}, middle {middleOpen}, block joint {blockJointOpen}, across the cell line {crossOpen})");
                R12Check("a fighter in the annex or in the deeper east part counts as inside the PVP room, one in the elevator room or in the way from its door to the vendor does not",
                    PvpRules.Inside(annexRect, 2.5f, 9f) && !PvpRules.Inside(annexRect, 7f, 9f) && !PvpRules.Inside(hallRect, 7f, 9f) && PvpRules.Inside(blockRect, 18f, 12f) && !PvpRules.Inside(blockRect, 18f, 9f) && !PvpRules.Inside(hallRect, 18f, 9f));
            }

            // ---- the dart board: on the east wall, across from the break room's doorway, middle 1.7 m up, its face to the west
            var board = FindFirstObjectByType<DartBoard>();
            R12Check("the dart board hangs in the docks", board != null);
            if (board != null)
            {
                var boardMeshes = board.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).Where(mm => mm != null).ToList();
                R12Check("the board has no portrait over the bull and no dart stuck in it: it is the clean mesh (one piece, the 160 triangles of the disc, under 5 cm deep)",
                    boardMeshes.Count(mm => mm.name == FloorGenerator.DartBoardCleanMesh) == 1 && !boardMeshes.Any(mm => mm.name == "SM_Prop_Darkboard_01") && boardMeshes.First(mm => mm.name == FloorGenerator.DartBoardCleanMesh).GetIndexCount(0) / 3 == 160 && boardMeshes.First(mm => mm.name == FloorGenerator.DartBoardCleanMesh).bounds.size.z < 0.05f,
                    $"({boardMeshes.Count} mesh(es): {string.Join(", ", boardMeshes.Select(mm => mm.name + " " + mm.GetIndexCount(0) / 3 + " triangles"))})");
                // the lane in front of the board is clear (the owner, 2026-10-19 night: "Boxes shouldn't be on the floor in-front of the dart board. It should be clear between the player and the board which should be a few feet")
                var laneProps = new List<string>();
                foreach (var pt in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Shelf" && pt.name != "StockShelf" && pt.name != "Forklift" && pt.name != "Drum" && pt.name != "Bollard") continue;
                    var prs = pt.GetComponentsInChildren<Renderer>();
                    if (prs.Length == 0) continue;
                    var pb = prs[0].bounds; foreach (var pr in prs) pb.Encapsulate(pr.bounds);
                    bool inFront = pb.max.x > board.FaceCentre.x - 4.6f && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > board.FaceCentre.z - 2.4f && pb.min.z < board.FaceCentre.z + 2.4f;
                    if (pb.min.y < 2.2f && inFront) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
                }
                R12Check("nothing stands on the floor in front of the dart board: the 4.6 m (15 ft) between the player and the board and 2.4 m to each side are clear of boxes, pallets, shelves, forklifts and drums", laneProps.Count == 0, $"(in the way: {string.Join("; ", laneProps)})");
                float width = gen.CellColumns * 18f;
                R12Check("it is on the east wall, straight across from the break room's doorway, facing west, its middle 1.7 m up",
                    Mathf.Abs(board.FaceCentre.x - (width - 0.2f)) < 0.5f && Mathf.Abs(board.FaceCentre.z - gen.BreakRoomDoor.z) < 0.3f && board.Normal.x < -0.99f && Mathf.Abs(board.FaceCentre.y - 1.7f) < 0.25f,
                    $"(face at {board.FaceCentre}, faces {board.Normal}, wall at {width - 0.2f}, doorway z {gen.BreakRoomDoor.z})");
                R12Check("E works it from a throwing distance (8 m) and a game is not on yet", board.reach >= 7.9f && board.Phase == DartBoard.State.Idle && !DartBoard.Playing);

                // a game of 301 and the throw: the same aim at the middle, a hard throw and a soft one, 3.5 m away
                player.TeleportTo(new Vector3(board.FaceCentre.x + board.Normal.x * 4f, 1.1f, board.FaceCentre.z), player.transform.rotation);       // (a game ends when the player walks 14 m from the board: the scenario's player starts in the elevator)
                yield return new WaitForSeconds(0.3f);
                board.Begin(DartsRules.Mode.Game301);
                R12Check("a game is on: the guns and the fists leave the left button alone (it throws)", DartBoard.Playing && board.Match != null && board.Match.mode == DartsRules.Mode.Game301);
                var from = board.FaceCentre + board.Normal * 3.5f + Vector3.down * 0.3f;
                var aimAbove = board.FaceCentre + Vector3.up * DartsRules.Drop(DartsRules.Speed(1f), 3.5f);        // (aim as far above the middle as the dart will drop)
                board.Launch(from, aimAbove, 1f, false);
                yield return new WaitForSeconds(1.0f);
                R12Check("a hard throw aimed over the middle lands on the board, near the middle, and its score is taken off 301", board.LastHit.OnBoard && board.DartsOnBoard == 1 && board.Match.DartsThrown == 1 && board.Match.Remaining == 301 - board.LastHit.Score,
                    $"(hit {board.LastHit.zone} {board.LastHit.number} = {board.LastHit.Score}, remaining {board.Match.Remaining})");
                var first = board.StuckDarts[0];
                int hardScore = board.LastHit.Score;
                float nearMiddle = Vector3.Distance(first.position, board.FaceCentre), inFace = Mathf.Abs(Vector3.Dot(first.position - board.FaceCentre, board.Normal));
                R12Check("the dart sticks in the board's face, within 8 cm of the middle (the drop-off was allowed for)", nearMiddle < 0.08f && inFace < 0.03f, $"({nearMiddle * 100f:0.0} cm from the middle, {inFace * 100f:0.0} cm off the face)");
                board.Launch(from, board.FaceCentre, 0.2f, false);
                yield return new WaitForSeconds(1.2f);
                var second = board.StuckDarts.Count > 1 ? board.StuckDarts[1] : null;
                R12Check("a soft throw at the same aim falls short: it lands well below the hard one (the drop-off)", second != null && second.position.y < first.position.y - 0.3f, $"(hard {first.position.y:0.00} m, soft {(second ? second.position.y : -1f):0.00} m)");
                // the scoreboard on the wall (the owner, 2026-10-19 night: "a little scoreboard on the wall above the dartboard that shows what score the player gets for each of their 3 darts thrown and everyone should see the results")
                int softScore = board.LastHit.Score;
                var sbv = board.ScoreboardView;
                R12Check("a little scoreboard hangs on the wall above the dart board, facing the room, about 2.6 m up",
                    sbv != null && sbv.transform.position.y > 2.3f && sbv.transform.position.y < 2.95f && Mathf.Abs(sbv.transform.position.z - board.FaceCentre.z) < 0.1f && Vector3.Dot(sbv.transform.forward, board.Normal) < -0.99f && Mathf.Abs(sbv.transform.position.x - board.FaceCentre.x) < 0.2f,
                    $"(at {(sbv ? sbv.transform.position.ToString("0.00") : "none")})");
                R12Check("after two darts it says PLAYER 1, the score of each of them (the third a dash) and what is left",
                    board.Scores.Slot == 0 && sbv != null && sbv.Lines[0] == "PLAYER 1" && sbv.Lines[1] == hardScore + " " + softScore + " -" && sbv.Lines[2] == "LEFT " + board.Match.Remaining && sbv.LitRuns > 20,
                    $"({(sbv ? string.Join(" | ", sbv.Lines) : "none")}, hard {hardScore}, soft {softScore}, left {board.Match.Remaining})");
                board.Launch(from, aimAbove, 1f, false);
                yield return new WaitForSeconds(1.0f);
                int thirdScore = board.LastHit.Score;
                R12Check("the third dart completes the turn: all three scores are on the wall, and the next dart starts a new turn",
                    sbv != null && sbv.Lines[1] == hardScore + " " + softScore + " " + thirdScore && board.Scores.Scores[2] == thirdScore && board.Match.Turn == 2 && board.Match.DartsLeft == 3,
                    $"({(sbv ? string.Join(" | ", sbv.Lines) : "none")}, turn {board.Match.Turn}, darts left {board.Match.DartsLeft})");

                // the throw line: tape on the floor 2.37 m from the board (7 ft 9 in), across the way to it
                var tape = board.ThrowLine;
                var tapeRend = tape ? tape.GetComponent<Renderer>() : null;
                float fromBoard = tape ? Vector2.Distance(new Vector2(tape.position.x, tape.position.z), new Vector2(board.FaceCentre.x, board.FaceCentre.z)) : -1f;
                R12Check("a strip of tape lies on the floor 2.37 m in front of the board, across the way to it: thin, about a metre long and flat",
                    tapeRend != null && Mathf.Abs(fromBoard - DartsRules.ThrowDistance) < 0.03f && tape.position.y < 0.05f && Vector3.Dot(tape.position - board.FaceCentre, board.Normal) > 2.3f
                    && Mathf.Min(tapeRend.bounds.size.x, tapeRend.bounds.size.z) < 0.1f && Mathf.Max(tapeRend.bounds.size.x, tapeRend.bounds.size.z) > 0.8f && tapeRend.bounds.size.y < 0.05f,
                    $"({fromBoard:0.00} m from the board, size {(tapeRend ? tapeRend.bounds.size.ToString("0.00") : "none")})");

                // standing at the line it says "throw darts", and E brings up the game's screen
                board.Leave();
                var onLine = board.LineCentre + board.Normal * 0.4f;
                player.TeleportTo(new Vector3(onLine.x, 1.1f, onLine.z), Quaternion.LookRotation(-board.Normal));
                yield return new WaitForSeconds(0.5f);
                R12Check("standing at the line the board offers a game: the prompt is \"[E] Throw darts\"", board.AtLine && board.Phase == DartBoard.State.Idle && board.Prompt == "[E] Throw darts", $"(at the line {board.AtLine}, phase {board.Phase}, prompt {board.Prompt})");
                board.testPressE = true;
                yield return null; yield return null;
                R12Check("E at the line brings up the game's screen (the chooser: 301 or Cricket)", board.Phase == DartBoard.State.Choosing && OutpostUi.Current == OutpostUi.Kind.Darts, $"(phase {board.Phase})");
                board.Begin(DartsRules.Mode.Game301);
                R12Check("...and a game can be started from it, with the player at the line", DartBoard.Playing);
                board.Leave();
                player.TeleportTo(new Vector3(onLine.x + board.Normal.x * 4f, 1.1f, onLine.z + board.Normal.z * 4f), Quaternion.LookRotation(-board.Normal));
                yield return new WaitForSeconds(0.4f);
                R12Check("four metres further back the prompt is gone", !board.AtLine);
                if (gfx)
                {
                    var cam = new GameObject("R12Cam").AddComponent<Camera>();
                    cam.fieldOfView = 50f; cam.nearClipPlane = 0.05f; cam.enabled = false;
                    cam.transform.position = board.FaceCentre + board.Normal * 2.4f + Vector3.up * 0.1f;
                    cam.transform.LookAt(board.FaceCentre);
                    ShotCam(cam, "round12_board");
                    cam.transform.position = board.FaceCentre + board.Normal * 3.4f + Vector3.up * 0.9f; cam.fieldOfView = 55f;
                    cam.transform.LookAt(board.FaceCentre + Vector3.up * 0.55f);
                    ShotCam(cam, "round12_scoreboard");
                    cam.transform.position = board.LineCentre + board.Normal * 2.6f + Vector3.up * 2.0f; cam.fieldOfView = 70f;
                    cam.transform.LookAt(Vector3.Lerp(board.LineCentre, board.FaceCentre, 0.35f) + Vector3.up * 0.3f);
                    ShotCam(cam, "round12_throw_line");
                    cam.transform.position = gen.BreakRoomDoor + new Vector3(-0.4f, 1.55f, 0f);
                    cam.transform.LookAt(board.FaceCentre);
                    cam.fieldOfView = 60f;
                    ShotCam(cam, "round12_across_from_breakroom");
                    var pd = gen.PvpRoomDoor;
                    cam.transform.position = new Vector3(pd.x + 6.5f, 2.0f, pd.z - 0.5f);
                    cam.transform.LookAt(new Vector3(pd.x, 1.2f, pd.z));
                    cam.fieldOfView = 70f;
                    ShotCam(cam, "round12_pvp_doorway");
                    cam.transform.position = new Vector3(8.6f, 1.7f, gen.PvpRoomArea.yMin + 2.3f);
                    cam.transform.LookAt(new Vector3(2.0f, 1.0f, gen.PvpRoomArea.yMin + 1.2f));
                    ShotCam(cam, "round12_pvp_hall");
                    cam.transform.position = new Vector3(3.5f, 1.8f, gen.PvpRoomAnnex.yMin + 0.8f);
                    cam.transform.LookAt(new Vector3(3.0f, 1.2f, gen.PvpRoomAnnex.yMax - 0.5f));
                    ShotCam(cam, "round12_pvp_annex");
                    Destroy(cam.gameObject);
                }
                board.Leave();
                R12Check("leaving ends the game", !DartBoard.Playing && board.Phase == DartBoard.State.Idle);
            }

            // ---- the weapon bar
            var at = player.transform.position + Vector3.up * 0.5f;
            var crowbar = MeleeWeapon.Create(MeleeWeapon.Kind.Crowbar, at, null).GetComponent<Pickup>();
            var pew = RangedWeapon.Create(RangedWeapon.Kind.Pew, at, null, 0f, 2).GetComponent<Pickup>();
            pack.TryStore(crowbar); pack.TryStore(pew);
            yield return new WaitForSeconds(0.4f);
            R12Check("the bar's slots hold the weapons in the order they came into the pack, the third slot is empty", pack.BarWeapon(0) == crowbar && pack.BarWeapon(1) == pew && pack.BarWeapon(2) == null);
            pack.SelectSlot(1); yield return null;
            bool pewInHand = pack.Equipped == pew;
            pack.SelectSlot(0); yield return null;
            bool crowbarInHand = pack.Equipped == crowbar;
            bool emptyKeeps = !pack.SelectSlot(2) && pack.Equipped == crowbar;
            R12Check("keys 2 and 1 take the pew and the crowbar in hand, key 3 (an empty slot) changes nothing", pewInHand && crowbarInHand && emptyKeeps);

            // ---- first and third person
            if (rig != null)
            {
                rig.SetFirstPerson(false); yield return new WaitForSeconds(0.5f);
                var body = rig.target;
                float third = Vector3.Distance(rig.transform.position, body.position + Vector3.up * rig.headHeight);
                R12Check("third person: the camera is behind the head (about 2.6 m) and the body is seen", !rig.IsFirstPerson && third > 1.5f && third < 3.2f, $"({third:0.00} m)");
                if (gfx) ShotCam(Camera.main, "round12_third_person");
                var parts = body.GetComponentsInChildren<Renderer>(true).Where(r => r && r.GetComponentInParent<HeldGun>() == null && r.GetComponentInParent<MeleeWeapon>() == null).ToList();
                var original = new Dictionary<Renderer, ShadowCastingMode>();
                foreach (var r in parts) original[r] = r.shadowCastingMode;
                rig.ToggleView(); yield return new WaitForSeconds(0.7f);
                float eyes = Vector3.Distance(rig.transform.position, body.position + Vector3.up * rig.eyeHeight);
                R12Check("P: first person - the camera is at the eyes, the body is out of the picture (it still casts its shadow)", rig.IsFirstPerson && eyes < 0.2f && parts.Count > 0 && parts.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly), $"(eyes {eyes:0.00} m off, {parts.Count} body parts)");
                if (gfx) ShotCam(Camera.main, "round12_first_person");
                rig.ToggleView(); yield return new WaitForSeconds(0.7f);
                R12Check("P again: back to third person, the body is back as it was", !rig.IsFirstPerson && parts.All(r => !r || r.shadowCastingMode == original[r]));
            }

            Log($"[R12] DONE fails={r12Fails}");
            Quit(r12Fails == 0 ? 0 : 1);
        }
    }
}
