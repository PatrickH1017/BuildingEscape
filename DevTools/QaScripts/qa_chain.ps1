# The unattended Q/A chain on BE_qa: render audit (finds pink / error shaders), the picture scenarios on a hidden desktop (real IMGUI), then the
# multiplayer runs (qa_mp.ps1). Summary: qa_summary_<Prefix>.txt (the multiplayer part writes qa_summary_<Prefix>mp.txt).
# Usage: .\qa_chain.ps1 -Prefix c1 [-Skip audit,pics,mp] [-MpRuns 'Armed:host,c1', ...]
param([string]$Prefix = 'c1', [string[]]$Skip = @(),
      [string[]]$MpRuns = @('Armed:host,c1', 'GunNet:host,c1', 'DemoNet:host,c1', 'ShaftNet:host,c1', 'Qa:host,c1,c2,c3,c4,late6:5'))
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\BE_qa"
$sum = "$here\qa_summary_$Prefix.txt"
function Out-Sum([string]$t) { Add-Content -Path $sum -Value $t -Encoding UTF8 }
function Refill-Cache { robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null }
Set-Content -Path $sum -Value "Q/A chain $Prefix on BE_qa, started $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -Encoding UTF8

if ($Skip -notcontains 'audit') {
  Refill-Cache
  $log = "$here\${Prefix}_audit.log"
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $p = Start-Process -FilePath $unity -ArgumentList '-batchmode','-quit','-projectPath',$proj,'-executeMethod','BuildingEscape.EditorTools.RenderAudit.AuditBatch','-logFile',$log -PassThru
  if (-not $p.WaitForExit(1500000)) { Stop-Process -Id $p.Id -Force; Out-Sum 'render audit TIMED OUT' }
  $rep = "$proj\Logs\RenderAudit.txt"
  if (Test-Path $rep) {
    Copy-Item $rep "$here\${Prefix}_RenderAudit.txt" -Force
    $lines = Get-Content $rep
    Out-Sum "=== render audit: exit=$($p.ExitCode) in $([int]$sw.Elapsed.TotalSeconds) s; report $($lines.Count) lines"
    $lines | Where-Object { $_ -match 'RESULT|SUMMARY|broken|pink|magenta|Total|OK:|problems' } | Select-Object -First 14 | ForEach-Object { Out-Sum ('   ' + $_.Trim().Substring(0, [Math]::Min(200, $_.Trim().Length))) }
  } else { Out-Sum "=== render audit: exit=$($p.ExitCode), NO REPORT (see ${Prefix}_audit.log)" }
}

if ($Skip -notcontains 'pics') {
  foreach ($m in 'Ui', 'DemoUi', 'Menus', 'MenusLobby', 'MenusRun') {
    Refill-Cache
    $log = "$here\${Prefix}_pics_$m.log"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $r = & "$here\hd.ps1" -Exe $unity -Arguments "-projectPath `"$proj`" -executeMethod BuildingEscape.EditorTools.NetDevHarness.$m -logFile `"$log`"" -TimeoutSeconds 420
    $done = Select-String -Path $log -Pattern '\[(Ui|Menu)\] DONE' | Select-Object -Last 1
    $fails = @(Select-String -Path $log -Pattern '\[(Ui|Menu)\] FAIL').Count
    $saved = (Select-String -Path $log -Pattern 'saved ').Count
    Out-Sum "=== pictures $m : $r in $([int]$sw.Elapsed.TotalSeconds) s; pictures saved $saved; FAIL lines $fails; $(if ($done) { $done.Line.Trim() -replace '^\[NetDev\] ', '' } else { 'NO DONE LINE' })"
  }
}
Out-Sum "audit and pictures finished $(Get-Date -Format 'HH:mm:ss')"

if ($Skip -notcontains 'mp') { & "$here\qa_mp.ps1" -Prefix "${Prefix}mp" -Runs $MpRuns | Out-Null }
Out-Sum "finished $(Get-Date -Format 'HH:mm:ss')"
