using System;

namespace BuildingEscape
{
    /// <summary>What a door has just started to do (the sound follows it).</summary>
    public enum DoorEvent { None, Opening, Closing }

    /// <summary>
    /// The brain of a sliding door, free of Unity types so it can be tested outside Unity. It opens as soon as it is told somebody wants
    /// it (<c>occupied</c>: a player has opened it with E, or an officer is about to walk through it), stays open while that goes on and for a
    /// moment after, then closes - unless something is in the doorway, in which case it stays (or goes back) open. The caller only gives
    /// the door a collider once <see cref="IsShut"/>, so a door that is opening, open or closing can never knock anybody over.
    /// </summary>
    public struct DoorLogic
    {
        /// <summary>A closing door turned round before it is this far shut makes no new opening sound (it never really closed).</summary>
        public const float SilentReopenAbove = 0.5f;
        /// <summary>While closing, in this last stretch the caller must check whether something is in the way (see <see cref="NeedsBlockCheck"/>).</summary>
        public const float CheckZone = 0.3f;
        /// <summary>When something is in the doorway, the door looks again after this many seconds.</summary>
        public const float BlockedRetry = 0.25f;

        /// <summary>0 = shut, 1 = wide open.</summary>
        public float Open;
        /// <summary>The direction it is heading.</summary>
        public bool WantOpen;
        /// <summary>An open door does not start closing before this time.</summary>
        public float CloseAfter;

        public bool IsShut => Open <= 0f && !WantOpen;
        /// <summary>True while the leaves are travelling.</summary>
        public bool Moving => WantOpen ? Open < 1f : Open > 0f;

        /// <summary>
        /// Somebody asked for the door to close: an open door does not wait out its linger time but starts closing as soon as nobody
        /// holds it open any more (and nothing is in the doorway).
        /// </summary>
        public void CloseNow(float now)
        {
            if (CloseAfter > now) CloseAfter = now;
        }

        /// <summary>
        /// Whether the caller must find out if something is in the doorway before calling <see cref="Step"/>. That is a physics query,
        /// so it is only made when the answer matters: when the door is about to start closing, and in the last stretch of closing.
        /// </summary>
        public bool NeedsBlockCheck(float now, bool occupied)
        {
            if (occupied) return false;
            if (WantOpen) return Open >= 1f && now >= CloseAfter;
            return Open > 0f && Open < CheckZone;
        }

        /// <summary>
        /// Moves the door on by <paramref name="dt"/> seconds and says if it just started to open or close.
        /// <paramref name="occupied"/>: something holds the door open (a player opened it with E, or an officer is at it).
        /// <paramref name="blocked"/>: something is in the doorway itself (it only has to be right when <see cref="NeedsBlockCheck"/>
        /// says so). <paramref name="linger"/>: how long it stays open after the last thing that held it open stopped.
        /// </summary>
        public DoorEvent Step(float now, float dt, bool occupied, bool blocked, float openSeconds, float closeSeconds, float linger)
        {
            var ev = DoorEvent.None;
            if (occupied)
            {
                CloseAfter = now + linger;
                if (!WantOpen)
                {
                    ev = Open < SilentReopenAbove ? DoorEvent.Opening : DoorEvent.None;
                    WantOpen = true;
                }
            }
            else if (WantOpen)
            {
                if (Open >= 1f && now >= CloseAfter)
                {
                    if (blocked) CloseAfter = now + BlockedRetry;            // something is in the doorway: stay open, look again soon
                    else { WantOpen = false; ev = DoorEvent.Closing; }
                }
            }
            else if (blocked && Open < CheckZone)
            {
                WantOpen = true;                                             // something is in the way of the last stretch: back open
                CloseAfter = now + linger;
            }

            if (WantOpen) Open = Math.Min(1f, Open + dt / Math.Max(0.01f, openSeconds));
            else Open = Math.Max(0f, Open - dt / Math.Max(0.01f, closeSeconds));
            return ev;
        }
    }

    /// <summary>The shapes of a door and its sensor.</summary>
    public static class DoorGeometry
    {
        /// <summary>The leaves are a hair narrower than half the doorway so the two halves show a seam.</summary>
        public const float Seam = 0.01f;

        /// <summary>
        /// Width of each of the two leaves at an opening of 0..1. They do not slide sideways (the wall beside a doorway may hold
        /// furniture or end at a corner): each one narrows towards its own jamb, as if it slid into the wall.
        /// </summary>
        public static float LeafWidth(float halfGap, float open) =>
            Math.Max(0f, (halfGap - Seam) * (1f - Math.Min(1f, Math.Max(0f, open))));

