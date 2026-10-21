using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The weapon bar (the owner, 2026-10-19 night: "There should be a tool bar (hot bar) of 3 items so players can click 1, 2, 3 to switch between weapons"): three slots at the bottom of the screen, filled with the first
    /// three weapons of the pack in the order they came into it; the keys 1, 2 and 3 take the weapon of that slot in hand. Pure rules, no Unity types.
    /// </summary>
    public static class WeaponBarRules
    {
        public const int Slots = 3;

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

        /// <summary>The slot (0 to 2) a number key picks (1 to 3), or -1.</summary>
        public static int SlotOfKey(int digit) => digit >= 1 && digit <= Slots ? digit - 1 : -1;
    }
}
