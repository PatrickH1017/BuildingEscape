using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>The pure half of the owner's restraint, drag and rescue clips (2026-10-28): <see cref="RestraintRules"/> and <see cref="StruggleMeter"/>, in PolicyTest.</summary>
    public static partial class NetDevTest
    {
        public static void RestraintRulesTest()
        {
            Check("restraint: a clip of n frames lasts (n - 1) / 30 s: the tie-up 2.5 s, the hogtied loop 2.97, a drag loop 1.17, the guard's drop 2.47, the free-up 5.97",
                Mathf.Abs(RestraintRules.Seconds(76) - 2.5f) < 1e-4f && Mathf.Abs(RestraintRules.Seconds(90) - 89f / 30f) < 1e-4f && Mathf.Abs(RestraintRules.Seconds(36) - 35f / 30f) < 1e-4f && Mathf.Abs(RestraintRules.Seconds(75) - 74f / 30f) < 1e-4f && Mathf.Abs(RestraintRules.Seconds(180) - 179f / 30f) < 1e-4f);
            Check("restraint: the tie-up ties the wrists at frame 31 (1.0 s), the ankles at 51 (1.67 s) and puts the link through both at 63 (2.07 s)",
                !RestraintRules.TieUpWrists(0.99f) && RestraintRules.TieUpWrists(1.0f) && !RestraintRules.TieUpAnkles(1.6f) && RestraintRules.TieUpAnkles(1.67f) && !RestraintRules.TieUpLink(2.0f) && RestraintRules.TieUpLink(2.07f));
            Check("restraint: the free-up takes the link off at frame 41 (1.33 s), the ankles' ties at 66 (2.17 s) and the wrists' at 91 (3.0 s)",
                RestraintRules.FreeUpLink(1.3f) && !RestraintRules.FreeUpLink(1.34f) && RestraintRules.FreeUpAnkles(2.1f) && !RestraintRules.FreeUpAnkles(2.17f) && RestraintRules.FreeUpWrists(2.9f) && !RestraintRules.FreeUpWrists(3.0f));
            Check("restraint: a dragger's walk clip is held when he stands still, plays as fast as the ground speed over 0.54 m/s (1.2 m/s: 2.2 times) and no slower than half or faster than 2.6 times",
                RestraintRules.DragRate(0f) == 0f && RestraintRules.DragRate(0.1f) == 0f && Mathf.Abs(RestraintRules.DragRate(0.54f) - 1f) < 1e-4f && Mathf.Abs(RestraintRules.DragRate(1.2f) - 1.2f / 0.54f) < 1e-4f && RestraintRules.DragRate(0.2f) == 0.5f && RestraintRules.DragRate(5f) == 2.6f);
            Check("restraint: a loop's time wraps round its length, also from a negative clock", Mathf.Abs(RestraintRules.LoopTime(2.5f, 1.0f) - 0.5f) < 1e-4f && Mathf.Abs(RestraintRules.LoopTime(-0.25f, 1.0f) - 0.75f) < 1e-4f && RestraintRules.LoopTime(3f, 0f) == 0f);
            Check("restraint: the officer drags at 1.2 m/s and at half of it (40-60 % slower: the owner's text) while the captive struggles",
                Mathf.Abs(RestraintRules.GuardSpeed(false) - 1.2f) < 1e-4f && Mathf.Abs(RestraintRules.GuardSpeed(true) - 0.6f) < 1e-4f && RestraintRules.StruggleSlow >= 0.4f && RestraintRules.StruggleSlow <= 0.6f);
            Check("restraint: the free-up plays 1.49 times as fast to take the four seconds a teammate holds the button", Mathf.Abs(RestraintRules.FreeRate - RestraintRules.Seconds(180) / 4f) < 1e-4f && RestraintRules.FreeSeconds == 4f);

            // ---- the struggle's stamina
            var m = new StruggleMeter();
            bool still = true;
            for (int i = 0; i < 34; i++) still &= m.Step(0.1f, true);
            Check("struggle: held for 3.4 s from full he still struggles (it empties in 3.5 s: the owner's 3-4 s)", still && !m.Exhausted && m.Stamina > 0f, $"(stamina {m.Stamina:0.00})");
            int steps = 34;
            while (!m.Exhausted && steps < 60) { m.Step(0.1f, true); steps++; }
            Check("struggle: at 3.5 s the stamina is gone: he cannot struggle any more, though the button is held", steps >= 35 && steps <= 36 && m.Stamina <= 0f && m.Exhausted && !m.Active && !m.Step(0.1f, true), $"(after {steps / 10f:0.0} s)");
            float back = 0f;
            while (m.Exhausted && back < 5f) { m.Step(0.1f, true); back += 0.1f; }
            Check("struggle: exhausted, he gets it back while he holds the button (about a second to 35 %, three seconds from empty to full) and can struggle again", back > 0.9f && back < 1.4f && !m.Exhausted && m.Step(0.1f, true), $"({back:0.0} s)");
            var f = new StruggleMeter();
            f.Step(2f, true);
            f.Step(1f, false);
            Check("struggle: not holding the button he recovers (a third a second) and the streak is broken", f.Streak == 0f && f.Stamina > 0.4f && f.Stamina < 0.8f, $"(stamina {f.Stamina:0.00}, streak {f.Streak:0.0})");
            var s = new StruggleMeter();
            for (int i = 0; i < 24; i++) s.Step(0.1f, true);
            bool before = s.ShoveDue;
            for (int i = 0; i < 2; i++) s.Step(0.1f, true);
            Check("struggle: the officer shoves a captive who has struggled 2.5 s without a break (not before), and the shove fills the stamina and ends the struggle", !before && s.ShoveDue, $"(streak {s.Streak:0.0})");
            s.Shoved();
            Check("struggle: after the shove the stamina is full and nobody is struggling", s.Stamina == 1f && !s.Active && s.Streak == 0f && !s.ShoveDue);

            // ---- the rescue's progress
            float fp = 0f;
            for (int i = 0; i < 39; i++) fp = RestraintRules.FreeStep(fp, 0.1f, true);
            bool notYet = fp < 1f && fp > 0.95f;
            for (int i = 0; i < 2; i++) fp = RestraintRules.FreeStep(fp, 0.1f, true);
            Check("rescue: holding the button for 3.9 s does not free him yet, 4.1 s does (progress 1, and no more than 1)", notYet && fp == 1f, $"(progress {fp:0.000})");
            float wb = 0.5f;
            for (int i = 0; i < 20; i++) wb = RestraintRules.FreeStep(wb, 0.1f, false);
            Check("rescue: let go, the progress winds back at the same pace (half of it in two seconds) and stops at 0", Mathf.Abs(wb) < 1e-4f && RestraintRules.FreeStep(0f, 0.5f, false) == 0f, $"(progress {wb:0.000})");
            Check("rescue: a negative step changes nothing; the free-up is shown from a hair above 0; the noises carry 20 m", RestraintRules.FreeStep(0.3f, -1f, true) == 0.3f && RestraintRules.FreeShownFrom > 0f && RestraintRules.FreeShownFrom < 0.02f && RestraintRules.HearRange == 20f);
            Check("drop: the drop (Guard_Shot_DropHead) lasts as long as its clip, 2.47 s, and the officer gives the captive up within 1.8 m of the outpost's middle", Mathf.Abs(RestraintRules.DropSeconds - 74f / 30f) < 1e-4f && RestraintRules.DragArrive == 1.8f);
        }
    }
}
