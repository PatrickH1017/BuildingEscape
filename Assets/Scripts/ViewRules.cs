using System;

namespace BuildingEscape
{
    /// <summary>
    /// First and third person (the owner, 2026-10-19: "mouse wheel scroll up to zoom in to first person and ... scroll down to zoom back out to third person or the player should be able to use the CTRL button plus the up arrow
    /// key for zoom in and CTRL plus the down arrow key for zoom out for those without a mouse"; 2026-10-19 night: "Clicking P should also switch back and forth between first and third person"). The view is a number from 0
    /// (third person, over the shoulder) to 1 (first person, at the eyes) that glides to where the input sent it. Pure rules, no Unity types.
    /// </summary>
    public static class ViewRules
    {
        public const float ThirdPerson = 0f, FirstPerson = 1f;
        /// <summary>The seconds a change of view takes.</summary>
        public const float Seconds = 0.25f;

        /// <summary>Where the view is heading after this frame's input: the wheel (up = in), Ctrl + Up (in), Ctrl + Down (out), P (the other one).</summary>
        public static float Target(float target, float wheel, bool ctrlUp, bool ctrlDown, bool toggle)
        {
            if (toggle) target = target > 0.5f ? ThirdPerson : FirstPerson;
            if (wheel > 0.01f || ctrlUp) target = FirstPerson;
            else if (wheel < -0.01f || ctrlDown) target = ThirdPerson;
            return target;
        }

        /// <summary>The view after <paramref name="dt"/> seconds of gliding to <paramref name="target"/>.</summary>
        public static float Step(float zoom, float target, float dt)
        {
            float step = dt / Seconds;
            return zoom < target ? Math.Min(target, zoom + step) : Math.Max(target, zoom - step);
        }

        /// <summary>The ease: 0 stays 0, 1 stays 1, slow at both ends.</summary>
        public static float Smooth(float z) { z = Math.Max(0f, Math.Min(1f, z)); return z * z * (3f - 2f * z); }

        /// <summary>Is it close enough to the eyes that the body is taken out of the picture?</summary>
        public static bool IsFirstPerson(float zoom) => zoom >= 0.85f;

        /// <summary>The camera's distance behind the head for a view (the third person's own distance at 0, none at 1).</summary>
        public static float Distance(float thirdPersonDistance, float zoom) => thirdPersonDistance * (1f - Smooth(zoom));
    }
}
