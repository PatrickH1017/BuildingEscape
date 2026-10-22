using System.Collections.Generic;
using System.Linq;
using BuildingEscape.Net;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The pure checks of the owner's list of 2026-10-21 (<c>build.ps1</c> runs them with the rest): the click latch of the gun, the carry rule (a treasure on the floor needs room on two opposite sides, and moves to a spot that has it), the ping rules.
    /// The Play Mode half is the scenario <c>Asks1021</c> (<c>NetDevAsks1021.cs</c>).
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1021Test()
        {
            TriggerLatchTest();
            CarryBothSidesTest();
            PingRulesTest();
        }

        // ------------------------------------------------------------------ the ping

        static void PingRulesTest()
        {
            Check("ping: a marker is up for 8 seconds - in at once, steady, out over the last 1.2 seconds", PingRules.Seconds == 8f && PingRules.Alpha(-1f) == 0f && PingRules.Alpha(0.5f) == 1f && PingRules.Alpha(6f) == 1f && PingRules.Alpha(7.4f) > 0.3f && PingRules.Alpha(7.4f) < 0.7f && PingRules.Alpha(8f) == 0f && PingRules.Alpha(9f) == 0f);
            Check("ping: a marker pops in a little over full size and settles at full size", PingRules.Pop(0f) == 0f && PingRules.Pop(0.001f) < 0.2f && PingRules.Pop(PingRules.PopSeconds * 0.5f) > 0.5f && PingRules.Pop(PingRules.PopSeconds * 0.75f) > 1f && PingRules.Pop(PingRules.PopSeconds) == 1f && PingRules.Pop(5f) == 1f);
            Check("ping: a weapon or armour is gear, a spare clip or a thing worth under $50 is used up, a thing worth $50 or more is loot (the owner: lootable items, consumable items, weapons)",
                PingRules.CategoryOf(true, false, false, 0) == PingCategory.Gear && PingRules.CategoryOf(false, true, false, 900) == PingCategory.Gear && PingRules.CategoryOf(false, false, true, 0) == PingCategory.Consumable
                && PingRules.CategoryOf(false, false, false, 25) == PingCategory.Consumable && PingRules.CategoryOf(false, false, false, 50) == PingCategory.Loot && PingRules.CategoryOf(false, false, false, 2500) == PingCategory.Loot);
            var here = new Vector3(10f, 0f, 10f);
            Check("ping: the host takes a ping within 80 m (and a little more: the sender's view is a moment old) and refuses one further away, or at nowhere",
                PingRules.HostAccepts(here, here + new Vector3(30f, 0f, 0f)) && PingRules.HostAccepts(here, here + new Vector3(85f, 0f, 0f)) && !PingRules.HostAccepts(here, here + new Vector3(95f, 0f, 0f))
                && !PingRules.HostAccepts(here, new Vector3(float.NaN, 0f, 0f)) && !PingRules.HostAccepts(here, new Vector3(0f, float.PositiveInfinity, 0f)));
            Check("ping: a door is Door (Locked door when it is locked), an item has its own name, a nameless one is Item",
                PingRules.NameOf(null, PingCategory.Door) == "Door" && PingRules.NameOf(null, PingCategory.Door, true) == "Locked door" && PingRules.NameOf("Gold Watch", PingCategory.Loot) == "Gold Watch" && PingRules.NameOf("  ", PingCategory.Loot) == "Item");
            Check("ping: loot shows what it is worth and how many carry it, gear and used-up things only their name",
                PingRules.NameOf("Gold Watch", PingCategory.Loot, false, 700) == "Gold Watch  $700" && PingRules.NameOf("Prototype Server", PingCategory.Loot, false, 2500, 2) == "Prototype Server  $2,500  (x2 carry)"
                && PingRules.NameOf("Pew", PingCategory.Gear, false, 900) == "Pew" && PingRules.NameOf("Mug", PingCategory.Consumable, false, 25) == "Mug" && PingRules.NameOf("Gold Watch", PingCategory.Loot) == "Gold Watch");
            Check("ping: the signs in the marker are $ loot, ! gear, + something used up, D door", PingRules.Sign(PingCategory.Loot) == "$" && PingRules.Sign(PingCategory.Gear) == "!" && PingRules.Sign(PingCategory.Consumable) == "+" && PingRules.Sign(PingCategory.Door) == "D");
            Check("ping: it is the middle mouse button (the click of the scroll wheel), half a second between pings", PingRules.MouseButton == 2 && PingRules.Cooldown == 0.5f);
            var clip = OutpostSound.Ping(0);
            float peak = 0f; bool finite = true;
            foreach (var v in clip) { if (float.IsNaN(v) || float.IsInfinity(v)) finite = false; peak = Mathf.Max(peak, Mathf.Abs(v)); }
            float a5 = Goertzel(clip, 880f), e6 = Goertzel(clip, 1318.5f), off = Goertzel(clip, 600f) + Goertzel(clip, 2000f);
            Check($"ping: the sound is a real clip - about a third of a second, not clipping, two notes (A5 then E6) and little else (A5 {a5:0.0}, E6 {e6:0.0}, other {off:0.0})",
                finite && clip.Length > Dsp.SampleRate * 0.25f && clip.Length < Dsp.SampleRate * 0.5f && peak > 0.3f && peak <= 1f && a5 > off && e6 > off * 0.8f && SoundBank.Variants(SoundId.Ping) == 1);
            bool listed = false;
            for (int g = 0; g < KeybindScreen.GroupCount; g++) for (int i = 0; i < KeybindScreen.EntryCount(g); i++) if (KeybindScreen.KeyAt(g, i) == "MMB") listed = KeybindScreen.LabelAt(g, i).Contains("ping");
            bool howTo = false;
            foreach (var c in HowToPlayContent.Controls) if (c.key.StartsWith("Middle mouse") && c.action.Contains("Ping")) howTo = true;
            Check("ping: the Keybinds screen and the How to Play page list it", listed && howTo);
            Check("ping: a new message, so the protocol was raised (27 or later)", int.Parse(NetConst.ProtocolVersion) >= 27);
        }

        // ------------------------------------------------------------------ the gun ignores a click that began in a menu

        static void TriggerLatchTest()
        {
            var t = new TriggerLatch();
            // the elevator panel: the button goes down on GO (the gun cannot be used), the panel closes, the button is still held for a few frames
            bool shot = false;
            shot |= t.Pull(false, true);     // the click lands while the panel is up
            shot |= t.Pull(true, true);      // the panel has closed on it: the gun is usable again, the button is still down
            shot |= t.Pull(true, true);
            Check("click latch: a click that begins while the gun cannot be used (the elevator panel's GO) never becomes a shot, however long the button stays down", !shot && t.Latched);
            Check("click latch: ... and the button has to be let go of first", !t.Pull(true, false) && !t.Latched);
            Check("click latch: ... after that a new press fires, and a held button keeps firing (full-auto guns)", t.Pull(true, true) && t.Pull(true, true) && t.Pull(true, true));
            var u = new TriggerLatch();
            Check("click latch: a press while the gun is usable fires at once", u.Pull(true, true));
            u.Pull(false, true);             // a menu opens over a held button...
            Check("click latch: ... and does not fire when it closes while the button is still down", !u.Pull(true, true));
            Check("click latch: nothing is held, nothing fires", !new TriggerLatch().Pull(true, false) && !new TriggerLatch().Pull(false, false));
            var v = new TriggerLatch();
            v.Pull(false, true); v.Reset();
            Check("click latch: reset opens it", v.Pull(true, true));
        }

        // ------------------------------------------------------------------ a treasure for two on the floor needs both sides

        static Piece Rack(float x0, float z0, float x1, float z1, float h = 2f) => new Piece { rc = new Rc(x0, z0, x1, z1), h = h, solid = true };

        static Layout ServerRoom(out RoomInfo room, float w = 3.4f, float d = 5.5f)
        {
            // a made-up server room: racks along the west, east and north walls (0.9 m deep), the door on the south side
            var l = new Layout();
            room = new RoomInfo { theme = "server", bounds = new Rc(0f, 0f, w, d) };
            l.rooms.Add(room);
            l.pieces.Add(Rack(0f, 0.6f, 0.9f, d - 0.3f));
            l.pieces.Add(Rack(w - 0.9f, 0.6f, w, d - 0.3f));
            l.pieces.Add(Rack(0.9f, d - 0.9f, w - 0.9f, d));
            room.doors.Add(new Rc(w * 0.5f - 0.6f, -0.2f, w * 0.5f + 0.6f, 0.2f));
            return l;
        }

        /// <summary>The rule as it was until 2026-10-21 (room on ONE side was enough, and the walls round the room did not count): for comparing how many treasures each keeps.</summary>
        static bool OldRoomForCarry(Layout layout, Slot slot, int lifters)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && LootPlan.FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            for (int k = 0; k < 4; k++)
            {
                float fx = k == 0 ? 1f : k == 2 ? -1f : 0f, fz = k == 1 ? 1f : k == 3 ? -1f : 0f;
                if (LootPlan.FreeRadius(layout, slot.x + fx * ahead, slot.z + fz * ahead, need, slot.host) >= need) return true;
            }
            return false;
        }

        static void CarryBothSidesTest()
        {
            // the picture the owner sent: the prototype server in the narrow aisle between two racks, the racks at its left and right, the end wall behind it: only the front is open
            var l = ServerRoom(out var room);
            var wedged = new Slot { x = 1.7f, y = 0f, z = 3.55f, kind = "server", room = "r" };
            Check("carry both sides: a server wedged between two racks with a rack behind it and one open end is refused for two (the owner's picture)", !LootPlan.RoomForCarry(l, wedged, 2));
            Check("carry both sides: ... and for one carrier it is never refused", LootPlan.RoomForCarry(l, wedged, 1));
            // the same room, the server in the middle of the aisle: room in front and behind it, but the aisle is only a little wider than the box, so nobody can walk past it to the far end (a dead end behind it)
            var aisle = new Slot { x = 1.7f, y = 0f, z = 2.1f, kind = "server", room = "r" };
            Check("carry both sides: in the middle of that narrow aisle there is room in front and behind the server, but it fills the aisle and nobody can walk round it to the far side (a dead end behind it): refused", !LootPlan.RoomForCarry(l, aisle, 2, 1.0f, 0.9f));
            // a wide room: the same racks along the walls, an aisle four metres wide: the server in the middle of it can be walked round
            var wide = ServerRoom(out var wideRoom, 6.0f, 6.5f);
            Check("carry both sides: in a room with a wide aisle the same server can be walked round and carried from both ends", LootPlan.RoomForCarry(wide, new Slot { x = 3.0f, y = 0f, z = 3.0f, kind = "server", room = "r" }, 2, 1.0f, 0.9f));
            // an empty floor: fine for two and for three
            var empty = new Layout();
            var open = new Slot { x = 20f, y = 0f, z = 20f, kind = "server", room = "r" };
            Check("carry both sides: on an empty floor there is room on every side for two or three", LootPlan.RoomForCarry(empty, open, 2) && LootPlan.RoomForCarry(empty, open, 3));
            // a treasure in a corner has two adjacent sides open, not two opposite ones
            var corner = new Layout();
            corner.walls.Add(new WallBox { rc = new Rc(19.1f, 18f, 19.3f, 22f), y0 = 0f, y1 = 3.5f });      // a wall 0.7 m west of its middle
            corner.walls.Add(new WallBox { rc = new Rc(18f, 19.1f, 22f, 19.3f), y0 = 0f, y1 = 3.5f });      // and one 0.7 m south of it
            var inCorner = new Slot { x = 20f, y = 0f, z = 20f, kind = "safe", room = "r" };
            Check("carry both sides: a treasure in a corner has two adjacent sides open: not enough for two carriers (they stand opposite each other), enough for three (a thing for three needs two sides, any two)", !LootPlan.RoomForCarry(corner, inCorner, 2) && LootPlan.RoomForCarry(corner, inCorner, 3));
            // one pair of opposite sides is enough: a treasure with a wall behind it and free room to its left and right
            var wallBehind = new Layout();
            wallBehind.walls.Add(new WallBox { rc = new Rc(15f, 20.9f, 25f, 21.1f), y0 = 0f, y1 = 3.5f });
            Check("carry both sides: with a wall behind it and free room to its left and right, two carriers can stand one on each side", LootPlan.RoomForCarry(wallBehind, new Slot { x = 20f, y = 0f, z = 20f, kind = "server", room = "r" }, 2));
            // on a counter the carriers stand side by side in front of it: one side is enough, as before
            var counter = new Piece { x = 20f, z = 20f, rc = new Rc(19.5f, 19.7f, 20.5f, 20.3f), h = 0.95f, solid = true, yaw = 180f };
            var counterRoom = new Layout(); counterRoom.pieces.Add(counter);
            counterRoom.walls.Add(new WallBox { rc = new Rc(18f, 20.3f, 22f, 20.5f), y0 = 0f, y1 = 3.5f });
            Check("carry both sides: a machine on a counter with free floor in front of it can still be carried by two (one side is enough there)", LootPlan.RoomForCarry(counterRoom, new Slot { x = 20f, y = 0.95f, z = 20f, kind = "espresso", host = counter, room = "r" }, 2));

            // a server in a pocket of a wide room (a rack east of it, the racks north of it) is moved to the nearest spot of its room where the carriers can stand on opposite sides and walk to them
            var pocket = ServerRoom(out var pocketRoom, 6.0f, 6.5f);
            pocket.pieces.Add(Rack(3.6f, 4.3f, 4.6f, 5.6f));
            var stuck = new Slot { x = 3.0f, y = 0f, z = 5.0f, kind = "server", room = "r" };
            Check("carry both sides: a server in a pocket (a rack beside it and the racks behind it) is refused for two", !LootPlan.RoomForCarry(pocket, stuck, 2, 1.0f, 0.9f));
            bool moved = LootPlan.TryFindCarrySpot(pocket, stuck, 2, 1.0f, 0.9f, out float mx, out float mz);
            var movedSlot = new Slot { x = mx, y = 0f, z = mz, kind = "server", room = "r" };
            bool insideRoom = pocketRoom.bounds.Grow(-0.4f).Contains(mx, mz);
            bool clearOfRacks = !pocket.pieces.Any(p => p.rc.Overlaps(new Rc(mx - 0.5f, mz - 0.45f, mx + 0.5f, mz + 0.45f)));
            Check($"carry both sides: ... it is moved to a spot of its room where two carriers can stand on opposite sides and walk to them ({mx:0.00}, {mz:0.00})", moved && LootPlan.RoomForCarry(pocket, movedSlot, 2, 1.0f, 0.9f) && insideRoom && clearOfRacks);
            Check("carry both sides: ... the spot is in the same room, clear of the racks, and the same answer every time (no randomness)", moved && LootPlan.TryFindCarrySpot(pocket, stuck, 2, 1.0f, 0.9f, out float mx2, out float mz2) && mx2 == mx && mz2 == mz);
            Check("carry both sides: nothing is moved that can be left (a thing for one, a thing on a counter)", !LootPlan.TryFindCarrySpot(l, wedged, 1, 1.0f, 0.9f, out _, out _) && !LootPlan.TryFindCarrySpot(counterRoom, new Slot { x = 20f, y = 0.95f, z = 20f, host = counter }, 2, 0.9f, 0.8f, out _, out _));
            Check("carry both sides: in the narrow aisle room there is nowhere a server can stand with both ends reachable, so it is left out, not squeezed in", !LootPlan.TryFindCarrySpot(l, wedged, 2, 1.0f, 0.9f, out _, out _));
            // the wall round the room (the maze's cell walls are not in the plan's walls): a treasure near the edge of its room has nobody who can stand beyond it
            var edge = new Layout();
            var edgeRoom = new RoomInfo { theme = "office", bounds = new Rc(0f, 0f, 6f, 6f) };
            edge.rooms.Add(edgeRoom);
            Check("carry both sides: a treasure 1.4 m from the east wall of its room, with free floor all round it inside, can be carried from both sides along the room", LootPlan.RoomForCarry(edge, new Slot { x = 3.0f, y = 0f, z = 3.0f, kind = "server", room = "r" }, 2));
            Check("carry both sides: ... one 0.6 m from the east wall has no spot to stand east of it (that is the wall), so its two sides are not both open on that axis, and on the other axis it is fine", LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 3.0f, kind = "server", room = "r" }, 2));
            Check("carry both sides: ... in a corner of the room (walls east and north) a thing for two is refused (only two adjacent sides are open), a thing for three is fine", !LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 2) && LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 3));
            // a room that is only a niche (nowhere with room on two sides): it is left out, not squeezed in
            var tiny = new Layout();
            var tinyRoom = new RoomInfo { theme = "server", bounds = new Rc(0f, 0f, 1.6f, 1.6f) };
            tiny.rooms.Add(tinyRoom);
            foreach (var tinyWalls in new[] { new Rc(-0.2f, -0.2f, 1.8f, 0f), new Rc(-0.2f, 1.6f, 1.8f, 1.8f), new Rc(-0.2f, 0f, 0f, 1.6f), new Rc(1.6f, 0f, 1.8f, 1.6f) }) tiny.walls.Add(new WallBox { rc = tinyWalls, y0 = 0f, y1 = 3.5f });
            Check("carry both sides: in a room with no spot that has room on two sides the treasure is left out", !LootPlan.TryFindCarrySpot(tiny, new Slot { x = 0.8f, z = 0.8f, kind = "server", room = "r" }, 2, 1.0f, 0.9f, out _, out _));

            // on real floors: what the rule leaves, what it moves, and that every treasure that is kept (as planned or moved) can be reached from two opposite sides
            const int cols = 8, rows = 5;
            int heavyFloor = 0, passedAsPlanned = 0, rescued = 0, lost = 0, badMoves = 0, oldKept = 0;
            int srvPlanned = 0, srvMoved = 0, srvLost = 0, safePlanned = 0, safeMoved = 0, safeLost = 0;
            foreach (var (floor, seed) in new[] { (1, 4101), (2, 777), (3, 1234), (4, 9172), (6, 31337), (9, 2288), (12, 515), (1, 7), (2, 8675309), (4, 424242), (5, 99), (7, 5150), (8, 24680), (10, 13579), (11, 112233) })
            {
                var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
                var layout = OfficePlanner.Build(shape.PlanFor(floor, seed, 18f, 3.5f, 777));
                foreach (var s in layout.bigSlots)
                {
                    int lifters = s.kind == "safe" ? 3 : s.kind == "espresso" || s.kind == "server" ? 2 : 1;
                    if (lifters < 2 || s.host != null) continue;
                    heavyFloor++;
                    if (OldRoomForCarry(layout, s, lifters)) oldKept++;
                    var size = s.kind == "safe" ? new Vector2(1.1f, 1.1f) : new Vector2(1.0f, 0.9f);
                    bool isSafe = s.kind == "safe";
                    if (LootPlan.RoomForCarry(layout, s, lifters, size.x, size.y)) { passedAsPlanned++; if (isSafe) safePlanned++; else srvPlanned++; continue; }
                    if (!LootPlan.TryFindCarrySpot(layout, s, lifters, size.x, size.y, out float nx, out float nz)) { lost++; if (isSafe) safeLost++; else srvLost++; continue; }
                    rescued++; if (isSafe) safeMoved++; else srvMoved++;
                    var ns = new Slot { x = nx, y = s.y, z = nz, yaw = s.yaw, kind = s.kind, room = s.room };
                    var foot = new Rc(nx - size.x * 0.5f, nz - size.y * 0.5f, nx + size.x * 0.5f, nz + size.y * 0.5f);
                    if (!LootPlan.RoomForCarry(layout, ns, lifters, size.x, size.y) || layout.pieces.Any(p => p.solid && !p.onTop && !p.mount && p.h >= 0.35f && p.rc.Overlaps(foot))) badMoves++;
                }
            }
            Debug.Log($"[NetTest] carry both sides: {heavyFloor} treasures for two or three on the floor in 15 floors: {passedAsPlanned} fine as planned, {rescued} moved to a spot with room on two sides, {lost} left out (the rule of before, one open side, would have kept {oldKept}); servers {srvPlanned} as planned / {srvMoved} moved / {srvLost} left out, safes {safePlanned} / {safeMoved} / {safeLost}");
            Check("carry both sides: on the real floors every treasure that is kept can be reached from two opposite sides and a moved one stands clear of the furniture", badMoves == 0, $"({badMoves} bad)");
            Check("carry both sides: on the real floors the rule is neither empty nor blind: some are fine as planned, some are moved, and a floor keeps at least half of what the rule of before kept (that rule did not see the walls, nor that the far side had to be reachable)", heavyFloor > 0 && passedAsPlanned > 0 && rescued > 0 && (passedAsPlanned + rescued) * 2 >= oldKept, $"({passedAsPlanned} as planned, {rescued} moved, {lost} lost of {heavyFloor}; before {oldKept})");
        }
    }
}
