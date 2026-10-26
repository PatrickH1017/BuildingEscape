# Two-machine Q/A runner for the 2026-10-23 scratch copy: the host is C:\bd, the second machine C:\bdc (a copy of the same project in agent 1's scratchpad). Both are Unity batch processes on this machine talking over the loopback.
# -Sync mirrors C:\bd's Assets / ProjectSettings / Packages manifest into C:\bdc and compiles it once first (so the run itself does not wait for a compile).
# Usage: .\qa_mp_bd.ps1 -Method DevSpectate -Prefix m1 [-Sync] ; the summary goes to qa_summary_<Prefix>.txt (read that, not the logs).
param([Parameter(Mandatory)][string]$Method, [string]$Prefix = 'm1', [switch]$Sync, [int]$TimeoutSeconds = 900)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$hostDir = 'C:\bd'; $clientDir = 'C:\bdc'
$sum = "$here\qa_summary_$Prefix.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8; $t }
Set-Content -Path $sum -Value "Two-machine Q/A $Prefix ($Method), started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
foreach ($d in @($hostDir, $clientDir)) { robocopy "$real\Library\PackageCache" "$d\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null }

if ($Sync) {
  robocopy "$hostDir\Assets" "$clientDir\Assets" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  robocopy "$hostDir\ProjectSettings" "$clientDir\ProjectSettings" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  Copy-Item "$hostDir\Packages\manifest.json" "$clientDir\Packages\manifest.json" -Force
  Copy-Item "$hostDir\Packages\packages-lock.json" "$clientDir\Packages\packages-lock.json" -Force
  $warm = "$here\${Prefix}_client_warm.log"
  $sw0 = [Diagnostics.Stopwatch]::StartNew()
  $pw = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',$clientDir,'-executeMethod','BuildingEscape.EditorTools.NetDevTest.PolicyTest','-logFile',$warm -PassThru
  if (-not $pw.WaitForExit(1200000)) { Stop-Process -Id $pw.Id -Force; Out-Sum 'client compile TIMED OUT' }
  Out-Sum "=== client compiled: exit=$($pw.ExitCode) in $([int]$sw0.Elapsed.TotalSeconds) s; compile errors=$((Select-String -Path $warm -Pattern 'error CS').Count)"
}

$roles = @(@{ name = 'host'; dir = $hostDir }, @{ name = 'c1'; dir = $clientDir })
$procs = @{}; $logs = @{}
$env:BE_RUN = $Prefix; $env:BE_QA_RUN = $Prefix; $env:BE_PLAYERS = '2'
$sw = [Diagnostics.Stopwatch]::StartNew()
foreach ($r in $roles) {
  $env:BE_QA_ROLE = $r.name
  $logs[$r.name] = "$here\mp_${Method}_${Prefix}_$($r.name).log"
  $procs[$r.name] = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-projectPath',$r.dir,'-executeMethod',"BuildingEscape.EditorTools.NetDevHarness.$Method",'-logFile',$logs[$r.name] -PassThru
  if ($r.name -eq 'host') { Start-Sleep -Seconds 20 }
}
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and ($procs.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Seconds 3 }
$status = @()
foreach ($r in $roles) { $p = $procs[$r.name]; if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; $status += "$($r.name) TIMED OUT" } else { $status += "$($r.name) exit=$($p.ExitCode)" } }
Out-Sum "=== $Method in $([int]$sw.Elapsed.TotalSeconds) s: $($status -join '; ')" | Out-Null
foreach ($r in $roles) {
  $log = $logs[$r.name]
  $fails = @(Select-String -Path $log -Pattern '\bFAIL\b' | Where-Object { $_.Line -notmatch '\bPASS\b' -or $_.Line -match '\] FAIL ' } | ForEach-Object { $_.Line.Trim() -replace '^\[NetDev\] ', '' })
  $passes = (Select-String -Path $log -Pattern '\] PASS ').Count
  $exc = @(Select-String -Path $log -Pattern 'NullReferenceException|IndexOutOfRangeException|KeyNotFoundException|MissingReferenceException|ArgumentException|InvalidOperationException|DESYNC' | Where-Object { $_.Line -notmatch 'UIElements|UIR\.|RenderChainCommand' })
  $done = Select-String -Path $log -Pattern 'DONE' | Select-Object -Last 1
  Out-Sum "   $($r.name) : PASS $passes, FAIL $($fails.Count), exception/desync lines $($exc.Count); $(if ($done) { $done.Line.Trim() -replace '^\[NetDev\] ', '' } else { 'NO DONE LINE' })" | Out-Null
  foreach ($f in ($fails | Select-Object -First 15)) { Out-Sum ('        ' + $(if ($f.Length -gt 230) { $f.Substring(0, 230) } else { $f })) | Out-Null }
  foreach ($e in ($exc | Select-Object -First 5)) { Out-Sum ('        ! ' + $(if ($e.Line.Length -gt 230) { $e.Line.Substring(0, 230) } else { $e.Line.Trim() })) | Out-Null }
}
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')" | Out-Null
