using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The weapon bar (the owner, 2026-10-19 night: "There should be a tool bar (hot bar) of 3 items so players can click 1, 2, 3 to switch between weapons"; 2026-10-20 night: "I want there to be a total of 6 different short cuts 1-6"):
    /// six slots at the bottom of the screen, filled with the first six weapons and tools of the pack in the order they came into it; the keys 1 to 6 take the item of that slot in hand. Pure rules, no Unity types.
    /// The owner, 2026-10-22: "give players the ability to drag equipment such as weapons into different shortcut menus from their backpack or from a different shortcut ... if they drag to another item it should swap spots with the item":
    /// the bar is also a LAYOUT the player arranges (an array with one key per slot, <see cref="Empty"/> for none: <see cref="Reconcile"/> keeps it in step with the pack, <see cref="MoveToSlot"/> and <see cref="SwapWith"/> are what a drop does).
    /// </summary>
    public static class WeaponBarRules
    {
        public const int Slots = 6;

        /// <summary>The index (in the pack's list) of the weapon in each slot, -1 for an empty slot; <paramref name="isWeapon"/> says for each item of the pack whether it is a weapon.</summary>
        public static int[] Fill(IReadOnlyList<bool> isWeapon)
        {
            var slots = new int[Slots];
            Fill(isWeapon, slots);
            return slots;
        }

        /// <summary>The same into an array that is kept (nothing is made).</summary>
        public static void Fill(IReadOnlyList<bool> isWeapon, int[] slots)
        {
            for (int s = 0; s < slots.Length; s++) slots[s] = -1;
            int n = 0;
            for (int i = 0; i < isWeapon.Count && n < slots.Length && n < Slots; i++) if (isWeapon[i]) slots[n++] = i;
        }

        /// <summary>The slot (0 to 5) a number key picks (1 to 6), or -1.</summary>
        public static int SlotOfKey(int digit) => digit >= 1 && digit <= Slots ? digit - 1 : -1;

        // ------------------------------------------------------------------ the layout the player arranges

        /// <summary>The key of a slot with nothing in it (a weapon's key is any other number: its instance id, say).</summary>
        public const int Empty = int.MinValue;

        /// <summary>A layout with every slot empty.</summary>
        public static int[] NewLayout()
        {
            var slots = new int[Slots];
            Clear(slots);
            return slots;
        }

        public static void Clear(int[] slots) { for (int s = 0; s < slots.Length; s++) slots[s] = Empty; }

        /// <summary>The slot that holds <paramref name="key"/>, or -1 (for <see cref="Empty"/>: the first empty slot).</summary>
        public static int SlotOf(int[] slots, int key)
        {
            for (int s = 0; s < slots.Length; s++) if (slots[s] == key) return s;
            return -1;
        }

        /// <summary>
        /// Brings a layout in step with the weapons of the pack (<paramref name="weapons"/>: their keys, in the order they came into it): a slot whose weapon has left the pack is emptied (and a weapon on two slots stays on the first),
        /// then each weapon of the pack that is not on the bar takes the first empty slot, in the pack's order, until the bar is full. What the player arranged is never reshuffled. Returns true when something changed.
        /// After it the bar either has no empty slot or has every weapon of the pack on it.
        /// </summary>
        public static bool Reconcile(int[] slots, IReadOnlyList<int> weapons)
        {
            bool changed = false;
            for (int s = 0; s < slots.Length; s++)
            {
                if (slots[s] == Empty) continue;
                bool inPack = false;
                for (int i = 0; i < weapons.Count; i++) if (weapons[i] == slots[s]) { inPack = true; break; }
                bool again = false;
                for (int t = 0; t < s; t++) if (slots[t] == slots[s]) { again = true; break; }
                if (!inPack || again) { slots[s] = Empty; changed = true; }
            }
            for (int i = 0; i < weapons.Count; i++)
            {
                if (SlotOf(slots, weapons[i]) >= 0) continue;
                int free = SlotOf(slots, Empty);
                if (free < 0) break;
                slots[free] = weapons[i];
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// A weapon is dropped on slot <paramref name="to"/>: it takes the slot, and whatever stood there takes the place the weapon came from (its old slot, or off the bar when the weapon came from the pack's list: it stays in the pack).
        /// Dropped on its own slot, or on a slot that does not exist, nothing changes. Returns true when the layout changed.
        /// </summary>
        public static bool MoveToSlot(int[] slots, int key, int to)
        {
            if (key == Empty || to < 0 || to >= slots.Length) return false;
            int from = SlotOf(slots, key);
            if (from == to) return false;
            int there = slots[to];
            slots[to] = key;
            if (from >= 0) slots[from] = there;
            return true;
        }

        /// <summary>
        /// A weapon is dropped on another weapon: they swap places. Both on the bar: their slots; one on the bar: the other takes its slot and it leaves the bar (it stays in the pack); neither on the bar: nothing to swap.
        /// Returns true when the layout changed.
        /// </summary>
        public static bool SwapWith(int[] slots, int key, int other)
        {
            if (key == Empty || other == Empty || key == other) return false;
            int a = SlotOf(slots, key), b = SlotOf(slots, other);
            if (a < 0 && b < 0) return false;
            if (a >= 0) slots[a] = other;
            if (b >= 0) slots[b] = key;
            return true;
        }
    }
}
