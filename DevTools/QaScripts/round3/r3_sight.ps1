# R14: the owner: "Officers should also see much further down a hallway if it is a straight line of sight".
# Down the line an officer faces (a narrow beam of +-9 degrees) his sight grows from 14 m to 44 m; the cone on the floor shows it. Walls still stop it (the ray decides).
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

# ---- the rule (pure)
Open-Src "$Tree\Assets\Scripts\GuardRules.cs"
if (-not $s.Contains('LongSightRange')) {
Rep @'
        /// <summary>Somebody this close (flat, in metres) counts as in view whichever way the guard faces: he is on top of him.</summary>
        public const float AlwaysSeenWithin = 0.4f;
'@ @'
        /// <summary>Somebody this close (flat, in metres) counts as in view whichever way the guard faces: he is on top of him.</summary>
        public const float AlwaysSeenWithin = 0.4f;

        /// <summary>
        /// Down a straight line of sight an officer sees much further (the owner, 2026-10-17: "Officers should also see much further down a hallway if it is a straight line of sight"):
        /// within <see cref="LongSightHalfAngle"/> degrees of the way he faces his sight grows from his ordinary range to <see cref="LongSightRange"/> metres, most of all dead ahead.
        /// A wall still stops it (his ray decides that), so it is only ever a long, clear hallway that he sees down.
        /// </summary>
        public const float LongSightRange = 44f, LongSightHalfAngle = 9f;

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
'@
Save-Src
}

# ---- the officer
Open-Src "$Tree\Assets\Scripts\GuardAI.cs"
if (-not $s.Contains('longSightRange')) {
Rep @'
        public float sightRange = 14f, sightAngle = 65f, hiddenSightRange = 1.5f;
'@ @'
        public float sightRange = 14f, sightAngle = 65f, hiddenSightRange = 1.5f;
        [Tooltip("Down a straight line of sight (a hallway) he sees this far, in a narrow beam of 'longSightAngle' degrees each side of the way he faces (GuardRules.RangeAt).")]
        public float longSightRange = GuardRules.LongSightRange, longSightAngle = GuardRules.LongSightHalfAngle;
'@
Rep @'
            float range = p.IsHidden ? hiddenSightRange : sightRange;
            // the cone is the fan he shows on the floor (GuardVision): flat, so standing close to him does not hide you from him
            var flat = p.transform.position - transform.position;
            var fwd = transform.forward;
            if (!GuardRules.InCone(fwd.x, fwd.z, flat.x, flat.z, sightAngle, range)) return false;
            // trigger volumes (hide spots, the elevator zone...) must not block the view, or standing in one would make you invisible
            if (Physics.Raycast(eye, to.normalized, out var hit, range, sightBlockers, QueryTriggerInteraction.Ignore))
'@ @'
            float range = p.IsHidden ? hiddenSightRange : sightRange;
            float far = p.IsHidden ? range : Mathf.Max(range, longSightRange);       // (down a straight line of sight he sees much further)
            // the cone is the fan he shows on the floor (GuardVision): flat, so standing close to him does not hide you from him
            var flat = p.transform.position - transform.position;
            var fwd = transform.forward;
            if (!GuardRules.InVision(fwd.x, fwd.z, flat.x, flat.z, sightAngle, range, far, longSightAngle)) return false;
            // trigger volumes (hide spots, the elevator zone...) must not block the view, or standing in one would make you invisible
            if (Physics.Raycast(eye, to.normalized, out var hit, far, sightBlockers, QueryTriggerInteraction.Ignore))
'@
Save-Src
}

# ---- the cone on the floor
Open-Src "$Tree\Assets\Scripts\GuardVision.cs"
if (-not $s.Contains('longSightAngle')) {
Rep '        const int Segments = 36;' '        const int Segments = 48;           // (rays are packed densely straight ahead, where the long beam is)'
Rep '        readonly RaycastHit[] hits = new RaycastHit[24];' '        readonly RaycastHit[] hits = new RaycastHit[64];      // (a long hallway has many things in it: the nearest wall must not fall off the end of the list)'
Rep @'
            float range = ai.sightRange, half = ai.sightAngle;
'@ @'
            float range = ai.sightRange, half = ai.sightAngle;
            float far = Mathf.Max(range, ai.longSightRange), longHalf = ai.longSightAngle;
'@
Rep @'
                float ang = Mathf.Lerp(-half, half, i / (float)n);
                var dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                float d = ClipToGate(origin, dir, Clip(rayOrigin, dir, range));
'@ @'
                float u = i / (float)n * 2f - 1f;                                    // -1 .. 1
                float ang = half * Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), 1.7f);     // (the rays crowd together straight ahead, where the long beam is)
                float reach = GuardRules.RangeAt(ang, range, far, longHalf);
                var dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                float d = ClipToGate(origin, dir, Clip(rayOrigin, dir, reach));
