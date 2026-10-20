using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The guard outpost and the floor safe. The outpost takes the farthest cell of the floor that is not the guards' elevator's:
    /// a room of 9 x 6.6 m with a long one-way window and a locked door in its front wall (which faces the corridor the cell opens to),
    /// desks with monitors under the window, lockers, a table for the things lying about, and one guard on duty at his post. The backup does
    /// not arrive here: it comes out of the guards' own elevator in the corner opposite the players' (FloorShape.ShaftCell). The safe is a
    /// small prop against a wall of one of the numbered rooms. Everything is pure data
    /// (no Unity types) and a function of the seed, so every machine builds the same one.
    ///
    /// The outpost's own frame is the usual room frame: local +z points from the front wall into the room, local +x is to the right
    /// when walking in - so, standing outside and looking at the front wall, the door is on your right and the window on your left.
    /// </summary>
    public sealed partial class OfficePlanner
    {
        /// <summary>The inside of the outpost: along the front wall and from the front wall to the back one.</summary>
        public const float OutpostW = 9.0f, OutpostD = 6.6f;
        /// <summary>The window in the front wall: where it starts and ends along the wall, and the heights of its sill and top.</summary>
        public const float OutpostWindowX0 = -4.1f, OutpostWindowX1 = 1.5f, OutpostSill = 1.0f, OutpostWindowTop = 2.5f;
        /// <summary>The door in the front wall: its middle along the wall and its width (the beam over it starts at <see cref="OutpostBeamY"/>).</summary>
        public const float OutpostDoorX = 2.9f, OutpostDoorW = 2.0f, OutpostBeamY = 2.45f;
        /// <summary>How deep the strip in front of the front wall is that nothing may stand in (the window needs a view, the lock needs room to stand).</summary>
        public const float OutpostFrontStrip = 3.2f;
        /// <summary>The height of the keypad's middle.</summary>
        public const float OutpostKeypadY = 1.25f;
        /// <summary>The alarm panel inside, on the front wall's pier beside the door: how far past the door's edge its middle is, and the height of its middle.</summary>
        public const float OutpostPanelOffset = 0.3f, OutpostPanelY = 1.3f;

        // ------------------------------------------------------------------ the room

        /// <summary>Plans the outpost in its cell: walls, window, door, and the block that the surrounding area works round.</summary>
        void PlanOutpost(CellCtx cell, RoomCtx ring)
        {
            int gx = inp.outpostDirX, gz = inp.outpostDirY;
            if (gx != 0 && gz != 0) gz = 0;
            if (gx == 0 && gz == 0) gx = -1;
            // local +z (from the front wall into the room) points away from the open side the front faces
            int turns = gx < 0 ? 1 : gx > 0 ? 3 : gz < 0 ? 0 : 2;

            float w = OutpostW, d = OutpostD, hx = w / 2, hz = d / 2, t = Thick, wallTop = inp.wallH;
            var room = NewRoom(cell, "outpost", 0, 0f, 0f, turns, w, d);

            void Box(float lx0, float lz0, float lx1, float lz1, float y0, float y1, string kind) =>
                AddBox(room.ToWorldRect(new Rc(lx0, lz0, lx1, lz1)), y0, y1, kind);

            // the back, west and east walls (the back wall and the front wall own the corners)
            Box(-hx - t, hz, hx + t, hz + t, 0f, wallTop, "wall");
            Box(-hx - t, -hz, -hx, hz, 0f, wallTop, "wall");
            Box(hx, -hz, hx + t, hz, 0f, wallTop, "wall");

            // the front wall: pier, window (sill below, lintel above), pier, the locked door under its beam, pier
            float fz0 = -hz - t, fz1 = -hz;
            float door0 = OutpostDoorX - OutpostDoorW / 2, door1 = OutpostDoorX + OutpostDoorW / 2;
            Box(-hx - t, fz0, OutpostWindowX0, fz1, 0f, wallTop, "wall");
            Box(OutpostWindowX0, fz0, OutpostWindowX1, fz1, 0f, OutpostSill, "wall");
            Box(OutpostWindowX0, fz0, OutpostWindowX1, fz1, OutpostWindowTop, wallTop, "wall");
            Box(OutpostWindowX1, fz0, door0, fz1, 0f, wallTop, "wall");
            Box(door0, fz0, door1, fz1, OutpostBeamY, wallTop, "lockheader");
            Box(door1, fz0, hx + t, fz1, 0f, wallTop, "wall");

            RoomDoor(cell, room, OutpostDoorX, OutpostDoorW);

            // the block the surrounding area (the ring) works round: the whole outpost with its walls
            var outer = room.ToWorldRect(new Rc(-hx - t, -hz - t, hx + t, hz + t));
            ring.blocks.Add(ring.ToLocalRect(outer));
            ring.info.blocks.Add(outer);

            // nothing stands in the strip in front of the front wall: the window needs a view and the lock needs room to stand
            cell.keeps.Add(room.ToWorldRect(new Rc(-hx - t - 0.4f, -hz - t - OutpostFrontStrip, hx + t + 0.4f, -hz - t)));

            room.ToWorld(door1 + 0.4f, fz0, out var keypadX, out var keypadZ);
            room.ToWorld(door1 + OutpostPanelOffset, fz1, out var panelX, out var panelZ);        // (inside: on the room's face of the same pier)
            layout.outpost = new OutpostInfo
            {
                room = room.info,
                bounds = room.WorldBounds,
                outer = outer,
                frontX = gx, frontZ = gz,
                door = room.ToWorldRect(new Rc(door0, fz0, door1, fz1)),
                keypadX = keypadX, keypadY = OutpostKeypadY, keypadZ = keypadZ,
                panelX = panelX, panelY = OutpostPanelY, panelZ = panelZ,
                window = room.ToWorldRect(new Rc(OutpostWindowX0, fz0, OutpostWindowX1, fz1)),
                windowY0 = OutpostSill, windowY1 = OutpostWindowTop,
            };
        }

        // ------------------------------------------------------------------ the furniture

        /// <summary>A place on top of an item (u across its front, v along it, in metres from its middle) for something that is not a prop.</summary>
        Slot TopSlot(RoomCtx r, Item host, float u, float v, string kind, float yawOff = 0f)
        {
            RoomCtx.RotQ((((int)Math.Round(host.localYaw / 90f)) % 4 + 4) % 4, u, v, out var du, out var dv);
            r.ToWorld(host.lx + du, host.lz + dv, out var wx, out var wz);
            float yaw = ((90f * r.turns + host.localYaw + yawOff) % 360f + 360f) % 360f;
            return new Slot { x = wx, y = host.Top, z = wz, yaw = yaw, kind = kind, room = r.name, host = host.piece };
        }

        /// <summary>Hangs the prop at the first of the spots (along the wall) that is free.</summary>
        static Item MountAt(RoomCtx r, char side, string key, float height, params float[] spots)
        {
            foreach (var s in spots)
            {
                var it = r.Mount(side, key, s, height);
                if (it != null) return it;
            }
            return null;
        }

        void ThOutpost(RoomCtx r)
        {
            var op = layout.outpost;

            // the briefing table against the east wall: whatever the guards keep lying about is put on it
            var table = r.Wall('E', "desk_3", 0.2f);
            if (table != null)
            {
                r.Put("chair_office", table.lx - table.def.D / 2 - Tuck("chair_office"), table.lz, 90f);
                if (op != null)
                    for (int row = 0; row < 2; row++)
                        for (int col = 0; col < 3; col++)
                            op.itemSlots.Add(TopSlot(r, table, -0.8f + 0.8f * col, row == 0 ? 0.15f : -0.15f, "item", (col + row) % 2 == 0 ? 0f : 20f));
                r.Mount('E', "whiteboard", 0.2f, 1.25f);
            }

            // the monitoring desks under the window, facing it; the computer with the record of a safe stands on the last of them
            var desks = new List<Item>();
            for (int i = 0; i < 3; i++)
            {
                float x = -3.4f + 2.1f * i;                                 // (the pack's desks are 2 m wide)
                var desk = r.Wall('S', "desk_1", x);
                if (desk == null) continue;
                desks.Add(desk);
                float dd = desk.def.D;
                r.Put("chair_office", x, desk.lz + dd / 2 + Tuck("chair_office"), 180f);
                r.Top(desk, "monitor_new", -0.5f, -dd / 2 + 0.2f);
                r.Top(desk, i == 1 ? "monitor_old" : "monitor_new", 0.3f, -dd / 2 + 0.2f);
                r.Top(desk, "keyboard", -0.25f, 0.10f);
                r.Top(desk, "mouse", 0.2f, 0.10f);
                if (i == 0) r.Top(desk, "phone", 0.75f, 0.22f);
                if (i == 1) r.Top(desk, "papers", 0.7f, 0.0f, 90f);
            }
            if (op != null && desks.Count > 0)
                op.itemSlots.Insert(0, TopSlot(r, desks[desks.Count - 1], 0.5f, 0.0f, "computer", 0f));

            // lockers along the whole back wall and the west wall
            for (int i = 0; i < 7; i++) r.Wall('N', "locker2", -3.9f + 1.2f * i);
            r.Wall('W', "locker2", -1.5f);

            // monitors on the wall above them, a clock, a bin
            for (int i = 0; i < 7; i += 2) r.Mount('N', "wall_tv", -3.9f + 1.2f * i, 2.2f);       // (the pack's wall screens are 1.5 m wide: one above every other locker)
            r.Mount('W', "wall_tv", -1.5f, 2.2f);
            MountAt(r, 'E', "clock", 2.1f, 2.4f, -2.2f);
            r.Wall('E', "trash_bin", 2.2f);
        }

        // ------------------------------------------------------------------ the floor safe

        static bool SafeFriendly(string theme)
        {
            switch (theme)
            {
                case "office": case "conference": case "meeting": case "storage": case "server": case "copy": case "training": return true;
                default: return false;
            }
        }

        /// <summary>
        /// Puts the floor safe against a wall of one of the numbered rooms, if this floor has one. The room is picked with the roll the
        /// caller supplied (offices, meeting rooms, storage and so on first); the spot is the first along the walls where the safe fits,
        /// is clear of doorways, walls nothing off and is not on top of the room's patrol stop.
        /// </summary>
        void PlaceSafe()
        {
            if (!inp.hasSafe) return;
            var suites = new List<RoomCtx>();
            var friendly = new List<RoomCtx>();
            var others = new List<RoomCtx>();
            foreach (var rc in allRooms)
            {
                if (!RoomNumbers.IsNumbered(rc.info)) continue;
                if (rc.info.role == RoleSuite) suites.Add(rc);
                else if (rc.info.role == RoleServer) { }                         // (the server room keeps its racks)
                else (SafeFriendly(rc.theme) ? friendly : others).Add(rc);
            }
            foreach (var tier in new[] { suites, friendly, others })
                for (int k = 0; k < tier.Count; k++)
                {
                    var room = tier[(int)((inp.safeRoll + (uint)k) % (uint)tier.Count)];
                    if (TryPlaceSafeIn(room)) return;
                }
        }

        bool TryPlaceSafeIn(RoomCtx room)
        {
            var def = PropTable.Get("vault_safe");
            char[] sides = { 'N', 'E', 'W', 'S' };
            int first = (int)((inp.safeRoll >> 8) % 4U);
            room.ToLocal(room.info.wx, room.info.wz, out var wpx, out var wpz);
            for (int si = 0; si < 4; si++)
            {
                char side = sides[(first + si) % 4];
                if (!room.HasWall(side)) continue;
                float len = (side == 'N' || side == 'S') ? room.w : room.d;
                float lo = -len / 2 + def.W / 2 + 0.3f, hi = len / 2 - def.W / 2 - 0.3f;
                if (hi < lo) continue;
                const float step = 0.35f;
                int steps = (int)((hi - lo) / step) + 1;
                int start = (int)((inp.safeRoll >> 16) % (uint)steps);
                for (int i = 0; i < steps; i++)
                {
                    float along = lo + ((start + i) % steps) * step;
                    var it = room.WallSafe(side, "vault_safe", along);
                    if (it == null) continue;
                    if (it.rc.Grow(0.7f).Contains(wpx, wpz)) { room.RemoveItem(it); continue; }      // (a patrol stop is not put inside the safe)
                    // somebody must be able to stand square in front of it (and get there)
                    float fd = def.D / 2 + 0.75f, sxl = it.lx, szl = it.lz;
                    switch (side)
                    {
                        case 'N': szl -= fd; break;
                        case 'S': szl += fd; break;
                        case 'W': sxl += fd; break;
                        default: sxl -= fd; break;
                    }
                    if (!room.IsFree(sxl, szl, 0.35f) || !room.ComputeReach().Seen(sxl, szl, 0.1f)) { room.RemoveItem(it); continue; }
                    layout.safe = new SafeSpot
                    {
                        piece = it.piece, room = room.info, roomIndex = layout.rooms.IndexOf(room.info),
                        x = it.piece.x, y = 0f, z = it.piece.z, yaw = it.piece.yaw
                    };
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ after everything is placed

        /// <summary>
        /// The outpost's places for its things must stand on something that still exists (a desk that nobody could reach is taken out by
        /// <see cref="Settle"/>); any that do not are replaced by places on the floor, so the outpost can always hold what it should.
        /// </summary>
        void ValidateOutpostSlots()
        {
            var op = layout.outpost;
            if (op == null) return;
            var live = new HashSet<Piece>(layout.pieces);
            op.itemSlots.RemoveAll(s => s.host != null && !live.Contains(s.host));

            int wanted = OutpostLoot.MaxItems + 1;                  // the computer's place and one for each thing
            bool hasNote = op.itemSlots.Exists(s => s.kind == "computer");
            if (op.itemSlots.Count >= wanted && hasNote) return;

            RoomCtx room = null;
            foreach (var rc in allRooms) if (ReferenceEquals(rc.info, op.room)) { room = rc; break; }
            if (room == null) return;
            var reach = room.ComputeReach();
            float[][] around =
            {
                new[] { 1f, -0.5f }, new[] { 1f, 0.5f }, new[] { 0f, 0f }, new[] { 0f, -1f }, new[] { 0f, 1f }, new[] { 2f, 0f }, new[] { -1f, 0f }, new[] { 2f, 1.5f }
            };
            for (int i = 0; i < around.Length && (op.itemSlots.Count < wanted || !hasNote); i++)
            {
                if (!room.FindFreeReach(reach, around[i][0], around[i][1], out var fx, out var fz, 0.6f)) continue;
                room.ToWorld(fx, fz, out var wx, out var wz);
                string kind = hasNote ? "item" : "computer";
                hasNote = true;
                op.itemSlots.Add(new Slot { x = wx, y = 0f, z = wz, yaw = 0f, kind = kind, room = room.name });
            }
        }
    }
}
