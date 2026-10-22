# A thing for THREE (the executive safe, which stands in a corner of its office) needs two reachable sides (adjacent ones will do: three people cannot all stand opposite each other); a thing for two needs an opposite pair.
# Measured on 15 real floors with the strict rule for both: 51 of 64 safes were lost. The owner's complaint is about the thing two people carry.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if ($s.Contains('int openSides')) { "already"; return }
Rep @'
            bool alongX = east && west, alongZ = north && south;
            if (!alongX && !alongZ) return false;
            // and a second person must be able to WALK to the far side: with the box in place, both spots must be reachable from where people come into the room
            if (room == null) return true;
            if (grid == null) grid = new WalkGrid(layout, room);
            if (!grid.HasSeeds) return true;                                                                  // (no way in is known: nothing to say about it)
            bool yawed = Math.Abs(Math.Sin(slot.yaw * Math.PI / 180.0)) > 0.5;
            float hw = (yawed ? sizeZ : sizeX) * 0.5f, hd = (yawed ? sizeX : sizeZ) * 0.5f;
            var seen = grid.Flood(new Rc(slot.x - hw, slot.z - hd, slot.x + hw, slot.z + hd));
            if (alongX && grid.Reached(seen, slot.x + ahead, slot.z) && grid.Reached(seen, slot.x - ahead, slot.z)) return true;
            if (alongZ && grid.Reached(seen, slot.x, slot.z + ahead) && grid.Reached(seen, slot.x, slot.z - ahead)) return true;
            return false;
        }
'@ @'
            int openSides = (east ? 1 : 0) + (north ? 1 : 0) + (west ? 1 : 0) + (south ? 1 : 0);
            // two carriers stand opposite each other; three cannot all do that, so a thing for three needs two sides (any two)
            if (lifters >= 3 ? openSides < 2 : !((east && west) || (north && south))) return false;
            // and a second person must be able to WALK to the far side: with the box in place, the spots must be reachable from where people come into the room
            if (room == null) return true;
            if (grid == null) grid = new WalkGrid(layout, room);
            if (!grid.HasSeeds) return true;                                                                  // (no way in is known: nothing to say about it)
            bool yawed = Math.Abs(Math.Sin(slot.yaw * Math.PI / 180.0)) > 0.5;
            float hw = (yawed ? sizeZ : sizeX) * 0.5f, hd = (yawed ? sizeX : sizeZ) * 0.5f;
            var seen = grid.Flood(new Rc(slot.x - hw, slot.z - hd, slot.x + hw, slot.z + hd));
            bool reachE = east && grid.Reached(seen, slot.x + ahead, slot.z), reachN = north && grid.Reached(seen, slot.x, slot.z + ahead);
            bool reachW = west && grid.Reached(seen, slot.x - ahead, slot.z), reachS = south && grid.Reached(seen, slot.x, slot.z - ahead);
            if (lifters >= 3) return (reachE ? 1 : 0) + (reachN ? 1 : 0) + (reachW ? 1 : 0) + (reachS ? 1 : 0) >= 2;
            return (reachE && reachW) || (reachN && reachS);
        }
'@
Save-Src

$p = "$PSScriptRoot\Asks1021Test.cs.txt"
Open-Src $p
if (-not $s.Contains('a thing for three needs two sides')) {
Rep '            Check("carry both sides: a treasure in a corner (two adjacent sides open) cannot be carried from both sides, by two or by three", !LootPlan.RoomForCarry(corner, inCorner, 2) && !LootPlan.RoomForCarry(corner, inCorner, 3));' '            Check("carry both sides: a treasure in a corner has two adjacent sides open: not enough for two carriers (they stand opposite each other), enough for three (a thing for three needs two sides, any two)", !LootPlan.RoomForCarry(corner, inCorner, 2) && LootPlan.RoomForCarry(corner, inCorner, 3));'
Rep '            Check("carry both sides: ... in a corner of the room (walls east and north) it is refused: only two adjacent sides are open", !LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 2) && !LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 3));' '            Check("carry both sides: ... in a corner of the room (walls east and north) a thing for two is refused (only two adjacent sides are open), a thing for three is fine", !LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 2) && LootPlan.RoomForCarry(edge, new Slot { x = 5.4f, y = 0f, z = 5.4f, kind = "safe", room = "r" }, 3));'
Save-Src
}
$t = [IO.File]::ReadAllText($p).Replace('public partial class NetDevTest', 'public static partial class NetDevTest')
[IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\NetDevAsks1021Test.cs", $t.Replace("`r`n","`n").Replace("`n","`r`n"), (New-Object Text.UTF8Encoding($false)))
"p18 done"
