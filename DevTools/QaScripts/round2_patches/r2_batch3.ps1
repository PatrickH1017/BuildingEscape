# Round 2, batch 3: the pistol first lies in the outpost of floor 3 and is for sale after floor 3 (the owner, 2026-10-17). Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---- OutpostLoot.cs
Open-Src (Join-Path $Dir 'Scripts\Outpost\OutpostLoot.cs')
Rep @'
    /// valuables worth more (they follow the floor's quota). **The outpost of floor <see cref="PewFloor"/> always holds the pew pistol**
    /// (the owner's request, 2026-10-16: the guards are armed from that floor, so the players get a gun there, 100 % of the time).
'@ @'
    /// valuables worth more (they follow the floor's quota). **The outpost of floor <see cref="PewFloor"/> always holds the pew pistol**
    /// (the owner's requests: 2026-10-16 "100 % of the time", and 2026-10-17 moved from floor 4 to floor 3 "so that players can buy the pistol after completing the
    /// third floor": the vendor sells it from then on, <see cref="Vendor.FreePewAfterFloor"/>, and the guards are armed from the floor after it).
'@
Rep @'
        /// <summary>The floor whose outpost always holds a pew (the first floor with armed guards, <see cref="ArmedGuards.DefaultFirstArmedFloor"/>).</summary>
        public const int PewFloor = ArmedGuards.DefaultFirstArmedFloor;
'@ @'
        /// <summary>The floor whose outpost always holds a pew: floor 3, one before the first floor with armed guards (<see cref="ArmedGuards.DefaultFirstArmedFloor"/>).</summary>
        public const int PewFloor = 3;
'@
Rep '(1 + 2 + 3 = 6; with the pistol on floor 4 a valuable may go)' '(1 + 2 + 3 = 6; with the pistol on floor 3 a valuable may go)'
Save-Src

# ---- Vendor.cs
Open-Src (Join-Path $Dir 'Scripts\Vendor.cs')
Rep @'
        /// <summary>The pew is for sale once this floor has been finished (the owner, 2026-10-16: "after completion of the 4th floor the vendor should now sell the pew pistol for free").</summary>
        public const int FreePewAfterFloor = 4;
'@ @'
        /// <summary>The pew is for sale once this floor has been finished (the owner, 2026-10-16: "after completion of the 4th floor the vendor should now sell the pew pistol for free"; 2026-10-17: the third floor, "so that players can buy the pistol after completing the third floor").</summary>
        public const int FreePewAfterFloor = 3;
'@
Rep 'blurb = "gun: LMB fires, R reloads. Free for finishing the demo - try it on the dummy"' 'blurb = "gun: LMB fires, R reloads. Free after floor 3 - try it on the dummy"'
Rep '(the pew is the last entry, and only there once the demo''s last floor is done)' '(the pew is the last entry, and only there once floor 3 is done)'
Save-Src

# ---- RunSync.Guns.cs (a comment)
Open-Src (Join-Path $Dir 'Scripts\Net\RunSync.Guns.cs')
Rep 'the pistol lies in the outpost of floor 4, so it has to work for a' 'the pistol lies in the outpost of floor 3 (floor 4 until 2026-10-17), so it has to work for a'
Save-Src

