using System;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner, 2026-10-22: "add them both to the main room of the loading dock and add the smaller black one to the break room ... In the main room I want them on the wall across from the vendor". The pure rules of where they stand
        /// (DockVendingRules, with the real sizes from the prop table), run on their own: the pair on the south wall across from the vendor, side by side and centred on him, the third in the break room's corner clear of the doorway
        /// and of the cast of the run-start cutscene, and the room each keeps free. The Play-mode half, with the real props, is the scenario <c>Vending</c> (NetDevVending.cs).
        /// </summary>
        static void DockVendingTest()
        {
            const float Eps = 1e-3f;
            // the docks as FloorGenerator builds them: cells of 18 m, two of them (x 0..36); the outer walls are 0.4 m thick, so their inner faces are 0.2 m in; the vendor's trailer is backed into the open bay of the north wall at the east cell's
            // middle (27 m) + 5.8 m; the wall between the two halves stands at x = 18; the break room (x 0.2..9.3, z 0.2..4.8) has a doorway 1.6 m wide centred on z 2.4 in its east wall, and the workers watch the TV on its south wall standing 2.0 m out
            const float WallInner = 0.2f, VendorX = 27f + 5.8f, EastHallFrom = 18.2f, EastWallInner = 35.8f;
            const float BreakEastInner = 9.3f, DoorLow = 2.4f - 0.8f, WorkersZ = 0.2f + 2.0f;

            var black = PropTable.Get("vending");
            var red = PropTable.Get("vending_red");
            Check($"dock vending: the prop table has the black machine ({black.W:0.00} wide, {black.D:0.00} deep) and the red one ({red.W:0.00} wide, {red.D:0.00} deep, front turned {red.FrontYaw:0} degrees to face the room)",
                Math.Abs(black.W - 1.2f) < Eps && Math.Abs(black.D - 0.8f) < Eps && Math.Abs(red.W - 1.51f) < Eps && Math.Abs(red.D - 1.28f) < Eps && red.FrontYaw == -90f && red.H > 2f && red.solid && black.solid);

            DockVendingRules.MainRoom(VendorX, WallInner, black.W, black.D, red.W, red.D, out var b, out var r);
            b.Front(out int bfx, out int bfz); r.Front(out int rfx, out int rfz);
            Check($"dock vending: both machines of the main room stand against the wall across from the vendor (the south wall: their backs at z {b.ZMin:0.00} and {r.ZMin:0.00}, the wall's face at {WallInner:0.00}, never more than 5 cm off it) and face the room (north)",
                DockVendingRules.WallGap >= 0f && DockVendingRules.WallGap <= 0.05f && Math.Abs(b.ZMin - (WallInner + DockVendingRules.WallGap)) < Eps && Math.Abs(r.ZMin - (WallInner + DockVendingRules.WallGap)) < Eps && bfx == 0 && bfz == 1 && rfx == 0 && rfz == 1 && b.yaw == 0f && r.yaw == 0f);
            Check($"dock vending: they stand side by side, {r.XMin - b.XMax:0.00} m apart, with nothing overlapping, the black one on the west and the red one on the east",
                Math.Abs((r.XMin - b.XMax) - DockVendingRules.SideGap) < Eps && !DockVendingRules.Overlap(b, r) && b.x < r.x);
            Check($"dock vending: the pair is centred on the vendor (x {VendorX:0.0}): it runs from {b.XMin:0.00} to {r.XMax:0.00}, its middle at {(b.XMin + r.XMax) / 2f:0.00}",
                Math.Abs((b.XMin + r.XMax) / 2f - VendorX) < Eps && b.XMin < VendorX && VendorX < r.XMax);
            Check($"dock vending: both are inside the east half of the docks, clear of the wall between the halves and of the east wall ({b.XMin:0.00} to {r.XMax:0.00}, the room runs {EastHallFrom:0.0} to {EastWallInner:0.0})",
                b.XMin > EastHallFrom && r.XMax < EastWallInner);

            var k = DockVendingRules.BreakRoom(BreakEastInner, WallInner, black.W, black.D);
            k.Front(out int kfx, out int kfz);
            Check($"dock vending: the break room's machine stands against its east wall (its back at x {k.XMax:0.00}, the wall's face at {BreakEastInner:0.0}) in the corner by the south wall, and faces west into the room",
                Math.Abs(k.XMax - (BreakEastInner - DockVendingRules.WallGap)) < Eps && Math.Abs(k.ZMin - (WallInner + DockVendingRules.CornerGap)) < Eps && kfx == -1 && kfz == 0 && k.Turned);
            Check($"dock vending: it leaves the doorway free (its far side at z {k.ZMax:0.00}, the doorway starts at {DoorLow:0.00}) and the workers' row (z {WorkersZ:0.0})",
                k.ZMax <= DoorLow - 0.1f + Eps && k.ZMax < WorkersZ && k.ZMin >= WallInner);

            // the room each keeps free of storage (the owner, 2026-10-22: "remove the boxes and red and blue barrel in front of the vending machines"): its footprint, the side room at each side, the back room behind it and the front room in front of its front face
            DockVendingRules.KeepClear(r, out float x0, out float z0, out float x1, out float z1);
            Check($"dock vending: the red machine keeps {DockVendingRules.FrontRoom:0.0} m clear in front of its front face, {DockVendingRules.SideRoom:0.0} m at each side and {DockVendingRules.BackRoom:0.0} m behind it (x {x0:0.00} to {x1:0.00}, z {z0:0.00} to {z1:0.00})",
                DockVendingRules.BackRoom >= 0.1f && Math.Abs(z1 - (r.ZMax + DockVendingRules.FrontRoom)) < Eps && Math.Abs(z0 - (r.ZMin - DockVendingRules.BackRoom)) < Eps && Math.Abs(x0 - (r.XMin - DockVendingRules.SideRoom)) < Eps && Math.Abs(x1 - (r.XMax + DockVendingRules.SideRoom)) < Eps);
            DockVendingRules.KeepClear(b, out float bx0, out float bz0, out float bx1, out float bz1);
            Check($"dock vending: the black machine's room is the same shape (x {bx0:0.00} to {bx1:0.00}, z {bz0:0.00} to {bz1:0.00}) and the two rooms together hold the pair with at least 0.5 m to spare at each end and the floor 4 m in front of the wall",
                Math.Abs(bz1 - (b.ZMax + DockVendingRules.FrontRoom)) < Eps && Math.Abs(bx0 - (b.XMin - DockVendingRules.SideRoom)) < Eps && Math.Abs(bx1 - (b.XMax + DockVendingRules.SideRoom)) < Eps &&
                Math.Min(bx0, x0) <= b.XMin - 0.5f && Math.Max(bx1, x1) >= r.XMax + 0.5f && Math.Min(bz1, z1) >= WallInner + 4f && DockVendingRules.FrontRoom >= 4f && DockVendingRules.SideRoom >= 0.5f);
            DockVendingRules.KeepClear(k, out x0, out z0, out x1, out z1);
            Check($"dock vending: the break room's machine keeps its front (the west side) clear: x from {x0:0.00} ({k.XMin - DockVendingRules.FrontRoom:0.00} expected), the sides north and south ({z0:0.00} to {z1:0.00}) and the back (x to {x1:0.00})",
                Math.Abs(x0 - (k.XMin - DockVendingRules.FrontRoom)) < Eps && Math.Abs(x1 - (k.XMax + DockVendingRules.BackRoom)) < Eps && Math.Abs(z0 - (k.ZMin - DockVendingRules.SideRoom)) < Eps && Math.Abs(z1 - (k.ZMax + DockVendingRules.SideRoom)) < Eps);
            var faceEast = k; faceEast.yaw = 90f;
            DockVendingRules.KeepClear(faceEast, out x0, out z0, out x1, out z1);
            var faceSouth = r; faceSouth.yaw = 180f;
            DockVendingRules.KeepClear(faceSouth, out float sx0, out float sz0, out float sx1, out float sz1);
            Check("dock vending: the room follows the way the machine faces (east: the front room is on its east side; south: on its south side)",
                Math.Abs(x1 - (faceEast.XMax + DockVendingRules.FrontRoom)) < Eps && Math.Abs(x0 - (faceEast.XMin - DockVendingRules.BackRoom)) < Eps &&
                Math.Abs(sz0 - (faceSouth.ZMin - DockVendingRules.FrontRoom)) < Eps && Math.Abs(sz1 - (faceSouth.ZMax + DockVendingRules.BackRoom)) < Eps && Math.Abs(sx0 - (faceSouth.XMin - DockVendingRules.SideRoom)) < Eps && Math.Abs(sx1 - (faceSouth.XMax + DockVendingRules.SideRoom)) < Eps);

            // other sizes and places: the rules do not depend on these particular machines
            bool general = true; string why = "";
            foreach (float vx in new[] { 20f, 27f, 32.8f, 34f })
                foreach (var (bw, bd, rw, rd) in new[] { (1.2f, 0.8f, 1.51f, 1.28f), (0.9f, 0.7f, 1.0f, 0.9f), (2.0f, 1.0f, 0.6f, 0.5f) })
                {
                    DockVendingRules.MainRoom(vx, WallInner, bw, bd, rw, rd, out var p, out var q);
                    bool ok = Math.Abs((p.XMin + q.XMax) / 2f - vx) < Eps && !DockVendingRules.Overlap(p, q) && Math.Abs(p.ZMin - q.ZMin) < Eps && Math.Abs(p.ZMin - (WallInner + DockVendingRules.WallGap)) < Eps &&
                              Math.Abs(p.XMax + DockVendingRules.SideGap - q.XMin) < Eps;
                    if (!ok) { general = false; why = $"vendor {vx}, sizes {bw}x{bd} and {rw}x{rd}"; }
                }
            Check("dock vending: whatever their sizes and wherever the vendor stands, the pair is centred on him, on the wall, apart by the gap and not overlapping" + (why.Length > 0 ? " (first fails: " + why + ")" : ""), general);

            // and the overlap test itself sees an overlap and lets touching footprints be
            var moved = r; moved.x = b.x + 0.5f;
            var touching = r; touching.x = b.XMax + red.W * 0.5f;
            Check("dock vending: the overlap test finds two machines pushed into each other and lets two that only touch be", DockVendingRules.Overlap(b, moved) && !DockVendingRules.Overlap(b, touching));
        }
    }
}
