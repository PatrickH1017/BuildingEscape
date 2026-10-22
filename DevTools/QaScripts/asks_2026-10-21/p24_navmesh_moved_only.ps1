# The NavMesh condition (ground an agent can reach within 2 m) only holds for a spot a treasure is MOVED to: a spot the planner chose is left as it was (the LootQa rule tolerates a few nooks), so safes in their corner pockets are not lost for it.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\Interior\LootPlan.cs"
if ($s.Contains('bool onNavMesh')) { "already"; return }
Rep 'public static bool RoomForCarry(Layout layout, Slot slot, int lifters, float sizeX = 1f, float sizeZ = 1f) => RoomForCarryCore(layout, slot, lifters, sizeX, sizeZ, null);' 'public static bool RoomForCarry(Layout layout, Slot slot, int lifters, float sizeX = 1f, float sizeZ = 1f) => RoomForCarryCore(layout, slot, lifters, sizeX, sizeZ, null, false);'
Rep 'static bool RoomForCarryCore(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, WalkGrid grid)' '/// <param name="onNavMesh">For a spot a treasure is moved to: it must also lie where the floor''s NavMesh is (see the end of this method).</param>
        static bool RoomForCarryCore(Layout layout, Slot slot, int lifters, float sizeX, float sizeZ, WalkGrid grid, bool onNavMesh)'
Rep '            // and the treasure must lie where the floor''s NavMesh is (what the guards and the QA call walkable: an agent keeps 0.55 m clear): open ground that an agent can reach within 2 m of it, as the loot test asks
            return grid.Near(grid.Flood(box, WalkGrid.AgentRadius), slot.x, slot.z, 2.0f);' '            // a treasure that is MOVED must also lie where the floor''s NavMesh is (what the guards and the QA call walkable: an agent keeps 0.5 m clear): open ground that an agent can reach within 2 m of it, as the loot test asks;
            // a spot the planner chose is left as it was
            return !onNavMesh || grid.Near(grid.Flood(box, WalkGrid.AgentRadius), slot.x, slot.z, 2.0f);'
Rep '            public const float Person = 0.3f, AgentRadius = 0.55f;' '            public const float Person = 0.3f, AgentRadius = 0.5f;'
Rep 'if (!RoomForCarryCore(layout, spot, lifters, sizeX, sizeZ, grid)) continue;' 'if (!RoomForCarryCore(layout, spot, lifters, sizeX, sizeZ, grid, true)) continue;'
Save-Src
"p24 done"
