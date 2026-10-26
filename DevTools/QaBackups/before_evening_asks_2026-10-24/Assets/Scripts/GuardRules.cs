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

        /// <summary>The most guards alive at once while the alarm is on (the owner, 2026-10-23: "the cap while the alarm is on should stay at 24 (this number may change later on)"): the patrols and the reinforcements together. The one place to change it.</summary>
        public const int MaxGuardsAlive = 24;

        /// <summary>The most guards that PATROL a floor (the owner, 2026-10-23: the guards scale with the floor): the rule above rises by one a floor and is cut here (floor 13: 18), which leaves room under <see cref="MaxGuardsAlive"/> for the officers a backup call sends.</summary>
        public const int PatrolCap = 18;

        /// <summary>The patrolling guards on a floor: <see cref="PatrolGuards"/>, never above <see cref="PatrolCap"/>.</summary>
        public static int PatrolCount(int floor, int baseGuards) => Math.Min(PatrolCap, PatrolGuards(floor, baseGuards));

        /// <summary>A guard who is eliminated is replaced: this many seconds later a new guard comes out of a stairwell (the owner, 2026-10-23: "If a guard is eliminated after 15 seconds a new guard should come from a stairwell").</summary>
        public const float ReplacementSeconds = 15f;

        /// <summary>The time the replacement of a guard who was eliminated at <paramref name="now"/> is due.</summary>
        public static float ReplacementDue(float now) => now + ReplacementSeconds;

        /// <summary>Is a replacement wanted: are fewer guards alive than the cap (<see cref="MaxGuardsAlive"/>)?</summary>
        public static bool ReplacementWanted(int alive, int cap = MaxGuardsAlive) => alive < cap;

        // ---- bodies (the owner, 2026-10-23: "Guards can see other guards eliminated bodies and get alerted")

        /// <summary>How often (seconds) a guard looks for the bodies of eliminated guards in his cone: a body does not move, so a few times a second is plenty.</summary>
        public const float BodyScanSeconds = 0.2f;

        /// <summary>How tall a body lying on the floor is (m): the lines of sight to it end between its feet and this height (<see cref="SeesBody"/>).</summary>
        public const float BodyHeightLying = 0.5f;

        /// <summary>Does the body raise the alarm: a guard sees it (in his cone, nothing in the way) and nobody raised it before? Each body raises the alarm once.</summary>
        public static bool BodyAlarms(bool seen, bool alreadyRaised) => seen && !alreadyRaised;


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

        // ------------------------------------------------------------------ what a guard sees, and how his notice builds up
        // (the owner, 2026-10-22: "the vision cones are misleading: the player can be inside a guard's cone and within its vision range without being detected consistently")

        /// <summary>
        /// How far above the middle of his body a guard's eyes are (his transform is the middle of his 2 m capsule, a metre above the floor; times his scale): 1.8 m above the floor for an ordinary guard. EVERY ray of his sight starts at
        /// his eyes: the ones that cut the cone on the floor short and the ones that decide whether he sees somebody. (Until 2026-10-22 the cone's rays started 0.9 m above his middle and the check's 1.6 m above it - 0.6 m over his
        /// head - so the two did not agree about what was in the way.)
        /// </summary>
        public const float EyeAboveMiddle = 0.8f;

        /// <summary>A player's body for sight: 2 m tall (the greybox capsule), his transform in the middle of it, so his feet are <see cref="FeetBelowMiddle"/> below it (both times his scale; a crouched player is lower, see WobblyPlayer.crouchHeight).</summary>
        public const float BodyHeight = 2f, FeetBelowMiddle = 1f;

        /// <summary>Where on a player's body a guard looks, as shares of his height above his feet: head, chest, knees. A clear line to any one of them is enough to see him (a low wall hides his knees, not his head).</summary>
        public static readonly float[] LookShares = { 0.88f, 0.55f, 0.2f };

        /// <summary>A straight line through the world, from (fromX, fromY, fromZ) to (toX, toY, toZ) in metres: is it free of anything that blocks a guard's sight? The game answers with a physics ray (GuardSight.LineClear).</summary>
        public delegate bool SightLine(float fromX, float fromY, float fromZ, float toX, float toY, float toZ);

        /// <summary>
        /// Does a guard whose eyes are at (<paramref name="eyeX"/>, <paramref name="eyeY"/>, <paramref name="eyeZ"/>) have a clear view of a player who stands at (<paramref name="x"/>, <paramref name="z"/>) with his feet at <paramref name="feetY"/>
        /// and is <paramref name="height"/> metres tall (a crouched player is lower)? Two things must hold, and they are what the cone on the floor shows: the line level with his eyes to where the player stands is not cut by a wall, a door or a tall
        /// piece of furniture (the cone is that line turned all the way round), and a line to the player's head, chest or knees is clear (so a player who crouches behind something low is hidden by it, a standing one is not). Only fixed things
        /// block a line (GuardSight.Blocks): walls, doors, furniture. A loose crate, the loot somebody carries, other people and the other officers do not, and they never cut the cone short either. The lines end AT the player: what is behind him does not matter.
        /// </summary>
        public static bool SeesBody(float eyeX, float eyeY, float eyeZ, float x, float feetY, float z, float height, SightLine clear)
        {
            if (clear == null) return true;
            if (!clear(eyeX, eyeY, eyeZ, x, eyeY, z)) return false;                         // (the line the cone is drawn with)
            for (int i = 0; i < LookShares.Length; i++)
                if (clear(eyeX, eyeY, eyeZ, x, feetY + height * LookShares[i], z)) return true;
            return false;
        }

        /// <summary>
        /// Ray <paramref name="i"/> (0 to <paramref name="n"/>) of the cone a guard shows on the floor (GuardVision): the rays fan out from minus <paramref name="halfAngle"/> to plus <paramref name="halfAngle"/> degrees off the way he faces. Returns how far the
        /// ray reaches before a wall cuts it short, <see cref="RangeAt"/>, and gives the angle it runs at in <paramref name="degrees"/>: the very numbers <see cref="InVision"/> judges a player by, so that the cone drawn and the area he sees are one and the same.
        /// The rays are crowded where the sight changes fastest (the sides of the long beam, where it falls from 34 m to 14 m within a few degrees): nearly half of them (up to the sides of the beam) are under a degree apart, and a wide
        /// 14 m arc needs few. (The drawn fan is straight lines between the rays: with the 48 of GuardVision it is never more than 0.13 m off the true edge; spread the way they were before 2026-10-22 it was up to 0.37 m off, at the foot of the beam's sides.)
        /// </summary>
        public static float ConeRay(int i, int n, float halfAngle, float range, float longRange, float longHalfAngle, out float degrees)
        {
            float u = n > 0 ? i / (float)n * 2f - 1f : 0f;                                  // -1 .. 1
            float a = Math.Abs(u);
            float angle;
            if (longRange <= range || longHalfAngle <= 0f) angle = halfAngle * a;           // (no long beam: the rays are evenly spread)
            else
            {
                float edge = Math.Min(longHalfAngle * ConeBeamMargin, halfAngle * 0.5f);   // (the beam and a little more: the foot of its sides is a ray)
                float mid = Math.Max(edge, halfAngle * 0.5f);
                if (a <= ConeBeamShare) angle = edge * (a / ConeBeamShare);                 // (the beam: nearly half the rays)
                else if (a <= ConeMidShare) angle = edge + (mid - edge) * ((a - ConeBeamShare) / (ConeMidShare - ConeBeamShare));
                else angle = mid + (halfAngle - mid) * ((a - ConeMidShare) / (1f - ConeMidShare));
            }
            degrees = u < 0f ? -angle : angle;
            return RangeAt(degrees, range, longRange, longHalfAngle);
        }

        /// <summary>
        /// Is the cone of vision drawn on the floor this frame (GuardVision)? The owner, 2026-10-22: "Make all guards vision cones off by default to players but still work the same way": the picture is drawn only when the cone is switched on
        /// (<paramref name="showCones"/>: the test menu's check box) or a test forces it (<paramref name="forced"/>), and then only for a guard who is up (<paramref name="awake"/>), near the camera (<paramref name="near"/>) and in view
        /// (<paramref name="shown"/> above nothing). What the guard sees is not decided here: this is a picture only.
        /// </summary>
        public static bool ConeDrawn(bool awake, bool near, float shown, bool showCones, bool forced) => awake && near && shown > 0.01f && (showCones || forced);

        /// <summary>How the cone's rays are shared out (<see cref="ConeRay"/>): the rays up to this share of the way from the middle to the edge cover the long beam (and <see cref="ConeBeamMargin"/> times its half angle), up to the next share the rest of the inner half of the cone, the others the outer half.</summary>
        const float ConeBeamShare = 11f / 24f, ConeMidShare = 18f / 24f, ConeBeamMargin = 1.06f;

        /// <summary>
        /// A guard's notice of a player is a meter from 0 to 1 that FILLS while the player is in his sight, at <see cref="NoticeRate"/> of the whole meter every second: the closer the player the faster (it is 1 / <see cref="SpotSeconds"/> at his
        /// distance now, so what counts is where the player is, never how he got there). When it is full the guard has noticed him. Out of his sight the meter first holds for <see cref="NoticeHoldSeconds"/> and then fades <see cref="NoticeFadeFactor"/>
        /// times as fast as it fills at that distance (the old rule: looking away let the notice fade twice as fast).
        /// </summary>
        public const float NoticeHoldSeconds = 0.25f, NoticeFadeFactor = 2f;

        /// <summary>How much of the notice meter (0 to 1) a player <paramref name="distance"/> metres away fills in a second: 1 / <see cref="SpotSeconds"/>, so about 2/s at 3 m, 0.55/s at 14 m and 0.24/s at 34 m. <paramref name="timeFactor"/> 0.25 is a backup officer (4 times as fast).</summary>
        public static float NoticeRate(float distance, float baseSeconds = SpotBase, float perMetre = SpotPerMetre, float timeFactor = 1f) =>
            1f / Math.Max(0.01f, SpotSeconds(distance, baseSeconds, perMetre) * timeFactor);

        /// <summary>
        /// One frame of one guard's notice meter for one player (see <see cref="NoticeHoldSeconds"/>): <paramref name="progress"/> is the meter now, <paramref name="unseenFor"/> how long the player has been out of sight in a row (kept by the caller),
        /// <paramref name="inSight"/> whether the guard sees him this frame, <paramref name="distance"/> how far away he is (metres, on the floor). Returns the meter after <paramref name="dt"/> seconds; 1 means noticed. The meter of one guard
        /// knows nothing of any other guard: nothing here depends on how many there are.
        /// </summary>
        public static float NoticeStep(float progress, ref float unseenFor, bool inSight, float dt, float distance, float baseSeconds = SpotBase, float perMetre = SpotPerMetre, float timeFactor = 1f)
        {
            if (dt <= 0f) return progress;
            float rate = NoticeRate(distance, baseSeconds, perMetre, timeFactor);
            if (inSight) { unseenFor = 0f; return Math.Min(1f, progress + rate * dt); }
            unseenFor += dt;
            if (unseenFor <= NoticeHoldSeconds) return progress;                             // (a sight line that flickers for a frame or two must not undo what he has seen)
            return Math.Max(0f, progress - NoticeFadeFactor * rate * dt);
        }
    }
}
