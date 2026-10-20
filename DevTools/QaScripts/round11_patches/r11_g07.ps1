# Round 11, batch G07 (the owner, 2026-10-19):
#  (1) "Safes should not always have a gun, it should be lucky to get a gun and even luckier to get the specific one you wanted"
#  (2) "The sniper and rocket launcher should be inside the outpost not in safes"
# A weapon safe now holds armour, a gun (40 % of the rest) or nothing; the new gun of a floor is no longer sure to be in the first safe; the sniper rifle is never in a safe, it lies on the desk of every outpost from floor 10.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor\NetDev'

# ======================================================================== the rules
Open-Src "$Src\Outpost\SafeRules.cs"
Rep @'
from floor 3 on the safes hold one weapon each (a pew pistol)
'@ @'
from floor 3 on the safes may hold a gun (by luck), a piece of armour or nothing
'@
Rep @'
        public const int WeaponsPerSafe = 1;
'@ @'
        public const int WeaponsPerSafe = 1;
        /// <summary>The chance (percent) that a weapon safe that holds no armour holds a gun at all (the owner, 2026-10-19: "Safes should not always have a gun, it should be lucky to get a gun and even luckier to get the specific one you wanted"). A placeholder number.</summary>
        public const int GunPercent = 40;
        /// <summary>How many times likelier the gun that unlocks on a floor is, on that floor, than it will be later: 1, no likelier (it was 3; the owner, 2026-10-19: "even luckier to get the specific one you wanted").</summary>
        public const int UnlockFloorBoost = 1;
