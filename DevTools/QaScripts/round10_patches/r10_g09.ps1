# Round 10, batch G09: the blast's colours (an orange fireball with a pale core, grey smoke).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\Guns\BlastFx.cs'
$t = [IO.File]::ReadAllText('C:\bqs\r10_draft\BlastFx.cs').Replace("`r`n", "`n").TrimEnd("`n")
$script:s = $t.Replace("`n", $script:eol) + $script:eol
Save-Src
