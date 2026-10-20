# Builds the player from BE_qa and lets the BUILT game start a run the way PLAY SOLO does (the run scene with no network session) and watch the run-start
# cinematic: played to its end, and skipped with Space held. Summary: qa_summary_<Tag>.txt, pictures in <Tag>_offline / <Tag>_offlineskip.
# Usage: .\qa_offline.ps1 -Tag oe1 [-Exe <a built EmployeesTakeRevenge.exe: nothing is built>]
param([string]$Tag = 'oe1', [string]$Exe = '')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\BE_qa"
$sum = "$here\qa_summary_$Tag.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8; Write-Output $t }
Set-Content -Path $sum -Value "Offline Intro Q/A $Tag started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8

if ($Exe -ne '') { $exe = $Exe; Out-Sum "=== testing the given build: $exe" }
else {
  robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  $out = "$here\Builds\Intro_shots"
  New-Item -ItemType Directory -Force $out | Out-Null
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process -FilePath $unity -ArgumentList '-batchmode', '-nographics', '-projectPath', $proj, '-executeMethod', 'BuildingEscape.EditorTools.DemoBuild.Build', '-buildOutput', $out, '-logFile', "$here\${Tag}_build.log" -PassThru
  if (-not $p.WaitForExit(1500000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'build TIMED OUT' }
  $line = Select-String -Path "$here\${Tag}_build.log" -Pattern '\[DemoBuild\] result' | Select-Object -Last 1
  Out-Sum "=== build: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; $(if ($line) { $line.Line.Trim() -replace '\\.*$', '' } else { 'NO RESULT LINE' })"
  $errs = @(Select-String -Path "$here\${Tag}_build.log" -Pattern 'error CS')
  Out-Sum "    compile errors in the build log: $($errs.Count)"
  $errs | Select-Object -First 12 | ForEach-Object { Out-Sum ('    ' + $_.Line.Trim()) }
  $exe = "$out\EmployeesTakeRevenge.exe"
}
if (-not (Test-Path $exe)) { Out-Sum 'STOPPED: no exe'; return }

foreach ($mode in @(@('offline', ''), @('offlineskip', '-be-intro-skip'))) {
  $dir = "$here\${Tag}_$($mode[0])"; New-Item -ItemType Directory -Force $dir | Out-Null
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $r = & "$here\hd.ps1" -Exe $exe -Arguments "-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -be-autopilot -be-autopilot-net introoffline $($mode[1]) -be-out `"$dir`" -logFile `"$here\${Tag}_$($mode[0])_player.log`"" -TimeoutSeconds 300
  $rep = "$dir\autopilot_report.txt"
  $pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
  $fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
  $fin = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^finished').Line } else { 'no report' }
  Out-Sum "=== PLAY SOLO, $($mode[0]) ($r, $([int]$sw.Elapsed.TotalSeconds) s): PASS $pass FAIL $fail; $fin"
  if (Test-Path $rep) { Select-String -Path $rep -Pattern '^FAIL|^logged' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(230, $_.Line.Length))) } }
}
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"
