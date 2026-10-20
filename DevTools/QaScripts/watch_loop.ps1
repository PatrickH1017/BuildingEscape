# Repeats the BUILT game's host + client loopback test until the client's safe holds a gold watch (30 % per run), so that the watch's pick-up over the network is exercised
# in the built player. Same launch as the host + client part of final_autopilot.ps1. Summary: C:\bqs\watch_loop_summary.txt (read it with cat, never tail -F).
# Usage: .\watch_loop.ps1 -Exe C:\bqs\Unzipped_fj1\Playtest_2026-10-18j\EmployeesTakeRevenge.exe [-MaxTries 8]
param([Parameter(Mandatory)][string]$Exe, [int]$MaxTries = 8)
$here = 'C:\bqs'
$sum = "$here\watch_loop_summary.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Watch loop started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') on $Exe (up to $MaxTries tries)" -Encoding UTF8
if (-not (Test-Path $Exe)) { Out-Sum 'STOPPED: no exe'; return }
$lock = Join-Path $env:TEMP 'be_mp_busy.txt'
for ($try = 1; $try -le $MaxTries; $try++) {
  $Tag = "fjw$try"
  Set-Content -Path $lock -Value "watch_loop.ps1 try $try $(Get-Date -Format 'HH:mm:ss')" -Encoding ASCII
  $apH = "$here\${Tag}_ap_host"; $apC = "$here\${Tag}_ap_client"
  foreach ($d in $apH, $apC) { if (Test-Path $d) { Remove-Item $d -Recurse -Force }; New-Item -ItemType Directory -Force $d | Out-Null }
  $argsH = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net host -be-out `"$apH`" -logFile `"$here\${Tag}_ap_host_player.log`""
  $argsC = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net client -be-out `"$apC`" -logFile `"$here\${Tag}_ap_client_player.log`""
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $jobH = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', "& '$here\hd.ps1' -Exe '$Exe' -Arguments '$argsH' -TimeoutSeconds 900 -Desktop ClaudeHiddenH *> '$here\${Tag}_ap_host_hd.txt'" -WindowStyle Hidden -PassThru
  Start-Sleep -Seconds 12
  $jobC = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', "& '$here\hd.ps1' -Exe '$Exe' -Arguments '$argsC' -TimeoutSeconds 900 -Desktop ClaudeHiddenC *> '$here\${Tag}_ap_client_hd.txt'" -WindowStyle Hidden -PassThru
  $jobH.WaitForExit(1000000) | Out-Null; $jobC.WaitForExit(1000000) | Out-Null
  $line = "try ${try}: $([int]$sw.Elapsed.TotalSeconds) s"
  $got = $false
  foreach ($who in 'host', 'client') {
    $d = if ($who -eq 'host') { $apH } else { $apC }
    $rep = "$d\autopilot_report.txt"
    $pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
    $fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
    $line += "; $who PASS $pass FAIL $fail"
    if ($fail -ne 0) { foreach ($f in (Select-String -Path $rep -Pattern '^FAIL|^logged')) { Out-Sum ('    ' + $f.Line.Substring(0, [Math]::Min(260, $f.Line.Length))) } }
  }
  $cw = Select-String -Path "$apC\autopilot_report.txt" -Pattern 'client: the gold watch is in my pack' -ErrorAction SilentlyContinue
  $hw = Select-String -Path "$apH\autopilot_report.txt" -Pattern "host: the client's gold watch is in his pack" -ErrorAction SilentlyContinue
  if ($cw) { $line += "; CLIENT WATCH CHECK: $($cw.Line.Substring(0, [Math]::Min(120, $cw.Line.Length)))" } else { $line += '; no watch in the client''s safe this time' }
  if ($hw) { $line += "; HOST SAW IT: $($hw.Line.Substring(0, [Math]::Min(100, $hw.Line.Length)))" }
  Out-Sum $line
  if ($cw -and $hw) { $got = $true }
  if ($got) { break }
  # a failing try ends the loop at once: it has to be looked at
  $anyFail = $false
  foreach ($d in $apH, $apC) { if (Test-Path "$d\autopilot_report.txt") { if ((Select-String -Path "$d\autopilot_report.txt" -Pattern '^FAIL').Count -ne 0) { $anyFail = $true } } else { $anyFail = $true } }
  if ($anyFail) { Out-Sum 'STOPPED: a try failed or left no report'; break }
}
Remove-Item $lock -Force -ErrorAction SilentlyContinue
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"
