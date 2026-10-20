# Compile BE_dev (restoring the package sources first), run the pure tests, then copy the whole compiled project to every client copy.
param([string]$Method = 'BuildingEscape.EditorTools.NetDevTest.PolicyTest', [string]$Tag = 'build', [switch]$NoClients)
$root = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
robocopy "$real\Library\PackageCache" "$root\BE_dev\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$log = "$root\$Tag.log"
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',"$root\BE_dev",'-executeMethod',$Method,'-logFile',$log -PassThru
if (-not $p.WaitForExit(900000)) { Stop-Process -Id $p.Id -Force; "compile TIMED OUT" }
$errs = (Select-String -Path $log -Pattern 'error CS').Count
$pass = (Select-String -Path $log -Pattern '\bPASS\b').Count
$fail = (Select-String -Path $log -Pattern '\bFAIL\b').Count
"dev: exit=$($p.ExitCode) compile errors=$errs pure PASS=$pass FAIL=$fail"
if ($errs -gt 0) { Select-String -Path $log -Pattern 'error CS' | Select-Object -First 8 | ForEach-Object { $_.Line }; return }
if ($NoClients) { return }
foreach ($c in 'BE_dev_c','BE_dev_c2','BE_dev_c3','BE_dev_c4','BE_dev_late') {
  robocopy "$root\BE_dev" "$root\$c" /MIR /MT:16 /XJ /XD Temp Logs /NFL /NDL /NJH /NJS /NP | Out-Null
  robocopy "$real\Library\PackageCache" "$root\$c\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  "$c mirrored"
}
