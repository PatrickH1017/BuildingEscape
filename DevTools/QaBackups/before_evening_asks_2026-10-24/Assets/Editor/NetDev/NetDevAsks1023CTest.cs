using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch B: the replacement of an eliminated guard (T13) and the bodies the guards see (T11). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is <c>NetDevAsks1023C.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023CTest()
        {
            // ---- T13: 15 seconds after a guard is eliminated a new guard comes out of a stairwell
            Check("replacement (T13): a guard eliminated at time t is replaced at t + 15 s", GuardRules.ReplacementSeconds == 15f && Math.Abs(GuardRules.ReplacementDue(100f) - 115f) < 1e-5f);
            Check("replacement (T13): a replacement is wanted while fewer than 24 guards are alive, never at the cap", GuardRules.ReplacementWanted(0) && GuardRules.ReplacementWanted(23) && !GuardRules.ReplacementWanted(24) && !GuardRules.ReplacementWanted(40) && GuardRules.ReplacementWanted(5, 6) && !GuardRules.ReplacementWanted(6, 6));

            // ---- T11: guards see the bodies of eliminated guards and are alerted, once per body
            Check("bodies (T11): a guard looks for bodies five times a second, and a body lying on the floor is half a metre high", Math.Abs(GuardRules.BodyScanSeconds - 0.2f) < 1e-5f && Math.Abs(GuardRules.BodyHeightLying - 0.5f) < 1e-5f);
            Check("bodies (T11): a body raises the alarm when it is seen and has not raised it before, not otherwise", GuardRules.BodyAlarms(true, false) && !GuardRules.BodyAlarms(true, true) && !GuardRules.BodyAlarms(false, false) && !GuardRules.BodyAlarms(false, true));
            // the line of sight to a body lying behind a wall is blocked; in the open it is clear (the same SeesBody the players are seen by)
            bool open = GuardRules.SeesBody(0f, 1.6f, 0f, 6f, 0f, 0f, GuardRules.BodyHeightLying, (ax, ay, az, bx, by, bz) => true);
            bool walled = GuardRules.SeesBody(0f, 1.6f, 0f, 6f, 0f, 0f, GuardRules.BodyHeightLying, (ax, ay, az, bx, by, bz) => false);
            Check("bodies (T11): a body in the open is seen and one behind a wall is not", open && !walled);
        }
    }
}