        /// <summary>Centre of a leaf along the doorway, counted from the middle: side -1 is the leaf at the low end, +1 the other.</summary>
        public static float LeafCentre(float halfGap, float leafWidth, int side) => side * (halfGap - leafWidth * 0.5f);

        /// <summary>
        /// Is a point within the door's sensor? The sensor is a box centred on the doorway: <paramref name="reachAlong"/> each way along
        /// the wall (counted from the middle) and <paramref name="reachAcross"/> each way through it.
        /// </summary>
        public static bool InSensor(float px, float pz, float cx, float cz, bool alongX, float reachAlong, float reachAcross)
        {
            float dx = Math.Abs(px - cx), dz = Math.Abs(pz - cz);
            float along = alongX ? dx : dz, across = alongX ? dz : dx;
            return along <= reachAlong && across <= reachAcross;
        }

        /// <summary>
        /// Does a walk of <paramref name="length"/> metres from (<paramref name="px"/>, <paramref name="pz"/>) the way (<paramref name="dx"/>,
        /// <paramref name="dz"/>) (a unit vector) go into the doorway's zone? The zone is the gap (<paramref name="reachAlong"/> each way from
        /// the middle) and <paramref name="thickness"/> metres in front of and behind the wall's middle plane. A walk that starts inside
        /// it counts. Walking along the wall further away than that, or away from the doorway, does not.
        /// </summary>
        public static bool WalkEnters(float px, float pz, float dx, float dz, float length, float cx, float cz, bool alongX, float reachAlong, float thickness)
        {
            float u0 = alongX ? px - cx : pz - cz, v0 = alongX ? pz - cz : px - cx;      // along the wall, across it
            float du = alongX ? dx : dz, dv = alongX ? dz : dx;
            float t0 = 0f, t1 = length;
            return Clip(u0, du, -reachAlong, reachAlong, ref t0, ref t1) && Clip(v0, dv, -thickness, thickness, ref t0, ref t1);
        }

        /// <summary>Liang-Barsky: the part of t in [t0, t1] where p + d t is within [lo, hi]; false when there is none.</summary>
        static bool Clip(float p, float d, float lo, float hi, ref float t0, ref float t1)
        {
            if (Math.Abs(d) < 1e-6f) return p >= lo && p <= hi;
            float a = (lo - p) / d, b = (hi - p) / d;
            if (a > b) { float s = a; a = b; b = s; }
            if (a > t0) t0 = a;
            if (b < t1) t1 = b;
            return t0 <= t1;
        }

        /// <summary>
        /// Does a walk along a route (the points <paramref name="xs"/> / <paramref name="zs"/>, <paramref name="count"/> of them, from
        /// where he stands through the corners of his path) go into the doorway's zone within its first <paramref name="reach"/> metres?
        /// (<see cref="WalkEnters"/> for each leg in turn.)
        /// </summary>
        public static bool RouteEnters(float[] xs, float[] zs, int count, float reach, float cx, float cz, bool alongX, float reachAlong, float thickness)
        {
            float left = reach;
            for (int i = 0; i + 1 < count && left > 0f; i++)
            {
                float dx = xs[i + 1] - xs[i], dz = zs[i + 1] - zs[i];
                float len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-4f) continue;
                if (WalkEnters(xs[i], zs[i], dx / len, dz / len, Math.Min(len, left), cx, cz, alongX, reachAlong, thickness)) return true;
                left -= len;
            }
            return false;
        }

        /// <summary>
        /// How good a choice a door is for a player who presses the use key, or <see cref="float.MaxValue"/> when it is out of reach (lower
        /// is better). The reach is measured to the nearest point of the doorway; doors in front of the player beat doors behind them, so
        /// in a corridor with doors on both sides the one he is looking at is the one that works.
        /// </summary>
        /// <param name="fx">Where the player faces (x, z; need not be normalised).</param>
        public static float UseScore(float px, float pz, float fx, float fz, float cx, float cz, bool alongX, float halfGap, float reach)
        {
            // nearest point of the doorway (a line along the wall)
            float qx = cx, qz = cz;
            if (alongX) qx = Math.Min(cx + halfGap, Math.Max(cx - halfGap, px));
            else qz = Math.Min(cz + halfGap, Math.Max(cz - halfGap, pz));
            float vx = qx - px, vz = qz - pz;
            float dist = (float)Math.Sqrt(vx * vx + vz * vz);
            if (dist > reach) return float.MaxValue;

            float facing = 1f;                                           // standing in the doorway: it is the one
            float flen = (float)Math.Sqrt(fx * fx + fz * fz);
            if (dist > 0.3f && flen > 0.0001f) facing = (fx * vx + fz * vz) / (flen * dist);
            return dist - 0.6f * facing;
        }
    }
}
