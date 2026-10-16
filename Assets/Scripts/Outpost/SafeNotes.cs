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

        /// <summary>The heading of the note.</summary>
        public static string Title(int code) => code > 0 ? "SAFE COMBINATION" : "SAFE - NO COMBINATION";

        /// <summary>The text of the note. <paramref name="code"/> is the safe's code, or 0 when the note does not give it.</summary>
        public static string Body(int runSeed, int outpostFloor, int safeFloor, string room, int code)
        {
            string where = Where(outpostFloor, safeFloor, room);
            uint pick = SafeRules.Hash(runSeed, outpostFloor * 100 + safeFloor, 77) % 3U;
            if (code > 0)
            {
                string digits = SafeRules.CodeText(code);
                switch (pick)
                {
                    case 0: return "Reminder for the night shift.\n\nThe safe is in " + where + ".\nThe combination is " + digits + ".\n\nDo not leave this lying around.";
                    case 1: return "Facilities changed the safe code again.\n\n" + where + "\nNew code: " + digits + "\n\nTell nobody. Burn this note.";
                    default: return "For whoever takes the next shift:\n\nthe cash safe is in " + where + ".\nIt is set to " + digits + ".\n\nLock up when you are done.";
                }
            }
            switch (pick)
            {
                case 0: return "There is a safe in " + where + ".\n\nNobody knows the combination any more. Facilities say a crowbar will open it - it may not survive the job.";
                case 1: return "Memo to security:\n\nthe safe in " + where + " is stuck on a lost code. Do not call a locksmith. Somebody just needs a crowbar and a quiet minute.";
                default: return "Does anybody remember the code for the safe in " + where + "?\n\nNo? Me neither. Pry it open with a crowbar, but be quiet about it.";
            }
        }

        /// <summary>A short line for the journal.</summary>
        public static string JournalLine(int safeFloor, string room, int code)
        {
            string where = string.IsNullOrEmpty(room) ? "Floor " + safeFloor : "Floor " + safeFloor + ", room " + room;
            return code > 0 ? where + " - code " + SafeRules.CodeText(code) : where + " - code unknown (needs a crowbar)";
        }
    }
}
