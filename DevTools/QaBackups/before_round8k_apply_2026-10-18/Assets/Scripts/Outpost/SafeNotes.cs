namespace BuildingEscape
{
    /// <summary>
    /// The words on the notes that tell the team about a floor safe, free of Unity types (tested outside Unity). A note says where the
    /// safe is (the room, and the floor if it is not this one) and either gives its code or says that nobody knows it, which means it
    /// takes a crowbar. The wording is picked from a few, by the run seed, so two notes in a run rarely read alike.
    /// </summary>
    public static class SafeNotes
    {
        /// <summary>Where the safe is, as the note puts it: "Room 507 on floor 5", "Room 307 on this floor", or just the floor when the room is not known.</summary>
        public static string Where(int outpostFloor, int safeFloor, string room)
        {
            bool here = outpostFloor == safeFloor;
            string floorText = here ? "this floor" : "floor " + safeFloor;
            return string.IsNullOrEmpty(room) ? (here ? "somewhere on this floor" : "somewhere on floor " + safeFloor) : "Room " + room + " on " + floorText;
        }

        /// <summary>What a safe holds, as the computer says it: "Cash $2,150", "Pew pistol", "2 Pew pistols", or "Empty".</summary>
        public static string ContentsText(int[] items, int cash)
        {
            if (items == null || items.Length == 0) return cash > 0 ? "Cash $" + cash.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) : "Empty";
            var names = new System.Collections.Generic.List<string>();
            var counts = new System.Collections.Generic.List<int>();
            foreach (int k in items)
            {
                string n = k == SafeRules.PewItem ? "Pew pistol" : OutpostLoot.WeaponName(k);
                int at = names.IndexOf(n);
                if (at < 0) { names.Add(n); counts.Add(1); } else counts[at]++;
            }
            var parts = new string[names.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = counts[i] > 1 ? counts[i] + " " + names[i] + "s" : names[i];
            return string.Join(", ", parts);
        }

        /// <summary>What the computer's record says safe number <paramref name="index"/> of a floor holds, as text.</summary>
        public static string ContentsOf(int runSeed, int floor, int index) =>
            SafeRules.IsWeaponFloor(floor) ? ContentsText(SafeRules.WeaponContents(runSeed, floor, index), 0) : ContentsText(null, index == 0 ? SafeRules.Cash(floor) : 0);

        /// <summary>A line for the journal about one safe of a floor (the number of safes on the floor is on the line when there are several).</summary>
        public static string JournalLine(int safeFloor, int index, int count, string room, int code, string contents)
        {
            string which = count > 1 ? "Safe " + (index + 1) + " of " + count + " on floor " + safeFloor : "Floor " + safeFloor;
            string where = string.IsNullOrEmpty(room) ? which : which + ", room " + room;
            string codeText = code > 0 ? "code " + SafeRules.CodeText(code) : "code unknown (needs a crowbar)";
            return string.IsNullOrEmpty(contents) ? where + " - " + codeText : where + " - " + codeText + " - " + contents;
        }

        /// <summary>A short line for the journal.</summary>
        public static string JournalLine(int safeFloor, string room, int code)
        {
            string where = string.IsNullOrEmpty(room) ? "Floor " + safeFloor : "Floor " + safeFloor + ", room " + room;
            return code > 0 ? where + " - code " + SafeRules.CodeText(code) : where + " - code unknown (needs a crowbar)";
        }
    }
}
