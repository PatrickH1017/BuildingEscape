# The planner's carry check did not know the walls round the room (the maze's cell walls are not in layout.walls): a carrier "standing" on the far side of the room's edge counted as free room. The room's bounds are walls now.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if (-not $s.Contains('EdgeClearance')) {
Rep @'
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            if (slot.host != null) return SideFree(layout, slot, 1f, 0f, ahead, need) || SideFree(layout, slot, 0f, 1f, ahead, need) || SideFree(layout, slot, -1f, 0f, ahead, need) || SideFree(layout, slot, 0f, -1f, ahead, need);
            // on the floor each carrier needs a spot of their own, on opposite sides: room for one person to stand and lift (<see cref="StandRadius"/>), not the room two people need side by side
            float stand = StandRadius(lifters);
            bool east = SideFree(layout, slot, 1f, 0f, ahead, stand), north = SideFree(layout, slot, 0f, 1f, ahead, stand);
            bool west = SideFree(layout, slot, -1f, 0f, ahead, stand), south = SideFree(layout, slot, 0f, -1f, ahead, stand);
            return (east && west) || (north && south);
        }
'@ @'
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            var room = RoomOf(layout, slot.x, slot.z);
            if (slot.host != null) return SideFree(layout, slot, 1f, 0f, ahead, need, room) || SideFree(layout, slot, 0f, 1f, ahead, need, room) || SideFree(layout, slot, -1f, 0f, ahead, need, room) || SideFree(layout, slot, 0f, -1f, ahead, need, room);
            // on the floor each carrier needs a spot of their own, on opposite sides: room for one person to stand and lift (<see cref="StandRadius"/>), not the room two people need side by side
            float stand = StandRadius(lifters);
            bool east = SideFree(layout, slot, 1f, 0f, ahead, stand, room), north = SideFree(layout, slot, 0f, 1f, ahead, stand, room);
            bool west = SideFree(layout, slot, -1f, 0f, ahead, stand, room), south = SideFree(layout, slot, 0f, -1f, ahead, stand, room);
            return (east && west) || (north && south);
        }

        /// <summary>The smallest room whose bounds hold this point, or null (a plan made of nothing but furniture).</summary>
        static RoomInfo RoomOf(Layout layout, float x, float z)
        {
            RoomInfo best = null; float area = float.MaxValue;
            foreach (var r in layout.rooms)
                if (r.bounds.Contains(x, z) && r.bounds.W * r.bounds.D < area) { best = r; area = r.bounds.W * r.bounds.D; }
            return best;
        }

        /// <summary>How far a point is from the edge of a room (negative outside it): the maze's cell walls stand at the edge and are not in <see cref="Layout.walls"/>, so the edge itself has to count as a wall.</summary>
        static float EdgeClearance(RoomInfo room, float x, float z) =>
            Math.Min(Math.Min(x - room.bounds.x0, room.bounds.x1 - x), Math.Min(z - room.bounds.z0, room.bounds.z1 - z));
'@
Rep @'
        static bool SideFree(Layout layout, Slot slot, float fx, float fz, float ahead, float need) =>
            FreeRadius(layout, slot.x + fx * ahead, slot.z + fz * ahead, need, slot.host) >= need;
'@ @'
        static bool SideFree(Layout layout, Slot slot, float fx, float fz, float ahead, float need, RoomInfo room = null)
        {
            float px = slot.x + fx * ahead, pz = slot.z + fz * ahead;
            if (room != null && EdgeClearance(room, px, pz) < need) return false;                      // (the spot is at, or beyond, the wall round the room)
            return FreeRadius(layout, px, pz, need, slot.host) >= need;
        }
'@
Rep '            RoomInfo room = null; float roomArea = float.MaxValue;
            foreach (var r in layout.rooms)
                if (r.bounds.Contains(slot.x, slot.z) && r.bounds.W * r.bounds.D < roomArea) { room = r; roomArea = r.bounds.W * r.bounds.D; }
            if (room == null) return false;' '            var room = RoomOf(layout, slot.x, slot.z);
            if (room == null) return false;'
Save-Src
}

# ---- the pure test: a treasure near the edge of its room has no room to stand beyond the edge
$p = "$PSScriptRoot\Asks1021Test.cs.txt"
Open-Src $p
if (-not $s.Contains('edgeRoom')) {
Rep '            // a room that is only a niche (nowhere with room on two sides): it is left out, not squeezed in' @'
            // the wall round the room (the maze's cell walls are not in the plan's walls): a treasure near the edge of its room has nobody who can stand beyond it
            var edge = new Layout();
            var edgeRoom = new RoomInfo { theme = "office", bounds = new Rc(0f, 0f, 6f, 6f) };
            edge.rooms.Add(edgeRoom);
            Check("carry both sides: a treasure 1.4 m from the east wall of its room, with free floor all round it inside, can be carried from both sides along the room", LootPlan.RoomForCarry(edge, new Slot { x = 3.0f, y = 0f, z = 3.0f, kind = "server", room = "r" }, 2));
            Check("carry both sides: ... one 0.6 m from the east wall has no spot to stand east of it (that is the wall), so its two sides are not both open on that axis, and on the other axis it is fine", LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 3.0f, kind = "server", room = "r" }, 2));
            Check("carry both sides: ... in a corner of the room (walls east and north) it is refused: only two adjacent sides are open", !LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 2) && !LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 3));
            // a room that is only a niche (nowhere with room on two sides): it is left out, not squeezed in
'@
Save-Src
}
$t = [IO.File]::ReadAllText($p).Replace('public partial class NetDevTest', 'public static partial class NetDevTest')
[IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\NetDevAsks1021Test.cs", $t.Replace("`r`n","`n").Replace("`n","`r`n"), (New-Object Text.UTF8Encoding($false)))
"p12 done"
