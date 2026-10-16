using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The office after hours: where the ceiling lights hang and which of them are on. Plain data and arithmetic (no Unity types) so it
    /// can be tested outside Unity. Whole areas have their lights off or on, the way a real office is left at night: the lobby by the
    /// elevator and the server rooms are always lit, some corridors are lit and some are dark (or lit in every other panel), most
    /// offices and meeting rooms are dark, a break room is often left on. Everything follows from the floor's seed and layout, so every
    /// machine builds the same lit and dark areas. <see cref="PanelGeometry"/> turns the panels into mesh data.
    /// </summary>
    public static class AfterHours
    {
        /// <summary>A light panel: 1.2 m x 0.6 m, the long side along <see cref="Panel.alongX"/>.</summary>
        public const float PanelLong = 1.2f, PanelShort = 0.6f;
        /// <summary>The panels keep this far from the walls (the frame is a little bigger than the lens).</summary>
        public const float WallMargin = 0.3f;
        /// <summary>About this far between panels in a room; corridors and lobbies use their own spacing.</summary>
        public const float RoomSpacing = 3.8f, CorridorSpacing = 4.8f, LobbySpacing = 4.4f, OpenSpacing = 5.4f;
        /// <summary>Two real lights of one room are at least this far apart.</summary>
        public const float SourceSpacing = 4.6f;
        /// <summary>At least this share of the panels of a floor is on (more rooms are switched on until it is).</summary>
        public const float MinLitShare = 0.34f;

        /// <summary>What the planner needs to know about a floor.</summary>
        public sealed class Input
        {
            /// <summary>The planned rooms; null for floors without them (the basement, the boss floor, a floor that could not be furnished).</summary>
            public Layout layout;
            public int floor, seed;
            /// <summary>The footprint of the floor (x0, z0, x1, z1) and the size of its cells.</summary>
            public Rc footprint;
            public float cell = 18f;
            /// <summary>Rectangles no panel may hang over (the walls of the elevator shafts).</summary>
            public readonly List<Rc> solids = new List<Rc>();
            /// <summary>Small closed rooms inside the floor (the elevator cars): one panel, always on, in the middle of each.</summary>
            public readonly List<Rc> cars = new List<Rc>();
            /// <summary>Every light is on (the basement and the boss floor).</summary>
            public bool allLit;
            /// <summary>1 = as designed; lower makes more of the floor dark, higher makes more of it lit.</summary>
            public float litScale = 1f;
        }

        public sealed class Panel
        {
            public float x, z;
            public bool alongX, lit;
            /// <summary>Cold white (corridors, server rooms) rather than warm white.</summary>
            public bool cool;
            /// <summary>Index of the room in the layout; -1 for a floor without rooms, -2 for an elevator car.</summary>
            public int room;
        }

        /// <summary>A real light, standing for the panels around it (the lit panels share these; far fewer than panels).</summary>
        public sealed class Source
        {
            public float x, z, range, intensity;
            public bool cool;
            public int room;
        }

        public sealed class Plan
        {
            public readonly List<Panel> panels = new List<Panel>();
            public readonly List<Source> sources = new List<Source>();
            /// <summary>How many rooms there are and how many have at least one panel on.</summary>
            public int rooms, litRooms;

            public int LitPanels { get { int n = 0; foreach (var p in panels) if (p.lit) n++; return n; } }
            public float LitShare => panels.Count == 0 ? 0f : LitPanels / (float)panels.Count;
        }

        // ------------------------------------------------------------------ the plan

        public static Plan Build(Input inp)
        {
            var plan = new Plan();
            if (inp == null) return plan;
            if (inp.layout != null && inp.layout.rooms.Count > 0) PlanRooms(inp, plan);
            else PlanOpenFloor(inp, plan);

            foreach (var car in inp.cars)
            {
                plan.panels.Add(new Panel { x = car.Cx, z = car.Cz, alongX = car.W >= car.D, lit = true, cool = true, room = -2 });
            }
            PlanSources(inp, plan);
            return plan;
        }

        /// <summary>How likely a room of this kind is to have been left lit.</summary>
        public static float LitChance(string theme, int kind)
        {
            switch (theme)
            {
                case "lobby": case "service": case "outpost": return 1f;
                case "hub": return 0.85f;
                case "hall": return 0.6f;
                case "server": return 0.9f;
                case "break": case "canteen": return 0.55f;
                case "cafe_counter": case "cafe_seating": return 0.6f;
                case "office": return 0.25f;
                case "conference": case "meeting": case "training": return 0.2f;
                case "storage": case "copy": return 0.12f;
                case "lounge": case "nook": return 0.3f;
                case "open": case "longopen": case "zone": return 0.4f;
                default: return kind == 2 ? 0.55f : 0.25f;
            }
        }

        static bool IsCool(string theme) => theme == "hub" || theme == "hall" || theme == "lobby" || theme == "service" || theme == "server" || theme == "outpost";

        static void PlanRooms(Input inp, Plan plan)
        {
            var rooms = inp.layout.rooms;
            int n = rooms.Count;
            plan.rooms = n;

            // 1. which rooms are lit
            var lit = new bool[n];
            var roll = new float[n];
            for (int i = 0; i < n; i++)
            {
                roll[i] = Roll(inp.seed, i, 1);
                float chance = Math.Min(1f, LitChance(rooms[i].theme, rooms[i].kind) * Math.Max(0f, inp.litScale));
                lit[i] = rooms[i].theme == "lobby" || rooms[i].theme == "service" || rooms[i].theme == "outpost" || roll[i] < chance;
            }

            // 2. every 18 m cell has a lit junction or room, so no stretch of the floor is dark from end to end
            var cells = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int key = rooms[i].cellY * 1000 + rooms[i].cellX;
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>();
                list.Add(i);
            }
            foreach (var kv in cells)
            {
                bool any = false;
                foreach (int i in kv.Value) if (lit[i]) { any = true; break; }
                if (any) continue;
                int pick = kv.Value[0];
                foreach (int i in kv.Value)               // the junction, or else the biggest corridor / room
                {
                    if (rooms[i].theme == "hub") { pick = i; break; }
                    if (Area(rooms[i]) > Area(rooms[pick])) pick = i;
                }
                lit[pick] = true;
            }

            // 3. the panels of every room
            var all = new List<Panel>[n];
            for (int i = 0; i < n; i++)
            {
                all[i] = RoomPanels(rooms[i], i, inp.solids);
                foreach (var p in all[i]) plan.panels.Add(p);
            }

            // 4. a floor with too few lights on gets more rooms switched on (in the order of their rolls)
            int totalPanels = plan.panels.Count, litPanels = 0;
            for (int i = 0; i < n; i++) if (lit[i]) litPanels += all[i].Count;
            float minShare = MinLitShare * Math.Min(1f, Math.Max(0.25f, inp.litScale));
            if (litPanels < minShare * totalPanels)
            {
                var order = new List<int>();
                for (int i = 0; i < n; i++) if (!lit[i] && all[i].Count > 0) order.Add(i);
                order.Sort((a, b) => { int c = roll[a].CompareTo(roll[b]); return c != 0 ? c : a.CompareTo(b); });
                foreach (int i in order)
                {
                    lit[i] = true; litPanels += all[i].Count;
                    if (litPanels >= minShare * totalPanels) break;
                }
            }

            // 5. inside a lit room: corridors are either lit all along or in every other panel, open areas in patches
            for (int i = 0; i < n; i++)
            {
                var r = rooms[i];
                bool cool = IsCool(r.theme);
                bool alternate = r.kind == 2 && r.theme != "lobby" && r.theme != "service" && Roll(inp.seed, i, 2) < 0.5f;
                for (int k = 0; k < all[i].Count; k++)
                {
                    var p = all[i][k];
                    p.cool = cool;
                    if (!lit[i]) { p.lit = false; continue; }
                    if (inp.allLit) { p.lit = true; continue; }
                    if (alternate) p.lit = k % 2 == 0;
                    else if (r.kind == 1 && all[i].Count > 3) p.lit = Roll(inp.seed, i * 64 + PatchOf(r, p), 3) < 0.65f || k == 0;
                    else p.lit = true;
                }
                if (lit[i]) plan.litRooms++;
            }
        }

        /// <summary>Open-plan areas are lit in patches of about 7 m: which patch is this panel in?</summary>
        static int PatchOf(RoomInfo r, Panel p)
        {
            int px = (int)Math.Floor((p.x - r.bounds.x0) / 7f), pz = (int)Math.Floor((p.z - r.bounds.z0) / 7f);
            return px * 8 + pz + 1;
        }

        static float Area(RoomInfo r) => r.bounds.W * r.bounds.D;

        /// <summary>The panels of one room: a grid (corridors: a line down the middle), kept clear of the walls and of the solids.</summary>
        static List<Panel> RoomPanels(RoomInfo r, int index, List<Rc> solids)
        {
            var res = new List<Panel>();
            var b = r.bounds;
            bool ring = r.theme == "lobby" || r.theme == "service";
            bool alongX = b.W >= b.D;
            float longLen = alongX ? b.W : b.D, shortLen = alongX ? b.D : b.W;
            int nLong, nShort;
            if (r.kind == 2 && !ring && shortLen < 6.5f)
            {
                nLong = Math.Max(1, (int)Math.Round(longLen / CorridorSpacing));
                nShort = 1;
            }
            else
            {
                float s = ring ? LobbySpacing : r.kind == 1 ? RoomSpacing + 0.4f : RoomSpacing;
                nLong = Math.Max(1, (int)Math.Round(longLen / s));
                nShort = Math.Max(1, (int)Math.Round(shortLen / s));
            }
            for (int i = 0; i < nLong; i++)
                for (int j = 0; j < nShort; j++)
                {
                    float u = (i + 0.5f) / nLong, v = (j + 0.5f) / nShort;
                    float x = alongX ? b.x0 + u * b.W : b.x0 + v * b.W;
                    float z = alongX ? b.z0 + v * b.D : b.z0 + u * b.D;
                    var p = new Panel { x = x, z = z, alongX = alongX, room = index };
                    if (!Fits(p, b, solids, r.blocks)) continue;
                    res.Add(p);
                }
            if (res.Count == 0)
            {
                // a room too small for the grid to fit: one panel in the middle if it can take it at all
                var p = new Panel { x = b.Cx, z = b.Cz, alongX = alongX, room = index };
                if (Fits(p, b, solids, r.blocks)) res.Add(p);
            }
            return res;
        }

        /// <summary>The footprint of a panel's frame.</summary>
        public static Rc FrameRect(Panel p)
        {
            float hx = (p.alongX ? PanelLong : PanelShort) * 0.5f + 0.05f, hz = (p.alongX ? PanelShort : PanelLong) * 0.5f + 0.05f;
            return new Rc(p.x - hx, p.z - hz, p.x + hx, p.z + hz);
        }

        static bool Fits(Panel p, Rc room, List<Rc> solids, List<Rc> blocks = null)
        {
            var f = FrameRect(p);
            if (f.x0 < room.x0 + WallMargin || f.x1 > room.x1 - WallMargin || f.z0 < room.z0 + WallMargin || f.z1 > room.z1 - WallMargin) return false;
            var g = f.Grow(0.3f);
            for (int i = 0; i < solids.Count; i++) if (g.Overlaps(solids[i])) return false;
            if (blocks != null) for (int i = 0; i < blocks.Count; i++) if (g.Overlaps(blocks[i])) return false;      // (the room's own blocks: the outpost inside its ring, the elevator alcove inside the outpost)
            return true;
        }

        /// <summary>A floor without rooms: a grid of panels all over it.</summary>
        static void PlanOpenFloor(Input inp, Plan plan)
        {
            var f = inp.footprint;
            int nx = Math.Max(1, (int)Math.Round(f.W / OpenSpacing)), nz = Math.Max(1, (int)Math.Round(f.D / OpenSpacing));
            plan.rooms = 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    var p = new Panel { x = f.x0 + (i + 0.5f) * f.W / nx, z = f.z0 + (j + 0.5f) * f.D / nz, alongX = true, room = -1, cool = true };
                    if (!Fits(p, f, inp.solids)) continue;
                    // a floor that could not be furnished still gets a night look: patches of about two cells
                    p.lit = inp.allLit || Roll(inp.seed, (int)(p.x / (inp.cell * 0.5f)) * 100 + (int)(p.z / (inp.cell * 0.5f)), 4) < 0.55f * Math.Max(0f, inp.litScale);
                    plan.panels.Add(p);
                }
            if (plan.LitPanels > 0) plan.litRooms = 1;
        }

        // ------------------------------------------------------------------ the real lights

        static void PlanSources(Input inp, Plan plan)
        {
            var byRoom = new Dictionary<int, List<Source>>();
            foreach (var p in plan.panels)
            {
                if (!p.lit) continue;
                if (!byRoom.TryGetValue(p.room, out var mine)) byRoom[p.room] = mine = new List<Source>();
                bool car = p.room == -2;
                float spacing = inp.allLit || p.room == -1 ? SourceSpacing + 2.4f : SourceSpacing;
                bool near = false;
                foreach (var s in mine)
                {
                    float dx = s.x - p.x, dz = s.z - p.z;
                    if (dx * dx + dz * dz < spacing * spacing) { near = true; break; }
                }
                if (near) continue;
                var src = new Source
                {
                    x = p.x, z = p.z, cool = p.cool, room = p.room,
                    range = car ? 7.5f : (inp.allLit ? 10.5f : 9f),
                    intensity = car ? 2.2f : (inp.allLit ? 2.4f : 2.6f)
                };
                mine.Add(src);
                plan.sources.Add(src);
            }
        }

        // ------------------------------------------------------------------ a fixed dice

        /// <summary>A number from 0 up to (not including) 1 that depends only on its arguments (the same on every machine).</summary>
        public static float Roll(int seed, int a, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= (uint)(a + 1) * 40503u + 0x9E3779B9u;
                h = (h << 13) | (h >> 19);
                h *= 2246822519u;
                h ^= (uint)(salt + 7) * 3266489917u;
                h ^= h >> 15;
                h *= 2654435769u;
                h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    /// <summary>
    /// The mesh of a floor's panels as plain numbers: a frame that hangs flush under the ceiling (a box without a top) and a lens in it, which
    /// is dark (off), warm white or cold white (on). Four triangle lists, one per material. All faces are wound so the side that should
    /// be seen is the front (checked by the tests).
    /// </summary>
    public sealed class PanelGeometry
    {
        public const int Frame = 0, Off = 1, OnWarm = 2, OnCool = 3;
        /// <summary>How thick the frame is, and how far the lens hangs below the frame's underside.</summary>
        public const float FrameThickness = 0.06f, LensDrop = 0.003f;

        /// <summary>x, y, z of every vertex, flattened; <see cref="normals"/> likewise.</summary>
        public readonly List<float> vertices = new List<float>();
        public readonly List<float> normals = new List<float>();
        public readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        public int VertexCount => vertices.Count / 3;

        /// <summary>Adds a panel whose frame is flush under the ceiling at height <paramref name="ceilingY"/>.</summary>
        public void Add(AfterHours.Panel p, float ceilingY)
        {
            float hx = (p.alongX ? AfterHours.PanelLong : AfterHours.PanelShort) * 0.5f, hz = (p.alongX ? AfterHours.PanelShort : AfterHours.PanelLong) * 0.5f;
            float fx = hx + 0.05f, fz = hz + 0.05f;
            float top = ceilingY, bottom = ceilingY - FrameThickness;

            // frame: four sides and the underside
            Face(Frame, p.x - fx, top, p.z - fz, p.x + fx, top, p.z - fz, p.x + fx, bottom, p.z - fz, p.x - fx, bottom, p.z - fz, 0, 0, -1);   // south
            Face(Frame, p.x + fx, top, p.z + fz, p.x - fx, top, p.z + fz, p.x - fx, bottom, p.z + fz, p.x + fx, bottom, p.z + fz, 0, 0, 1);    // north
            Face(Frame, p.x - fx, top, p.z + fz, p.x - fx, top, p.z - fz, p.x - fx, bottom, p.z - fz, p.x - fx, bottom, p.z + fz, -1, 0, 0);   // west
            Face(Frame, p.x + fx, top, p.z - fz, p.x + fx, top, p.z + fz, p.x + fx, bottom, p.z + fz, p.x + fx, bottom, p.z - fz, 1, 0, 0);    // east
            Face(Frame, p.x - fx, bottom, p.z - fz, p.x + fx, bottom, p.z - fz, p.x + fx, bottom, p.z + fz, p.x - fx, bottom, p.z + fz, 0, -1, 0);   // underside

            // lens
            float y = bottom - LensDrop;
            Face(p.lit ? (p.cool ? OnCool : OnWarm) : Off,
                 p.x - hx, y, p.z - hz, p.x + hx, y, p.z - hz, p.x + hx, y, p.z + hz, p.x - hx, y, p.z + hz, 0, -1, 0);
        }

        /// <summary>
        /// A quad given by its four corners in order and the direction it must face; the corners are put in whichever order makes that side the front
        /// (Unity draws the side from which the corners run clockwise).
        /// </summary>
        void Face(int material, float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, float dx, float dy, float dz,
                  float nx, float ny, float nz)
        {
            // the cross product of the first two edges points at the viewer for whom a, b, c run clockwise
            float ux = bx - ax, uy = by - ay, uz = bz - az, vx = cx - ax, vy = cy - ay, vz = cz - az;
            float qx = uy * vz - uz * vy, qy = uz * vx - ux * vz, qz = ux * vy - uy * vx;
            bool flip = qx * nx + qy * ny + qz * nz < 0f;

            int i = VertexCount;
            AddVertex(ax, ay, az, nx, ny, nz); AddVertex(bx, by, bz, nx, ny, nz); AddVertex(cx, cy, cz, nx, ny, nz); AddVertex(dx, dy, dz, nx, ny, nz);
            var t = triangles[material];
            if (!flip) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        void AddVertex(float x, float y, float z, float nx, float ny, float nz)
        {
            vertices.Add(x); vertices.Add(y); vertices.Add(z);
            normals.Add(nx); normals.Add(ny); normals.Add(nz);
        }
    }
}
