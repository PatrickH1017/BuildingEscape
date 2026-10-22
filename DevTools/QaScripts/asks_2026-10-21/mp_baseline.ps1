# Runs two-machine scenarios one after another through runmp.ps1 and prints a short summary of each (PASS/FAIL lines of both logs).
# Usage: .\mp_baseline.ps1 -Prefix base -Methods GunNet,DemoNet,OutpostNet
param([string[]]$Methods = @('GunNet', 'DemoNet', 'OutpostNet'), [string]$Prefix = 'base', [int]$TimeoutSeconds = 600)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$n = 0
foreach ($m in $Methods) {
  $n++; $runId = "$Prefix$n"
  $lock = Join-Path $env:TEMP 'be_mp_busy.txt'
  $waited = 0
  while ($waited -lt 1800) {
    $busyFile = (Test-Path $lock) -and ((Get-Item $lock).LastWriteTime -gt (Get-Date).AddMinutes(-20))
    $busyProc = @(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object { $_.CommandLine -match 'executeMethod' }).Count -gt 0
    if (-not $busyFile -and -not $busyProc) { break }
    Start-Sleep -Seconds 10; $waited += 10
  }
  Set-Content -Path $lock -Value "asks1021 mp_baseline.ps1 $m $(Get-Date -Format 'HH:mm:ss')" -Encoding ASCII
  $res = & "$root\runmp.ps1" -Method $m -Roles @('host', 'c1') -Players 2 -RunId $runId -TimeoutSeconds $TimeoutSeconds
  Remove-Item $lock -Force -ErrorAction SilentlyContinue
  "=== $m : " + (($res | ForEach-Object { "$_" }) -join '; ')
  foreach ($r in 'host', 'c1') {
    $log = "$root\mp_${m}_${runId}_$r.log"
    if (-not (Test-Path $log)) { "    $r : no log"; continue }
    $fails = @(Select-String -Path $log -Pattern '\bFAIL\b' | Where-Object { $_.Line -notmatch '\bPASS\b' } | ForEach-Object { $_.Line.Trim() -replace '^\[NetDev\] ', '' })
    $passes = (Select-String -Path $log -Pattern '\bPASS\b').Count
    $exc = @(Select-String -Path $log -Pattern 'NullReferenceException|IndexOutOfRangeException|KeyNotFoundException|MissingReferenceException|DESYNC' | Where-Object { $_.Line -notmatch 'UIElements|UIR\.|RenderChainCommand' }).Count
    $done = Select-String -Path $log -Pattern 'DONE' | Select-Object -Last 1
    "    $r : PASS $passes, FAIL $($fails.Count), exceptions/desync $exc; $(if ($done) { $done.Line.Trim() -replace '^\[NetDev\] ', '' } else { 'NO DONE LINE' })"
    foreach ($f in ($fails | Select-Object -First 8)) { '        ' + $(if ($f.Length -gt 230) { $f.Substring(0, 230) } else { $f }) }
  }
}
"finished $(Get-Date -Format 'HH:mm:ss')"
