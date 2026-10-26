using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>Pure checks of <see cref="ViewClearRules"/> (the owner, 2026-10-24: nothing on the windows, nothing blocking the view outside glass offices). Called from <c>NetDevTest.PolicyTest</c>; the finished floors are checked by the scenario PropScan.</summary>
    public static partial class NetDevTest
    {
        public static void ViewClearTest()
        {
            // an outside window from x 2 to 6 reaching from 0.85 to 2.65 m
            Check("views: a clock, a poster or an extinguisher on a window opening hangs on it (above the sill, below the head, along the opening)",
                ViewClearRules.MountOnOpening(3.9f, 4.1f, 2.0f, 2.3f, 2f, 6f, 0.85f, 2.65f) && ViewClearRules.MountOnOpening(3f, 4f, 1.4f, 2.2f, 2f, 6f, 0.85f, 2.65f) && ViewClearRules.MountOnOpening(2.5f, 2.8f, 0.95f, 1.5f, 2f, 6f, 0.85f, 2.65f));
            Check("views: a prop above the head, below the sill, or beside the opening (on the pier or the wall) hangs on nothing",
                !ViewClearRules.MountOnOpening(3.9f, 4.1f, 2.8f, 3.1f, 2f, 6f, 0.85f, 2.65f) && !ViewClearRules.MountOnOpening(3.9f, 4.1f, 0.2f, 0.8f, 2f, 6f, 0.85f, 2.65f) && !ViewClearRules.MountOnOpening(6.1f, 6.5f, 1.5f, 2f, 2f, 6f, 0.85f, 2.65f)
                && !ViewClearRules.MountOnOpening(0.5f, 2.01f, 1.5f, 2f, 2f, 6f, 0.85f, 2.65f));
            Check("views: furniture higher than 0.6 m blocks the view through glass (a filing cabinet, a bookcase, a big plant, a thing that floats up there), a bench, a bin or a rug does not, nor does anything mounted",
                ViewClearRules.BlocksView(false, 0f, 1.3f) && ViewClearRules.BlocksView(false, 0f, 1.16f) && ViewClearRules.BlocksView(false, 0.5f, 0.3f) && !ViewClearRules.BlocksView(false, 0f, 0.45f) && !ViewClearRules.BlocksView(false, 0f, 0.6f) && !ViewClearRules.BlocksView(true, 1.5f, 1.2f));
            Check("views: the water cooler may stand outside a glass front (the owner's picture), a filing cabinet, a bookshelf and a big plant may not", ViewClearRules.MayStandBy("water") && !ViewClearRules.MayStandBy("file_cabinet") && !ViewClearRules.MayStandBy("bookshelf") && !ViewClearRules.MayStandBy("plant_big"));
            var s = ViewClearRules.Strip(true, 10f, 0.2f, 0f, 5f, 1f, 1.4f);
            var t = ViewClearRules.Strip(true, 10f, 0.2f, 0f, 5f, -1f, 1.4f);
            var u = ViewClearRules.Strip(false, 3f, 0.2f, 1f, 4f, -1f, 1.4f);
            Check("views: the strip outside a glass front starts at the wall's outer face and is 1.4 m deep along the whole wall, on the side that is outside (z up, z down, x down)",
                Near(s, 0f, 10.2f, 5f, 11.6f) && Near(t, 0f, 8.4f, 5f, 9.8f) && Near(u, 1.4f, 1f, 2.8f, 4f));
            Check("views: a mounted prop on the wall's centre line, or within 0.7 m of it, is on the wall; one 2 m away is not; the nearer edge counts",
                ViewClearRules.Distance(true, 10f, new Rc(3f, 9.9f, 4f, 10.1f)) == 0f && ViewClearRules.Distance(true, 10f, new Rc(3f, 10.3f, 4f, 10.5f)) < 0.31f && ViewClearRules.Distance(true, 10f, new Rc(3f, 12f, 4f, 12.2f)) > 1.9f);
            Check("views: MountOnWall puts it together: a clock 0.3 m from the wall's line inside the window hangs on it; the same clock 2 m out, or above the head, does not",
                ViewClearRules.MountOnWall(true, 10f, 2f, 6f, 0.85f, 2.65f, new Rc(3.9f, 10.2f, 4.1f, 10.3f), 2.0f, 0.3f) && !ViewClearRules.MountOnWall(true, 10f, 2f, 6f, 0.85f, 2.65f, new Rc(3.9f, 12f, 4.1f, 12.1f), 2.0f, 0.3f)
                && !ViewClearRules.MountOnWall(true, 10f, 2f, 6f, 0.85f, 2.65f, new Rc(3.9f, 10.2f, 4.1f, 10.3f), 2.8f, 0.3f));
        }

        static bool Near(Rc r, float x0, float z0, float x1, float z1) => Math.Abs(r.x0 - x0) < 1e-4f && Math.Abs(r.z0 - z0) < 1e-4f && Math.Abs(r.x1 - x1) < 1e-4f && Math.Abs(r.z1 - z1) < 1e-4f;
    }
}
