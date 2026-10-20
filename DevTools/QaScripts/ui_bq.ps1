# Runs one NetDevHarness scenario in a NON-batch Editor on a hidden desktop on the QA tree BE_qa (IMGUI pictures need a real window; the owner's screen is not touched).
# Usage: .\ui_bq.ps1 -Method IntroTv -Tag u1 [-Timeout 600]      pictures: %TEMP%\be_ui_*.png, log: <Tag>.log
param([string]$Method = 'IntroTv', [string]$Tag = 'u1', [int]$Timeout = 600, [string]$Project = 'BE_qa')
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = "$here\$Project"
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$log = "$here\$Tag.log"
$sw = [Diagnostics.Stopwatch]::StartNew()
$r = & "$here\hd.ps1" -Exe $unity -Arguments "-projectPath `"$proj`" -executeMethod BuildingEscape.EditorTools.NetDevHarness.$Method -logFile `"$log`"" -TimeoutSeconds $Timeout
"editor on the hidden desktop: $r in $([int]$sw.Elapsed.TotalSeconds) s"
Select-String -Path $log -Pattern '\[Ui\]|\[Etr\]|\[Intro\]|error CS|NullReference|DONE' | Select-Object -First 80 | ForEach-Object { $l = $_.Line.Trim() -replace '^\[NetDev\] ', ''; if ($l.Length -gt 230) { $l.Substring(0, 230) } else { $l } }
exit 0
