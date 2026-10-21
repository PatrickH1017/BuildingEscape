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
            // the stance at the line (the owner, 2026-10-20): held at the middle of the tape, slides along it, turns a little
            Check("darts: the slide along the tape is 1.6 m/s, stops 0.45 m either side of the middle, a key held both ways cancels; the turn stops 25 degrees either side of the board (across the 0 / 360 line too)",
                Mathf.Abs(DartsRules.Lateral(0f, 1f, 0.1f) - 0.16f) < 0.001f && DartsRules.Lateral(0.4f, 1f, 1f) == DartsRules.LateralHalf && DartsRules.Lateral(-0.4f, -1f, 1f) == -DartsRules.LateralHalf
                && DartsRules.Lateral(0.2f, 0f, 1f) == 0.2f && DartsRules.Lateral(0f, 5f, 0.1f) == DartsRules.Lateral(0f, 1f, 0.1f)
                && Mathf.Abs(DartsRules.ClampAim(90f, 100f) - 100f) < 0.001f && Mathf.Abs(DartsRules.ClampAim(90f, 200f) - 115f) < 0.001f && Mathf.Abs(DartsRules.ClampAim(90f, -50f) - 65f) < 0.001f
                && Mathf.Abs(Mathf.DeltaAngle(DartsRules.ClampAim(350f, 20f), 15f)) < 0.001f && Mathf.Abs(Mathf.DeltaAngle(DartsRules.ClampAim(10f, 330f), 345f)) < 0.001f);
            Check("darts: the keys shown while a game is on are the slide, the turn, the throw and leaving; the stance fits inside the spot where \"throw darts\" is offered",
                DartsRules.Hints.Length == 4 && DartsRules.Hints[0].key == "A  D" && DartsRules.Hints[1].key == "MOUSE" && DartsRules.Hints[2].key == "LMB" && DartsRules.Hints[3].key == "E" && DartsRules.Hints.All(h => h.label.Length > 0)
                && DartsRules.StandBehind > 0f && DartsRules.StandBehind < DartsRules.LineZoneBehind && DartsRules.LateralHalf < DartsRules.LineZoneHalfWidth && DartsRules.PitchMin <= -20f && DartsRules.PitchMax >= 40f);
            // the throw: how long the button is held, the speed, the drop-off, the spread
            Check("darts: the power is the time the button was held (full after 1.2 s, no more after it); a hard throw is faster, drops less over 4 m and is wilder than a soft one",
                DartsRules.Power(0.6f) > 0.49f && DartsRules.Power(0.6f) < 0.51f && DartsRules.Power(5f) == 1f && DartsRules.Power(-1f) == 0f && DartsRules.Speed(1f) > DartsRules.Speed(0.2f)
                && DartsRules.Drop(DartsRules.Speed(1f), 4f) < DartsRules.Drop(DartsRules.Speed(0.2f), 4f) && DartsRules.Drop(DartsRules.Speed(1f), 4f) > 0.1f && DartsRules.Spread(1f) > DartsRules.Spread(0.2f));

            // ---- the leaderboard of the PVP room (the owner, 2026-10-20)
            PvpBoard.Reset();
            bool firstEnter = PvpBoard.Enter(2), secondEnter = PvpBoard.Enter(2), badEnter = PvpBoard.Enter(9) || PvpBoard.Enter(-1);
            PvpBoard.Enter(0); PvpBoard.Eliminate(0); PvpBoard.Eliminate(0); PvpBoard.Eliminate(2); PvpBoard.Eliminate(4); PvpBoard.Eliminate(-3);
            var boardOrder = new int[PvpBoard.Slots]; int boardRows = PvpBoard.Ranked(boardOrder);
            bool rankOk = boardRows == 3 && boardOrder[0] == 0 && boardOrder[1] == 2 && boardOrder[2] == 4;
            ulong boardPacked = PvpBoard.Pack(); int versionBefore = PvpBoard.Version;
            PvpBoard.Reset(); bool emptied = !PvpBoard.Any && PvpBoard.Count(0) == 0;
            PvpBoard.Apply(boardPacked);
            bool restored = PvpBoard.Listed(0) && PvpBoard.Listed(2) && PvpBoard.Listed(4) && !PvpBoard.Listed(1) && PvpBoard.Count(0) == 2 && PvpBoard.Count(2) == 1 && PvpBoard.Count(4) == 1 && PvpBoard.Pack() == boardPacked;
            for (int i = 0; i < 300; i++) PvpBoard.Eliminate(1);
            bool capped = PvpBoard.Count(1) == PvpBoard.MaxCount && PvpBoard.Count(7) == 0 && !PvpBoard.Listed(-1);
            PvpBoard.Reset();
            Check("pvp leaderboard: a player who walks into the room is put on it once, a knock-out counts for the one who made it (and puts him on it), the list is ranked by eliminations (a tie: the lower slot), it packs into one number and comes back the same, it stops at 255, nonsense slots are refused",
                firstEnter && !secondEnter && !badEnter && rankOk && emptied && restored && capped && PvpBoard.Version > versionBefore, $"(rows {boardRows}, order {boardOrder[0]} {boardOrder[1]} {boardOrder[2]}, restored {restored}, capped {capped})");
            // ---- backup officers spot a player much faster (the owner, 2026-10-20)
            Check("guards: a backup officer notices a player in his cone in a quarter of the usual time (14 m: about 0.5 s instead of 1.8 s), the usual guard as before",
                GuardRules.BackupSpotFactor <= 0.34f && GuardRules.BackupSpotFactor > 0f && GuardRules.SpotSeconds(14f) * GuardRules.BackupSpotFactor < 0.5f && GuardRules.SpotSeconds(14f) > 1.5f);
            // ---- the weapon bar: the first six weapons of the pack, in the order they came into it (six shortcuts, 1 to 6: the owner, 2026-10-20 night)
            var bar = WeaponBarRules.Fill(new List<bool> { false, true, false, true, true, true, true, false, true, true });
            var oneWeapon = WeaponBarRules.Fill(new List<bool> { true }); var none = WeaponBarRules.Fill(new List<bool>());
            Check("weapon bar: six slots hold the first six weapons of the pack (the loot between them is skipped), the other slots are empty, the keys 1 to 6 pick the slots and no other key does",
                WeaponBarRules.Slots == 6 && bar.Length == 6 && bar[0] == 1 && bar[1] == 3 && bar[2] == 4 && bar[3] == 5 && bar[4] == 6 && bar[5] == 8 && oneWeapon[0] == 0 && oneWeapon[1] == -1 && oneWeapon[5] == -1 && none[0] == -1 && none[5] == -1
                && WeaponBarRules.SlotOfKey(1) == 0 && WeaponBarRules.SlotOfKey(6) == 5 && WeaponBarRules.SlotOfKey(0) == -1 && WeaponBarRules.SlotOfKey(7) == -1);

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
            // ---- the intro: the vendor's bay opens after the workers' line and the vendor says his four lines (the owner, 2026-10-20)
            var beats = IntroRules.Beats; int last = beats.Length - 1;
            Check("intro: the vendor's bay door starts to roll up after the workers' line has ended and is all the way up before he speaks, and his four lines end the Intro",
                beats[IntroRules.DoorBeat].dock == DockShot.Bay && IntroRules.BayOpensAt >= beats[IntroRules.WorkerBeat].end && IntroRules.BayOpensAt + DockBayDoor.RollSeconds < beats[IntroRules.FirstVendorBeat].start && beats[last].speaker == Speaker.Vendor && IntroRules.Total == beats[last].end);
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
                    if (b.min.x < door.x + (door == gen.PvpRoomDoor ? 6.4f : 4.6f) && b.max.x > door.x && b.min.z < door.z + 2.4f && b.max.z > door.z - 2.4f) liftOk = false;
                }
                R12Check($"no forklift parks in front of the {label}'s doorway (within 4.6 m of it, 6.4 m for the PVP room's)", liftOk, $"({lifts.Count} forklifts)");
            }

            // ---- the PVP room (the owner, 2026-10-20, with a sketch: "extend it further back behind the elevator and extend out the walls wider to the right when you walk into the PvP room"): the old room with its doorway where it was, 6.4 m more to the
            // north (out through the building's north wall) and the pocket behind the elevator room joined to it
            {
                var room = gen.PvpRoomArea; var annex = gen.PvpRoomAnnex;
                float area = room.width * room.height + annex.width * annex.height;
                R12Check("the PVP room is the old room with the doorway where it was, 6.4 m further north, and the pocket behind the elevator room joined to it: 120 to 150 m2 (it was 42)",
                    room.xMin < 0.5f && room.yMin > 13f && room.yMax > 24f && room.width > 8.5f && room.width < 9.6f && annex.xMin < 0.5f && annex.yMin < 6f && annex.yMax > 12.5f && Mathf.Abs(annex.yMax - room.yMin) < 0.3f && annex.width > 4.2f && area > 120f && area < 150f
                    && Mathf.Abs(gen.PvpRoomDoor.x - 9.5f) < 0.1f && Mathf.Abs(gen.PvpRoomDoor.z - 15.6f) < 0.1f,
                    $"(room {room}, annex {annex}, {area:0} m2, doorway {gen.PvpRoomDoor})");
                // the part that goes out to the north: a floor to stand on, a ceiling, walls on three sides (above the containers), and the old north wall open in front of it but standing east of it
                var inNorth = new Vector3(4.75f, 1.0f, 21.5f);
                bool floorThere = Physics.Raycast(inNorth, Vector3.down, out var floorHit, 3f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(floorHit.point.y) < 0.05f;
                bool ceilingThere = Physics.Raycast(inNorth, Vector3.up, out var ceilHit, 6f, ~0, QueryTriggerInteraction.Ignore) && ceilHit.collider.name == "Ceiling_PvpNorth";
                var high = new Vector3(4.75f, 2.9f, 21.5f);
                bool wallN = Physics.Raycast(high, Vector3.forward, out var hn, 6f, ~0, QueryTriggerInteraction.Ignore) && hn.point.z > 24f && hn.point.z < 24.5f;
                bool wallE = Physics.Raycast(high, Vector3.right, out var he, 8f, ~0, QueryTriggerInteraction.Ignore) && he.point.x > 9.2f && he.point.x < 9.7f;
                bool wallW = Physics.Raycast(high, Vector3.left, out var hw, 8f, ~0, QueryTriggerInteraction.Ignore) && hw.point.x > -0.3f && hw.point.x < 0.3f;
                var gapSpot = new Vector3(5.0f, 0f, 18.0f); var wallSpot = new Vector3(12.0f, 0f, 18.0f);
                bool oldWallOpen = !Physics.CheckCapsule(gapSpot + Vector3.up * 0.5f, gapSpot + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
                bool oldWallStands = Physics.CheckCapsule(wallSpot + Vector3.up * 0.5f, wallSpot + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore);
                R12Check("the north part has a floor under it, a ceiling over it and walls on the north, east and west; the building's old north wall is open in front of it and still stands east of it",
                    floorThere && ceilingThere && wallN && wallE && wallW && oldWallOpen && oldWallStands, $"(floor {floorThere}, ceiling {ceilingThere}, walls N {wallN} E {wallE} W {wallW}, old wall open {oldWallOpen}, stands {oldWallStands})");
                var joint = new Vector3(4.0f, 0f, room.yMin);
                bool jointOpen = !Physics.CheckCapsule(joint + Vector3.up * 0.5f, joint + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var lane = new Vector3(gen.PvpRoomDoor.x - 1.4f, 0f, gen.PvpRoomDoor.z);
                bool laneOpen = !Physics.CheckCapsule(lane + Vector3.up * 0.5f, lane + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore);
                var middle = new Vector3((room.xMin + room.xMax) * 0.5f, 0f, (room.yMin + room.yMax) * 0.5f);
                bool middleOpen = !Physics.CheckCapsule(middle + Vector3.up * 1.1f, middle + Vector3.up * 1.7f, 1.0f, ~0, QueryTriggerInteraction.Ignore);          // (a capsule of 1 m radius: it starts above 1 m or it would reach into the floor)
                R12Check("the pocket opens into the room (no wall: a player fits through the joint), the way in from the doorway is open and so is the middle of the room (where the fighters start)",
                    jointOpen && laneOpen && middleOpen, $"(joint {jointOpen}, doorway lane {laneOpen}, middle {middleOpen})");
                var warehouse = FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(w => w && w.parent != null).ToList();
                bool InPvp(Vector3 p) => room.Contains(new Vector2(p.x, p.z)) || annex.Contains(new Vector2(p.x, p.z));
                // no freight containers (the owner, 2026-10-20: "the PvP room should not have any freight containers inside of it, more shelves with boxes"): steel shelving with its cardboard instead, pallets and crates for lower cover
                var pvpContainers = warehouse.Where(w => (w.name == "container" || w.name == "container_small") && InPvp(w.position)).ToList();
                var pvpRacks = warehouse.Where(w => w.name == "Shelf" && InPvp(w.position)).ToList();
                int cover = warehouse.Count(w => (w.name == "pallet_loaded" || w.name == "crate_large" || w.name == "barrel") && InPvp(w.position));
                bool racksSolid = pvpRacks.All(w => w.GetComponentsInChildren<Collider>().Length == 1 && w.GetComponent<BoxCollider>() != null);
                int rackBoxes = pvpRacks.Sum(w => w.GetComponentsInChildren<Transform>().Count(c => c.name == "ShelfBox"));
                R12Check("no shipping container stands in the PVP room; steel shelving with its boxes does (20 units or more, each solid with one box collider), and pallets and crates give lower cover (at least 6 pieces)",
                    pvpContainers.Count == 0 && pvpRacks.Count >= 20 && racksSolid && rackBoxes >= pvpRacks.Count * 4 && cover >= 6, $"({pvpContainers.Count} containers, {pvpRacks.Count} shelves with {rackBoxes} boxes, {cover} pallets / crates / barrels)");
                // nothing stands inside anything else or through a wall: the floor plans of the shelves, pallets, crates and box blocks do not overlap (a crate on a crate is above it, not in it) and lie within the room
                var pvpPieces = new List<(string name, Bounds box)>();
                foreach (var piece in warehouse.Where(w => (w.name == "Shelf" || w.name == "pallet_loaded" || w.name == "crate_large" || w.name == "barrel" || w.name == "BoxBlock") && InPvp(w.position)))
                {
                    var pieceRend = piece.GetComponentsInChildren<Renderer>();
                    if (pieceRend.Length == 0) continue;
                    var pieceBox = pieceRend[0].bounds; foreach (var pr0 in pieceRend) pieceBox.Encapsulate(pr0.bounds);
                    pvpPieces.Add((piece.name, pieceBox));
                }
                var pvpClash = new List<string>();
                for (int ia = 0; ia < pvpPieces.Count; ia++)
                    for (int ib = ia + 1; ib < pvpPieces.Count; ib++)
                    {
                        var pa = pvpPieces[ia].box; var pb = pvpPieces[ib].box;
                        bool inPlan = pa.min.x < pb.max.x - 0.03f && pb.min.x < pa.max.x - 0.03f && pa.min.z < pb.max.z - 0.03f && pb.min.z < pa.max.z - 0.03f;
                        bool inHeight = pa.min.y < pb.max.y - 0.05f && pb.min.y < pa.max.y - 0.05f;
                        if (inPlan && inHeight) pvpClash.Add($"{pvpPieces[ia].name} {pa.center:0.0} / {pvpPieces[ib].name} {pb.center:0.0}");
                    }
                var pvpOut = pvpPieces.Where(pp => !(InPvp(new Vector3(pp.box.min.x + 0.01f, 0f, pp.box.min.z + 0.01f)) && InPvp(new Vector3(pp.box.max.x - 0.01f, 0f, pp.box.max.z - 0.01f))
                    && InPvp(new Vector3(pp.box.min.x + 0.01f, 0f, pp.box.max.z - 0.01f)) && InPvp(new Vector3(pp.box.max.x - 0.01f, 0f, pp.box.min.z + 0.01f)))).Select(pp => pp.name + " " + pp.box.center.ToString("0.0")).ToList();
                R12Check("nothing in the PVP room stands inside anything else or through a wall: the shelves, pallets, crates and box blocks have floor plans that do not overlap and lie within the room",
                    pvpClash.Count == 0 && pvpOut.Count == 0, $"({pvpPieces.Count} pieces; overlapping: {string.Join("; ", pvpClash.Take(6))}; outside: {string.Join("; ", pvpOut.Take(6))})");
                // the doorway is a forklift door (the owner, 2026-10-20: "the forklift should technically fit through it"): a box the size of the lobby's forklift passes it side-on with room to spare, and the break room's doorway is still the ordinary one
                var pvpForkTmp = new GameObject("R12ForkSize").transform;
                var pvpFork = DockDressing.Forklift(pvpForkTmp, 0f, 0f, 0f);
                var pvpForkRend = pvpFork.GetComponentsInChildren<Renderer>();
                var pvpForkBox = pvpForkRend[0].bounds; foreach (var fr0 in pvpForkRend) pvpForkBox.Encapsulate(fr0.bounds);
                Destroy(pvpForkTmp.gameObject);
                bool DoorPasses(Vector3 doorAt, float width, float height) => !Physics.CheckBox(new Vector3(doorAt.x, 0.06f + height * 0.5f, doorAt.z), new Vector3(0.3f, height * 0.5f, width * 0.5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                R12Check("the PVP room's doorway is a forklift door (2.4 m wide, 2.8 m high): the lobby's forklift goes through it with room to spare, and the break room's door is still the ordinary 1.6 m one",
                    DoorPasses(gen.PvpRoomDoor, pvpForkBox.size.x + 0.6f, pvpForkBox.size.y + 0.3f) && DoorPasses(gen.PvpRoomDoor, 2.3f, 2.7f) && !DoorPasses(gen.PvpRoomDoor, 2.5f, 2.7f)
                    && DoorPasses(gen.BreakRoomDoor, 1.5f, 2.5f) && !DoorPasses(gen.BreakRoomDoor, 1.7f, 2.5f), $"(the forklift is {pvpForkBox.size.x:0.00} x {pvpForkBox.size.y:0.00} m)");
                // nothing blocks the way in (the owner, 2026-10-20: "the Forklift should not be blocking the PvP room doorway ... players need to be able to get into the PvP room"): a walk-through on the floor plan
                string pvpWayIn = PvpWayIn(gen);
                R12Check("players can walk from just outside the elevator's door to EVERY free spot of the PVP room, and a forklift-sized body (1.3 x 2.3 m) to its doorway and through it: nothing blocks the way in", pvpWayIn.Length == 0, $"({pvpWayIn})");
                R12Check("a fighter in the pocket or in the north part counts as inside the PVP room, one in the elevator room or in the hallway outside does not",
                    PvpRules.Inside(annex, 2.5f, 9f) && PvpRules.Inside(room, 4.75f, 22f) && !PvpRules.Inside(annex, 7f, 9f) && !PvpRules.Inside(room, 12f, 15.6f) && !PvpRules.Inside(room, 12f, 20f));
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
                    if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Forklift" && pt.name != "Drum" && pt.name != "Bollard") continue;          // (the shelves against the wall may stay: the owner, 2026-10-20)
                    var prs = pt.GetComponentsInChildren<Renderer>();
                    if (prs.Length == 0) continue;
                    var pb = prs[0].bounds; foreach (var pr in prs) pb.Encapsulate(pr.bounds);
                    bool inFront = pb.max.x > board.FaceCentre.x - 4.6f && pb.min.x < board.FaceCentre.x + 0.4f && pb.max.z > board.FaceCentre.z - 2.4f && pb.min.z < board.FaceCentre.z + 2.4f;
                    if (pb.min.y < 2.2f && inFront) laneProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
                }
                R12Check("nothing stands on the floor in front of the dart board: the 4.6 m (15 ft) between the player and the board and 2.4 m to each side are clear of boxes, pallets, forklifts and drums (the steel shelves against the wall may stay)", laneProps.Count == 0, $"(in the way: {string.Join("; ", laneProps)})");
                float wallFace = 18f - 0.2f;                                                       // (the west face of the wall between the two halves of the docks)
                R12Check("it hangs at the end of the hallway outside the break room (on the wall between the two halves of the docks), straight across from the break room's doorway, facing west, its middle 1.7 m up",
                    Mathf.Abs(board.FaceCentre.x - wallFace) < 0.5f && Mathf.Abs(board.FaceCentre.z - gen.BreakRoomDoor.z) < 0.3f && board.Normal.x < -0.99f && Mathf.Abs(board.FaceCentre.y - 1.7f) < 0.25f,
                    $"(face at {board.FaceCentre}, faces {board.Normal}, wall face at {wallFace}, doorway z {gen.BreakRoomDoor.z})");
                // the hallway outside the break room is cleaned up (the owner, 2026-10-20: "the hallway should just be cleaned up a bit (leave the shelves against the wall)"): nothing but the shelves stands on its floor
                var hallProps = new List<string>();
                foreach (var pt in FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (pt.name != "BoxStack" && pt.name != "BoxBlock" && pt.name != "Forklift" && pt.name != "Drum") continue;
                    var prs = pt.GetComponentsInChildren<Renderer>();
                    if (prs.Length == 0) continue;
                    var pb = prs[0].bounds; foreach (var pr in prs) pb.Encapsulate(pr.bounds);
                    if (pb.min.y < 2.2f && pb.max.x > gen.BreakRoomDoor.x + 0.2f && pb.min.x < wallFace && pb.max.z > 0.2f && pb.min.z < 7f) hallProps.Add(pt.name + " at " + pb.center.ToString("0.0"));
                }
                R12Check("the hallway outside the break room is cleaned up: no box, pallet, drum or forklift stands on its floor between the doorway and the board's wall (the steel shelves along the walls stay)", hallProps.Count == 0, $"(in the way: {string.Join("; ", hallProps)})");
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

                // the stance (the owner, 2026-10-20: "when a player presses to throw the darts they should get locked into the position at the center of the tape but there should be able to move left and right and turn a bit like Wii bowling"):
                // the game holds the player at the middle of the tape, A / D slide him along it, the mouse turns him a little
                R12Check("the tape lies in the hallway outside the break room: east of its doorway, a few metres short of the board's wall, across the way to the board",
                    tape != null && tape.position.x > gen.BreakRoomDoor.x + 2f && tape.position.x < 18f - 2f && tape.position.z > 0.5f && tape.position.z < 5f, $"(tape at {(tape ? tape.position.ToString("0.00") : "none")})");
                var wobLock = board.LockedBody;
                float centreYaw = Mathf.Atan2(-board.Normal.x, -board.Normal.z) * Mathf.Rad2Deg;
                var sideways = Vector3.Cross(Vector3.up, -board.Normal);
                var rel0 = player.transform.position - board.LineCentre; rel0.y = 0f;
                R12Check("a game holds the player at the middle of the tape, a step behind it, facing the board, and the keys do not walk him",
                    wobLock == player && player.inputLocked && Mathf.Abs(Vector3.Dot(rel0, board.Normal) - DartsRules.StandBehind) < 0.08f && Mathf.Abs(Vector3.Dot(rel0, sideways)) < 0.08f && rig != null && rig.AimLimited,
                    $"(locked {wobLock != null}, behind {Vector3.Dot(rel0, board.Normal):0.00}, aside {Vector3.Dot(rel0, sideways):0.00})");
                var camMain = Camera.main;
                if (camMain != null)
                {
                    var sight = new Ray(camMain.transform.position, camMain.transform.forward);
                    float den = Vector3.Dot(sight.direction, board.Normal);
                    float tt = den < -0.001f ? Vector3.Dot(board.FaceCentre - sight.origin, board.Normal) / den : -1f;
                    float off = tt > 0f ? Vector3.Distance(sight.GetPoint(tt), board.FaceCentre) : 99f;
                    R12Check("the crosshair starts on the board: the view is turned so that the line of sight from the camera meets the board's middle (within 12 cm)", off < 0.12f, $"({off * 100f:0} cm off)");
                }
                if (gfx)
                {
                    var scam = new GameObject("R12StanceCam").AddComponent<Camera>();
                    scam.fieldOfView = 60f; scam.nearClipPlane = 0.05f; scam.enabled = false;
                    scam.transform.position = player.transform.position + board.Normal * 2.6f + Vector3.up * 1.3f + sideways * 0.45f;
                    scam.transform.LookAt(board.FaceCentre);
                    ShotCam(scam, "round12_stance");
                    Destroy(scam.gameObject);
                }
                player.testMove = new Vector3(1f, 0f, 0f);
                yield return new WaitForSeconds(1.2f);
                var rel1 = player.transform.position - board.LineCentre; rel1.y = 0f;
                R12Check("holding D slides him to the right along the tape, as far as its end and no further (0.45 m)", Mathf.Abs(Vector3.Dot(rel1, sideways) - DartsRules.LateralHalf) < 0.06f && Mathf.Abs(board.LateralOffset - DartsRules.LateralHalf) < 0.02f,
                    $"(aside {Vector3.Dot(rel1, sideways):0.00}, offset {board.LateralOffset:0.00})");
                player.testMove = new Vector3(-1f, 0f, 0f);
                yield return new WaitForSeconds(2.4f);
                var rel2 = player.transform.position - board.LineCentre; rel2.y = 0f;
                R12Check("holding A slides him back across the middle to the other end", Mathf.Abs(Vector3.Dot(rel2, sideways) + DartsRules.LateralHalf) < 0.06f, $"(aside {Vector3.Dot(rel2, sideways):0.00})");
                player.testMove = new Vector3(0f, 0f, 1f);
                yield return new WaitForSeconds(1.0f);
                var rel3 = player.transform.position - board.LineCentre; rel3.y = 0f;
                R12Check("holding W does not walk him off the line (he stays a step behind the tape)", Mathf.Abs(Vector3.Dot(rel3, board.Normal) - DartsRules.StandBehind) < 0.1f, $"(behind {Vector3.Dot(rel3, board.Normal):0.00})");
                player.testMove = null;
                rig.TurnAim(90f);
                bool turnedRight = Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) <= DartsRules.TurnHalf + 0.01f && Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) > DartsRules.TurnHalf - 1f;
                rig.TurnAim(-200f);
                bool turnedLeft = Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) <= DartsRules.TurnHalf + 0.01f && Mathf.Abs(Mathf.DeltaAngle(centreYaw, rig.AimYaw)) > DartsRules.TurnHalf - 1f;
                R12Check("the mouse turns him only a little: 90 degrees to one side or 200 to the other stop at 25 degrees either side of the board", turnedRight && turnedLeft, $"(yaw {rig.AimYaw:0}, centre {centreYaw:0})");
                // the camera also goes up and down (the owner, 2026-10-20: "The dart camera should also be able to go up and down as well"): from looking 30 degrees up to 50 degrees down, which takes in the scoreboard and all of the board
                float pitchStart = rig.AimPitch;
                rig.TiltAim(1000f); float pitchDown = rig.AimPitch;
                float sightDown = float.NaN, sightUp = float.NaN;
                var camTilt = Camera.main;
                float SightHeight() { var sr = new Ray(camTilt.transform.position, camTilt.transform.forward); float dn = Vector3.Dot(sr.direction, board.Normal); return dn > -0.001f ? float.NaN : sr.GetPoint(Vector3.Dot(board.FaceCentre - sr.origin, board.Normal) / dn).y; }
                if (camTilt != null) { yield return new WaitForSeconds(0.4f); sightDown = SightHeight(); }
                rig.TiltAim(-1000f); float pitchUp = rig.AimPitch;
                if (camTilt != null) { yield return new WaitForSeconds(0.4f); sightUp = SightHeight(); }
                R12Check("the camera also tilts up and down: from 30 degrees up to 50 degrees down and no further; looking down the line of sight passes below the board, looking up it passes above the scoreboard",
                    Mathf.Abs(pitchDown - DartsRules.PitchMax) < 0.01f && Mathf.Abs(pitchUp - DartsRules.PitchMin) < 0.01f && (camTilt == null || (sightDown < board.FaceCentre.y - 0.35f && sightUp > board.FaceCentre.y + DartBoard.ScoreboardHeight + 0.3f)),
                    $"(start {pitchStart:0}, down {pitchDown:0}, up {pitchUp:0}, the line of sight at the board: {sightDown:0.00} m low, {sightUp:0.00} m high)");
                board.Leave();
                R12Check("leaving lets him go: the keys walk him again and the aim turns freely", !player.inputLocked && !rig.AimLimited && board.LockedBody == null);

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
                player.TeleportTo(new Vector3(board.LineCentre.x, 1.1f, board.LineCentre.z), Quaternion.LookRotation(-board.Normal));          // (back at the tape, facing the board: open floor behind him for the pictures and the camera tests below)
                yield return new WaitForSeconds(0.4f);
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
                    cam.transform.position = new Vector3(8.8f, 1.8f, 15.6f);
                    cam.transform.LookAt(new Vector3(3.5f, 1.2f, 21.0f));
                    ShotCam(cam, "round12_pvp_north");
                    cam.transform.position = new Vector3(8.7f, 2.2f, 14.2f);
                    cam.transform.LookAt(new Vector3(2.0f, 1.0f, 16.0f));
                    ShotCam(cam, "round12_pvp_hall");
                    cam.transform.position = new Vector3(3.5f, 1.8f, gen.PvpRoomAnnex.yMin + 0.8f);
                    cam.transform.LookAt(new Vector3(3.0f, 1.2f, gen.PvpRoomAnnex.yMax - 0.5f));
                    ShotCam(cam, "round12_pvp_annex");
                    // the way in (the owner, 2026-10-20: "players need to be able to get into the PvP room"): from the elevator's door up the clear lane to the plaza in front of the doorway
                    var elevOut = gen.CellCentre(Vector2Int.zero) + new Vector3(5.2f, 0f, 0f);
                    cam.fieldOfView = 70f;
                    cam.transform.position = new Vector3(elevOut.x, 1.7f, elevOut.z);
                    cam.transform.LookAt(new Vector3(elevOut.x, 1.3f, pd.z));
                    ShotCam(cam, "round12_pvp_way_in");
                    // the plan of the room: a camera straight down from just under the ceiling (north to the right, west at the top)
                    var planCam = new GameObject("R12PlanCam").AddComponent<Camera>();
                    planCam.enabled = false; planCam.orthographic = true; planCam.orthographicSize = 5.6f; planCam.nearClipPlane = 0.05f; planCam.farClipPlane = 8f;
                    planCam.clearFlags = CameraClearFlags.SolidColor; planCam.backgroundColor = new Color(0.08f, 0.08f, 0.1f);
                    planCam.transform.SetPositionAndRotation(new Vector3(4.75f, 3.4f, 14.7f), Quaternion.Euler(90f, 270f, 0f));
                    ShotCam(planCam, "round12_pvp_plan");
                    Destroy(planCam.gameObject);
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
                var parts = body.GetComponentsInChildren<Renderer>(true).Where(r => r && !CameraFollow.IsHeldModel(r.transform, body)).ToList();
                var heldParts = body.GetComponentsInChildren<Renderer>(true).Where(r => r && CameraFollow.IsHeldModel(r.transform, body)).ToList();
                var original = new Dictionary<Renderer, ShadowCastingMode>();
                var originalMats = new Dictionary<Renderer, Material[]>();
                foreach (var r in parts) { original[r] = r.shadowCastingMode; originalMats[r] = r.sharedMaterials; }
                const string ArmsShader = "BuildingEscape/FirstPersonArms";
                bool IsArms(Renderer r) => r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.shader && m.shader.name == ArmsShader);
                rig.ToggleView(); yield return new WaitForSeconds(0.9f);
                float eyes = Vector3.Distance(rig.transform.position, body.position + Vector3.up * rig.eyeHeight);
                bool armsOnly = parts.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || (rig.ArmsShown && IsArms(r))) && (!rig.ArmsShown || parts.Any(IsArms));
                R12Check("P: first person - the camera is at the eyes and the body is out of the picture but for its two forearms (the arms shader on the body's meshes, the whole body still casts its shadow); what is in the hands stays whole (the owner, 2026-10-20)",
                    rig.IsFirstPerson && eyes < 0.2f && parts.Count > 0 && armsOnly && heldParts.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !IsArms(r)), $"(eyes {eyes:0.00} m off, arms shown {rig.ArmsShown}, {parts.Count} body parts, {heldParts.Count} parts in the hands)");
                if (gfx) ShotCam(Camera.main, "round12_first_person");

                // with a gun in the hands: the gun is whole, the body is only its arms (the owner, 2026-10-20: "When in first person with a weapon the character is somewhat visible blocking the screen")
                var smg = RangedWeapon.Create(RangedWeapon.Kind.Smg, player.transform.position + Vector3.up * 0.5f, null, 0f, 2).GetComponent<Pickup>();
                pack.TryStore(smg); yield return new WaitForSeconds(0.3f);
                pack.SelectSlot(2); yield return new WaitForSeconds(1.2f);
                var gunNow = player.GetComponent<GunController>();
                var heldGun = gunNow ? gunNow.Held : null;
                var gunParts = heldGun ? heldGun.Grip.GetComponentsInChildren<Renderer>(true).ToList() : new List<Renderer>();
                var bodyNow = body.GetComponentsInChildren<Renderer>(true).Where(r => r && !CameraFollow.IsHeldModel(r.transform, body) && r.enabled && r.gameObject.activeInHierarchy).ToList();
                R12Check("first person with the SMG in the hands: the gun is whole (every part drawn, none of it the arms shader), every other part of the body is hidden or only its arms - the character no longer blocks the screen",
                    pack.Equipped == smg && heldGun != null && gunParts.Count > 3 && gunParts.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !IsArms(r)) && bodyNow.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || IsArms(r)),
                    $"(gun parts {gunParts.Count}, other parts {bodyNow.Count}, arms shown {rig.ArmsShown}, equipped {(pack.Equipped ? pack.Equipped.Label : "nothing")})");
                if (gfx) ShotCam(Camera.main, "round12_first_person_gun");
                pack.SelectSlot(0); yield return new WaitForSeconds(0.4f);
                rig.ToggleView(); yield return new WaitForSeconds(0.9f);
                R12Check("P again: back to third person, the body is back as it was (its shadow mode and its own materials)", !rig.IsFirstPerson && parts.All(r => !r || (r.shadowCastingMode == original[r] && r.sharedMaterials.SequenceEqual(originalMats[r]))));
            }

            Log($"[R12] DONE fails={r12Fails}");
            Quit(r12Fails == 0 ? 0 : 1);
        }
    }
}
