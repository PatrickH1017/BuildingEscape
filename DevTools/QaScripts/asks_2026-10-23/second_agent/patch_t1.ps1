# T1 (the owner, 2026-10-23: "Please update floors 1-6 to use the assets the 7th floor is using, the style is much cleaner"): floors 1 to 7 get the Floor 7 walls / ceiling, doors and dressing. One shared rule, FloorArtRules.
# Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; the new files are written with CRLF.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't1_new\FloorArtRules.cs') ($A + 'Scripts\Interior\FloorArtRules.cs')
Put-Crlf (Join-Path $here 't1_new\NetDevAsks1023ETest.cs') ($A + 'Editor\NetDev\NetDevAsks1023ETest.cs')
Put-Crlf (Join-Path $here 't1_new\NetDevAsks1023E.cs') ($A + 'Editor\NetDev\NetDevAsks1023E.cs')

# ---- the three places that said "floor 7 only"
$SC = $A + 'Scripts\Interior\WallSkinRules.cs'; Open-Src $SC
Rep '        /// <summary>The one floor this is tried on.</summary>' '        /// <summary>The last floor of the new art: floor 7 was the test floor and floors 1 to 6 follow since the owner''s go (2026-10-23); <see cref="FloorArtRules"/> is the one rule.</summary>'
Rep '        public const int SkinnedFloor = 7;' '        public const int SkinnedFloor = FloorArtRules.LastFloor;'
Rep '        public static bool AppliesTo(int floor) => floor == SkinnedFloor;' '        public static bool AppliesTo(int floor) => FloorArtRules.AppliesTo(floor);'
Save-Src

$SC = $A + 'Scripts\Interior\OfficePlanner.Dressing.cs'; Open-Src $SC
Rep '        /// <summary>The one floor that is dressed (the test floor).</summary>' '        /// <summary>The last floor that is dressed: floor 7 was the test floor and floors 1 to 6 follow since the owner''s go (2026-10-23); <see cref="FloorArtRules"/> is the one rule.</summary>'
Rep '        public const int DressedFloor = 7;' '        public const int DressedFloor = FloorArtRules.LastFloor;'
Rep '            if (!DressingOn || inp.floor != DressedFloor) return;' '            if (!DressingOn || !FloorArtRules.AppliesTo(inp.floor)) return;'
Save-Src

$SC = $A + 'Scripts\Interior\DoorSkinRules.cs'; Open-Src $SC
Rep '        /// <summary>Floor 7 only, like the walls and the ceiling: the doors follow <see cref="WallSkinRules"/> (when the walls are shown with the pack''s pieces, so are the doors).</summary>' '        /// <summary>The same floors as the walls and the ceiling (floors 1 to 7: <see cref="FloorArtRules"/>): the doors follow <see cref="WallSkinRules"/> (when the walls are shown with the pack''s pieces, so are the doors).</summary>'
Save-Src

# ---- the pure tests that pinned "no other floor"
$SC = $A + 'Editor\NetDev\NetDevWallSkinTest.cs'; Open-Src $SC
Rep 'Check("wall skin: it works on floor 7 and no other floor", WallSkinRules.AppliesTo(7) && WallSkinRules.SkinnedFloor == 7 && Enumerable.Range(1, 12).Where(f => f != 7).All(f => !WallSkinRules.AppliesTo(f)));' 'Check("wall skin: it works on floors 1 to 7 (the owner, 2026-10-23) and on no other floor", Enumerable.Range(1, 7).All(f => WallSkinRules.AppliesTo(f)) && WallSkinRules.SkinnedFloor == 7 && Enumerable.Range(8, 6).All(f => !WallSkinRules.AppliesTo(f)) && !WallSkinRules.AppliesTo(0));'
Save-Src

$SC = $A + 'Editor\NetDev\NetDevDoorSkinTest.cs'; Open-Src $SC
Rep 'Check("door skin: it works on floor 7 and no other floor", DoorSkinRules.AppliesTo(7) && Enumerable.Range(1, 12).Where(f => f != 7).All(f => !DoorSkinRules.AppliesTo(f)));' 'Check("door skin: it works on floors 1 to 7 (the owner, 2026-10-23) and on no other floor", Enumerable.Range(1, 7).All(f => DoorSkinRules.AppliesTo(f)) && Enumerable.Range(8, 6).All(f => !DoorSkinRules.AppliesTo(f)) && !DoorSkinRules.AppliesTo(0));'
Save-Src

$SC = $A + 'Editor\NetDev\NetDevFloorDressTest.cs'; Open-Src $SC
Rep '                FdDress(777, 6, 4, false);' ('                FdDress(777, 6, 4, false);' + "`n" + '                FdDress(4101, 8, 5, true, 1);          // (the owner, 2026-10-23: floors 1 to 6 are dressed like floor 7 - the same audit on floors 1, 3 and 6)' + "`n" + '                FdDress(4101, 8, 5, false, 3);' + "`n" + '                FdDress(4101, 8, 5, true, 6);')
Rep '                // the other floors are untouched by the pass' '                // the floors without the new art are untouched by the pass'
Rep '                foreach (int fl in new[] { 1, 3, 6, 8 })' '                foreach (int fl in new[] { 8, 9, 12 })'
Rep 'Check("floor dressing: floors 1, 3, 6 and 8 are exactly the same with the pass on or off", same);' 'Check("floor dressing: floors 8, 9 and 12 (no new art) are exactly the same with the pass on or off", same);'
Rep 'Check("floor dressing: the floor number it works on is 7", OfficePlanner.DressedFloor == 7);' 'Check("floor dressing: it works on floors 1 to 7 and no other", OfficePlanner.DressedFloor == 7 && Enumerable.Range(1, 7).All(FloorArtRules.AppliesTo) && !FloorArtRules.AppliesTo(8));'
Rep '        static void FdDress(int seed, int cols, int rows, bool walk)' '        static void FdDress(int seed, int cols, int rows, bool walk, int floor = 7)'
Rep '            string tag = "floor dressing (seed " + seed + ", " + cols + "x" + rows + "): ";' '            string tag = "floor dressing (seed " + seed + ", " + cols + "x" + rows + (floor == 7 ? "" : ", floor " + floor) + "): ";'
Rep '            var b = FdMake(seed, 7, cols, rows, false);' '            var b = FdMake(seed, floor, cols, rows, false);'
Rep '            var a = FdMake(seed, 7, cols, rows, true);' '            var a = FdMake(seed, floor, cols, rows, true);'
Rep '            var a2 = FdMake(seed, 7, cols, rows, true);' '            var a2 = FdMake(seed, floor, cols, rows, true);'
Save-Src

# ---- the scenario and the pure test are registered
$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void Asks1023D()' ('        public static void Asks1023E()' + "`n" + '        {' + "`n" + '            SessionState.SetString("netdev.mode", "asks1023e");' + "`n" + '            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");' + "`n" + '            EditorApplication.isPlaying = true;' + "`n" + '        }' + "`n" + '        public static void Asks1023D()')
Rep '&& mode != "asks1023d")' '&& mode != "asks1023d" && mode != "asks1023e")'
Rep '                case "asks1023d": yield return Asks1023DScenario(); break;' ('                case "asks1023d": yield return Asks1023DScenario(); break;' + "`n" + '                case "asks1023e": yield return Asks1023EScenario(); break;')
Save-Src
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            Asks1023DTest();' ('            Asks1023DTest();' + "`n" + '            Asks1023ETest();')
Save-Src
"T1 patched"