'@
Rep @'
ONE gun (<see cref="WeaponsPerSafe"/>; the owner, 2026-10-18: "each safe should be one weapon max"), which one follows the floor (<see cref="PickGun"/>: floors 3 and
'@ @'
at most ONE thing (<see cref="WeaponsPerSafe"/>; the owner, 2026-10-18: "each safe should be one weapon max"): a piece of armour (<see cref="HoldsArmour"/>), a gun (<see cref="HoldsGun"/>, only some of the time: the owner, 2026-10-19, "Safes should not always have a gun") or nothing at all; which gun follows the floor (<see cref="PickGun"/>: floors 3 and
'@
Rep @'
            var items = new int[WeaponsPerSafe];
            for (int i = 0; i < items.Length; i++) items[i] = HoldsArmour(runSeed, floor, index, i) ? ArmourItemNumber(PickArmour(runSeed, floor, index, i)) : GunItem(PickGun(runSeed, floor, index, i));
            return items;
'@ @'
            var items = new int[WeaponsPerSafe];
            int held = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (HoldsArmour(runSeed, floor, index, i)) items[held++] = ArmourItemNumber(PickArmour(runSeed, floor, index, i));
                else if (HoldsGun(runSeed, floor, index, i)) items[held++] = GunItem(PickGun(runSeed, floor, index, i));
            }
            if (held < items.Length) Array.Resize(ref items, held);          // (a safe may hold no weapon at all: the owner, 2026-10-19)
            return items;
'@
Rep @'
(the order of <see cref="RangedWeapon.Kind"/>).</summary>
        static readonly int[] GunBaseWeight = { 24, 30, 20, 20, 10 };
'@ @'
(the order of <see cref="RangedWeapon.Kind"/>). The sniper rifle: never, 0 (the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes": <see cref="OutpostLoot.SniperFloor"/>).</summary>
        static readonly int[] GunBaseWeight = { 24, 30, 20, 20, 0 };
'@
Rep @'
its base weight after, three times that on its own floor (the new gun is the likeliest one there).
'@ @'
its base weight after, <see cref="UnlockFloorBoost"/> times that on its own floor (1: no likelier there; the owner, 2026-10-19: even luckier to get the specific one). The sniper rifle is never in a safe (weight 0).
'@
Rep @'
                w[k] = GunBaseWeight[k] * (floor == unlock ? 3 : 1);
'@ @'
                w[k] = GunBaseWeight[k] * (floor == unlock ? UnlockFloorBoost : 1);
'@
Rep @'
The gun in slot <paramref name="slot"/> of weapon safe <paramref name="index"/> on this floor: the floor that unlocks a gun always has it in its first safe (the one whose code is always on record, so the team can fetch it);
'@ @'
The kind of gun in slot <paramref name="slot"/> of weapon safe <paramref name="index"/> on this floor, when that safe holds one (<see cref="HoldsGun"/>):
'@
Rep @'
every other safe rolls by <see cref="GunWeights"/>. The same on every machine.
'@ @'
rolled by <see cref="GunWeights"/> whatever the safe (the owner, 2026-10-19: "even luckier to get the specific one you wanted" - the floor that unlocks a gun no longer has it in its first safe for sure). The same on every machine.
'@
RepRx 'if \(index == 0 && slot == 0\)\s*\{\s*int u = GunUnlockedOn\(floor\);\s*if \(u > 0\) return \(RangedWeapon\.Kind\)u;[^\r\n]*\r?\n\s*\}\r?\n\s*' ''
Rep @'
        /// <summary>The kind of the spare clip that may lie in this weapon safe: for the gun the safe holds. The pew's when the safe holds nothing.</summary>
'@ @'
        const int SaltGunChance = 59;
        /// <summary>
        /// Does this weapon safe hold a gun (the owner, 2026-10-19: "Safes should not always have a gun, it should be lucky to get a gun and even luckier to get the specific one you wanted")? Not when it holds armour instead (<see cref="HoldsArmour"/>,
        /// rolled first); otherwise <see cref="GunPercent"/> percent of the time - whatever the safe: no safe is sure to hold a gun, the first of a floor neither. Which gun it is: <see cref="PickGun"/>. The same on every machine.
        /// </summary>
        public static bool HoldsGun(int runSeed, int floor, int index, int slot = 0)
        {
            if (!WeaponSafeExists(runSeed, floor, index) || HoldsArmour(runSeed, floor, index, slot)) return false;
            return Hash(runSeed, floor, SaltGunChance + index * 149 + slot * 29) % 100U < (uint)GunPercent;
        }

        /// <summary>The kind of the spare clip that may lie in this weapon safe: for the gun the safe holds. The pew's when the safe holds nothing.</summary>
'@
Rep @'
            if (!WeaponSafeExists(runSeed, floor, index) || index == 0) return false;
'@ @'
            if (!WeaponSafeExists(runSeed, floor, index)) return false;
'@
Rep @'
? Never the first safe of a floor (a gun is always in it and its code is always on record) and never before floor 3; the chance is
'@ @'
? Any weapon safe may, the first of a floor too (no safe is sure to hold a gun any more: the owner, 2026-10-19), but never before floor 3; the chance is
'@
Save-Src

Open-Src "$Src\Guns\ArmourRules.cs"
Rep @'
that a safe that is not the first of its floor holds armour instead of a gun
'@ @'
that a weapon safe holds armour instead of a gun
'@
Rep @'
for one safe (not the first of its floor: a gun is always in it) to hold armour
'@ @'
for one safe to hold armour
'@
Save-Src

# ======================================================================== the sniper rifle lies in the outposts
Open-Src "$Src\Outpost\OutpostLoot.cs"
Rep @'
        public const int PewFloor = 3;
'@ @'
        public const int PewFloor = 3;
        /// <summary>The sniper rifle in a stash's weapon list: its number among what a safe holds (<see cref="SafeRules.GunItem"/>: the pew 3, the sniper rifle 7), though no safe ever holds one.</summary>
        public const int SniperKind = SafeRules.PewItem + 4;
        /// <summary>The first floor whose outposts hold a sniper rifle, the floor that unlocks it (the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes").</summary>
        public const int SniperFloor = 10;
'@
Rep @'
or <see cref="OutpostLoot.PewKind"/> (3, the pistol).</summary>
'@ @'
or <see cref="OutpostLoot.PewKind"/> (3, the pistol) or <see cref="OutpostLoot.SniperKind"/> (7, the sniper rifle).</summary>
'@
Rep @'
            if (floor == PewFloor) stash.weapons.Insert(0, PewKind);
'@ @'
            if (floor == PewFloor) stash.weapons.Insert(0, PewKind);
            if (floor >= SniperFloor) stash.weapons.Insert(0, SniperKind);          // (the sniper rifle: on every outpost from the floor that unlocks it, first on the desk; never in a safe - the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes")
'@
Rep @'
        public static string WeaponName(int kind) => kind == PewKind ? "Pew" : kind == BatKind ? "Baseball Bat" : kind == HatchetKind ? "Hatchet" : "Crowbar";
'@ @'
        public static string WeaponName(int kind) => kind == PewKind ? "Pew" : kind == SniperKind ? RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].name : kind == BatKind ? "Baseball Bat" : kind == HatchetKind ? "Hatchet" : "Crowbar";
'@
Save-Src

Open-Src "$Src\FloorGenerator.Outpost.cs"
Rep @'
                    bool pew = w == OutpostLoot.PewKind;
'@ @'
                    bool pew = w == OutpostLoot.PewKind, sniper = w == OutpostLoot.SniperKind;
'@
Rep @'
                    if (pew ? !RangedWeapon.Available : !MeleeWeapon.Available) continue;
'@ @'
                    if ((pew || sniper) ? !RangedWeapon.Available || (sniper && !RangedWeapon.Has(RangedWeapon.Kind.Sniper)) : !MeleeWeapon.Available) continue;
'@
Rep @'
                    else MeleeWeapon.Create((MeleeWeapon.Kind)w, at, parent, s.yaw);
'@ @'
                    else if (sniper) RangedWeapon.Create(RangedWeapon.Kind.Sniper, at, parent, s.yaw, 0);          // (the sniper rifle lies on the desk in the open, not in a safe: the owner, 2026-10-19; a gun found holds its one clip and no spare)
                    else MeleeWeapon.Create((MeleeWeapon.Kind)w, at, parent, s.yaw);
'@
Rep @'
(floor 3 and up: a pistol in every safe...)
'@ @'
(floor 3 and up: a gun in some of the safes - by luck -, armour in others, nothing in the rest...)
'@
Save-Src

# ======================================================================== the built-player test: a run seed whose floor-3 safes hold a pew, a clip, cash and a watch to take
Open-Src "$Src\Net\NetSession.cs"
Rep @'
        public void StartRun()
'@ @'
        /// <summary>QA only: the run seed the host starts the run with instead of a random one (0: random). The built-player autopilot picks one whose safes hold what it wants to take (a safe holds a gun only by luck).</summary>
        public int QaSeed;

        public void StartRun()
'@
Rep @'
            PendingSeed = UnityEngine.Random.Range(1, int.MaxValue);
'@ @'
            PendingSeed = QaSeed != 0 ? QaSeed : UnityEngine.Random.Range(1, int.MaxValue);
'@
Save-Src

Open-Src "$Src\QaAutopilot.cs"
Rep @'
qsGuns = 0, qsCash = 0,
'@ @'
qsGuns = 0, qsWantGuns = 0, qsCash = 0,
'@
Rep @'
for (int qi = 0; qi < qsWant; qi++) { if (SafeRules.HasSideCash(tm.IntelSeed, floor, qi)) qsWantCash++;
'@ @'
for (int qi = 0; qi < qsWant; qi++) { qsWantGuns += SafeRules.WeaponContents(tm.IntelSeed, floor, qi).Length; if (SafeRules.HasSideCash(tm.IntelSeed, floor, qi)) qsWantCash++;
'@
Rep @'
holding {(qsWeapons ? "one pistol each (and the cash and the clips the rules give)" : "cash")}
'@ @'
holding {(qsWeapons ? "what the rules give (a gun, a piece of armour or nothing, and the cash and the clips)" : "cash")}
'@
Rep @'
(qsWeapons ? qsGuns == qsWant && qsCash == qsWantCash
'@ @'
(qsWeapons ? qsGuns == qsWantGuns && qsCash == qsWantCash
'@
Rep @'
var cs = safes.Find(sf => sf.ClipCount > 0) ?? safes[0];
'@ @'
var cs = PickPewSafe(safes, seed, floor);
'@
Rep @'
var hs = safes.Find(sf => sf.ClipCount > 0) ?? safes[0];
'@ @'
var hs = PickPewSafe(safes, seed, floor);
'@
Rep @'
            Check("two players are in the lobby", net.Roster.Count == 2, $"({net.Roster.Count})");
            yield return Shot("n2_host_lobby_two");
            net.StartRun();
'@ @'
            Check("two players are in the lobby", net.Roster.Count == 2, $"({net.Roster.Count})");
            yield return Shot("n2_host_lobby_two");
            net.QaSeed = SeedWithPewSafe();          // (a safe holds a gun only by luck: this run's floor-3 safes hold a pew, a clip, a bit of cash and a watch for the test to take)
            net.StartRun();
'@
Rep @'
        /// <summary>
        /// The guards over the network (the owner's queue of 2026-10-19), floor 3, after the safes.
'@ @'
        /// <summary>The safe the safes test takes from: one that holds a gun (a safe holds one only by luck: the owner, 2026-10-19), with a spare clip when a safe has both; every machine picks the same one.</summary>
        static FloorSafe PickPewSafe(List<FloorSafe> safes, int seed, int floor) =>
            safes.Find(sf => SafeRules.HoldsGun(seed, floor, sf.Index) && sf.ClipCount > 0) ?? safes.Find(sf => SafeRules.HoldsGun(seed, floor, sf.Index)) ?? safes[0];

        /// <summary>A run seed whose floor-3 safes have one that holds a gun and also a spare clip, a bit of cash and a gold watch, so that the network test has all four to take (the host starts the run with it).</summary>
        static int SeedWithPewSafe()
        {
            for (int s = 1; s < 200000; s++)
            {
                int intel = s ^ 0x2D5AFE1;                                  // (what TowerManager makes of a run's seed)
                for (int i = 0, n = SafeRules.SafeCount(intel, 3); i < n; i++)
                    if (SafeRules.HoldsGun(intel, 3, i) && SafeRules.HasSideClip(intel, 3, i) && SafeRules.HasSideCash(intel, 3, i) && SafeRules.HasSideWatch(intel, 3, i)) return s;
            }
            return 0;
        }

        /// <summary>
        /// The guards over the network (the owner's queue of 2026-10-19), floor 3, after the safes.
'@
Save-Src

# ======================================================================== the tests
Open-Src "$Ed\NetDevGunsTest.cs"
RepRegion 'Check("safes: which guns can be in a safe on which floor - none before floor 3, the pew alone on 3 and 4, the SMG from 5, the shotgun from 6, the rifle from 8, the sniper rifle from 10",' 'seenKinds.Count == 5 && clipMatches, $"({seenKinds.Count} kinds, clips match {clipMatches})");' @'
Check("safes: which guns can be in a safe on which floor - none before floor 3, the pew alone on 3 and 4, the SMG from 5, the shotgun from 6, the rifle from 8, and never the sniper rifle (it lies in the guard outpost: the owner, 2026-10-19)",
                Sum(SafeRules.GunWeights(2)) == 0 && Only(SafeRules.GunWeights(3), pew) && Only(SafeRules.GunWeights(4), pew)
                && Has(SafeRules.GunWeights(5), pew, smg) && Has(SafeRules.GunWeights(7), pew, smg, shotgun) && Has(SafeRules.GunWeights(8), pew, smg, shotgun, rifle) && Has(SafeRules.GunWeights(10), pew, smg, shotgun, rifle)
                && Has(SafeRules.GunWeights(12), pew, smg, shotgun, rifle));
            var w5 = SafeRules.GunWeights(5); var w6 = SafeRules.GunWeights(6);
            Check("safes: the gun that unlocks on a floor is no likelier there than it will be later (the owner, 2026-10-19: \"even luckier to get the specific one you wanted\")", SafeRules.UnlockFloorBoost == 1 && w5[smg] == SafeRules.GunWeights(6)[smg] && w6[shotgun] == SafeRules.GunWeights(7)[shotgun] && w5[pew] == SafeRules.GunWeights(6)[pew]);
            Check("safes: the floor each gun is first found on", SafeRules.GunUnlockedOn(3) == pew && SafeRules.GunUnlockedOn(5) == smg && SafeRules.GunUnlockedOn(6) == shotgun && SafeRules.GunUnlockedOn(8) == rifle && SafeRules.GunUnlockedOn(10) == sniper
                && SafeRules.GunUnlockedOn(4) == -1 && SafeRules.GunUnlockedOn(7) == -1 && SafeRules.GunUnlockedOn(9) == -1 && SafeRules.GunUnlockedOn(11) == -1);

            bool onlyPewEarly = true, allAvailable = true, deterministic = true, atMostOne = true, clipMatches = true, noSniper = true, consistent = true;
            var seenKinds = new System.Collections.Generic.HashSet<int>();
            int safesTotal = 0, safesWithGun = 0, safesWithArmour = 0, safesEmpty = 0, runsOn5 = 0, smgFirstOn5 = 0, smgOn5 = 0, safesOn5 = 0, shotgunOn6 = 0, safesOn6 = 0;
            for (int sd = 0; sd < 150; sd++)
                for (int f = 3; f <= 12; f++)
                {
                    int rs = sd * 77 + 5, n = SafeRules.SafeCount(rs, f);
                    if (f == 5) runsOn5++;
                    for (int i = 0; i < n; i++)
                    {
                        var items1 = SafeRules.WeaponContents(rs, f, i); var items2 = SafeRules.WeaponContents(rs, f, i);
                        atMostOne &= items1.Length <= 1;
                        deterministic &= items1.Length == items2.Length && (items1.Length == 0 || items1[0] == items2[0]);
                        bool armour = items1.Length == 1 && SafeRules.IsArmourItem(items1[0]), gun = items1.Length == 1 && SafeRules.IsGunItem(items1[0]);
                        consistent &= SafeRules.HoldsArmour(rs, f, i) == armour && SafeRules.HoldsGun(rs, f, i) == gun && (items1.Length == 0 || armour || gun);
                        safesTotal++;
                        if (armour) safesWithArmour++;
                        else if (!gun) safesEmpty++;
                        if (f == 5) safesOn5++;
                        if (f == 6) safesOn6++;
                        clipMatches &= (int)SafeRules.ClipKindOf(rs, f, i) == (gun ? (int)SafeRules.GunOfItem(items1[0]) : pew);
                        if (!gun) continue;                                              // (nothing, or armour instead of a gun: ArmourTest has the armour's rules)
                        safesWithGun++;
                        int kind = (int)SafeRules.GunOfItem(items1[0]);
                        allAvailable &= f >= S[kind].unlockFloor;
                        noSniper &= kind != sniper;
                        if (f <= 4) onlyPewEarly &= kind == pew;
                        if (f == 12) seenKinds.Add(kind);
                        if (f == 5 && kind == smg) { smgOn5++; if (i == 0) smgFirstOn5++; }
                        if (f == 6 && kind == shotgun) shotgunOn6++;
                    }
                }
            int notArmour = safesTotal - safesWithArmour;
            float gunShare = notArmour > 0 ? safesWithGun / (float)notArmour : 0f;
            Check("safes: a weapon safe holds at most one thing (nothing, a gun or a piece of armour), the same on every machine, and the owner's floors keep to it - floors 3 and 4 the pew only, no gun before its floor, never the sniper rifle",
                atMostOne && deterministic && consistent && onlyPewEarly && allAvailable && noSniper, $"(at most one {atMostOne}, deterministic {deterministic}, the rules agree {consistent}, pew only early {onlyPewEarly}, available {allAvailable}, no sniper {noSniper})");
            Check("safes: it is lucky to get a gun - a safe that holds no armour holds one about 4 times in 10, and some safes hold nothing at all (the owner, 2026-10-19: \"Safes should not always have a gun\")",
                SafeRules.GunPercent == 40 && System.Math.Abs(gunShare - 0.40f) < 0.05f && safesEmpty > 0 && safesWithGun < notArmour, $"({safesWithGun} of {notArmour} safes without armour hold a gun = {gunShare:0.00}; {safesEmpty} hold nothing, {safesWithArmour} armour, of {safesTotal})");
            Check("safes: and luckier still to get the specific one - the first safe of the floor that unlocks the SMG holds it in some runs only, and one kind is a small share of all the safes (the SMG on floor 5, the shotgun on floor 6)",
                smgFirstOn5 > 0 && smgFirstOn5 < runsOn5 / 2 && smgOn5 > 0 && smgOn5 < safesOn5 / 4 && shotgunOn6 > 0 && shotgunOn6 < safesOn6 / 6, $"(SMG in the first safe of floor 5: {smgFirstOn5} of {runsOn5} runs, in any safe of floor 5: {smgOn5} of {safesOn5}; shotgun on floor 6: {shotgunOn6} of {safesOn6})");
            Check("safes: floor 12 has every kind of gun that can be in a safe about (a mix of all four over 150 runs, no sniper rifle), and the spare clip in a safe is for the gun in it (the pew's when it holds none)", seenKinds.Count == 4 && !seenKinds.Contains(sniper) && clipMatches, $"({seenKinds.Count} kinds, clips match {clipMatches})");
'@
Save-Src

Open-Src "$Ed\NetDevR7Test.cs"
Rep @'
contents &= items.Length == 1 && SafeRules.WeaponsPerSafe == 1;
'@ @'
contents &= items.Length <= 1 && SafeRules.WeaponsPerSafe == 1;
'@
Rep @'
floors 1 and 2 hold no weapon, every other safe exactly one gun (never two; the pew on floors 3 and 4, the guns of the owner's ladder after), a safe that is not there holds nothing
'@ @'
floors 1 and 2 hold no weapon, every other safe at most one thing (a gun, a piece of armour or nothing - never two; the pew on floors 3 and 4, the guns of the owner's ladder after), a safe that is not there holds nothing
'@
Save-Src

Open-Src "$Ed\NetDevArmourTest.cs"
Rep @'
bool firstNever = true, deterministic = true, oneEach = true, noClip = true, available = true, clipsOk = true;
'@ @'
int firstHas = 0; bool deterministic = true, oneEach = true, noClip = true, available = true, clipsOk = true;
'@
Rep @'
oneEach &= a.Length == 1;
'@ @'
oneEach &= a.Length <= 1;
'@
Rep @'
deterministic &= a.Length == b.Length && a[0] == b[0] && SafeRules.HoldsArmour(rs, f, i) == SafeRules.HoldsArmour(rs, f, i);
'@ @'
deterministic &= a.Length == b.Length && (a.Length == 0 || a[0] == b[0]) && SafeRules.HoldsArmour(rs, f, i) == SafeRules.HoldsArmour(rs, f, i);
'@
Rep @'
bool arm = SafeRules.IsArmourItem(a[0]);
'@ @'
bool arm = a.Length > 0 && SafeRules.IsArmourItem(a[0]);
'@
Rep @'
firstNever &= !(i == 0 && arm) && !(i == 0 && SafeRules.HoldsArmour(rs, f, i));
'@ @'
if (i == 0 && arm) firstHas++;          // (no safe is sure to hold a gun any more, so the first safe of a floor may hold armour too)
'@
Rep @'
a safe holds exactly one thing (a gun or a piece of armour), the same on every machine, never the first safe of a floor (a gun is always in it), no armour before its floor, and none with a spare pistol clip
'@ @'
a safe holds at most one thing (a gun, a piece of armour or nothing), the same on every machine, any safe may hold armour (the first of a floor too), no armour before its floor, and none with a spare pistol clip
'@
Rep @'
oneEach && deterministic && firstNever && clipsOk
'@ @'
oneEach && deterministic && firstHas > 0 && clipsOk
'@
Rep @'
first never {firstNever}
'@ @'
armour in a first safe {firstHas} times
'@
Save-Src

Open-Src "$Ed\NetDevArmour.cs"
Rep @'
if (SafeRules.HoldsArmour(s, 3, 1) && SafeRules.PickArmour(s, 3, 1, 0) == ShieldTier.Light) seed = s;
'@ @'
if (SafeRules.HoldsGun(s, 3, 0) && SafeRules.HoldsArmour(s, 3, 1) && SafeRules.PickArmour(s, 3, 1, 0) == ShieldTier.Light) seed = s;
'@
Save-Src

Open-Src "$Ed\NetDevOutpost.cs"
Rep @'
!WatchOn(seed, 3) || !WatchOn(seed, 7)) seed++;
'@ @'
!WatchOn(seed, 3) || !WatchOn(seed, 7) || !GunsOn(seed, 3) || !TallOn(seed, 7)) seed++;
'@
Rep @'
wsGuns &= wsS.GunCount == wsItems && wsItems == 1;
'@ @'
wsGuns &= wsS.GunCount == wsItems && wsItems <= 1;
'@
Rep @'
each holds exactly one pistol, as the rules say
'@ @'
each holds what the rules say: a gun, a piece of armour or nothing, never two
'@
Rep @'
wsExpected += SafeRules.IsGunItem(SafeRules.WeaponContents(seed, wsFloor, wsS.Index)[0]) ? 1 : 0;
'@ @'
wsExpected += GunsInSafe(seed, wsFloor, wsS.Index);
'@
Rep @'
xl.y < 0.04f || xl.y > 0.5f || xl.z < -0.30f
'@ @'
xl.y < 0.04f || xl.y > (xSafe.Tall ? 1.6f : 0.5f) || xl.z < -0.30f
'@
Rep @'
        /// <summary>Does this world position lie in the hollow of the safe?</summary>
'@ @'
        /// <summary>Do the safes of this floor hold at least one gun (a safe holds one only by luck: the owner, 2026-10-19), so that a test can try it?</summary>
        static bool GunsOn(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HoldsGun(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Does a safe of this floor hold a shotgun, so that its vertical safe can be tried?</summary>
        static bool TallOn(int runSeed, int floor)
        {
            for (int i = 0, n = SafeRules.SafeCount(runSeed, floor); i < n; i++) if (SafeRules.HoldsTallGun(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>Does this world position lie in the hollow of the safe?</summary>
'@
Save-Src

Open-Src "$Ed\NetDevGunSheet.cs"
Rep @'
if (!RangedWeapon.Has(kind)) continue;
'@ @'
if (!RangedWeapon.Has(kind) || kind == RangedWeapon.Kind.Sniper) continue;          // (no safe holds a sniper rifle: it lies in the guard outpost)
'@
Save-Src

Open-Src "$Ed\NetDevLootQa.cs"
Rep @'
.Where(g => g && !FloorSafe.HoldsGun(g.gameObject)).ToList();
'@ @'
.Where(g => g && g.kind == RangedWeapon.Kind.Pew && !FloorSafe.HoldsGun(g.gameObject)).ToList();
'@
Save-Src

Open-Src "$Ed\NetDevR5Test.cs"
Rep @'
int melee = s.weapons.Count(w => w != OutpostLoot.PewKind);
'@ @'
int melee = s.weapons.Count(w => w < OutpostLoot.PewKind);          // (the melee weapons are numbered 0 to 2; the pew and the sniper rifle are guns)
'@
Save-Src

Open-Src "$Ed\NetDevPewLootTest.cs"
Rep @'
OutpostLoot.WeaponName(OutpostLoot.PewKind) == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].name);
'@ @'
OutpostLoot.WeaponName(OutpostLoot.PewKind) == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].name);

            // ---- the sniper rifle lies in the outposts, never in a safe (the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes")
            bool snOnce = true, snFirst = true, snNone = true, snFits = true, snSame = true;
            for (int snSeed = 1; snSeed <= 300; snSeed++)
                for (int snFloor = 1; snFloor <= 13; snFloor++)
                {
                    var snStash = OutpostLoot.Roll(snSeed * 7919 + 13, snFloor);
                    int snCount = 0; foreach (int snW in snStash.weapons) if (snW == OutpostLoot.SniperKind) snCount++;
                    if (snFloor >= OutpostLoot.SniperFloor) { snOnce &= snCount == 1; snFirst &= snStash.weapons.Count > 0 && snStash.weapons[0] == OutpostLoot.SniperKind; }
                    else snNone &= snCount == 0;
                    snFits &= snStash.ItemCount <= OutpostLoot.MaxItems;
                    snSame &= OutpostLoot.Roll(snSeed * 7919 + 13, snFloor).weapons.Count == snStash.weapons.Count;
                }
            Check("sniper loot: from floor 10 on every outpost holds exactly one sniper rifle, first on the desk, none before it (300 runs), and none holds more than it has places for", snOnce && snFirst && snNone && snFits && snSame
                && OutpostLoot.SniperFloor == RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].unlockFloor && OutpostLoot.SniperKind == SafeRules.GunItem(RangedWeapon.Kind.Sniper));
            Check("sniper loot: the loot list and the pickup call it the same thing", OutpostLoot.WeaponName(OutpostLoot.SniperKind) == RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper].name);
'@
Save-Src

Open-Src "$Ed\NetDevPewLoot.cs"
Rep @'
Where(r => r && r.GetComponent<Pickup>() && !FloorSafe.HoldsGun(r.gameObject))
'@ @'
Where(r => r && r.kind == RangedWeapon.Kind.Pew && r.GetComponent<Pickup>() && !FloorSafe.HoldsGun(r.gameObject))
'@ 2
Rep @'
foreach (int floor in new[] { 1, 2, 3, 4, 5, 6 })
'@ @'
bool gfx = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            foreach (int floor in new[] { 1, 2, 3, 4, 5, 6, 9, 10 })
'@
Rep @'
else PewLootCheck($"{f} no pew lies on the floor", pews.Count == 0, $"({pews.Count})");
'@ @'
else PewLootCheck($"{f} no pew lies on the floor", pews.Count == 0, $"({pews.Count})");

                // ---- the sniper rifle lies in the outpost from floor 10 on, never in a safe (the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes")
                var snipers = Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r && r.kind == RangedWeapon.Kind.Sniper && r.GetComponent<Pickup>() && !FloorSafe.HoldsGun(r.gameObject)).Select(r => r.GetComponent<Pickup>()).ToList();
                if (floor >= OutpostLoot.SniperFloor)
                {
                    var sn = snipers.Count == 1 ? snipers[0] : null;
                    var snSpec = RangedWeapon.Specs[(int)RangedWeapon.Kind.Sniper];
                    bool snInRoom = sn != null && hasOutpost && room.Grow(0.4f).Contains(sn.transform.position.x, sn.transform.position.z);
                    PewLootCheck($"{f} exactly one sniper rifle lies on the floor: in the guard outpost, on a desk, in the open and in the world", sn != null && snInRoom && sn.transform.position.y > 0.35f && sn.gameObject.activeInHierarchy, sn != null ? $"({snipers.Count}, y {sn.transform.position.y:0.00})" : $"({snipers.Count})");
                    PewLootCheck($"{f} ... it is a pickup that is called as its spec says, weighs what the spec says and has a network id", sn != null && sn.Label == snSpec.name && Mathf.Approximately(sn.weight, snSpec.weight) && sn.NetId >= 0);
                    int snInSafes = Object.FindObjectsByType<RangedWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Count(r => r && r.kind == RangedWeapon.Kind.Sniper && FloorSafe.HoldsGun(r.gameObject));
                    PewLootCheck($"{f} ... and no safe holds one", snInSafes == 0, $"({snInSafes} in safes)");
                    if (gfx && sn != null)
                    {
                        // two pictures of it on its desk, from above at either end
                        foreach (var look in new[] { new Vector3(0.9f, 1.5f, 0.9f), new Vector3(-0.9f, 1.5f, -0.9f) })
                        {
                            var eyeS = new GameObject("SniperEye").AddComponent<Camera>();
                            eyeS.clearFlags = CameraClearFlags.SolidColor; eyeS.backgroundColor = new Color(0.2f, 0.22f, 0.26f); eyeS.fieldOfView = 55f; eyeS.enabled = false;
                            eyeS.transform.position = sn.transform.position + look;
                            eyeS.transform.LookAt(sn.transform.position);
                            yield return null;
                            ShotCam(eyeS, $"sniper_outpost_floor{floor}_{(look.x > 0f ? "a" : "b")}");
                            Destroy(eyeS.gameObject);
                        }
                    }
                    if (sn != null && pack != null && gc != null)
                    {
                        pack.TryStore(sn);
                        yield return new WaitForSeconds(0.6f);
                        PewLootCheck($"{f} picked up, the sniper rifle is in the pack and in the hand", pack.Contains(sn) && gc.Gun != null && gc.Gun.kind == RangedWeapon.Kind.Sniper);
                        RunUnlocks.Reset();
                        int snUnlocked = RunUnlocks.CollectFrom(pack);
                        PewLootCheck($"{f} carried back to the docks it unlocks the sniper's ammunition for the party", snUnlocked >= 1 && RunUnlocks.Has(RangedWeapon.Kind.Sniper), $"({snUnlocked})");
                        RunUnlocks.Reset();
                    }
                }
                else PewLootCheck($"{f} no sniper rifle lies about", snipers.Count == 0, $"({snipers.Count})");
'@
Save-Src
