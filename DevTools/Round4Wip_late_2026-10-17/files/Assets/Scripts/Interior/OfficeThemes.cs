using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// The furnishing of each kind of room. Every theme works in the room's canonical frame (door on the south wall).
    /// Since 2026-10-17 night the props are the Synty office pack's, which are bigger than the old ones. The owner: "The layout you had previously of the floors with the old builds was perfect, just with
    /// updated assets would be great": every room keeps its old arrangement (what stands where, how many chairs sit where); only the numbers follow the new sizes, never a number that belonged to an old prop
    /// (a chair stands half its own depth from a table's edge, a counter run advances by the width of what stands in it, ...).
    /// </summary>
    public sealed partial class OfficePlanner
    {
        // ------------------------------------------------------------------ small helpers

        static void Fwd(float yaw, out float fx, out float fz)
        {
            int q = (((int)Math.Round(yaw / 90f)) % 4 + 4) % 4;
            RoomCtx.RotQ(q, 0f, 1f, out fx, out fz);
        }

        float Rnd(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Chance(double p) => rng.NextDouble() < p;
        T OneOf<T>(params T[] xs) => xs[rng.Next(xs.Length)];

        static float Dw(string key) => PropTable.Get(key).W;
        static float Dd(string key) => PropTable.Get(key).D;

        /// <summary>How far beyond the edge of a table or desk the middle of a chair stands when it is tucked in: half its own depth, less 12 cm that go under the edge.</summary>
        static float Tuck(string chair) => PropTable.Get(chair).D / 2f - 0.12f;

        /// <summary>A picture for a wall: the pack has posters, certificates, a calendar and a child's drawing (the old rooms had two paintings).</summary>
        string Art(bool big) => big ? OneOf("painting", "painting", "poster_3", "poster_stocks", "poster_1") : OneOf("painting_small", "certificate", "child_art", "calendar");

        /// <summary>Hangs a picture on a wall with its middle at the given height (Mount takes the underside).</summary>
        Item Hang(RoomCtx r, char side, string key, float along, float middle = 1.7f) => r.Mount(side, key, along, middle - PropTable.Get(key).H * 0.5f);

        string PlantKey(bool big) => big ? OneOf("plant_big", "plant_big", "plant_tall", "plant_floor") : OneOf("plant_small", "plant_pot", "plant_med");

        /// <summary>First of the keys that fits against the wall at that spot.</summary>
        Item TryWall(RoomCtx r, char side, string[] keys, float along)
        {
            foreach (var k in keys)
            {
                var it = r.Wall(side, k, along);
                if (it != null) return it;
            }
            return null;
        }

        /// <summary>A desk with a chair, screen, keyboard and bits of paper. Its front (the user's side) faces <paramref name="yaw"/>.</summary>
        Item Station(RoomCtx r, float lx, float lz, float yaw, string deskKey, bool chair = true, double lootBase = 1.0)
        {
            var desk = r.Put(deskKey, lx, lz, yaw);
            if (desk == null) return null;
            float dw = desk.def.W, dd = desk.def.D;
            Fwd(yaw, out var fx, out var fz);
            if (chair)
            {
                string ck = OneOf("chair_office", "chair_std", "chair_office");
                float off = dd / 2 + Tuck(ck);
                r.Put(ck, lx + fx * off, lz + fz * off, yaw + 180f);
            }
            float back = -dd / 2 + 0.2f;                                   // the screen stands at the back edge, the keyboard in front of it
            float mu = deskKey == "desk_3" ? -0.5f : -0.15f;
            var mon = r.Top(desk, Chance(0.3) ? "monitor_old" : "monitor_new", mu, back);
            r.Loot(mon, 0.30 * lootBase);
            r.Loot(r.Top(desk, "keyboard", mu, 0.10f), 0.12 * lootBase);
            r.Top(desk, "mouse", mu + 0.45f, 0.10f);
            if (Chance(0.55)) r.Loot(r.Top(desk, "phone", dw / 2 - 0.32f, -0.12f, 0f), 0.35 * lootBase);
            if (Chance(0.55)) r.Top(desk, "papers", -dw / 2 + 0.30f, -0.05f, OneOf(0f, 90f));
            if (Chance(0.35)) r.Top(desk, "pencil_holder", -dw / 2 + 0.15f, -dd / 2 + 0.13f);
            if (Chance(0.35)) r.Loot(r.Top(desk, "notepad", dw / 2 - 0.36f, 0.26f, OneOf(0f, 15f, -15f)), 0.3 * lootBase);
            if (Chance(0.30)) r.Loot(r.Top(desk, "stapler", dw / 2 - 0.06f, 0.20f, 0f), 0.4 * lootBase);
            if (Chance(0.30)) r.MugSlot(desk, -dw / 2 + 0.55f, 0.28f);
            return desk;
        }

        /// <summary>
        /// Two rows of workstations back to back with a divider, like a cubicle cluster (the pack's own cubicle walls: the spine, the two ends, a half wall between neighbours). The long axis
        /// runs along local x. Does nothing when the cluster does not fit.
        /// </summary>
        void Island(RoomCtx r, float cx, float cz, int nx, string deskKey = "desk_1")
        {
            nx = Math.Min(nx, 2);                                           // (a pod is at most two desks across: natural walkways round it, the owner: "some of the desks are too close together")
            var def = PropTable.Get(deskKey);
            float dw = def.W, dd = def.D;
            float pitch = Math.Max(dw, Dw("cube_wall")) + 0.02f;           // one cubicle wall of the spine per desk
            float len = nx * pitch;
            float gap = Dd("cube_wall") + 0.04f;                           // the spine between the two rows of desks
            if (!r.CanPlaceRect(new Rc(cx - len / 2 - 0.08f, cz - 1.0f, cx + len / 2 + 0.08f, cz + 1.0f))) return;
            for (int i = 0; i < nx; i++) r.Put("cube_wall", cx - len / 2 + pitch / 2 + i * pitch, cz, 0f);
            for (int e = -1; e <= 1; e += 2) r.Put("cube_wall", cx + e * (len / 2 + 0.04f), cz, 90f);
            for (int i = 1; i < nx; i++)
            {
                float x = cx - len / 2 + i * pitch;
                for (int side = -1; side <= 1; side += 2) r.Put("cube_wall_half", x, cz + side * (gap / 2 + 0.5f), 90f);
            }
            for (int side = 1; side >= -1; side -= 2)
                for (int i = 0; i < nx; i++)
                {
                    float x = cx - len / 2 + pitch / 2 + i * pitch;
                    float z = cz + side * (gap / 2 + dd / 2);
                    Station(r, x, z, side > 0 ? 0f : 180f, deskKey);
                }
        }

        void Plant(RoomCtx r, char side, float along)
        {
            r.Wall(side, PlantKey(Chance(0.45)), along);
        }

        /// <summary>Plants in the four corners that fit.</summary>
        void CornerPlants(RoomCtx r, double chance, bool bigOnes = false)
        {
            float hx = r.w / 2, hz = r.d / 2;
            string Key() => PlantKey(bigOnes);
            void Corner(float sx, float sz, bool a, bool b)
            {
                if (!(a && b) || !Chance(chance)) return;
                string key = Key();
                float m = PropTable.Get(key).W / 2 + 0.03f;
                r.Put(key, sx * (hx - m), sz * (hz - m), 0f);
            }
            Corner(-1, 1, r.HasWall('N'), r.HasWall('W'));
            Corner(1, 1, r.HasWall('N'), r.HasWall('E'));
            Corner(-1, -1, r.HasWall('S'), r.HasWall('W'));
            Corner(1, -1, r.HasWall('S'), r.HasWall('E'));
        }

        void WallArt(RoomCtx r, char side, float from, float to, float step, double chance = 0.8)
        {
            for (float a = from; a <= to; a += step)
                if (Chance(chance))
                {
                    if (Chance(0.7)) Hang(r, side, Art(true), a, 1.7f);
                    else Hang(r, side, Art(false), a, 1.7f);
                }
        }

        // ------------------------------------------------------------------ the building-safety kit (the owner, 2026-10-17 night: "signs for the stairs, some fire alarms placed, some emergency hatchets placed, some fire extinguishers placed, etc")

        /// <summary>
        /// A corridor gets, now and then, a fire extinguisher on its wall (1.0 m up, as the codes want: within about 23 m of walking from anywhere), a fire alarm pull station beside it and once in a while an emergency
        /// axe in its case. The stairwells and the elevator lobby have their own kit (<see cref="StairKit"/>, <see cref="ThLobby"/>).
        /// </summary>
        void CorridorKit(RoomCtx r)
        {
            float len = r.longHall ? r.w : r.d;
            int count = r.longHall ? 2 : 1;
            for (int n = 0; n < count; n++)
            {
                if (!Chance(r.longHall ? 0.9 : 0.5)) continue;
                for (int tries = 0; tries < 8; tries++)
                {
                    char side = r.longHall ? OneOf('S', 'N') : OneOf('W', 'E');
                    if (!r.HasWall(side)) continue;
                    float a = r.longHall ? (n == 0 ? -1f : 1f) * Rnd(2.5f, 6.5f) : Rnd(-len / 2 + 1.3f, len / 2 - 1.3f);
                    if (r.Mount(side, "extinguisher", a, 0.95f, 0f, KitClear) == null) continue;
                    if (Chance(0.45)) r.Mount(side, "fire_alarm", a + 0.5f, 1.0f, 0f, KitClear);
                    else if (Chance(0.25)) r.Mount(side, "fire_alarm", a - 0.5f, 1.0f, 0f, KitClear);
                    if (Chance(0.14)) r.Mount(side, "emergency_axe", a - 1.0f, 0.95f, 0f, KitClear);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ dispatch

        void Furnish(RoomCtx r)
        {
            switch (r.theme)
            {
                case "hub": ThHub(r); break;
                case "hall": if (r.longHall) ThLongHall(r); else ThHall(r); break;
                case "office": ThOffice(r); break;
                case "conference": case "meeting": ThConference(r); break;
                case "break": ThBreak(r); break;
                case "storage": ThStorage(r); break;
                case "copy": ThCopy(r); break;
                case "server": ThServer(r); break;
                case "lounge": ThLounge(r); break;
                case "training": ThTraining(r); break;
                case "open": ThOpen(r); break;
                case "nook": ThNook(r); break;
                case "longopen": ThLongOpen(r); break;
                case "canteen": ThCanteen(r); break;
                case "cafe_counter": ThCafeCounter(r); break;
                case "cafe_seating": ThCafeSeating(r); break;
                case "lobby": ThLobby(r); break;
                case "service": ThService(r); break;
                case "outpost": ThOutpost(r); break;
                case "restroom": break;                              // (nobody goes in: nothing is furnished)
            }
        }

        static int HideNeed(RoomCtx r) => r.theme == ThemeRestroom ? 0 : r.kind == 0 ? 2 : r.kind == 1 ? 3 : (r.isRing ? 3 : 0);

        /// <summary>
        /// Final pass over a furnished room: furniture nobody can walk up to goes, every room gets enough places to
        /// duck behind (that a person can actually reach), and treasure spots nobody can get to are dropped.
        /// </summary>
        public static readonly Dictionary<string, int> Stats = new Dictionary<string, int>();
        static void Stat(string k, int n) { if (n == 0) return; Stats.TryGetValue(k, out var o); Stats[k] = o + n; }

        void Settle(RoomCtx r)
        {
            var g = r.ComputeReach();
            Stat(r.theme + ".rooms", 1);
            Stat(r.theme + ".removed", r.RemoveUnreachable(g));
            int need = HideNeed(r);
            for (int pass = 0; pass < 3 && r.hides.Count < need; pass++)
            {
                g = r.ComputeReach();
                Stat(r.theme + ".addhides", 1);
                AddHides(r, g, need);
                g = r.ComputeReach();
                Stat(r.theme + ".removed2", r.RemoveUnreachable(g));
            }
            if (r.hides.Count < need) Stat(r.theme + ".SHORT", 1);
            r.reach = r.ComputeReach();
            r.FilterSlots(r.reach);
        }

        /// <summary>Adds cabinets / shelves along the walls (or a free-standing screen) until the room has enough hiding places.</summary>
        void AddHides(RoomCtx r, ReachGrid g, int need)
        {
            string[] keys = { "cabinet_med", "bookshelf", "cabinet_med", "shelf", "cabinet_small" };
            char[] sides = { 'N', 'E', 'W', 'S' };
            for (int guard = 0; guard < 14 && r.hides.Count < need; guard++)
            {
                string key = keys[guard % keys.Length];
                bool placed = false;
                foreach (char side in sides)
                {
                    if (!r.HasWall(side)) continue;
                    float len = (side == 'N' || side == 'S') ? r.w : r.d;
                    for (float a = -len / 2 + 0.5f; a <= len / 2 - 0.5f; a += 0.4f)
                    {
                        var it = r.Wall(side, key, a);
                        if (it == null) continue;
                        if (!g.SeenNear(it.rc, 0.3f)) { r.RemoveItem(it); continue; }
                        placed = true; break;
                    }
                    if (placed) break;
                }
                if (placed) continue;
                // no wall space left: a free-standing screen
                for (float x = -r.w / 2 + 0.6f; x <= r.w / 2 - 0.6f && !placed; x += 0.5f)
                    for (float z = r.d / 2 - 0.5f; z >= -r.d / 2 + 0.5f && !placed; z -= 0.5f)
                    {
                        var it = r.Panel(x, z, 1.4f, 0.06f, 'x', 1.6f);
                        if (it == null) continue;
                        if (!g.SeenNear(it.rc, 0.3f)) { r.RemoveItem(it); continue; }
                        placed = true;
                    }
                if (!placed) break;
            }
        }

        // ------------------------------------------------------------------ corridors

        void ThHub(RoomCtx r)
        {
            // the junction stays open; just a plant in each corner of the square (against the partition corners)
            float h = r.w / 2 - 0.25f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    if (Chance(0.55)) r.Put(PlantKey(Chance(0.5)), sx * (h - 0.1f), sz * (h - 0.1f), 0f);
        }

        void ThHall(RoomCtx r)
        {
            // r.w = across (4.4 or 4.0), r.d = outward from the junction
            if (r.openLane) { HallDecorOpen(r); return; }
            foreach (char side in new[] { 'W', 'E' })
            {
                if (!r.HasWall(side)) continue;
                float z = -r.d / 2 + 0.9f;
                int run = 0;
                while (z < r.d / 2 - 0.6f && run < 12)
                {
                    run++;
                    int kind = rng.Next(10);
                    Item it = null; float used = 0.8f;
                    if (kind <= 1) { it = r.Wall(side, PlantKey(true), z + 0.4f); used = 0.9f; }
                    else if (kind == 2) { it = r.Wall(side, "water", z + 0.25f); used = 0.6f; r.Loot(it, 0.5); }
                    else if (kind == 3) { it = r.Wall(side, "sofa1", z + 0.62f); used = 1.3f; }
                    else if (kind == 4) { it = r.Wall(side, "cabinet_med", z + 0.3f); used = 0.7f; }
                    else if (kind == 5) { it = r.Wall(side, "bookshelf", z + 0.6f); used = 1.3f; }
                    else if (kind == 6) { it = r.Wall(side, "trash_bin", z + 0.2f); used = 0.5f; }
                    else if (kind == 7) { it = r.Wall(side, PlantKey(false), z + 0.2f); used = 0.5f; }
                    if (it != null && it.def.key == "sofa1" && Chance(0.5)) r.Wall(side, PlantKey(false), z + 1.45f);
                    z += used + Rnd(0.5f, 1.4f);
                }
                WallArt(r, side, -r.d / 2 + 1.5f, r.d / 2 - 0.9f, 2.3f, 0.75);
            }
            if (!r.endGap && r.HasWall('N'))
            {
                // dead end: a bench or a cooler and a big painting
                if (Chance(0.5)) { r.Wall('N', "sofa2", 0f); Hang(r, 'N', Art(true), 0f, 1.7f); }
                else { r.Wall('N', "water", -0.6f); r.Wall('N', PlantKey(true), 0.8f); r.Mount('N', "clock", 0f, 2.0f); }
            }
            else if (r.HasWall('N'))
            {
                r.Mount('N', "ac_unit", -1.9f, 2.7f);
            }
            CorridorKit(r);
        }

        void HallDecorOpen(RoomCtx r)
        {
            // an arm of an open-plan hall: nothing beside it, so only the end wall
            if (!r.endGap && r.HasWall('N'))
            {
                r.Wall('N', PlantKey(true), -1.0f); r.Wall('N', PlantKey(true), 1.0f);
                Hang(r, 'N', Art(true), 0f, 1.7f);
            }
        }

        void ThLongHall(RoomCtx r)
        {
            // the corridor of a split cell: w = 17.6 along the corridor, d = 4.4 across; both long sides are partitions
            foreach (char side in new[] { 'S', 'N' })
            {
                float x = -r.w / 2 + 0.6f;
                int run = 0;
                while (x < r.w / 2 - 0.6f && run < 40)
                {
                    run++;
                    int kind = rng.Next(10);
                    Item it = null; float used = 0.8f;
                    if (kind <= 1) { it = r.Wall(side, PlantKey(true), x + 0.4f); used = 0.9f; }
                    else if (kind == 2) { it = r.Wall(side, "water", x + 0.25f); used = 0.6f; r.Loot(it, 0.5); }
                    else if (kind == 3) { it = r.Wall(side, "sofa2", x + 1.0f); used = 2.1f; }
                    else if (kind == 4) { it = r.Wall(side, "cabinet_med", x + 0.3f); used = 0.7f; }
                    else if (kind == 5) { it = r.Wall(side, "trash_bin", x + 0.2f); used = 0.5f; }
                    x += used + Rnd(0.6f, 1.6f);
                }
                WallArt(r, side, -r.w / 2 + 1.5f, r.w / 2 - 1.5f, 2.6f, 0.7);
            }
            if (!r.endGapA && r.HasWall('W')) { r.Wall('W', PlantKey(true), 0f); Hang(r, 'W', Art(true), 0f, 1.7f); }
            if (!r.endGapB && r.HasWall('E')) { r.Wall('E', PlantKey(true), 0f); Hang(r, 'E', Art(true), 0f, 1.7f); }
            CorridorKit(r);
        }

        // ------------------------------------------------------------------ offices

        void ThOffice(RoomCtx r)
        {
            bool bigRoom = r.w >= 5f && r.d >= 5f;
            if (bigRoom) ExecOffice(r);
            else if (r.w >= r.d) RowOffice(r);
            else NarrowOffice(r);
            CornerPlants(r, 0.5);
        }

        void ExecOffice(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // the credenza stands against the far wall, the desk 1.1 m in front of it: the executive sits between them and looks at the door
            float credD = Dd("credenza");
            float deskD = Dd("desk_3");
            float zd = hz - credD - 1.1f - deskD / 2;
            var cred = r.Wall('N', "credenza", 0.4f);
            var desk = r.Put("desk_3", 0.4f, zd, 0f);
            if (desk != null)
            {
                float dd = desk.def.D;
                r.Put("chair_exec", 0.4f, zd + dd / 2 + Tuck("chair_exec"), 180f);
                r.Put("chair_modern", -0.35f, zd - dd / 2 - 0.2f - Dd("chair_modern") / 2, 0f);
                r.Put("chair_modern", 1.15f, zd - dd / 2 - 0.2f - Dd("chair_modern") / 2, 0f);
                r.Loot(r.Top(desk, "monitor_new", -0.55f, -dd / 2 + 0.2f), 0.4);
                r.Top(desk, "keyboard", -0.55f, 0.10f);
                r.Top(desk, "mouse", -0.05f, 0.10f);
                r.Loot(r.Top(desk, "phone", 0.65f, -0.12f), 0.4);
                r.Top(desk, "notepad", 0.35f, 0.26f, 10f);
                r.Top(desk, "pencil_holder", -0.95f, -dd / 2 + 0.14f);
                if (Chance(0.6)) { var b = r.Top(desk, "briefcase", 0.05f, -0.10f); if (b != null) { b.piece.loot = true; } }
                r.MugSlot(desk, 0.95f, 0.2f);
                if (Chance(0.6)) r.BigSlot("stapler", 0.4f + 0.85f, zd + 0.1f, desk.Top);
            }
            if (cred != null)
            {
                r.Top(cred, "name_plate", -0.45f, 0f, 180f);
                r.Top(cred, "photo", 0.1f, 0f);
                r.BigSlot("trophy", cred.lx + 0.45f, cred.lz, cred.Top);
            }
            // bookshelves along the west wall, cabinets under the painting
            r.Wall('W', "bookshelf", hz - 1.2f); r.Wall('W', "bookshelf", hz - 2.5f); r.Wall('W', "cabinet_med", hz - 3.4f);
            if (r.Wall('W', "bookshelf", hz - 4.4f) == null) r.Wall('W', "cabinet_small", hz - 4.2f);
            // a sofa with a coffee table on the east wall
            var sofa = r.Wall('E', OneOf("sofa2", "sofa2", "sofa1"), -0.6f);
            if (sofa != null)
            {
                r.Put("coffee_table", hx - sofa.def.D - 0.35f - Dd("coffee_table") / 2, -0.6f, 90f);
                r.Mount('E', "clock", -0.6f, 2.1f);
            }
            r.Wall('E', PlantKey(true), 0.9f);
            Hang(r, 'N', "painting", 0.4f, 1.9f);
            if (Chance(0.5)) { r.Reserve(hx - 0.95f, hz - 0.9f, 1.5f, 1.5f); r.BigSlot("safe", hx - 0.95f, hz - 0.9f); }
            Hang(r, 'S', Art(false), -hx + 1.2f, 1.7f);
        }

        void RowOffice(RoomCtx r)
        {
            // wide and shallow: the desk sits against the north wall, the worker faces it
            float hx = r.w / 2, hz = r.d / 2;
            var desk = r.Wall('N', OneOf("desk_2", "desk_1", "desk_3"), Rnd(-0.6f, 0.9f));
            if (desk != null)
            {
                float dd = desk.def.D, dw = desk.def.W;
                r.Put("chair_office", desk.lx, desk.lz - dd / 2 - Tuck("chair_office"), 0f);
                r.Loot(r.Top(desk, "monitor_new", -0.15f, -dd / 2 + 0.2f), 0.35);
                r.Top(desk, "keyboard", -0.15f, 0.10f);
                r.Top(desk, "mouse", 0.30f, 0.10f);
                r.Loot(r.Top(desk, "phone", dw / 2 - 0.32f, -0.12f), 0.3);
                r.Top(desk, "papers", -dw / 2 + 0.3f, -0.05f);
                r.MugSlot(desk, -dw / 2 + 0.55f, 0.28f);
                Hang(r, 'N', Art(true), desk.lx, 2.1f);
            }
            r.Wall('W', "bookshelf", 0.0f);
            r.Wall('E', "cabinet_med", -0.2f); r.Wall('E', "cabinet_med", 0.4f);
            r.Wall('S', Chance(0.5) ? "bookshelf" : "shelf", -hx + 0.8f);
            r.Wall('S', PlantKey(true), hx - 0.6f);
            if (r.w > 5.5f) { r.Put("table_box", hx - 1.6f, 0.0f, 0f); r.Put("chair_simple", hx - 1.6f, 0.65f + Tuck("chair_simple"), 180f); }
        }

        void NarrowOffice(RoomCtx r)
        {
            // narrow and deep: the desk at the far end, shelves along the sides
            float hx = r.w / 2, hz = r.d / 2;
            var desk = r.Wall('N', "desk_2", 0.0f);
            if (desk != null)
            {
                float dd = desk.def.D, dw = desk.def.W;
                r.Put("chair_office", 0f, desk.lz - dd / 2 - Tuck("chair_office"), 0f);
                r.Loot(r.Top(desk, "monitor_new", -0.15f, -dd / 2 + 0.2f), 0.35);
                r.Top(desk, "keyboard", -0.15f, 0.10f);
                r.Top(desk, "mouse", 0.30f, 0.10f);
                r.Loot(r.Top(desk, "phone", dw / 2 - 0.32f, -0.12f), 0.3);
                r.MugSlot(desk, -dw / 2 + 0.55f, 0.28f);
                Hang(r, 'N', Art(true), 0f, 2.1f);
            }
            r.Wall('W', "bookshelf", 0.2f);
            r.Wall('E', "cabinet_med", 0.5f); r.Wall('E', "cabinet_med", -0.2f);
            r.Wall('W', "bookshelf", -1.2f);
            r.Wall('E', PlantKey(true), -1.3f);
        }

        // ------------------------------------------------------------------ meeting rooms

        void ThConference(RoomCtx r)
        {
            if (r.HasWall('N') && r.w >= 5.8f && r.d >= 5.8f) { ConferenceWide(r); return; }
            float hx = r.w / 2, hz = r.d / 2;
            float tz = 0.1f;
            // a narrow room: the (narrower) table lies along the room, the people sit along its long sides and one at each head
            var t = r.Put("conf_table2", 0f, tz, 90f);
            if (t != null)
            {
                float tw = t.rc.W, td = t.rc.D;                              // across and along (local footprint)
                int perSide = Math.Max(2, (int)Math.Floor(td / 1.0f) - 1);
                for (int k = 0; k < perSide; k++)
                {
                    float z = tz + (k - (perSide - 1) / 2f) * 1.0f;
                    r.Put("chair_modern", -(tw / 2 + Tuck("chair_modern")), z, 90f);
                    r.Put("chair_modern", tw / 2 + Tuck("chair_modern"), z, 270f);
                }
                r.Put("chair_office", 0f, tz + td / 2 + Tuck("chair_office"), 180f);
                r.Put("chair_modern", 0f, tz - td / 2 - Tuck("chair_modern"), 0f);
                var proj = r.Top(t, "projector", 0f, 0f); r.Loot(proj, 0.8);
                r.Loot(r.Top(t, "notepad", -0.3f, -0.7f, 20f), 0.3);
                r.Loot(r.Top(t, "notepad", 0.3f, 0.6f, -10f), 0.3);
                r.Top(t, "cup", 0.25f, -0.4f);
                r.MugSlot(t, -0.2f, 0.9f);
            }
            if (r.HasWall('N'))
            {
                var cred = r.Wall('N', "credenza", 0f);
                if (cred != null)
                {
                    r.BigSlot("trophy", cred.lx + 0.45f, cred.lz, cred.Top);
                    r.Top(cred, "cup", -0.5f, 0f);
                    r.Mount('N', "wall_tv", 0f, 1.5f);
                }
                else r.Mount('N', "wall_tv", 0f, 1.5f);
            }
            if (r.HasWall('W')) r.Mount('W', "whiteboard", 0f, 1.0f);
            if (r.HasWall('E')) Hang(r, 'E', Art(true), 0f, 1.7f);
            CornerPlants(r, 0.8, true);
        }

        /// <summary>A big meeting room: the table lies across the room, so the people on the door side face the TV on the far wall.</summary>
        void ConferenceWide(RoomCtx r)
        {
            var cred = r.Wall('N', "credenza", 0f);
            r.Mount('N', "wall_tv", 0f, 1.5f);
            if (cred != null)
            {
                r.BigSlot("trophy", cred.lx + 0.45f, cred.lz, cred.Top);
                r.Top(cred, "cup", -0.5f, 0f);
            }
            float tz = 0.4f;    // the door-side chairs stay clear of the doorway, 0.7 m or more behind the screen-side chairs
            var t = r.Put("conf_table", 0f, tz, 0f);
            if (t != null)
            {
                float length = t.def.W, depth = t.def.D;      // 4.06 along x, 2.22 across
                const int perSide = 4;
                for (int k = 0; k < perSide; k++)
                {
                    float x = (k - (perSide - 1) / 2f) * 1.0f;
                    string ck = OneOf("chair_modern", "chair_std", "chair_modern");
                    r.Put(ck, x, tz - depth / 2 - Tuck(ck), 0f);        // door side: looks at the screen
                    r.Put(ck, x, tz + depth / 2 + Tuck(ck), 180f);      // screen side
                }
                r.Put("chair_office", -(length / 2 + Tuck("chair_office")), tz, 90f);       // the two heads of the table
                r.Put("chair_modern", length / 2 + Tuck("chair_modern"), tz, 270f);
                var proj = r.Top(t, "projector", 0f, 0f); r.Loot(proj, 0.8);
                foreach (float x in new[] { -1.5f, -0.5f, 0.5f, 1.5f })
                    if (Chance(0.55)) r.Loot(r.Top(t, "notepad", x, OneOf(-0.75f, 0.75f), OneOf(20f, -10f)), 0.3);
                r.Top(t, "cup", 0.9f, -0.4f);
                r.MugSlot(t, -0.9f, 0.5f);
            }
            if (r.HasWall('W')) r.Mount('W', "whiteboard", 0f, 1.0f);
            if (r.HasWall('E')) Hang(r, 'E', Art(true), 0f, 1.7f);
            CornerPlants(r, 0.8, true);
        }

        void ThTraining(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // a board on the north wall, rows of desks facing it
            if (r.HasWall('N')) r.Mount('N', "whiteboard", 0f, 1.0f);
            else if (r.HasWall('E')) r.Mount('E', "whiteboard", 0f, 1.0f);
            float dw = Dw("desk_1"), dd = Dd("desk_1");
            float pitchZ = dd + 0.90f;                                       // a desk, the chair behind it and a way past
            float[] rowsZ = { -0.3f, -0.3f + pitchZ };
            // desks on both sides of a centre aisle
            float aisle = r.lanes.Count > 0 ? 1.5f : 0.8f;
            var xs = new List<float>();
            float xd = aisle + 0.1f + dw / 2;
            for (int k = 0; k < 8; k++)
            {
                if (xd + dw / 2 > hx - 0.3f) break;
                xs.Add(xd); xs.Add(-xd);
                xd += dw + (k % 2 == 0 ? 0.12f : 0.9f);                          // (two desks side by side, then a walkway)
            }
            for (int row = 0; row < rowsZ.Length; row++)
                foreach (float x in xs)
                {
                    var d = r.Put("desk_1", x, rowsZ[row], 180f);
                    if (d == null) continue;
                    string ck = OneOf("chair_std", "chair_simple");
                    r.Put(ck, x, rowsZ[row] - d.def.D / 2 - Tuck(ck), 0f);
                    r.Loot(r.Top(d, "monitor_old", -0.2f, -0.12f), 0.2);
                    r.Top(d, "keyboard", -0.2f, 0.12f);
                    if (Chance(0.4)) r.Top(d, "notepad", 0.6f, 0.1f, 8f);
                }
            float tx = hx > 3f ? 1.2f : 0f;
            var teacher = r.Wall('N', "desk_2", tx);
            if (teacher != null)
            {
                r.Put("chair_office", teacher.lx, teacher.lz - teacher.def.D / 2 - Tuck("chair_office"), 0f);
                r.Loot(r.Top(teacher, "projector", 0f, 0f), 0.7);
            }
            r.Wall('W', "bookshelf", hz - 1.0f); r.Wall('E', "cabinet_med", hz - 1.0f);
            CornerPlants(r, 0.6);
        }

        // ------------------------------------------------------------------ break room, storage, copy, server

        /// <summary>
        /// The break room (the owner, 2026-10-17 night: "a break room on each floor with some tables, chairs, vending machines and maybe a couch", "can also have a microwave, etc."): the kitchen run along the north wall
        /// (fridge, counters, sink, microwave, coffee machine, toaster, kettle), two vending machines and the water cooler on the west wall, two round tables with four chairs each in the middle, the notice board, the
        /// bin and the extinguisher by the door; a couch with a coffee table when the room is wide enough. The sign KITCHEN hangs on the corridor side of the door.
        /// </summary>
        void ThBreak(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // kitchen run along the north wall (or the east wall in a deep room)
            char kside = r.HasWall('N') ? 'N' : 'E';
            float len = kside == 'N' ? r.w : r.d;
            float cur = -len / 2 + 0.15f;
            Item Next(string key) { var it = r.Wall(kside, key, cur + Dw(key) / 2); if (it != null) cur += it.def.W + 0.02f; return it; }
            var fridge = Next("fridge");
            var c1 = Next("counter_a2");
            var sink = Next("ksink");
            var c2 = Next("counter_a2");
            var c3 = Next("counter_a2");
            var mini = Next("minifridge");
            if (c1 != null) r.Loot(r.Top(c1, "microwave", 0f, 0.05f), 0.55);
            if (c3 != null) { r.Loot(r.Top(c3, "coffee_machine", -0.2f, 0.05f), 0.8); r.Top(c3, "toaster", 0.28f, 0.05f); r.MugSlot(c3, 0.0f, 0.3f); }
            if (c2 != null) { r.Top(c2, "kettle", 0.15f, 0.05f); r.Top(c2, "cup", -0.3f, 0.2f); r.MugSlot(c2, -0.2f, -0.2f); }
            if (c1 != null && Chance(0.5)) r.BigSlot("espresso", c1.lx, c1.lz, c1.Top);
            float kitchenDepth = Dd("counter_a2");

            // two vending machines and the water cooler along the west wall (the east wall when the west side is open)
            char vside = r.HasWall('W') ? 'W' : r.HasWall('E') ? 'E' : ' ';
            float m = vside == 'E' ? -1f : 1f;                                // 1: the west side
            if (vside != ' ')
            {
                float vz = hz - kitchenDepth - 0.3f - Dw("vending") / 2 - 0.35f;
                var v1 = r.Wall(vside, "vending", vz);
                var v2 = r.Wall(vside, "vending", vz - Dw("vending") - 0.02f);
                if (v1 != null && v2 != null) r.Wall(vside, "water", vz - 2 * Dw("vending") - 0.4f);
                else if (v1 != null) r.Wall(vside, "water", vz - Dw("vending") - 0.4f);
                r.Mount(vside, "clock", vz - Dw("vending") / 2, 2.4f);
            }

            // tables with chairs: the round tables stand clear of the door, the vending machines and the counters
            float tz = Math.Min(-0.1f, hz - kitchenDepth - 1.05f - 0.82f - 0.29f);
            int tables = r.w >= 5f && r.d >= 5f ? 2 : 1;
            float pitch = 2.35f;
            float tx0 = m * (-hx + (vside == ' ' ? 1.4f : Dd("vending") + 1.0f + 1.11f)) ;
            for (int i = 0; i < tables; i++)
            {
                float tx = tables == 1 ? m * (-hx + Dd("vending") + 1.0f + 1.11f + 0.6f) : tx0 + m * i * pitch;
                var t = r.Put("cs_table", tx, tz, 0f);
                if (t == null) continue;
                float off = t.def.W / 2 + Tuck("chair_cafe");
                r.Put("chair_cafe", tx - off, tz, 90f); r.Put("chair_cafe", tx + off, tz, 270f);
                r.Put("chair_cafe", tx, tz - off, 0f); r.Put("chair_cafe", tx, tz + off, 180f);
                r.Top(t, "cup", 0.2f, 0.1f);
                if (Chance(0.5)) r.MugSlot(t, -0.15f, -0.1f);
            }
            // a couch with a coffee table on the far side when there is room (not needed: the owner)
            if (r.w >= 6.6f && r.HasWall(vside == 'W' ? 'E' : 'W'))
            {
                char cs = vside == 'W' ? 'E' : 'W';
                var sofa = r.Wall(cs, "sofa_wood", -hz + 3.4f);
                if (sofa != null) r.Put("coffee_table2", (cs == 'E' ? 1f : -1f) * (hx - sofa.def.D - 0.35f - Dd("coffee_table2") / 2), sofa.lz, 90f);
            }
            if (r.HasWall('E') && vside == 'W') { r.Wall('E', "trash_bin", -hz + 2.2f); r.Mount('E', "corkboard_blank", 0.4f, 1.0f); }
            else if (r.HasWall('W') && vside == 'E') { r.Wall('W', "trash_bin", -hz + 2.2f); r.Mount('W', "corkboard_blank", 0.4f, 1.0f); }
            if (r.HasWall('S')) Hang(r, 'S', Art(false), -hx + 1.3f, 1.7f);
            CornerPlants(r, 0.5);
            // by the door: the extinguisher (a break room is where the toaster is) and the sign KITCHEN outside it
            for (float x = -hx + 0.5f; x <= hx - 0.5f; x += 0.5f) if (r.Mount('S', "extinguisher", x, 0.95f, 0f, KitClear) != null) break;
            if (r.kind == 0 && r.doors.Count > 0) HangDoorSign(r, "sign_kitchen", 1.65f);
        }

        void ThStorage(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // shelving along the two long walls
            bool wide = r.w >= r.d;
            char s1 = wide ? 'N' : 'W', s2 = wide ? 'S' : 'E';
            float len = wide ? r.w : r.d;
            foreach (char side in new[] { s1, s2 })
            {
                if (!r.HasWall(side)) continue;
                r.Run(side, new[] { "shelf", "shelf", "cabinet_med", "shelf" }, -len / 2 + 0.15f, len / 2 - 0.15f, true);
            }
            // boxes stacked on the floor
            int boxes = 4 + rng.Next(4);
            for (int i = 0; i < boxes; i++)
            {
                var b = r.PutSafe(OneOf("box", "box", "box_small", "box_long"), Rnd(-hx + 1.2f, hx - 1.2f), Rnd(-hz + 1.2f, hz - 1.2f), OneOf(0f, 90f));
                if (b != null && b.def.key == "box") r.Loot(b, 0.35);
            }
            if (r.w > 4f && r.d > 4f && r.HasWall('E')) r.Wall('E', "kshelve", 0f);
            r.Mount('S', "clock", hx - 1f, 2.1f);
        }

        void ThCopy(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            float len = r.w;
            float cur = -len / 2 + 0.2f;
            var p1 = r.Wall('N', "printer", cur + Dw("printer") / 2); if (p1 != null) cur += p1.def.W + 0.25f;
            r.Loot(p1, 0.8);
            var cp = r.Wall('N', "copier", cur + Dw("copier") / 2); if (cp != null) cur += cp.def.W + 0.25f;
            r.Loot(cp, 0.9);
            var p2 = r.Wall('N', "printer", len / 2 - 0.2f - Dw("printer") / 2); r.Loot(p2, 0.6);
            r.Wall('N', "shredder", cur + 0.4f);
            r.Wall('N', "cabinet_med", cur + 1.1f);
            if (r.HasWall('W')) r.Wall('W', "shelf", 0.0f);
            if (r.HasWall('E')) { var t = r.Wall('E', "table_box", 0.2f); if (t != null) { r.Top(t, "papers", 0f, 0f); } }
            r.Wall('E', "trash_bin", -hz + 0.9f);
            r.PutSafe("box_long", -hx + 1.0f, -hz + 1.2f, 0f);
            r.Mount('N', "clock", 0.3f, 2.2f);
            for (float x = hx - 0.5f; x >= -hx + 0.5f; x -= 0.5f) if (r.Mount('S', "extinguisher", x, 0.95f, 0f, KitClear) != null) break;      // (copiers get hot)
            CornerPlants(r, 0.3);
        }

        void ThServer(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // racks along the walls
            foreach (char side in new[] { 'N', 'E', 'W' })
            {
                float len = (side == 'N') ? r.w : r.d;
                float a0 = side == 'N' ? -len / 2 + 0.3f : -len / 2 + 1.9f;
                r.Run(side, new[] { "server_rack", "server_rack2" }, a0, len / 2 - 0.3f);
            }
            // the prototype server itself, with space around it
            float sx = hx - 1.6f, sz = 0.2f;
            r.Reserve(sx, sz, 1.5f, 1.5f);
            r.BigSlot("server", sx, sz);
            r.Mount('N', "ac_unit", 0f, 2.6f);
            if (r.w > 4f) r.PutSafe("box_long", -hx + 1.2f, -hz + 1.1f, 0f);
            for (float x = hx - 0.5f; x >= -hx + 0.5f; x -= 0.5f) if (r.Mount('S', "extinguisher", x, 0.95f, 0f, KitClear) != null) break;
        }

        // ------------------------------------------------------------------ lounge, nook, open plan

        void ThLounge(RoomCtx r)
        {
            if (r.HasWall('N') && r.d >= 5.8f && r.w >= 4.8f) LoungeTv(r); else LoungeCompact(r);
        }

        /// <summary>
        /// The TV on a console against the far wall; the sofa faces it across a coffee table, with an armchair on each side,
        /// a plant, a bean bag, the water cooler and a bookcase around the edges.
        /// </summary>
        void LoungeTv(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            var console = r.Wall('N', "credenza", 0f);
            float consD = console != null ? console.def.D : 0.1f;
            r.Mount('N', "wall_tv", 0f, console != null ? 1.45f : 1.4f);
            // (1.5 m between the table and the console, and a metre and a half down each side, so people can walk round)
            float ctD = Dd("coffee_table"), sofaD = Dd("sofa3");
            float zt = hz - consD - 1.5f - ctD / 2;                // coffee table
            var table = r.Put("coffee_table", 0f, zt, 0f);
            float zs = zt - ctD / 2 - 0.40f - sofaD / 2;           // the sofa looks north at the TV
            var sofa = r.Put("sofa3", 0f, zs, 0f);
            float armX = Dw("sofa3") / 2 + Dd("lounge_chair") / 2 + 0.1f;
            r.Put("lounge_chair", -armX, zt, 90f);                 // the armchairs face each other across the table
            r.Put("lounge_chair", armX, zt, 270f);
            if (table != null) { r.Top(table, "cup", 0.3f, 0f); if (Chance(0.5)) r.Top(table, "papers", -0.3f, 0f, 20f); }
            r.Put("plant_tall", armX + 0.3f, zs - 0.2f, 0f);
            r.Put("fatboy", hx - 1.1f, -hz + 1.9f, 0f);
            if (r.HasWall('W'))
            {
                r.Wall('W', "water", -hz + 1.0f);
                Hang(r, 'W', Art(true), 0.3f, 1.7f);
            }
            if (r.HasWall('E')) { Hang(r, 'E', Art(true), -0.4f, 1.7f); r.Mount('E', "clock", 1.4f, 2.1f); }
            if (r.HasWall('S'))
            {
                Hang(r, 'S', Art(true), -hx + 1.3f, 1.7f);
                r.Wall('S', PlantKey(true), hx - 0.5f); r.Wall('S', PlantKey(true), -hx + 0.5f);
            }
        }

        /// <summary>A small lounge: the sofa against one side wall, the TV on the opposite wall, a coffee table between.</summary>
        void LoungeCompact(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            char sofaSide = r.HasWall('E') ? 'E' : 'W';
            char tvSide = sofaSide == 'E' ? 'W' : 'E';
            var sofa = r.Wall(sofaSide, "sofa2", 0f);
            if (r.HasWall(tvSide)) r.Mount(tvSide, "wall_tv", 0f, 1.4f);
            if (sofa != null)
            {
                float tx = sofaSide == 'E' ? hx - sofa.def.D - 0.35f - Dd("coffee_table") / 2 : -hx + sofa.def.D + 0.35f + Dd("coffee_table") / 2;
                var table = r.Put("coffee_table", tx, 0f, 90f);
                if (table != null) r.Top(table, "cup", 0f, 0f);
            }
            r.Put("fatboy", sofaSide == 'E' ? -hx + 1.2f : hx - 1.2f, hz - 1.3f, 0f);
            if (r.HasWall('N'))
            {
                r.Wall('N', "water", sofaSide == 'E' ? hx - 0.8f : -hx + 0.8f);
                r.Wall('N', PlantKey(true), sofaSide == 'E' ? -hx + 0.6f : hx - 0.6f);
                Hang(r, 'N', Art(true), 0f, 1.7f);
            }
            if (r.HasWall('S')) Hang(r, 'S', Art(true), -hx + 1.3f, 1.7f);
            r.Put("plant_tall", sofaSide == 'E' ? hx - 0.5f : -hx + 0.5f, hz - 0.5f, 0f);
        }

        void ThNook(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            if (r.HasWall('N')) { r.Wall('N', "bookshelf", -1.2f); r.Wall('N', "bookshelf", 0.05f); r.Wall('N', "cabinet_med", 1.0f); }
            if (r.HasWall('E')) r.Wall('E', "sofa1", -0.3f);
            r.Put("lounge_chair", 0.4f, 0.4f, 90f);
            r.Put("coffee_table", 1.6f, 0.4f, 90f);
            r.Put("plant_tall", hx - 0.5f, hz - 0.5f, 0f);
            Hang(r, 'E', Art(true), 1.6f, 1.7f);
            CornerPlants(r, 0.7, true);
        }

        void ThOpen(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            Island(r, -0.4f, 0.1f, 2);
            if (r.w > 6.0f) Island(r, 2.35f, 0.1f, 1);
            // storage along the back walls
            if (r.HasWall('N')) r.Run('N', new[] { "bookshelf", "cabinet_med", "cabinet_med", "printer", "shelf" }, -hx + 0.3f, hx - 0.3f, true);
            if (r.HasWall('E')) r.Run('E', new[] { "cabinet_med", "shelf", "cabinet_med" }, -hz + 0.5f, hz - 1.6f, true);
            if (r.HasWall('N')) r.Mount('N', "clock", 0f, 2.3f);
            CornerPlants(r, 0.6);
        }

        void ThLongOpen(RoomCtx r)
        {
            // a long room: an island of workstations on each side of the cross aisle, storage along the back wall
            float hx = r.w / 2, hz = r.d / 2;
            float pitch = Math.Max(Dw("desk_1"), Dw("cube_wall")) + 0.02f;
            foreach (int s in new[] { -1, 1 })
            {
                float a0 = 2.0f, a1 = hx - 0.7f;
                int nx = Math.Min(2, (int)Math.Floor((a1 - a0) / pitch));
                if (nx < 1) continue;
                float len = nx * pitch;
                Island(r, s * (a0 + (a1 - a0) / 2f), 0.0f, nx);              // (centred between the cross aisle and the end wall: a walkway on both sides)
            }
            r.Run('N', new[] { "bookshelf", "cabinet_med", "cabinet_med", "printer", "shelf", "cabinet_med" }, -hx + 0.3f, hx - 0.3f, true);
            r.Mount('N', "clock", 0f, 2.3f);
            CornerPlants(r, 0.6);
        }

        void ThCanteen(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // serving counter along the back wall, vending machines at its end
            float cur = -hx + 0.3f;
            var fr = r.Wall('N', "fridge", cur + Dw("fridge") / 2); if (fr != null) cur += fr.def.W + 0.05f;
            for (int i = 0; i < 3; i++) { var c = r.Wall('N', "counter_a2", cur + 0.5f); if (c != null) { cur += 1.0f; if (i == 1) r.Loot(r.Top(c, "coffee_machine", 0f, 0f), 0.8); else r.MugSlot(c, 0f, 0.1f); } }
            var sk = r.Wall('N', "ksink", cur + 0.5f); if (sk != null) cur += 1.0f;
            for (int i = 0; i < 3; i++) { var c = r.Wall('N', "counter_a2", cur + 0.5f); if (c != null) { cur += 1.0f; if (i == 0) r.Loot(r.Top(c, "microwave", 0f, 0f), 0.6); } }
            for (int i = 0; i < 3; i++) { var v = r.Wall('N', "vending", cur + Dw("vending") / 2 + 0.3f); if (v != null) cur += Dw("vending") + 0.02f; }
            // dining tables in a row in front of the counter (the pack's round tables with four chairs each)
            float tz = Math.Min(-0.4f, hz - Dd("counter_a2") - 1.05f - 0.82f - 0.29f);
            for (float x = -hx + 2.0f; x < hx - 1.5f; x += 2.9f)
            {
                if (r.InLane(x, 1.4f)) continue;
                var t = r.Put("cs_table", x, tz, 0f);
                if (t == null) continue;
                float off = t.def.W / 2 + Tuck("chair_cafe");
                r.Put("chair_cafe", x - off, tz, 90f); r.Put("chair_cafe", x + off, tz, 270f);
                r.Put("chair_cafe", x, tz - off, 0f); r.Put("chair_cafe", x, tz + off, 180f);
                if (Chance(0.4)) r.MugSlot(t, 0f, 0f);
            }
            r.Mount('S', "clock", hx - 3f, 2.1f);
            CornerPlants(r, 0.5, true);
        }

        // ------------------------------------------------------------------ coffee shop (first floor)

        void ThCafeCounter(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            // the order counter along the north wall with the register and a coffee machine, a pastry case beside it
            var counter = r.Wall('N', "cs_counter", hx - 2.3f);
            if (counter != null)
            {
                float cw = counter.def.W;
                var reg = r.Top(counter, "cs_register", cw / 2 - 0.6f, 0f, 180f);
                r.Loot(r.Top(counter, "coffee_machine", -cw / 2 + 0.5f, 0f), 0.8);
                r.Loot(r.Top(counter, "takeaway", 0.2f, 0.1f), 0.7);
                r.Loot(r.Top(counter, "takeaway", 0.5f, 0.1f), 0.5);
                r.MugSlot(counter, -0.3f, 0.2f);
                r.Mount('N', "cs_menu", counter.lx, 2.15f);
            }
            var disp = r.Wall('E', "cs_display", -0.2f);
            if (disp != null)
            {
                r.Loot(r.Top(disp, "pastry", 0f, 0.0f), 0.8);
            }
            r.Put("cs_pot", -hx + 0.6f, hz - 0.6f, 0f);
            r.Wall('E', "cs_pot", -hz + 0.6f);
            Hang(r, 'E', Art(true), 2.0f, 1.7f);
            // a couple of seats at the window end
            var t = r.Put("cs_table", -1.8f, -0.4f, 0f);
            if (t != null)
            {
                float off = t.def.W / 2 + Tuck("cs_chair");
                r.Put("cs_chair", -1.8f - off, -0.4f, 90f); r.Put("cs_chair", -1.8f + off, -0.4f, 270f);
            }
        }

        void ThCafeSeating(RoomCtx r)
        {
            float hx = r.w / 2, hz = r.d / 2;
            for (float x = -1.5f; x <= 1.6f; x += 3.0f)
                for (float z = -1.5f; z <= 1.6f; z += 3.0f)
                {
                    var t = r.Put("cs_table", x, z, 0f);
                    if (t == null) continue;
                    float off = t.def.W / 2 + Tuck("cs_chair");
                    r.Put("cs_chair", x - off, z, 90f); r.Put("cs_chair", x + off, z, 270f);
                    r.Put("cs_chair", x, z - off, 0f); r.Put("cs_chair", x, z + off, 180f);
                    r.Loot(r.Top(t, "takeaway", 0.1f, 0.05f), 0.25);
                    if (Chance(0.5)) r.MugSlot(t, -0.2f, 0.1f);
                }
            if (r.HasWall('N')) { r.Wall('N', "cs_pot", -hx + 0.7f); r.Wall('N', "cs_pot", hx - 0.7f); Hang(r, 'N', Art(true), 0f, 1.7f); }
            if (r.HasWall('E')) { r.Wall('E', "cs_pot", 0f); Hang(r, 'E', Art(true), 2.0f, 1.7f); }
            if (r.HasWall('E')) r.Wall('E', "trash_bin", -hz + 0.6f);
        }

        // ------------------------------------------------------------------ elevator lobbies

        void ThLobby(RoomCtx r)
        {
            // the lobby around the service elevator: waiting area on the west side, reception desk on the south side,
            // plants and art by the doorways. (The ring is 17.6 m across with the shaft in the middle.)
            float h = Inner;
            // west wall: two sofas with a coffee table between and lounge chairs
            if (r.HasWall('W'))
            {
                r.Wall('W', "sofa2", 3.2f); r.Wall('W', "sofa2", -3.2f);
                r.Put("coffee_table", -h + Dd("sofa2") + 0.35f + Dd("coffee_table") / 2, 0f, 90f);
                r.Wall('W', PlantKey(true), 5.8f); r.Wall('W', PlantKey(true), -5.8f);
                Hang(r, 'W', Art(true), 3.2f, 1.7f); Hang(r, 'W', Art(true), -3.2f, 1.7f);
                r.Mount('W', "clock", 0f, 2.1f);
            }
            // south wall: the reception desk facing the room, with the staff side towards the wall (room for a chair behind)
            float back = -h + 1.75f;
            float rd = Dd("desk_reception");
            var c = r.Put("desk_reception", -h + 3.4f + Dw("desk_reception") / 2, back + rd / 2, 0f);
            if (c != null)
            {
                r.Loot(r.Top(c, "monitor_new", -0.7f, 0.0f, 180f), 0.4); r.Top(c, "keyboard", -0.7f, 0.25f, 180f);
                r.Loot(r.Top(c, "phone", 0.2f, 0.1f, 180f), 0.4);
                r.Put("chair_office", c.lx - 0.6f, back - Tuck("chair_office") - 0.1f, 0f);
                Hang(r, 'S', Art(true), c.lx, 2.0f);
                r.Mount('S', "sign_reception", c.lx - 0.0f, 2.45f);
            }
            // gaps: plants beside them
            r.Wall('S', PlantKey(true), h - 0.7f);
            r.Wall('N', PlantKey(true), -h + 0.7f); r.Wall('N', PlantKey(true), h - 0.7f);
            r.Wall('E', PlantKey(true), -h + 0.7f); r.Wall('E', PlantKey(true), h - 0.7f);
            r.Wall('N', "water", -h + 2.0f); r.Wall('N', "sofa2", -h + 5.0f);
            r.Wall('E', "sofa2", h - 4.5f);
            Hang(r, 'N', Art(true), -h + 5.0f, 1.7f);
            r.Mount('N', "clock", h - 4.0f, 2.2f);
            Hang(r, 'E', Art(true), h - 4.5f, 1.7f);
            r.Wall('N', "cabinet_med", h - 2.9f);
            // more waiting areas, built onto the sofas already along the walls (so nothing blocks the way round the ring):
            // a coffee table in front of each sofa on the east and north sides, an armchair at the end of it, a plant, art above
            var east = r.Wall('E', "sofa2", -h + 4.5f);
            foreach (float ez in new[] { h - 4.5f, -h + 4.5f })
            {
                float tx = h - Dd("sofa2") - 0.35f - Dd("coffee_table") / 2;
                var t = r.Put("coffee_table", tx, ez, 90f);
                if (t == null) continue;
                r.Top(t, "cup", 0f, 0.25f); if (Chance(0.5)) r.Top(t, "papers", 0f, -0.25f, 90f);
                r.Put("lounge_chair", tx, ez + (ez > 0 ? 1.6f : -1.6f), ez > 0 ? 180f : 0f);
                Hang(r, 'E', Art(true), ez, 1.7f);
            }
            var nt = r.Put("coffee_table", -h + 5.0f, h - Dd("sofa2") - 0.35f - Dd("coffee_table") / 2, 0f);
            if (nt != null) r.Top(nt, "cup", 0.3f, 0f);
            r.Put("plant_tall", -h + 5.0f + 1.9f, h - 0.5f, 0f);
            // the safety kit of the lobby: extinguisher, pull station and an emergency axe in its case by the nearest doorway
            foreach (char side in new[] { 'N', 'E' })
                for (float a = h - 6.0f; a >= -h + 6.0f; a -= 0.5f)
                    if (r.Mount(side, "extinguisher", a, 0.95f, 0f, KitClear) != null)
                    {
                        r.Mount(side, "fire_alarm", a + 0.5f, 1.0f, 0f, KitClear);
                        if (side == 'N') r.Mount(side, "emergency_axe", a - 1.0f, 0.95f, 0f, KitClear);
                        a = -h;                                                             // (one set per wall)
                    }
        }

        void ThService(RoomCtx r)
        {
            // around the guest elevator: a loading area, shelves and crates along the outer walls
            float h = Inner;
            int stair = StairIndex(r.info.cellX, r.info.cellY);
            if (stair >= 0) StairFront(r, stair);                           // (the floor in front of the stairwell door and its safety kit stays clear: reserved before the crates go down)
            foreach (char side in new[] { 'N', 'S', 'E', 'W' })
            {
                if (!r.HasWall(side)) continue;
                r.Run(side, new[] { "shelf", "cabinet_med", "shelf", "box", "cabinet_med" }, -h + 0.4f, h - 0.4f, true, 0.2f);
            }
            for (int i = 0; i < 6; i++)
            {
                var b = r.PutSafe(OneOf("box", "box", "box_small", "box_long", "box_stack"), Rnd(-h + 1.5f, h - 1.5f), Rnd(-h + 1.5f, h - 1.5f), OneOf(0f, 90f));
                if (b != null && b.def.key == "box") r.Loot(b, 0.4);
            }
            r.Mount('N', "ac_unit", 0f, 2.7f);
            if (stair >= 0) StairKit(r, stair);
        }

        /// <summary>
        /// The outside of a stairwell's landing, round its door (the owner, 2026-10-17 night: "signs for the stairs, some fire alarms placed, some emergency hatchets placed, some fire extinguishers placed"): to the
        /// left of the door (seen from the corridor) the pack STAIRS sign (twice its size), under it the emergency axe in its case; to the right the green EXIT sign, the fire alarm pull
        /// station and the fire extinguisher. The ring is not turned (its frame is the world's), the landing's outer face lies 2.4 m from the middle of the cell.
        /// </summary>
        void StairKit(RoomCtx r, int stair)
        {
            if (r.turns != 0 || inp.stairDirX == null) return;
            int dx = inp.stairDirX[stair], dz = inp.stairDirY[stair];
            if (dx == 0 && dz == 0) return;
            float yaw = dx > 0 ? 90f : dx < 0 ? 270f : dz > 0 ? 0f : 180f;
            float ax = dx != 0 ? 0f : 1f, az = dx != 0 ? 1f : 0f;                 // along the landing's face (the door is in the middle)
            float face = StairHalf;                                               // the outer face of the landing's wall
            void Put(string key, float u, float bottom)
            {
                var def = PropTable.Get(key);
                float lx = dx * (face + def.D / 2 + 0.01f) + ax * u, lz = dz * (face + def.D / 2 + 0.01f) + az * u;
                r.PutDef(def, lx, lz, yaw, bottom, false, true, 0);
            }
            // (u: along the face, from the middle of the door; the door gap is 2 m wide, so the two bits of wall lie at 1.0 .. 2.25 on either side)
            Put("sign_stairs_big", -1.63f, 2.2f);
            Put("emergency_axe", -1.63f, 0.95f);
            Put("sign_exit", 1.63f, 2.5f);
            Put("fire_alarm", 1.63f, 1.15f);
            Put("extinguisher", 1.63f, 0.55f);
        }

        /// <summary>Safety gear stays reachable: the floor 0.9 m out from it, and this far (m) to either side of it, is free of anything solid (<see cref="RoomCtx.Mount"/>).</summary>
        const float KitClear = 0.35f;

        /// <summary>The floor in front of a stairwell door and its safety kit (the axe case, the extinguisher, the pull station) stays clear: nothing is put within 0.9 m of the landing face, along its whole width.</summary>
        void StairFront(RoomCtx r, int stair)
        {
            if (r.turns != 0 || inp.stairDirX == null) return;
            int dx = inp.stairDirX[stair], dz = inp.stairDirY[stair];
            if (dx == 0 && dz == 0) return;
            float mid = StairHalf + 0.45f;
            if (dx != 0) r.Reserve(dx * mid, 0f, 0.9f, 4.7f); else r.Reserve(0f, dz * mid, 4.7f, 0.9f);
        }
    }
}
