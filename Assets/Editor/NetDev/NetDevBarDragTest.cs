using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner, 2026-10-22: "give players the ability to drag equipment such as weapons into different shortcut menus from their backpack or from a different shortcut ... if they drag to another item it should swap spots with the item".
        /// The pure rules of the weapon bar's layout (WeaponBarRules.Reconcile / MoveToSlot / SwapWith), on their own: how it fills, that what the player arranged is never reshuffled, what a drop on a slot and a drop on another weapon do, and (over
        /// thousands of random moves) that the layout never holds a weapon twice or one that left the pack. The window itself (Backpack's drag and drop) is the Play-mode scenario <c>BarDrag</c> (NetDevBarDrag.cs).
        /// </summary>
        static void BarDragTest()
        {
            const int E = WeaponBarRules.Empty;
            string Show(int[] s) => "[" + string.Join(",", s.Select(k => k == E ? "-" : k.ToString())) + "]";
            bool Is(int[] s, params int[] want) => s.Length == want.Length && s.SequenceEqual(want);

            Check($"weapon bar layout: {WeaponBarRules.Slots} slots, a new layout is empty, and Empty is no key a weapon could have (the first empty slot is what SlotOf(Empty) finds)",
                WeaponBarRules.Slots == 6 && WeaponBarRules.NewLayout().Length == 6 && WeaponBarRules.NewLayout().All(k => k == E) && WeaponBarRules.SlotOf(WeaponBarRules.NewLayout(), E) == 0 && WeaponBarRules.SlotOf(WeaponBarRules.NewLayout(), 5) == -1);

            // ---- filling: the weapons of the pack take the first empty slots in the order they came into it, and the bar stops when it is full
            var bar = WeaponBarRules.NewLayout();
            bool first = WeaponBarRules.Reconcile(bar, new[] { 10, 20, 30 });
            bool again = WeaponBarRules.Reconcile(bar, new[] { 10, 20, 30 });
            Check($"weapon bar layout: three weapons fill slots 1 to 3 in the order they came, and filling again changes nothing {Show(bar)}", first && !again && Is(bar, 10, 20, 30, E, E, E));
            var nine = WeaponBarRules.NewLayout();
            WeaponBarRules.Reconcile(nine, new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            Check($"weapon bar layout: nine weapons: the first six are on the bar, the other three are not {Show(nine)}", Is(nine, 1, 2, 3, 4, 5, 6));
            var negative = WeaponBarRules.NewLayout();
            WeaponBarRules.Reconcile(negative, new[] { -5, -9, 4 });
            Check($"weapon bar layout: keys can be negative (an instance id of an object made while the game runs is) {Show(negative)}", Is(negative, -5, -9, 4, E, E, E) && WeaponBarRules.SlotOf(negative, -9) == 1);

            // ---- what the player arranged is not reshuffled; a gap stays a gap until something new comes
            var gap = WeaponBarRules.NewLayout();
            WeaponBarRules.Reconcile(gap, new[] { 1, 2, 3 });
            bool changed = WeaponBarRules.Reconcile(gap, new[] { 2, 3 });
            Check($"weapon bar layout: the weapon of slot 1 is dropped: its slot is empty and the others stay where they are (no shuffling up) {Show(gap)}", changed && Is(gap, E, 2, 3, E, E, E));
            WeaponBarRules.Reconcile(gap, new[] { 2, 3, 4 });
            Check($"weapon bar layout: a weapon picked up after that takes the FIRST empty slot, the gap {Show(gap)}", Is(gap, 4, 2, 3, E, E, E));
            var seven = WeaponBarRules.NewLayout();
            WeaponBarRules.Reconcile(seven, new[] { 1, 2, 3, 4, 5, 6, 7 });
            WeaponBarRules.Reconcile(seven, new[] { 1, 2, 4, 5, 6, 7 });
            Check($"weapon bar layout: with the bar full and a seventh weapon in the pack, a dropped weapon's slot is taken by the one that was off the bar {Show(seven)}", Is(seven, 1, 2, 7, 4, 5, 6));

            var twice = new[] { 5, 5, E, 7, E, E };
            bool twiceChanged = WeaponBarRules.Reconcile(twice, new[] { 5, 7 });
            Check($"weapon bar layout: a weapon found on two slots (it cannot happen through the moves) stays on the first {Show(twice)}", twiceChanged && Is(twice, 5, E, E, 7, E, E));

            // ---- a drop on a slot
            var m = WeaponBarRules.NewLayout();
            WeaponBarRules.Reconcile(m, new[] { 1, 2, 3 });
            bool r1 = WeaponBarRules.MoveToSlot(m, 1, 4);
            Check($"weapon bar layout: a weapon dropped on an empty slot moves there and leaves its own slot empty {Show(m)}", r1 && Is(m, E, 2, 3, E, 1, E));
            bool r2 = WeaponBarRules.MoveToSlot(m, 1, 2);
            Check($"weapon bar layout: dropped on a slot that holds another weapon, the two swap slots {Show(m)}", r2 && Is(m, E, 2, 1, E, 3, E));
            var snapshot = (int[])m.Clone();
            bool r3 = WeaponBarRules.MoveToSlot(m, 1, 2), r4 = WeaponBarRules.MoveToSlot(m, 1, -1), r5 = WeaponBarRules.MoveToSlot(m, 1, 6), r6 = WeaponBarRules.MoveToSlot(m, E, 3);
            Check("weapon bar layout: dropped on its own slot, on a slot that does not exist, or a nothing dropped: nothing changes and the answer is no", !r3 && !r4 && !r5 && !r6 && m.SequenceEqual(snapshot));
            var full = WeaponBarRules.NewLayout();
            var pack7 = new[] { 1, 2, 3, 4, 5, 6, 7 };
            WeaponBarRules.Reconcile(full, pack7);
            bool r7 = WeaponBarRules.MoveToSlot(full, 7, 2);
            bool back = WeaponBarRules.Reconcile(full, pack7);
            Check($"weapon bar layout: a weapon taken from the pack's list (not on the bar) and dropped on a full bar's slot takes it, the one that was there leaves the bar and is NOT put back by the next fill {Show(full)}",
                r7 && !back && Is(full, 1, 2, 7, 4, 5, 6) && WeaponBarRules.SlotOf(full, 3) == -1);

            // ---- a drop on another weapon: they swap spots
            var s1 = WeaponBarRules.NewLayout();
            WeaponBarRules.Reconcile(s1, pack7);
            bool w1 = WeaponBarRules.SwapWith(s1, 2, 5);
            Check($"weapon bar layout: two weapons of the bar dropped on each other swap slots {Show(s1)}", w1 && Is(s1, 1, 5, 3, 4, 2, 6));
            bool w2 = WeaponBarRules.SwapWith(s1, 7, 3);                                    // (7 is off the bar, 3 is on slot 3)
            Check($"weapon bar layout: a weapon of the pack's list dropped on one of the bar takes its slot, and that one leaves the bar {Show(s1)}", w2 && Is(s1, 1, 5, 7, 4, 2, 6));
            bool w3 = WeaponBarRules.SwapWith(s1, 4, 3);                                    // (4 is on slot 4, 3 is off the bar now)
            Check($"weapon bar layout: a weapon of the bar dropped on one that is not on it gives it its slot and leaves the bar {Show(s1)}", w3 && Is(s1, 1, 5, 7, 3, 2, 6));
            var s2 = (int[])s1.Clone();
            bool w4 = WeaponBarRules.SwapWith(s1, 4, 8), w5 = WeaponBarRules.SwapWith(s1, 1, 1), w6 = WeaponBarRules.SwapWith(s1, E, 1), w7 = WeaponBarRules.SwapWith(s1, 1, E);
            Check("weapon bar layout: two weapons that are both off the bar have no spots to swap, a weapon dropped on itself and a nothing change nothing", !w4 && !w5 && !w6 && !w7 && s1.SequenceEqual(s2));

            // ---- over thousands of random moves the layout stays sound
            var rng = new Random(2026);
            var pack = new List<int>();
            int nextKey = -50;
            var lay = WeaponBarRules.NewLayout();
            string firstBad = null;
            int moves = 0;
            for (int step = 0; step < 6000 && firstBad == null; step++)
            {
                int op = rng.Next(4);
                if (op == 0 && pack.Count < 12) pack.Add(nextKey += rng.Next(1, 4));
                else if (op == 1 && pack.Count > 0) pack.RemoveAt(rng.Next(pack.Count));
                else if (op == 2 && pack.Count > 0) { WeaponBarRules.MoveToSlot(lay, pack[rng.Next(pack.Count)], rng.Next(WeaponBarRules.Slots)); moves++; }
                else if (op == 3 && pack.Count > 1) { WeaponBarRules.SwapWith(lay, pack[rng.Next(pack.Count)], pack[rng.Next(pack.Count)]); moves++; }
                var before = (int[])lay.Clone();
                WeaponBarRules.Reconcile(lay, pack);
                bool dup = lay.Where(k => k != E).GroupBy(k => k).Any(g => g.Count() > 1);
                bool gone = lay.Any(k => k != E && !pack.Contains(k));
                int onBar = lay.Count(k => k != E);
                bool fullOrAll = !lay.Contains(E) || pack.All(k => lay.Contains(k));
                bool right = onBar == Math.Min(pack.Count, WeaponBarRules.Slots);
                bool kept = true;
                for (int s = 0; s < lay.Length; s++) if (before[s] != E && pack.Contains(before[s]) && lay[s] != before[s]) kept = false;
                bool idem = !WeaponBarRules.Reconcile((int[])lay.Clone(), pack);
                if (dup || gone || !fullOrAll || !right || !kept || !idem) firstBad = $"step {step}: pack [{string.Join(",", pack)}] bar {Show(lay)} (dup {dup}, a weapon that left {gone}, not full and not all {!fullOrAll}, wrong count {!right}, a weapon moved by the fill {!kept}, fill again changes it {!idem})";
            }
            Check("weapon bar layout: over 6000 random pick-ups, drops, moves and swaps (" + moves + " of them moves) no weapon is on the bar twice or after it left the pack, the bar is full or holds every weapon, the fill never moves one, and filling twice is the same as once" + (firstBad != null ? " - FIRST BAD " + firstBad : ""), firstBad == null);
        }
    }
}
