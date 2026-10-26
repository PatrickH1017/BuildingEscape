using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Glass in the walls of the offices (the owner, 2026-10-17: "make some offices, especially corner offices, have see through glass walls" - "by see through walls I mean glass windows"):
    /// the wall of an office or a meeting room that looks into a corridor gets a long glass window - a solid base and head, steel mullions, faint bluish panes - so you can see in from the
    /// corridor and out of the room. The offices at the edge of the building (and the ones in a corner of it) nearly always have one and theirs reaches nearly from floor to ceiling; the
    /// offices inside the building have one now and then. Cosmetic only, like the windows in the outside walls: the wall's collider stays, so walking, shooting and what the guards can see
    /// do not change. A failure puts the plain walls back.
    /// </summary>
    public partial class FloorGenerator
    {
        /// <summary>For tests and pictures: how many glass panes, how many offices have a glass front, how many of those are in a corner of the building, and where the fronts are.</summary>
        public int GlassPanes { get; private set; }
        public int GlassOffices { get; private set; }
        public int CornerOffices { get; private set; }
        public int GlassWallCount => windowedWalls.Count;
        /// <summary>How many of the doors of those offices were made of glass too, and the rooms that have a glass front.</summary>
        public int GlassDoors { get; private set; }
        /// <summary>The rooms that have a glass front (for the tests).</summary>
        public IReadOnlyList<Rc> GlassRoomBounds => glassRooms;
        readonly List<Rc> glassRooms = new List<Rc>();
        public readonly List<(Vector3 centre, Vector3 outward)> GlassFronts = new List<(Vector3, Vector3)>();

        readonly HashSet<WallBox> windowedWalls = new HashSet<WallBox>();
        /// <summary>For each glass front: the step from the wall towards the corridor (a door leaf slides in front of the glass on that side, see DoorSlide).</summary>
        readonly Dictionary<WallBox, Vector3> windowedOutward = new Dictionary<WallBox, Vector3>();
        readonly Dictionary<WallBox, GameObject> wallObjects = new Dictionary<WallBox, GameObject>();

        static bool GlassFriendly(string theme) => theme == "office" || theme == "conference" || theme == "meeting" || theme == "training";

        /// <summary>How many of the building's outside walls a rectangle touches (the four real corners of a floor are the lifts and stairs, so 2 is rare).</summary>
        int OutsideSides(Rc b)
        {
            float width = cols * cellSize, depth = rows * cellSize;
            int n = 0;
            if (b.x0 < 0.7f) n++;
            if (b.x1 > width - 0.7f) n++;
            if (b.z0 < 0.7f) n++;
            if (b.z1 > depth - 0.7f) n++;
            return n;
        }

        static bool InCorridor(Layout L, float x, float z)
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

        /// <summary>Does this wall run along the edge of the room with a corridor on the other side? <paramref name="outward"/> is the step from the wall towards the corridor.</summary>
        static bool FrontOf(WallBox w, Rc b, Layout L, out Vector3 outward)
        {
            outward = Vector3.zero;
            var r = w.rc;
            bool alongX = r.W > r.D;
            float overlap = alongX ? Mathf.Min(r.x1, b.x1) - Mathf.Max(r.x0, b.x0) : Mathf.Min(r.z1, b.z1) - Mathf.Max(r.z0, b.z0);
            if (overlap < 1.2f) return false;
            if (alongX)
            {
                if (Mathf.Abs(r.Cz - b.z0) < 0.4f) outward = Vector3.back;
                else if (Mathf.Abs(r.Cz - b.z1) < 0.4f) outward = Vector3.forward;
                else return false;
            }
            else
            {
                if (Mathf.Abs(r.Cx - b.x0) < 0.4f) outward = Vector3.left;
                else if (Mathf.Abs(r.Cx - b.x1) < 0.4f) outward = Vector3.right;
                else return false;
            }
            return InCorridor(L, r.Cx + outward.x * 0.9f, r.Cz + outward.z * 0.9f);
        }

        static void WallPart(MeshBuilder m, bool alongX, float t0, float t1, float centre, float half, float y0, float y1)
        {
            if (t1 - t0 < 0.005f || y1 - y0 < 0.005f) return;
            if (alongX) m.Box(new Vector3(t0, y0, centre - half), new Vector3(t1, y1, centre + half));
            else m.Box(new Vector3(centre - half, y0, t0), new Vector3(centre + half, y1, t1));
        }

        /// <summary>One glass front in a wall: piers at the ends, a sill and a head, steel frame and mullions, panes of glass between them. False if the wall is too short for one.</summary>
        bool AddGlassFront(MeshBuilder solid, MeshBuilder frame, MeshBuilder glass, WallBox w, Vector3 outward, bool grand)
        {
            var r = w.rc;
            bool alongX = r.W > r.D;
            float lo = alongX ? r.x0 : r.z0, hi = alongX ? r.x1 : r.z1, centre = alongX ? r.Cz : r.Cx, half = (alongX ? r.D : r.W) * 0.5f;
            const float pier = 0.35f;
            float a = lo + pier, b = hi - pier;
            if (b - a < 1.0f) return false;
            float sill = grand ? 0.10f : 0.90f, head = grand ? 3.05f : 2.45f, top = w.y1;
            WallPart(solid, alongX, lo, a, centre, half, 0f, top);                 // the two piers
            WallPart(solid, alongX, b, hi, centre, half, 0f, top);
            WallPart(solid, alongX, a, b, centre, half, 0f, sill);                 // the sill and the head
            WallPart(solid, alongX, a, b, centre, half, head, top);

            float fd = half + 0.012f;
            const float fw = 0.05f;
            WallPart(frame, alongX, a, a + fw, centre, fd, sill, head);            // the jambs
            WallPart(frame, alongX, b - fw, b, centre, fd, sill, head);
            WallPart(frame, alongX, a, b, centre, fd, sill, sill + 0.05f);         // the rails
            WallPart(frame, alongX, a, b, centre, fd, head - 0.05f, head);
            int panes = Mathf.Max(1, Mathf.CeilToInt((b - a) / 1.35f));
            float pane = (b - a) / panes;
            for (int i = 1; i < panes; i++) WallPart(frame, alongX, a + i * pane - 0.025f, a + i * pane + 0.025f, centre, fd, sill, head);
            if (!grand) WallPart(frame, alongX, a, b, centre, fd, 1.55f, 1.60f);   // a transom bar across the window

            float y0 = sill + 0.05f, y1 = head - 0.05f;
            for (int i = 0; i < panes; i++)
            {
                float p0 = a + i * pane + 0.03f, p1 = a + (i + 1) * pane - 0.03f;
                Vector3 bl, tl, tr, br;
                if (alongX) { bl = new Vector3(p0, y0, centre); tl = new Vector3(p0, y1, centre); tr = new Vector3(p1, y1, centre); br = new Vector3(p1, y0, centre); }
                else { bl = new Vector3(centre, y0, p0); tl = new Vector3(centre, y1, p0); tr = new Vector3(centre, y1, p1); br = new Vector3(centre, y0, p1); }
                glass.QuadUV(bl, tl, tr, br, outward, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));       // (the glass material shows both sides)
            }
            GlassPanes += panes;
            GlassFronts.Add((new Vector3(r.Cx, 1.6f, r.Cz), outward));
            glassSpans.Add(new WindowSpan { alongX = alongX, line = centre, half = half, lo = lo, hi = hi, a = a, b = b, sill = sill, head = head, outwardSign = alongX ? outward.z : outward.x });          // (ClearViews keeps these clear)
            return true;
        }

        // ------------------------------------------------------------------ glass doors for the offices with glass fronts

        Mesh glassLeafMesh;

        /// <summary>Is this doorway (the beam over it) the door of an office that has a glass front?</summary>
        bool InGlassOffice(WallBox beam)
        {
            foreach (var b in glassRooms) if (b.Grow(0.3f).Contains(beam.rc.Cx, beam.rc.Cz)) return true;
            return false;
        }

        /// <summary>
        /// The frame of a glass door's leaf, in the leaf's own unit cube (x across, y -0.5 = the floor to +0.5 = the top, z the thickness): a kick plate, a top rail and a narrow stile on each side,
        /// with ONE big opening between them. (Public and static so that a test can look through it.)
        /// </summary>
        public static Mesh MakeGlassLeafMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.42f, 0.5f));          // the kick plate
            b.Box(new Vector3(-0.5f, 0.44f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f));            // the top rail
            b.Box(new Vector3(-0.5f, -0.42f, -0.5f), new Vector3(-0.4f, 0.44f, 0.5f));         // the stiles
            b.Box(new Vector3(0.4f, -0.42f, -0.5f), new Vector3(0.5f, 0.44f, 0.5f));
            var m = b.ToMesh("GlassLeaf");
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }

        /// <summary>A door of an office with a glass front: both leaves are frames with one big opening and a faint pane in it, on both faces (decoration: a failure leaves the plain door).</summary>
        void DressGlassDoor(SlidingDoor door)
        {
            try
            {
                var frame = glassLeafMesh ? glassLeafMesh : (glassLeafMesh = MakeGlassLeafMesh());
                door.LowLeaf.GetComponent<MeshFilter>().sharedMesh = frame;
                door.HighLeaf.GetComponent<MeshFilter>().sharedMesh = frame;
                var glass = HallGlassMat();
                for (int side = -1; side <= 1; side += 2)
                {
                    var leaf = side < 0 ? door.LowLeaf : door.HighLeaf;
                    for (int face = -1; face <= 1; face += 2) door.AddDecor(side, DoorPicture(leaf, face, 0f, 0.01f, 0.80f, 0.86f, 0.03f, glass));
                }
                GlassDoors++;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Glass] Could not make an office's door of glass (it stays a plain one): " + e.Message);
            }
        }

        void RestoreWindowedWalls()
        {
            foreach (var w in windowedWalls)
                if (wallObjects.TryGetValue(w, out var go) && go) { var rr = go.GetComponent<Renderer>(); if (rr) rr.enabled = true; }
            windowedWalls.Clear();
            glassSpans.Clear();
        }

        /// <summary>Glass fronts for the offices and meeting rooms that get them (see the class summary). Runs before the accent walls (a window is not painted).</summary>
        void BuildOfficeWindows(Transform interior, Layout L)
        {
            GlassPanes = 0; GlassOffices = 0; CornerOffices = 0; GlassDoors = 0;
            GlassFronts.Clear();
            glassSpans.Clear();
            glassRooms.Clear();
            windowedWalls.Clear();
            windowedOutward.Clear();
            var solid = new MeshBuilder(); var frame = new MeshBuilder(); var glass = new MeshBuilder();
            var op = L.outpost;
            var opArea = op != null ? op.outer.Grow(0.02f) : default(Rc);
            var signs = PlanSigns();
            foreach (var room in L.rooms)
            {
                if (room.kind != 0 || !GlassFriendly(room.theme)) continue;
                if (op != null && ReferenceEquals(op.room, room)) continue;
                var rb = room.bounds;
                int outside = OutsideSides(rb);
                float chance = room.role == OfficePlanner.RoleSuite ? 1f : outside >= 2 ? 1f : outside == 1 ? 0.6f : 0.25f;      // (an executive suite always has its glass front)
                if (SurfHash(Mathf.RoundToInt(rb.Cx * 4f) + 17, Mathf.RoundToInt(rb.Cz * 4f) + 5) >= chance) continue;
                int fronts = 0;
                foreach (var w in L.walls)
                {
                    if (w.kind != "wall" || w.y0 > 0.01f || w.y1 < wallHeight - 0.01f || windowedWalls.Contains(w)) continue;
                    var r = w.rc;
                    if ((r.W > r.D ? r.D : r.W) > 0.45f || (r.W > r.D ? r.W : r.D) < 1.6f) continue;
                    if (op != null && r.x0 >= opArea.x0 && r.x1 <= opArea.x1 && r.z0 >= opArea.z0 && r.z1 <= opArea.z1) continue;      // the outpost's own walls
                    if (!FrontOf(w, rb, L, out var outward)) continue;
                    // nothing hangs on the wall where the window would be: no picture, board or TV on the room's side, no direction sign on the corridor's
                    var near = r.Grow(0.5f);
                    bool hung = false;
                    foreach (var p in L.pieces) if (p.mount && near.Overlaps(p.rc)) { hung = true; break; }
                    if (!hung && signs != null) { var nearSign = r.Grow(0.3f); foreach (var sgn in signs) if (nearSign.Contains(sgn.x, sgn.z)) { hung = true; break; } }
                    if (hung && room.role == OfficePlanner.RoleSuite)
                    {
                        L.pieces.RemoveAll(p => p.mount && near.Overlaps(p.rc));              // an executive suite always gets its glass front: the pictures and boards on that bit of wall come down
                        hung = false;
                        foreach (var sgn in signs) if (r.Grow(0.3f).Contains(sgn.x, sgn.z)) { hung = true; break; }      // (a direction sign on the corridor side stays: look for another wall)
                    }
                    if (hung) continue;
                    if (!wallObjects.TryGetValue(w, out var wallGo) || !wallGo) continue;
                    if (AddGlassFront(solid, frame, glass, w, outward, outside >= 1 || room.role == OfficePlanner.RoleSuite)) { windowedWalls.Add(w); windowedOutward[w] = outward; fronts++; }
                }
                if (fronts > 0) { GlassOffices++; glassRooms.Add(rb); if (outside >= 2) CornerOffices++; }
            }
            if (windowedWalls.Count == 0) return;

            var holder = new GameObject("OfficeWindows").transform;
            holder.SetParent(interior, false);
            var solidMesh = solid.ToMesh("OfficeWindowWalls");
            var frameMesh = frame.ToMesh("OfficeWindowFrames");
            var glassMesh = glass.ToMesh("OfficeWindowGlass");
            if (solidMesh != null) MeshObject(holder, "WindowWalls", solidMesh, PartitionMat() ? PartitionMat() : wallMat, false);
            if (frameMesh != null) MeshObject(holder, "WindowFrames", frameMesh, SurfaceMat("glass_frame", Texture2D.whiteTexture, Rgb(0.10f, 0.11f, 0.13f), 0.45f, 0.5f), false);
            if (glassMesh != null) MeshObject(holder, "WindowGlass", glassMesh, HallGlassMat(), false);
            foreach (var w in windowedWalls)
                if (wallObjects.TryGetValue(w, out var go) && go) { var rr = go.GetComponent<Renderer>(); if (rr) rr.enabled = false; }      // (the collider stays)
        }
    }
}
