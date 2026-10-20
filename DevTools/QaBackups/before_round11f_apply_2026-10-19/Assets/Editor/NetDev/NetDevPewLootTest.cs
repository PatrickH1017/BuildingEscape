using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner's request of 2026-10-16: "add the armed guards on wave 4 and the pistol should be in the outpost on wave 4 100% of the time".
        /// The owner's request of 2026-10-17: the pistol first appears in the outpost of floor 3 (not 4), so it can be bought after floor 3.
        /// Pure checks: the guards are armed from floor 4 up and from no lower floor; the outpost of floor 3 holds a pew on every run seed (and
        /// no other floor's does), first in its list of weapons, and never more than the outpost has places for; the planner gives that
        /// outpost the places; the pistol has the one name the pickup and the loot list use.
        /// </summary>
        public static void PewLootTest()
        {
            Check("pew loot: the guards are armed from floor 3 and the pistol lies in floor 3's outpost", ArmedGuards.DefaultFirstArmedFloor == 3 && OutpostLoot.PewFloor == 3);
            Check("pew loot: the pistol is found on the floor where the guards are first armed (the owner, 2026-10-19), and the vendor sells it once that floor is finished", OutpostLoot.PewFloor == ArmedGuards.DefaultFirstArmedFloor && Vendor.PewAfterFloor == OutpostLoot.PewFloor);
            Check("pew loot: a game starts with the armed-floor rule at the first armed floor", ArmedGuards.FirstArmedFloor == ArmedGuards.DefaultFirstArmedFloor);

            int first = ArmedGuards.DefaultFirstArmedFloor;
            bool armedRule = true;
            for (int f = 1; f < first; f++) armedRule &= !ArmedRules.IsArmed(f, first, false);
            for (int f = first; f <= 13; f++) armedRule &= ArmedRules.IsArmed(f, first, false);
            Check("pew loot: guards on floors 1-2 are unarmed, on floors 3-13 some are armed (the rule for the floor)", armedRule);
            Check("pew loot: the docks have no guards to arm", !ArmedRules.IsArmed(0, first, false));

            // ---- the stash of every outpost, on many runs
            bool onlyPewFloor = true, always = true, firstInList = true, once = true, fits = true, deterministic = true, otherWeaponsKept = true;
            int runs = 0, minItems = int.MaxValue, maxWeapons = 0;
            for (int seed = 1; seed <= 500; seed++)
            {
                int runSeed = seed * 7919 + 13;
                for (int floor = 1; floor <= 13; floor++)
                {
                    var stash = OutpostLoot.Roll(runSeed, floor);
                    var again = OutpostLoot.Roll(runSeed, floor);
                    runs++;
                    int pews = 0;
                    foreach (int w in stash.weapons) if (w == OutpostLoot.PewKind) pews++;
                    deterministic &= again.weapons.Count == stash.weapons.Count && again.ItemCount == stash.ItemCount;
                    fits &= stash.ItemCount <= OutpostLoot.MaxItems;
                    if (floor == OutpostLoot.PewFloor)
                    {
                        always &= pews >= 1;
                        once &= pews == 1;
                        firstInList &= stash.weapons.Count > 0 && stash.weapons[0] == OutpostLoot.PewKind;
                        maxWeapons = Mathf.Max(maxWeapons, stash.weapons.Count);
                        // the melee weapons are what they were: at most OutpostLoot.MaxWeapons of them, as on every floor
                        otherWeaponsKept &= stash.weapons.Count - pews <= OutpostLoot.MaxWeapons;
                    }
                    else onlyPewFloor &= pews == 0;
                    minItems = Mathf.Min(minItems, stash.ItemCount);
                }
            }
            Check($"pew loot: the outpost of floor {OutpostLoot.PewFloor} holds a pew on all 500 runs", always);
            Check("pew loot: exactly one pew there", once);
            Check("pew loot: it is the first weapon put on a desk, so a full outpost drops a valuable before it", firstInList);
            Check("pew loot: no other floor's outpost has one", onlyPewFloor);
            Check($"pew loot: the melee weapons of floor {OutpostLoot.PewFloor}'s outpost are rolled as before (at most two)", otherWeaponsKept);
            Check($"pew loot: no outpost holds more than the {OutpostLoot.MaxItems} things it has places for (6500 stashes, floor {OutpostLoot.PewFloor} has {maxWeapons} weapons at most)", fits);
            Check("pew loot: the same run and floor give the same stash", deterministic);
            Check("pew loot: the loot list and the pickup call it the same thing", OutpostLoot.WeaponName(OutpostLoot.PewKind) == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].name);

            // ---- the planner gives the outpost of the pew floor the places (it adds places on the floor when its desks are too few)
            bool places = true, noteToo = true;
            int minPlaces = int.MaxValue, planned = 0;
            foreach (var (cols, rows) in new[] { (8, 5), (7, 5), (6, 4), (9, 6), (3, 2) })
                foreach (int sd in new[] { 4101, 777, 31, 9172, 2288, 515 })
                {
                    var shape = FloorShape.Maze(new System.Random(sd), cols, rows, 0.12);
                    var lay = OfficePlanner.Build(shape.PlanFor(OutpostLoot.PewFloor, sd, 18f, 3.5f, 777));
                    var op = lay.outpost;
                    if (op == null) { places = false; continue; }
                    planned++;
                    int items = 0;
                    foreach (var s in op.itemSlots) if (s.kind == "item") items++;
                    minPlaces = Mathf.Min(minPlaces, items);
                    places &= items >= OutpostLoot.MaxItems;
                    noteToo &= op.itemSlots.Exists(s => s.kind == "computer");
                }
            Check($"pew loot: floor {OutpostLoot.PewFloor}'s outpost has {OutpostLoot.MaxItems} places for things on all {planned} planned floors (fewest: {minPlaces})", places && planned == 30);
            Check("pew loot: and a place for its computer", noteToo);
        }
    }
}
