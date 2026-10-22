# Q/A runner for the private QA copy (BE_qa): compile + pure tests, then single-process Play Mode scenarios one after another.
# A scenario with a trailing ":gfx" runs with graphics (Floors and Shaft pictures need them). The summary goes to qa_summary_<Prefix>.txt (read that, not the logs).
# Usage: .\qa_regress.ps1 -Prefix q1 [-Methods Offline,Demo,Shaft:gfx] [-Project BE_qa] [-NoBuild]
param([string[]]$Methods = @('Offline', 'Demo', 'PewLoot', 'Pew', 'Shaft:gfx', 'Floors:gfx', 'Doors', 'Outpost', 'Sneak'),
      [string]$Prefix = 'q1', [string]$Project = 'BE_qa', [switch]$NoBuild)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = if ($Project -eq 'BE_dev') { 'C:\bd' } else { "$here\$Project" }          # (the short junction: deep package files do not open on a long path)
$sum = "$here\qa_summary_$Prefix.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8; $t }
Set-Content -Path $sum -Value "Q/A run $Prefix on $Project, started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8
function Refill-Cache { robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null }

if (-not $NoBuild) {
  Refill-Cache
  $log = "$here\${Prefix}_build.log"
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-quit','-projectPath',$proj,'-executeMethod','BuildingEscape.EditorTools.NetDevTest.PolicyTest','-logFile',$log -PassThru
  if (-not $p.WaitForExit(900000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'compile TIMED OUT' }
  $errs = (Select-String -Path $log -Pattern 'error CS').Count
  $warn = (Select-String -Path $log -Pattern 'warning CS').Count
  $pass = (Select-String -Path $log -Pattern '\[NetTest\].*\bPASS\b').Count
  $fail = @(Select-String -Path $log -Pattern '\[NetTest\].*\bFAIL\b').Count
  Out-Sum "=== compile + pure tests: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; compile errors=$errs; compiler warnings=$warn; pure PASS=$pass FAIL=$fail"
  if ($errs -gt 0) { Select-String -Path $log -Pattern 'error CS' | Select-Object -First 10 | ForEach-Object { Out-Sum ('   ' + $_.Line.Trim()) } | Out-Null; Out-Sum 'STOPPED: the project does not compile'; return }
}

foreach ($entry in $Methods) {
  $name = $entry.Split(':')[0]; $gfx = $entry.EndsWith(':gfx')
  Refill-Cache
  $log = "$here\${Prefix}_$name.log"
  $args = @('-batchmode') + $(if (-not $gfx) { '-nographics' }) + @('-projectPath', $proj, '-executeMethod', "BuildingEscape.EditorTools.NetDevHarness.$name", '-logFile', $log)
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process -FilePath $unity -ArgumentList $args -PassThru
  if (-not $p.WaitForExit(1200000)) { Stop-Process -Id $p.Id -Force; $r = 'TIMED OUT (killed)' } else { $r = "exit=$($p.ExitCode)" }
  $fails = @(Select-String -Path $log -Pattern '\bFAIL\b' | Where-Object { $_.Line -notmatch '\bPASS\b' } | ForEach-Object { $_.Line.Trim() -replace '^\[NetDev\] ', '' })
  $passes = (Select-String -Path $log -Pattern '\bPASS\b').Count
  $exc = @(Select-String -Path $log -Pattern 'NullReferenceException|IndexOutOfRangeException|ArgumentException|InvalidOperationException|KeyNotFoundException|MissingReferenceException|UnassignedReferenceException' | Where-Object { $_.Line -notmatch 'UIElements|UIR\.|RenderChainCommand' }).Count
  $done = (Select-String -Path $log -Pattern '\] DONE|DONE offline|DONE fails|all done' | Select-Object -Last 1)
  Out-Sum "=== $name : $r in $([int]$sw.Elapsed.TotalSeconds) s; PASS lines $passes; FAIL lines $($fails.Count); exceptions in log $exc; $(if ($done) { $done.Line.Trim() -replace '^\[NetDev\] ', '' } else { 'NO DONE LINE' })" | Out-Null
  foreach ($f in ($fails | Select-Object -First 25)) { Out-Sum ('   ' + $(if ($f.Length -gt 230) { $f.Substring(0, 230) } else { $f })) | Out-Null }
}
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')" | Out-Null
