# TEMPORARY diagnosis (removed again): why does no planned treasure spot pass on the real floors?
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if ($s.Contains('DiagCarry')) { "already"; return }
Rep '        /// <summary>Is there room to stand and lift on one side of the treasure' @'
        /// <summary>TEMPORARY diagnosis.</summary>
        public static string DiagCarry(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ)
        {
            float need = lifters >= 3 ? 0.95f : 0.80f, ahead = 1.0f, stand = StandRadius(lifters);
            var room = RoomOf(layout, slot.x, slot.z);
            var grid = room != null ? new WalkGrid(layout, room) : null;
            float fr = FreeRadius(layout, slot.x, slot.z, need);
            bool e = SideFree(layout, slot, 1f, 0f, ahead, stand, room), n = SideFree(layout, slot, 0f, 1f, ahead, stand, room), w = SideFree(layout, slot, -1f, 0f, ahead, stand, room), so = SideFree(layout, slot, 0f, -1f, ahead, stand, room);
            string reach = "no grid";
            if (grid != null)
            {
                var seen = grid.Flood(new Rc(slot.x - sizeX * 0.5f, slot.z - sizeZ * 0.5f, slot.x + sizeX * 0.5f, slot.z + sizeZ * 0.5f));
                reach = $"seeds {(grid.HasSeeds ? "yes" : "NO")} reach E{(grid.Reached(seen, slot.x + ahead, slot.z) ? 1 : 0)} N{(grid.Reached(seen, slot.x, slot.z + ahead) ? 1 : 0)} W{(grid.Reached(seen, slot.x - ahead, slot.z) ? 1 : 0)} S{(grid.Reached(seen, slot.x, slot.z - ahead) ? 1 : 0)}";
            }
            return $"{slot.kind} at ({slot.x:0.0},{slot.z:0.0}) yaw {slot.yaw:0} room {(room != null ? room.theme + $" {room.bounds.W:0.0}x{room.bounds.D:0.0}" : "none")} wedge {fr:0.00} (needs {need * 0.85f:0.00}) sides E{(e ? 1 : 0)} N{(n ? 1 : 0)} W{(w ? 1 : 0)} S{(so ? 1 : 0)} {reach}";
        }

        /// <summary>Is there room to stand and lift on one side of the treasure
'@
Save-Src
$p = "$PSScriptRoot\Asks1021Test.cs.txt"
Open-Src $p
if (-not $s.Contains('DiagCarry')) {
Rep '                    heavyFloor++;' @'
                    heavyFloor++;
                    if (heavyFloor <= 24) Debug.Log("[NetTest] carry diag: " + LootPlan.DiagCarry(layout, s, lifters, s.kind == "safe" ? 1.1f : 1.0f, s.kind == "safe" ? 1.1f : 0.9f));
'@
Save-Src
}
$t = [IO.File]::ReadAllText($p).Replace('public partial class NetDevTest', 'public static partial class NetDevTest')
[IO.File]::WriteAllText("$Tree\Assets\Editor\NetDev\NetDevAsks1021Test.cs", $t.Replace("`r`n","`n").Replace("`n","`r`n"), (New-Object Text.UTF8Encoding($false)))
"diag installed"
