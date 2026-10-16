using System;

namespace BuildingEscape
{
    /// <summary>
    /// The arithmetic of aiming with the crosshair, without Unity types so it can be tested outside Unity. The crosshair is a ray from the
    /// camera straight through the middle of the screen; this works out where that ray meets a doorway, an item's box or the dealer, which
    /// of them the player means, and which way a swing goes. <see cref="Crosshair"/> feeds it what the physics world says.
    /// </summary>
    public static class AimMath
    {
        /// <summary>A doorway counts a hair wider than it is, so aiming at the jamb still works the door.</summary>
        public const float DoorPad = 0.1f;
        /// <summary>...and the beam over it counts as the door too (the camera looks down on a door from above and behind the player).</summary>
        public const float DoorTopExtra = 0.7f;
        /// <summary>An item counts a hair bigger than it is: small things (a mug) are hard to hit with a thin ray.</summary>
        public const float ItemPad = 0.06f;
        /// <summary>The aim ray starts this far before the player's depth: everything between the camera and the player is ignored.</summary>
        public const float StartBack = 0.25f;

        // ------------------------------------------------------------------ rays

        /// <summary>
        /// Ray against an axis-aligned box (slab method). <paramref name="tEnter"/> is 0 when the origin is inside the box. False when the
        /// ray misses it, the box is behind the origin, or it starts farther than <paramref name="maxT"/>.
        /// </summary>
        public static bool RayBox(float ox, float oy, float oz, float dx, float dy, float dz,
                                  float x0, float y0, float z0, float x1, float y1, float z1,
                                  float maxT, out float tEnter, out float tExit)
        {
            tEnter = 0f; tExit = float.MaxValue;
            if (!Slab(ox, dx, x0, x1, ref tEnter, ref tExit) ||
                !Slab(oy, dy, y0, y1, ref tEnter, ref tExit) ||
                !Slab(oz, dz, z0, z1, ref tEnter, ref tExit)) { tEnter = tExit = 0f; return false; }
            if (tEnter > maxT || tExit < tEnter) { tEnter = tExit = 0f; return false; }
            return true;
        }

        static bool Slab(float o, float d, float lo, float hi, ref float tMin, ref float tMax)
        {
            if (Math.Abs(d) < 1e-8f) return o >= lo && o <= hi;
            float inv = 1f / d;
            float t1 = (lo - o) * inv, t2 = (hi - o) * inv;
            if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
            if (t1 > tMin) tMin = t1;
            if (t2 < tMax) tMax = t2;
            return tMin <= tMax;
        }

        /// <summary>
        /// Ray against a doorway: the box of the gap (<paramref name="halfGap"/> each way along the wall from the middle at
        /// <paramref name="cx"/>, <paramref name="cz"/>), from the floor up to <paramref name="height"/> (and the beam above it, see
        /// <see cref="DoorTopExtra"/>), as deep as the wall.
        /// Open or shut, a door is aimed at where it is, not at its leaves.
        /// </summary>
        public static bool RayDoorway(float ox, float oy, float oz, float dx, float dy, float dz,
                                      float cx, float cz, bool alongX, float halfGap, float height, float depth,
                                      float maxT, out float tEnter, out float tExit)
        {
            float hx = (alongX ? halfGap : depth * 0.5f) + DoorPad;
            float hz = (alongX ? depth * 0.5f : halfGap) + DoorPad;
            return RayBox(ox, oy, oz, dx, dy, dz, cx - hx, 0f, cz - hz, cx + hx, height + DoorTopExtra, cz + hz, maxT, out tEnter, out tExit);
        }

        /// <summary>Ray against an item's (world) box, grown by <see cref="ItemPad"/>.</summary>
        public static bool RayItem(float ox, float oy, float oz, float dx, float dy, float dz,
                                   float minX, float minY, float minZ, float maxX, float maxY, float maxZ,
                                   float maxT, out float tEnter)
        {
            bool hit = RayBox(ox, oy, oz, dx, dy, dz, minX - ItemPad, minY - ItemPad, minZ - ItemPad,
                              maxX + ItemPad, maxY + ItemPad, maxZ + ItemPad, maxT, out tEnter, out _);
            return hit;
        }

