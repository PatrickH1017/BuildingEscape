using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the Floor 7 environmental art pass (<c>OfficePlanner.Dressing.cs</c>, 2026-10-23). They plan whole floors twice - with the pass off and on - and prove that the pass is additive
    /// (walls, rooms, doorways, patrol stops, treasure and safe spots are the same; the old pieces come first and are unchanged), that nothing it adds blocks or squeezes a walkway (a thing taller than 30 cm stands
    /// flush with a wall or a solid, there is never a 6..120 cm gap, solids keep clear of patrol stops, treasure spots, doorways and the safety gear, no solid in the ring lobbies) and that a round walker of
    /// 0.45 m (a guard) and 0.6 m (the planner's walking width) can still reach everything that was reachable before. Called from <c>NetDevTest.PolicyTest</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void FloorDressTest()
        {
            bool saved = OfficePlanner.DressingOn; float savedBudget = OfficePlanner.DressBudget;
            try
            {
                OfficePlanner.DressBudget = 1f;
                FdDress(4101, 8, 5, true);
                FdDress(5, 8, 5, true);          // (the seed whose first version cut off a corner pocket by a fish tank)
                FdDress(777, 6, 4, false);

                // the other floors are untouched by the pass
                bool same = true;
                foreach (int fl in new[] { 1, 3, 6, 8 })
                {
                    var x = FdMake(4101, fl, 8, 5, false); var y = FdMake(4101, fl, 8, 5, true);
                    same &= FdHash(FdSig(x, true)) == FdHash(FdSig(y, true)) && y.pieces.All(p => !p.dressing);
                }
                Check("floor dressing: floors 1, 3, 6 and 8 are exactly the same with the pass on or off", same);

                // the switch and the amount: off = nothing added; a lower budget = fewer things, never more
                var off = FdMake(4101, 7, 8, 5, false); var full = FdMake(4101, 7, 8, 5, true);
                OfficePlanner.DressBudget = 0.5f;
                var half = FdMake(4101, 7, 8, 5, true);
                Check("floor dressing: the pass adds things, and half the budget adds fewer", full.pieces.Count > off.pieces.Count + 200 && half.pieces.Count < full.pieces.Count && half.pieces.Count > off.pieces.Count,
                    off.pieces.Count + " -> " + half.pieces.Count + " (half) -> " + full.pieces.Count);
                Check("floor dressing: the floor number it works on is 7", OfficePlanner.DressedFloor == 7);
                FloorDressAuditSelfTest();
            }
            finally { OfficePlanner.DressingOn = saved; OfficePlanner.DressBudget = savedBudget; }
        }

        // ------------------------------------------------------------------ helpers

        static Layout FdMake(int seed, int floor, int cols, int rows, bool dress)
        {
            OfficePlanner.DressingOn = dress;
            var shape = FloorShape.Maze(new System.Random(seed), cols, rows, 0.12);
            return OfficePlanner.Build(shape.PlanFor(floor, seed, 18f, 3.5f, 5 * seed + floor));
        }

        static string FdF(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        static string FdR(Rc r) => "[" + FdF(r.x0) + "," + FdF(r.z0) + "," + FdF(r.x1) + "," + FdF(r.z1) + "]";
        static string FdPiece(Piece p) => p.key + "|" + FdF(p.x) + "|" + FdF(p.y) + "|" + FdF(p.z) + "|" + FdF(p.yaw) + "|" + FdR(p.rc) + "|" + (p.solid ? 1 : 0) + (p.mount ? 1 : 0) + (p.onTop ? 1 : 0) + (p.hide ? 1 : 0) + (p.loot ? 1 : 0) + "|" + p.room;
        static int FdHash(string s) { unchecked { int h = 17; foreach (char c in s) h = h * 31 + c; return h; } }

        /// <summary>Everything about a floor that the pass must not change (and, with <paramref name="pieces"/>, the pieces too).</summary>
        static string FdSig(Layout L, bool pieces)
        {
            var sb = new StringBuilder();
            foreach (var w in L.walls) sb.Append("W" + FdR(w.rc) + FdF(w.y0) + FdF(w.y1) + w.kind);
            foreach (var p in L.patches) sb.Append("P" + FdR(p.rc) + p.tint);
            foreach (var h in L.hideZones) sb.Append("H" + FdR(h));
            foreach (var s in L.bigSlots) sb.Append("B" + FdF(s.x) + FdF(s.y) + FdF(s.z) + FdF(s.yaw) + s.kind + s.room);
            foreach (var s in L.mugSlots) sb.Append("M" + FdF(s.x) + FdF(s.y) + FdF(s.z) + s.room);
            foreach (var w in L.waypoints) sb.Append("V" + FdF(w[0]) + "," + FdF(w[1]));
            foreach (var c in L.clear) sb.Append("C" + FdR(c));
            foreach (var c in L.deadDoors) sb.Append("D" + FdR(c));
            foreach (var r in L.rooms)
            {
                sb.Append("R" + r.theme + r.role + r.kind + FdR(r.bounds) + FdF(r.wx) + FdF(r.wz) + r.hideCount);
                foreach (var d in r.doors) sb.Append("d" + FdR(d));
                foreach (var b in r.blocks) sb.Append("b" + FdR(b));
            }
            if (pieces) foreach (var p in L.pieces) sb.Append("\nX" + FdPiece(p));
            return sb.ToString();
        }

        static float FdGap(Rc a, Rc b)
        {
            float dx = Math.Max(0f, Math.Max(a.x0 - b.x1, b.x0 - a.x1)), dz = Math.Max(0f, Math.Max(a.z0 - b.z1, b.z0 - a.z1));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Everything a walker bumps into: solid furniture, solid walls and panels, the blocked parts of the rooms and the outer walls (with the doorways left open).</summary>
        static List<Rc> FdSolids(Layout L)
        {
            var res = new List<Rc>();
            foreach (var p in L.pieces) if (p.solid && !p.mount && !p.onTop) res.Add(p.rc);
            foreach (var w in L.walls) if ((w.kind == "wall" || w.kind == "panel") && w.y0 < 0.5f) res.Add(w.rc);
            foreach (var ri in L.rooms) foreach (var b in ri.blocks) res.Add(b);
            var inp = L.input; float cs = inp.cell; const float t = 0.2f, hg = 2f;
            for (int c = 0; c <= inp.cols; c++)
                for (int rr = 0; rr < inp.rows; rr++)
                {
                    float x = c * cs, z0 = rr * cs, z1 = z0 + cs, zm = (z0 + z1) * 0.5f;
                    bool open = c > 0 && c < inp.cols && inp.openE[c - 1, rr];
                    if (!open) res.Add(new Rc(x - t, z0, x + t, z1));
                    else { res.Add(new Rc(x - t, z0, x + t, zm - hg)); res.Add(new Rc(x - t, zm + hg, x + t, z1)); }
                }
            for (int rr = 0; rr <= inp.rows; rr++)
                for (int c = 0; c < inp.cols; c++)
                {
                    float z = rr * cs, x0 = c * cs, x1 = x0 + cs, xm = (x0 + x1) * 0.5f;
                    bool open = rr > 0 && rr < inp.rows && inp.openN[c, rr - 1];
                    if (!open) res.Add(new Rc(x0, z - t, x1, z + t));
                    else { res.Add(new Rc(x0, z - t, xm - hg, z + t)); res.Add(new Rc(xm + hg, z - t, x1, z + t)); }
                }
            return res;
        }

        /// <summary>The zone in front of each wall-mounted extinguisher, axe and fire alarm that must stay free of tall solid things (0.9 m deep, a little wider than the item).</summary>
        static List<Rc> FdKitZones(Layout L)
        {
            var res = new List<Rc>();
            foreach (var g in L.pieces)
            {
                if (!g.mount || (g.key != "extinguisher" && g.key != "emergency_axe" && g.key != "fire_alarm")) continue;
                float fx = (float)Math.Sin(g.yaw * Math.PI / 180.0), fz = (float)Math.Cos(g.yaw * Math.PI / 180.0);
                Rc z = g.rc;
                if (Math.Abs(fx) > Math.Abs(fz)) { if (fx > 0f) z.x1 = g.rc.x0 + 0.9f; else z.x0 = g.rc.x1 - 0.9f; z.z0 -= 0.35f; z.z1 += 0.35f; }
                else { if (fz > 0f) z.z1 = g.rc.z0 + 0.9f; else z.z0 = g.rc.z1 - 0.9f; z.x0 -= 0.35f; z.x1 += 0.35f; }
                res.Add(z);
            }
            return res;
        }

        static RoomInfo FdRoomAt(Layout L, float x, float z)
        {
            RoomInfo best = null; float area = float.MaxValue;
            foreach (var r in L.rooms)
            {
                if (!r.bounds.Contains(x, z)) continue;
                float a = r.bounds.W * r.bounds.D;
                if (a < area) { area = a; best = r; }
            }
            return best;
        }

        // ---- glass fronts: would a wall of an office still have its window? (a mount within 0.5 m of a glass-candidate wall takes the window away)

        static bool FdInCorridor(Layout L, float x, float z)
        {
            foreach (var r in L.rooms)
            {
                if (r.kind != 2 || !r.bounds.Contains(x, z)) continue;
                bool blocked = false;
                foreach (var b in r.blocks) if (b.Contains(x, z)) { blocked = true; break; }
                if (!blocked) return true;
            }
            return false;
        }

        static bool FdFrontOf(WallBox w, Rc b, Layout L)
        {
            var r = w.rc; bool alongX = r.W > r.D;
            float overlap = alongX ? Math.Min(r.x1, b.x1) - Math.Max(r.x0, b.x0) : Math.Min(r.z1, b.z1) - Math.Max(r.z0, b.z0);
            if (overlap < 1.2f) return false;
            float ox, oz;
            if (alongX) { if (Math.Abs(r.Cz - b.z0) < 0.4f) { ox = 0; oz = -1; } else if (Math.Abs(r.Cz - b.z1) < 0.4f) { ox = 0; oz = 1; } else return false; }
            else { if (Math.Abs(r.Cx - b.x0) < 0.4f) { ox = -1; oz = 0; } else if (Math.Abs(r.Cx - b.x1) < 0.4f) { ox = 1; oz = 0; } else return false; }
            return FdInCorridor(L, r.Cx + ox * 0.9f, r.Cz + oz * 0.9f);
        }

        static List<bool> FdGlassHung(Layout L)
        {
            var res = new List<bool>();
            var op = L.outpost;
            foreach (var room in L.rooms)
            {
                if (room.kind != 0 || !(room.theme == "office" || room.theme == "conference" || room.theme == "meeting" || room.theme == "training")) continue;
                if (op != null && ReferenceEquals(op.room, room)) continue;
                foreach (var w in L.walls)
                {
                    if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < L.input.wallH - 0.01f) continue;
                    var r = w.rc;
                    if ((r.W > r.D ? r.D : r.W) > 0.45f || (r.W > r.D ? r.W : r.D) < 1.6f) continue;
                    if (!FdFrontOf(w, room.bounds, L)) continue;
                    var near = r.Grow(0.5f);
                    bool hung = false;
                    foreach (var p in L.pieces) if (p.mount && near.Overlaps(p.rc)) { hung = true; break; }
                    res.Add(hung);
                }
            }
            return res;
        }

        // ---- the walk test: a round walker on a 0.1 m grid, flooded from the patrol stops

        sealed class FdWalk { public int nx, nz; public bool[] blocked, seen; public const float Res = 0.1f; }

        static FdWalk FdFlood(Layout L, List<Rc> solids, float agent, List<float[]> seeds)
        {
            var inp = L.input;
            var g = new FdWalk();
            g.nx = (int)Math.Ceiling(inp.cols * inp.cell / FdWalk.Res); g.nz = (int)Math.Ceiling(inp.rows * inp.cell / FdWalk.Res);
            g.blocked = new bool[g.nx * g.nz];
            foreach (var s in solids)
            {
                var rc = s.Grow(agent);
                int x0 = Math.Max(0, (int)Math.Ceiling(rc.x0 / FdWalk.Res - 0.5f)), x1 = Math.Min(g.nx - 1, (int)Math.Floor(rc.x1 / FdWalk.Res - 0.5f));
                int z0 = Math.Max(0, (int)Math.Ceiling(rc.z0 / FdWalk.Res - 0.5f)), z1 = Math.Min(g.nz - 1, (int)Math.Floor(rc.z1 / FdWalk.Res - 0.5f));
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float cx = (x + 0.5f) * FdWalk.Res, cz = (z + 0.5f) * FdWalk.Res;
                        float dx = Math.Max(Math.Max(s.x0 - cx, 0f), cx - s.x1), dz = Math.Max(Math.Max(s.z0 - cz, 0f), cz - s.z1);
                        if (dx * dx + dz * dz < agent * agent) g.blocked[z * g.nx + x] = true;
                    }
            }
            FdFill(g, seeds);
            return g;
        }

        /// <summary>Floods the free cells from the seeds (each seed starts at the nearest free cell within 0.8 m); fills <c>seen</c>.</summary>
        static void FdFill(FdWalk g, List<float[]> seeds)
        {
            g.seen = new bool[g.nx * g.nz];
            var q = new Queue<int>();
            foreach (var sd in seeds)
            {
                int cx = (int)Math.Floor(sd[0] / FdWalk.Res), cz = (int)Math.Floor(sd[1] / FdWalk.Res);
                int best = -1; float bd = float.MaxValue;
                for (int a = -8; a <= 8; a++)
                    for (int b = -8; b <= 8; b++)
                    {
                        int x = cx + a, z = cz + b;
                        if (x < 0 || z < 0 || x >= g.nx || z >= g.nz || g.blocked[z * g.nx + x]) continue;
                        float d = a * a + b * b;
                        if (d < bd) { bd = d; best = z * g.nx + x; }
                    }
                if (best >= 0 && !g.seen[best]) { g.seen[best] = true; q.Enqueue(best); }
            }
            while (q.Count > 0)
            {
                int k = q.Dequeue(); int x = k % g.nx, z = k / g.nx;
                for (int d = 0; d < 4; d++)
                {
                    int xx = x + (d == 0 ? 1 : d == 1 ? -1 : 0), zz = z + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (xx < 0 || zz < 0 || xx >= g.nx || zz >= g.nz) continue;
                    int kk = zz * g.nx + xx;
                    if (g.blocked[kk] || g.seen[kk]) continue;
                    g.seen[kk] = true; q.Enqueue(kk);
                }
            }
        }

        /// <summary>The cells reached from the first patrol stop only (the walk across the whole floor, from room to room) on the same blocked grid.</summary>
        static FdWalk FdFromFirstStop(FdWalk g, Layout L)
        {
            var h = new FdWalk { nx = g.nx, nz = g.nz, blocked = g.blocked };
            FdFill(h, L.waypoints.Take(1).Select(w => new[] { w[0], w[1] }).ToList());
            return h;
        }

        // ------------------------------------------------------------------ one floor

        static void FdDress(int seed, int cols, int rows, bool walk)
        {
            string tag = "floor dressing (seed " + seed + ", " + cols + "x" + rows + "): ";
            var b = FdMake(seed, 7, cols, rows, false);
            var a = FdMake(seed, 7, cols, rows, true);
            var a2 = FdMake(seed, 7, cols, rows, true);
            Check(tag + "planning is deterministic", FdHash(FdSig(a, true)) == FdHash(FdSig(a2, true)));
            Check(tag + "with the switch off nothing is flagged as dressing", b.pieces.All(p => !p.dressing));
            FdAudit(tag, b, a, seed, walk, null);
        }

        /// <summary>Reports a verdict: as a check when <paramref name="bad"/> is null, else only the failed names are collected (the self-test below feeds the audit a floor with something wrong on purpose).</summary>
        static void FdV(List<string> bad, string name, bool ok, string detail = "")
        {
            if (bad == null) { if (detail.Length == 0) Check(name, ok); else Check(name, ok, detail); }
            else if (!ok) bad.Add(name);
        }

        /// <summary>All the scope and clearance verdicts for a floor <paramref name="a"/> (with the pass) against the same floor <paramref name="b"/> (without it).</summary>
        static void FdAudit(string tag, Layout b, Layout a, int seed, bool walk, List<string> bad)
        {

            // --- scope: the same structure, the old pieces first and unchanged
            FdV(bad, tag + "walls, rooms, doorways, patrol stops, treasure and safe spots, hide zones are unchanged", FdHash(FdSig(b, false)) == FdHash(FdSig(a, false)));
            int n0 = b.pieces.Count;
            bool prefix = a.pieces.Count >= n0;
            for (int i = 0; prefix && i < n0; i++) if (FdPiece(a.pieces[i]) != FdPiece(b.pieces[i])) prefix = false;
            FdV(bad, tag + "the old pieces come first, exactly as before", prefix);
            var added = a.pieces.Skip(n0).ToList();
            FdV(bad, tag + "the pass adds things (" + added.Count + ")", added.Count > 200);
            FdV(bad, tag + "every added piece is flagged, and none hides a person, is loot or is picked", added.All(p => p.dressing && !p.hide && !p.loot && !p.picked) && a.pieces.Take(n0).All(p => !p.dressing));
            FdV(bad, tag + "the safes and the guard outpost are where they were",
                a.safes.Count == b.safes.Count && a.safes.Zip(b.safes, (x, y) => FdPiece(x.piece) == FdPiece(y.piece)).All(t => t) && (a.outpost == null) == (b.outpost == null));

            // --- every added thing on the floor
            var solidsB = FdSolids(b); var solidsA = FdSolids(a);
            var kit = FdKitZones(a);
            var newFloor = added.Where(p => !p.mount && !p.onTop).ToList();
            var newSolid = newFloor.Where(p => p.solid).ToList();
            int badGap = 0, badFlush = 0, badStop = 0, badSlot = 0, badClear = 0, badKit = 0, badRoom = 0, badRing = 0, badOver = 0, badBehind = 0;
            var allMounts = a.pieces.Where(m => m.mount).ToList();
            foreach (var p in newFloor)
            {
                var room = FdRoomAt(a, p.x, p.z);
                if (room == null || !room.bounds.Grow(0.01f).Contains(p.rc.x0, p.rc.z0) || !room.bounds.Grow(0.01f).Contains(p.rc.x1, p.rc.z1) || room.theme == "restroom" || room.theme == "outpost") { badRoom++; continue; }
                if (p.solid && p.h > 0.3f && room.kind == 2 && room.blocks.Count > 0 && room.theme != "hall") badRing++;
                bool rugP = p.key.StartsWith("rug_");
                foreach (var o in a.pieces)
                {
                    if (ReferenceEquals(o, p) || o.mount || o.onTop || !p.rc.Overlaps(o.rc, 0.02f)) continue;
                    bool rugO = o.key.StartsWith("rug_");
                    // a rug may lie under the furniture people sit or stand at; nothing else may overlap anything
                    bool friendly = o.key.StartsWith("desk_") || o.key.StartsWith("chair_") || o.key.StartsWith("sofa") || o.key == "armchair" || o.key == "lounge_chair" || o.key.StartsWith("coffee_table")
                        || o.key.StartsWith("table_") || o.key.StartsWith("conf_table") || o.key == "fatboy" || o.key.StartsWith("beanbag") || o.key == "stool" || o.key == "cs_table" || o.key == "cs_chair" || o.key == "drafting_table";
                    if (rugP && !rugO && friendly) continue;
                    badOver++;
                }
                if (p.h > 0.3f)
                {
                    var near = p.rc;
                    foreach (var m in allMounts) if (m.y < p.h && m.rc.Overlaps(near, 0.004f)) { badBehind++; break; }
                }
                if (p.solid || p.h > 0.3f)
                {
                    bool flush = false, squeeze = false;
                    foreach (var s in solidsA)
                    {
                        if (s.x0 == p.rc.x0 && s.z0 == p.rc.z0 && s.x1 == p.rc.x1 && s.z1 == p.rc.z1) continue;
                        float g = FdGap(p.rc, s);
                        if (g > 1.21f) continue;
                        if (g <= 0.06f) flush = true;
                        else if (g < (p.solid ? 1.2f : 0.45f)) squeeze = true;
                    }
                    if (squeeze && p.solid) badGap++;
                    if (!flush) badFlush++;
                }
                if (p.solid)
                {
                    foreach (var w in a.waypoints) if (p.rc.Grow(0.8f).Contains(w[0], w[1])) { badStop++; break; }
                    foreach (var w in a.rooms) if ((w.wx != 0 || w.wz != 0) && p.rc.Grow(0.8f).Contains(w.wx, w.wz)) { badStop++; break; }
                    foreach (var s in a.bigSlots) if (p.rc.Grow(1.7f).Contains(s.x, s.z)) { badSlot++; break; }
                    foreach (var c in a.clear) if (p.rc.Overlaps(c.Grow(0.9f))) { badClear++; break; }
                    if (p.h > 0.3f) foreach (var z in kit) if (p.rc.Overlaps(z, 0.02f)) { badKit++; break; }
                }
                else foreach (var c in a.clear) if (p.rc.Overlaps(c.Grow(0.3f))) { badClear++; break; }
            }
            FdV(bad, tag + "everything added on the floor is inside a room (none in the restrooms or the outpost)", badRoom == 0, badRoom.ToString());
            FdV(bad, tag + "no added solid in the ring lobbies", badRing == 0, badRing.ToString());
            FdV(bad, tag + "nothing added overlaps the furniture (apart from rugs under desks, chairs, sofas and tables)", badOver == 0, badOver.ToString());
            FdV(bad, tag + "every added tall thing stands flush with a wall or a solid, and no added solid leaves a 6..120 cm gap", badGap == 0 && badFlush == 0, "gaps " + badGap + ", not flush " + badFlush);
            FdV(bad, tag + "no tall thing stands against (touches) a picture, clock, switch or anything else hung on a wall", badBehind == 0, badBehind.ToString());
            FdV(bad, tag + "added solids keep 0.8 m from patrol stops and 1.7 m from treasure spots", badStop == 0 && badSlot == 0, "stops " + badStop + ", treasure " + badSlot);
            FdV(bad, tag + "added things keep clear of doorways (solids 0.9 m, the rest 0.3 m)", badClear == 0, badClear.ToString());
            FdV(bad, tag + "nothing tall and solid in front of the extinguishers, axes and alarms", badKit == 0, badKit.ToString());

            // --- things on furniture and on walls
            int badHost = 0, badTopY = 0, badMountY = 0, badCorr = 0;
            foreach (var p in added)
            {
                if (p.onTop)
                {
                    var h = p.host;
                    if (h == null) { badHost++; continue; }
                    if (!(p.rc.x0 >= h.rc.x0 - 0.003f && p.rc.x1 <= h.rc.x1 + 0.003f && p.rc.z0 >= h.rc.z0 - 0.003f && p.rc.z1 <= h.rc.z1 + 0.003f)) badHost++;
                    if (p.y < h.y + 0.04f || p.y > h.y + h.h + 0.003f) badTopY++;
                }
                if (p.mount)
                {
                    if (p.y < 0.05f || p.y + p.h > a.input.wallH) badMountY++;
                    var room = FdRoomAt(a, p.x, p.z);
                    if (room != null && room.kind == 2 && p.y + p.h > 1.951f) badCorr++;
                }
            }
            FdV(bad, tag + "every thing on furniture has a host and stands inside it", badHost == 0, badHost.ToString());
            FdV(bad, tag + "every thing on furniture rests on its host, not floating above it", badTopY == 0, badTopY.ToString());
            FdV(bad, tag + "wall things hang below the ceiling, and none in a corridor tops out above 1.95 m", badMountY == 0 && badCorr == 0, "ceiling " + badMountY + ", corridor " + badCorr);

            // --- what the other systems need
            var gb = FdGlassHung(b); var ga = FdGlassHung(a);
            bool glassOk = gb.Count == ga.Count;
            for (int i = 0; glassOk && i < gb.Count; i++) if (!gb[i] && ga[i]) glassOk = false;
            FdV(bad, tag + "no glass front is lost to the dressing", glassOk);
            var inp = a.input;
            int signsB = DirectionSigns.Plan(b, 7, inp.cols, inp.rows, inp.openE, inp.openN, inp.cell, inp.wallH, seed).Count;
            int signsA = DirectionSigns.Plan(a, 7, inp.cols, inp.rows, inp.openE, inp.openN, inp.cell, inp.wallH, seed).Count;
            FdV(bad, tag + "the direction signs are as many as before", signsA >= signsB, signsB + " -> " + signsA);
            int vb = ValuablePlan.FindSpots(b, seed).Count, va = ValuablePlan.FindSpots(a, seed).Count;
            var pvb = ValuablePlan.Plan(b, seed, 7, inp.cell, inp.cols, inp.rows); var pva = ValuablePlan.Plan(a, seed, 7, inp.cell, inp.cols, inp.rows);
            FdV(bad, tag + "the valuables: spots within 2% and as many planned as before", va >= vb * 0.98 && pva.Count >= pvb.Count, "spots " + vb + " -> " + va + ", planned " + pvb.Count + " -> " + pva.Count);
            int carryLost = 0;
            for (int i = 0; i < b.bigSlots.Count; i++)
                foreach (int lifters in new[] { 2, 3 })
                    if (LootPlan.RoomForCarry(b, b.bigSlots[i], lifters) && !LootPlan.RoomForCarry(a, a.bigSlots[i], lifters)) carryLost++;
            FdV(bad, tag + "every treasure spot keeps room to carry the loot out", carryLost == 0, carryLost.ToString());

            // --- the walk test: a round walker of 0.45 m (a guard) and 0.6 m (the planner's walking width), flooded from the patrol stops (every one: pockets) and from the first one only (the walk across the whole
            //     floor, from room to room): whatever it could reach before it can still reach, apart from the footprints of the new solids
            if (!walk) return;
            var seeds = b.waypoints.Select(w => new[] { w[0], w[1] }).ToList();
            var shadows = newSolid.Select(p => p.rc).ToList();
            foreach (float agent in new[] { 0.45f, 0.6f })
            {
                var fb = FdFlood(b, solidsB, agent, seeds); var fa = FdFlood(a, solidsA, agent, seeds);
                var gb2 = FdFromFirstStop(fb, b); var ga2 = FdFromFirstStop(fa, a);
                int lost = 0, lostAcross = 0, reachB = 0, reachA = 0;
                for (int z = 0; z < fb.nz; z++)
                    for (int x = 0; x < fb.nx; x++)
                    {
                        int k = z * fb.nx + x;
                        if (fb.seen[k]) reachB++;
                        if (fa.seen[k]) reachA++;
                        bool cutPocket = fb.seen[k] && !fa.seen[k], cutAcross = gb2.seen[k] && !ga2.seen[k];
                        if (!cutPocket && !cutAcross) continue;
                        float cx = (x + 0.5f) * FdWalk.Res, cz = (z + 0.5f) * FdWalk.Res;
                        bool shadow = false;
                        foreach (var s in shadows) if (s.Grow(agent + 0.11f).Contains(cx, cz)) { shadow = true; break; }
                        if (shadow) continue;
                        if (cutPocket) lost++;
                        if (cutAcross) lostAcross++;
                    }
                FdV(bad, tag + "walk test, " + FdF(agent) + " m walker: nothing that was reachable is cut off, and every room can still be walked to from the first stop", lost == 0 && lostAcross == 0,
                    "pockets lost " + lost + ", across the floor lost " + lostAcross + "; reachable " + reachB + " -> " + reachA);
            }
            var f45b = FdFlood(b, solidsB, 0.45f, seeds); var f45a = FdFlood(a, solidsA, 0.45f, seeds);
            var s45b = FdFromFirstStop(f45b, b); var s45a = FdFromFirstStop(f45a, a);
            int wpLost = 0;
            foreach (var w in b.waypoints)
            {
                int x = (int)(w[0] / FdWalk.Res), z = (int)(w[1] / FdWalk.Res);
                if (x < 0 || z < 0 || x >= f45b.nx || z >= f45b.nz) continue;
                int k = z * f45b.nx + x;
                if ((f45b.seen[k] && !f45a.seen[k]) || (s45b.seen[k] && !s45a.seen[k])) wpLost++;
            }
            FdV(bad, tag + "every patrol stop is still reachable, from its own spot and from the first stop", wpLost == 0, wpLost.ToString());
        }

        // ------------------------------------------------------------------ the audit audits itself: a floor with something wrong on purpose has to fail it

        static Piece FdInject(Layout a, Rc rc, float h, bool solid)
        {
            var p = new Piece { key = "file_cabinet", x = rc.Cx, z = rc.Cz, y = 0f, h = h, rc = rc, solid = solid, dressing = true, room = "" };
            a.pieces.Add(p);
            return p;
        }

        public static void FloorDressAuditSelfTest()
        {
            bool saved = OfficePlanner.DressingOn;
            try
            {
                const int seed = 777;
                // (1) a tall solid cabinet by itself in the middle of a corridor
                var b = FdMake(seed, 7, 6, 4, false); var a = FdMake(seed, 7, 6, 4, true);
                RoomInfo hall = null;
                foreach (var r in a.rooms) if (r.kind == 2 && r.blocks.Count == 0 && r.theme == "hall") { hall = r; break; }
                var bad = new List<string>();
                if (hall != null) FdInject(a, Rc.Centered(hall.bounds.Cx, hall.bounds.Cz, 0.5f, 0.5f), 1.4f, true);
                FdAudit("", b, a, seed, false, bad);
                Check("floor dressing audit: it notices a tall cabinet standing by itself in a corridor", hall != null && bad.Any(n => n.Contains("flush")), string.Join(" | ", bad));

                // (2) a solid thing on a patrol stop
                a = FdMake(seed, 7, 6, 4, true); bad = new List<string>();
                var wp = a.waypoints[a.waypoints.Count / 2];
                FdInject(a, Rc.Centered(wp[0], wp[1], 0.5f, 0.5f), 0.9f, true);
                FdAudit("", b, a, seed, false, bad);
                Check("floor dressing audit: it notices a solid thing on a patrol stop", bad.Any(n => n.Contains("patrol stops")), string.Join(" | ", bad));

                // (3) a solid thing in a doorway
                a = FdMake(seed, 7, 6, 4, true); bad = new List<string>();
                RoomInfo shut = null;
                foreach (var r in a.rooms) if (r.kind == 0 && r.theme != "restroom" && r.theme != "outpost" && r.doors.Count == 1 && r.bounds.W > 3f) { shut = r; break; }
                if (shut != null) FdInject(a, shut.doors[0].Grow(0.5f), 1.0f, true);
                FdAudit("", b, a, seed, true, bad);
                Check("floor dressing audit: it notices a doorway with a cabinet in it (a room with one door cut off)", shut != null && bad.Any(n => n.Contains("walk test")), string.Join(" | ", bad));

                // (4) a thing on a desk that floats above it
                a = FdMake(seed, 7, 6, 4, true); bad = new List<string>();
                Piece host = null;
                foreach (var p in a.pieces) if (!p.dressing && p.key.StartsWith("desk_") && p.solid) { host = p; break; }
                if (host != null) a.pieces.Add(new Piece { key = "book_pile_1", x = host.x, z = host.z, y = host.y + host.h + 0.5f, h = 0.2f, rc = Rc.Centered(host.x, host.z, 0.3f, 0.3f), onTop = true, host = host, dressing = true, room = "" });
                FdAudit("", b, a, seed, false, bad);
                Check("floor dressing audit: it notices a thing floating above its desk", host != null && bad.Any(n => n.Contains("not floating")), string.Join(" | ", bad));
            }
            finally { OfficePlanner.DressingOn = saved; }
        }
    }
}
