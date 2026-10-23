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
        /// within <see cref="LongSightHalfAngle"/> degrees of the way he faces his sight grows from his ordinary range to <see cref="LongSightFactor"/> times that (34 m for the usual 14 m; it was 44 m until the owner asked for a bit less, 2026-10-18: "Officers see too far down the hallway currently, please reduce their vision distance a bit"), most of all dead ahead.
        /// A wall still stops it (his ray decides that), so it is only ever a long, clear hallway that he sees down.
        /// </summary>
        public const float LongSightFactor = 2.4f, LongSightHalfAngle = 9f;

        /// <summary>
        /// Spotting takes time, and the further away the player is the longer (the owner, 2026-10-17: "the further away the longer it should take for them to see you"):
        /// seconds = <see cref="SpotBase"/> + <see cref="SpotPerMetre"/> x metres (about 1.8 s at 14 m, 5 s at 40 m).
        /// </summary>
        public const float SpotBase = 0.15f, SpotPerMetre = 0.12f;
        public static float SpotSeconds(float distance, float baseSeconds = SpotBase, float perMetre = SpotPerMetre) => baseSeconds + perMetre * (distance > 0f ? distance : 0f);

        /// <summary>A backup officer (called in by a radio call or the alarm) notices a player in his cone in this fraction of the usual time (the owner, 2026-10-20: "Backup guards should have a much shorter scan time when a player enters their vision cone"): 14 m takes about 0.5 s instead of 1.8 s.</summary>
        public const float BackupSpotFactor = 0.25f;

        /// <summary>From this floor on a floor holds <see cref="BigFloorBonus"/> more patrolling guards than the plain "base + floor" (the owner, 2026-10-18: "Wave 3 should have more guards than 2 by a decent amount").</summary>
        public const int BigFloor = 3, BigFloorBonus = 2;

        /// <summary>
        /// The seed of the random numbers that choose a floor's patrol routes, and so where each guard starts (the owner, 2026-10-22: "Every time a run restarts it should be a random start to where the guards are on that floor. I don't
        /// want players to be able to memorize guard spawns"): <paramref name="runGuardSeed"/> is the run's (<see cref="TowerManager.GuardSeed"/>: a new one each run), so the same run puts the same guards in the same places on every
        /// machine and another run puts them elsewhere; the floor keeps its own layout and loot.
        /// </summary>
        public static int RouteSeed(int runGuardSeed, int floor) => unchecked(runGuardSeed * 7919 + floor * 104729 + 11);

        /// <summary>An online run's guard seed: from the host's run seed (the same on every machine, new with every run), never 0 (0 means "the floor's own random numbers", the editor tools' way).</summary>
        public static int OnlineGuardSeed(int hostSeed) { int s = unchecked(hostSeed ^ 0x3C6EF372); return s != 0 ? s : 1; }

        /// <summary>Patrolling guards on a floor: <paramref name="baseGuards"/> + the floor, and from floor 3 on two more (floor 2: 5, floor 3: 8, floor 4: 9 with a base of 3).</summary>
        public static int PatrolGuards(int floor, int baseGuards) => baseGuards + floor + (floor >= BigFloor ? BigFloorBonus : 0);


        /// <summary>
        /// A guard who is down for good lies there this long, then sinks away in <see cref="CorpseSinkSeconds"/> and is gone (the owner, 2026-10-18: "Guards bodies should de-spawn").
        /// </summary>
        public const float CorpseSeconds = 15f, CorpseSinkSeconds = 1f;

        /// <summary>How far a body has sunk away <paramref name="secondsDown"/> seconds after he went down: 0 while he still lies there, 1 when he is gone.</summary>
        public static float CorpseSink(float secondsDown, float wait = CorpseSeconds, float sinkSeconds = CorpseSinkSeconds) =>
            System.Math.Min(1f, System.Math.Max(0f, (secondsDown - wait) / System.Math.Max(0.01f, sinkSeconds)));

        /// <summary>
        /// The owner, 2026-10-18: "Crazy chances that a guard (not the armed guards) will randomly stop what they are doing to eat a donut". At a patrol stop an unarmed guard has this chance (percent)
        /// of stopping for <see cref="DonutSeconds"/> to eat a donut: now and then somebody on a floor, never a regular thing.
        /// </summary>
        public const float DonutChancePercent = 1.5f, DonutSeconds = 6.5f;

        /// <summary>Does this patrol stop turn into a donut break? <paramref name="roll01"/> is a dice roll from 0 to 1; armed guards, the boss and a guard on a post never stop.</summary>
        public static bool DonutBreak(float roll01, bool armed, bool boss, bool onPost) => !armed && !boss && !onPost && roll01 * 100f < DonutChancePercent;

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
