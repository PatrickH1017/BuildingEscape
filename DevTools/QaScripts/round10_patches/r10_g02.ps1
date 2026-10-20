# Round 10, batch G02: the old tests that assumed the old rules (a bullet stuns, the vest of floor 4, every guard armed from floor 4, a blow counts as a hit), the legacy rule without a shield, the pew's stun gone, and the new pure tests.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

function NewFile([string]$target, [string]$draft) {
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
  if ($env:R2_DRY) { Write-Output "dry run: would write $target"; return }
  [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "written $target"
}

# ---- the old rule (a test or the test menu changed the first armed floor): a pew and no shield, as before
Open-Src "$Src\Guns\ArmedGuards.cs"
Rep @'
        /// Is the old rule in force instead of the floors' loadouts (<see cref="GuardLoadouts"/>)? Yes when a test or the test menu changed the first armed floor or armed everybody: then every guard of an armed floor carries a pew with
        /// a light shield, and nothing else.
'@ @'
        /// Is the old rule in force instead of the floors' loadouts (<see cref="GuardLoadouts"/>)? Yes when a test or the test menu changed the first armed floor or armed everybody: then every guard of an armed floor carries a pew
        /// and wears no shield, and nothing else.
'@
Rep @'
                var l = ShouldArm(floor) ? GuardLoadout.Of(RangedWeapon.Kind.Pew) : GuardLoadout.Unarmed;
'@ @'
                var l = ShouldArm(floor) ? new GuardLoadout(true, RangedWeapon.Kind.Pew, ShieldTier.None) : GuardLoadout.Unarmed;
'@
Rep @'
            if (LegacyRule()) return ShouldArm(floor) ? GuardLoadout.Of(RangedWeapon.Kind.Pew) : GuardLoadout.Unarmed;
'@ @'
            if (LegacyRule()) return ShouldArm(floor) ? new GuardLoadout(true, RangedWeapon.Kind.Pew, ShieldTier.None) : GuardLoadout.Unarmed;
'@
Save-Src

# ---- the pew no longer stuns (the number was only read by the old bullet code)
Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
guardHits = 4, stun = 8f, knock = 16f
'@ @'
guardHits = 4, stun = 0f, knock = 16f
'@
Save-Src

# ---- pure tests
Open-Src "$Ed\NetDevTest.cs"
Rep @'
            GunsTest();
            string v = NetConst.BuildVersion;
'@ @'
            GunsTest();
            GuardsTest();
            string v = NetConst.BuildVersion;
'@
Rep @'
pew.damage > mw.damage && pew.stun > mw.stun && pew.knock > mw.knock
'@ @'
pew.damage > mw.damage && pew.knock > mw.knock
'@
Rep @'
            Check("gun: the pew beats the crowbar, the bat and the hatchet in damage, stun, knock-back, noise and speed", strongest);
'@ @'
            Check("gun: the pew beats the crowbar, the bat and the hatchet in damage, knock-back, noise and speed (it does not stun: the owner, 2026-10-19)", strongest && pew.stun == 0f);
'@
Save-Src
NewFile "$Ed\NetDevGuardsTest.cs" 'C:\bqs\r10_draft\NetDevGuardsTest.cs'

Open-Src "$Ed\NetDevR7Test.cs"
Rep @'
            // ---- body armour from floor 4 on: a bullet counts for half the hits, so the pew needs five bullets instead of three
            Check("armour: floors 1-3 have none, floors 4-12 have it, the boss floor does not", !GuardRules.Armoured(1) && !GuardRules.Armoured(3) && GuardRules.Armoured(4) && GuardRules.Armoured(12) && !GuardRules.Armoured(13) && !GuardRules.Armoured(0));
            var pew = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            Check("armour: an armoured officer takes five pew bullets where an unarmoured one takes three", GunRules.BulletsToKill(10, pew.guardHits) == 3 && GunRules.BulletsToKill(10, GuardRules.ArmouredHits(pew.guardHits)) == 5, $"({pew.guardHits} hits a bullet, {GuardRules.ArmouredHits(pew.guardHits)} against armour)");
            Check("armour: a bullet always counts for at least one hit", GuardRules.ArmouredHits(1) == 1 && GuardRules.ArmouredHits(0) == 1 && GuardRules.ArmouredHits(2) == 1 && GuardRules.ArmouredHits(8) == 4);
'@ @'
            // ---- (the vest of floor 4, which halved bullets, is now the medium shield of the guard's loadout: see GuardsTest)
            var pew = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
            Check("armour: an unshielded officer takes three pew bullets, one with a medium shield (the old vest) five", GunRules.BulletsToKill(10, pew.guardHits) == 3 && GuardLoadouts.BulletsToKill(10f, pew.guardHits, ShieldTier.Medium) == 5);
'@
Save-Src

# ---- the Play scenarios
Open-Src "$Ed\NetDevPew.cs"
Rep @'
                PewCheck("one bullet counts as four hits on a guard and stuns him", victim.Hits == hits0 + 4 && victim.State == GuardState.Stunned && !victim.Dead,
'@ @'
                PewCheck("one bullet counts as four hits on a guard and does not knock him over (the owner, 2026-10-19: bullets damage, melee knocks over)", victim.Hits == hits0 + 4 && victim.State != GuardState.Stunned && !victim.Dead,
'@
Rep @'
                PewCheck("the second bullet stuns him again, but he is not down yet (eight of ten hits)", second
'@ @'
                PewCheck("the second bullet hits him again, but he is not down yet (eight of ten hits)", second
'@
Rep @'
            PewCheck("the guards are armed from floor 4 by default (and nobody at the docks)"
'@ @'
            PewCheck("the guards are armed from the first armed floor by default (and nobody at the docks)"
'@
Save-Src

Open-Src "$Ed\NetDevMeleeNet.cs"
Rep @'
            MeleeCheck("host: the first hit stunned the guard", g != null && g.State == GuardState.Stunned && g.Hits == 1,
'@ @'
            MeleeCheck("host: the first blow knocked the guard over and did not count towards knocking him out", g != null && g.State == GuardState.Stunned && g.Hits == 0,
'@
Rep @'
            MeleeCheck("host: a second hit counts too", g != null && g.Hits == 2, $"(hits {g?.Hits})");
'@ @'
            MeleeCheck("host: a second blow does not count either, and he is still up to be hit", g != null && g.Hits == 0 && !g.Dead, $"(hits {g?.Hits})");
'@
Save-Src

Open-Src "$Ed\NetDevPewLootTest.cs"
Rep @'
            Check("pew loot: the guards are armed from floor 4 and the pistol lies in floor 3's outpost", ArmedGuards.DefaultFirstArmedFloor == 4 && OutpostLoot.PewFloor == 3);
            Check("pew loot: the pistol is found a floor before the guards are armed, and the vendor sells it once that floor is finished", OutpostLoot.PewFloor == ArmedGuards.DefaultFirstArmedFloor - 1 && Vendor.PewAfterFloor == OutpostLoot.PewFloor);
            Check("pew loot: a game starts with the armed-floor rule at floor 4", ArmedGuards.FirstArmedFloor == ArmedGuards.DefaultFirstArmedFloor);
'@ @'
            Check("pew loot: the guards are armed from floor 3 and the pistol lies in floor 3's outpost", ArmedGuards.DefaultFirstArmedFloor == 3 && OutpostLoot.PewFloor == 3);
            Check("pew loot: the pistol is found on the floor where the guards are first armed (the owner, 2026-10-19), and the vendor sells it once that floor is finished", OutpostLoot.PewFloor == ArmedGuards.DefaultFirstArmedFloor && Vendor.PewAfterFloor == OutpostLoot.PewFloor);
            Check("pew loot: a game starts with the armed-floor rule at the first armed floor", ArmedGuards.FirstArmedFloor == ArmedGuards.DefaultFirstArmedFloor);
'@
Rep @'
            Check("pew loot: guards on floors 1-3 are unarmed, on floors 4-13 armed", armedRule);
'@ @'
            Check("pew loot: guards on floors 1-2 are unarmed, on floors 3-13 some are armed (the rule for the floor)", armedRule);
'@
Save-Src

Open-Src "$Ed\NetDevPewLoot.cs"
Rep @'
                bool armed = floor >= ArmedGuards.DefaultFirstArmedFloor;
                PewLootCheck($"{f} {(armed ? "every guard carries a pew" : "nobody carries a pew")}", guards.Count > 0 && guards.All(g => g.Armed == armed), $"({guards.Count(g => g.Armed)} of {guards.Count})");
                var duty = GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");
                PewLootCheck($"{f} the guard on duty in the outpost {(armed ? "is armed too" : "is not armed")}", duty != null && duty.Armed == armed);
'@ @'
                bool armed = floor >= ArmedGuards.DefaultFirstArmedFloor;
                PewLootCheck($"{f} {(armed ? "some guards carry a gun, not all" : "nobody carries a gun")}", guards.Count > 0 && (armed ? guards.Any(g => g.Armed) && guards.Any(g => !g.Armed) : guards.All(g => !g.Armed)), $"({guards.Count(g => g.Armed)} of {guards.Count})");
                PewLootCheck($"{f} every gun is the kind of his loadout and every shield the one that goes with it", guards.All(g => g.Armed == g.Loadout.Armed && (!g.Armed || (g.Gun != null && g.Gun.kind == g.Loadout.Kind && g.Shield == GuardLoadouts.ShieldOf(g.Loadout.Kind))) && (g.Armed || g.Shield == ShieldTier.None)));
                var duty = GuardAI.All.FirstOrDefault(g => g && g.name == "Guard_Outpost");
                PewLootCheck($"{f} the guard on duty in the outpost is there", duty != null);
'@
Rep @'
                    PewLootCheck($"{f} the backup that comes is armed", after.Count > before && after.All(g => g.Armed), $"({before} -> {after.Count}, armed {after.Count(g => g.Armed)})");
'@ @'
                    PewLootCheck($"{f} the backup that comes follows the same rule (a gun of his loadout's kind, or nothing)", after.Count > before && after.All(g => g.Armed == g.Loadout.Armed && (!g.Armed || g.Gun.kind == g.Loadout.Kind)), $"({before} -> {after.Count}, armed {after.Count(g => g.Armed)})");
'@
Save-Src

Open-Src "$Ed\NetDevR7.cs"
Rep @'
            R7Check("floor 3: still no armour, a safe", GuardAI.All.All(g => !g || !g.Armoured) && FloorSafe.Active != null);
            yield return R5LoadFloor(4);
            var armoured = GuardAI.All.Where(g => g && !g.IsPuppet && !g.IsBoss).ToList();
            R7Check("floor 4: every guard wears body armour (a vest on his chest)", armoured.Count > 0 && armoured.All(g => g.Armoured && g.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BodyArmour")), $"({armoured.Count(g => g.Armoured)} of {armoured.Count})");
            R7Check("floor 4: a safe", FloorSafe.Active != null);
            var victim = armoured.FirstOrDefault(g => !g.Dead);
            if (victim != null)
            {
                int bullet = RangedWeapon.Specs[0].guardHits;
                for (int i = 0; i < 4; i++) victim.WeaponHitHost(8f, null, bullet);
                R7Check("four pew bullets do not knock an armoured guard down for good (two hits each, eight of ten)", !victim.Dead && victim.Hits == 8, $"(hits {victim.Hits}, dead {victim.Dead})");
                victim.WeaponHitHost(8f, null, bullet);
                R7Check("the fifth one does (five bullets where an unarmoured guard takes three)", victim.Dead, $"(hits {victim.Hits})");
            }
            var other = armoured.FirstOrDefault(g => g && !g.Dead);
            if (other != null)
            {
                for (int i = 0; i < 9; i++) other.WeaponHitHost(1f, null, 1);
                bool alive9 = !other.Dead;
                other.WeaponHitHost(1f, null, 1);
                R7Check("a melee blow is still one hit against armour (ten blows knock him down)", alive9 && other.Dead, $"(hits {other.Hits})");
            }
'@ @'
            {
                var floor3 = GuardAI.All.Where(g => g && !g.IsPuppet && !g.IsBoss).ToList();
                bool vest3(GuardAI g) => g.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BodyArmour");
                R7Check("floor 3: some guards carry a pistol and wear a light shield (a vest on the chest), the others carry nothing and wear nothing; a safe",
                    floor3.Count(g => g.Loadout.Armed) >= 2 && floor3.Where(g => g.Loadout.Armed).All(g => g.Loadout.Kind == RangedWeapon.Kind.Pew && g.Shield == ShieldTier.Light && vest3(g))
                    && floor3.Where(g => !g.Loadout.Armed).All(g => g.Shield == ShieldTier.None && !vest3(g)) && FloorSafe.Active != null, $"({floor3.Count(g => g.Loadout.Armed)} armed of {floor3.Count})");
            }
            yield return R5LoadFloor(4);
            var armoured = GuardAI.All.Where(g => g && !g.IsPuppet && !g.IsBoss).ToList();
            var shielded = armoured.Where(g => g.Armoured).ToList();
            R7Check("floor 4: the armed guards wear a light shield (a vest on his chest), the others nothing", shielded.Count >= 2 && shielded.All(g => g.Loadout.Armed && g.Shield == ShieldTier.Light && g.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BodyArmour"))
                && armoured.Where(g => !g.Armoured).All(g => !g.Loadout.Armed && g.GetComponentsInChildren<Transform>(true).All(t => t.name != "BodyArmour")), $"({shielded.Count} shielded of {armoured.Count})");
            R7Check("floor 4: a safe", FloorSafe.Active != null);
            var victim = shielded.FirstOrDefault(g => !g.Dead);
            if (victim != null)
            {
                int bullet = RangedWeapon.Specs[0].guardHits;
                for (int i = 0; i < 3; i++) victim.BulletHitHost(bullet, false, null);
                R7Check("three pew bullets do not put a light-shield guard down (three of ten hits each, nine) and do not knock him over", !victim.Dead && victim.Hits == 9 && victim.State != GuardState.Stunned, $"(hits {victim.Hits}, dead {victim.Dead}, {victim.State})");
                victim.BulletHitHost(bullet, false, null);
                R7Check("the fourth one does (four bullets where an unshielded guard takes three)", victim.Dead, $"(hits {victim.Hits})");
            }
            var bare = armoured.Where(g => g && !g.Dead && !g.Armoured).ToList();
            if (bare.Count >= 2)
            {
                int bullet = RangedWeapon.Specs[0].guardHits;
                for (int i = 0; i < 3; i++) bare[0].BulletHitHost(bullet, false, null);
                R7Check("an unshielded guard takes three pew bullets", bare[0].Dead, $"(hits {bare[0].Hits})");
                for (int i = 0; i < 12; i++) bare[1].MeleeHitHost(0.05f, null);
                R7Check("a melee blow knocks a guard over and never knocks him out (twelve blows: damage 0, still alive)", !bare[1].Dead && bare[1].Hits == 0, $"(hits {bare[1].Hits})");
            }
            var other = armoured.FirstOrDefault(g => g && !g.Dead);
'@
Save-Src
