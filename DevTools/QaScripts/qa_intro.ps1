# Builds the player from BE_qa and lets the BUILT game play the run-start cinematic (DEVELOPER_NOTES 2.19): solo to its end, solo with Space held (skip), the same for PLAY SOLO (offline),
# and host + client over the loopback (the client holds Space: 1 of 2). Summary: qa_summary_<Tag>.txt, pictures in <Tag>_solo / <Tag>_skip / <Tag>_host / <Tag>_client.
# Usage: .\qa_intro.ps1 -Tag in1
param([string]$Tag = 'in1', [string]$Exe = '')   # -Exe <a built EmployeesTakeRevenge.exe>: nothing is built, that exe is tested (e.g. the unzipped friends' build)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\BE_qa"
$sum = "$here\qa_summary_$Tag.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Intro Q/A $Tag started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8

# the build (skipped when a built exe is given)
if ($Exe -ne '') { $exe = $Exe; Out-Sum "=== testing the given build: $exe" } else {
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$out = "$here\Builds\Intro_$Tag"
New-Item -ItemType Directory -Force $out | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-projectPath',$proj,'-executeMethod','BuildingEscape.EditorTools.DemoBuild.Build','-buildOutput',$out,'-logFile',"$here\${Tag}_build.log" -PassThru
if (-not $p.WaitForExit(1500000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'build TIMED OUT' }
$line = Select-String -Path "$here\${Tag}_build.log" -Pattern '\[DemoBuild\] result' | Select-Object -Last 1
Out-Sum "=== build: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; $(if ($line) { $line.Line.Trim() -replace '\\.*$', '' } else { 'NO RESULT LINE' })"
Out-Sum "    compile errors in the build log: $((Select-String -Path "$here\${Tag}_build.log" -Pattern 'error CS').Count)"
$exe = "$out\EmployeesTakeRevenge.exe"
}
if (-not (Test-Path $exe)) { Out-Sum 'STOPPED: no exe'; return }

function Report([string]$what, [string]$dir) {
  $rep = "$dir\autopilot_report.txt"
  $pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
  $fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
  $fin = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^finished').Line } else { 'no report' }
  Out-Sum "=== $what : PASS $pass FAIL $fail; $fin"
  if (Test-Path $rep) { Select-String -Path $rep -Pattern '^FAIL|^logged' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(230, $_.Line.Length))) } }
}

# solo: played to its end, then solo: Space held
# (name, the -be-autopilot-net role, extra switch): a lobby of one (HOST LOCAL + START RUN) and PLAY SOLO (no network session), each played out and skipped
foreach ($mode in @(@('solo', 'introsolo', ''), @('skip', 'introsolo', '-be-intro-skip'), @('offline', 'introoffline', ''), @('offlineskip', 'introoffline', '-be-intro-skip'))) {
  $dir = "$here\${Tag}_$($mode[0])"; New-Item -ItemType Directory -Force $dir | Out-Null
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $r = & "$here\hd.ps1" -Exe $exe -Arguments "-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -be-autopilot -be-autopilot-net $($mode[1]) $($mode[2]) -be-out `"$dir`" -logFile `"$here\${Tag}_$($mode[0])_player.log`"" -TimeoutSeconds 400
  Report "intro $($mode[0]) ($r, $([int]$sw.Elapsed.TotalSeconds) s)" $dir
}

# host + client over the loopback (one multi-process thing at a time on this machine)
$lock = Join-Path $env:TEMP 'be_mp_busy.txt'
$waited = 0
while ($waited -lt 1800) {
  $busyFile = (Test-Path $lock) -and ((Get-Item $lock).LastWriteTime -gt (Get-Date).AddMinutes(-20))
  $busyProc = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match 'executeMethod' }).Count -gt 0
  if (-not $busyFile -and -not $busyProc) { break }
  Start-Sleep -Seconds 10; $waited += 10
}
Set-Content -Path $lock -Value "bad83f09 qa_intro.ps1 $(Get-Date -Format 'HH:mm:ss')" -Encoding ASCII
$apH = "$here\${Tag}_host"; $apC = "$here\${Tag}_client"
New-Item -ItemType Directory -Force $apH, $apC | Out-Null
$argsH = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net introhost -be-out `"$apH`" -logFile `"$here\${Tag}_host_player.log`""
$argsC = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net introclient -be-out `"$apC`" -logFile `"$here\${Tag}_client_player.log`""
$sw = [Diagnostics.Stopwatch]::StartNew()
$jobH = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-Command',"& '$here\hd.ps1' -Exe '$exe' -Arguments '$argsH' -TimeoutSeconds 600 -Desktop ClaudeHiddenH *> '$here\${Tag}_host_hd.txt'" -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 12
$jobC = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-Command',"& '$here\hd.ps1' -Exe '$exe' -Arguments '$argsC' -TimeoutSeconds 600 -Desktop ClaudeHiddenC *> '$here\${Tag}_client_hd.txt'" -WindowStyle Hidden -PassThru
$jobH.WaitForExit(700000) | Out-Null; $jobC.WaitForExit(700000) | Out-Null
Remove-Item $lock -Force -ErrorAction SilentlyContinue
Report "intro host (two built players, $([int]$sw.Elapsed.TotalSeconds) s)" $apH
Report "intro client (two built players)" $apC
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"
exit 0
