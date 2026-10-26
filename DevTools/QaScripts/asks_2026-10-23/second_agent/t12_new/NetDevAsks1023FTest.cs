using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch D, T12: dragging the body of an eliminated guard (<c>DraggableBodyRules</c>). Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is <c>NetDevAsks1023F.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023FTest()
        {
            Check("drag (T12): a body is taken hold of from 2.2 m, not from further away and not when somebody drags it already",
                DraggableBodyRules.CanGrab(0f, false) && DraggableBodyRules.CanGrab(2.2f, false) && !DraggableBodyRules.CanGrab(2.21f, false) && !DraggableBodyRules.CanGrab(1f, true));
            Check("drag (T12): the player lets go when he is further than 4.5 m from the body", !DraggableBodyRules.MustLetGo(4.5f) && DraggableBodyRules.MustLetGo(4.51f));
            DraggableBodyRules.HoldSpot(10f, 20f, 0f, 1f, out float hx, out float hz);
            DraggableBodyRules.HoldSpot(0f, 0f, 3f, 4f, out float ax, out float az);
            DraggableBodyRules.HoldSpot(5f, 5f, 0f, 0f, out float nx, out float nz);
            Check("drag (T12): the body is held 1.2 m behind the player, whatever way he faces (a player who faces nothing faces north)",
                Math.Abs(hx - 10f) < 1e-4f && Math.Abs(hz - (20f - DraggableBodyRules.FollowDistance)) < 1e-4f
                && Math.Abs(ax - (-0.6f * DraggableBodyRules.FollowDistance)) < 1e-4f && Math.Abs(az - (-0.8f * DraggableBodyRules.FollowDistance)) < 1e-4f
                && Math.Abs(nx - 5f) < 1e-4f && Math.Abs(nz - (5f - DraggableBodyRules.FollowDistance)) < 1e-4f);
            DraggableBodyRules.Step(0f, 0f, 10f, 0f, 0.1f, out float sx, out float sz);
            DraggableBodyRules.Step(0f, 0f, 0.2f, 0f, 0.1f, out float ex, out float ez);
            DraggableBodyRules.Step(0f, 0f, 3f, 4f, -1f, out float bx, out float bz);
            Check("drag (T12): the body is pulled along at most 4.5 m/s, never goes past the spot it is pulled to and does not move in no time",
                Math.Abs(sx - DraggableBodyRules.FollowSpeed * 0.1f) < 1e-4f && sz == 0f && ex == 0.2f && ez == 0f && bx == 0f && bz == 0f);
            Check("drag (T12): a player who drags a body keeps 60 % of his walking speed", Math.Abs(DraggableBodyRules.DraggerSpeedShare - 0.6f) < 1e-5f);
        }
    }
}
