using System;
using System.Collections.Generic;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner, 2026-10-22: "Fix and improve guard vision and player detection ... Being inside the vision cone determines whether the guard can see the player. The closer the player is to the guard, the faster the guard detects them."
        /// The pure rules of it (GuardRules), run on their own: how fast the notice meter fills at close, medium and maximum range and that it always fills, that it keeps what it got however the player moved and stops when he is out of sight, that one
        /// guard's meter knows nothing of the others, that the cone drawn on the floor is the area the guard judges a player by, and what stops the guard's sight (a small made-up world of boxes stands in for the physics rays).
        /// The Play-mode half, with the real guard, the real raycasts and the real cone mesh, is the scenario <c>GuardSight</c> (NetDevGuardSight.cs).
        /// </summary>
        static void GuardSightTest()
        {
            const float Dt = 1f / 60f;
            float baseS = GuardRules.SpotBase, perM = GuardRules.SpotPerMetre;

            // ---- the notice meter: how long the whole meter takes to fill while the player stays in sight at one distance
            float FillTime(float distance, float timeFactor = 1f)
            {
                float progress = 0f, unseen = 0f, t = 0f;
                while (progress < 1f && t < 60f) { progress = GuardRules.NoticeStep(progress, ref unseen, true, Dt, distance, baseS, perM, timeFactor); t += Dt; }
                return t;
            }
            float close = FillTime(3f), medium = FillTime(14f), maxRange = FillTime(14f * GuardRules.LongSightFactor);
            Check($"guard sight: the closer the player, the faster the notice meter fills ({close:0.00} s at 3 m, {medium:0.00} s at 14 m, {maxRange:0.00} s at the longest sight of {14f * GuardRules.LongSightFactor:0} m)",
                close < medium && medium < maxRange && close < 0.8f && medium > 1.4f && medium < 2.4f && maxRange > 3f && maxRange < 6f);
            Check("guard sight: each of those is the spotting time the guard always had (about a frame of slack): the speeds were kept, the way they are applied is what changed",
                Math.Abs(close - GuardRules.SpotSeconds(3f)) <= Dt + 1e-3f && Math.Abs(medium - GuardRules.SpotSeconds(14f)) <= Dt + 1e-3f && Math.Abs(maxRange - GuardRules.SpotSeconds(14f * GuardRules.LongSightFactor)) <= Dt + 1e-3f);

            bool everyDistance = true, neverFaster = true; float previous = 0f, slowest = 0f;
            for (float d = 0f; d <= 14f * GuardRules.LongSightFactor + 0.01f; d += 0.5f)
            {
                float t = FillTime(d);
                if (t >= 60f) everyDistance = false;
                if (t < previous - Dt) neverFaster = false;
                previous = t; slowest = Math.Max(slowest, t);
            }
            Check($"guard sight: a player who stays in sight is noticed at EVERY distance up to the longest sight (the slowest takes {slowest:0.0} s), and a further player is never noticed sooner than a nearer one", everyDistance && neverFaster && slowest < 6f);

            bool smooth = true, formula = true;
            for (float d = 0f; d < 33f; d += 0.1f)
            {
                float r0 = GuardRules.NoticeRate(d, baseS, perM), r1 = GuardRules.NoticeRate(d + 0.1f, baseS, perM);
                if (Math.Abs(r1 - r0) / r0 > 0.15f) smooth = false;                      // (no step anywhere: the rate follows the distance continuously)
                if (Math.Abs(1f / r0 - GuardRules.SpotSeconds(d)) > 1e-4f) formula = false;
            }
            Check("guard sight: the rate is a continuous curve of the distance (no jump anywhere in 0.1 m steps), 1 / spotting seconds", smooth && formula && GuardRules.NoticeRate(1f) > GuardRules.NoticeRate(14f) && GuardRules.NoticeRate(14f) > GuardRules.NoticeRate(33f));

            // ---- what counts is where the player is, never how he got there: the meter adds up what each moment gives
            float Walk(params (float distance, float seconds)[] legs)
            {
                float progress = 0f, unseen = 0f;
                foreach (var leg in legs) for (float t = 0f; t < leg.seconds - 1e-4f; t += Dt) progress = GuardRules.NoticeStep(progress, ref unseen, true, Dt, leg.distance, baseS, perM);
                return progress;
            }
            float there = Walk((5f, 0.3f), (25f, 0.8f), (12f, 0.2f)), back = Walk((12f, 0.2f), (25f, 0.8f), (5f, 0.3f));
            float expected = 0.3f * GuardRules.NoticeRate(5f) + 0.8f * GuardRules.NoticeRate(25f) + 0.2f * GuardRules.NoticeRate(12f);
            Check($"guard sight: the same stretches of time at the same distances fill the same, in whatever order the player walks them ({there:0.000} and {back:0.000}, the sum is {expected:0.000})",
                Math.Abs(there - back) < 1e-3f && Math.Abs(there - expected) < 1e-3f && there < 1f);

            bool neverDrops = true; float p = 0f, un = 0f, last = 0f;
            for (int f = 0; f < 180; f++)
            {
                float d = 5f + 25f * (f / 179f);                                         // (running away down the hall, in sight the whole time)
                p = GuardRules.NoticeStep(p, ref un, true, Dt, d, baseS, perM);
                if (p < last - 1e-6f) neverDrops = false;
                last = p;
            }
            Check($"guard sight: running away while still in sight never takes the notice back (the old stopwatch lost it as the limit grew): {last:0.00} of the meter after 3 s", neverDrops && last > 0.5f);

            // ---- out of sight: it stops growing, holds for a moment, then fades twice as fast as it filled
            {
                float prog = 0.6f, gone = 0f; float rate14 = GuardRules.NoticeRate(14f, baseS, perM);
                bool grew = false, heldAtFirst = true, fell = false; float holdEnd = -1f, atHoldEnd = 0f, t = 0f, emptyAt = -1f;
                while (t < 6f && emptyAt < 0f)
                {
                    float before = prog;
                    prog = GuardRules.NoticeStep(prog, ref gone, false, Dt, 14f, baseS, perM); t += Dt;
                    if (prog > before + 1e-7f) grew = true;
                    if (t <= GuardRules.NoticeHoldSeconds && Math.Abs(prog - 0.6f) > 1e-6f) heldAtFirst = false;
                    if (t > GuardRules.NoticeHoldSeconds && holdEnd < 0f) { holdEnd = t; atHoldEnd = prog; }
                    if (prog < before - 1e-7f) fell = true;
                    if (prog <= 0f) emptyAt = t;
                }
                float expectEmpty = GuardRules.NoticeHoldSeconds + 0.6f / (GuardRules.NoticeFadeFactor * rate14);
                Check($"guard sight: out of sight the meter never grows, holds for {GuardRules.NoticeHoldSeconds:0.00} s, then fades and is empty after {emptyAt:0.00} s (expected {expectEmpty:0.00} s)",
                    !grew && heldAtFirst && fell && emptyAt > 0f && Math.Abs(emptyAt - expectEmpty) < 0.05f);
                float fillPerSecond = rate14, fadePerSecond = GuardRules.NoticeFadeFactor * rate14;
                Check($"guard sight: it fades twice as fast as it fills at the same distance, as it always did ({fadePerSecond:0.00} against {fillPerSecond:0.00} of the meter per second at 14 m)", Math.Abs(fadePerSecond / fillPerSecond - 2f) < 1e-4f && GuardRules.NoticeFadeFactor == 2f);
            }

            // ---- a sight line that fails for a frame now and then no longer decides whether a player is ever noticed
            {
                // (the old rule: a stopwatch of seconds in sight, minus twice the time out of it, against the spotting time of the distance right now)
                float OldNoticeTime(int period, int failOn, float distance)
                {
                    float seen = 0f, t = 0f;
                    for (int f = 0; f < 60 * 30; f++)
                    {
                        bool visible = (f % period) != failOn;
                        if (visible) seen += Dt; else seen = Math.Max(0f, seen - 2f * Dt);
                        t += Dt;
                        if (seen >= GuardRules.SpotSeconds(distance)) return t;
                    }
                    return -1f;
                }
                float NewNoticeTime(int period, int failOn, float distance)
                {
                    float progress = 0f, unseen = 0f, t = 0f;
                    for (int f = 0; f < 60 * 30; f++)
                    {
                        bool visible = (f % period) != failOn;
                        progress = GuardRules.NoticeStep(progress, ref unseen, visible, Dt, distance, baseS, perM); t += Dt;
                        if (progress >= 1f) return t;
                    }
                    return -1f;
                }
                float oldThird = OldNoticeTime(3, 2, 14f), newThird = NewNoticeTime(3, 2, 14f), newHalf = NewNoticeTime(2, 1, 14f), oldHalf = OldNoticeTime(2, 1, 14f);
                Check($"guard sight: a player in plain sight is noticed even when the sight line fails one frame in three ({newThird:0.0} s; the old rule never got there: {(oldThird < 0f ? "never" : oldThird.ToString("0.0") + " s")}) or every other frame ({newHalf:0.0} s; old {(oldHalf < 0f ? "never" : oldHalf.ToString("0.0") + " s")})",
                    newThird > 0f && newThird < 3f && newHalf > 0f && newHalf < 6f && oldThird < 0f && oldHalf < 0f);
            }

            // ---- every guard keeps his own meters
            {
                var rng = new Random(11);
                const int Guards = 12; const int Frames = 600;
                var distances = new float[Guards, Frames]; var seenMap = new bool[Guards, Frames];
                for (int g = 0; g < Guards; g++) for (int f = 0; f < Frames; f++) { distances[g, f] = 2f + (float)rng.NextDouble() * 30f; seenMap[g, f] = rng.NextDouble() < 0.7; }
                float Alone(int g)
                {
                    float prog = 0f, un = 0f;
                    for (int f = 0; f < Frames; f++) prog = GuardRules.NoticeStep(prog, ref un, seenMap[g, f], Dt, distances[g, f], baseS, perM);
                    return prog;
                }
                var together = new float[Guards]; var unseenFor = new float[Guards];
                for (int f = 0; f < Frames; f++) for (int g = 0; g < Guards; g++) together[g] = GuardRules.NoticeStep(together[g], ref unseenFor[g], seenMap[g, f], Dt, distances[g, f], baseS, perM);
                bool same = true; for (int g = 0; g < Guards; g++) if (Math.Abs(together[g] - Alone(g)) > 1e-6f) same = false;
                Check("guard sight: a guard's meter comes out the same whether he is the only guard or one of twelve being stepped in the same frames (nothing is shared, there is no hidden group factor)", same);
                float backup = FillTime(14f, GuardRules.BackupSpotFactor);
                Check($"guard sight: a backup officer's meter fills in a quarter of the time ({backup:0.00} s against {medium:0.00} s at 14 m): that was and is the only thing that changes how fast a guard notices",
                    Math.Abs(backup - medium * GuardRules.BackupSpotFactor) <= Dt + 1e-3f);
            }

            // ---- the cone on the floor is the area the guard judges a player by
            const float Half = 65f, Range = 14f, LongHalf = GuardRules.LongSightHalfAngle;
            float far = Range * GuardRules.LongSightFactor;
            const int Rays = 48;
            var rimX = new float[Rays + 1]; var rimZ = new float[Rays + 1];
            for (int i = 0; i <= Rays; i++)
            {
                float reach = GuardRules.ConeRay(i, Rays, Half, Range, far, LongHalf, out float deg);
                rimX[i] = reach * (float)Math.Sin(deg * Math.PI / 180.0); rimZ[i] = reach * (float)Math.Cos(deg * Math.PI / 180.0);
            }
            bool InsideFan(float px, float pz)
            {
                for (int i = 0; i < Rays; i++)
                {
                    float ax = rimX[i], az = rimZ[i], bx = rimX[i + 1], bz = rimZ[i + 1];
                    // (the triangle guard, rim i, rim i + 1: the point is inside when it is on the same side of all three edges)
                    float e1 = ax * pz - az * px, e2 = (bx - ax) * (pz - az) - (bz - az) * (px - ax), e3 = -bx * (pz - bz) + bz * (px - bx);
                    bool neg = e1 < 0f || e2 < 0f || e3 < 0f, pos = e1 > 0f || e2 > 0f || e3 > 0f;
                    if (!(neg && pos)) return true;
                }
                return false;
            }
            {
                // how far the drawn fan (straight lines between the rim points) strays from the exact edge of what he sees, along every bearing
                double worstOut = 0, worstIn = 0;
                for (double th = -64.99; th <= 64.99; th += 0.01)
                {
                    int i = 0; while (i < Rays - 1 && Math.Atan2(rimX[i + 1], rimZ[i + 1]) * 180.0 / Math.PI < th) i++;
                    double t = th * Math.PI / 180.0, dx = Math.Sin(t), dz = Math.Cos(t), ex = rimX[i + 1] - rimX[i], ez = rimZ[i + 1] - rimZ[i];
                    double drawn = (rimX[i] * ez - rimZ[i] * ex) / (dx * ez - dz * ex), exact = GuardRules.RangeAt((float)th, Range, far, LongHalf);
                    worstOut = Math.Max(worstOut, drawn - exact); worstIn = Math.Max(worstIn, exact - drawn);
                }
                Check($"guard sight: the drawn cone is never more than 0.15 m off the exact edge of what he sees, along any bearing (longer by at most {worstOut:0.000} m, shorter by at most {worstIn:0.000} m)", worstOut < 0.15 && worstIn < 0.15);

                var rng = new Random(7);
                int compared = 0, mismatches = 0, inside = 0; string firstBad = "";
                for (int k = 0; k < 40000; k++)
                {
                    float r = 0.5f + (float)rng.NextDouble() * 35f, deg = (float)(rng.NextDouble() * 170.0 - 85.0);
                    float px = r * (float)Math.Sin(deg * Math.PI / 180.0), pz = r * (float)Math.Cos(deg * Math.PI / 180.0);
                    float reachHere = GuardRules.RangeAt(deg, Range, far, LongHalf);
                    bool nearEdge = Math.Abs(r - reachHere) < 0.15f || Math.Abs(Math.Abs(deg) - Half) * r * Math.PI / 180.0 < 0.15;
                    if (nearEdge) continue;                                              // (the fan is straight lines between the rays, up to 0.13 m off the true edge: see the check above)
                    bool seen = GuardRules.InVision(0f, 1f, px, pz, Half, Range, far, LongHalf), drawn = InsideFan(px, pz);
                    compared++; if (drawn) inside++;
                    if (seen != drawn) { mismatches++; if (firstBad.Length == 0) firstBad = $" first at {r:0.0} m, {deg:0.0} degrees: sees {seen}, drawn {drawn}"; }
                }
                Check($"guard sight: the cone drawn on the floor (its 49 rays, GuardRules.ConeRay) and the area the guard judges a player by (GuardRules.InVision) agree on {compared} random spots, {mismatches} differ{firstBad}", compared > 8000 && inside > 800 && mismatches == 0);
                float aheadReach = GuardRules.ConeRay(Rays / 2, Rays, Half, Range, far, LongHalf, out float aheadDeg), leftReach = GuardRules.ConeRay(0, Rays, Half, Range, far, LongHalf, out float leftDeg), rightReach = GuardRules.ConeRay(Rays, Rays, Half, Range, far, LongHalf, out float rightDeg);
                Check($"guard sight: the cone spans 65 degrees each side, is as long as the long beam dead ahead ({aheadReach:0.0} m) and 14 m at its edges: the range he had is kept",
                    Math.Abs(aheadDeg) < 1e-4f && Math.Abs(leftDeg + Half) < 1e-3f && Math.Abs(rightDeg - Half) < 1e-3f && Math.Abs(aheadReach - far) < 1e-3f && Math.Abs(leftReach - Range) < 1e-3f && Math.Abs(rightReach - Range) < 1e-3f
                    && GuardRules.InVision(0f, 1f, 0f, far - 0.1f, Half, Range, far, LongHalf) && !GuardRules.InVision(0f, 1f, 0f, far + 0.1f, Half, Range, far, LongHalf)
                    && GuardRules.InVision(0f, 1f, (float)Math.Sin(60 * Math.PI / 180.0) * 13.8f, (float)Math.Cos(60 * Math.PI / 180.0) * 13.8f, Half, Range, far, LongHalf)
                    && !GuardRules.InVision(0f, 1f, (float)Math.Sin(60 * Math.PI / 180.0) * 14.3f, (float)Math.Cos(60 * Math.PI / 180.0) * 14.3f, Half, Range, far, LongHalf)
                    && !GuardRules.InVision(0f, 1f, (float)Math.Sin(70 * Math.PI / 180.0) * 5f, (float)Math.Cos(70 * Math.PI / 180.0) * 5f, Half, Range, far, LongHalf));
            }

            // ---- what stops a guard's sight, in a small world of boxes (the game uses physics rays and one rule for both the cone and the check: GuardSight.Blocks)
            var boxes = new List<float[]>();                                             // x0, y0, z0, x1, y1, z1, blocks (1 or 0)
            void Box(float x0, float y0, float z0, float x1, float y1, float z1, bool blocks = true) => boxes.Add(new[] { x0, y0, z0, x1, y1, z1, blocks ? 1f : 0f });
            // (distance along the segment from a to b, 0 to 1, at which it first enters a box that blocks sight; 2 if it never does)
            float FirstBlock(float ax, float ay, float az, float bx, float by, float bz)
            {
                float best = 2f;
                foreach (var b in boxes)
                {
                    if (b[6] < 0.5f) continue;
                    float tmin = 0f, tmax = 1f; bool miss = false;
                    float[] o = { ax, ay, az }, dd = { bx - ax, by - ay, bz - az };
                    for (int k = 0; k < 3 && !miss; k++)
                    {
                        float lo = b[k], hi = b[k + 3];
                        if (Math.Abs(dd[k]) < 1e-9f) { if (o[k] < lo || o[k] > hi) miss = true; }
                        else
                        {
                            float t1 = (lo - o[k]) / dd[k], t2 = (hi - o[k]) / dd[k];
                            if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                            tmin = Math.Max(tmin, t1); tmax = Math.Min(tmax, t2);
                            if (tmin > tmax) miss = true;
                        }
                    }
                    if (!miss) best = Math.Min(best, tmin);
                }
                return best;
            }
            bool Clear(float ax, float ay, float az, float bx, float by, float bz) => FirstBlock(ax, ay, az, bx, by, bz) > 1.5f;
            const float EyeY = 1f + GuardRules.EyeAboveMiddle;                           // a guard standing at the origin, facing +z, his middle a metre up
            const float Stand = GuardRules.BodyHeight, Crouch = GuardRules.BodyHeight * 0.65f;
            bool Sees(float z, float height, float x = 0f) => GuardRules.SeesBody(0f, EyeY, 0f, x, 0f, z, height, Clear);

            Check("guard sight: in the open a player is seen at 3 m, 14 m and 33 m, standing or crouching",
                Sees(3f, Stand) && Sees(14f, Stand) && Sees(33f, Stand) && Sees(3f, Crouch) && Sees(14f, Crouch) && Sees(33f, Crouch));

            Box(-8f, 0f, 36f, 8f, 3.5f, 37f);                                            // the far wall, behind every player below
            Check("guard sight: what is behind the player does not matter: a crouching player with a wall behind him is seen (the old check aimed past his head and met the wall)", Sees(14f, Crouch) && Sees(30f, Crouch) && Sees(14f, Stand));

            Box(-8f, 0f, 9.9f, 8f, 3.5f, 10.1f);                                         // a wall (or a shut door) across the way at 10 m
            Check("guard sight: a wall or a shut door stops his sight: a player behind it is not seen, one in front of it is", !Sees(14f, Stand) && !Sees(14f, Crouch) && Sees(8f, Stand) && Sees(9.5f, Crouch));
            Check("guard sight: it stops the line to a player off to the side as well (he is not seen through the wall at an angle)", !Sees(14f, Stand, 3f) && !Sees(14f, Stand, -6f));
            boxes.Clear(); Box(-8f, 0f, 36f, 8f, 3.5f, 37f);

            Box(-2f, 0f, 6f, 2f, 0.75f, 8f);                                             // a desk
            Check("guard sight: a desk does not hide a player: his head is over it, standing or crouching", Sees(12f, Stand) && Sees(12f, Crouch) && Sees(7f, Stand));
            boxes.Clear(); Box(-8f, 0f, 36f, 8f, 3.5f, 37f);

            Box(-3f, 0f, 9.5f, 3f, 1.5f, 9.7f);                                          // a cubicle partition, 1.5 m high
            Check("guard sight: a 1.5 m partition hides a player who crouches right behind it, not one who stands (his head is above it), and a crouching player far behind it is seen over the top",
                Sees(10.4f, Stand) && !Sees(10.4f, Crouch) && Sees(26f, Crouch));
            boxes.Clear(); Box(-8f, 0f, 36f, 8f, 3.5f, 37f);

            Box(-4f, 1.7f, 5f, 4f, 2.2f, 5.4f);                                          // a bulkhead hanging at eye height
            Check("guard sight: something hanging at the height of his eyes cuts the cone short and he does not see past it either, even along a line that would pass under it", !Sees(12f, Stand) && !Sees(12f, Crouch) && Sees(4f, Stand));
            boxes.Clear(); Box(-8f, 0f, 36f, 8f, 3.5f, 37f);

            Box(-4f, 1.6f, 12.8f, 4f, 2f, 13.2f);                                        // a beam hanging across the way at 13 m, from 1.6 m to 2 m up
            {
                const float TallEye = 1.35f + GuardRules.EyeAboveMiddle * 1.35f;         // the boss: built 1.35 times bigger, his eyes 2.43 m up
                bool TallSees(float z, float height) => GuardRules.SeesBody(0f, TallEye, 0f, 0f, 0f, z, height, Clear);
                Check("guard sight: a beam that hides only a standing player's head from a tall guard (the boss, eyes at 2.4 m) does not hide him, any clear line to his head, chest or knees is enough; to an ordinary guard, whose eyes are level with the beam, it is a wall",
                    TallSees(14f, Stand) && TallSees(14f, Crouch) && !Sees(14f, Stand) && !Sees(14f, Crouch) && Sees(10f, Stand));
            }
            boxes.Clear(); Box(-8f, 0f, 36f, 8f, 3.5f, 37f);

            Box(-1f, 0f, 6f, 1f, 2.6f, 7f, false);                                       // a loose crate, a big one: it has a rigidbody
            Box(-0.6f, 0f, 11.5f, 0.6f, 2.2f, 12.5f, false);                             // a player carrying a server, say
            Check("guard sight: loose things (a crate, the loot somebody carries, another player) do not hide a player; the cone is not cut short by them either", Sees(14f, Stand) && Sees(14f, Crouch) && Sees(12f, Stand) && Sees(25f, Crouch));
            boxes.Clear(); Box(-8f, 0f, 36f, 8f, 3.5f, 37f);

            Box(2.5f, 0f, 5f, 3.5f, 3.5f, 9f);                                           // a pillar beside the way
            Check("guard sight: a pillar to one side does not hide a player who stands in the clear", Sees(14f, Stand) && Sees(14f, Stand, 0.5f) && !Sees(14f, Stand, 4.5f));
            boxes.Clear();

            // ---- the cone cut short by a wall and the sight stop in the same place: on every ray of the cone, a player is seen up to where the ray ends and not past it
            Box(-6f, 0f, 9.9f, 6f, 3.5f, 10.1f);                                         // a wall across the way
            Box(8f, 0f, 3f, 9f, 3.5f, 5f);                                               // a pillar on the right
            Box(-9f, 1.7f, 2f, -6f, 2.4f, 12f);                                          // something hanging at eye height, on the left
            Box(-3f, 0f, 3f, 0f, 0.75f, 4f);                                             // a desk (does not cut the cone)
            Box(1f, 0f, 3f, 2f, 2.5f, 3.5f, false);                                      // a crate (does not cut the cone)
            {
                int onRays = 0, wrong = 0, cut = 0; string firstBad = "";
                for (int i = 1; i < Rays; i++)                                           // (not the two outermost rays: they run exactly along the edge of the angle)
                {
                    float reach = GuardRules.ConeRay(i, Rays, Half, Range, far, LongHalf, out float deg);
                    float dx = (float)Math.Sin(deg * Math.PI / 180.0), dz = (float)Math.Cos(deg * Math.PI / 180.0);
                    float hit = FirstBlock(0f, EyeY, 0f, dx * reach, EyeY, dz * reach);   // (the cone's ray: level at his eyes, as far as the wall)
                    float ends = hit > 1.5f ? reach : hit * reach;
                    if (ends < reach - 0.01f) cut++;
                    for (float r = 0.6f; r <= reach - 0.01f; r += 0.4f)
                    {
                        if (Math.Abs(r - ends) < 0.15f) continue;
                        bool shouldSee = r < ends;
                        bool seen = GuardRules.InVision(0f, 1f, dx * r, dz * r, Half, Range, far, LongHalf) && GuardRules.SeesBody(0f, EyeY, 0f, dx * r, 0f, dz * r, Stand, Clear);
                        onRays++;
                        if (seen != shouldSee) { wrong++; if (firstBad.Length == 0) firstBad = $" first on ray {i} at {r:0.0} m (the ray ends at {ends:0.0} m)"; }
                    }
                }
                Check($"guard sight: with walls, a pillar, a hanging bulkhead, a desk and a crate about, a standing player is seen exactly as far as each ray of the cone reaches: {onRays} spots on 49 rays ({cut} of them cut short), {wrong} wrong{firstBad}", onRays > 1500 && cut >= 8 && wrong == 0);
            }
        }
    }
}
