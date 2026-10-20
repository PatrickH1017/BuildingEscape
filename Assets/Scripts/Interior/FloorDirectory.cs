using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The rooms of the floor that is on show, by number: the label ("507"), the rectangle, the role (executive suite / server room) and the indices of its doors in the
    /// <see cref="DoorManager"/>. The security computer's Nav map draws it and its Hacked terminal locks doors through it. Built from the generator's layout (the same on every machine) and
    /// matched to the doors by position, and rebuilt when the floor changes.
    /// </summary>
    public sealed class FloorDirectory
    {
        public sealed class Room
        {
            public string label;
            public RoomInfo info;
            public Rc bounds;
            public string role;
            public readonly List<int> doors = new List<int>();
        }

        public readonly List<Room> rooms = new List<Room>();
        /// <summary>Every room of the layout (corridors, the outpost...) for drawing, numbered or not.</summary>
        public readonly List<RoomInfo> all = new List<RoomInfo>();
        public Rc extent;

        static FloorDirectory cached;
        static Layout cachedLayout;
        static DoorManager cachedDoors;
        static int cachedFloor;

        /// <summary>The directory of the floor on show, or null while there is none.</summary>
        public static FloorDirectory Current(int floor)
        {
            var tm = TowerManager.Instance;
            var layout = tm != null && tm.generator != null ? tm.generator.CurrentLayout : null;
            var doors = DoorManager.Active;
            if (layout == null || floor < 1) return null;
            if (cached != null && cachedLayout == layout && cachedDoors == doors && cachedFloor == floor) return cached;
            cached = Build(layout, floor, doors);
            cachedLayout = layout; cachedDoors = doors; cachedFloor = floor;
            return cached;
        }

        public static FloorDirectory Build(Layout layout, int floor, DoorManager doors)
        {
            var d = new FloorDirectory();
            bool any = false;
            foreach (var r in layout.rooms)
            {
                d.all.Add(r);
                if (!any) { d.extent = r.bounds; any = true; }
                else
                {
                    d.extent.x0 = Mathf.Min(d.extent.x0, r.bounds.x0); d.extent.z0 = Mathf.Min(d.extent.z0, r.bounds.z0);
                    d.extent.x1 = Mathf.Max(d.extent.x1, r.bounds.x1); d.extent.z1 = Mathf.Max(d.extent.z1, r.bounds.z1);
                }
                string label = RoomNumbers.LabelOf(layout, r, floor);
                if (label == null) continue;
                var room = new Room { label = label, info = r, bounds = r.bounds, role = r.role };
                if (doors != null)
                    foreach (var dw in r.doors)
                    {
                        var zone = dw.Grow(0.7f);
                        for (int i = 0; i < doors.Count; i++)
                        {
                            var sd = doors.Get(i);
                            if (sd != null && zone.Contains(sd.Centre.x, sd.Centre.z) && !room.doors.Contains(i)) room.doors.Add(i);
                        }
                    }
                d.rooms.Add(room);
            }
            return d;
        }

        /// <summary>What the terminal calls the security office's own door once the line is read (a short word, so that it fits the request to the host).</summary>
        public const string OutpostName = "SECURITY";

        /// <summary>Does this text name the security office (the guard outpost) and not a numbered room? Spaces and capitals do not matter.</summary>
        public static bool IsOutpostName(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = string.Join(" ", text.Trim().ToUpperInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
            return t == "SECURITY" || t == "SECURITY OFFICE" || t == "SEC" || t == "OFFICE" || t == "OUTPOST";
        }

        static bool IsVerbWord(string w) => string.Equals(w, "lock", System.StringComparison.OrdinalIgnoreCase) || string.Equals(w, "unlock", System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// One line typed at the security terminal when it asks for a room: the room (a number, or one of the names of the security office) and the verb LOCK / UNLOCK, either of which may be missing
        /// ("507", "507 lock", "lock 507", "security office unlock"). Two words without a verb are read as "room, then whatever" ("507 foo" says it does not know FOO), except the security office's own names.
        /// </summary>
        public static void ParseTerminal(string typed, out string room, out string verb)
        {
            room = null; verb = null;
            var parts = (typed ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return;
            if (parts.Length >= 2)
            {
                bool first = IsVerbWord(parts[0]), last = IsVerbWord(parts[parts.Length - 1]);
                if (first != last)
                {
                    verb = first ? parts[0] : parts[parts.Length - 1];
                    room = string.Join(" ", parts, first ? 1 : 0, parts.Length - 1);
                    return;
                }
                if (!IsOutpostName(typed)) { room = parts[0]; verb = parts[1]; return; }
            }
            room = string.Join(" ", parts);
        }

        /// <summary>The room with this number: spaces and capitals do not matter ("507", " 507a ").</summary>
        public Room Find(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string t = text.Trim();
            foreach (var r in rooms) if (string.Equals(r.label, t, System.StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        /// <summary>Is this door the door of an executive suite or the server room (the lock is loud)?</summary>
        public bool IsHeavy(int doorIndex)
        {
            foreach (var r in rooms) if (r.role != null && r.doors.Contains(doorIndex)) return true;
            return false;
        }

        /// <summary>The door indices of a room number on the floor on show (for the host's check), or null.</summary>
        public static ushort[] DoorsOf(int floor, string text)
        {
            var dir = Current(floor);
            var room = dir != null ? dir.Find(text) : null;
            if (room == null || room.doors.Count == 0) return null;
            var list = new ushort[room.doors.Count];
            for (int i = 0; i < list.Length; i++) list[i] = (ushort)room.doors[i];
            return list;
        }
    }
}