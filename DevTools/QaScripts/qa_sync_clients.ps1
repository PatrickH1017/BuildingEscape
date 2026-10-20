# Mirrors BE_qa's Assets / ProjectSettings / Packages manifest into the five client copies in agent 1's scratchpad, refreshes their timestamps
# (Windows removes old files from %TEMP%) and compiles each copy once, one Unity at a time, so a recompile never lands in the middle of a Play run.
# Usage: .\qa_sync_clients.ps1 [-Copies BE_dev_c,BE_dev_c2,...] [-NoWarm]
param([string[]]$Copies = @('BE_dev_c', 'BE_dev_c2', 'BE_dev_c3', 'BE_dev_c4', 'BE_dev_late'), [switch]$NoWarm)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$old = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$src = "$here\BE_qa"
foreach ($c in $Copies) {
  $dst = "$old\$c"
  robocopy "$src\Assets" "$dst\Assets" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  robocopy "$src\ProjectSettings" "$dst\ProjectSettings" /MIR /MT:16 /NFL /NDL /NJH /NJS /NP | Out-Null
  Copy-Item "$src\Packages\manifest.json" "$dst\Packages\manifest.json" -Force
  Copy-Item "$src\Packages\packages-lock.json" "$dst\Packages\packages-lock.json" -Force
  & "$old\touch.ps1" -Roots @("$dst\Assets", "$dst\ProjectSettings", "$dst\Packages") | Out-Null
  if (-not $NoWarm) {
    robocopy "$real\Library\PackageCache" "$dst\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
    $log = "$here\warm_$c.log"
    $p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',$dst,'-executeMethod','BuildingEscape.EditorTools.NetDevTest.RulesTest','-logFile',$log -PassThru -Wait
    $errs = (Select-String -Path $log -Pattern 'error CS').Count
    "$c warmed: exit=$($p.ExitCode) compile errors=$errs"
  } else { "$c synced" }
}
