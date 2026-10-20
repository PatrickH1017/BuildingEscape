# The BUILT game plays itself, from the unzipped send copy (a fresh folder, as a friend would have it): solo (menu -> docks -> floors 1-4 -> demo complete),
# then host + client over the loopback (two built players). Summary: C:\bqs\final_ap_summary.txt. Nothing else may use the loopback port or %TEMP%\be_sig_* meanwhile.
# Usage: .\final_autopilot.ps1 [-Tag fa1] [-Exe C:\bqs\Unzipped_final\Playtest_2026-10-17_final\EmployeesTakeRevenge.exe]
param([string]$Tag = 'fa1', [string]$Exe = 'C:\bqs\Unzipped_final\Playtest_2026-10-17_final\EmployeesTakeRevenge.exe')
$here = 'C:\bqs'
$sum = "$here\final_ap_summary.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
Set-Content -Path $sum -Value "Built-player autopilot $Tag started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') on $Exe" -Encoding UTF8
if (-not (Test-Path $Exe)) { Out-Sum 'STOPPED: no exe'; return }

# solo
$ap1 = "$here\${Tag}_ap_solo"; if (Test-Path $ap1) { Remove-Item $ap1 -Recurse -Force }; New-Item -ItemType Directory -Force $ap1 | Out-Null
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = & "$here\hd.ps1" -Exe $Exe -Arguments "-screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -be-autopilot -be-out `"$ap1`" -logFile `"$here\${Tag}_ap_solo_player.log`"" -TimeoutSeconds 900
$rep = "$ap1\autopilot_report.txt"
$pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
$fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
$fin = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^finished').Line } else { 'no report' }
Out-Sum "=== autopilot solo on the unzipped build: $r in $([int]$sw.Elapsed.TotalSeconds) s; PASS $pass FAIL $fail; $fin"
if (Test-Path $rep) { Select-String -Path $rep -Pattern '^FAIL|^logged|lobby song|^perf (menu|docks:|floor 1 with)' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(260, $_.Line.Length))) } }

# host + client
$lock = Join-Path $env:TEMP 'be_mp_busy.txt'
Set-Content -Path $lock -Value "agent5 final_autopilot.ps1 $(Get-Date -Format 'HH:mm:ss')" -Encoding ASCII
$apH = "$here\${Tag}_ap_host"; $apC = "$here\${Tag}_ap_client"
foreach ($d in $apH, $apC) { if (Test-Path $d) { Remove-Item $d -Recurse -Force }; New-Item -ItemType Directory -Force $d | Out-Null }
$argsH = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net host -be-out `"$apH`" -logFile `"$here\${Tag}_ap_host_player.log`""
$argsC = "-screen-width 1280 -screen-height 720 -screen-fullscreen 0 -be-autopilot -be-autopilot-net client -be-out `"$apC`" -logFile `"$here\${Tag}_ap_client_player.log`""
$sw = [Diagnostics.Stopwatch]::StartNew()
$jobH = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', "& '$here\hd.ps1' -Exe '$Exe' -Arguments '$argsH' -TimeoutSeconds 900 -Desktop ClaudeHiddenH *> '$here\${Tag}_ap_host_hd.txt'" -WindowStyle Hidden -PassThru
Start-Sleep -Seconds 12
$jobC = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', "& '$here\hd.ps1' -Exe '$Exe' -Arguments '$argsC' -TimeoutSeconds 900 -Desktop ClaudeHiddenC *> '$here\${Tag}_ap_client_hd.txt'" -WindowStyle Hidden -PassThru
$jobH.WaitForExit(1000000) | Out-Null; $jobC.WaitForExit(1000000) | Out-Null
Remove-Item $lock -Force -ErrorAction SilentlyContinue
foreach ($who in 'host', 'client') {
  $d = if ($who -eq 'host') { $apH } else { $apC }
  $rep = "$d\autopilot_report.txt"
  $pass = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^PASS').Count } else { 0 }
  $fail = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^FAIL').Count } else { -1 }
  $fin = if (Test-Path $rep) { (Select-String -Path $rep -Pattern '^finished').Line } else { 'no report' }
  Out-Sum "=== autopilot $who (two built players, loopback) in $([int]$sw.Elapsed.TotalSeconds) s; PASS $pass FAIL $fail; $fin"
  if (Test-Path $rep) { Select-String -Path $rep -Pattern '^FAIL|^logged' | ForEach-Object { Out-Sum ('    ' + $_.Line.Substring(0, [Math]::Min(260, $_.Line.Length))) } }
}
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"
