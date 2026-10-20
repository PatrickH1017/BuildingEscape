# Round 9, batch G09 (the four guns): the two-handed hold fits the body (HeldGun.FitTwoHand: the gun is centred and made smaller until the left hand reaches the foregrip; a body that cannot hold it in both hands holds it in one).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Src\Guns\HeldGun.cs" 'C:\bqs\gun_draft\HeldGun.cs'
