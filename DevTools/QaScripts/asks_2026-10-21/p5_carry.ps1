# 2026-10-21, the owner's list: "In the screenshot attached there is a object that two people can pick up together but it is not accessible from both sides."
# The prototype server (two carriers) stood in the aisle between two racks of the server room, open at one end only. RoomForCarry asked for room on ONE side; a treasure on the floor needs two opposite sides now (one carrier each),
# and one that does not have them where the planner put it moves to the nearest spot of its room that does.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if (-not $s.Contains('TryFindCarrySpot')) {
Rep @'
        /// <summary>
        /// Can the people who must carry a treasure of <paramref name="lifters"/> hands get at it and walk it out? A thing for one needs nothing. For two or
        /// three there must be free room to stand and lift on at least one side of it - a disc 0.8 m (0.95 m for three) wide round a point 1 m (1.15 m in front
        /// of a counter) from it - and a thing standing on the floor must not be wedged in. The owner, 2026-10-17: "Carrying loot together was glitchy because a lot of items
        /// where in places where two players couldn't fit together while carrying".
        /// </summary>
        public static bool RoomForCarry(Layout layout, Slot slot, int lifters)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            for (int k = 0; k < 4; k++)
            {
                float fx = k == 0 ? 1f : k == 2 ? -1f : 0f, fz = k == 1 ? 1f : k == 3 ? -1f : 0f;
                if (FreeRadius(layout, slot.x + fx * ahead, slot.z + fz * ahead, need, slot.host) >= need) return true;
            }
            return false;
        }
'@ @'
        /// <summary>
        /// Can the people who must carry a treasure of <paramref name="lifters"/> hands get at it and walk it out? A thing for one needs nothing. For two or
        /// three there must be free room to stand and lift - a disc 0.8 m (0.95 m for three) wide round a point 1 m (1.15 m in front
        /// of a counter) from it - and a thing standing on the floor must not be wedged in. The owner, 2026-10-17: "Carrying loot together was glitchy because a lot of items
        /// where in places where two players couldn't fit together while carrying". Since 2026-10-21 a thing standing on the FLOOR needs that room on two OPPOSITE sides, one carrier on each (the owner: "a object that two people can pick up together
        /// but it is not accessible from both sides": the prototype server stood in the aisle between two racks, open at one end only); a thing on a counter needs it on one side (its carriers stand side by side in front of it).
        /// </summary>
        public static bool RoomForCarry(Layout layout, Slot slot, int lifters)
        {
            if (lifters <= 1) return true;
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = slot.host != null ? 1.15f : 1.0f;
            if (slot.host == null && FreeRadius(layout, slot.x, slot.z, need) < need * 0.85f) return false;
            bool east = SideFree(layout, slot, 1f, 0f, ahead, need), north = SideFree(layout, slot, 0f, 1f, ahead, need);
            bool west = SideFree(layout, slot, -1f, 0f, ahead, need), south = SideFree(layout, slot, 0f, -1f, ahead, need);
            if (slot.host != null) return east || north || west || south;
            return (east && west) || (north && south);
        }

        /// <summary>Is there room to stand and lift on one side of the treasure (a free disc of radius <paramref name="need"/> a distance <paramref name="ahead"/> from its middle, in the direction (fx, fz))?</summary>
        static bool SideFree(Layout layout, Slot slot, float fx, float fz, float ahead, float need) =>
            FreeRadius(layout, slot.x + fx * ahead, slot.z + fz * ahead, need, slot.host) >= need;

        /// <summary>
        /// A treasure on the floor that cannot be reached from two opposite sides where the planner put it moves to the nearest spot of its own room where it can: the box of its size (grown a little) clear of furniture and walls, a metre clear of every doorway
        /// of the room and of the guards' standing places and of the rectangles that must stay walkable, and <see cref="RoomForCarry"/> true there. A grid of 0.25 m steps up to 3.5 m from the planned spot, the nearest first (the first found
        /// wins a tie); nothing random, so every machine finds the same spot. False when there is none (the treasure is then left out). Only for a thing for two or three on the floor.
        /// </summary>
        public static bool TryFindCarrySpot(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, out float x, out float z)
        {
            x = slot.x; z = slot.z;
            if (lifters <= 1 || slot.host != null) return false;
            RoomInfo room = null; float roomArea = float.MaxValue;
            foreach (var r in layout.rooms)
                if (r.bounds.Contains(slot.x, slot.z) && r.bounds.W * r.bounds.D < roomArea) { room = r; roomArea = r.bounds.W * r.bounds.D; }
            if (room == null) return false;
            float hx = sizeX * 0.5f + 0.06f, hz = sizeZ * 0.5f + 0.06f;
            const float step = 0.25f, reach = 3.5f;
            int n = (int)(reach / step);
            int best = int.MaxValue; bool found = false;
            for (int iz = -n; iz <= n; iz++)
                for (int ix = -n; ix <= n; ix++)
                {
                    int dd = ix * ix + iz * iz;
                    if (dd >= best || dd > n * n) continue;
                    float cx = slot.x + ix * step, cz = slot.z + iz * step;
                    var foot = new Rc(cx - hx, cz - hz, cx + hx, cz + hz);
                    if (foot.x0 < room.bounds.x0 + 0.05f || foot.x1 > room.bounds.x1 - 0.05f || foot.z0 < room.bounds.z0 + 0.05f || foot.z1 > room.bounds.z1 - 0.05f) continue;
                    if (!FootprintClear(layout, room, foot)) continue;
                    var spot = new Slot { x = cx, y = slot.y, z = cz, yaw = slot.yaw, kind = slot.kind, room = slot.room, host = null };
                    if (!RoomForCarry(layout, spot, lifters)) continue;
                    best = dd; x = cx; z = cz; found = true;
                }
            return found;
        }

        /// <summary>Is the box <paramref name="foot"/> free of solid furniture and walls, a metre from the room's doorways and the guards' standing places, and out of the rectangles that must stay walkable?</summary>
        static bool FootprintClear(Layout layout, RoomInfo room, Rc foot)
        {
            foreach (var p in layout.pieces)
                if (p.solid && !p.onTop && !p.mount && p.h >= 0.35f && p.rc.Overlaps(foot)) return false;
            foreach (var w in layout.walls)
                if (w.y0 <= 1.0f && w.y1 >= 0.5f && w.rc.Overlaps(foot)) return false;
            foreach (var d in room.doors)
                if (d.Grow(1.0f).Overlaps(foot)) return false;
            foreach (var c in layout.clear)
                if (c.Overlaps(foot)) return false;
            foreach (var wp in layout.waypoints)
                if (wp != null && wp.Length >= 2 && DistanceTo(foot, wp[0], wp[1]) < 1.0f) return false;
            return true;
        }
