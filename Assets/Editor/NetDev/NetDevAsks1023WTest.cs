using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch C part 3: looking round at the security computer, and E at the microphone (T16). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is
    /// <c>NetDevAsks1023W.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023WTest()
        {
            // a monitor of 1160 x 858 pixels at (380, 90) on a screen of 1920 x 1080 (y down), the way the computer draws it
            const float mx = 380f, my = 90f, mw = 1160f, mh = 858f, sw = 1920f, sh = 1080f;
            DeskLookRules.Target(960f, 520f, mx, my, mw, mh, sw, sh, out float yIn, out float pIn);
            Check("desk look (T16): with the cursor over the monitor the view does not turn, not even on its edge", yIn == 0f && pIn == 0f && !DeskLookRules.Outside(960f, 520f, mx, my, mw, mh) && !DeskLookRules.Outside(mx, my, mx, my, mw, mh) && DeskLookRules.Outside(mx - 1f, 500f, mx, my, mw, mh));
            DeskLookRules.Target(sw, 520f, mx, my, mw, mh, sw, sh, out float yR, out float pR);
            DeskLookRules.Target(0f, 520f, mx, my, mw, mh, sw, sh, out float yL, out float pL);
            Check("desk look (T16): with the cursor at the screen's right edge the view turns the whole 35 degrees to the right, at the left edge 35 to the left, and neither goes up or down", Math.Abs(yR - 35f) < 1e-3f && Math.Abs(yL + 35f) < 1e-3f && pR == 0f && pL == 0f);
            DeskLookRules.Target(960f, 0f, mx, my, mw, mh, sw, sh, out float yU, out float pU);
            DeskLookRules.Target(960f, sh, mx, my, mw, mh, sw, sh, out float yD, out float pD);
            Check("desk look (T16): with the cursor at the top of the screen the view tilts 20 degrees up (negative), at the bottom 20 degrees down", yU == 0f && yD == 0f && Math.Abs(pU + 20f) < 1e-3f && Math.Abs(pD - 20f) < 1e-3f);
            DeskLookRules.Target(1730f, 520f, mx, my, mw, mh, sw, sh, out float yHalf, out _);
            Check("desk look (T16): halfway between the monitor's edge and the screen's edge the view has turned half the range (17.5 degrees)", Math.Abs(yHalf - 17.5f) < 0.01f);
            bool limited = true;
            for (float cx = -200f; cx <= sw + 200f; cx += 111f)
                for (float cy = -200f; cy <= sh + 200f; cy += 97f)
                {
                    DeskLookRules.Target(cx, cy, mx, my, mw, mh, sw, sh, out float ty, out float tp);
                    limited &= Math.Abs(ty) <= DeskLookRules.MaxYaw + 1e-3f && Math.Abs(tp) <= DeskLookRules.MaxPitch + 1e-3f;
                }
            Check("desk look (T16): wherever the cursor is, on the screen or beyond it, the look stays within 35 degrees sideways and 20 up or down (the limited range)", limited && DeskLookRules.MaxYaw == 35f && DeskLookRules.MaxPitch == 20f);

            // the ease: a lot of deceleration (it moves a share of the gap each step, so it starts quickly and settles slowly, and the steps add up the same however small they are)
            float v = 0f;
            for (int i = 0; i < 9; i++) v = DeskLookRules.Ease(v, 35f, 0.05f);
            float oneStep = DeskLookRules.Ease(0f, 35f, 0.45f);
            Check("desk look (T16): after 0.45 s (the time constant) the view is two thirds of the way, however the time is cut into steps", Math.Abs(v - oneStep) < 0.01f && Math.Abs(oneStep - 35f * (1f - (float)Math.Exp(-1f))) < 0.01f);
            Check("desk look (T16): it settles slowly: 0.1 s gives a fifth of the way, 1 s nine tenths, 3 s all but a hair; and it eases back the same way", DeskLookRules.Ease(0f, 35f, 0.1f) < 0.21f * 35f && DeskLookRules.Ease(0f, 35f, 1f) < 0.9f * 35f && DeskLookRules.Ease(0f, 35f, 3f) > 0.99f * 35f && DeskLookRules.Ease(35f, 0f, 0.45f) < 35f * 0.37f && DeskLookRules.Ease(5f, 5f, 1f) == 5f && DeskLookRules.Ease(1f, 9f, -1f) == 1f);

            // E at the microphone
            Check("desk look (T16): the microphone counts as looked at when the middle of the view is within 30 degrees of its button and it is within 5 m", DeskLookRules.AtMicrophone(5f, 3f) && DeskLookRules.AtMicrophone(30f, 5f) && !DeskLookRules.AtMicrophone(40f, 3f) && !DeskLookRules.AtMicrophone(5f, 6f));
        }
    }
}
