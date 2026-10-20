# Round 10, batch G07: the Outpost scenario's three-clip check - a pistol found in a safe holds one clip and no spare ones now, so the two spare clips are bought first.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevOutpost.cs'
Rep @'
                            // (the owner, 2026-10-19: three clips at most) a pistol with its clip and two spare ones is full: a clip is refused and stays where it lies; with a spare clip used up there is room for one
                            bool tFullRefused = tGun != null && tGun.Magazine.Room == 0 && !pack.TryStore(tClip);
'@ @'
                            OutpostCheck($"{wsTag} a pistol found in a safe holds its one clip and no spare ones (the owner, 2026-10-19)", tGun != null && tGun.Magazine.Rounds == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine && tGun.Magazine.Reserve == 0, $"(rounds {(tGun != null ? tGun.Magazine.Rounds : -1)}, reserve {(tGun != null ? tGun.Magazine.Reserve : -1)})");
                            if (tGun != null) tGun.Magazine.SetReserve(2 * RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine);        // (two clips bought from the dealer)
                            // (the owner, 2026-10-19: three clips at most) a pistol with its clip and two spare ones is full: a clip is refused and stays where it lies; with a spare clip used up there is room for one
                            bool tFullRefused = tGun != null && tGun.Magazine.Room == 0 && !pack.TryStore(tClip);
'@
Save-Src
