using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// What the local player has learned about the floor safes from the notes in the guard outposts: for each safe, which floor it is on, the
    /// number of its room and (if the note gave it) its code. It lives for the run (it is cleared when a run starts, see
    /// <see cref="TowerManager"/>) and is shown in the journal (J). Each player reads the notes himself, so the journal is his own; the safe's
    /// keypad uses it to remind him of a code he has found.
    /// </summary>
    public static class SafeIntel
    {
        public struct Entry
        {
            public int floor;
            /// <summary>The number of the room ("507"), or null when it could not be worked out.</summary>
            public string room;
            /// <summary>The safe's code, or 0 when no note gave it (then the safe has to be pried open).</summary>
            public int code;

            public bool KnowsCode => code > 0;
        }

        static readonly List<Entry> entries = new List<Entry>();
        static readonly HashSet<int> loggedIn = new HashSet<int>();

        /// <summary>Has the player logged in to the computer of the guard outpost on this floor (in this run)?</summary>
        public static bool IsLoggedIn(int outpostFloor) => loggedIn.Contains(outpostFloor);
        public static void LogIn(int outpostFloor) { loggedIn.Add(outpostFloor); }
        public static void LogOut(int outpostFloor) { loggedIn.Remove(outpostFloor); }

        /// <summary>Everything the player knows, lowest floor first.</summary>
        public static IReadOnlyList<Entry> Entries => entries;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { entries.Clear(); loggedIn.Clear(); }

        /// <summary>A new run: forget everything.</summary>
        public static void Reset() { entries.Clear(); loggedIn.Clear(); }

        /// <summary>
        /// A note was read. Returns true when it taught something new: a safe that was not known yet, or the code of one whose code was not.
        /// </summary>
        public static bool Add(int floor, string room, int code)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].floor != floor) continue;
                var e = entries[i];
                bool better = false;
                if (code > 0 && e.code <= 0) { e.code = code; better = true; }
                if (string.IsNullOrEmpty(e.room) && !string.IsNullOrEmpty(room)) { e.room = room; better = true; }
                entries[i] = e;
                return better;
            }
            entries.Add(new Entry { floor = floor, room = room, code = code });
            entries.Sort((a, b) => a.floor.CompareTo(b.floor));
            return true;
        }

        /// <summary>The player's notes about the safe on a floor, if he has any.</summary>
        public static bool TryGet(int floor, out Entry entry)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].floor == floor) { entry = entries[i]; return true; }
            entry = default;
            return false;
        }

        /// <summary>The code the player has found for the safe on a floor (0 if none).</summary>
        public static int CodeFor(int floor) => TryGet(floor, out var e) ? e.code : 0;
    }
}
