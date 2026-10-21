# Round 12, batch G42: the cutscene scenario takes a picture of the OPENING (five workers hanging out, the TV dark) before it moves on to the next beats (their pose is lost after the first seek).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevEtr.cs'
Rep '$"({notFacing} of {intro.WorkerCount} not facing the TV)");' @'
$"({notFacing} of {intro.WorkerCount} not facing the TV)");
            yield return Snap("intro_tv_opening_workers");
'@
Save-Src
Write-Output 'round 12 batch G42 patched'
