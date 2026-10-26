using System;

namespace BuildingEscape
{
    /// <summary>
    /// What may stand in front of a window or hang on it (the owner, 2026-10-24, with pictures of real glass offices: "make sure there aren't too many objects outside offices with glass walls, in the real world people with glass office walls usually leave them clear of view such as filing
    /// cabinets outside glass windows" and "make sure there aren't clocks and other items on the windows to the outside, that doesn't make sense"). Pure numbers, no Unity types (<see cref="Rc"/> is a plain struct):
    ///  - the strip of corridor outside a glass front (<see cref="ClearDepth"/> deep, the whole length of the wall) holds nothing that blocks the view (<see cref="BlocksView"/>: anything higher than <see cref="LowTop"/> - a bench, a bin and a rug may stay);
    ///  - nothing is mounted on a window opening (<see cref="MountOnOpening"/>): not on the outside windows (a clock, a picture, an extinguisher on the glass), not on a glass front of an office, from either side.
    /// <c>FloorGenerator.ClearViews</c> takes the pieces out of the layout (and what stands on them with them) after it knows where the windows and the glass are.
    /// </summary>
    public static class ViewClearRules
    {
        /// <summary>How deep the strip outside a glass front is (m).</summary>
        public const float ClearDepth = 1.4f;
        /// <summary>Furniture whose top is lower than this does not block the view through glass (m).</summary>
        public const float LowTop = 0.6f;
        /// <summary>How close to the wall's centre line a mounted prop has to be to count as hanging on it (m): half the wall's thickness and the prop's own.</summary>
        public const float OnWallReach = 0.7f;
        /// <summary>The reach of a furniture's hiding place (OfficePlanner.HideReach): a hiding zone is kept while any piece of furniture that can be hidden behind lies within it.</summary>
        public const float HideReach = 0.8f;

        /// <summary>Does a prop hang on a window opening? Its extent along the wall is [lo, hi], its underside is at <paramref name="y0"/> and its top at <paramref name="y1"/>; the opening spans [a, b] along the wall from <paramref name="sill"/> up to <paramref name="head"/>.</summary>
        public static bool MountOnOpening(float lo, float hi, float y0, float y1, float a, float b, float sill, float head)
            => hi > a + 0.02f && lo < b - 0.02f && y1 > sill + 0.02f && y0 < head - 0.02f;

        /// <summary>Does this piece of furniture block the view through a glass wall? Anything that is not mounted and stands higher than <see cref="LowTop"/>.</summary>
        public static bool BlocksView(bool mount, float y0, float height) => !mount && y0 + height > LowTop;

        /// <summary>What may stand in the strip outside a glass front all the same (the owner, 2026-10-24, with a picture of one by an interior window: "the water jug works perfectly there"): the water cooler.</summary>
        public static bool MayStandBy(string key) => key == "water";

        /// <summary>
        /// The strip outside a wall: along the wall from <paramref name="lo"/> to <paramref name="hi"/>, <paramref name="depth"/> deep from the wall's outer face (the wall's centre line is <paramref name="line"/>, its half thickness <paramref name="half"/>);
        /// <paramref name="outwardSign"/> is +1 when outside lies towards larger z (or x for a wall that runs along z), -1 the other way.
        /// </summary>
        public static Rc Strip(bool alongX, float line, float half, float lo, float hi, float outwardSign, float depth)
        {
            float t0 = outwardSign > 0f ? line + half : line - half - depth;
            float t1 = t0 + depth;
            return alongX ? new Rc(lo, t0, hi, t1) : new Rc(t0, lo, t1, hi);
        }

        /// <summary>How near a rectangle lies to a wall's centre line (the nearer of its two edges across the wall, 0 if the line runs through it).</summary>
        public static float Distance(bool alongX, float line, Rc rc)
        {
            float a = alongX ? rc.z0 : rc.x0, b = alongX ? rc.z1 : rc.x1;
            if (line >= a && line <= b) return 0f;
            return Math.Min(Math.Abs(a - line), Math.Abs(b - line));
        }

        /// <summary>Does a mounted prop (its footprint <paramref name="rc"/>, underside <paramref name="y0"/>, height <paramref name="h"/>) hang on this opening of a wall (a window of the outside wall or a glass front)?</summary>
        public static bool MountOnWall(bool alongX, float line, float a, float b, float sill, float head, Rc rc, float y0, float h)
        {
            if (Distance(alongX, line, rc) > OnWallReach) return false;
            float lo = alongX ? rc.x0 : rc.z0, hi = alongX ? rc.x1 : rc.z1;
            return MountOnOpening(lo, hi, y0, y0 + h, a, b, sill, head);
        }
    }
}
