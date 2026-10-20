using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    // =====================================================================================================
    // Plain data the office generator hands to the floor builder. No Unity types in here (so the whole layout
    // can be run and tested outside the editor). Coordinates: x = east, z = north, y = up, metres; the floor
    // is y = 0 and everything is in the same world space as FloorGenerator.CellCenter.
    // =====================================================================================================

    /// <summary>Axis-aligned rectangle on the floor plane.</summary>
    public struct Rc
    {
        public float x0, z0, x1, z1;
        public Rc(float x0, float z0, float x1, float z1) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; }
        public float W => x1 - x0;
        public float D => z1 - z0;
        public float Cx => (x0 + x1) * 0.5f;
        public float Cz => (z0 + z1) * 0.5f;
        public bool Overlaps(Rc o, float eps = 0.001f) => x0 < o.x1 - eps && x1 > o.x0 + eps && z0 < o.z1 - eps && z1 > o.z0 + eps;
        public bool Contains(float x, float z) => x >= x0 && x <= x1 && z >= z0 && z <= z1;
        public Rc Grow(float m) => new Rc(x0 - m, z0 - m, x1 + m, z1 + m);
        public Rc Shift(float dx, float dz) => new Rc(x0 + dx, z0 + dz, x1 + dx, z1 + dz);
        public static Rc Centered(float cx, float cz, float w, float d) => new Rc(cx - w * 0.5f, cz - d * 0.5f, cx + w * 0.5f, cz + d * 0.5f);
        public override string ToString() => $"[{x0:0.##},{z0:0.##} .. {x1:0.##},{z1:0.##}]";
    }

    /// <summary>A plain box: an interior wall, a door header or a cubicle divider.</summary>
    public sealed class WallBox
    {
        public Rc rc;
        public float y0, y1;
        /// <summary>"wall", "header" (over a doorway) or "panel" (cubicle divider).</summary>
        public string kind = "wall";
    }

    /// <summary>One prop to put in the world.</summary>
    public sealed class Piece
    {
        public PropDef def;
        public string key;
        /// <summary>Footprint centre (x, z) and the height of its underside (y).</summary>
        public float x, y, z;
        /// <summary>Degrees; the prop's front faces (sin yaw, cos yaw).</summary>
        public float yaw;
        /// <summary>World footprint (axis aligned).</summary>
        public Rc rc;
        public float h;
        public bool solid, mount, onTop, hide, loot;
        /// <summary>Chosen by the floor builder: this one is spawned as an object to steal (the rest of the flagged ones stay plain props).</summary>
        public bool picked;
        public string room;
        /// <summary>The piece this one stands on (a monitor on a desk); it goes when the host goes.</summary>
        public Piece host;
    }

    public sealed class FloorPatch { public Rc rc; public int tint; }

    /// <summary>A spot for one of the big treasures (safe, server, ...) or a mug.</summary>
    public sealed class Slot
    {
        public float x, y, z, yaw;
        public string kind;
        public string room;
        public Piece host;
    }

    public sealed class RoomInfo
    {
        public string theme;
        /// <summary>null, "suite" (an executive suite) or "server" (the server room): the rooms the security computer locks (OfficePlanner.Roles.cs).</summary>
        public string role;
        public int kind;                 // 0 room, 1 open zone, 2 corridor / lobby
        public Rc bounds;                // world
        public int cellX, cellY;
        public float wx, wz;             // a free standing spot (guard waypoint)
        public int hideCount, pieceCount;
        public readonly List<Rc> doors = new List<Rc>();   // world rectangles of the doorway openings
        /// <summary>World rectangles inside the room that nobody can stand in (the block in the middle of a ring); no ceiling panel hangs over them.</summary>
        public readonly List<Rc> blocks = new List<Rc>();
    }

    public sealed class Layout
    {
        public readonly List<WallBox> walls = new List<WallBox>();
        public readonly List<Piece> pieces = new List<Piece>();
        public readonly List<FloorPatch> patches = new List<FloorPatch>();
        /// <summary>Merged world rectangles around big furniture: each becomes one hide trigger.</summary>
        public readonly List<Rc> hideZones = new List<Rc>();
        public readonly List<Slot> bigSlots = new List<Slot>();
        public readonly List<Slot> mugSlots = new List<Slot>();
        public readonly List<RoomInfo> rooms = new List<RoomInfo>();
        /// <summary>Walkable spots for guard routes: x, z and the cell they belong to.</summary>
        public readonly List<float[]> waypoints = new List<float[]>();
        /// <summary>World rectangles that must stay walkable (doorways, gaps); used by the tests.</summary>
        public readonly List<Rc> clear = new List<Rc>();
        /// <summary>World rectangles of doorways whose door NEVER opens (the restrooms: the owner, 2026-10-17 night, "Players should not be allowed to enter the restroom"): FloorGenerator builds them as locked doors.</summary>
        public readonly List<Rc> deadDoors = new List<Rc>();
        /// <summary>The guard outpost (the room with the guard on duty, in a cell of its own); null on a floor without one.</summary>
        public OutpostInfo outpost;
        /// <summary>The floor safe of this floor; null when this floor has none.</summary>
        public SafeSpot safe;
        /// <summary>What this layout was planned from (the maze, the elevator's cell, the cell size); the room numbers follow the maze.</summary>
        public PlanInput input;
    }

    /// <summary>
    /// The guard outpost: a room with a long one-way window and a locked door on its front wall, desks and monitors, lockers, a table for
    /// the things lying about (a note, weapons, valuables) and one guard on duty (the backup does not arrive here: see FloorShape.ShaftCell). All rectangles are
    /// world rectangles; directions are unit steps along x or z.
    /// </summary>
    public sealed class OutpostInfo
    {
        public RoomInfo room;
        /// <summary>The inside of the room (the walls stand outside it) and the outside of the walls.</summary>
        public Rc bounds, outer;
        /// <summary>The way the front wall faces: out of the room, towards the corridor.</summary>
        public int frontX, frontZ;
        /// <summary>The doorway in the front wall (a thin rectangle through the wall), and the lock panel beside it.</summary>
        public Rc door;
        /// <summary>Where the keypad hangs: the middle of its back, on the outside of the front wall (x, y, z).</summary>
        public float keypadX, keypadY, keypadZ;
        /// <summary>Where the alarm panel hangs: the middle of its back, on the inside face of the front wall beside the door (x, y, z). It faces into the room.</summary>
        public float panelX, panelY, panelZ;
        /// <summary>The window in the front wall: a thin rectangle through the wall, and the heights of its sill and top.</summary>
        public Rc window;
        public float windowY0, windowY1;
        /// <summary>Places on the table for a note, weapons and valuables, in the order to fill them (the first is the note's).</summary>
        public readonly List<Slot> itemSlots = new List<Slot>();
    }

    /// <summary>Where the floor's safe stands.</summary>
    public sealed class SafeSpot
    {
        public Piece piece;
        public RoomInfo room;
        public int roomIndex;
        public float x, y, z, yaw;
    }

    /// <summary>A prop placed in a room, in the room's own frame.</summary>
    public sealed class Item
    {
        public PropDef def;
        public Rc rc;                    // local footprint
        public float elev, h, lx, lz, localYaw;
        public bool solid, mount;
        public Piece piece;
        public float Top => elev + h;
    }

    /// <summary>Which parts of a room a person can actually walk to (grid flood fill from the doorways).</summary>
    public sealed class ReachGrid
    {
        public const float Res = 0.2f;
        public int nx, nz;
        public float ox, oz, agent;
        public bool[] free, seen;

        public bool Seen(float lx, float lz, float within = 0f)
        {
            int cx = (int)Math.Floor((lx - ox) / Res), cz = (int)Math.Floor((lz - oz) / Res);
            int rr = (int)Math.Ceiling(within / Res);
            for (int a = -rr; a <= rr; a++)
                for (int b = -rr; b <= rr; b++)
                {
                    int x = cx + a, z = cz + b;
                    if (x >= 0 && z >= 0 && x < nx && z < nz && seen[z * nx + x]) return true;
                }
            return false;
        }

        /// <summary>Can a person stand within <paramref name="extra"/> metres of the rectangle (beyond their own radius)?</summary>
        public bool SeenNear(Rc rc, float extra)
        {
            var g = rc.Grow(agent + extra);
            int x0 = Math.Max(0, (int)Math.Floor((g.x0 - ox) / Res)), x1 = Math.Min(nx - 1, (int)Math.Floor((g.x1 - ox) / Res));
            int z0 = Math.Max(0, (int)Math.Floor((g.z0 - oz) / Res)), z1 = Math.Min(nz - 1, (int)Math.Floor((g.z1 - oz) / Res));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                    if (seen[z * nx + x]) return true;
            return false;
        }
    }

    /// <summary>
    /// A furnished area in its own "canonical" frame: local +Z points from the (south) door wall into the room,
    /// local +X is to the right when you walk in. <c>turns</c> quarter turns map that frame onto the world.
    /// Every theme is written once against this frame and works for any wall of the building.
    /// </summary>
    public sealed class RoomCtx
    {
        public readonly OfficePlanner pl;
        public string theme;
        public int kind;
        public readonly float cx, cz;
        public readonly int turns;
        public readonly float w, d;
        /// <summary>Local rectangles nothing may be placed in (door approaches, passages).</summary>
        public readonly List<Rc> keep = new List<Rc>();
        /// <summary>Aisles that must stay free of floor furniture (the cross aisle of a long room).</summary>
        public readonly List<Rc> lanes = new List<Rc>();
        public readonly List<Item> floor = new List<Item>();
        public readonly List<Item> hides = new List<Item>();
        public readonly List<float[]> doors = new List<float[]>();   // local x of each door on the south wall, and its width
        public RoomInfo info;
        public string name;
        /// <summary>Filled in once the room is furnished: where a person can walk.</summary>
        public ReachGrid reach;
        /// <summary>Which sides are real walls (S, N, W, E) - false where the room is open to a corridor.</summary>
        public readonly bool[] wallOn = { true, true, true, true };
        /// <summary>Corridor arms: the far end has a doorway to the next cell.</summary>
        public bool endGap;
        /// <summary>Corridor arm of an open-plan hall (no walls beside it).</summary>
        public bool openLane;
        /// <summary>The long corridor of a split cell (doorways at both ends).</summary>
        public bool longHall, endGapA, endGapB;
        /// <summary>The lobby around an elevator: one big room with a solid block (shaft / car) in the middle.</summary>
        public bool isRing;
        /// <summary>Solid blocks inside the room that nobody can stand in (local rectangles).</summary>
        public readonly List<Rc> blocks = new List<Rc>();

        public System.Random rng => pl.rng;

        public void SetTheme(string t) { theme = t; if (info != null) info.theme = t; }
        /// <summary>Is local x within a cross aisle (plus a margin)?</summary>
        public bool InLane(float lx, float margin)
        {
            for (int i = 0; i < lanes.Count; i++) if (lx > lanes[i].x0 - margin && lx < lanes[i].x1 + margin) return true;
            return false;
        }

        public bool HasWall(char side) => wallOn[side == 'S' ? 0 : side == 'N' ? 1 : side == 'W' ? 2 : 3];

        public RoomCtx(OfficePlanner pl, string theme, int kind, float cx, float cz, int turns, float w, float d)
        {
            this.pl = pl; this.theme = theme; this.kind = kind; this.cx = cx; this.cz = cz;
            this.turns = ((turns % 4) + 4) % 4; this.w = w; this.d = d;
        }

        // ------------------------------------------------------------------ frames

        public static void RotQ(int q, float x, float z, out float ox, out float oz)
        {
            switch (((q % 4) + 4) % 4)
            {
                case 0: ox = x; oz = z; break;
                case 1: ox = z; oz = -x; break;
                case 2: ox = -x; oz = -z; break;
                default: ox = -z; oz = x; break;
            }
        }

        public void ToWorld(float lx, float lz, out float wx, out float wz)
        {
            RotQ(turns, lx, lz, out var dx, out var dz);
            wx = cx + dx; wz = cz + dz;
        }

        public void ToLocal(float wx, float wz, out float lx, out float lz)
        {
            RotQ(-turns, wx - cx, wz - cz, out lx, out lz);
        }

        public Rc ToWorldRect(Rc r)
        {
            ToWorld(r.x0, r.z0, out var ax, out var az);
            ToWorld(r.x1, r.z1, out var bx, out var bz);
            return new Rc(Math.Min(ax, bx), Math.Min(az, bz), Math.Max(ax, bx), Math.Max(az, bz));
        }

        public Rc ToLocalRect(Rc r)
        {
            ToLocal(r.x0, r.z0, out var ax, out var az);
            ToLocal(r.x1, r.z1, out var bx, out var bz);
            return new Rc(Math.Min(ax, bx), Math.Min(az, bz), Math.Max(ax, bx), Math.Max(az, bz));
        }

        public Rc WorldBounds => ToWorldRect(new Rc(-w / 2, -d / 2, w / 2, d / 2));

        // ------------------------------------------------------------------ placing

        static bool Conflict(Item o, Rc rc, bool solid)
        {
            float eps = (o.solid && solid) ? 0.01f : (o.solid || solid) ? 0.28f : 0.08f;
            return o.rc.Overlaps(rc, eps);
        }

        /// <summary>
        /// Places a prop with its centre at (lx, lz). Returns null (and places nothing) if it would leave the room,
        /// block a passage or overlap something. elev &gt; 0 puts it on top of something (no overlap checks).
        /// </summary>
        public Item Put(string key, float lx, float lz, float localYaw, float elev = 0f, bool check = true, bool mount = false, int solidOverride = -1)
        {
            return PutDef(PropTable.Get(key), lx, lz, localYaw, elev, check, mount, solidOverride);
        }

        public Item PutDef(PropDef def, float lx, float lz, float localYaw, float elev, bool check, bool mount, int solidOverride)
        {
            float pw = def.W, ph = def.H, pd = def.D;
            int q = (((int)Math.Round(localYaw / 90f)) % 4 + 4) % 4;
            float fx = (q & 1) == 0 ? pw : pd, fz = (q & 1) == 0 ? pd : pw;
            var rc = new Rc(lx - fx / 2, lz - fz / 2, lx + fx / 2, lz + fz / 2);
            bool solid = solidOverride < 0 ? def.solid : solidOverride == 1;
            if (check)
            {
                const float e = 0.005f;
                if (rc.x0 < -w / 2 - e || rc.x1 > w / 2 + e || rc.z0 < -d / 2 - e || rc.z1 > d / 2 + e) return null;
                for (int i = 0; i < keep.Count; i++) if (keep[i].Overlaps(rc)) return null;
                if (elev <= 0f && !mount)
                {
                    for (int i = 0; i < blocks.Count; i++) if (blocks[i].Overlaps(rc)) return null;
                    for (int i = 0; i < lanes.Count; i++) if (lanes[i].Overlaps(rc)) return null;
                    for (int i = 0; i < floor.Count; i++) if (Conflict(floor[i], rc, solid)) return null;
                }
            }
            var it = new Item { def = def, rc = rc, elev = elev, h = ph, lx = lx, lz = lz, localYaw = localYaw, solid = solid, mount = mount };
            ToWorld(lx, lz, out var wx, out var wz);
            var wrc = ToWorldRect(rc);
            float yaw = ((90f * turns + localYaw) % 360f + 360f) % 360f;
            bool onFloor = elev <= 0f && !mount;
            bool hide = solid && onFloor && ph >= HideMinHeight && Math.Max(fx, fz) >= 0.4f && def.cat != PropCat.Chair;
            var pc = new Piece
            {
                def = def, key = def.key, x = wx, y = elev, z = wz, yaw = yaw, rc = wrc, h = ph,
                solid = solid, mount = mount, onTop = elev > 0f && !mount, hide = hide, room = name
            };
            it.piece = pc;
            pl.layout.pieces.Add(pc);
            if (onFloor) floor.Add(it);
            if (hide) hides.Add(it);
            return it;
        }

        /// <summary>
        /// How tall a piece of furniture must be to duck behind. It was 0.9 m while the desks were 0.95 m; the Synty office pack's desks and tables are 0.75 m (a person crouches behind a desk all the same), so it is
        /// 0.7 m now and the hiding places per room stay about what they were (the owner, 2026-10-17 night: "use the office assets"; the rules stay).
        /// </summary>
        public const float HideMinHeight = 0.7f;

        static int FaceQ(char side) => side == 'S' ? 0 : side == 'N' ? 2 : side == 'W' ? 1 : 3;

        /// <summary>Stands a prop against a wall of the room, facing in. <paramref name="along"/> is its centre along that wall.</summary>
        public Item Wall(char side, string key, float along, float gap = 0.02f, bool check = true, int solidOverride = -1)
        {
            var def = PropTable.Get(key);
            float pd = def.D;
            float ly = 90f * FaceQ(side);
            switch (side)
            {
                case 'S': return PutDef(def, along, -d / 2 + gap + pd / 2, ly, 0f, check, false, solidOverride);
                case 'N': return PutDef(def, along, d / 2 - gap - pd / 2, ly, 0f, check, false, solidOverride);
                case 'W': return PutDef(def, -w / 2 + gap + pd / 2, along, ly, 0f, check, false, solidOverride);
                default: return PutDef(def, w / 2 - gap - pd / 2, along, ly, 0f, check, false, solidOverride);
            }
        }

        /// <summary>Places props one after another along a wall between a0 and a1 (positions along the wall).</summary>
        public List<Item> Run(char side, string[] keys, float a0, float a1, bool random = false, float spacing = 0f, float gap = 0.02f)
        {
            var res = new List<Item>();
            float cur = a0; int i = 0, fails = 0;
            while (cur < a1 - 0.05f && fails < 6 && i < 60)
            {
                string key = random ? keys[rng.Next(keys.Length)] : keys[i % keys.Length];
                i++;
                var def = PropTable.Get(key);
                float pw = def.W;
                if (cur + pw > a1 + 1e-4f) { if (random) { fails++; continue; } break; }
                var it = Wall(side, key, cur + pw / 2f, gap);
                if (it != null) res.Add(it); else fails++;
                cur += pw + spacing;
            }
            return res;
        }

        /// <summary>Hangs a flat prop on a wall at the given height (no floor space used). Skips doorways and tall furniture.</summary>
        public Item Mount(char side, string key, float along, float height, float gap = 0.0f)
        {
            var def = PropTable.Get(key);
            float pw = def.W, pd = def.D;
            float ly = 90f * FaceQ(side);
            float lx, lz;
            switch (side)
            {
                case 'S': lx = along; lz = -d / 2 + gap + pd / 2; break;
                case 'N': lx = along; lz = d / 2 - gap - pd / 2; break;
                case 'W': lx = -w / 2 + gap + pd / 2; lz = along; break;
                default: lx = w / 2 - gap - pd / 2; lz = along; break;
            }
            int q = (((int)Math.Round(ly / 90f)) % 4 + 4) % 4;
            float fx = (q & 1) == 0 ? pw : pd, fz = (q & 1) == 0 ? pd : pw;
            var rc = new Rc(lx - fx / 2, lz - fz / 2, lx + fx / 2, lz + fz / 2);
            // inside the room along the wall
            if (rc.x0 < -w / 2 - 0.01f || rc.x1 > w / 2 + 0.01f || rc.z0 < -d / 2 - 0.01f || rc.z1 > d / 2 + 0.01f) return null;
            for (int i = 0; i < keep.Count; i++) if (keep[i].Overlaps(rc)) return null;
            // tall furniture standing in front of this spot would poke through it
            Rc deep = rc;
            const float depth = 0.45f;
            switch (side)
            {
                case 'S': deep.z1 = rc.z0 + depth; break;
                case 'N': deep.z0 = rc.z1 - depth; break;
                case 'W': deep.x1 = rc.x0 + depth; break;
                default: deep.x0 = rc.x1 - depth; break;
            }
            for (int i = 0; i < floor.Count; i++)
                if (floor[i].h > height - 0.05f && floor[i].rc.Overlaps(deep, 0.02f)) return null;
            return PutDef(def, lx, lz, ly, height, false, true, 0);
        }

        /// <summary>Puts a prop on top of a host item. u is across the host's front, v along it (host-local metres from its centre).</summary>
        public Item Top(Item host, string key, float u, float v, float yawOff = 0f)
        {
            if (host == null) return null;
            RotQ((((int)Math.Round(host.localYaw / 90f)) % 4 + 4) % 4, u, v, out var du, out var dv);
            var top = Put(key, host.lx + du, host.lz + dv, host.localYaw + yawOff, host.Top, false, false, 0);
            if (top != null && top.piece != null) top.piece.host = host.piece;
            return top;
        }

        /// <summary>Marks a prop as something to steal (when it has a value) with the given chance.</summary>
        public Item Loot(Item it, double chance)
        {
            if (it == null || it.piece == null || !it.def.Lootable) return it;
            if (rng.NextDouble() < chance) it.piece.loot = true;
            return it;
        }

        /// <summary>Adds a cubicle divider / screen (a plain box). axis 'x' runs along the room's x.</summary>
        public Item Panel(float lx, float lz, float length, float thick, char axis, float height = 1.45f)
        {
            float fx = axis == 'x' ? length : thick, fz = axis == 'x' ? thick : length;
            var rc = new Rc(lx - fx / 2, lz - fz / 2, lx + fx / 2, lz + fz / 2);
            const float e = 0.01f;
            if (rc.x0 < -w / 2 - e || rc.x1 > w / 2 + e || rc.z0 < -d / 2 - e || rc.z1 > d / 2 + e) return null;
            for (int i = 0; i < keep.Count; i++) if (keep[i].Overlaps(rc)) return null;
            for (int i = 0; i < lanes.Count; i++) if (lanes[i].Overlaps(rc)) return null;
            for (int i = 0; i < blocks.Count; i++) if (blocks[i].Overlaps(rc)) return null;
            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return null;
            var def = new PropDef("panel", null, fx, height, fz, 1f, 0, true, PropCat.Furniture, 0, 1f, 1);
            var it = new Item { def = def, rc = rc, elev = 0f, h = height, lx = lx, lz = lz, localYaw = 0f, solid = true, mount = false };
            ToWorld(lx, lz, out var wx, out var wz);
            var wrc = ToWorldRect(rc);
            var pc = new Piece { def = def, key = "panel", x = wx, y = 0f, z = wz, yaw = 0f, rc = wrc, h = height, solid = true, hide = true, room = name };
            it.piece = pc;
            pl.layout.pieces.Add(pc);
            floor.Add(it); hides.Add(it);
            return it;
        }

        /// <summary>Can solid furniture stand on this local rectangle: inside the room, clear of the keep-outs, the blocks and the aisles, and of the solid things already placed? (A whole cluster is tested before it is built.)</summary>
        public bool CanPlaceRect(Rc rc)
        {
            const float e = 0.005f;
            if (rc.x0 < -w / 2 - e || rc.x1 > w / 2 + e || rc.z0 < -d / 2 - e || rc.z1 > d / 2 + e) return false;
            for (int i = 0; i < keep.Count; i++) if (keep[i].Overlaps(rc)) return false;
            for (int i = 0; i < blocks.Count; i++) if (blocks[i].Overlaps(rc)) return false;
            for (int i = 0; i < lanes.Count; i++) if (lanes[i].Overlaps(rc)) return false;
            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return false;
            return true;
        }

        /// <summary>Reserves floor space (keeps other furniture away) for a treasure.</summary>
        public void Reserve(float lx, float lz, float rw, float rd)
        {
            keep.Add(new Rc(lx - rw / 2, lz - rd / 2, lx + rw / 2, lz + rd / 2));
        }

        /// <summary>Notes a place for one of the big treasures ("safe", "server", "espresso", "trophy", "stapler").</summary>
        public void BigSlot(string kind, float lx, float lz, float elev = 0f, float localYaw = 0f)
        {
            ToWorld(lx, lz, out var wx, out var wz);
            Piece host = null;
            if (elev > 0f)
                for (int i = 0; i < floor.Count; i++)
                    if (floor[i].rc.Contains(lx, lz) && Math.Abs(floor[i].Top - elev) < 0.05f) { host = floor[i].piece; break; }
            pl.layout.bigSlots.Add(new Slot { x = wx, y = elev, z = wz, yaw = ((90f * turns + localYaw) % 360f + 360f) % 360f, kind = kind, room = name, host = host });
        }

        public void MugSlot(Item host, float u, float v)
        {
            if (host == null) return;
            RotQ((((int)Math.Round(host.localYaw / 90f)) % 4 + 4) % 4, u, v, out var du, out var dv);
            ToWorld(host.lx + du, host.lz + dv, out var wx, out var wz);
            pl.layout.mugSlots.Add(new Slot { x = wx, y = host.Top, z = wz, kind = "mug", room = name, host = host.piece });
        }

        // ------------------------------------------------------------------ queries

        /// <summary>True if (lx, lz) is at least <paramref name="margin"/> away from every piece of furniture and keep-out.</summary>
        public bool IsFree(float lx, float lz, float margin)
        {
            if (lx < -w / 2 + margin || lx > w / 2 - margin || lz < -d / 2 + margin || lz > d / 2 - margin) return false;
            for (int i = 0; i < blocks.Count; i++)
                if (blocks[i].Grow(margin).Contains(lx, lz)) return false;
            for (int i = 0; i < floor.Count; i++)
                if (floor[i].solid && floor[i].rc.Grow(margin).Contains(lx, lz)) return false;
            return true;
        }

        static float[][] spiral;
        static float[][] Spiral()
        {
            if (spiral != null) return spiral;
            var l = new List<float[]>();
            for (int i = -20; i <= 20; i++)
                for (int j = -20; j <= 20; j++)
                    l.Add(new[] { i * 0.25f, j * 0.25f, i * i + j * j });
            l.Sort((a, b) => { int c = a[2].CompareTo(b[2]); if (c != 0) return c; c = a[0].CompareTo(b[0]); return c != 0 ? c : a[1].CompareTo(b[1]); });
            return spiral = l.ToArray();
        }

        /// <summary>A free standing spot as close to (tx, tz) as possible (local), or false if the room is full.</summary>
        public bool FindFree(float tx, float tz, out float fxo, out float fzo, float margin = 0.7f)
        {
            fxo = tx; fzo = tz;
            foreach (var o in Spiral())
            {
                float x = tx + o[0], z = tz + o[1];
                if (IsFree(x, z, margin)) { fxo = x; fzo = z; return true; }
            }
            return false;
        }

        public int FloorCount => floor.Count;

        // ------------------------------------------------------------------ reachability

        /// <summary>Flood fill from the doorways / open sides over the free floor (furniture grown by the walking radius).</summary>
        public ReachGrid ComputeReach(float agent = 0.6f)
        {
            const float res = ReachGrid.Res;
            var g = new ReachGrid { ox = -w / 2, oz = -d / 2, agent = agent };
            g.nx = (int)Math.Ceiling(w / res); g.nz = (int)Math.Ceiling(d / res);
            g.free = new bool[g.nx * g.nz]; g.seen = new bool[g.nx * g.nz];
            for (int i = 0; i < g.free.Length; i++) g.free[i] = true;
            void Mark(Rc rc)
            {
                int x0 = Math.Max(0, (int)Math.Ceiling((rc.x0 - g.ox) / res - 0.5f)), x1 = Math.Min(g.nx - 1, (int)Math.Floor((rc.x1 - g.ox) / res - 0.5f));
                int z0 = Math.Max(0, (int)Math.Ceiling((rc.z0 - g.oz) / res - 0.5f)), z1 = Math.Min(g.nz - 1, (int)Math.Floor((rc.z1 - g.oz) / res - 0.5f));
                for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) g.free[z * g.nx + x] = false;
            }
            for (int i = 0; i < blocks.Count; i++) Mark(blocks[i].Grow(agent));
            for (int i = 0; i < floor.Count; i++) if (floor[i].solid) Mark(floor[i].rc.Grow(agent));
            // real walls: nobody walks within their own radius of one (doorways and gaps, which sit in keeps, excepted)
            for (int z = 0; z < g.nz; z++)
                for (int x = 0; x < g.nx; x++)
                {
                    int k = z * g.nx + x;
                    if (!g.free[k]) continue;
                    float cx = g.ox + (x + 0.5f) * res, cz = g.oz + (z + 0.5f) * res;
                    bool near = (wallOn[0] && cz < -d / 2 + agent) || (wallOn[1] && cz > d / 2 - agent)
                             || (wallOn[2] && cx < -w / 2 + agent) || (wallOn[3] && cx > w / 2 - agent);
                    if (!near) continue;
                    bool inKeep = false;
                    for (int i = 0; i < keep.Count && !inKeep; i++) if (keep[i].Contains(cx, cz)) inKeep = true;
                    if (!inKeep) g.free[k] = false;
                }
            // seeds: doorway approaches, open sides
            var q = new Queue<int>();
            void Seed(int x, int z)
            {
                if (x < 0 || z < 0 || x >= g.nx || z >= g.nz) return;
                int k = z * g.nx + x;
                if (!g.free[k] || g.seen[k]) return;
                g.seen[k] = true; q.Enqueue(k);
            }
            bool any = false;
            for (int z = 0; z < g.nz; z++)
                for (int x = 0; x < g.nx; x++)
                {
                    float cx = g.ox + (x + 0.5f) * res, cz = g.oz + (z + 0.5f) * res;
                    bool seed = false;
                    for (int i = 0; i < keep.Count && !seed; i++) if (keep[i].Contains(cx, cz)) seed = true;
                    if (!seed)
                    {
                        if (!wallOn[0] && cz < -d / 2 + 0.6f) seed = true;
                        else if (!wallOn[1] && cz > d / 2 - 0.6f) seed = true;
                        else if (!wallOn[2] && cx < -w / 2 + 0.6f) seed = true;
                        else if (!wallOn[3] && cx > w / 2 - 0.6f) seed = true;
                    }
                    if (seed) { int before = q.Count; Seed(x, z); if (q.Count > before) any = true; }
                }
            if (!any)
            {
                // a room without doorways (a hub): start in the middle
                int best = -1; float bd = float.MaxValue;
                for (int z = 0; z < g.nz; z++)
                    for (int x = 0; x < g.nx; x++)
                    {
                        if (!g.free[z * g.nx + x]) continue;
                        float cx = g.ox + (x + 0.5f) * res, cz = g.oz + (z + 0.5f) * res;
                        float dd = cx * cx + cz * cz;
                        if (dd < bd) { bd = dd; best = z * g.nx + x; }
                    }
                if (best >= 0) Seed(best % g.nx, best / g.nx);
            }
            while (q.Count > 0)
            {
                int k = q.Dequeue(); int x = k % g.nx, z = k / g.nx;
                Seed(x + 1, z); Seed(x - 1, z); Seed(x, z + 1); Seed(x, z - 1);
            }
            return g;
        }

        int CountReachable(ReachGrid g)
        {
            int n = 0;
            for (int i = 0; i < floor.Count; i++) if (g.SeenNear(floor[i].rc, 0.3f)) n++;
            return n;
        }

        /// <summary>
        /// Like <see cref="Put"/>, but gives up (and removes the prop again) if it would wall off anything that a person
        /// could walk up to before. For crates and other things scattered about.
        /// </summary>
        public Item PutSafe(string key, float lx, float lz, float localYaw)
        {
            var before = ComputeReach();
            int n0 = CountReachable(before);
            var it = Put(key, lx, lz, localYaw);
            if (it == null) return null;
            var after = ComputeReach();
            if (CountReachable(after) < n0 + 1) { RemoveItem(it); return null; }
            return it;
        }

        /// <summary>
        /// Like <see cref="PutSafe"/> but against a wall (see <see cref="Wall"/>): gives up, and removes the prop again, when it would wall
        /// off anything a person could walk up to before - or when nobody could walk up to the prop itself.
        /// </summary>
        public Item WallSafe(char side, string key, float along, float gap = 0.02f)
        {
            var before = ComputeReach();
            int n0 = CountReachable(before);
            var it = Wall(side, key, along, gap);
            if (it == null) return null;
            var after = ComputeReach();
            if (CountReachable(after) < n0 + 1) { RemoveItem(it); return null; }       // (the prop itself counts when a person can reach it)
            return it;
        }

        public void RemoveItem(Item it)
        {
            floor.Remove(it); hides.Remove(it);
            var pc = it.piece;
            if (pc == null) return;
            var L = pl.layout;
            L.pieces.RemoveAll(p => p == pc || p.host == pc);
            L.mugSlots.RemoveAll(s => s.host == pc);
            L.bigSlots.RemoveAll(s => s.host == pc);
        }

        /// <summary>Takes out furniture nobody could walk up to (a desk walled in behind others). Returns how many items went.</summary>
        public static readonly Dictionary<string, int> RemovedKeys = new Dictionary<string, int>();
        public int RemoveUnreachable(ReachGrid g)
        {
            int removed = 0;
            for (int i = floor.Count - 1; i >= 0; i--)
            {
                var it = floor[i];
                if (g.SeenNear(it.rc, 0.3f)) continue;
                // chairs tucked in at a desk and small decor are fine where nobody can reach them
                if (it.def.cat == PropCat.Chair || (it.def.cat == PropCat.Deco && !it.def.Lootable && !it.solid)) continue;
                RemovedKeys.TryGetValue(theme + "/" + it.def.key, out var o); RemovedKeys[theme + "/" + it.def.key] = o + 1;
                RemoveItem(it);
                removed++;
            }
            return removed;
        }

        /// <summary>Drops treasure spots nobody can get to.</summary>
        public void FilterSlots(ReachGrid g)
        {
            var L = pl.layout;
            L.bigSlots.RemoveAll(s => s.room == name && !SlotOk(s, g));
            L.mugSlots.RemoveAll(s => s.room == name && !SlotOk(s, g));
        }

        bool SlotOk(Slot s, ReachGrid g)
        {
            ToLocal(s.x, s.z, out var lx, out var lz);
            return g.Seen(lx, lz, 1.1f);
        }

        /// <summary>A free standing spot near (tx, tz) that a person can really reach.</summary>
        public bool FindFreeReach(ReachGrid g, float tx, float tz, out float fxo, out float fzo, float margin = 0.7f)
        {
            fxo = tx; fzo = tz;
            foreach (var o in Spiral())
            {
                float x = tx + o[0], z = tz + o[1];
                if (IsFree(x, z, margin) && g.Seen(x, z, 0.1f)) { fxo = x; fzo = z; return true; }
            }
            return false;
        }
    }
}
