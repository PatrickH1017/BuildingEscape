param([string]$Method, [string]$Tag, [int]$TimeoutSeconds = 900, [string]$Project = "BE_dev", [switch]$Graphics)
$root = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
robocopy 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\Library\PackageCache' "$root\$Project\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$log = "$root\$Tag.log"
$p = Start-Process -FilePath $unity -ArgumentList (@('-batchmode') + $(if (-not $Graphics) { '-nographics' }) + @('-projectPath',"$root\$Project",'-executeMethod',"BuildingEscape.EditorTools.NetDevHarness.$Method",'-logFile',$log)) -PassThru
if (-not $p.WaitForExit($TimeoutSeconds * 1000)) { Stop-Process -Id $p.Id -Force; "$Method TIMED OUT" } else { "$Method exit=$($p.ExitCode)" }


