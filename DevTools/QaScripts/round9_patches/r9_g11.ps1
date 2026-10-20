# Round 9, batch G11 (the four guns): the pure tests - a new group (GunsTest) and the two older checks that the new rules changed (the dealer has thirteen things for sale, the safes hold a gun by floor).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'

$t = [IO.File]::ReadAllText('C:\bqs\gun_draft\NetDevGunsTest.cs').Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
$target = "$Ed\NetDev\NetDevGunsTest.cs"
if ($env:R2_DRY) { Write-Output "dry run: would write $target" } else { [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false))); Write-Output "written $target" }

Open-Src "$Ed\NetDev\NetDevTest.cs"
Rep @'
            Round7Test();
            string v = NetConst.BuildVersion;
'@ @'
            Round7Test();
            GunsTest();
            string v = NetConst.BuildVersion;
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevR7Test.cs"
Rep @'
                                foreach (int k in items) contents &= k == SafeRules.PewItem;
'@ @'
                                foreach (int k in items) contents &= SafeRules.IsGunItem(k) && f >= RangedWeapon.Specs[(int)SafeRules.GunOfItem(k)].unlockFloor && (f > 4 || k == SafeRules.PewItem);      // (a gun by floor: the pew only on floors 3 and 4, GunsTest has the rest)
'@
Rep 'Check("safes: floors 1 and 2 hold no weapon, every other safe exactly one pew pistol (never two), a safe that is not there holds nothing", contents);' 'Check("safes: floors 1 and 2 hold no weapon, every other safe exactly one gun (never two; the pew on floors 3 and 4, the guns of the owner''s ladder after), a safe that is not there holds nothing", contents);'
Save-Src

Open-Src "$Ed\NetDev\NetDevDemoTest.cs"
Rep 'Check("vendor: the pew is the eighth entry, for money (not free any more), a gun; a clip of ammunition follows it; the seven others are where they were", c.Length == 9 && Vendor.PewIndex == 7' 'Check("vendor: the pew is the eighth entry, for money (not free any more), a gun; a clip of ammunition follows it, then the ammunition of the other guns; the seven others are where they were", c.Length == 13 && Vendor.PewIndex == 7'
Save-Src
