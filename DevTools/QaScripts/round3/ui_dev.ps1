# Runs one NetDevHarness scenario of the scratch tree BE_dev in a NON-batch Editor on a hidden desktop (IMGUI pictures need a real window; the owner's screen is not touched).
# Usage: .\ui_dev.ps1 -Method InvUi -Tag u1 [-Timeout 600] [-Project BE_dev]      pictures: %TEMP%\be_ui_*.png ..., log: <Tag>.log
param([string]$Method = 'InvUi', [string]$Tag = 'u1', [int]$Timeout = 600, [string]$Project = 'BE_dev')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\$Project"
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$log = "$here\$Tag.log"
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = & "$here\hd.ps1" -Exe $unity -Arguments "-projectPath `"$proj`" -executeMethod BuildingEscape.EditorTools.NetDevHarness.$Method -logFile `"$log`"" -TimeoutSeconds $Timeout
"editor on the hidden desktop: $r in $([int]$sw.Elapsed.TotalSeconds) s"
Select-String -Path $log -Pattern '\[Ui\]|\[Etr\]|\[Intro\]|\[R3\]|\[Menu\]|\[Look\]|error CS|NullReference|DONE' | Select-Object -First 120 | ForEach-Object { $l = $_.Line.Trim() -replace '^\[NetDev\] ', ''; if ($l.Length -gt 230) { $l.Substring(0, 230) } else { $l } }
exit 0
