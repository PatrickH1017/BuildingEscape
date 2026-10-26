# The owner's playtest of 2026-10-23: "Plants also should not spawn in the middle of rooms (along the walls are good) and floating items such as fire extinguishers should not be floating randomly". Found with the PropScan scenario (NetDevPropScan.cs):
#  - ThHub put a plant in each corner of every open hallway junction (the middle of a crossing, 3 m from any wall): gone;
#  - the big lounge put a plant_tall beside the sofa's armchair, in the middle of the room (the lounge has plants against its south wall already): gone;
#  - RoomCtx.Mount hung a wall prop (extinguisher, axe case, clock, pull station ...) on a side of the room without a wall (an open side of an open-plan area): it hung in the air; it now needs a wall there (HasWall);
#  - OfficeThemes.Plant (a plant against a wall) needs the wall too.
# And the scan: floors 1 to 6, the shelves' cardboard boxes (box_*) are not "floating" (they stand on boards that have no collider).
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }

Open-Src ($Root + 'Scripts\Interior\OfficeLayout.cs')
Rep @'
        public Item Mount(char side, string key, float along, float height, float gap = 0.0f, float clear = 0f)
        {
            var def = PropTable.Get(key);
'@ @'
        public Item Mount(char side, string key, float along, float height, float gap = 0.0f, float clear = 0f)
        {
            if (!HasWall(side)) return null;                                  // (the owner's playtest, 2026-10-23: "floating items such as fire extinguishers should not be floating randomly": a side of the room without a wall has nothing to hang it on)
            var def = PropTable.Get(key);
'@
Save-Src

Open-Src ($Root + 'Scripts\Interior\OfficeThemes.cs')
Rep @'
        void Plant(RoomCtx r, char side, float along)
        {
            r.Wall(side, PlantKey(Chance(0.45)), along);
        }
'@ @'
        void Plant(RoomCtx r, char side, float along)
        {
            if (!r.HasWall(side)) return;                                       // (a plant stands against a wall, never in the open: the owner's playtest, 2026-10-23)
            r.Wall(side, PlantKey(Chance(0.45)), along);
        }
'@
Rep @'
            // the junction stays open; just a plant in each corner of the square (against the partition corners)
            float h = r.w / 2 - 0.25f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    if (Chance(0.55)) r.Put(PlantKey(Chance(0.5)), sx * (h - 0.1f), sz * (h - 0.1f), 0f);
'@ @'
            // the junction stays open and bare: it had a plant in each corner of its square, which stood in the middle of the crossing, 3 m from any wall (the owner's playtest, 2026-10-23: "Plants also should not spawn in the middle of rooms (along the walls are good)")
'@
Rep '            r.Put("plant_tall", armX + 0.3f, zs - 0.2f, 0f);' '            // (a plant_tall stood here beside the armchair, in the middle of the room: the owner, 2026-10-23, "Plants also should not spawn in the middle of rooms"; the lounge has its plants against the south wall)'
Save-Src

Open-Src ($Root + 'Editor\NetDev\NetDevPropScan.cs')
Rep 'for (int floor = 1; floor <= 4; floor++)' 'for (int floor = 1; floor <= 6; floor++)'
Rep '                        if (b.min.y > 0.2f)' '                        if (b.min.y > 0.2f && !def.key.StartsWith("box_"))'
Save-Src
"props fix patched"
