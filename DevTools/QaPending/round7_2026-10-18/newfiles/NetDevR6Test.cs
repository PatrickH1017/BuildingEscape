using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// Round 6 (the owner's task list of 2026-10-18): the pure rules of the items that have one. Armed officers get faster the higher the floor; (later items add their checks here).
        /// </summary>
        public static void Round6Test()
        {
            // ---- armed officers: faster to attack as the floors go up
            bool monotonic = true, bounded = true;
            float la = 9f, ls = 9f, lr = 9f;
            for (int floor = 1; floor <= 14; floor++)
            {
                GunRules.GuardTempo(floor, 4, out float a, out float s, out float r);
                if (a > la + 1e-4f || s > ls + 1e-4f || r > lr + 1e-4f) monotonic = false;
                la = a; ls = s; lr = r;
                bounded &= a >= 0.29f && a <= 0.81f && s >= 0.54f && s <= 0.91f && r >= 0.69f && r <= 0.96f;
            }
            GunRules.GuardTempo(4, 4, out float a4, out float s4, out float r4);
            GunRules.GuardTempo(12, 4, out float a12, out float s12, out float r12);
            GunRules.GuardTempo(13, 4, out float a13, out float s13, out float r13);
            Check("armed officers: the higher the floor, the shorter the aim, the pause between shots and the reload (never longer)", monotonic && bounded);
            Check("...the first armed floor is already a little faster than before (all multipliers under 1) and floor 12 is the fastest", a4 < 1f && s4 < 1f && r4 < 1f && a12 < a4 && s12 < s4 && r12 < r4 && Mathf.Approximately(a12, 0.3f) && Mathf.Approximately(s12, 0.55f));
            Check("...above the top floor nothing gets faster still", Mathf.Approximately(a13, a12) && Mathf.Approximately(s13, s12) && Mathf.Approximately(r13, r12));
            GunRules.GuardTempo(8, ArmedRules.Never, out float an, out _, out _);
            Check("...with no armed floor (the test menu's never) the first-floor values apply", Mathf.Approximately(an, 0.8f));
        }
    }
}
