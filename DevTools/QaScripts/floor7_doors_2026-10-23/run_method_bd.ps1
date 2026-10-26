# Runs one static editor method of the private QA copy in batch mode and reports the log's key lines.
# Usage: .\run_method.ps1 -Method BuildingEscape.EditorTools.CharacterCatalogBuilder.BuildBatch -Log cat1 [-Gfx] [-Project BE_qa] [-Pattern 'regex of lines to show'] [-Minutes 15]
param([Parameter(Mandatory)][string]$Method, [Parameter(Mandatory)][string]$Log, [switch]$Gfx, [string]$Project = 'BE_qa', [string]$Pattern = '', [int]$Minutes = 15)
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$real = 'C:\Users\phick\OneDrive\Desktop\BuildingEscape'
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.0.45f1\Editor\Unity.exe'
$proj = 'C:\bd'   # (a short path: the Shader Graph package has paths that do not fit under the long scratchpad path)
$logPath = "$here\run_$Log.log"
robocopy "$real\Library\PackageCache" "$proj\Library\PackageCache" /MIR /MT:16 /XJ /NFL /NDL /NJH /NJS /NP | Out-Null
$args = @('-batchmode') + $(if (-not $Gfx) { '-nographics' }) + @('-projectPath', $proj, '-executeMethod', $Method, '-logFile', $logPath)
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $unity -ArgumentList $args -PassThru
if (-not $p.WaitForExit($Minutes * 60000)) { Stop-Process -Id $p.Id -Force; $r = 'TIMED OUT (killed)' } else { $r = "exit=$($p.ExitCode)" }
"=== $Method : $r in $([int]$sw.Elapsed.TotalSeconds) s; compile errors: $((Select-String -Path $logPath -Pattern 'error CS').Count)"
if ($Pattern) { Select-String -Path $logPath -Pattern $Pattern | ForEach-Object { $_.Line.Trim() } | Select-Object -First 80 }
