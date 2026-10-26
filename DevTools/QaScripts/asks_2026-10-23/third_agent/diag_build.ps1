# A quick build of the SCRATCH copy (C:\bd: the real project plus what is being tried) to look at how the built game plays itself, without the send copy, the scan and the zip: sync C:\bd into the build copy C:\bqfinal, build an IL2CPP player into
# C:\bqs\diag_build (replaced every time: it is only a test build, never sent), then run the built-player autopilot on it. Summary: C:\bqs\final_ap_summary.txt (and C:\bqs\<Tag>_ap_*). Usage: .\diag_build.ps1 [-Tag dg1]
param([string]$Tag = 'dg1')
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$proj = 'C:\bqfinal'; $out = 'C:\bqs\diag_build'; $log = 'C:\bqs\diag_build.log'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
foreach ($d in 'Assets', 'Packages', 'ProjectSettings') { robocopy "C:\bd\$d" "$proj\$d" /MIR /MT:16 /XJ /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null }
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-nographics', '-projectPath', $proj, '-executeMethod', 'BuildingEscape.EditorTools.DemoBuild.Build', '-buildOutput', $out, '-buildBackend', 'il2cpp', '-logFile', $log -PassThru
if (-not $p.WaitForExit(3300000)) { Stop-Process -Id $p.Id -Force; 'build TIMED OUT' }
$line = Select-String -Path $log -Pattern '\[DemoBuild\] result' | Select-Object -Last 1
"build: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; $(if ($line) { $line.Line.Trim() } else { 'NO RESULT LINE' }); compile errors: $((Select-String -Path $log -Pattern 'error CS').Count)"
if (-not (Test-Path "$out\EmployeesTakeRevenge.exe")) { 'STOPPED: no exe'; return }
Remove-Item C:\bqs\final_ap_summary.txt -ErrorAction SilentlyContinue
& 'C:\bqs\final_autopilot.ps1' -Tag $Tag -Exe "$out\EmployeesTakeRevenge.exe"
Get-Content C:\bqs\final_ap_summary.txt
