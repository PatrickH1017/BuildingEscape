# Round 11, batch G04: the Outpost scenario's floor-7 safe checks count the guns (a safe may hold armour instead of one); the Armour scenario's pictures are taken in the docks.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Ed\NetDevArmour.cs" 'C:\bqs\r11_draft\NetDevArmour.cs'

Open-Src "$Ed\NetDevOutpost.cs"
Rep @'
                    foreach (var wsS in wsSafes) wsExpected += wsS.GunCount;
'@ @'
                    foreach (var wsS in wsSafes) wsExpected += SafeRules.IsGunItem(SafeRules.WeaponContents(seed, wsFloor, wsS.Index)[0]) ? 1 : 0;          // (a safe that is not the first of its floor may hold armour instead of a gun)
'@
Rep @'
wsTotal == wsExpected && wsInSafe == wsTotal && wsTotal >= wsWant && wsBarrels && wsDormant
'@ @'
wsTotal == wsExpected && wsInSafe == wsTotal && wsTotal >= 1 && wsBarrels && wsDormant
'@
Rep @'
wsLive3 == wsAgain[1].GunCount + wsAgain[2].GunCount + wsAgain[3].GunCount, $"({wsLive3} pistols live)");
'@ @'
wsLive3 == GunsInSafe(seed, wsFloor, 1) + GunsInSafe(seed, wsFloor, 2) + GunsInSafe(seed, wsFloor, 3), $"({wsLive3} pistols live)");
'@
Save-Src
