using System;
using System.Collections.Generic;
using System.Linq;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), one section per finished task, in the order they were built. Batch A1: the elevator panel's buttons (T19), the weights of the weapons (T20), the scan's 3 seconds (T21),
    /// the Vendor cards' two lines (T10), the computer's tabs (T15), the codes of every safe (T18) and the number of guards by floor (T14). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is <c>NetDevAsks1023.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023Test()
        {
            // ---- T19: the elevator panel's buttons rise from left to right and from the bottom row up (the owner's photograph)
            {
                int n = ElevatorPanelRules.Buttons;
                var cells = new List<(int col, int row, float x, float y)>();
                for (int k = 0; k < n; k++)
                {
                    ElevatorPanelRules.Cell(k, out int c, out int r);
                    ElevatorPanelRules.Position(k, 0.5f, out float x, out float y);
                    cells.Add((c, r, x, y));
                }
                bool floorsInOrder = Enumerable.Range(0, n).All(k => ElevatorPanelRules.FloorOf(k) == k);
                bool rightThenUp = true;
                for (int k = 1; k < n; k++)
                {
                    bool sameRow = cells[k].row == cells[k - 1].row;
                    rightThenUp &= sameRow ? cells[k].x > cells[k - 1].x && Math.Abs(cells[k].y - cells[k - 1].y) < 1e-4f : cells[k].y > cells[k - 1].y && cells[k].col == 0;
                }
                bool distinct = cells.Select(c => (Math.Round(c.x, 3), Math.Round(c.y, 3))).Distinct().Count() == n;
                float top = cells.Max(c => c.y), bottom = cells.Min(c => c.y);
                bool onPlate = cells.All(c => Math.Abs(c.x) <= 0.6f) && Math.Abs(top - 0.5f) < 1e-4f && bottom >= -1.2f;
                var bottomRow = Enumerable.Range(0, n).Where(k => Math.Abs(cells[k].y - bottom) < 1e-4f).Select(ElevatorPanelRules.FloorOf).ToList();
                var topRow = Enumerable.Range(0, n).Where(k => Math.Abs(cells[k].y - top) < 1e-4f).Select(ElevatorPanelRules.FloorOf).ToList();
                Check("elevator panel (T19): 14 buttons, the docks (0) first and then floors 1 to 13 in order", n == 14 && floorsInOrder);
                Check("elevator panel (T19): along a row they rise from left to right, and the next row starts at the left, higher up", rightThenUp);
                Check("elevator panel (T19): the bottom row is the docks, 1 and 2, the top row 12 and 13 at the left, as on the owner's photograph (G L 2 at the bottom, 12 14 at the top)", bottomRow.SequenceEqual(new[] { 0, 1, 2 }) && topRow.SequenceEqual(new[] { 12, 13 }) && cells[12].col == 0 && cells[13].col == 1);
                Check("elevator panel (T19): no two buttons share a place and all of them stand on the plate (1.4 m wide, 2.6 m high)", distinct && onPlate);
            }

            // ---- T20: weapons weigh about half what they did
            {
                float[] melee = { 1.5f, 1f, 1.5f };                                    // crowbar, bat, hatchet (they were 3, 2, 3 kg)
                float[] gun = { 0.6f, 0.95f, 1.7f, 1.8f, 2.4f };                       // pew, SMG, rifle, shotgun, sniper (1.2, 1.9, 3.4, 3.6, 4.8 kg)
                bool m = MeleeWeapon.Specs.Length == melee.Length, g = RangedWeapon.Specs.Length == gun.Length;
                for (int i = 0; m && i < melee.Length; i++) m &= Math.Abs(MeleeWeapon.Specs[i].weight - melee[i]) < 1e-4f;
                for (int i = 0; g && i < gun.Length; i++) g &= Math.Abs(RangedWeapon.Specs[i].weight - gun[i]) < 1e-4f;
                Check("weapons (T20): the crowbar, the bat and the hatchet weigh 1.5, 1 and 1.5 kg (half of 3, 2 and 3)", m);
                Check("weapons (T20): the pew, the SMG, the rifle, the shotgun and the sniper rifle weigh 0.6, 0.95, 1.7, 1.8 and 2.4 kg (half)", g);
            }

            // ---- T21 (changed 2026-10-23, after the playtest: "a 2 second cooldown between each use not a charge up"): the scan goes off at once, then waits its cooldown
            Check("scan (T21): the cooldown between two scans is 2 seconds, and there is no charge-up (the first scan leaves at once)", LootScanRules.PulseGap == 2f && LootScanRules.NextPulse(true, -1f) && !LootScanRules.NextPulse(false, -1f));
            Check("scan (T21): 1.99 s after the last scan the next one cannot leave, 2 s after it can, and a key not held scans never", !LootScanRules.NextPulse(true, 1.99f) && LootScanRules.NextPulse(true, 2f) && LootScanRules.NextPulse(true, 7f) && !LootScanRules.NextPulse(false, 7f));
            Check("scan (T21): the screen asks to wait 2 seconds right after a scan, 1 second in the second half, nothing when a scan can leave (the owner, 2026-10-24: rounded to the nearest whole second, 1 or 2)",
                LootScanRules.WaitSeconds(0f) == 2 && LootScanRules.WaitSeconds(0.4f) == 2 && LootScanRules.WaitSeconds(0.5f) == 2 && LootScanRules.WaitSeconds(0.6f) == 1 && LootScanRules.WaitSeconds(1.4f) == 1 && LootScanRules.WaitSeconds(1.99f) == 1 && LootScanRules.WaitSeconds(2f) == 0 && LootScanRules.WaitSeconds(9f) == 0 && LootScanRules.WaitSeconds(-1f) == 0);
            Check("scan (T21): the words are 'Wait 2 seconds' and 'Wait 1 second' (nothing to wait: no words)", LootScanRules.WaitText(2) == "Wait 2 seconds" && LootScanRules.WaitText(1) == "Wait 1 second" && LootScanRules.WaitText(0) == "");
            Check("scan (T21): the bar of the cooldown fills evenly, never below 0 or above 1, and is full when there has been no scan", Math.Abs(LootScanRules.ReadyFraction(1f) - 0.5f) < 1e-5f && LootScanRules.ReadyFraction(9f) == 1f && LootScanRules.ReadyFraction(-1f) == 1f && LootScanRules.ReadyFraction(0f) == 0f);

            // ---- T10: the Vendor cards for weapons say "Damage: x" and "Stun Time: x"
            {
                var ms = MeleeWeapon.Specs;
                int cr = (int)MeleeWeapon.Kind.Crowbar, bt = (int)MeleeWeapon.Kind.Bat, ht = (int)MeleeWeapon.Kind.Hatchet;
                Check("vendor cards (T10): the crowbar says Damage: 15 and Stun Time: 4s, the bat 25 and 3s, the hatchet 35 and 6s",
                      WeaponCardRules.DamageLine(ms[cr].guardDamage) == "Damage: 15" && WeaponCardRules.StunLine(ms[cr].stun) == "Stun Time: 4s" &&
                      WeaponCardRules.DamageLine(ms[bt].guardDamage) == "Damage: 25" && WeaponCardRules.StunLine(ms[bt].stun) == "Stun Time: 3s" &&
                      WeaponCardRules.DamageLine(ms[ht].guardDamage) == "Damage: 35" && WeaponCardRules.StunLine(ms[ht].stun) == "Stun Time: 6s");
                var pew = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew];
                Check("vendor cards (T10): the pistol says Damage: 40 (a bullet is four hits of ten HP) and Stun Time: 0s (a bullet does not stun)", WeaponCardRules.DamageLine(WeaponCardRules.GunDamage(pew.guardHits)) == "Damage: 40" && WeaponCardRules.StunLine(pew.stun) == "Stun Time: 0s");
                Check("vendor cards (T10): a number is written whole when it is whole and with one decimal when it is not; a stun is never negative", WeaponCardRules.Number(15f) == "15" && WeaponCardRules.Number(2.5f) == "2.5" && WeaponCardRules.Number(0.0f) == "0" && WeaponCardRules.StunLine(-3f) == "Stun Time: 0s");
            }

            // ---- T15: the computer's tabs stand Nav, Safes, Commands
            Check("computer (T15): the tabs, left to right, are Nav, Safes and Commands (the door terminal's old name was Hacked), each keeping its id",
                  ComputerScreen.TabOrder.SequenceEqual(new[] { ComputerScreen.TabNav, ComputerScreen.TabSafes, ComputerScreen.TabHacked }) && ComputerScreen.TabNames.SequenceEqual(new[] { "Nav", "Safes", "Commands" }) && ComputerScreen.TabOrder.Distinct().Count() == 3);

            // ---- T18: every safe has its code on record
            {
                bool every = SafeRules.EveryRecordGivesCode, invalidOk = true, noCrowbar = true; int seen = 0;
                for (int seed = 1; seed <= 60; seed++)
                    for (int f = 1; f <= 12; f++)
                    {
                        int n = SafeRules.SafeCount(seed * 37 + 11, f);
                        for (int i = 0; i < n; i++) { seen++; every &= SafeRules.RecordGivesCode(seed * 37 + 11, f, i); }
                        invalidOk &= !SafeRules.RecordGivesCode(seed * 37 + 11, f, n) && !SafeRules.RecordGivesCode(seed * 37 + 11, f, -1);
                        noCrowbar &= !SafeRules.AnySafeNeedsCrowbar(seed * 37 + 11, f);
                    }
                Check("safes (T18): the computer's record gives the code of every safe of every floor, and no safe is left that needs a crowbar for want of a code", every && noCrowbar && seen > 500, $"({seen} safes)");
                Check("safes (T18): a safe that is not there still has no record", invalidOk);
            }

            // ---- T14: the guards keep rising in number with the floor; the alarm cap stays 24
            {
                bool rising = true, rule = true;
                for (int f = 1; f <= 12; f++)
                {
                    rising &= GuardRules.PatrolCount(f + 1, 3) >= GuardRules.PatrolCount(f, 3);
                    rule &= GuardRules.PatrolCount(f, 3) == Math.Min(GuardRules.PatrolCap, GuardRules.PatrolGuards(f, 3));
                }
                Check("guards (T14): the patrolling guards rise with the floor (4, 5, 8, 9 ... 12 on floor 7 as before, 13 on floor 8 ... 18 on floor 13) and are only cut at the cap", rising && rule && GuardRules.PatrolCount(1, 3) == 4 && GuardRules.PatrolCount(7, 3) == 12 && GuardRules.PatrolCount(8, 3) == 13 && GuardRules.PatrolCount(13, 3) == 18);
                Check("guards (T14): the most guards alive at once with the alarm on is 24 and there is room for at least 6 officers on top of the patrols on the top floor", GuardRules.MaxGuardsAlive == 24 && GuardRules.PatrolCap + 6 <= GuardRules.MaxGuardsAlive);
            }

            // ---- T26: Alt (or V) unlocks the camera only while it is held
            {
                // a tap: the key is down for 0.1 s (six frames of a sixtieth), then up
                float held = 0f; bool unlockedInTap = false;
                for (int i = 0; i < 6; i++) { held = FreeLookRules.Held(held, true, 1f / 60f); unlockedInTap |= FreeLookRules.Unlocked(held); }
                held = FreeLookRules.Held(held, false, 1f / 60f);
                Check("free look (T26): a tap on Alt (0.1 s) never unlocks the view, and letting go starts the count over", !unlockedInTap && held == 0f && !FreeLookRules.Unlocked(held));
                // a hold: unlocked only once 0.25 s have passed, and locked again the frame the key comes up
                held = 0f; float unlockedAt = -1f;
                for (int i = 1; i <= 60; i++) { held = FreeLookRules.Held(held, true, 1f / 60f); if (unlockedAt < 0f && FreeLookRules.Unlocked(held)) unlockedAt = i / 60f; }
                bool relocked = !FreeLookRules.Unlocked(FreeLookRules.Held(held, false, 1f / 60f));
                Check("free look (T26): held down it unlocks after a quarter of a second, not before, and stays unlocked while held; it locks again at once when let go", unlockedAt >= FreeLookRules.HoldSeconds - 1e-4f && unlockedAt <= FreeLookRules.HoldSeconds + 1f / 60f + 1e-4f && FreeLookRules.Unlocked(held) && relocked, $"(unlocked at {unlockedAt:0.000} s)");
                Check("free look (T26): the hold time is a quarter of a second and a negative frame time counts nothing", FreeLookRules.HoldSeconds == 0.25f && FreeLookRules.Held(0.1f, true, -1f) == 0.1f);
            }
        }
    }
}
