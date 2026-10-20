# The final build of the night: sync the real project into the pristine build copy C:\bqfinal, build the Windows player, make the send copy
# (no DoNotShip folder, a README for the friends) and a zip. Summary: C:\bqs\final_summary.txt.
# Usage: .\final_build.ps1 [-Raw Final9_2026-10-17] [-Send Playtest_2026-10-17_final]
param([string]$Raw = 'Final9_2026-10-17', [string]$Send = 'Playtest_2026-10-17_final')
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$proj = 'C:\bqfinal'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$builds = "$real\Builds"
$rawDir = "$builds\$Raw"; $sendDir = "$builds\$Send"; $zip = "$builds\$Send.zip"
$sum = 'C:\bqs\final_summary.txt'
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Final build started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
if ((Test-Path $rawDir) -or (Test-Path $sendDir) -or (Test-Path $zip)) { Out-Sum "STOPPED: $rawDir, $sendDir or $zip already exists (not overwriting a build)"; return }

# 1: the real project into the pristine build copy (and the one edited file into the test copy C:\bq)
foreach ($d in 'Assets', 'Packages', 'ProjectSettings') { robocopy "$real\$d" "$proj\$d" /MIR /MT:16 /XJ /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null }
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
Out-Sum "synced $(Get-Date -Format 'HH:mm:ss')"

# 2: the build
New-Item -ItemType Directory -Force $rawDir | Out-Null
$log = "$proj\build_final9.log"
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-nographics', '-projectPath', $proj, '-executeMethod', 'BuildingEscape.EditorTools.DemoBuild.Build', '-buildOutput', $rawDir, '-logFile', $log -PassThru
if (-not $p.WaitForExit(1500000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'build TIMED OUT' }
$line = Select-String -Path $log -Pattern '\[DemoBuild\] result' | Select-Object -Last 1
$cserr = (Select-String -Path $log -Pattern 'error CS').Count
Out-Sum "=== build: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; $(if ($line) { $line.Line.Trim() -replace '\\.*$', '' } else { 'NO RESULT LINE' }); compile errors in the log: $cserr"
if (-not (Test-Path "$rawDir\EmployeesTakeRevenge.exe")) { Out-Sum 'STOPPED: no exe'; return }

# 3: the send copy: without the Burst debug folder, with a README
robocopy $rawDir $sendDir /MIR /MT:16 /XD '*DoNotShip' /NFL /NDL /NJH /NJS /NP | Out-Null
Copy-Item 'C:\bqs\README_PLAYTEST.txt' "$sendDir\README.txt" -Force
$bad = @(Get-ChildItem $sendDir -Recurse -Directory -Filter '*DoNotShip*')
$mb = [int]((Get-ChildItem $sendDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB)
Out-Sum "=== send copy: $sendDir = $mb MB; do-not-ship folders in it: $($bad.Count); exe $(Test-Path "$sendDir\EmployeesTakeRevenge.exe"); README $(Test-Path "$sendDir\README.txt")"

# 4: a zip of the send copy
Compress-Archive -Path $sendDir -DestinationPath $zip -CompressionLevel Optimal
Out-Sum "=== zip: $zip = $([int]((Get-Item $zip).Length / 1MB)) MB"
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"
