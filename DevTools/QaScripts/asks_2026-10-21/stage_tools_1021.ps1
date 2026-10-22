# Copies my scripts, the master texts, the regression summaries and the pictures of the 2026-10-21 asks into the REAL project's DevTools (outside Assets: the Editor does not import them). Nothing is overwritten that is newer.
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$tools = "$real\DevTools\QaScripts\asks_2026-10-21"
$pics = "$real\DevTools\Pictures_2026-10-21"
New-Item -ItemType Directory -Force $tools, $pics | Out-Null
$n = 0
Get-ChildItem $here -File | Where-Object { $_.Name -match '^(p\d+_.*\.ps1|patchlib\.ps1|build\.ps1|run1\.ps1|qa_regress\.ps1|ui_dev\.ps1|touch\.ps1|runmp\.ps1|mp_baseline\.ps1|final_regress\.ps1|restore_mine_1021\.ps1|stage_tools_1021\.ps1|apply1021\.ps1|notes_1021_.*|regression_1021\.txt|baseline_1021_dev\.tsv|NetDevAsks1021\.cs\.txt|Asks1021Test\.cs\.txt|NetDevPingNet\.cs\.txt|NetDevPingUi\.cs\.txt|ping_section\.txt|lootplan_tail\.txt|qa_summary_(rg1|rg2|fin)\.txt)$' } | ForEach-Object {
  Copy-Item $_.FullName "$tools\$($_.Name)" -Force; $n++
}
"copied $n scripts and texts to $tools"
$m = 0
# the pictures of the final run (the server pictures of the older rule are left out)
$names = @('be_shot_asks_corpse.png', 'be_shot_fplook_Pew.png', 'be_ui_ping_popping.png', 'be_ui_ping_steady.png', 'be_ui_ping_fading.png')
Get-ChildItem $env:TEMP -File | Where-Object { $names -contains $_.Name -or ($_.Name -match '^be_shot_asks_server_f\d_\d+\.png$' -and $_.LastWriteTime -gt (Get-Date '2026-10-21 18:00')) } | ForEach-Object { Copy-Item $_.FullName "$pics\$($_.Name)" -Force; $m++ }
"copied $m pictures to $pics"
