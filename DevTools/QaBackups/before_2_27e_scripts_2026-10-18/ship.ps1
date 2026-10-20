# Ship: apply the verified QA tree to the real project, check they are identical, build, make the send copy + zip, unzip it and let the built game play itself.
# Usage: .\ship.ps1 -Name 2026-10-18d -Raw Final13_2026-10-18 -Backup before_round8d_apply_2026-10-18 -Tag fd1      (summary: C:\bqs\ship_summary.txt)
param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Raw, [Parameter(Mandatory)][string]$Backup, [string]$Tag = 'fs1')
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$sum = 'C:\bqs\ship_summary.txt'
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Ship $Name started $(Get-Date -Format 'HH:mm:ss')" -Encoding UTF8
$a = & 'C:\bqs\etr_apply3.ps1' -Backup "$real\DevTools\QaBackups\$Backup" | Select-Object -Last 1
Out-Sum "apply: $a"
$r = & 'C:\bqs\cmp_trees.ps1' -A $real -B 'C:\bq' 2>&1
if (-not (($r | Select-String 'DIFFER \(0\)') -and ($r | Select-String 'ONLY IN A \(0\)'))) { Out-Sum 'STOPPED: the real project and the QA tree differ'; Out-Sum (($r | Select-Object -First 12) -join ' | '); Out-Sum 'finished'; return }
Out-Sum 'trees identical'
Remove-Item C:\bqs\final_summary.txt -ErrorAction SilentlyContinue
& 'C:\bqs\final_build.ps1' -Raw $Raw -Send "Playtest_$Name"
foreach ($l in (Get-Content C:\bqs\final_summary.txt)) { Out-Sum $l }
$zip = "$real\Builds\Playtest_$Name.zip"
if (-not (Test-Path $zip)) { Out-Sum 'STOPPED: no zip'; Out-Sum 'finished'; return }
$dest = "C:\bqs\Unzipped_$Tag"
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Expand-Archive -Path $zip -DestinationPath $dest -Force
Remove-Item C:\bqs\final_ap_summary.txt -ErrorAction SilentlyContinue
& 'C:\bqs\final_autopilot.ps1' -Tag $Tag -Exe "$dest\Playtest_$Name\EmployeesTakeRevenge.exe"
foreach ($l in (Get-Content C:\bqs\final_ap_summary.txt)) { if ($l -notmatch 'perf ') { Out-Sum $l } }
Out-Sum 'finished'
