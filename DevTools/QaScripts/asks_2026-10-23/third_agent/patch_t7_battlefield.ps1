# T7 (the owner, 2026-10-23: "The PvP room should use the battle royale assets from SYNTY to make it seem more like a battlefield"): the PVP room's cover in the middle becomes the Battle Royale pack's (PvpBattlefieldRules lists the pieces; BuildPvpBattlefield places them).
# The shelving along the walls and the crates in the pocket stay. PvpBattlefieldRules.On = false brings the old layout back. Needs patch_t7_measure.ps1 + patch_t7_probe.ps1 (the harness anchors).
# Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't7_new\PvpBattlefieldRules.cs') ($A + 'Scripts\PvpBattlefieldRules.cs')
Put-Crlf (Join-Path $here 't7_new\NetDevPvpBattlefield.cs') ($A + 'Editor\NetDev\NetDevPvpBattlefield.cs')

# ---- the props (sizes MEASURED, 2026-10-23, with PropMeasure: the models' own bounds)
Open-Src ($A + 'Scripts\Interior\PropTable.cs')
Rep '            Add("barrel", Br + "SM_Prop_Barrel_01", .66f, .91f, .57f, cat: PropCat.Deco);' @'
            Add("barrel", Br + "SM_Prop_Barrel_01", .66f, .91f, .57f, cat: PropCat.Deco);
            // the PVP room as a battlefield (the owner, 2026-10-23: "use the battle royale assets from SYNTY to make it seem more like a battlefield"; PvpBattlefieldRules): gabion barriers, a long barrier wall, a block wall, a tank trap, a wire spool
            Add("hesco_a", Br + "SM_Prop_Barrier_01", 1.12f, 1.07f, 1.12f, cat: PropCat.Deco);
            Add("hesco_b", Br + "SM_Prop_Barrier_02", 1.12f, 1.07f, 1.12f, cat: PropCat.Deco);
            Add("hesco_c", Br + "SM_Prop_Barrier_03", 1.15f, 1.07f, 1.15f, cat: PropCat.Deco);
            Add("hesco_wall", Br + "SM_Prop_Barrier_04", 5.5f, 2.13f, 1.39f, cat: PropCat.Deco);
            Add("cinder_wall", Br + "SM_Prop_Cinderblock_Wall_01", 2.5f, 1.08f, .27f, cat: PropCat.Deco);
            Add("tank_trap", Br + "SM_Prop_TankTrap_01", 1.61f, 1.31f, 1.5f, cat: PropCat.Deco);
            Add("wire_spool", Br + "SM_Prop_Wirespool_01", 1.56f, 1.02f, 1.56f, cat: PropCat.Deco);
'@
Save-Src

# ---- the room
Open-Src ($A + 'Scripts\FloorGenerator.DockRooms.cs')
Rep '        void BuildDockRooms(Transform parent)' @'
        /// <summary>The PVP room's cover in the middle: the Battle Royale pack's pieces of <see cref="PvpBattlefieldRules"/> (the owner, 2026-10-23), solid, square to the room.</summary>
        void BuildPvpBattlefield(Transform parent)
        {
            foreach (var p in PvpBattlefieldRules.Pieces) PlaceKey(p.key, p.x, p.z, p.yaw, parent, 0f, true);
        }

        void BuildDockRooms(Transform parent)
'@
Rep '            RackRow(4.75f - 1.83f, 4.75f + 1.83f, 21.9f, 3);' @'
            bool battlefield = PvpBattlefieldRules.On;                                                                                        // (T7, the owner, 2026-10-23: the cover in the middle is the Battle Royale pack's: PvpBattlefieldRules; false = the racks and cardboard of before)
            if (!battlefield) RackRow(4.75f - 1.83f, 4.75f + 1.83f, 21.9f, 3);
'@
Rep '            RackRow(2.05f + halfLen, 5.65f - halfLen, 15.5f, 2);' '            if (!battlefield) RackRow(2.05f + halfLen, 5.65f - halfLen, 15.5f, 2);'
Rep '            RackColumn(2.3f, PvpRoomAnnex.center.y - 1.83f, PvpRoomAnnex.center.y + 1.83f, 3);' '            if (!battlefield) RackColumn(2.3f, PvpRoomAnnex.center.y - 1.83f, PvpRoomAnnex.center.y + 1.83f, 3);'
Rep '            PlaceKey("pallet_loaded", 7.9f, 18.2f, 90f, parent, 0f, true);' '            if (!battlefield) PlaceKey("pallet_loaded", 7.9f, 18.2f, 90f, parent, 0f, true);'
Rep '            PlaceKey("pallet_loaded", 7.9f, 20.2f, 0f, parent, 0f, true);' '            if (!battlefield) PlaceKey("pallet_loaded", 7.9f, 20.2f, 0f, parent, 0f, true);'
Rep '            PlaceKey("crate_large", 8.7f, 21.6f, 0f, parent, 0f, true);' '            if (!battlefield) PlaceKey("crate_large", 8.7f, 21.6f, 0f, parent, 0f, true);'
Rep '            PlaceKey("crate_large", 8.7f, 22.4f, 0f, parent, 0f, true);' '            if (!battlefield) PlaceKey("crate_large", 8.7f, 22.4f, 0f, parent, 0f, true);'
Rep '            BoxBlock(parent, 1.9f, 16.85f, 0f, 2, 2, 2, true);' '            if (!battlefield) BoxBlock(parent, 1.9f, 16.85f, 0f, 2, 2, 2, true);'
Rep '            BoxBlock(parent, 1.9f, 19.9f, 0f, 2, 2, 1, true);' @'
            if (!battlefield) BoxBlock(parent, 1.9f, 19.9f, 0f, 2, 2, 1, true);
            if (battlefield) BuildPvpBattlefield(parent);
'@
Save-Src

# ---- the tests
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            Asks1023WTest();' @'
            Asks1023WTest();
            PvpBattlefieldTest();
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void PvpMap()' @'
        public static void PvpBattlefield()
        {
            SessionState.SetString("netdev.mode", "pvpbattlefield");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PvpBattlefieldUi()
        {
            SessionState.SetString("netdev.mode", "pvpbattlefieldui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void PvpMap()
'@
Rep '&& mode != "pvplineupui")' '&& mode != "pvplineupui" && mode != "pvpbattlefield" && mode != "pvpbattlefieldui")'
Rep '                case "pvplineupui": yield return PvpLineupUiScenario(); break;' @'
                case "pvplineupui": yield return PvpLineupUiScenario(); break;
                case "pvpbattlefield": yield return PvpBattlefieldScenario(); break;
                case "pvpbattlefieldui": yield return PvpBattlefieldUiScenario(); break;
'@
Save-Src
"T7 battlefield patched"