# ---- QaAutopilot.cs (runs in the built game)
Open-Src (Join-Path $Dir 'Scripts\QaAutopilot.cs')
Rep @'
                if (floor == 4)
                {
                    var pew = FindObjectsByType<RangedWeapon>
'@ @'
                if (floor == OutpostLoot.PewFloor)
                {
                    var pew = FindObjectsByType<RangedWeapon>
'@
Rep 'View("floor4_5_pew",' 'View($"floor{floor}_5_pew",'
Rep @'
                if (floor == 4)
                {
                    int pews = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Count(g => g && g.isActiveAndEnabled);
                    Check("a pew lies in floor 4's outpost", pews == 1, $"({pews} pews on the floor)");
                    Check("the guards of floor 4 are armed", GuardAI.All.Count(g => g && g.Armed) >= 3, $"({GuardAI.All.Count(g => g && g.Armed)} armed)");
                }
'@ @'
                if (floor == OutpostLoot.PewFloor)
                {
                    int pews = FindObjectsByType<RangedWeapon>(FindObjectsSortMode.None).Count(g => g && g.isActiveAndEnabled);
                    Check($"a pew lies in floor {floor}'s outpost", pews == 1, $"({pews} pews on the floor)");
                }
                if (floor == ArmedGuards.DefaultFirstArmedFloor)
                    Check($"the guards of floor {floor} are armed", GuardAI.All.Count(g => g && g.Armed) >= 3, $"({GuardAI.All.Count(g => g && g.Armed)} armed)");
'@
Rep @'
                Check($"floor {floor} done: back in the docks, the elevator resumes at floor {floor + 1}", tm.ResumeLevel == floor + 1, $"(resume {tm.ResumeLevel})");
'@ @'
                Check($"floor {floor} done: back in the docks, the elevator resumes at floor {floor + 1}", tm.ResumeLevel == floor + 1, $"(resume {tm.ResumeLevel})");
                bool pewNow = floor >= Vendor.FreePewAfterFloor;
                Check($"floor {floor} done: the vendor's pew is {(pewNow ? "for sale" : "not for sale yet")}", Vendor.PewForSale(tm.ResumeLevel) == pewNow);
'@
Save-Src

# ---- NetDevPewLootTest.cs (pure)
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevPewLootTest.cs')
Rep @'
        /// Pure checks: the guards are armed from floor 4 up and from no lower floor; the outpost of floor 4 holds a pew on every run seed (and
        /// no other floor's does), first in its list of weapons, and never more than the outpost has places for; the planner gives floor 4's
        /// outpost the places; the pistol has the one name the pickup and the loot list use.
'@ @'
        /// The owner's request of 2026-10-17: the pistol first appears in the outpost of floor 3 (not 4), so it can be bought after floor 3.
        /// Pure checks: the guards are armed from floor 4 up and from no lower floor; the outpost of floor 3 holds a pew on every run seed (and
        /// no other floor's does), first in its list of weapons, and never more than the outpost has places for; the planner gives that
        /// outpost the places; the pistol has the one name the pickup and the loot list use.
'@
Rep 'Check("pew loot: the guards are armed from floor 4 and the pistol lies in floor 4''s outpost", ArmedGuards.DefaultFirstArmedFloor == 4 && OutpostLoot.PewFloor == 4);' @'
Check("pew loot: the guards are armed from floor 4 and the pistol lies in floor 3's outpost", ArmedGuards.DefaultFirstArmedFloor == 4 && OutpostLoot.PewFloor == 3);
            Check("pew loot: the pistol is found a floor before the guards are armed, and the vendor sells it once that floor is finished", OutpostLoot.PewFloor == ArmedGuards.DefaultFirstArmedFloor - 1 && Vendor.FreePewAfterFloor == OutpostLoot.PewFloor);
'@
Rep 'bool onlyFloor4 = true,' 'bool onlyPewFloor = true,'
Rep 'else onlyFloor4 &= pews == 0;' 'else onlyPewFloor &= pews == 0;'
Rep 'Check("pew loot: no other floor''s outpost has one", onlyFloor4);' 'Check("pew loot: no other floor''s outpost has one", onlyPewFloor);'
Rep 'Check($"pew loot: the outpost of floor 4 holds a pew on all 500 runs", always);' 'Check($"pew loot: the outpost of floor {OutpostLoot.PewFloor} holds a pew on all 500 runs", always);'
Rep 'Check("pew loot: the melee weapons of floor 4''s outpost are rolled as before (at most two)", otherWeaponsKept);' 'Check($"pew loot: the melee weapons of floor {OutpostLoot.PewFloor}''s outpost are rolled as before (at most two)", otherWeaponsKept);'
Rep 'floor 4 has {maxWeapons} weapons at most)", fits);' 'floor {OutpostLoot.PewFloor} has {maxWeapons} weapons at most)", fits);'
Rep '// ---- the planner gives floor 4''s outpost the places' '// ---- the planner gives the outpost of the pew floor the places'
Rep 'Check($"pew loot: floor 4''s outpost has {OutpostLoot.MaxItems} places' 'Check($"pew loot: floor {OutpostLoot.PewFloor}''s outpost has {OutpostLoot.MaxItems} places'
Save-Src

# ---- NetDevDemoTest.cs (pure)
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevDemoTest.cs')
Rep @'
            Check("vendor: the pew is for sale once floor 4 is finished (the next floor is 5), not before", !Vendor.PewForSale(1) && !Vendor.PewForSale(4) && Vendor.PewForSale(5) && Vendor.PewForSale(13)
                && Vendor.FreePewAfterFloor == 4);
'@ @'
            Check("vendor: the pew is for sale once floor 3 is finished (the next floor is 4), not before", !Vendor.PewForSale(1) && !Vendor.PewForSale(3) && Vendor.PewForSale(4) && Vendor.PewForSale(5) && Vendor.PewForSale(13)
                && Vendor.FreePewAfterFloor == 3);
'@
Save-Src

# ---- NetDevDemo.cs (scenario: the elevator trip through the four floors)
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevDemo.cs')
Rep @'
                if (floor < 4)
                    DemoCheck($"floor {floor}: the shop still has no pew", !Vendor.PewForSale(tm.ResumeLevel) && BuyFrom(Vendor.Current, hm, pack, Vendor.PewIndex).Contains("nothing like that"));
'@ @'
                if (floor < Vendor.FreePewAfterFloor)
                    DemoCheck($"floor {floor}: the shop still has no pew", !Vendor.PewForSale(tm.ResumeLevel) && BuyFrom(Vendor.Current, hm, pack, Vendor.PewIndex).Contains("nothing like that"));
                else
                    DemoCheck($"floor {floor} done: the shop has the pew now", Vendor.PewForSale(tm.ResumeLevel));
'@
Save-Src

# ---- NetDevPewLoot.cs (scenarios: offline, and the two-process gun test)
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevPewLoot.cs')
Rep @'
                if (floor == OutpostLoot.PewFloor)
                {
                    // ---- the backup is armed as well
'@ @'
                if (floor == ArmedGuards.DefaultFirstArmedFloor)
                {
                    // ---- the backup is armed as well
'@
Rep @'
                    PewLootCheck($"{f} the backup that comes is armed", after.Count > before && after.All(g => g.Armed), $"({before} -> {after.Count}, armed {after.Count(g => g.Armed)})");

                    // ---- the pew can be picked up and ends up in the hand
'@ @'
                    PewLootCheck($"{f} the backup that comes is armed", after.Count > before && after.All(g => g.Armed), $"({before} -> {after.Count}, armed {after.Count(g => g.Armed)})");
                }

                if (floor == OutpostLoot.PewFloor)
                {
                    // ---- the pew can be picked up and ends up in the hand
'@
Rep @'
            tm.GoToLevel(OutpostLoot.PewFloor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);

            var gen = FindFirstObjectByType<FloorGenerator>();
'@ @'
            ArmedGuards.ForceForTests = true;      // (the guards are armed from floor 4 and the pew lies on floor 3: this test arms floor 3 too, to see the clients get the flag)
            tm.GoToLevel(OutpostLoot.PewFloor);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == OutpostLoot.PewFloor);
            yield return FloorReady(OutpostLoot.PewFloor, 120);
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(3f);

            var gen = FindFirstObjectByType<FloorGenerator>();
'@
Rep 'GunNetCheck("host: exactly one pew lies on floor 4, in the outpost",' 'GunNetCheck($"host: exactly one pew lies on floor {OutpostLoot.PewFloor}, in the outpost",'
Rep 'GunNetCheck("host: every guard carries a pew (no test switch: the floor rule)",' 'GunNetCheck("host: every guard carries a pew (the test switch arms this floor)",'
Save-Src
Write-Output 'batch 3 patched'
