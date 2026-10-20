# Runs multiplayer scenarios for the Q/A: one Unity process per role (host = my BE_qa, the clients = the five copies in agent 1's scratchpad that
# qa_sync_clients.ps1 mirrored from BE_qa). A summary of every run goes to qa_summary_<Prefix>.txt.
# Usage: .\qa_mp.ps1 -Prefix m1 -Runs 'Armed:host,c1', 'GunNet:host,c1', 'Qa:host,c1,c2,c3,c4,late6:5'   (entry = Method:roles[:players])
param([string[]]$Runs = @('Armed:host,c1', 'GunNet:host,c1', 'DemoNet:host,c1', 'ShaftNet:host,c1'), [string]$Prefix = 'm1', [int]$TimeoutSeconds = 900)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$old = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad'
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$dirs = @{ host = "$here\BE_qa"; c1 = "$old\BE_dev_c2"; c2 = "$old\BE_dev_c2"; c3 = "$old\BE_dev_c3"; c4 = "$old\BE_dev_c4"; late = "$old\BE_dev_late"; late6 = "$old\BE_dev_late" }
$sum = "$here\qa_summary_$Prefix.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8; $t }
Set-Content -Path $sum -Value "Multiplayer Q/A $Prefix, started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
$n = 0
foreach ($entry in $Runs) {
  $a = $entry.Split(':'); $method = $a[0]; $roles = $a[1].Split(','); $players = if ($a.Length -gt 2) { [int]$a[2] } else { $roles.Count }
  $n++; $runId = "$Prefix$n"
  $dirs.c1 = if ($roles -contains 'c2') { "$old\BE_dev_c" } else { "$old\BE_dev_c2" }      # (every role needs a copy of its own)
  # only one multi-process scenario at a time on this machine (loopback port, %TEMP%\be_sig_*, project locks): wait for the other session, then take the lock
  $lock = Join-Path $env:TEMP 'be_mp_busy.txt'
  $waited = 0
  while ($waited -lt 1800) {
    $busyFile = (Test-Path $lock) -and ((Get-Item $lock).LastWriteTime -gt (Get-Date).AddMinutes(-20))
    $busyProc = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match 'executeMethod' }).Count -gt 0
    if (-not $busyFile -and -not $busyProc) { break }
    Start-Sleep -Seconds 10; $waited += 10
  }
  Set-Content -Path $lock -Value "bad83f09 qa_mp.ps1 $method $(Get-Date -Format 'HH:mm:ss')" -Encoding ASCII
  $env:BE_RUN = $runId; $env:BE_QA_RUN = $runId; $env:BE_PLAYERS = "$players"
  $procs = @{}; $logs = @{}
  $sw = [Diagnostics.Stopwatch]::StartNew()
  foreach ($r in $roles) {
    robocopy "$real\Library\PackageCache" "$($dirs[$r])\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
    $env:BE_QA_ROLE = $r
    $logs[$r] = "$here\mp_${method}_${runId}_$r.log"
    $procs[$r] = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-projectPath',$dirs[$r],'-executeMethod',"BuildingEscape.EditorTools.NetDevHarness.$method",'-logFile',$logs[$r] -PassThru
    if ($r -eq 'host') { Start-Sleep -Seconds 20 }
  }
  $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
  while ((Get-Date) -lt $deadline -and ($procs.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Seconds 3 }
  $status = @()
  foreach ($r in $roles) { $p = $procs[$r]; if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; $status += "$r TIMED OUT" } else { $status += "$r exit=$($p.ExitCode)" } }
  Remove-Item (Join-Path $env:TEMP 'be_mp_busy.txt') -Force -ErrorAction SilentlyContinue
  Out-Sum "=== $method ($($roles -join ',')) in $([int]$sw.Elapsed.TotalSeconds) s: $($status -join '; ')" | Out-Null
  foreach ($r in $roles) {
    $log = $logs[$r]
    $fails = @(Select-String -Path $log -Pattern '\bFAIL\b' | Where-Object { $_.Line -notmatch '\bPASS\b' } | ForEach-Object { $_.Line.Trim() -replace '^\[NetDev\] ', '' })
    $passes = (Select-String -Path $log -Pattern '\bPASS\b').Count
    $exc = @(Select-String -Path $log -Pattern 'NullReferenceException|IndexOutOfRangeException|KeyNotFoundException|MissingReferenceException|DESYNC' | Where-Object { $_.Line -notmatch 'UIElements|UIR\.|RenderChainCommand' }).Count
    $done = Select-String -Path $log -Pattern 'DONE' | Select-Object -Last 1
    Out-Sum "   $r : PASS $passes, FAIL $($fails.Count), exceptions/desync lines $exc; $(if ($done) { $done.Line.Trim() -replace '^\[NetDev\] ', '' } else { 'NO DONE LINE' })" | Out-Null
    foreach ($f in ($fails | Select-Object -First 12)) { Out-Sum ('        ' + $(if ($f.Length -gt 220) { $f.Substring(0, 220) } else { $f })) | Out-Null }
  }
}
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')" | Out-Null
