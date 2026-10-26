# Floor 7 doors, patch 1: hooks the pack's doors into BuildDoors, adds the four door pieces to the prop table and the pure test to PolicyTest (scratch copy C:\bd). Exact matches only (patchlib).
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1

# ---- FloorGenerator.Doors.cs
$SC = 'C:\bd\Assets\Scripts\FloorGenerator.Doors.cs'
Open-Src $SC
Rep '            var byBeam = new Dictionary<WallBox, SlidingDoor>();' ('            var byBeam = new Dictionary<WallBox, SlidingDoor>();' + "`n" + '            StartPackDoors();')
RepRx '(?m)^[ ]+if \(door\.Wide\) DressHallDoor\(door\);[^\r\n]*\r?\n[ ]+else if \(InGlassOffice\(w\)\) DressGlassDoor\(door\);[^\r\n]*' (
  '                    if (!DressPackDoor(door, w))                           // (floor 7: the pack''s doors, see FloorGenerator.DoorSkin; any other floor, or if that fails, the old dressing)' + "`n" +
  '                    {' + "`n" +
  '                        if (door.Wide) DressHallDoor(door);                // the main hallway doors get windows' + "`n" +
  '                        else if (InGlassOffice(w)) DressGlassDoor(door);   // the doors of the offices with a glass front are glass' + "`n" +
  '                    }')
Rep '            manager.OpenDoorsWithPeople();' ('            manager.OpenDoorsWithPeople();' + "`n" + '            LogPackDoors(manager.Count);')
Rep '            hallGlassMat = hallBarMat = null; hallGlassTex = null;' ('            hallGlassMat = hallBarMat = null; hallGlassTex = null;' + "`n" + '            DestroyPackDoorAssets();')
Save-Src

# ---- PropTable.cs
$SC = 'C:\bd\Assets\Scripts\Interior\PropTable.cs'
Open-Src $SC
RepRx '(?m)^[ ]*Add\("skin_ceiling_panel".*$' (
  '            Add("skin_ceiling_panel", Syn + "Buildings/SM_Bld_Ceiling_Panel_02", 2.5f, 0.039f, 2.5f, solid: false, cat: PropCat.Deco);' + "`n" +
  '            // ---------- the Floor 7 doors (the owner, 2026-10-23): the pack''s doors, fitted to the sliding leaves by DoorSkinRules / FloorGenerator.DoorSkin (a leaf is a unit cube, so only the measures matter, not the way they face).' + "`n" +
  '            Add("skin_door_white", Syn + "Buildings/SM_Bld_Door_02", 1.029f, 1.967f, 0.247f, solid: false, cat: PropCat.Deco);' + "`n" +
  '            Add("skin_door_wood", Syn + "Buildings/SM_Bld_Door_05", 1.029f, 1.967f, 0.247f, solid: false, cat: PropCat.Deco);' + "`n" +
  '            Add("skin_door_glass_l", Syn + "Buildings/SM_Bld_Door_Large_01_L", 0.974f, 2.452f, 0.172f, solid: false, cat: PropCat.Deco);' + "`n" +
  '            Add("skin_door_glass_r", Syn + "Buildings/SM_Bld_Door_Large_01_R", 0.974f, 2.452f, 0.172f, solid: false, cat: PropCat.Deco);')
Save-Src

# ---- NetDevTest.cs (PolicyTest)
$SC = 'C:\bd\Assets\Editor\NetDev\NetDevTest.cs'
Open-Src $SC
Rep '            WallSkinTest();' ('            WallSkinTest();' + "`n" + '            DoorSkinTest();')
Save-Src