        /// <summary>
        /// Ray against an upright cylinder (the dealer): axis through (<paramref name="cx"/>, <paramref name="cz"/>), from y0 up to y1.
        /// <paramref name="t"/> is where the ray gets in (0 when it starts inside).
        /// </summary>
        public static bool RayCylinderY(float ox, float oy, float oz, float dx, float dy, float dz,
                                        float cx, float cz, float radius, float y0, float y1, float maxT, out float t)
        {
            t = 0f;
            float lo = 0f, hi = float.MaxValue;
            if (!Slab(oy, dy, y0, y1, ref lo, ref hi)) return false;
            float px = ox - cx, pz = oz - cz;
            float a = dx * dx + dz * dz;
            float c = px * px + pz * pz - radius * radius;
            if (a < 1e-10f)
            {
                if (c > 0f) return false;                       // straight up or down, outside the cylinder
            }
            else
            {
                float b = 2f * (px * dx + pz * dz);
                float disc = b * b - 4f * a * c;
                if (disc < 0f) return false;
                float root = (float)Math.Sqrt(disc);
                float n = (-b - root) / (2f * a), f = (-b + root) / (2f * a);
                if (n > lo) lo = n;
                if (f < hi) hi = f;
            }
            if (lo > hi || lo > maxT) return false;
            t = lo;
            return true;
        }

        // ------------------------------------------------------------------ where the aim ray lives

        /// <summary>How far along the aim ray the player is: the length of (player - origin) along the ray (the ray need not be a unit one).</summary>
        public static float Depth(float ox, float oy, float oz, float dx, float dy, float dz, float px, float py, float pz) =>
            (px - ox) * dx + (py - oy) * dy + (pz - oz) * dz;

        /// <summary>Where the aim starts: just before the player, so a wall or a desk between the camera and the player never counts.</summary>
        public static float StartT(float depth) => Math.Max(0f, depth - StartBack);

        /// <summary>Where the aim ends: the player's depth plus the reach plus a margin for the ray being sideways to the player.</summary>
        public static float EndT(float depth, float reach) => Math.Max(depth, 0f) + reach + 1.2f;

        // ------------------------------------------------------------------ which one does E mean

        public enum Pick { None, Pickup, Door, Dealer, Interact }

        /// <summary>
        /// Of the things under the crosshair, the one E works. Each argument is where along the ray the ray meets that thing, or
        /// <see cref="float.PositiveInfinity"/> when the crosshair is not on it (or it is out of reach, or hidden behind a wall).
        /// The nearest wins, an item beats the dealer beats a door on a tie, and an item inside the doorway (or in front of it) beats
        /// the door, so a crate that keeps a door from closing can be picked up.
        /// </summary>
        public static Pick Choose(float pickupT, float doorEnter, float doorExit, float dealerT) =>
            Choose(pickupT, doorEnter, doorExit, dealerT, float.PositiveInfinity);

        /// <summary>
        /// The same with one more thing: <paramref name="interactT"/> is where the ray meets something in the world that E works (the
        /// outpost's keypad, a safe, a note). On a tie an item beats the dealer beats such a thing beats a door.
        /// </summary>
        public static Pick Choose(float pickupT, float doorEnter, float doorExit, float dealerT, float interactT)
        {
            bool item = !float.IsPositiveInfinity(pickupT), door = !float.IsPositiveInfinity(doorEnter), dealer = !float.IsPositiveInfinity(dealerT);
            bool thing = !float.IsPositiveInfinity(interactT);
            if (item && door && pickupT <= doorExit + 0.05f) door = false;
            float best = float.PositiveInfinity;
            var pick = Pick.None;
            if (item) { best = pickupT; pick = Pick.Pickup; }
            if (dealer && dealerT < best) { best = dealerT; pick = Pick.Dealer; }
            if (thing && interactT < best) { best = interactT; pick = Pick.Interact; }
            if (door && doorEnter < best) { pick = Pick.Door; }
            return pick;
        }

        // ------------------------------------------------------------------ the swing

        /// <summary>
        /// Which way a swing goes: from the player (<paramref name="px"/>, <paramref name="pz"/>) towards the point of the aim ray that is
        /// <paramref name="ahead"/> metres past the player's depth, flattened to the floor and made one metre long. False if there is no
        /// sensible direction (then the swing uses the way the player faces).
        /// </summary>
        public static bool SwingDirection(float ox, float oy, float oz, float dx, float dy, float dz, float px, float py, float pz,
                                          float ahead, out float sx, out float sz)
        {
            float t = Depth(ox, oy, oz, dx, dy, dz, px, py, pz) + ahead;
            float ax = ox + dx * t - px, az = oz + dz * t - pz;
            float len = (float)Math.Sqrt(ax * ax + az * az);
            if (len < 1e-3f) { sx = sz = 0f; return false; }
            sx = ax / len; sz = az / len;
            return true;
        }
    }
}
