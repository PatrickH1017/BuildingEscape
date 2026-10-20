# Round 11, batch G03: the three pure tests the armour changed - the dealer's list is sixteen long now (three more pieces at the end) and a safe with armour in it holds no spare pistol clip.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$Ed\NetDevDemoTest.cs"
Rep @'
c.Length == 13 && Vendor.PewIndex == 7
'@ @'
c.Length == 16 && Vendor.PewIndex == 7
'@
Save-Src

Open-Src "$Ed\NetDevGunsTest.cs"
Rep @'
            bool ammoRows = cat.Length == 13;
'@ @'
            bool ammoRows = cat.Length == 16;                               // (the nine it was, the ammunition of the other four guns, and the three pieces of armour at the end)
'@
Rep @'
vendor: the dealer's list is the nine it was and four more, the ammunition
'@ @'
vendor: the dealer's list is the nine it was, four more (the ammunition
'@
Rep @'
(their own names, prices and clip sizes) - no gun but the pew is for sale"
'@ @'
(their own names, prices and clip sizes) and three pieces of armour at the end - no gun but the pew is for sale"
'@
Save-Src

Open-Src "$Ed\NetDevR7Test.cs"
Rep @'
                bool sideOk = true; int cashHits = 0, clipHits = 0, weaponSafes = 0;
'@ @'
                bool sideOk = true; int cashHits = 0, clipHits = 0, weaponSafes = 0, clipSafes = 0;       // (a spare clip is for the gun the safe holds: a safe with armour in it has none, so the clips are counted among the safes with a gun)
'@
Rep @'
                            else { weaponSafes++; if (cash) cashHits++; if (clip) clipHits++; }
'@ @'
                            else { weaponSafes++; if (cash) cashHits++; if (!SafeRules.HoldsArmour(rs, f, i)) { clipSafes++; if (clip) clipHits++; } }
'@
Rep @'
clipHits * 100 >= weaponSafes * 43 && clipHits * 100 <= weaponSafes * 57, $"(cash {cashHits}, clips {clipHits} of {weaponSafes} safes)");
'@ @'
clipHits * 100 >= clipSafes * 43 && clipHits * 100 <= clipSafes * 57, $"(cash {cashHits} of {weaponSafes} safes, clips {clipHits} of {clipSafes} safes with a gun)");
'@
Save-Src
