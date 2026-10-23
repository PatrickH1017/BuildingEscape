using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Guard sight with the real guards, the real raycasts and the real cone mesh (the owner, 2026-10-22: "Fix and improve guard vision and player detection"). The rules on their own are in NetDevGuardSightTest.cs; this is the game:
    /// a guard of floor 1 is put on the best straight, clear line the floor has (found with the real physics), held still, and the real player is put in front of him at chosen distances.
    ///  - the cone drawn on the floor is read from the guard's own mesh, and on each of its rays a player 0.5 m inside the rim is seen and one 0.5 m outside is not (and a few hundred random spots agree);
    ///  - the notice meter (<see cref="GuardAI.NoticeProgress"/>) fills at 1 / the spotting time of the distance he is at, the closer the faster, at every distance up to the longest sight, and a guard's meter does not depend on how many guards are looking;
    ///  - a wall in the way (a ray of the cone that ends early) keeps the meter at 0 though the player is inside the angle and the range; turning away holds the meter, then it fades, and turning back fills it again;
    ///  - a crouching player in the open is seen, a loose crate or another officer in the way does not hide him or shorten the cone;
    ///  - at the longest sight he still notices the player in the time the rules say, and starts his radio call.
    /// The meter is slowed down (x4) for the timed parts so that nobody calls backup while it is measured; only the last part runs at the real speed (and the call is cut at once). Run: <c>qa_regress.ps1 -Methods GuardSight</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int guardSightFails;

        void GuardSightCheck(string name, bool ok, string detail = "")
        {
            if (!ok) guardSightFails++;
            Log($"[GuardSight] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static void GuardSightInfo(string text) => Log("[GuardSight] INFO " + text);

        static readonly MethodInfo gsCanSee = typeof(GuardAI).GetMethod("CanSee", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo gsVerts = typeof(GuardVision).GetField("verts", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly Collider[] gsOverlap = new Collider[24];

        /// <summary>The guard's own sight check, private in the game (GuardAI.CanSee).</summary>
        static bool GsCanSee(GuardAI g, WobblyPlayer p) => (bool)gsCanSee.Invoke(g, new object[] { p });

        /// <summary>The sight line as it was before 2026-10-22 (only the ray: one line from 0.6 m above his head to 0.9 m above the player's middle, which must meet the player first), to show next to the new answer.</summary>
        static bool GuardSightOldRay(GuardAI g, WobblyPlayer p)
        {
            var eye = g.transform.position + Vector3.up * 1.6f;
            var to = p.transform.position + Vector3.up * 0.9f - eye;
            float far = g.sightRange * Mathf.Max(1f, g.longSightFactor);
            if (Physics.Raycast(eye, to.normalized, out var hit, far, g.sightBlockers, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<WobblyPlayer>() == p;
            return true;
        }

        sealed class GsLine { public float length; public Vector3 floor, dir; }
        sealed class GsMeasure { public float rate, distance, progress; }

        static float GuardSightFlat(GuardAI g, WobblyPlayer p)
        {
            var d = p.transform.position - g.transform.position; d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Stops a guard where he stands: no route, no hearing, the agent still, and nothing turns him.</summary>
        static void GuardSightFreeze(GuardAI x)
        {
            x.patrolPoints = new Transform[0]; x.roam = null; x.hearingMultiplier = 0f;
            var agent = x.GetComponent<NavMeshAgent>();
            if (agent && agent.enabled && agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = true; agent.velocity = Vector3.zero; agent.updateRotation = false; }
        }

        /// <summary>How far a level line from <paramref name="eye"/> goes before something that stops a guard's sight (<see cref="GuardSight.Blocks"/>) is in the way.</summary>
        static float GuardSightClearLength(Vector3 eye, Vector3 dir, float max, RaycastHit[] buf)
        {
            int count = Physics.RaycastNonAlloc(eye, dir, buf, max, ~0, QueryTriggerInteraction.Ignore);
            float best = max;
            for (int i = 0; i < count; i++)
                if (buf[i].distance < best && GuardSight.Blocks(buf[i].collider, null)) best = buf[i].distance;
            return best;
        }

        /// <summary>Looks over the floor's NavMesh for the longest straight line a guard can look along (at his eyes' height), up to 36 m.</summary>
        IEnumerator GuardSightFindLine(GsLine best)
        {
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0) yield break;
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, sumY = 0f;
            foreach (var v in tri.vertices) { minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x); minZ = Mathf.Min(minZ, v.z); maxZ = Mathf.Max(maxZ, v.z); sumY += v.y; }
            float y = sumY / tri.vertices.Length;
            var buf = new RaycastHit[64];
            int work = 0;
            for (float x = minX; x <= maxX; x += 4f)
                for (float z = minZ; z <= maxZ; z += 4f)
                {
                    if (!NavMesh.SamplePosition(new Vector3(x, y, z), out var nh, 1.5f, NavMesh.AllAreas)) continue;
                    var floor = nh.position;
                    var eye = floor + Vector3.up * (1f + GuardRules.EyeAboveMiddle);
                    for (int k = 0; k < 16; k++)
                    {
                        var dir = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
                        float len = GuardSightClearLength(eye, dir, 36f, buf);
                        if (len > best.length) { best.length = len; best.floor = floor; best.dir = dir; }
                    }
                    if (best.length >= 36f) yield break;
                    if (++work % 120 == 0) yield return null;
                }
        }

        /// <summary>Puts the player at <paramref name="at"/> (x and z), standing on the floor, then lets one frame go by so that the guard's Update sees him, and measures how fast the guard's meter fills over <paramref name="seconds"/>.</summary>
        IEnumerator GuardSightMeasure(GuardAI g, WobblyPlayer player, Vector3 at, float floorY, float seconds, GsMeasure m)
        {
            player.TeleportTo(new Vector3(at.x, floorY + 1.03f, at.z), player.transform.rotation);
            g.ForgetNotices();
            yield return null;
            float p0 = g.NoticeProgress(player), t0 = Time.time, d0 = GuardSightFlat(g, player);
            while (Time.time - t0 < seconds) yield return null;
            float p1 = g.NoticeProgress(player), t1 = Time.time, d1 = GuardSightFlat(g, player);
            m.rate = t1 > t0 ? (p1 - p0) / (t1 - t0) : 0f;     // (both readings are taken in the same part of the frame, so the guard's Updates in between add up to exactly t1 - t0)
            m.distance = 0.5f * (d0 + d1);
            m.progress = p1;
        }

        static bool GuardSightInsideFan(Vector3[] verts, int n, Vector3 g, Vector3 p)
        {
            for (int i = 0; i < n; i++)
            {
                var a = verts[n + 2 + i]; var b = verts[n + 3 + i];
                float ax = a.x - g.x, az = a.z - g.z, bx = b.x - g.x, bz = b.z - g.z, px = p.x - g.x, pz = p.z - g.z;
                // (the triangle guard, rim i, rim i + 1: the point is inside when it is on the same side of all three edges)
                float e1 = ax * pz - az * px, e2 = (bx - ax) * (pz - az) - (bz - az) * (px - ax), e3 = -bx * (pz - bz) + bz * (px - bx);
                bool neg = e1 < 0f || e2 < 0f || e3 < 0f, pos = e1 > 0f || e2 > 0f || e3 > 0f;
                if (!(neg && pos)) return true;
            }
            return false;
        }

        static float GuardSightSegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az, len2 = dx * dx + dz * dz;
            float t = len2 > 1e-9f ? Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / len2) : 0f;
            float qx = ax + dx * t - px, qz = az + dz * t - pz;
            return Mathf.Sqrt(qx * qx + qz * qz);
        }

        /// <summary>How far (on the floor) a point is from the edge of the drawn cone.</summary>
        static float GuardSightEdgeDistance(Vector3[] verts, int n, Vector3 g, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var a = verts[n + 2 + i]; var b = verts[n + 3 + i];
                best = Mathf.Min(best, GuardSightSegmentDistance(p.x, p.z, a.x, a.z, b.x, b.z));
            }
            var first = verts[n + 2]; var last = verts[n + 2 + n];
            best = Mathf.Min(best, GuardSightSegmentDistance(p.x, p.z, g.x, g.z, first.x, first.z));
            best = Mathf.Min(best, GuardSightSegmentDistance(p.x, p.z, g.x, g.z, last.x, last.z));
            return best;
        }

        /// <summary>Is there room for a person to stand here (no wall, door or furniture through the middle of him)? Loose things and other officers do not count.</summary>
        static bool GuardSightStandable(Vector3 floorPoint)
        {
            int c = Physics.OverlapSphereNonAlloc(floorPoint + Vector3.up * 0.95f, 0.4f, gsOverlap, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < c; i++) if (GuardSight.Blocks(gsOverlap[i], null)) return false;
            return true;
        }

        IEnumerator GuardSightScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            tm.GoToLevel(1);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return WaitFor(() => !tm.Loading && hm.Floor == 1 && hm.Phase == HeistPhase.Heist, 90, "floor 1");
            hm.godMode = true; hm.freezeTimer = true;
            yield return WaitFor(() => GuardAI.All.Count(x => x && !x.Dead && !x.IsBoss) >= 3, 30, "three guards on the floor");
            yield return new WaitForSeconds(1.0f);
            if (gsCanSee == null || gsVerts == null)
            {
                GuardSightCheck("the guard's CanSee and the cone's vertex list are where this test looks for them", false, "(were they renamed? update NetDevGuardSight.cs)");
                Log($"[GuardSight] DONE fails={guardSightFails}");
                Quit(1);
                yield break;
            }

            // ---- one guard to test (held still), the others blind and still
            var live = GuardAI.All.Where(x => x && !x.Dead && !x.IsPuppet && !x.IsBoss).ToList();
            var g = live.FirstOrDefault(x => !x.IsBackup && x.noticeGate == null && !x.stationary) ?? live[0];
            var helpers = live.Where(x => x != g).ToList();
            foreach (var x in GuardAI.All.ToList())
            {
                if (!x) continue;
                GuardSightFreeze(x);
                if (x != g) x.sightRange = 0.1f;
            }
            GuardVision.AlwaysShownForTests = true;
            var vision = g.GetComponent<GuardVision>();
            var agentG = g.GetComponent<NavMeshAgent>();
            GuardSightCheck($"a guard of floor 1 to test with ({g.name}), {helpers.Count} others held still and blind, his cone is built", vision != null && agentG != null && gsVerts.GetValue(vision) != null);
            if (vision == null || agentG == null || gsVerts.GetValue(vision) == null) { Log($"[GuardSight] DONE fails={guardSightFails}"); Quit(1); yield break; }

            // ---- the best straight, clear line the floor has
            var line = new GsLine();
            yield return GuardSightFindLine(line);
            {
                var checkBuf = new RaycastHit[64];
                var eye0 = line.floor + Vector3.up * (1f + GuardRules.EyeAboveMiddle);
                while (line.length > 6f && !GuardSight.LineClear(eye0, eye0 + line.dir * line.length, ~0, null, checkBuf)) line.length -= 1.5f;      // (the list of hits may have been full: make sure)
            }
            GuardSightCheck($"the floor has a straight line to look along of at least 12 m ({line.length:0.0} m from {line.floor}, heading {Quaternion.LookRotation(line.dir).eulerAngles.y:0} degrees)", line.length >= 12f);
            if (line.length < 12f) { Log($"[GuardSight] DONE fails={guardSightFails}"); Quit(1); yield break; }

            // ---- put him at its start looking down it
            bool warped = agentG.Warp(line.floor);
            var heading = line.dir;
            g.transform.rotation = Quaternion.LookRotation(heading);
            yield return null;
            var gpos = g.transform.position;                                 // (the middle of his body)
            float floorY = line.floor.y;
            GuardSightCheck($"he stands at the start of the line, facing along it, eyes {g.EyePoint.y - floorY:0.00} m above the floor", warped && Vector3.Distance(new Vector3(gpos.x, 0f, gpos.z), new Vector3(line.floor.x, 0f, line.floor.z)) < 0.5f && Vector3.Angle(g.transform.forward, heading) < 1f);
            Vector3 On(float d) => new Vector3(gpos.x + heading.x * d, floorY, gpos.z + heading.z * d);
            void Put(Vector3 flat) => player.TeleportTo(new Vector3(flat.x, floorY + 1.03f, flat.z), player.transform.rotation);

            float far = g.sightRange * Mathf.Max(1f, g.longSightFactor);
            float rangeFar = Mathf.Min(line.length - 1.0f, far - 0.6f);
            float rangeMid = Mathf.Min(14f, rangeFar);
            float rangeNear = 3f;

            // ================= 1. a player inside the cone, in range, with a clear view, is seen - close, medium and at the longest sight
            {
                var seenAt = new List<string>(); bool all = true;
                foreach (var d in new[] { rangeNear, rangeMid, rangeFar }.Distinct())
                {
                    Put(On(d));
                    bool sees = GsCanSee(g, player), old = GuardSightOldRay(g, player);
                    all &= sees;
                    seenAt.Add($"{d:0.0} m: {(sees ? "seen" : "NOT seen")} (old line: {(old ? "seen" : "not seen")})");
                }
                GuardSightCheck("a standing player straight ahead in the clear is seen close by, at medium range and at the longest sight", all, "(" + string.Join(", ", seenAt) + ")");
            }
            {
                // beyond the longest sight, and behind him, he sees nobody
                bool beyondOk = true, behindOk = true;
                if (line.length >= far + 1.5f) { Put(On(far + 1.5f)); beyondOk = !GsCanSee(g, player); }
                Put(On(-4f)); behindOk = !GsCanSee(g, player);
                GuardSightCheck("nobody is seen behind him" + (line.length >= far + 1.5f ? " or beyond the longest sight" : " (the floor has no line long enough to test beyond the longest sight)"), beyondOk && behindOk);
            }

            // ================= 2. the cone on the floor is what he sees
            Put(On(3f));
            yield return new WaitForSeconds(1.0f);                           // (the camera follows the player, the cone is drawn when the camera is near, and rebuilt every second frame)
            var verts = (Vector3[])gsVerts.GetValue(vision);
            int n = (verts.Length - 1) / 2 - 1;
            float aheadReach = new Vector2(verts[n + 2 + n / 2].x - gpos.x, verts[n + 2 + n / 2].z - gpos.z).magnitude;
            float expectedAhead = Mathf.Min(line.length, far);
            GuardSightCheck($"the drawn cone is as long straight ahead as the clear line (cone {aheadReach:0.00} m, line {expectedAhead:0.00} m)", Mathf.Abs(aheadReach - expectedAhead) < 0.15f);
            {
                int inChecked = 0, inWrong = 0, outChecked = 0, outWrong = 0, cut = 0; string firstBad = "";
                int cutRay = -1; float cutReach = 0f, cutNominal = 0f; Vector3 cutDir = Vector3.zero;
                for (int i = 1; i < n; i++)                                  // (not the two outermost rays: they run exactly along the edge of the angle)
                {
                    var rim = verts[n + 2 + i];
                    var off = new Vector3(rim.x - gpos.x, 0f, rim.z - gpos.z);
                    float reach = off.magnitude;
                    if (reach < 1.5f) continue;
                    var dir = off / reach;
                    float nominal = GuardRules.RangeAt(Vector3.SignedAngle(g.transform.forward, dir, Vector3.up), g.sightRange, far, g.longSightAngle);
                    if (reach < nominal - 0.3f) { cut++; if (cutRay < 0 && reach >= 3f && nominal - reach >= 3.5f) { cutRay = i; cutReach = reach; cutNominal = nominal; cutDir = dir; } }
                    Put(gpos + dir * (reach - 0.5f));
                    bool seenIn = GsCanSee(g, player); inChecked++;
                    if (!seenIn) { inWrong++; if (firstBad.Length == 0) firstBad = $" first: ray {i} ({reach:0.0} m of {nominal:0.0}), a player 0.5 m inside the rim is NOT seen"; }
                    Put(gpos + dir * (reach + 0.5f));
                    bool seenOut = GsCanSee(g, player); outChecked++;
                    if (seenOut) { outWrong++; if (firstBad.Length == 0) firstBad = $" first: ray {i} ({reach:0.0} m of {nominal:0.0}), a player 0.5 m outside the rim IS seen"; }
                }
                GuardSightCheck($"on every ray of the drawn cone a player 0.5 m inside the rim is seen and one 0.5 m outside is not ({inChecked} rays, {cut} of them cut short by something): {inWrong} wrong inside, {outWrong} wrong outside{firstBad}", inChecked >= 30 && inWrong == 0 && outWrong == 0);

                // a few hundred random free spots: seen exactly when inside the drawn cone (the fan is straight lines between 49 rays, so spots right at its edge are left out)
                var rng = new System.Random(5);
                int sampled = 0, mismatches = 0; string sampleBad = "";
                for (int k = 0; k < 8000 && sampled < 300; k++)                // (in a hall most random spots are inside the walls: keep drawing until there are enough free ones)
                {
                    float r = 1f + (float)rng.NextDouble() * 34f, deg = (float)(rng.NextDouble() * 150.0 - 75.0);
                    var dir = Quaternion.AngleAxis(deg, Vector3.up) * g.transform.forward; dir.y = 0f; dir.Normalize();
                    var spot = gpos + dir * r;
                    if (GuardSightEdgeDistance(verts, n, gpos, spot) < 0.4f) continue;
                    if (!GuardSightStandable(new Vector3(spot.x, floorY, spot.z))) continue;
                    bool drawn = GuardSightInsideFan(verts, n, gpos, spot);
                    Put(spot);
                    bool seen = GsCanSee(g, player);
                    sampled++;
                    if (seen != drawn) { mismatches++; if (sampleBad.Length == 0) sampleBad = $" first at {r:0.0} m, {deg:0} degrees: sees {seen}, drawn {drawn}"; }
                }
                GuardSightInfo($"{sampled} random spots in and around the cone: {mismatches} differ from the drawn cone{sampleBad} (what is left is the straight edges between the rays, and thin things that fall between two rays)");
                GuardSightCheck($"random spots agree with the drawn cone ({sampled - mismatches} of {sampled})", sampled >= 60 && mismatches * 20 <= sampled);

                // ================= 3. the notice meter fills faster the closer the player is, at every distance
                const float K = 4f;                                          // (the meter is slowed down so that nobody calls backup while it is measured)
                g.spotBase = GuardRules.SpotBase * K; g.spotPerMetre = GuardRules.SpotPerMetre * K;
                var distances = new[] { 1.5f, 3f, 5f, 8f, 11f, 13.5f, 14f, 14.5f, 17f, 20f, 24f, 28f, rangeFar }.Where(d => d <= rangeFar + 0.01f).Distinct().OrderBy(d => d).ToList();
                var rates = new List<float>(); bool onFormula = true, faster = true; float worst = 0f; var listing = new List<string>();
                foreach (var d in distances)
                {
                    var m = new GsMeasure();
                    yield return GuardSightMeasure(g, player, On(d), floorY, 0.4f, m);
                    float expect = GuardRules.NoticeRate(m.distance, g.spotBase, g.spotPerMetre);
                    float off2 = Mathf.Abs(m.rate / expect - 1f);
                    worst = Mathf.Max(worst, off2);
                    if (off2 > 0.04f) onFormula = false;
                    if (rates.Count > 0 && m.rate >= rates[rates.Count - 1]) faster = false;
                    rates.Add(m.rate);
                    listing.Add($"{m.distance:0.0} m {m.rate:0.000}/s");
                }
                GuardSightInfo($"the meter's speed at {distances.Count} distances (of the whole meter per second, slowed x{K:0}): " + string.Join(", ", listing));
                GuardSightCheck($"the notice meter fills at 1 / the spotting time of the distance he is at, at every distance from {distances.First():0.0} m to {distances.Last():0.0} m (worst {worst * 100f:0.0} % off)", distances.Count >= 5 && onFormula);
                GuardSightCheck($"and the closer the player the faster, with no step anywhere ({rates.First() / Mathf.Max(1e-6f, rates.Last()):0.0} times as fast at {distances.First():0.0} m as at {distances.Last():0.0} m)", faster && rates.First() > rates.Last() * 3f);

                // ---- the same again gives the same
                var repeat = new List<float>();
                for (int k = 0; k < 4; k++)
                {
                    var m = new GsMeasure();
                    yield return GuardSightMeasure(g, player, On(rangeMid), floorY, 0.4f, m);
                    repeat.Add(m.rate / GuardRules.NoticeRate(m.distance, g.spotBase, g.spotPerMetre));
                }
                GuardSightCheck($"the same player at the same place is noticed at the same speed every time ({string.Join(", ", repeat.Select(x => x.ToString("0.000")))} of the formula)", repeat.Max() - repeat.Min() < 0.04f && repeat.All(x => Mathf.Abs(x - 1f) < 0.04f));

                // ================= 4. a wall in the way: inside the angle and the range, but no view - the meter stays at 0; in front of the wall it fills
                if (cutRay >= 0)
                {
                    var behind = gpos + cutDir * (cutReach + 1.5f);
                    var before = gpos + cutDir * (cutReach - 1.0f);
                    bool okSpots = GuardSightStandable(new Vector3(behind.x, floorY, behind.z)) && GuardSightStandable(new Vector3(before.x, floorY, before.z));
                    if (okSpots)
                    {
                        var mBehind = new GsMeasure();
                        yield return GuardSightMeasure(g, player, behind, floorY, 1.0f, mBehind);
                        bool seesBehind = GsCanSee(g, player);
                        var mBefore = new GsMeasure();
                        yield return GuardSightMeasure(g, player, before, floorY, 0.5f, mBefore);
                        bool seesBefore = GsCanSee(g, player);
                        GuardSightCheck($"behind a wall that cuts his cone short (ray {cutRay}: the wall at {cutReach:0.0} m, his sight would reach {cutNominal:0.0} m) the meter stays at 0 for a second ({mBehind.progress:0.000}); in front of it the meter fills ({mBefore.progress:0.000})",
                            !seesBehind && mBehind.progress <= 1e-6f && seesBefore && mBefore.progress > 0.01f);
                    }
                    else GuardSightInfo("the spots on either side of the nearest wall are not free to stand in: the wall test is left out");
                }
                else GuardSightInfo("no ray of the cone is cut short by a wall from this spot: the wall test is left out");
            }

            // ================= 5. leaving and entering the cone: the meter stops, holds, fades, and fills again
            {
                float d = Mathf.Min(10f, rangeFar);
                var m = new GsMeasure();
                yield return GuardSightMeasure(g, player, On(d), floorY, 1.0f, m);
                float grown = g.NoticeProgress(player);
                g.transform.rotation = Quaternion.LookRotation(-heading);           // he turns his back on the player
                float atTurn = g.NoticeProgress(player);
                float t0 = Time.time;
                while (Time.time - t0 < 0.12f) yield return null;
                float held = g.NoticeProgress(player);
                while (Time.time - t0 < 0.6f) yield return null;
                float faded = g.NoticeProgress(player);
                g.transform.rotation = Quaternion.LookRotation(heading);            // and back
                float atReturn = g.NoticeProgress(player);
                float t1 = Time.time;
                while (Time.time - t1 < 0.4f) yield return null;
                float regrown = g.NoticeProgress(player);
                GuardSightCheck($"he turns away: the meter stops growing and holds for a moment ({atTurn:0.000} -> {held:0.000}), then fades ({faded:0.000}); he turns back and it fills again ({atReturn:0.000} -> {regrown:0.000})",
                    grown > 0.03f && Mathf.Abs(held - atTurn) < 1e-4f && faded < held - 0.01f && regrown > atReturn + 0.02f);
            }

            // ================= 6. a crouching player in the open is seen as fast as a standing one; a loose crate or another officer in the way does not hide him or shorten the cone
            {
                float d = Mathf.Min(8f, rangeFar);
                var standing = new GsMeasure();
                yield return GuardSightMeasure(g, player, On(d), floorY, 0.4f, standing);
                player.testCrouch = true;
                yield return new WaitForSeconds(0.8f);
                var crouching = new GsMeasure();
                yield return GuardSightMeasure(g, player, On(d), floorY, 0.4f, crouching);
                bool crouchSees = GsCanSee(g, player), crouchOld = GuardSightOldRay(g, player);
                float ratio = crouching.rate / Mathf.Max(1e-6f, standing.rate);
                GuardSightCheck($"a crouching player ({(player.IsCrouching ? "crouching" : "NOT crouching")}) in the open at {d:0.0} m is seen and noticed as fast as a standing one ({crouching.rate:0.000}/s against {standing.rate:0.000}/s); the old line said {(crouchOld ? "seen" : "NOT seen")}",
                    player.IsCrouching && crouchSees && Mathf.Abs(ratio - 1f) < 0.05f);
                player.testCrouch = false;
                yield return new WaitForSeconds(0.8f);

                Put(On(Mathf.Min(9f, rangeFar)));
                float reachBefore = new Vector2(verts[n + 2 + n / 2].x - gpos.x, verts[n + 2 + n / 2].z - gpos.z).magnitude;
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "GuardSightTestCrate";
                box.transform.localScale = new Vector3(1.2f, 2.4f, 1.2f);
                var mid = On(Mathf.Min(9f, rangeFar) * 0.45f);
                box.transform.position = new Vector3(mid.x, floorY + 1.25f, mid.z);
                var boxBody = box.AddComponent<Rigidbody>(); boxBody.isKinematic = true;            // (a loose crate: it has a rigidbody, so it moves, so it is not a wall)
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.4f);
                bool seesThroughBox = GsCanSee(g, player), oldThroughBox = GuardSightOldRay(g, player);
                float reachWithBox = new Vector2(verts[n + 2 + n / 2].x - gpos.x, verts[n + 2 + n / 2].z - gpos.z).magnitude;
                GuardSightCheck($"a loose crate in the line does not hide the player or shorten the cone (seen {seesThroughBox}; cone {reachBefore:0.0} m -> {reachWithBox:0.0} m); the old line said {(oldThroughBox ? "seen" : "NOT seen")}", seesThroughBox && Mathf.Abs(reachWithBox - reachBefore) < 0.1f);
                Destroy(box);

                if (helpers.Count > 0)
                {
                    var officer = helpers[0];
                    var officerAgent = officer.GetComponent<NavMeshAgent>();
                    var home = officer.transform.position;
                    var officerCollider = officer.GetComponent<Collider>();
                    GuardSightCheck("the other officers' bodies do not stop a guard's sight (GuardSight.Blocks)", officerCollider != null && !GuardSight.Blocks(officerCollider, g.transform));
                    if (officerAgent != null && officerAgent.enabled && officerAgent.Warp(On(Mathf.Min(9f, rangeFar) * 0.5f)))
                    {
                        Physics.SyncTransforms();
                        yield return new WaitForSeconds(0.4f);
                        bool seesThroughOfficer = GsCanSee(g, player);
                        float reachWithOfficer = new Vector2(verts[n + 2 + n / 2].x - gpos.x, verts[n + 2 + n / 2].z - gpos.z).magnitude;
                        GuardSightCheck($"another officer standing in the line does not hide the player or shorten the cone (seen {seesThroughOfficer}; cone {reachBefore:0.0} m -> {reachWithOfficer:0.0} m)", seesThroughOfficer && Mathf.Abs(reachWithOfficer - reachBefore) < 0.1f);
                        officerAgent.Warp(home);
                    }
                    else GuardSightInfo("could not put another officer into the line on the NavMesh: that test is left out");
                }
            }

            // ================= 7. every guard keeps his own meter: how many are looking changes nothing
            {
                float d = Mathf.Min(12f, rangeFar);
                var alone = new GsMeasure();
                yield return GuardSightMeasure(g, player, On(d), floorY, 0.4f, alone);
                var placed = new List<GuardAI>(); var offsets = new List<float>();
                float[] spots = { 3f, -3f, 6f, -6f };
                for (int i = 0; i < helpers.Count && i < spots.Length; i++)
                {
                    var h = helpers[i];
                    var ha = h.GetComponent<NavMeshAgent>();
                    if (ha == null || !ha.enabled) continue;
                    if (!ha.Warp(On(spots[i]))) continue;
                    h.transform.rotation = Quaternion.LookRotation(heading);
                    h.sightRange = g.sightRange; h.spotBase = g.spotBase; h.spotPerMetre = g.spotPerMetre;
                    placed.Add(h); offsets.Add(spots[i]);
                }
                if (placed.Count >= 2)
                {
                    player.TeleportTo(new Vector3(On(d).x, floorY + 1.03f, On(d).z), player.transform.rotation);
                    foreach (var h in placed) h.ForgetNotices();
                    g.ForgetNotices();
                    yield return null;
                    var p0 = new float[placed.Count + 1]; float t0 = Time.time;
                    p0[0] = g.NoticeProgress(player); for (int i = 0; i < placed.Count; i++) p0[i + 1] = placed[i].NoticeProgress(player);
                    while (Time.time - t0 < 0.5f) yield return null;
                    float dt = Time.time - t0;
                    float rateA = (g.NoticeProgress(player) - p0[0]) / dt;
                    bool each = true; var listing = new List<string>();
                    for (int i = 0; i < placed.Count; i++)
                    {
                        float own = (placed[i].NoticeProgress(player) - p0[i + 1]) / dt;
                        float expect = GuardRules.NoticeRate(GuardSightFlat(placed[i], player), placed[i].spotBase, placed[i].spotPerMetre);
                        if (Mathf.Abs(own / expect - 1f) > 0.05f) each = false;
                        listing.Add($"{GuardSightFlat(placed[i], player):0.0} m {own:0.000}/s");
                    }
                    GuardSightCheck($"with {placed.Count} other officers looking at the same player from other distances ({string.Join(", ", listing)}) the test guard's meter fills just as fast as alone ({rateA:0.000}/s against {alone.rate:0.000}/s) and each of theirs follows his own distance",
                        Mathf.Abs(rateA / Mathf.Max(1e-6f, alone.rate) - 1f) < 0.04f && each);
                    foreach (var h in placed) { h.sightRange = 0.1f; h.ForgetNotices(); }
                }
                else GuardSightInfo($"only {placed.Count} other officers could be put into the line on the NavMesh: the group test is left out");
            }

            // ================= 8. at the longest sight he still notices the player, in the time the rules say, and starts his radio call (which is cut at once)
            {
                g.spotBase = GuardRules.SpotBase; g.spotPerMetre = GuardRules.SpotPerMetre;
                agentG.Warp(line.floor);
                g.transform.rotation = Quaternion.LookRotation(heading);
                foreach (var h in helpers) { h.sightRange = 0.1f; h.ForgetNotices(); }
                g.ForgetNotices();
                yield return new WaitForSeconds(0.3f);
                bool wasCalled = hm.BackupCalled;
                gpos = g.transform.position;
                float d = rangeFar;
                Put(On(d));
                float start = Time.time;
                yield return null;
                float expectAt = GuardRules.SpotSeconds(GuardSightFlat(g, player), g.spotBase, g.spotPerMetre);
                while (!g.IsCalling && g.State != GuardState.Calling && Time.time - start < expectAt + 3f) yield return null;
                float took = Time.time - start;
                bool started = g.IsCalling || g.State == GuardState.Calling;
                Put(gpos - heading * 4f);                                           // (out of his sight: the call is dropped, nobody comes)
                GuardSightCheck($"at the longest sight ({d:0.0} m) a player who stays in the clear is noticed after {took:0.00} s (the rules say {expectAt:0.00} s) and he starts his radio call",
                    started && took > expectAt - 0.15f && took < expectAt + 0.35f);
                yield return new WaitForSeconds(1.0f);
                GuardSightCheck("and when the player slips out of sight the call is dropped and no backup is called", !g.IsCalling && (wasCalled || !hm.BackupCalled), $"(state {g.State}, calling {g.IsCalling}, backup called {hm.BackupCalled})");
            }

            GuardVision.AlwaysShownForTests = false;
            Log($"[GuardSight] DONE fails={guardSightFails}");
            Quit(guardSightFails == 0 ? 0 : 1);
        }
    }
}
