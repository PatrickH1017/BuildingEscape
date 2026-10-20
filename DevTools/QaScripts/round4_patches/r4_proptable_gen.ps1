# Round 4 (A7): PropTable -> the Synty office pack. Every old key stays (the themes, the tests and the loot code use the names) but points at a Synty prefab with its MEASURED size
# (synty_measure.tsv, made by SyntySheet.Measure); new keys for the new rooms and the safety kit. Writes the table into BE_qa\Assets\Scripts\Interior\PropTable.cs.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

$rows = @{}
Import-Csv (Join-Path $PSScriptRoot 'synty_measure.tsv') -Delimiter "`t" | ForEach-Object { $rows[$_.path] = $_ }

$F = 'solid: false, cat: PropCat.Chair'
$D = 'solid: false, cat: PropCat.DeskItem'
$M = 'solid: false, cat: PropCat.Mount'
$DEC = 'solid: false, cat: PropCat.Deco'
$APP = 'cat: PropCat.Appliance'
$defs = @(
  # ---- desks and seating
  @('desk_1', 'Props/Furniture/SM_Prop_Desk_01', ''),
  @('desk_2', 'Props/Furniture/SM_Prop_Desk_04', ''),
  @('desk_3', 'Props/Furniture/SM_Prop_Desk_03', ''),
  @('desk_l', 'Props/Furniture/SM_Prop_Desk_02', ''),
  @('desk_l_big', 'Props/Furniture/SM_Prop_Desk_05', ''),
  @('desk_reception', 'Props/Furniture/SM_Prop_Desk_06', ''),
  @('desk_stand', 'Props/Furniture/SM_Prop_Desk_Standing_01', ''),
  @('chair_office', 'Props/Furniture/SM_Prop_Chair_05', $F),
  @('chair_std', 'Props/Furniture/SM_Prop_Chair_07', $F),
  @('chair_modern', 'Props/Furniture/SM_Prop_Chair_02', $F),
  @('chair_simple', 'Props/Furniture/SM_Prop_Chair_01', $F),
  @('chair_exec', 'Props/Furniture/SM_Prop_Chair_08', $F),
  @('chair_cafe', 'Props/Furniture/SM_Prop_Chair_09', $F),
  @('chair_blue', 'Props/Furniture/SM_Prop_Chair_03', $F),
  @('stool', 'Props/Furniture/SM_Prop_Chair_11', $F),
  @('sofa1', 'Props/Furniture/SM_Prop_Couch_01', ''),
  @('sofa2', 'Props/Furniture/SM_Prop_Couch_02', ''),
  @('sofa3', 'Props/Furniture/SM_Prop_Couch_03', ''),
  @('sofa_wood', 'Props/Furniture/SM_Prop_Couch_04', ''),
  @('armchair', 'Props/Furniture/SM_Prop_Couch_05', ''),
  @('lounge_chair', 'Props/Furniture/SM_Prop_Couch_06', ''),
  @('fatboy', 'Props/Furniture/SM_Prop_Chair_BeanBag_02', ''),
  @('beanbag_blue', 'Props/Furniture/SM_Prop_Chair_BeanBag_01', ''),
  # ---- tables
  @('table_box', 'Props/Furniture/SM_Prop_Table_02', ''),
  @('table_round', 'Props/Furniture/SM_Prop_Table_Round_01', ''),
  @('table_round2', 'Props/Furniture/SM_Prop_Table_Round_02', ''),
  @('table_long', 'Props/Furniture/SM_Prop_Table_03', ''),
  @('table_big', 'Props/Furniture/SM_Prop_Table_01', ''),
  @('conf_table', 'Props/Furniture/SM_Prop_Table_Conference_01', ''),
  @('conf_table2', 'Props/Furniture/SM_Prop_Table_Conference_02', ''),
  @('coffee_table', 'Props/Furniture/SM_Prop_CoffeeTable_01', ''),
  @('coffee_table2', 'Props/Furniture/SM_Prop_CoffeeTable_02', ''),
  @('coffee_table3', 'Props/Furniture/SM_Prop_CoffeeTable_03', ''),
  # ---- storage
  @('bookshelf', 'Props/Furniture/SM_Prop_Shelf_01', ''),
  @('bookshelf_locked', 'Props/Furniture/SM_Prop_Shelf_05', ''),
  @('shelf', 'Props/Furniture/SM_Prop_Shelf_03', ''),
  @('shelf_low', 'Props/Furniture/SM_Prop_Shelf_06', ''),
  @('shelf_box', 'Props/Furniture/SM_Prop_Cabinets_02', ''),
  @('shelf_wide', 'Props/Furniture/SM_Prop_Shelf_07', ''),
  @('shelf_wood', 'Props/Furniture/SM_Prop_Shelf_02', ''),
  @('cabinet_med', 'Props/Furniture/SM_Prop_Cabinets_03', ''),
  @('cabinet_small', 'Props/Furniture/SM_Prop_Cabinets_01', ''),
  @('credenza', 'Props/Furniture/SM_Prop_Shelf_06', ''),
  @('cube_wall', 'Props/Furniture/SM_Prop_CubicalWall_01', ''),
  @('cube_wall_tall', 'Props/Furniture/SM_Prop_CubicalWall_Tall_01', ''),
  @('divider', 'Props/Furniture/SM_Prop_Divider_01', ''),
  # ---- the desk's things
  @('monitor_new', 'Props/Desk Props/SM_Prop_Computer_Monitor_01', "$D, value: 400, weight: 5f, label: `"Monitor`""),
  @('monitor_old', 'Props/Desk Props/SM_Prop_Monitor_Crt_01', "$D, value: 250, weight: 6f, label: `"Old Monitor`""),
  @('keyboard', 'Props/Desk Props/SM_Prop_Computer_Keyboard_01', "$D, value: 100, weight: 1f, label: `"Keyboard`""),
  @('mouse', 'Props/Desk Props/SM_Prop_Computer_Mouse_01', $D),
  @('phone', 'Props/Desk Props/SM_Prop_Phone_Desk_01', "$D, value: 200, weight: 1.5f, label: `"Desk Phone`""),
  @('stapler', 'Props/Desk Props/SM_Prop_Stapler_01', "$D, value: 90, weight: .6f, label: `"Stapler`""),
  @('tape', 'Props/Desk Props/SM_Prop_Cellotape_01', "$D, value: 70, weight: .4f, label: `"Roll of Tape`""),
  @('notepad', 'Props/Desk Props/SM_Prop_NotePad_01', "$D, value: 50, weight: .4f, label: `"Notepad`""),
  @('papers', 'Props/Desk Props/SM_Prop_Paper_Pile_01', $D),
  @('pencil_holder', 'Props/Desk Props/SM_Prop_PencilCup_01', $D),
  @('cup', 'Props/Desk Props/SM_Prop_Cup_Paper_01', $D),
  @('laptop', 'Props/Desk Props/SM_Prop_Laptop_01', $D),
  @('desk_lamp', 'Props/Desk Props/SM_Prop_Desklamp_02', $D),
  @('folder_tray', 'Props/Desk Props/SM_Prop_FolderTray_02', $D),
  @('photo', 'Props/Desk Props/SM_Prop_Photo_01', $D),
  @('trophy_cup', 'Props/Desk Props/SM_Prop_Trophy_01', $D),
  @('name_plate', 'Props/Desk Props/SM_Prop_NamePlate_01', $D),
  # ---- appliances
  @('coffee_machine', 'Props/Kitchen Props/SM_Prop_CoffeeMachine_Dripper_01', "$APP, value: 650, weight: 6f, label: `"Coffee Machine`""),
  @('espresso', 'Props/Kitchen Props/SM_Prop_CoffeeMachine_01', "$APP, value: 900, weight: 12f, lifters: 2, label: `"Espresso Machine`""),
  @('microwave', 'Props/Kitchen Props/SM_Prop_Microwave_01', "$APP, value: 350, weight: 9f, label: `"Microwave`""),
  @('toaster', 'Props/Kitchen Props/SM_Prop_Toaster_01', "$D"),
  @('kettle', 'Props/Kitchen Props/SM_Prop_Kettle_01', "$D"),
  @('printer', 'Props/Misc/SM_Prop_Photocopier_02', "$APP, value: 800, weight: 16f, lifters: 2, label: `"Office Printer`""),
  @('copier', 'Props/Misc/SM_Prop_Photocopier_01', "$APP, value: 1400, weight: 30f, lifters: 3, label: `"Copy Machine`""),
  @('shredder', 'Props/Desk Props/SM_Prop_PaperShredder_01', "$APP"),
  @('projector', 'Props/Desk Props/SM_Prop_Projector_01', "$APP, value: 900, weight: 5f, label: `"Projector`""),
  @('water', 'Props/Misc/SM_Prop_WaterCooler_01', "$APP, value: 300, weight: 10f, lifters: 2, label: `"Water Cooler`""),
  @('fridge', 'Props/Kitchen Props/SM_Prop_Fridge_01', "$APP"),
  @('fridge2', 'Props/Kitchen Props/SM_Prop_Fridge_02', "$APP"),
  @('minifridge', 'Props/Kitchen Props/SM_Prop_MiniFridge_01', "$APP"),
  @('vending', 'Props/Bathroom/SM_Prop_VendingMachine_01', "$APP"),
  @('counter_a1', 'Props/Kitchen Props/SM_Prop_Kitchen_Cabinet_01', ''),
  @('counter_a2', 'Props/Kitchen Props/SM_Prop_Kitchen_Counter_01', ''),
  @('counter_b1', 'Props/Kitchen Props/SM_Prop_Kitchen_Cabinet_01', ''),
  @('counter_b2', 'Props/Kitchen Props/SM_Prop_Kitchen_Counter_01', ''),
  @('kcab', 'Props/Kitchen Props/SM_Prop_Kitchen_Cabinet_01', ''),
  @('ksink', 'Props/Kitchen Props/SM_Prop_Kitchen_CounterSink_01', ''),
  @('kstorage', 'Props/Furniture/SM_Prop_Cabinets_02', ''),
  @('kshelve', 'Props/Furniture/SM_Prop_Shelf_02', ''),
  @('kitchen_unit', 'Props/Furniture/SM_Prop_Shelf_07', ''),
  # ---- server room, storage, janitor, arcade
  @('server_rack', 'Props/Misc/SM_Prop_Server_Cabinet_01_Full', ''),
  @('server_rack2', 'Props/Misc/SM_Prop_Server_Cabinet_02_Full', ''),
  @('server_half', 'Props/Misc/SM_Prop_Server_Cabinet_03_Full', ''),
  @('pallet', 'Props/Misc/SM_Prop_Pallet_01', ''),
  @('cart', 'Props/Misc/SM_Prop_Cart_01', ''),
  @('janitor_cart', 'Props/Misc/SM_Prop_Janitor_Cart_01', ''),
  @('mop_bucket', 'Props/Misc/SM_Prop_Janitor_Bucket_01', "$DEC"),
  @('coat_rack', 'Props/Misc/SM_Prop_CoatRack_01', "$DEC"),
  @('arcade', 'Props/Misc/SM_Prop_ArcadeMachine_01', ''),
  @('jukebox', 'Props/Misc/SM_Prop_Jukebox_01', ''),
  @('pool_table', 'Props/Misc/SM_Prop_PoolTable_01', ''),
  @('table_football', 'Props/Furniture/SM_Prop_TableFootball_01', ''),
  @('fishtank', 'Props/Misc/SM_Prop_Fishtank_01', ''),
  @('presentation_chart', 'Props/Misc/SM_Prop_Presentation_Chart_01', "$DEC"),
  @('rolling_tv', 'Props/Misc/SM_Prop_Rolling_TV_01', ''),
  # ---- walls: screens, boards, art
  @('wall_tv', 'Props/Wall Props/SM_Prop_TV_Wall_01', $M),
  @('whiteboard', 'Props/Misc/SM_Prop_Whiteboard_01', $M),
  @('blackboard', 'Props/Wall Props/SM_Prop_Blackboard_01', $M),
  @('projector_screen', 'Props/Wall Props/SM_Prop_ProjectorScreen_01', $M),
  @('clock', 'Props/Wall Props/SM_Prop_Clock_01', $M),
  @('ac_unit', 'Props/Wall Props/SM_Prop_Aircon_01', $M),
  @('painting', 'Props/Wall Props/SM_Prop_Poster_02', $M),
  @('painting_small', 'Props/Wall Props/SM_Prop_Poster_04', $M),
  @('poster_1', 'Props/Wall Props/SM_Prop_Poster_01', $M),
  @('poster_3', 'Props/Wall Props/SM_Prop_Poster_03', $M),
  @('poster_stocks', 'Props/Wall Props/SM_Prop_Poster_Stocks_01', $M),
  @('certificate', 'Props/Wall Props/SM_Prop_Certificate_01', $M),
  @('calendar', 'Props/Wall Props/SM_Prop_Calender_01', $M),
  @('child_art', 'Props/Wall Props/SM_Prop_Child_Art_01', $M),
  @('corkboard', 'Props/Wall Props/SM_Prop_CorkBoard_01', $M),
  @('corkboard_blank', 'Props/Wall Props/SM_Prop_CorkBoard_03', $M),
  @('wall_dressing', 'Props/Wall Props/SM_Prop_WallDressing_01', $M),
  @('wall_dressing2', 'Props/Wall Props/SM_Prop_WallDressing_02', $M),
  @('wall_shelf', 'Props/Wall Props/SM_Prop_Shelf_Wall_01', $M),
  @('dartboard', 'Props/Wall Props/SM_Prop_Darkboard_01', $M),
  # ---- the building-safety kit and the signs (A10 / A11)
  @('extinguisher', 'Props/Wall Props/SM_Prop_Fire_Extinguisher_01', $M),
  @('fire_alarm', 'Props/Wall Props/SM_Prop_FireAlarm_Switch_01', $M),
  @('emergency_axe', 'Props/Wall Props/SM_Prop_Emergency_Axe_01', $M),
  @('fire_detector', 'Props/Wall Props/SM_Prop_Fire_Detector_01', $M),
  @('sign_exit', 'Props/Wall Props/SM_Prop_Sign_Exit_01', $M),
  @('sign_stairs', 'Props/Wall Props/SM_Prop_Sign_Stairs_01', $M),
  @('sign_kitchen', 'Props/Wall Props/SM_Prop_Sign_Kitchen_01', $M),
  @('sign_wc_men', 'Props/Wall Props/SM_Prop_Sign_Toilets_Male_01', $M),
  @('sign_wc_women', 'Props/Wall Props/SM_Prop_Sign_Toilets_Female_01', $M),
  @('sign_wc', 'Props/Wall Props/SM_Prop_Sign_Toilets_01', $M),
  @('sign_out_of_order', 'Props/Wall Props/SM_Prop_Sign_OutofOrder_01', $M),
  @('sign_manager', 'Props/Wall Props/SM_Prop_Sign_Manager_01', $M),
  @('sign_director', 'Props/Wall Props/SM_Prop_Sign_Director_01', $M),
  @('sign_hr', 'Props/Wall Props/SM_Prop_Sign_HR_Dep_01', $M),
  @('sign_sales', 'Props/Wall Props/SM_Prop_Sign_Sales_01', $M),
  @('sign_reception', 'Props/Wall Props/SM_Prop_Sign_Receptionist_01', $M),
  # ---- deco
  @('trash_bin', 'Props/Misc/SM_Prop_Bin_02', $DEC),
  @('trash_small', 'Props/Misc/SM_Prop_Bin_01', $DEC),
  @('box', 'Props/Misc/SM_Prop_CardboardBox_04', 'cat: PropCat.Deco, value: 150, weight: 3f, label: "Sealed Crate"'),
  @('box_small', 'Props/Misc/SM_Prop_CardboardBox_01', 'cat: PropCat.Deco'),
  @('box_long', 'Props/Misc/SM_Prop_CardboardBox_03', 'cat: PropCat.Deco'),
  @('box_stack', 'Props/Misc/SM_Prop_CardboardBox_Stack_01', 'cat: PropCat.Deco'),
  @('briefcase', 'Props/Misc/SM_Prop_Briefcase_01', 'solid: false, cat: PropCat.Deco, value: 1100, weight: 4f, label: "Briefcase"'),
  @('plant_small', 'Props/Misc/SM_Prop_Plant_09', 'cat: PropCat.Deco'),
  @('plant_big', 'Props/Misc/SM_Prop_Plant_04', 'cat: PropCat.Deco'),
  @('plant_tall', 'Props/Misc/SM_Prop_Plant_02', 'cat: PropCat.Deco'),
  @('plant_floor', 'Props/Misc/SM_Prop_Plant_01', 'cat: PropCat.Deco'),
  @('plant_box', 'Props/Misc/SM_Prop_Plant_05', 'cat: PropCat.Deco'),
  @('plant_med', 'Props/Misc/SM_Prop_Plant_10', 'cat: PropCat.Deco'),
  @('plant_pot', 'Props/Misc/SM_Prop_Plant_08', 'cat: PropCat.Deco'),
  @('plant_palm', 'Props/Misc/SM_Prop_Plant_14', 'cat: PropCat.Deco'),
  @('plant_tiny', 'Props/Misc/SM_Prop_Plant_11', "$DEC"),
  @('rug_1', 'Props/Misc/SM_Prop_Rug_01', "$DEC"),
  @('rug_2', 'Props/Misc/SM_Prop_Rug_02', "$DEC"),
  @('rug_3', 'Props/Misc/SM_Prop_Rug_03', "$DEC"),
  @('lamp', 'Props/Misc/SM_Prop_Plant_02', 'solid: false, cat: PropCat.Deco'),
  # ---- the coffee shop on the first floor
  @('cs_counter', 'Props/Furniture/SM_Prop_Desk_06', ''),
  @('cs_display', 'Props/Kitchen Props/SM_Prop_MiniFridge_01', ''),
  @('cs_register', 'Props/Desk Props/SM_Prop_Computer_Monitor_01', $D),
  @('cs_table', 'Props/Furniture/SM_Prop_Table_Round_02', ''),
  @('cs_chair', 'Props/Furniture/SM_Prop_Chair_09', $F),
  @('cs_pot', 'Props/Misc/SM_Prop_Plant_05', 'cat: PropCat.Deco'),
  @('cs_menu', 'Props/Wall Props/SM_Prop_Blackboard_01', $M),
  @('pastry', 'Props/Kitchen Props/SM_Prop_Donut_Box_01', "$D, value: 60, weight: .5f, label: `"Donut Box`""),
  @('takeaway', 'Props/Desk Props/SM_Prop_Cup_Coffee_Disposable_Closed_01', "$D, value: 50, weight: .4f, label: `"Takeaway Coffee`"")
)

function F2([string]$v) { $x = [math]::Round([double]::Parse($v, [Globalization.CultureInfo]::InvariantCulture), 2); $t = $x.ToString('0.##', [Globalization.CultureInfo]::InvariantCulture); if ($t -notmatch '\.') { $t += '.0' } ; if ($t.StartsWith('0.')) { $t = $t.Substring(1) } ; return $t + 'f' }
$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('            // ---------- the Synty POLYGON Office pack (the owner, 2026-10-17 night: "use the office assets for each floor"). Every prop faces +Z (frontQ 0); the sizes are the prefabs'' own, MEASURED')
$lines.Add('            // by Editor\SyntySheet.cs Measure (Employees Take Revenge > the tools; synty_measure.tsv) - do not guess them: the planner places by these numbers.')
$missing = @()
foreach ($d in $defs) {
  $key = $d[0]; $path = $d[1]; $flags = $d[2]
  $row = $rows[$path + '.prefab']
  if (-not $row) { $missing += "$key -> $path"; continue }
  $argText = '"' + $key + '", Syn + "' + $path + '", ' + (F2 $row.sx) + ', ' + (F2 $row.sy) + ', ' + (F2 $row.sz)
  if ($flags) { $argText += ', ' + $flags }
  # named arguments after positional ones need scale / frontQ to be left out: that is fine, they have defaults
  $lines.Add('            Add(' + $argText + ');')
}
if ($missing.Count -gt 0) { throw ("not in the measuring table: " + ($missing -join '; ')) }

Open-Src (Join-Path $Dir 'Scripts\Interior\PropTable.cs')
$startMark = '            // ---------- VNB low-poly office set (front = +Z)'
$endMark = '            // ---------- built from boxes in code'
$i0 = $s.IndexOf($startMark); $i1 = $s.IndexOf($endMark)
if ($i0 -lt 0 -or $i1 -lt 0 -or $i1 -le $i0) { throw 'markers of the old table not found' }
$nl = $eol
$s = $s.Substring(0, $i0) + ($lines -join $nl) + $nl + $nl + $s.Substring($i1)
# the constants of the old packs
$before = $s.Length
$s = [regex]::Replace($s, '(?m)^[ \t]*const string (Vnb|Nap|Pan|Cof) = [^\r\n]*\r?\n', '')
Write-Output ("old pack constants removed: {0} characters" -f ($before - $s.Length))
$dups = $defs | Group-Object { $_[0] } | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name }
if ($dups) { throw ("duplicate keys: " + ($dups -join ', ')) }
$s = $s.Replace('        static readonly Dictionary<string, PropDef> map = new Dictionary<string, PropDef>();', '        const string Syn = "Assets/Synty/PolygonOffice/Prefabs/";' + $nl + $nl + '        static readonly Dictionary<string, PropDef> map = new Dictionary<string, PropDef>();')
Save-Src
Write-Output ("PropTable written: {0} props" -f $defs.Count)
