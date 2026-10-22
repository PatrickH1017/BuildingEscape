# Runs one multiplayer scenario: one Unity process per role, same script, signal files keep them in step.
param([string]$Method, [string[]]$Roles, [int]$Players, [string]$RunId, [int]$TimeoutSeconds = 600, [string]$ExtraEnv = '')
$root = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$dirs = @{ host='C:\bd'; c1='C:\bdc' }
$procs = @{}
$env:BE_RUN = $RunId; $env:BE_QA_RUN = $RunId; $env:BE_PLAYERS = "$Players"
if ($ExtraEnv) { foreach ($kv in $ExtraEnv.Split(';')) { $a = $kv.Split('='); Set-Item -Path "env:$($a[0])" -Value $a[1] } }
foreach ($r in $Roles) {
  robocopy 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\Library\PackageCache' "$($dirs[$r])\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
  $env:BE_QA_ROLE = $r
  $log = "$root\mp_${Method}_${RunId}_$r.log"
  $procs[$r] = Start-Process -FilePath $unity -ArgumentList '-batchmode','-nographics','-projectPath',$dirs[$r],'-executeMethod',"BuildingEscape.EditorTools.NetDevHarness.$Method",'-logFile',$log -PassThru
  if ($r -eq 'host') { Start-Sleep -Seconds 20 }
}
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and ($procs.Values | Where-Object { -not $_.HasExited })) { Start-Sleep -Seconds 3 }
foreach ($r in $Roles) {
  $p = $procs[$r]
  if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force; "$r TIMED OUT (killed)" } else { "$r exit=$($p.ExitCode)" }
}

