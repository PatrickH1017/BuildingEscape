using System;

namespace BuildingEscape
{
    /// <summary>
    /// The rules of what a guard sees and for how long he keeps after somebody, free of Unity types so they can be tested outside Unity.
    /// <see cref="GuardAI"/> feeds them what the game says.
    /// </summary>
    public static class GuardRules
    {
        /// <summary>
        /// A guard on a chase gives up when no player has been in his cone of vision for this many seconds in a row. For as long as
        /// somebody is in it (however long that is) he keeps after him.
        /// </summary>
        public const float LoseSightSeconds = 5f;

        /// <summary>Somebody this close (flat, in metres) counts as in view whichever way the guard faces: he is on top of him.</summary>
        public const float AlwaysSeenWithin = 0.4f;

        /// <summary>
        /// Down a straight line of sight an officer sees much further (the owner, 2026-10-17: "Officers should also see much further down a hallway if it is a straight line of sight"):
        /// within <see cref="LongSightHalfAngle"/> degrees of the way he faces his sight grows from his ordinary range to <see cref="LongSightFactor"/> times that (44 m for the usual 14 m), most of all dead ahead.
        /// A wall still stops it (his ray decides that), so it is only ever a long, clear hallway that he sees down.
        /// </summary>
        public const float LongSightFactor = 3.15f, LongSightHalfAngle = 9f;

        /// <summary>How far an officer sees at <paramref name="degreesOff"/> from the way he faces: <paramref name="baseRange"/> out to the side, up to <paramref name="longRange"/> straight ahead (smoothly in between).</summary>
        public static float RangeAt(float degreesOff, float baseRange, float longRange, float longHalfAngle)
        {
            if (float.IsNaN(degreesOff)) return baseRange;
            if (degreesOff < 0f) degreesOff = -degreesOff;
            if (longRange <= baseRange || longHalfAngle <= 0f || degreesOff >= longHalfAngle) return baseRange;
            float t = 1f - degreesOff / longHalfAngle;               // 1 dead ahead, 0 at the edge of the long beam
            t = t * t * (3f - 2f * t);
            return baseRange + (longRange - baseRange) * t;
        }

        /// <summary>
        /// <see cref="InCone"/> with the long beam: is a player in view? Within <paramref name="halfAngleDegrees"/> of the way the guard faces and no further than <see cref="RangeAt"/> says for
        /// that angle. <paramref name="longRange"/> at or below <paramref name="range"/> means no long beam (a player who hides is seen from 1.5 m only).
        /// </summary>
        public static bool InVision(float facingX, float facingZ, float toX, float toZ, float halfAngleDegrees, float range, float longRange, float longHalfAngle)
        {
            float dist = (float)Math.Sqrt(toX * toX + toZ * toZ);
            if (dist < AlwaysSeenWithin) return true;
            float flen = (float)Math.Sqrt(facingX * facingX + facingZ * facingZ);
            if (flen < 1e-4f) return dist <= range;                           // (no way of facing to go by)
            float cos = Math.Max(-1f, Math.Min(1f, (facingX * toX + facingZ * toZ) / (flen * dist)));
            float degrees = (float)(Math.Acos(cos) * 180.0 / Math.PI);
            if (degrees > halfAngleDegrees) return false;
            return dist <= RangeAt(degrees, range, longRange, longHalfAngle);
        }

        /// <summary>
        /// Is a player in the guard's cone of vision? The cone is the fan the guard shows on the floor: flat, <paramref name="range"/>
        /// metres long and <paramref name="halfAngleDegrees"/> each side of the way he faces. (Whether a wall is in the way is for the
        /// guard's ray to say.) <paramref name="facingX"/> / <paramref name="facingZ"/> need not be unit length; <paramref name="toX"/> /
        /// <paramref name="toZ"/> point from the guard to the player.
        /// </summary>
        public static bool InCone(float facingX, float facingZ, float toX, float toZ, float halfAngleDegrees, float range)
        {
            float dist = (float)Math.Sqrt(toX * toX + toZ * toZ);
            if (dist > range) return false;
            if (dist < AlwaysSeenWithin) return true;
            float flen = (float)Math.Sqrt(facingX * facingX + facingZ * facingZ);
            if (flen < 1e-4f) return true;                                   // (no way of facing to go by)
            float cos = (facingX * toX + facingZ * toZ) / (flen * dist);
            return cos >= (float)Math.Cos(halfAngleDegrees * Math.PI / 180.0);
        }

        /// <summary>
        /// One frame of the chase's out-of-view clock. In view: the clock is back to zero. Out of view: it runs on, and when it reaches
        /// <paramref name="limit"/> the chase is over (true). A glimpse in between starts it again from zero, so it counts time out of
        /// view in a row.
        /// </summary>
        public static bool PursuitOver(ref float unseenFor, bool inView, float dt, float limit)
        {
            if (inView) { unseenFor = 0f; return false; }
            unseenFor += dt;
            return unseenFor >= limit;
        }
    }
}
