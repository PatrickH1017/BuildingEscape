# Round 4, batch 9 (A9 + A10): the planner plans every cell first, then guarantees a break room and the two restrooms, then furnishes; the restroom doors are permanently locked doors. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep '        public readonly List<Rc> clear = new List<Rc>();' '        public readonly List<Rc> clear = new List<Rc>();
        /// <summary>World rectangles of doorways whose door NEVER opens (the restrooms: the owner, 2026-10-17 night, "Players should not be allowed to enter the restroom"): FloorGenerator builds them as locked doors.</summary>
        public readonly List<Rc> deadDoors = new List<Rc>();'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficePlanner.cs')
Rep '        public string plan;
        public readonly List<RoomCtx> rooms = new List<RoomCtx>();' '        public string plan;
        /// <summary>The random numbers of this cell: its planning and its furnishing share one sequence, the furnishing runs after every cell is planned (OfficePlanner.Core.cs).</summary>
        public System.Random rng;
        public readonly List<RoomCtx> rooms = new List<RoomCtx>();
        /// <summary>Two stacked rooms of one quadrant (6.4 x 3.1 m each), recorded while planning: where the restrooms can go.</summary>
        public readonly List<RoomCtx[]> pairs = new List<RoomCtx[]>();'
Rep '            for (int c = 0; c < inp.cols; c++)
                for (int r = 0; r < inp.rows; r++)
                {
                    rng = new System.Random(Mix(inp.seed, c, r, 17));
                    BuildCell(c, r);
                }
            ValidateOutpostSlots();' '            // 1. plan every cell (rooms, walls, doorways, themes), 2. the break room and the restrooms every floor must have (OfficePlanner.Core.cs), 3. furnish every cell with the random
            // numbers its own planning left off at: a floor is the same as before wherever the core rooms do not touch it
            var cells = new List<CellCtx>();
            for (int c = 0; c < inp.cols; c++)
                for (int r = 0; r < inp.rows; r++)
                {
                    rng = new System.Random(Mix(inp.seed, c, r, 17));
                    var cell = BuildCell(c, r);
                    cell.rng = rng;
                    cells.Add(cell);
                }
            EnsureRestrooms(cells);
            EnsureBreakRoom(cells);
            foreach (var cell in cells) { rng = cell.rng; FurnishCell(cell); }
            ValidateOutpostSlots();'
Rep '        void BuildCell(int c, int r)
        {' '        CellCtx BuildCell(int c, int r)
        {'
Rep '                else PlanSplit(cell);
            }
            FurnishCell(cell);
        }' '                else PlanSplit(cell);
            }
            return cell;
        }'
Rep '            foreach (var s in specs)
            {
                float a0 = s[0], a1 = s[1], b0 = s[2], b1 = s[3];' '            var madeRooms = new List<RoomCtx>();
            foreach (var s in specs)
            {
                float a0 = s[0], a1 = s[1], b0 = s[2], b1 = s[3];'
Rep '                    : Pick(new[] { "office", "conference", "break", "storage", "lounge", "server", "training" }, new[] { 5, 3, 3, 1, 2, 1, 2 }));
            }' '                    : Pick(new[] { "office", "conference", "break", "storage", "lounge", "server", "training" }, new[] { 5, 3, 3, 1, 2, 1, 2 }));
                madeRooms.Add(room);
            }
            if (madeRooms.Count == 2) cell.pairs.Add(madeRooms.ToArray());          // (two stacked rooms: the restrooms are picked from these)'
Rep '            else if (room.theme == "outpost")
            {
                room.info.wx = wb.Cx; room.info.wz = wb.Cz;      // (no patrol stops inside the guard outpost: the officers come from its elevator and leave)
            }' '            else if (room.theme == "outpost" || room.theme == ThemeRestroom)
            {
                room.info.wx = wb.Cx; room.info.wz = wb.Cz;      // (no patrol stops inside the guard outpost: the officers come from its elevator and leave; none in a restroom: its door never opens)
            }'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeThemes.cs')
Rep '        static int HideNeed(RoomCtx r) => r.kind == 0 ? 2 : r.kind == 1 ? 3 : (r.isRing ? 3 : 0);' '        static int HideNeed(RoomCtx r) => r.theme == ThemeRestroom ? 0 : r.kind == 0 ? 2 : r.kind == 1 ? 3 : (r.isRing ? 3 : 0);'
Rep '                case "outpost": ThOutpost(r); break;
            }' '                case "outpost": ThOutpost(r); break;
                case "restroom": break;                              // (nobody goes in: nothing is furnished)
            }'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficePlanner.Roles.cs')
Rep '            foreach (var rc in allRooms) if (RoomNumbers.IsNumbered(rc.info)) numbered.Add(rc);' '            foreach (var rc in allRooms) if (RoomNumbers.IsNumbered(rc.info) && rc.theme != ThemeBreak) numbered.Add(rc);        // (the break room is for everybody: never a suite, never the server room)'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\RoomNumbers.cs')
Rep 'public static bool IsNumbered(RoomInfo r) => r != null && r.kind == 0 && r.doors.Count > 0 && r.theme != "outpost";' 'public static bool IsNumbered(RoomInfo r) => r != null && r.kind == 0 && r.doors.Count > 0 && r.theme != "outpost" && r.theme != "restroom";'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\AfterHours.cs')
Rep '                case "server": return 0.9f;' '                case "server": return 0.9f;
                case "restroom": return 0f;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Doors\SlidingDoor.cs')
Rep '        public bool Locked;' '        public bool Locked;
        /// <summary>A door that never opens, for anybody (a restroom: "Players should not be allowed to enter the restroom"): <see cref="Locked"/> as well, but no lock to hack and officers do not open it either.</summary>
        public bool Permanent;'
Rep '            bool held = Latched || GuardNear;' '            bool held = !Permanent && (Latched || GuardNear);'
Rep '        public void SnapOpen()
        {' '        public void SnapOpen()
        {
            if (Permanent) return;'
Rep '        public void SnapLatched()
        {' '        public void SnapLatched()
        {
            if (Permanent) return;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Doors\DoorManager.cs')
Rep '                if (d.Locked) OutpostController.Active?.UseLockedDoor(me);     // (a locked door is hacked, not opened)' '                if (d.Permanent) { }                                              // (a restroom door: nothing happens, the prompt says it is locked)
                else if (d.Locked) OutpostController.Active?.UseLockedDoor(me);     // (a locked door is hacked, not opened)'
Rep '            string text = d.Locked ? (OutpostController.Active != null ? OutpostController.Active.LockPrompt : "Locked")' '            string text = d.Permanent ? "Locked" : d.Locked ? (OutpostController.Active != null ? OutpostController.Active.LockPrompt : "Locked")'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Doors.cs')
Rep '                    var door = MakeDoor(holder.transform, w, mat);
                    byBeam[w] = door;' '                    var door = MakeDoor(holder.transform, w, mat);
                    if (IsDeadDoor(w)) { door.Locked = true; door.Permanent = true; }       // (a restroom: the door never opens)
                    byBeam[w] = door;'
Rep '        /// <summary>How many doors of the floor slide into the wall beside them' '        /// <summary>Is this doorway one of the restrooms (the planner marked it: Layout.deadDoors)?</summary>
        bool IsDeadDoor(WallBox beam)
        {
            var area = beam.rc.Grow(0.05f);
            for (int i = 0; i < layout.deadDoors.Count; i++) if (layout.deadDoors[i].Overlaps(area)) return true;
            return false;
        }

        /// <summary>How many doors of the floor slide into the wall beside them'
Save-Src
Write-Output 'round 4 batch 9 patched'
