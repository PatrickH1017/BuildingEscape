using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>One alarm beacon: the spot of the wall it is mounted on (x, z on the floor plan: where its back touches the wall) and the way it faces (yaw in degrees, 0 = +z: straight out of the wall into the room).</summary>
    public sealed class BeaconSpot
    {
        public float x, z, yaw;
        /// <summary>True for a beacon in a hallway.</summary>
        public bool hall;
    }

    /// <summary>One face of a wall (what a room sees of it): the line from (x0, z0) to (x1, z1) along the wall, and the unit direction (nx, nz) that points out of the wall into the room.</summary>
    public struct WallFace
    {
        public float x0, z0, x1, z1, nx, nz;
        public WallFace(float x0, float z0, float x1, float z1, float nx, float nz) { this.x0 = x0; this.z0 = z0; this.x1 = x1; this.z1 = z1; this.nx = nx; this.nz = nz; }
    }

    /// <summary>
    /// The alarm beacons (the owner, 2026-10-24: "when the big alarm is activated there should be red lights activated that spin in circles (similar to a police car light)"; and, after the first build, "they are spawning in the middle of rooms ... instead they should be placed high up on the walls
    /// sporadically (touching the wall)"), without Unity types so it is tested outside Unity: where they hang and how they spin. A beacon is mounted on a wall face of a hallway or of a big room (the owner's answer: "hallway corners + big rooms"), high up under the ceiling, its back against the wall;
    /// they are scattered, not in a row: every possible spot along the walls gets a random order from the floor's seed, and a spot is taken when it is clear and at least <see cref="MinGap"/> m from every beacon taken before it.
    /// </summary>
    public static class BeaconRules
    {
        /// <summary>The most beacons on a floor (the spots come in random order, so the ones left out are scattered too).</summary>
        public const int MaxBeacons = 60;
        /// <summary>Two beacons are at least this far apart (m, on the floor plan): the scatter's spacing.</summary>
        public const float MinGap = 9f;
        /// <summary>A beacon is no closer than this to either end of a wall face (a doorway, a corner, a wall that meets it) (m), and spots are tried every <see cref="SpotStep"/> m along a face.</summary>
        public const float EndMargin = 0.9f, SpotStep = 2.5f;
        /// <summary>The wall must go on this far (m) to both sides of a beacon, and nothing solid may stand this far in front of it: the lamp is 0.3 m across.</summary>
        public const float SideClear = 0.3f, FrontClear = 0.4f;
        /// <summary>A hallway beacon sees a point this far in front of the wall inside a hallway (or a big room): that decides which room the wall face belongs to.</summary>
        public const float ProbeFront = 0.5f;
        /// <summary>A room this big (m2) is a big room.</summary>
        public const float BigRoomArea = 60f;
        /// <summary>How fast the beam turns (degrees a second), how far it points down (degrees), the red spotlight's reach (m), its opening (degrees) and strength.</summary>
        public const float SpinDegreesPerSecond = 150f, BeamPitch = 28f, BeamRange = 13f, BeamAngle = 62f, BeamIntensity = 5.5f;
        /// <summary>At most this many beacons of the floor cast real light at once (the ones nearest the camera, within <see cref="LiveRange"/> m); the others only glow.</summary>
        public const int MaxLive = 5;
        public const float LiveRange = 30f;
        /// <summary>How big the pack's small roof lamp is made (it is 10 cm in the pack), and how far below the ceiling its middle is (m): high up, with its ring 15 cm under the ceiling.</summary>
        public const float LampScale = 3f, Drop = 0.30f;
        /// <summary>How far the wall is from the back of the lamp's ring (m): the ring's back is 2 cm off the wall (so a coat of paint or a moulding cannot swallow it), and the lamp's middle is <see cref="RingBack"/> in front of the ring's back.</summary>
        public const float Standoff = 0.02f, RingBack = 0.007f;

        /// <summary>
        /// The beacons of a floor. <paramref name="faces"/> are the wall faces that may carry one; <paramref name="zoneAt"/> tells what a point of the floor plan is (0 not wanted, 1 a big room, 2 a hallway); <paramref name="solid"/> tells whether a point is inside a wall that reaches the lamp's height.
        /// The same inputs and seed give the same plan on every machine.
        /// </summary>
        public static List<BeaconSpot> Plan(IList<WallFace> faces, int seed, Func<float, float, int> zoneAt, Func<float, float, bool> solid)
        {
            var spots = new List<BeaconSpot>();
            if (faces == null || zoneAt == null) return spots;
            var cands = new List<Cand>();
            for (int fi = 0; fi < faces.Count; fi++)
            {
                var f = faces[fi];
                float dx = f.x1 - f.x0, dz = f.z1 - f.z0, len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 2f * EndMargin + 0.01f) continue;
                float tx = dx / len, tz = dz / len, usable = len - 2f * EndMargin;
                int n = Math.Max(1, (int)(usable / SpotStep) + 1);
                for (int k = 0; k < n; k++)
                {
                    float t = n == 1 ? EndMargin + usable * 0.5f : EndMargin + usable * k / (n - 1);
                    t = Math.Min(len - EndMargin, Math.Max(EndMargin, t + (Hash01(seed, fi, k, 1) - 0.5f) * SpotStep * 0.6f));
                    cands.Add(new Cand { x = f.x0 + tx * t, z = f.z0 + tz * t, nx = f.nx, nz = f.nz, tx = tx, tz = tz, order = Hash01(seed, fi, k, 2), index = cands.Count });
                }
            }
            cands.Sort((a, b) => { int c = a.order.CompareTo(b.order); return c != 0 ? c : a.index.CompareTo(b.index); });
            float gap2 = MinGap * MinGap;
            foreach (var c in cands)
            {
                if (spots.Count >= MaxBeacons) break;
                int zone = zoneAt(c.x + c.nx * ProbeFront, c.z + c.nz * ProbeFront);
                if (zone <= 0) continue;
                bool near = false;
                foreach (var s in spots) { float ex = s.x - c.x, ez = s.z - c.z; if (ex * ex + ez * ez < gap2) { near = true; break; } }
                if (near) continue;
                if (solid != null)
                {
                    // the wall is behind the lamp on both sides of it, and nothing solid is in front of it (a pillar, another wall)
                    bool ok = solid(c.x - c.nx * 0.05f + c.tx * SideClear, c.z - c.nz * 0.05f + c.tz * SideClear) && solid(c.x - c.nx * 0.05f - c.tx * SideClear, c.z - c.nz * 0.05f - c.tz * SideClear);
                    ok = ok && !solid(c.x + c.nx * 0.05f, c.z + c.nz * 0.05f) && !solid(c.x + c.nx * FrontClear, c.z + c.nz * FrontClear)
                       && !solid(c.x + c.nx * 0.05f + c.tx * SideClear, c.z + c.nz * 0.05f + c.tz * SideClear) && !solid(c.x + c.nx * 0.05f - c.tx * SideClear, c.z + c.nz * 0.05f - c.tz * SideClear);
                    if (!ok) continue;
                }
                spots.Add(new BeaconSpot { x = c.x, z = c.z, yaw = (float)(Math.Atan2(c.nx, c.nz) * 180.0 / Math.PI), hall = zone == 2 });
            }
            return spots;
        }

        sealed class Cand { public float x, z, nx, nz, tx, tz, order; public int index; }

        /// <summary>The four faces of a wall box seen from outside it (a box of a floor plan: x0..x1 by z0..z1); a box thicker than <paramref name="thickest"/> is not a wall.</summary>
        public static void FacesOf(float x0, float z0, float x1, float z1, float thickest, List<WallFace> into)
        {
            float w = x1 - x0, d = z1 - z0;
            if (w <= 0f || d <= 0f || Math.Min(w, d) > thickest) return;
            if (w >= d)
            {
                into.Add(new WallFace(x0, z0, x1, z0, 0f, -1f));
                into.Add(new WallFace(x0, z1, x1, z1, 0f, 1f));
            }
            else
            {
                into.Add(new WallFace(x0, z0, x0, z1, -1f, 0f));
                into.Add(new WallFace(x1, z0, x1, z1, 1f, 0f));
            }
        }

        /// <summary>A number from 0 up to (not including) 1 that depends only on its four inputs (the same on every machine).</summary>
        public static float Hash01(int a, int b, int c, int d)
        {
            unchecked
            {
                uint h = (uint)a * 2654435761u ^ (uint)b * 2246822519u ^ (uint)c * 3266489917u ^ (uint)d * 668265263u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>The turn of a beacon's beam <paramref name="seconds"/> into the alarm (degrees, 0 to 360); <paramref name="index"/> gives every beacon its own start so they do not turn in step.</summary>
        public static float Spin(float seconds, int index) => ((seconds * SpinDegreesPerSecond + index * 47f) % 360f + 360f) % 360f;

        /// <summary>How bright the lamp's glow is (0.35 to 1): it swells as the beam turns past a point <paramref name="degreesOff"/> degrees from where the beam points, like a police light seen from the side.</summary>
        public static float Glow(float degreesOff)
        {
            float c = (float)Math.Cos(degreesOff * Math.PI / 180.0);
            return 0.35f + 0.65f * Math.Max(0f, c);
        }
    }
}
