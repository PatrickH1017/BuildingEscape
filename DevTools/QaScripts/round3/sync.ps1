# Copies Assets / ProjectSettings of BE_dev into every client copy and compiles each one once (one Unity at a time).
param([string[]]$Copies = @('BE_dev_c','BE_dev_c2','BE_dev_c3','BE_dev_c4','BE_dev_late'), [switch]$Warm)
$root = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
foreach ($c in $Copies) {
  robocopy "$root\BE_dev\Assets" "$root\$c\Assets" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  robocopy "$root\BE_dev\ProjectSettings" "$root\$c\ProjectSettings" /MIR /MT:16 /NFL /NDL /NJH /NJS /NP | Out-Null
  Copy-Item "$root\BE_dev\Packages\manifest.json" "$root\$c\Packages\manifest.json" -Force
  Copy-Item "$root\BE_dev\Packages\packages-lock.json" "$root\$c\Packages\packages-lock.json" -Force
  if ($Warm) {
    $p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',"$root\$c",'-executeMethod','BuildingEscape.EditorTools.NetDevTest.RulesTest','-logFile',"$root\warm_$c.log" -PassThru -Wait
    $errs = (Select-String -Path "$root\warm_$c.log" -Pattern 'error CS').Count
    "$c warmed: exit=$($p.ExitCode) compile errors=$errs"
  } else { "$c synced" }
}
