using System;
using System.Collections.Generic;

namespace BuildingEscape
{
    /// <summary>One alarm beacon: where it hangs (x, z on the floor plan) and which way its dome faces (yaw in degrees, 0 = +z).</summary>
    public sealed class BeaconSpot
    {
        public float x, z, yaw;
        /// <summary>True for a beacon in a hallway.</summary>
        public bool hall;
    }

    /// <summary>
    /// The alarm beacons (the owner, 2026-10-24: "when the big alarm is activated there should be red lights activated that spin in circles (similar to a police car light) ... add these lights around the floors on corners near the ceiling"), without Unity types so
    /// it is tested outside Unity: where they hang (the corners of the hallways, a long hallway also along its walls, and two opposite corners of every big room: his answer, "hallway corners + big rooms"), how many there may be, and how they spin.
    /// </summary>
    public static class BeaconRules
    {
        /// <summary>The most beacons on a floor; a plan with more has every other one taken out until it fits.</summary>
        public const int MaxBeacons = 60;
        /// <summary>How far a beacon hangs from the two walls of its corner (m).</summary>
        public const float CornerInset = 0.32f;
        /// <summary>A hallway shorter than this gets none; a hallway longer than twice <see cref="WallStep"/> also gets one every <see cref="WallStep"/> metres along its walls, side by side in turn.</summary>
        public const float ShortestHall = 5f, WallStep = 12f;
        /// <summary>A room this big (m2) gets two beacons in opposite corners.</summary>
        public const float BigRoomArea = 60f;
        /// <summary>How fast the beam turns (degrees a second), how far it points down (degrees), the red spotlight's reach (m), its opening (degrees) and strength.</summary>
        public const float SpinDegreesPerSecond = 150f, BeamPitch = 28f, BeamRange = 13f, BeamAngle = 62f, BeamIntensity = 5.5f;
        /// <summary>At most this many beacons of the floor cast real light at once (the ones nearest the camera, within <see cref="LiveRange"/> m); the others only glow.</summary>
        public const int MaxLive = 5;
        public const float LiveRange = 30f;
        /// <summary>How big the pack's small roof lamp is made (it is 10 cm in the pack), and how far below the ceiling its middle hangs (m).</summary>
        public const float LampScale = 3f, Drop = 0.28f;

        /// <summary>The beacons of a floor: <paramref name="halls"/> are the hallways (and lobbies), <paramref name="rooms"/> every other room.</summary>
        public static List<BeaconSpot> Plan(IList<Rc> halls, IList<Rc> rooms)
        {
            var list = new List<BeaconSpot>();
            float i = CornerInset;
            if (halls != null)
                foreach (var r in halls)
                {
                    bool alongX = r.W >= r.D;
                    float len = alongX ? r.W : r.D;
                    if (len < ShortestHall) continue;
                    Corner(list, r.x0 + i, r.z0 + i, r, true);
                    Corner(list, r.x1 - i, r.z1 - i, r, true);
                    if (len > 2f * WallStep)
                    {
                        int n = (int)(len / WallStep);
                        for (int k = 1; k < n; k++)
                        {
                            float t = k * len / n;
                            bool far = k % 2 == 0;
                            if (alongX) Corner(list, r.x0 + t, far ? r.z1 - i : r.z0 + i, r, true);
                            else Corner(list, far ? r.x1 - i : r.x0 + i, r.z0 + t, r, true);
                        }
                    }
                }
            if (rooms != null)
                foreach (var r in rooms)
                {
                    if (r.W * r.D < BigRoomArea) continue;
                    Corner(list, r.x0 + i, r.z0 + i, r, false);
                    Corner(list, r.x1 - i, r.z1 - i, r, false);
                }
            while (list.Count > MaxBeacons)
            {
                var thinned = new List<BeaconSpot>();
                for (int k = 0; k < list.Count; k += 2) thinned.Add(list[k]);
                list = thinned;
            }
            return list;
        }

        static void Corner(List<BeaconSpot> list, float x, float z, Rc room, bool hall)
        {
            float dx = room.Cx - x, dz = room.Cz - z;
            float yaw = Math.Abs(dx) < 1e-4f && Math.Abs(dz) < 1e-4f ? 0f : (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
            list.Add(new BeaconSpot { x = x, z = z, yaw = yaw, hall = hall });
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
