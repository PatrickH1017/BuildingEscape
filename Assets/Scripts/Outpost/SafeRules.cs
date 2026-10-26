using System;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of the safes and of the notes that tell the team about them, in one place and free of Unity types (so they can be
    /// tested outside Unity). Everything is worked out from the run's seed and the floor number alone, with an integer hash, so every
    /// machine of a networked run - and the guard outpost on an earlier floor, which has to name a safe on a floor that has not been
    /// built yet - gets exactly the same answer.
    ///
    ///  - A floor (1 to 12) has a safe (every floor does, see OneIn). Floors 1 and 2 have one, with cash in it; from floor 3 on the safes may hold a gun (by luck; not on floor 3 itself: see NoGunFloor), a piece of armour or nothing and there are several (up to four): two on floor 3, three on floors 4 to 6, four from floor 7 (the owner, 2026-10-18).
    ///  - Every safe has a 4-digit code. The note about it either gives the code and the room, or only the room ("it has a safe, nobody
    ///    knows the code"): then it takes a crowbar, which breaks 35% of the time.
    ///  - The record of a floor's safes is on the computer of that floor's own guard outpost, and the computer shows the safes of that floor and no other (the owner, 2026-10-18: "only the safes for that current floor").
    ///  - A weapon safe may also hold a bit of cash (about 40 percent of them) and a spare clip for the pistol (about 50 percent): each is an item of its own, picked up on its own (the owner, 2026-10-18).
    ///  - The cash inside (floors 1 and 2) is half the floor's quota: $2,150 on floor 1. The computer's record says, for every safe of its floor, the room and the code (always for the first safe of a weapon floor, else most of the time), and nothing about what is inside (the owner, 2026-10-18).
    /// </summary>
    public static class SafeRules
    {
        /// <summary>One floor in this many has a safe (the owner, 2026-10-18, after the first playtests: "There should be a safe on every floor": 1; it was 5).</summary>
        public const int OneIn = 1;
        public const int FirstFloor = 1;
        /// <summary>The highest floor that can have a safe (the floor with the hidden hallway; the boss floor has none).</summary>
        public const int LastFloor = TowerRules.KeyFloor;
        /// <summary>The chance that a crowbar breaks on a safe it has pried open.</summary>
        public const float CrowbarBreakChance = 0.35f;
        /// <summary>Share of the safes whose note gives the code (the rest only say where the safe is).</summary>
        public const int NoteGivesCodePercent = 62;
        /// <summary>Every safe's code is on record in the computer's Safes tab (the owner, 2026-10-23: "All Safes should have a code in the Safes tab"). Set it to false for the old rule: a share of the records (<see cref="NoteGivesCodePercent"/>) left the code out and the safe had to be pried open.</summary>
        public static bool EveryRecordGivesCode = true;
        /// <summary>The cash inside, as a share of the floor's quota.</summary>
        public const float CashShareOfQuota = 0.5f;
        /// <summary>From this floor on the safes hold weapons, not cash (the owner, 2026-10-18); floors 1 and 2 still hold cash.</summary>
        public const int WeaponFirstFloor = 3;
        /// <summary>The floor whose safes hold no gun and no spare clip: the one pistol of floor 3 is in the guard outpost's gun safe (the owner, 2026-10-19 night: "there should only be one pistol on floor 3 (the one inside the outpost). Floor 3 should have no weapons or ammo in the safes"). Armour, cash and watches can still be in them.</summary>
        public const int NoGunFloor = 3;
        /// <summary>Do the safes of this floor hold guns and spare clips (every weapon floor but <see cref="NoGunFloor"/>)?</summary>
        public static bool SafesHoldGuns(int floor) => floor != NoGunFloor;
        /// <summary>The most safes one floor can have.</summary>
        public const int MaxSafes = 4;
        /// <summary>The number of the pew pistol among what a safe holds (the same number as <see cref="OutpostLoot.PewKind"/>).</summary>
        public const int PewItem = 3;
        /// <summary>The most weapons one safe holds (the owner, 2026-10-18: "each safe should be one weapon max").</summary>
        public const int WeaponsPerSafe = 1;
        /// <summary>The chance (percent) that a weapon safe that holds no armour holds a gun at all (the owner, 2026-10-19: "Safes should not always have a gun, it should be lucky to get a gun and even luckier to get the specific one you wanted"). A placeholder number.</summary>
        public const int GunPercent = 40;
        /// <summary>How many times likelier the gun that unlocks on a floor is, on that floor, than it will be later: 1, no likelier (it was 3; the owner, 2026-10-19: "even luckier to get the specific one you wanted").</summary>
        public const int UnlockFloorBoost = 1;

        // ------------------------------------------------------------------ hashing

        /// <summary>Good 32-bit mixing (Chris Wellons' "lowbias32").</summary>
        static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352dU;
                x ^= x >> 15; x *= 0x846ca68bU;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>A number from the run seed and two more numbers; the same inputs always give the same output, on every machine.</summary>
        public static uint Hash(int seed, int a, int b)
        {
            unchecked
            {
                uint h = Mix((uint)seed ^ 0x9E3779B9U);
                h = Mix(h + (uint)a * 0x85EBCA6BU + 0x1234567U);
                h = Mix(h + (uint)b * 0xC2B2AE35U + 0x7654321U);
                return h;
            }
        }

        /// <summary>The hash as a number in [0, 1).</summary>
        public static float Unit(uint hash) => (hash >> 8) / 16777216f;

        const int SaltExists = 1, SaltCode = 2, SaltNoteCode = 3, SaltRoom = 5, SaltCash = 6, SaltIndexCode = 31, SaltIndexNote = 32, SaltSideCash = 34, SaltSideClip = 35, SaltSideWatch = 36;

        // ------------------------------------------------------------------ which floors have a safe

        /// <summary>Can this floor have a safe at all?</summary>
        public static bool IsSafeFloor(int floor) => floor >= FirstFloor && floor <= LastFloor;

        /// <summary>Does this floor have a safe? (Every office floor does; <see cref="OneIn"/> could make it a lottery again.)</summary>
        public static bool HasSafe(int runSeed, int floor) =>
            IsSafeFloor(floor) && Hash(runSeed, floor, SaltExists) % OneIn == 0;

        /// <summary>The 4-digit code of the safe on this floor (1000 to 9999), whether or not the floor has a safe.</summary>
        public static int Code(int runSeed, int floor) => 1000 + (int)(Hash(runSeed, floor, SaltCode) % 9000U);

        /// <summary>Does the note about the safe on this floor give its code? (If not, it only says which room the safe is in.)</summary>
        public static bool NoteGivesCode(int runSeed, int floor) =>
            EveryRecordGivesCode || Hash(runSeed, floor, SaltNoteCode) % 100U < (uint)NoteGivesCodePercent;

        /// <summary>A number the planner uses to choose the safe's room among those that can take one.</summary>
        public static uint RoomRoll(int runSeed, int floor) => Hash(runSeed, floor, SaltRoom);

        /// <summary>The floors that have a safe, in order.</summary>
        public static int[] SafeFloors(int runSeed)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int f = FirstFloor; f <= LastFloor; f++) if (HasSafe(runSeed, f)) list.Add(f);
            return list.ToArray();
        }

        // ------------------------------------------------------------------ several safes, and weapons in them (the owner, 2026-10-18)

        /// <summary>Do the safes of this floor hold weapons (floor 3 and up) rather than cash (floors 1 and 2)?</summary>
        public static bool IsWeaponFloor(int floor) => floor >= WeaponFirstFloor;

        /// <summary>Do the safes of this floor hold cash (floors 1 and 2)?</summary>
        public static bool HoldsCash(int floor) => IsSafeFloor(floor) && !IsWeaponFloor(floor);

        /// <summary>How many safes this floor has: one on floors 1 and 2, two on floor 3, three on floors 4 to 6, four from floor 7 ("floor three and up should also have multiple safes").</summary>
        public static int SafeCount(int runSeed, int floor)
        {
            if (!HasSafe(runSeed, floor)) return 0;
            if (floor < WeaponFirstFloor) return 1;
            return floor == WeaponFirstFloor ? 2 : floor < 7 ? 3 : MaxSafes;
        }

        /// <summary>The code of safe number <paramref name="index"/> (0 first) on this floor; the first one is <see cref="Code(int,int)"/>, and no two safes of a floor have the same.</summary>
        public static int Code(int runSeed, int floor, int index)
        {
            if (index <= 0) return Code(runSeed, floor);
            for (int attempt = 0; ; attempt++)
            {
                int code = 1000 + (int)(Hash(runSeed, floor, SaltIndexCode + index * 101 + attempt * 1009) % 9000U);
                bool taken = false;
                for (int i = 0; i < index; i++) if (Code(runSeed, floor, i) == code) taken = true;
                if (!taken || attempt > 30) return code;
            }
        }

        /// <summary>
        /// Does the computer's record give the code of safe number <paramref name="index"/> on this floor (otherwise it only says where the safe is, and it takes a crowbar)? On a floor with weapons the first
        /// safe's code is always on record, so that the team always has a safe to open; the others are on record <see cref="NoteGivesCodePercent"/> percent of the time. Floors 1 and 2: as before.
        /// </summary>
        public static bool RecordGivesCode(int runSeed, int floor, int index)
        {
            if (index < 0 || index >= SafeCount(runSeed, floor)) return false;
            if (EveryRecordGivesCode) return true;
            if (!IsWeaponFloor(floor)) return NoteGivesCode(runSeed, floor);
            if (index == 0) return true;
            return Hash(runSeed, floor, SaltIndexNote + index * 103) % 100U < (uint)NoteGivesCodePercent;
        }

        /// <summary>Is there a safe on this floor whose code the record does not give (so that a crowbar is wanted)?</summary>
        public static bool AnySafeNeedsCrowbar(int runSeed, int floor)
        {
            int n = SafeCount(runSeed, floor);
            for (int i = 0; i < n; i++) if (!RecordGivesCode(runSeed, floor, i)) return true;
            return false;
        }

        /// <summary>
        /// What a weapon safe holds, as item numbers (<see cref="GunItem"/>): at most ONE thing (<see cref="WeaponsPerSafe"/>; the owner, 2026-10-18: "each safe should be one weapon max"): a piece of armour (<see cref="HoldsArmour"/>), a gun (<see cref="HoldsGun"/>, only some of the time: the owner, 2026-10-19, "Safes should not always have a gun") or nothing at all; which gun follows the floor (<see cref="PickGun"/>: floors 3 and
        /// 4 hold the pew only, as before). A melee weapon is longer than the safe is wide, so a safe holds guns only. Nothing for a safe on a floor with cash, or one that does not exist.
        /// </summary>
        public static int[] WeaponContents(int runSeed, int floor, int index)
        {
            if (!IsWeaponFloor(floor) || index < 0 || index >= SafeCount(runSeed, floor)) return new int[0];
            var items = new int[WeaponsPerSafe];
            int held = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (HoldsArmour(runSeed, floor, index, i)) items[held++] = ArmourItemNumber(PickArmour(runSeed, floor, index, i));
                else if (HoldsGun(runSeed, floor, index, i)) items[held++] = GunItem(PickGun(runSeed, floor, index, i));
            }
            if (held < items.Length) Array.Resize(ref items, held);          // (a safe may hold no weapon at all: the owner, 2026-10-19)
            return items;
        }

        // ------------------------------------------------------------------ which gun (the owner's ladder of unlocks, 2026-10-19)

        const int SaltGun = 41;
        /// <summary>How likely each gun is in a weapon safe, before the floor changes it: the pew, the sub machine gun, the rifle, the shotgun, the sniper rifle (the order of <see cref="RangedWeapon.Kind"/>). The sniper rifle: never, 0 (the owner, 2026-10-19: "The sniper and rocket launcher should be inside the outpost not in safes": <see cref="OutpostLoot.SniperFloor"/>).</summary>
        static readonly int[] GunBaseWeight = { 24, 30, 20, 20, 0 };

        /// <summary>The number of a gun among what a safe holds: <see cref="PewItem"/> plus the gun's kind (the pew 3, the sub machine gun 4, the rifle 5, the shotgun 6, the sniper rifle 7).</summary>
        public static int GunItem(RangedWeapon.Kind kind) => PewItem + (int)kind;
        public static bool IsGunItem(int item) => item >= PewItem && item < PewItem + RangedWeapon.Specs.Length;
        public static RangedWeapon.Kind GunOfItem(int item) => (RangedWeapon.Kind)(item - PewItem);

        /// <summary>
        /// How likely each gun is to be the one a weapon safe on this floor holds, by kind (the owner, 2026-10-19: the floors that unlock the guns are theirs - the pew 3, the sub machine gun 5, the shotgun 6, the assault rifle 8, the
        /// sniper rifle 10, <see cref="RangedWeapon.Spec.unlockFloor"/>): nothing for a gun before its floor, its base weight after, <see cref="UnlockFloorBoost"/> times that on its own floor (1: no likelier there; the owner, 2026-10-19: even luckier to get the specific one). The sniper rifle is never in a safe (weight 0).
        /// </summary>
        public static int[] GunWeights(int floor)
        {
            var w = new int[RangedWeapon.Specs.Length];
            for (int k = 0; k < w.Length; k++)
            {
                int unlock = RangedWeapon.Specs[k].unlockFloor;
                if (floor < unlock) continue;
                w[k] = GunBaseWeight[k] * (floor == unlock ? UnlockFloorBoost : 1);
            }
            return w;
        }

        /// <summary>The kind of gun that is first found on this floor, or -1 when no gun is.</summary>
        public static int GunUnlockedOn(int floor)
        {
            for (int k = 0; k < RangedWeapon.Specs.Length; k++) if (RangedWeapon.Specs[k].unlockFloor == floor) return k;
            return -1;
        }

        /// <summary>
        /// The kind of gun in slot <paramref name="slot"/> of weapon safe <paramref name="index"/> on this floor, when that safe holds one (<see cref="HoldsGun"/>):
        /// rolled by <see cref="GunWeights"/> whatever the safe (the owner, 2026-10-19: "even luckier to get the specific one you wanted" - the floor that unlocks a gun no longer has it in its first safe for sure). The same on every machine.
        /// </summary>
        public static RangedWeapon.Kind PickGun(int runSeed, int floor, int index, int slot)
        {
            var w = GunWeights(floor);
            long total = 0;
            for (int k = 0; k < w.Length; k++) total += w[k];
            if (total <= 0) return RangedWeapon.Kind.Pew;
            long roll = Hash(runSeed, floor, SaltGun + index * 131 + slot * 17) % (uint)total;
            for (int k = 0; k < w.Length; k++)
            {
                if (roll < w[k]) return (RangedWeapon.Kind)k;
                roll -= w[k];
            }
            return RangedWeapon.Kind.Pew;
        }

        const int SaltGunChance = 59;
        /// <summary>
        /// Does this weapon safe hold a gun (the owner, 2026-10-19: "Safes should not always have a gun, it should be lucky to get a gun and even luckier to get the specific one you wanted")? Not when it holds armour instead (<see cref="HoldsArmour"/>,
        /// rolled first); otherwise <see cref="GunPercent"/> percent of the time - whatever the safe: no safe is sure to hold a gun, the first of a floor neither. Which gun it is: <see cref="PickGun"/>. The same on every machine.
        /// </summary>
        public static bool HoldsGun(int runSeed, int floor, int index, int slot = 0)
        {
            if (!SafesHoldGuns(floor) || !WeaponSafeExists(runSeed, floor, index) || HoldsArmour(runSeed, floor, index, slot)) return false;
            return Hash(runSeed, floor, SaltGunChance + index * 149 + slot * 29) % 100U < (uint)GunPercent;
        }

        /// <summary>The kind of the spare clip that may lie in this weapon safe: for the gun the safe holds. The pew's when the safe holds nothing.</summary>
        public static RangedWeapon.Kind ClipKindOf(int runSeed, int floor, int index)
        {
            var items = WeaponContents(runSeed, floor, index);
            return items.Length > 0 && IsGunItem(items[0]) ? GunOfItem(items[0]) : RangedWeapon.Kind.Pew;
        }

        /// <summary>
        /// What safe number <paramref name="index"/> of this floor holds, in words, for the test menu (the owner, 2026-10-22: the list used to say "a pistol" after every safe; this reads the same rules the floor is built from):
        /// cash on floors 1 and 2, else the gun (by name), the piece of armour (by tier) or nothing it holds, then what lies beside it: a bit of cash, a spare clip, a gold watch. Empty when there is no such safe.
        /// </summary>
        public static string ContentsText(int runSeed, int floor, int index)
        {
            if (index < 0 || index >= SafeCount(runSeed, floor)) return "";
            string text;
            if (IsWeaponFloor(floor))
            {
                var items = WeaponContents(runSeed, floor, index);
                if (items.Length == 0) text = "no gun or armour";
                else if (IsArmourItem(items[0])) text = ArmourRules.Name(ArmourOfItem(items[0]));
                else if (IsGunItem(items[0])) { var kind = GunOfItem(items[0]); text = kind == RangedWeapon.Kind.Pew ? "Pew pistol" : RangedWeapon.Specs[(int)kind].name; }
                else text = "item " + items[0];
                if (HasSideCash(runSeed, floor, index)) text += " + cash";
                if (HasSideClip(runSeed, floor, index)) text += " + spare clip";
            }
            else text = index == 0 ? "cash" : "nothing";
            if (HasSideWatch(runSeed, floor, index)) text += " + gold watch";
            return text;
        }

        // ------------------------------------------------------------------ armour instead of a gun (the owner, 2026-10-19)

        const int SaltArmour = 43, SaltArmourTier = 47;
        /// <summary>Armour among what a safe holds: <see cref="ArmourItemBase"/> plus the tier's number (Light Armour 21, Medium 22, Heavy 23).</summary>
        public const int ArmourItemBase = 20;
        public static int ArmourItemNumber(ShieldTier tier) => ArmourItemBase + (int)tier;
        public static bool IsArmourItem(int item) => item > ArmourItemBase && item <= ArmourItemBase + 3;
        public static ShieldTier ArmourOfItem(int item) => (ShieldTier)(item - ArmourItemBase);

        /// <summary>
        /// Does this weapon safe hold a piece of armour instead of a gun (the owner, 2026-10-19: "On the third floor players should have the chance of finding Light Armour instead of a weapon in the safe", the same for the Medium Armour found on
        /// the 7th and the Heavy Armour found on the 10th floor)? Any weapon safe may, the first of a floor too (no safe is sure to hold a gun any more: the owner, 2026-10-19), but never before floor 3; the chance is <see cref="ArmourRules.SafePercent"/>.
        /// The same on every machine.
        /// </summary>
        public static bool HoldsArmour(int runSeed, int floor, int index, int slot = 0)
        {
            if (!WeaponSafeExists(runSeed, floor, index)) return false;
            int percent = ArmourRules.SafePercent(floor);
            return percent > 0 && Hash(runSeed, floor, SaltArmour + index * 137 + slot * 19) % 100U < (uint)percent;
        }

        /// <summary>The tier of the armour such a safe holds, by <see cref="ArmourRules.TierWeights"/> (the tier that is first found on the floor is three times as likely there).</summary>
        public static ShieldTier PickArmour(int runSeed, int floor, int index, int slot)
        {
            var w = ArmourRules.TierWeights(floor);
            long total = 0;
            for (int t = 1; t < w.Length; t++) total += w[t];
            if (total <= 0) return ShieldTier.Light;
            long roll = Hash(runSeed, floor, SaltArmourTier + index * 139 + slot * 23) % (uint)total;
            for (int t = 1; t < w.Length; t++)
            {
                if (roll < w[t]) return (ShieldTier)t;
                roll -= w[t];
            }
            return ShieldTier.Light;
        }

        /// <summary>Does this weapon safe hold a gun too long to lie in it - a shotgun or a sniper rifle (the owner, 2026-10-19: those safes are vertical, the gun standing in them)?</summary>
        public static bool HoldsTallGun(int runSeed, int floor, int index)
        {
            var items = WeaponContents(runSeed, floor, index);
            if (items.Length == 0 || !IsGunItem(items[0])) return false;
            var kind = GunOfItem(items[0]);
            return kind == RangedWeapon.Kind.Shotgun || kind == RangedWeapon.Kind.Sniper;
        }

        // ------------------------------------------------------------------ what else a weapon safe may hold (the owner, 2026-10-18)

        /// <summary>
        /// The chance (percent) that a weapon safe also holds a bit of cash, and the chance that it also holds a spare clip for the pistol (the owner, 2026-10-18: "Safes with a weapon can also have a chance to spawn a bit of cash and a
        /// chance to spawn a back up clip of ammo that all have to be picked up separately"). The two chances are independent of each other; the pistol is always there.
        /// </summary>
        public const int SideCashPercent = 40, SideClipPercent = 50;
        /// <summary>The bit of cash: this share of the floor's quota.</summary>
        public const float SideCashShareOfQuota = 0.03f;

        static bool WeaponSafeExists(int runSeed, int floor, int index) => IsWeaponFloor(floor) && index >= 0 && index < SafeCount(runSeed, floor);

        /// <summary>Does this weapon safe also hold a bit of cash? (Never the one safe of floors 1 and 2, which is full of cash anyway, nor a safe that does not exist.)</summary>
        public static bool HasSideCash(int runSeed, int floor, int index) =>
            WeaponSafeExists(runSeed, floor, index) && Hash(runSeed, floor, SaltSideCash + index * 109) % 100U < (uint)SideCashPercent;

        /// <summary>Does this weapon safe also hold a spare clip for the pistol?</summary>
        public static bool HasSideClip(int runSeed, int floor, int index) =>
            WeaponSafeExists(runSeed, floor, index) && SafesHoldGuns(floor) && !HoldsArmour(runSeed, floor, index) && Hash(runSeed, floor, SaltSideClip + index * 113) % 100U < (uint)SideClipPercent;

        /// <summary>The bit of cash: 3 % of the floor's quota in $25 steps, $50 at least.</summary>
        public static int SideCash(int floor) => Math.Max(50, (int)(Math.Round(TowerRules.QuotaFor(floor) * (double)SideCashShareOfQuota / 25.0) * 25.0));

        // ------------------------------------------------------------------ and sometimes a gold watch (the owner, 2026-10-18)

        /// <summary>
        /// The chance (percent) that a safe also holds a gold watch (the owner, 2026-10-18: "Please also add the chance of a watch being in the safe"): ANY safe - the cash safes of floors 1 and 2 as well as the weapon safes -
        /// a valuable of its own, picked up on its own like the cash and the clip. Decided by the seed alone, independent of the other two chances.
        /// </summary>
        public const int SideWatchPercent = 30;

        /// <summary>Does this safe also hold a gold watch? (Never a safe that does not exist.)</summary>
        public static bool HasSideWatch(int runSeed, int floor, int index) =>
            index >= 0 && index < SafeCount(runSeed, floor) && Hash(runSeed, floor, SaltSideWatch + index * 127) % 100U < (uint)SideWatchPercent;

        /// <summary>What the watch is worth: 1.4 times a typical small valuable of the floor (the same as a gold watch lying in a guard outpost), in $25 steps, $100 at least.</summary>
        public static int SideWatchValue(int floor) => Math.Max(100, (int)(Math.Round(OutpostLoot.ValuableUnit(floor) * 1.4 / 25.0) * 25.0));

        // ------------------------------------------------------------------ where the record is

        /// <summary>
        /// The floor whose guard outpost holds the record of the safes on <paramref name="safeFloor"/> (0 if that floor has no safe): its own. The computer of a floor lists the safes of that floor and no other (the owner,
        /// 2026-10-18: "the Safes tab on the computer should only have the safes for that current floor"); a safe used to be announced a floor or two ahead.
        /// </summary>
        public static int SourceFloor(int runSeed, int safeFloor) => HasSafe(runSeed, safeFloor) ? safeFloor : 0;

        /// <summary>The floor whose safes the guard outpost on <paramref name="outpostFloor"/> has a record of (0: no record there): its own.</summary>
        public static int NoteFor(int runSeed, int outpostFloor) => HasSafe(runSeed, outpostFloor) ? outpostFloor : 0;

        // ------------------------------------------------------------------ what is inside

        /// <summary>The cash in the safe on this floor: half the floor's quota ($2,150 on floor 1).</summary>
        public static int Cash(int floor) => (int)(Math.Round(TowerRules.QuotaFor(floor) * (double)CashShareOfQuota / 50.0) * 50.0);

        /// <summary>How many bundles of cash the safe holds on this floor (a few more on higher floors).</summary>
        public static int Bundles(int floor) => 4 + Math.Min(4, Math.Max(0, floor - 1) / 3);

        /// <summary>The value of each bundle: the cash shared out in $5 steps (the first bundles get the odd dollars), adding up to exactly <see cref="Cash"/>.</summary>
        public static int[] Split(int total, int count)
        {
            if (count < 1) count = 1;
            var parts = new int[count];
            int each = total / count / 5 * 5;
            int left = total;
            for (int i = 0; i < count; i++) { parts[i] = each; left -= each; }
            for (int i = 0; left > 0; i = (i + 1) % count)
            {
                int add = Math.Min(5, left);
                parts[i] += add; left -= add;
            }
            return parts;
        }

        /// <summary>Does the crowbar break after prying a safe open? <paramref name="roll"/> is a random number in [0, 1).</summary>
        public static bool CrowbarBreaks(float roll) => roll < CrowbarBreakChance;

        // ------------------------------------------------------------------ the codes on the keypad

        /// <summary>The code as the four digits it is typed with.</summary>
        public static string CodeText(int code) => code.ToString("D4");

        /// <summary>
        /// Adds a digit to the code being typed (up to 4 digits); returns the new text. A non-digit is ignored.
        /// </summary>
        public static string AddDigit(string typed, char digit)
        {
            typed = typed ?? "";
            if (digit < '0' || digit > '9' || typed.Length >= 4) return typed;
            return typed + digit;
        }

        /// <summary>Removes the last typed digit.</summary>
        public static string Backspace(string typed) => string.IsNullOrEmpty(typed) ? "" : typed.Substring(0, typed.Length - 1);

        /// <summary>Is this the safe's code?</summary>
        public static bool CodeMatches(string typed, int code) => typed != null && typed.Length == 4 && typed == CodeText(code);
    }
}
