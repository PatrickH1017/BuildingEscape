# T5: the walkie talkie is the 17th and last entry of the dealer's list (after the three pieces of armour): three old pure checks pinned "16 entries" / "the armour is last". Their numbers move by one; nothing else changes.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\Editor\NetDev\'
$SC = $A + 'NetDevDemoTest.cs'; Open-Src $SC
Rep 'c.Length == 16 && Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8' 'c.Length == 17 && Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8'
Save-Src
$SC = $A + 'NetDevGunsTest.cs'; Open-Src $SC
Rep 'bool ammoRows = cat.Length == 16;' 'bool ammoRows = cat.Length == 17;'
Rep '// (the nine it was, the ammunition of the other four guns, and the three pieces of armour at the end)' '// (the nine it was, the ammunition of the other four guns, the three pieces of armour and the walkie talkie at the end)'
Rep ') and three pieces of armour at the end - no gun but the pew is for sale"' '), three pieces of armour and the walkie talkie at the end - no gun but the pew is for sale"'
Save-Src
$SC = $A + 'NetDevArmourTest.cs'; Open-Src $SC
Rep '"armour: the dealer has the three pieces at the end of his list - priced by the rules - and sells a tier only once' '"armour: the dealer has the three pieces at the end of his list (before the walkie talkie) - priced by the rules - and sells a tier only once'
Rep 'ih == Vendor.Catalog.Length - 1 &&' 'ih == Vendor.Catalog.Length - 2 && Vendor.Catalog[Vendor.Catalog.Length - 1].isWalkie &&'
Save-Src
$SC = $A + 'NetDevGuns.cs'; Open-Src $SC
Rep 'GunCheck("dealer: the shop shows the first seven, and the ammunition of the rifle and the shotgun that were found - nothing else", offeredNow.Count == 9 && offeredNow.Contains(rifleAmmo)' 'GunCheck("dealer: the shop shows the first seven, the walkie talkie, and the ammunition of the rifle and the shotgun that were found - nothing else", offeredNow.Count == 10 && offeredNow.Contains(Vendor.WalkieIndex) && offeredNow.Contains(rifleAmmo)'
Save-Src
"T5 test numbers moved"
