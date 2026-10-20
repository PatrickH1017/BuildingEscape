# Round 10, batch G05: the blast made of solid shapes (the sprite quads did not show), the armed share by floor (the rifles stay the most numerous), the pure and Play tests of both.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Src\Guns\BlastFx.cs" 'C:\bqs\r10_draft\BlastFx.cs'
FromDraft "$Src\Guns\GuardLoadouts.cs" 'C:\bqs\r10_draft\GuardLoadouts.cs'
FromDraft "$Ed\NetDevGuardsTest.cs" 'C:\bqs\r10_draft\NetDevGuardsTest.cs'
FromDraft "$Ed\NetDevCombat.cs" 'C:\bqs\r10_draft\NetDevCombat.cs'