'@
Save-Src
}

# ---- the pure tests
$testFile = "$Tree\Assets\Editor\NetDev\NetDevR3Test.cs"
if (-not (Test-Path $testFile)) {
$test = @'
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>The rules of round 3 (DEVELOPER_NOTES 2.23) that can be checked without playing.</summary>
    public static partial class NetDevTest
    {
        public static void R3RulesTest()
        {
            SightTest();
        }

        /// <summary>R14: officers see much further down a straight line of sight.</summary>
        static void SightTest()
        {
            const float range = 14f, angle = 65f, far = GuardRules.LongSightRange, longHalf = GuardRules.LongSightHalfAngle;
            Check("the long sight is much further than the ordinary one", far >= range * 2.5f);
            Check("dead ahead the sight is the long range", Mathf.Approximately(GuardRules.RangeAt(0f, range, far, longHalf), far));
            Check("at the edge of the long beam and beyond it is the ordinary range", Mathf.Approximately(GuardRules.RangeAt(longHalf, range, far, longHalf), range) && Mathf.Approximately(GuardRules.RangeAt(40f, range, far, longHalf), range));
            bool monotonic = true, symmetric = true;
            float last = float.MaxValue;
            for (float d = 0f; d <= 12f; d += 0.5f)
            {
                float r = GuardRules.RangeAt(d, range, far, longHalf);
                if (r > last + 1e-4f) monotonic = false;
                if (!Mathf.Approximately(r, GuardRules.RangeAt(-d, range, far, longHalf))) symmetric = false;
                last = r;
            }
            Check("the sight falls smoothly from the middle of the beam to its edge, the same on both sides", monotonic && symmetric);
            Check("no long beam when the long range is not longer (a hiding player is seen from close by only)", Mathf.Approximately(GuardRules.RangeAt(0f, 1.5f, 1.5f, longHalf), 1.5f));

            // a hallway: the officer faces +z
            bool Sees(float dist, float degrees) => GuardRules.InVision(0f, 1f, Mathf.Sin(degrees * Mathf.Deg2Rad) * dist, Mathf.Cos(degrees * Mathf.Deg2Rad) * dist, angle, range, far, longHalf);
            Check("a player 40 m down the hall, dead ahead, is seen", Sees(40f, 0f));
            Check("...but 46 m away is not", !Sees(46f, 0f));
            Check("...and 40 m away but 5 degrees off is not (the beam narrows with the distance)", !Sees(40f, 5f));
            Check("20 m away and 5 degrees off is seen", Sees(20f, 5f));
            Check("20 m away and 15 degrees off is not seen (only the ordinary 14 m reach there)", !Sees(20f, 15f));
            Check("12 m away and 60 degrees off is seen (the ordinary cone is as it was)", Sees(12f, 60f));
            Check("15 m away and 60 degrees off is not seen", !Sees(15f, 60f));
            Check("behind him is not seen", !Sees(5f, 120f));
            Check("somebody on top of him is seen whatever way he faces", GuardRules.InVision(0f, 1f, 0f, -0.3f, angle, range, far, longHalf));
            Check("InVision with no long beam is InCone", GuardRules.InVision(0f, 1f, 3f, 9f, angle, range, range, longHalf) == GuardRules.InCone(0f, 1f, 3f, 9f, angle, range)
                                                         && GuardRules.InVision(0f, 1f, 9f, 14f, angle, range, range, longHalf) == GuardRules.InCone(0f, 1f, 9f, 14f, angle, range));
        }
    }
}
'@
[IO.File]::WriteAllText($testFile, $test.Replace("`r`n", "`n").Replace("`n", "`r`n"), (New-Object Text.UTF8Encoding($false)))
"wrote NetDevR3Test.cs"
}

# ---- hook the test into PolicyTest (shared file: an exact match)
Open-Src "$Tree\Assets\Editor\NetDev\NetDevTest.cs"
if (-not $s.Contains('R3RulesTest();')) {
  Rep '            KeybindTest();
            string v = NetConst.BuildVersion;' '            KeybindTest();
            R3RulesTest();
            string v = NetConst.BuildVersion;'
  Save-Src
}
