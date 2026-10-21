# Round 12, batch G08 (the owner, 2026-10-19 night: "Max ammo for the pistol should be about 100 shots"):
# the pistol carries twelve clips (96 rounds, the clip in the gun counted) instead of three; the other guns keep three clips. Data-driven: a gun's Spec has maxClips (0 = the three of GunRules.MaxClips).
# The tests that filled a pistol "to three clips" fill it to its limit now; a new test covers the twelve clips.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\Guns\RangedWeapon.cs'
Rep 'public int spareClips;' @'
public int spareClips;
            /// <summary>The most clips of this gun a player may carry, the one in the gun counted; 0 = the three of <see cref="GunRules.MaxClips"/>. The pistol carries twelve: 96 rounds (the owner, 2026-10-19 night: "Max ammo for the pistol should be about 100 shots").</summary>
            public int maxClips;
'@
Rep 'magazine = 8, spareClips = 2, reloadSeconds = 2.2f' 'magazine = 8, spareClips = 2, maxClips = 12, reloadSeconds = 2.2f'
Rep 'm.MaxRounds = GunRules.MaxClips * s.magazine;                    // (a player carries at most three clips)' 'm.MaxRounds = (s.maxClips > 0 ? s.maxClips : GunRules.MaxClips) * s.magazine;                    // (a player carries at most three clips of a gun; of the pistol twelve)'
Save-Src

Open-Src 'C:\bqm\Assets\Scripts\Guns\GunRules.cs'
Rep @'
        /// The most rounds a player may carry for this gun, the clip in it counted (the owner, 2026-10-19: "A player should be allowed to have three gun clips max"): <see cref="GunRules.MaxClips"/> clips of it. A gun with no
'@ @'
        /// The most rounds a player may carry for this gun, the clip in it counted (the owner, 2026-10-19: "A player should be allowed to have three gun clips max"): <see cref="GunRules.MaxClips"/> clips of it, the pistol twelve
        /// (2026-10-19 night: "Max ammo for the pistol should be about 100 shots": see RangedWeapon.Spec.maxClips). A gun with no
'@
Save-Src

# ---- the tests: a pistol is full at twelve clips now
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevGunsTest.cs'
Rep @'
            Check("ammunition: a gun that was bought holds its clip and two spares (nothing more fits); a gun that was found holds its one clip and has room for two more",
                fresh.Capacity == 8 && fresh.Rounds == 8 && fresh.Reserve == 16 && fresh.Room == 0 && fresh.AddReserve(8) == 0 && fresh.Reserve == 16
                && found.Capacity == 24 && found.Rounds == 24 && found.Reserve == 0 && found.Room == 48);
'@ @'
            Check("ammunition: a pistol that was bought holds its clip and two spares and has room for nine clips more (twelve in all: 96 rounds); a gun that was found holds its one clip and has room for two more",
                fresh.Capacity == 8 && fresh.Rounds == 8 && fresh.Reserve == 16 && fresh.Room == 72 && S[pew].maxClips == 12
                && found.Capacity == 24 && found.Rounds == 24 && found.Reserve == 0 && found.Room == 48);
            int pistolAdded = 0;
            for (int i = 0; i < 12; i++) pistolAdded += fresh.AddReserve(8);
            Check("ammunition: the pistol takes nine more clips and no tenth (twelve clips, 96 rounds, the one in the gun counted); the other guns keep their three", pistolAdded == 72 && fresh.Reserve == 88 && fresh.Room == 0 && fresh.AddReserve(8) == 0
                && S[smg].maxClips == 0 && S[rifle].maxClips == 0 && S[shotgun].maxClips == 0 && S[sniper].maxClips == 0);
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevGuns.cs'
Rep @'
                GunCheck($"{tag} the magazine holds {spec.magazine} and the gun comes with {spec.spareClips} spare clips, none more fit",
                    gun.Magazine.Capacity == spec.magazine && gun.Magazine.Rounds == spec.magazine && gun.Magazine.Reserve == spec.spareClips * spec.magazine && gun.Magazine.Room == 0);
'@ @'
                GunCheck($"{tag} the magazine holds {spec.magazine} and the gun comes with {spec.spareClips} spare clips, and room for the rest of its limit ({(spec.maxClips > 0 ? spec.maxClips : GunRules.MaxClips)} clips)",
                    gun.Magazine.Capacity == spec.magazine && gun.Magazine.Rounds == spec.magazine && gun.Magazine.Reserve == spec.spareClips * spec.magazine
                    && gun.Magazine.Room == ((spec.maxClips > 0 ? spec.maxClips : GunRules.MaxClips) - 1 - spec.spareClips) * spec.magazine);
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevOutpost.cs'
Rep @'
                            if (tGun != null) tGun.Magazine.SetReserve(2 * RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine);        // (two clips bought from the dealer)
                            // (the owner, 2026-10-19: three clips at most) a pistol with its clip and two spare ones is full: a clip is refused and stays where it lies; with a spare clip used up there is room for one
'@ @'
                            if (tGun != null) tGun.Magazine.SetReserve(tGun.Magazine.MaxRounds - tGun.Magazine.Rounds);        // (the pistol full to its limit: twelve clips, the owner, 2026-10-19 night)
                            // a pistol with all its clips is full: a clip is refused and stays where it lies; with a spare clip used up there is room for one
'@
Rep 'with the pistol and its two spare clips (three clips) the clip is refused too and stays where it lies' 'with the pistol full to its limit (twelve clips) the clip is refused too and stays where it lies'
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevDemo.cs'
Rep @'
            int cashClip = hm.Cash;
            string fullPack = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a pew with its clip and two spare ones is carrying the three clips a player may (the owner, 2026-10-19): the dealer refuses a fourth and takes no money", fullPack.Contains("three clips") && hm.Cash == cashClip && gc.Gun.Magazine.Reserve == 16, $"(\"{fullPack}\")");
'@ @'
            gc.Gun.Magazine.SetReserve(gc.Gun.Magazine.MaxRounds - gc.Gun.Magazine.Rounds);       // (the pew full to its limit: twelve clips, 96 rounds; the owner, 2026-10-19 night: "about 100 shots")
            int cashClip = hm.Cash;
            int fullReserve = gc.Gun.Magazine.Reserve;
            string fullPack = BuyFrom(vendor, hm, pack, Vendor.AmmoIndex);
            DemoCheck("a pew with all its twelve clips is carrying all a player may: the dealer refuses another and takes no money", fullPack.Contains("clips") && hm.Cash == cashClip && gc.Gun.Magazine.Reserve == fullReserve && fullReserve == 88, $"(\"{fullPack}\")");
'@
Save-Src
