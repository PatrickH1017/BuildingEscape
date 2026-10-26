using System;

namespace BuildingEscape
{
    /// <summary>
    /// Dragging the body of an eliminated guard (the owner, 2026-10-23: "Eliminated guards and players bodies should be draggable (in order to hide them)"): how near a player must be to take hold of a body, where the body is held (behind the
    /// player), how fast it is pulled along, how much the player is slowed by the weight and when he lets go. Pure numbers, no Unity types, so the rules are tested outside the editor; <see cref="DraggableBody"/> and <see cref="PlayerGrab"/> apply them.
    /// </summary>
    public static class DraggableBodyRules
    {
        /// <summary>How near (flat, metres) a player must be to a body to take hold of it with the grab key (Q), as near as for any item.</summary>
        public const float GrabReach = 2.2f;

        /// <summary>How far behind the player the body is held (m), and the fastest it is pulled along (m/s: a little under a walking player's speed, so it trails and never overtakes him).</summary>
        public const float FollowDistance = 1.2f, FollowSpeed = 4.5f;

        /// <summary>The share of his walking speed a player keeps while he drags a body (a body is heavy).</summary>
        public const float DraggerSpeedShare = 0.6f;

        /// <summary>Further than this from the body (flat, m) and the player lets go of it (he was teleported, he was pinned, the floor changed).</summary>
        public const float LetGoDistance = 4.5f;

        /// <summary>Can a player at this flat distance take hold of a body that nobody drags yet?</summary>
        public static bool CanGrab(float flatDistance, bool alreadyDragged) => !alreadyDragged && flatDistance <= GrabReach;

        /// <summary>Must the player let go of the body: he is too far from it?</summary>
        public static bool MustLetGo(float flatDistance) => flatDistance > LetGoDistance;

        /// <summary>Where the body is held: <see cref="FollowDistance"/> behind a player at (<paramref name="px"/>, <paramref name="pz"/>) who faces (<paramref name="fx"/>, <paramref name="fz"/>) (any length; nothing means north), on the floor plane.</summary>
        public static void HoldSpot(float px, float pz, float fx, float fz, out float x, out float z)
        {
            float len = (float)Math.Sqrt(fx * fx + fz * fz);
            if (len < 1e-4f) { fx = 0f; fz = 1f; len = 1f; }
            x = px - fx / len * FollowDistance;
            z = pz - fz / len * FollowDistance;
        }

        /// <summary>One step of the body from (<paramref name="cx"/>, <paramref name="cz"/>) towards (<paramref name="tx"/>, <paramref name="tz"/>) in <paramref name="dt"/> seconds at <see cref="FollowSpeed"/>: it never goes past the target.</summary>
        public static void Step(float cx, float cz, float tx, float tz, float dt, out float x, out float z)
        {
            float dx = tx - cx, dz = tz - cz;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            float reach = FollowSpeed * Math.Max(0f, dt);
            if (d <= reach || d < 1e-5f) { x = tx; z = tz; return; }
            x = cx + dx / d * reach;
            z = cz + dz / d * reach;
        }
    }
}
