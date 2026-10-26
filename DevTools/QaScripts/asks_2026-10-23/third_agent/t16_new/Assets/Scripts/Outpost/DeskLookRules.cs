using System;

namespace BuildingEscape
{
    /// <summary>
    /// Looking round at the security computer (the owner, 2026-10-23: "When using the computer make it so if the players cursor is over the monitor they stay viewing it but if their cursor goes outside the monitor screen they look around with a lot of
    /// deceleration on the movement and limit range for looking around (when looking at the microphone the player should be able to press E to speak into it"). Pure numbers, no Unity types: with the cursor over the monitor the view stays where it is;
    /// with the cursor outside it the view turns towards the cursor - the further out, the more, never past <see cref="MaxYaw"/> degrees sideways or <see cref="MaxPitch"/> up or down - and eases there slowly (<see cref="EaseSeconds"/>);
    /// back over the monitor it eases back. With the view on the microphone and the cursor outside the monitor, E speaks into it, as V does (<see cref="AtMicrophone"/>).
    /// </summary>
    public static class DeskLookRules
    {
        /// <summary>The most the view turns sideways, and up or down, from where the player sat down (degrees): the limited range of the look.</summary>
        public const float MaxYaw = 35f, MaxPitch = 20f;

        /// <summary>The time constant of the ease (seconds): after this long the view has made two thirds of the way, after three times it about all of it. Long: "a lot of deceleration".</summary>
        public const float EaseSeconds = 0.45f;

        /// <summary>
        /// How close to the microphone's button the middle of the view has to be (degrees), and how near the microphone (m: the camera hangs behind the player, so the desk is a few metres off). 30 degrees: the camera orbits the player, so with the view turned the whole
        /// way (<see cref="MaxYaw"/>) the microphone, which stands to the side of the monitor, is still about 26 degrees off the middle (measured, 2026-10-23: best 26.5 over a grid of cursor places); it is in the picture and the view is turned towards it.
        /// </summary>
        public const float MicAngle = 30f, MicRange = 5f;

        /// <summary>Is the cursor (screen pixels, y down) outside the monitor's rectangle (x, y, width, height)?</summary>
        public static bool Outside(float cx, float cy, float mx, float my, float mw, float mh) => cx < mx || cx > mx + mw || cy < my || cy > my + mh;

        /// <summary>
        /// The turn the view wants (degrees) for a cursor at (cx, cy) with the monitor at (mx, my, mw, mh) on a screen of sw x sh pixels, y down: <paramref name="yaw"/> positive to the right, <paramref name="pitch"/> positive downwards (the camera's own
        /// signs). Zero while the cursor is over the monitor; outside it grows with how far the cursor is on its way from the monitor's edge to the screen's edge.
        /// </summary>
        public static void Target(float cx, float cy, float mx, float my, float mw, float mh, float sw, float sh, out float yaw, out float pitch)
        {
            yaw = pitch = 0f;
            if (sw <= 1f || sh <= 1f) return;
            yaw = Reach(cx, mx, mx + mw, sw) * MaxYaw;
            pitch = Reach(cy, my, my + mh, sh) * MaxPitch;
        }

        /// <summary>How far out of the monitor's span [lo, hi] the cursor is on its way to the screen's edge: 0 inside, -1 at the near edge of the screen, +1 at the far edge.</summary>
        static float Reach(float c, float lo, float hi, float edge)
        {
            if (c > hi) return Math.Min(1f, (c - hi) / Math.Max(1f, edge - hi));
            if (c < lo) return -Math.Min(1f, (lo - c) / Math.Max(1f, lo));
            return 0f;
        }

        /// <summary>One step of the slow ease: the value moves a share of the gap to the target each step (exponential: it starts quickly and settles slowly).</summary>
        public static float Ease(float current, float target, float dt) => current + (target - current) * (1f - (float)Math.Exp(-Math.Max(0f, dt) / EaseSeconds));

        /// <summary>Is the view on the microphone: the angle between the way the camera looks and the way to its button (degrees) is small, and it is near?</summary>
        public static bool AtMicrophone(float angleDegrees, float distance) => angleDegrees <= MicAngle && distance <= MicRange;
    }
}
