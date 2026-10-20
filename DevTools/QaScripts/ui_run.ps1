# Runs an editor method of a project copy in a NON-batch Editor on a hidden desktop (pictures of IMGUI, window tests).
# Usage: .\ui_run.ps1 -Project <project root> -Class NetDevHarness -Method MenusLobby -Tag x [-Timeout 600] [-Filter '\[Menu\]']
param([Parameter(Mandatory = $true)][string]$Project, [string]$Class = 'NetDevHarness', [Parameter(Mandatory = $true)][string]$Method, [string]$Tag = 'ui', [int]$Timeout = 600, [string]$Filter = '\[Ui\]|\[Menu\]|\[MenuClick\]|error CS|NullReference|DONE')
$mine = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
robocopy "$real\Library\PackageCache" "$Project\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$log = "$mine\$Tag.log"
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = & "$mine\hd.ps1" -Exe $unity -Arguments "-projectPath `"$Project`" -executeMethod BuildingEscape.EditorTools.$Class.$Method -logFile `"$log`"" -TimeoutSeconds $Timeout
"editor on the hidden desktop: $r in $([int]$sw.Elapsed.TotalSeconds) s"
Select-String -Path $log -Pattern $Filter | Select-Object -First 120 | ForEach-Object { $_.Line.Trim() }