'@
Save-Src
}

Open-Src "$Tree\Assets\Scripts\FloorGenerator.Interior.cs"
if (-not $s.Contains('TryFindCarrySpot')) {
Rep '            int want = LootPlan.TreasureCount(floor), placed = 0, cramped = 0;' '            int want = LootPlan.TreasureCount(floor), placed = 0, cramped = 0, moved = 0;'
Rep @'
                    if (!LootPlan.RoomForCarry(layout, s, t.lifters)) { cramped++; continue; }
'@ @'
                    if (!LootPlan.RoomForCarry(layout, s, t.lifters))
                    {
                        // (the owner, 2026-10-21: "a object that two people can pick up together but it is not accessible from both sides") a treasure on the floor that has no room on two opposite sides where the planner put it
                        // goes to the nearest spot of its room that has, and is left out when there is none
                        if (!LootPlan.TryFindCarrySpot(layout, s, t.lifters, t.size.x, t.size.z, out float mx, out float mz)) { cramped++; continue; }
                        s = new Slot { x = mx, y = s.y, z = mz, yaw = s.yaw, kind = s.kind, room = s.room, host = s.host };
                        moved++;
                    }
'@
Rep '            if (cramped > 0) Debug.Log($"[Loot] floor {floor}: {placed} treasures placed, {cramped} spots skipped because two or three people could not carry what stands there");' '            if (cramped > 0 || moved > 0) Debug.Log($"[Loot] floor {floor}: {placed} treasures placed, {moved} moved to where two or three people can reach them from both sides, {cramped} spots skipped because two or three people could not carry what stands there");'
Save-Src
}
"p5 done"
