# The final regression of the 2026-10-21 asks on the finished code in BE_dev (and BE_dev_c for the two-machine runs):
# compile + pure tests, every single-process scenario, then the two-machine scenarios. The summary goes to qa_summary_fin.txt (one line per scenario, as each ends).
param([string]$Prefix = 'fin')
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$sum = "$root\qa_summary_$Prefix.txt"
$methods = @('Offline', 'Sneak', 'Pew', 'Guns', 'Combat', 'Armour', 'Shaft:gfx', 'PewLoot', 'Doors', 'Outpost', 'Demo', 'Etr', 'LootQa', 'R7', 'Round12', 'R5', 'R6', 'Looks', 'FloatQa', 'GunSafes', 'Checklist', 'Floors:gfx', 'Asks1021:gfx', 'GuardCall', 'FpLook:gfx')
& "$root\qa_regress.ps1" -Prefix $Prefix -Project BE_dev -Methods $methods | Out-Null
Add-Content -Path $sum -Value "--- two machines ---" -Encoding UTF8
$mp = & "$root\mp_baseline.ps1" -Prefix "${Prefix}mp" -Methods @('PingNet', 'Armed', 'MeleeNet')
foreach ($l in $mp) { Add-Content -Path $sum -Value "$l" -Encoding UTF8 }
Add-Content -Path $sum -Value "ALL DONE $(Get-Date -Format 'HH:mm:ss')" -Encoding UTF8
