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

        /// <summary>A line for the journal about one safe of a floor (the number of safes on the floor is on the line when there are several). It does not say what is inside (the owner, 2026-10-18).</summary>
        public static string JournalLine(int safeFloor, int index, int count, string room, int code)
        {
            string which = count > 1 ? "Safe " + (index + 1) + " of " + count + " on floor " + safeFloor : "Floor " + safeFloor;
            string where = string.IsNullOrEmpty(room) ? which : which + ", room " + room;
            return code > 0 ? where + " - code " + SafeRules.CodeText(code) : where + " - code unknown (needs a crowbar)";
        }

        /// <summary>A short line for the journal.</summary>
        public static string JournalLine(int safeFloor, string room, int code)
        {
            string where = string.IsNullOrEmpty(room) ? "Floor " + safeFloor : "Floor " + safeFloor + ", room " + room;
            return code > 0 ? where + " - code " + SafeRules.CodeText(code) : where + " - code unknown (needs a crowbar)";
        }
    }
}
