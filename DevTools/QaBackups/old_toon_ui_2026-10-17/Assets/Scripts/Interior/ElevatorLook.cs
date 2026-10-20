using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>
    /// What the inside of the service elevator looks like, as plain numbers (no Unity types, so it can be tested outside Unity).
    /// The car is dressed like a real passenger elevator: wood-veneer wall panels with dark seams, a cream lower wall, a stainless
    /// kick plate, chair rail and handrail all round, stainless door jambs and sills, a carpet floor and a wood ceiling with round
    /// downlights. The wall opposite the floor panel is a mirror (its reflection is made by <c>PlanarMirror</c>; this class only says where
    /// the mirror is and keeps everything else clear of it). Everything is axis-aligned boxes in car coordinates: the origin is the
    /// middle of the car on the floor, x east, z north, y up, the inner faces of the walls are at +-<see cref="Half"/>.
    /// <see cref="CarMesh"/> turns the plan into mesh data, one mesh per material.
    /// </summary>
    public static class ElevatorLook
    {
        public enum Side { East, West, North, South }

        /// <summary>The materials of the car, in the order of <see cref="CarMesh.parts"/>.</summary>
        public const int Wood = 0, Cream = 1, Steel = 2, Carpet = 3, Seam = 4, Lens = 5, Ceiling = 6, MaterialCount = 7;

        /// <summary>The car is 7.6 m by 7.6 m inside and as tall as a floor; the doorway is 3 m wide.</summary>
        public const float Half = 3.8f, DoorHalf = 1.5f, Height = 3.5f;
        /// <summary>Where the floor-button panel and its GO button hang on the panel wall: distance along the wall from the middle (towards the front door).</summary>
        public const float PanelAlong = 2.9f, GoAlong = PanelAlong - 1.1f;   // (the GO button hangs 1.1 m beside the panel, see ElevatorPanel)
        /// <summary>Half the width of the panel plate and of the GO plate (the rail stops short of them, plus a margin).</summary>
        public const float PanelHalfWidth = 0.7f, GoHalfWidth = 0.33f, PanelMargin = 0.1f;

        public const float CarpetTop = 0.04f, KickHeight = 0.12f, BandLow = 0.90f, BandHigh = 1.08f;
        /// <summary>How far the wall covering stands out of the wall, and the rail band and the bar on it.</summary>
        public const float ClothDepth = 0.03f, BandDepth = 0.045f, BarDepth = 0.10f;
        public const float CeilingThickness = 0.12f;
        public const float JambWidth = 0.14f, PostWidth = 0.20f, JambDepth = 0.09f;
        public const float WoodWidth = 1.25f, SeamWidth = 0.025f, SeamDepth = 0.012f;
        public const float DiscRadius = 0.15f, RingRadius = 0.20f;
        /// <summary>The mirror runs from just above the kick plate to a steel strip under the ceiling.</summary>
        public const float MirrorBottom = KickHeight, TopStrip = 0.10f;
        /// <summary>Wall thickness of the car's walls (the door gap goes through it).</summary>
        public const float WallThickness = 0.4f;

        public static Side Opposite(Side s)
        {
            switch (s)
            {
                case Side.East: return Side.West;
                case Side.West: return Side.East;
                case Side.North: return Side.South;
                default: return Side.North;
            }
        }

        /// <summary>The direction pointing into the car from that wall, in x and z.</summary>
        public static void Inward(Side s, out float nx, out float nz)
        {
            nx = s == Side.East ? -1f : s == Side.West ? 1f : 0f;
            nz = s == Side.North ? -1f : s == Side.South ? 1f : 0f;
        }

        /// <summary>One box of the car: x0..x1, y0..y1, z0..z1, its material and where in the wood texture it starts.</summary>
        public sealed class CarBox
        {
            public float x0, y0, z0, x1, y1, z1;
            public int mat;
            /// <summary>Shifts the texture sideways (each panel of veneer has its own grain).</summary>
            public float uOff;
            public string name = "";
        }

        /// <summary>A flat round disc facing down (a downlight's lens or its ring).</summary>
        public sealed class Disc
        {
            public float x, y, z, radius;
            public int mat;
        }

        public struct Spot
        {
            public float x, y, z;
        }

        /// <summary>The mirror: a flat rectangle on <see cref="wall"/>, its middle, its size, and the direction it faces (into the car).</summary>
        public sealed class Mirror
        {
            public Side wall;
            public float cx, cy, cz, width, height, nx, nz;
        }

        public sealed class Input
        {
            public float half = Half, height = Height, doorHalf = DoorHalf;
            /// <summary>The wall with the front door.</summary>
            public Side front = Side.East;
            /// <summary>The wall with the locked back door (floor 12), or -1.</summary>
            public int back = -1;
            /// <summary>The wall with the floor panel and the GO button.</summary>
            public Side panelWall = Side.North;
            /// <summary>The wall opposite the floor panel is a mirror.</summary>
            public bool mirror = true;
            public bool downlights = true;
            public int seed;
        }

        public sealed class Plan
        {
            public readonly List<CarBox> boxes = new List<CarBox>();
            public readonly List<Disc> discs = new List<Disc>();
            /// <summary>Where the real lights hang (in the ceiling, over the corner downlights).</summary>
            public readonly List<Spot> lamps = new List<Spot>();
            /// <summary>Null when there is no mirror.</summary>
            public Mirror mirror;
            /// <summary>Along the panel wall, where the rail stops (the floor panel and the GO button take the rest).</summary>
            public float railEnd;
        }

        /// <summary>How far along the panel wall the rail may run before it would pass in front of the floor panel / GO button.</summary>
        public static float RailEnd => Math.Min(PanelAlong - PanelHalfWidth, GoAlong - GoHalfWidth) - PanelMargin;

        public static Plan Build(Input inp)
        {
            var plan = new Plan { railEnd = RailEnd };
            float H = inp.half, dh = inp.doorHalf, height = inp.height;
            float ceilB = height - CeilingThickness;
            int id = 0;

            CarBox Wall(Side s, float u0, float u1, float d0, float d1, float y0, float y1, int mat, string name, float uOff = 0f)
            {
                float a = Math.Min(u0, u1), b = Math.Max(u0, u1), lo = Math.Min(d0, d1), hi = Math.Max(d0, d1);
                var box = new CarBox { y0 = y0, y1 = y1, mat = mat, uOff = uOff, name = name };
                switch (s)
                {
                    case Side.East: box.x0 = H - hi; box.x1 = H - lo; box.z0 = a; box.z1 = b; break;
                    case Side.West: box.x0 = -H + lo; box.x1 = -H + hi; box.z0 = a; box.z1 = b; break;
                    case Side.North: box.z0 = H - hi; box.z1 = H - lo; box.x0 = a; box.x1 = b; break;
                    default: box.z0 = -H + lo; box.z1 = -H + hi; box.x0 = a; box.x1 = b; break;
                }
                plan.boxes.Add(box);
                return box;
            }

            float Grain() => AfterHours.Roll(inp.seed, id++, 0x57A1);

            // ---- floor and ceiling
            plan.boxes.Add(new CarBox { x0 = -H, x1 = H, z0 = -H, z1 = H, y0 = 0f, y1 = CarpetTop, mat = Carpet, name = "Carpet" });
            {
                // the ceiling: three strips of wood with a dark seam between them
                float third = H * 2f / 3f;
                for (int i = 0; i < 3; i++)
                {
                    float a = -H + i * third, b = a + third;
                    float ga = i > 0 ? SeamWidth * 0.5f : 0f, gb = i < 2 ? SeamWidth * 0.5f : 0f;
                    plan.boxes.Add(new CarBox { x0 = a + ga, x1 = b - gb, z0 = -H, z1 = H, y0 = ceilB, y1 = height, mat = Ceiling, uOff = Grain(), name = "CeilingWood" });
                    if (i < 2)
                        plan.boxes.Add(new CarBox { x0 = b - SeamWidth * 0.5f, x1 = b + SeamWidth * 0.5f, z0 = -H, z1 = H, y0 = ceilB, y1 = height, mat = Seam, name = "CeilingSeam" });
                }
                if (inp.downlights)
                {
                    float p = H * 2f / 3f;
                    foreach (float x in new[] { -p, 0f, p })
                        foreach (float z in new[] { -p, 0f, p })
                        {
                            plan.discs.Add(new Disc { x = x, y = ceilB - 0.003f, z = z, radius = RingRadius, mat = Steel });
                            plan.discs.Add(new Disc { x = x, y = ceilB - 0.006f, z = z, radius = DiscRadius, mat = Lens });
                            if (x != 0f && z != 0f) plan.lamps.Add(new Spot { x = x, y = ceilB - 0.05f, z = z });
                        }
                }
            }

            // ---- the four walls
            var mirrorWall = Opposite(inp.panelWall);
            foreach (Side s in new[] { Side.East, Side.West, Side.North, Side.South })
            {
                bool hasDoor = s == inp.front || (inp.back >= 0 && s == (Side)inp.back);
                bool isPanel = s == inp.panelWall;
                bool isMirror = inp.mirror && s == mirrorWall && !hasDoor;

                if (hasDoor)
                {
                    // stainless jambs on both sides of the doorway, a sill across it, and a frame round the doorway on the other side of the wall
                    foreach (int sign in new[] { -1, 1 })
                    {
                        float a = sign * dh, b = sign * (dh + JambWidth);
                        Wall(s, a, b, 0f, JambDepth, 0f, ceilB, Steel, "Jamb");
                        Wall(s, a, b, -WallThickness - 0.07f, -WallThickness, 0f, height, Steel, "DoorFrame");
                    }
                    Wall(s, -dh, dh, -WallThickness, 0.10f, 0f, CarpetTop + 0.006f, Steel, "Sill");
                }

                if (isMirror)
                {
                    float ua = -H + PostWidth, ub = H - PostWidth;
                    Wall(s, -H, H, 0f, 0.05f, 0f, KickHeight, Steel, "Kick");
                    Wall(s, -H, -H + PostWidth, 0f, 0.06f, KickHeight, ceilB, Steel, "MirrorPost");
                    Wall(s, H - PostWidth, H, 0f, 0.06f, KickHeight, ceilB, Steel, "MirrorPost");
                    float mirrorTop = ceilB - TopStrip;
                    Wall(s, ua, ub, 0f, 0.06f, mirrorTop, ceilB, Steel, "MirrorTop");
                    // the handrail is mounted on the mirror
                    Wall(s, ua, ub, ClothDepth, ClothDepth + 0.055f, 0.96f, 1.02f, Steel, "Rail");
                    var m = Wall(s, ua, ub, ClothDepth, ClothDepth, MirrorBottom, mirrorTop, Steel, "MirrorFace");
                    plan.boxes.Remove(m);
                    Inward(s, out float nx, out float nz);
                    plan.mirror = new Mirror
                    {
                        wall = s, width = ub - ua, height = mirrorTop - MirrorBottom, nx = nx, nz = nz,
                        cx = (m.x0 + m.x1) * 0.5f, cy = (MirrorBottom + mirrorTop) * 0.5f, cz = (m.z0 + m.z1) * 0.5f
                    };
                    continue;
                }

                var spans = new List<float[]>();
                if (hasDoor) { spans.Add(new[] { -H, -dh - JambWidth }); spans.Add(new[] { dh + JambWidth, H }); }
                else spans.Add(new[] { -H, H });

                foreach (var span in spans)
                {
                    float a = span[0], b = span[1];
                    Wall(s, a, b, 0f, 0.04f, 0f, KickHeight, Steel, "Kick");
                    Wall(s, a, b, 0f, ClothDepth, KickHeight, BandLow, Cream, "Cream");
                    float railTo = isPanel ? Math.Min(b, plan.railEnd) : b;
                    if (railTo - a > 0.2f)
                    {
                        Wall(s, a, railTo, 0f, BandDepth, BandLow, BandHigh, Steel, "Band");
                        Wall(s, a, railTo, BandDepth, BarDepth, 0.96f, 1.02f, Steel, "Rail");
                    }
                    int n = Math.Max(1, (int)Math.Round((b - a) / WoodWidth));
                    float w = (b - a) / n;
                    for (int i = 0; i < n; i++)
                    {
                        float pa = a + i * w, pb = pa + w;
                        float ga = i > 0 ? SeamWidth * 0.5f : 0f, gb = i < n - 1 ? SeamWidth * 0.5f : 0f;
                        Wall(s, pa + ga, pb - gb, 0f, ClothDepth, BandHigh, ceilB, Wood, "Wood", Grain());
                        if (i < n - 1) Wall(s, pb - SeamWidth * 0.5f, pb + SeamWidth * 0.5f, 0f, SeamDepth, BandHigh, ceilB, Seam, "Seam");
                    }
                }
            }
            return plan;
        }
    }

    /// <summary>
    /// The mesh data of the car, one list per material: boxes (all six faces) and flat discs, with normals and texture coordinates that follow
    /// the world (a metre of wall is the same amount of wood everywhere, so the grain runs straight up the walls). All faces are wound so the
    /// side that should be seen is the front (checked by the tests).
    /// </summary>
    public sealed class CarMesh
    {
        /// <summary>The wood texture shows this much wall: this many metres across and up.</summary>
        public const float MetresPerTextureU = 1.0f, MetresPerTextureV = 2.4f;
        public const int DiscSides = 20;

        public sealed class Part
        {
            public readonly List<float> vertices = new List<float>();
            public readonly List<float> normals = new List<float>();
            public readonly List<float> uvs = new List<float>();
            public readonly List<int> triangles = new List<int>();
            public int VertexCount => vertices.Count / 3;
        }

        public readonly Part[] parts = new Part[ElevatorLook.MaterialCount];

        public CarMesh()
        {
            for (int i = 0; i < parts.Length; i++) parts[i] = new Part();
        }

        public static CarMesh From(ElevatorLook.Plan plan)
        {
            var m = new CarMesh();
            foreach (var b in plan.boxes) m.Add(b);
            foreach (var d in plan.discs) m.Add(d);
            return m;
        }

        public void Add(ElevatorLook.CarBox b)
        {
            var p = parts[b.mat];
            float u = b.uOff;
            // +x, -x, +y, -y, +z, -z
            Face(p, u, b.x1, b.y0, b.z0, b.x1, b.y0, b.z1, b.x1, b.y1, b.z1, b.x1, b.y1, b.z0, 1, 0, 0);
            Face(p, u, b.x0, b.y0, b.z1, b.x0, b.y0, b.z0, b.x0, b.y1, b.z0, b.x0, b.y1, b.z1, -1, 0, 0);
            Face(p, u, b.x0, b.y1, b.z0, b.x0, b.y1, b.z1, b.x1, b.y1, b.z1, b.x1, b.y1, b.z0, 0, 1, 0);
            Face(p, u, b.x0, b.y0, b.z1, b.x0, b.y0, b.z0, b.x1, b.y0, b.z0, b.x1, b.y0, b.z1, 0, -1, 0);
            Face(p, u, b.x1, b.y0, b.z1, b.x0, b.y0, b.z1, b.x0, b.y1, b.z1, b.x1, b.y1, b.z1, 0, 0, 1);
            Face(p, u, b.x0, b.y0, b.z0, b.x1, b.y0, b.z0, b.x1, b.y1, b.z0, b.x0, b.y1, b.z0, 0, 0, -1);
        }

        /// <summary>A flat disc facing down.</summary>
        public void Add(ElevatorLook.Disc d)
        {
            var p = parts[d.mat];
            int centre = p.VertexCount;
            Vertex(p, 0f, d.x, d.y, d.z, 0, -1, 0);
            for (int i = 0; i < DiscSides; i++)
            {
                double a = i * 2.0 * Math.PI / DiscSides;
                Vertex(p, 0f, d.x + (float)Math.Cos(a) * d.radius, d.y, d.z + (float)Math.Sin(a) * d.radius, 0, -1, 0);
            }
            for (int i = 0; i < DiscSides; i++)
            {
                int a = centre + 1 + i, b = centre + 1 + (i + 1) % DiscSides;
                // (centre, a, b) faces up when the rim runs anticlockwise seen from above; the cross product tells
                float ux = p.vertices[a * 3] - p.vertices[centre * 3], uz = p.vertices[a * 3 + 2] - p.vertices[centre * 3 + 2];
                float vx = p.vertices[b * 3] - p.vertices[centre * 3], vz = p.vertices[b * 3 + 2] - p.vertices[centre * 3 + 2];
                float qy = uz * vx - ux * vz;                 // y of cross((a - c), (b - c))
                if (qy < 0f) { p.triangles.Add(centre); p.triangles.Add(a); p.triangles.Add(b); }
                else { p.triangles.Add(centre); p.triangles.Add(b); p.triangles.Add(a); }
            }
        }

        /// <summary>A quad given by its four corners in order and the direction it must face; wound so that side is the front.</summary>
        static void Face(Part p, float uOff, float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz, float dx, float dy, float dz,
                         float nx, float ny, float nz)
        {
            float ux = bx - ax, uy = by - ay, uz = bz - az, vx = cx - ax, vy = cy - ay, vz = cz - az;
            float qx = uy * vz - uz * vy, qy = uz * vx - ux * vz, qz = ux * vy - uy * vx;
            bool flip = qx * nx + qy * ny + qz * nz < 0f;

            int i = p.VertexCount;
            Vertex(p, uOff, ax, ay, az, nx, ny, nz); Vertex(p, uOff, bx, by, bz, nx, ny, nz);
            Vertex(p, uOff, cx, cy, cz, nx, ny, nz); Vertex(p, uOff, dx, dy, dz, nx, ny, nz);
            var t = p.triangles;
            if (!flip) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
            else { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
        }

        static void Vertex(Part p, float uOff, float x, float y, float z, float nx, float ny, float nz)
        {
            p.vertices.Add(x); p.vertices.Add(y); p.vertices.Add(z);
            p.normals.Add(nx); p.normals.Add(ny); p.normals.Add(nz);
            // texture coordinates follow the world: along the wall (x or z) and up (y; z on a ceiling or floor)
            float along = Math.Abs(nx) > 0.5f ? z : x;
            float up = Math.Abs(ny) > 0.5f ? z : y;
            p.uvs.Add(along / MetresPerTextureU + uOff);
            p.uvs.Add(up / MetresPerTextureV);
        }
    }
}
