# T7, second step: PropMeasure (Employees Take Revenge's tool for the size of a prefab) also measures the Synty Battle Royale pieces that could make the PVP room a battlefield (the owner, 2026-10-23: "The PvP room should use the battle royale assets
# from SYNTY to make it seem more like a battlefield"): barriers, barricade, tank trap, tyres, cinderblocks, fences, rubble, walls, an armoured car. Run: Unity -batchmode -nographics -projectPath C:\bd -executeMethod BuildingEscape.EditorTools.PropMeasure.Log
# (the sizes are the renderers' bounds with the prefab at the origin, turned 0 degrees: what PropTable.Add wants as width, height, depth).
# Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Editor\PropMeasure.cs')
$props = 'Barrier_01', 'Barrier_02', 'Barrier_03', 'Barrier_04', 'Barrier_Dirt_01', 'Barricade_01', 'TankTrap_01', 'TireStack_01', 'Tire_01', 'Cinderblock_01', 'Cinderblock_Wall_01', 'WireFence_01', 'WireFence_02', 'Wirespool_01',
  'Iron_Sheet_01', 'Slatbox_01', 'Propane_01', 'Generator_01', 'Spotlight_01', 'Target_01', 'MissileStack_01', 'AmmoBox_01', 'WeaponCase_01', 'Crate_Medical_01', 'Range_Wall_01', 'Range_Wall_Half_01', 'BaseWall_01', 'BaseWall_Broken_01',
  'BaseWall_Broken_02', 'WoodPlank_01', 'EmergencyDrop_Crate_01', 'Crate_01', 'Crate_Small_01'
$lines = ($props | ForEach-Object { '            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_' + $_ + '.prefab",' }) -join "`n"
$envs = 'Rubble_Pile_01', 'Rubble_Pile_02', 'Rubble_Stone_01', 'Rubble_Stone_02', 'Port_Wall_01', 'Port_Wall_02', 'Port_Concrete_Slab_01', 'Concrete_Base_01'
$lines += "`n" + (($envs | ForEach-Object { '            "Assets/Synty/PolygonBattleRoyale/Prefabs/Environments/SM_Env_' + $_ + '.prefab",' }) -join "`n")
$lines += "`n" + '            "Assets/Synty/PolygonBattleRoyale/Prefabs/Vehicles/SM_Veh_Armored_Car_Destroyed_01.prefab",' + "`n" + '            "Assets/Synty/PolygonBattleRoyale/Prefabs/Vehicles/SM_Veh_Armored_Car_01.prefab",'
Rep '            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Barrel_02.prefab",' ('            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Barrel_02.prefab",' + "`n" + $lines)
Save-Src
"PropMeasure list extended"
