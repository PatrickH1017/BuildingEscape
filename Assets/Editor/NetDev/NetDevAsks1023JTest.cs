using System;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), T12 part 3: a dragged body follows the path the player walked (<c>DraggableBodyRules.Trail</c>), not a spot behind where he faces (the player's body follows the camera, so
    /// "behind him" is not where he walks: a body pulled to that spot caught on the first wall). Called from <c>NetDevTest.PolicyTest</c>; the Play half is in <c>NetDevAsks1023F.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023JTest()
        {
            // ---- a straight walk: the body ends 1.2 m behind the player, on his line, and never reaches him
            var t = new DraggableBodyRules.Trail();
            t.Begin(0f, 0f, 1f, 0f);
            float bx = 0f, bz = 0f;
            for (float x = 1.2f; x <= 6.001f; x += 0.2f) { t.Add(x, 0f); t.Advance(0.1f, out bx, out bz); }
            for (int i = 0; i < 100; i++) t.Advance(0.1f, out bx, out bz);
            Check("drag trail (T12): the player walks six metres in a line: the body comes to rest on that line, the follow distance behind him",
                Math.Abs(bx - (6f - DraggableBodyRules.FollowDistance)) < 0.01f && Math.Abs(bz) < 1e-4f, $"({bx:0.00}, {bz:0.00})");

            // ---- it is held back by its speed: in one second it moves no further than the follow speed allows; next to the player it does not move at all
            var s = new DraggableBodyRules.Trail();
            s.Begin(0f, 0f, 1f, 0f);
            s.Add(8f, 0f, true);
            s.Advance(1f, out float sx, out _);
            Check("drag trail (T12): the body moves at the follow speed at the most", Math.Abs(sx - DraggableBodyRules.FollowSpeed) < 0.01f, $"({sx:0.00} m in a second)");
            var near = new DraggableBodyRules.Trail();
            near.Begin(0f, 0f, 1.0f, 0f);
            near.Advance(1f, out float nx0, out float nz0);
            near.Advance(1f, out float nx1, out _);
            Check("drag trail (T12): a player who stands next to the body (closer than the follow distance) leaves it where it is, however long he stands", Math.Abs(nx0) < 1e-4f && Math.Abs(nz0) < 1e-4f && Math.Abs(nx1) < 1e-4f);

            // ---- a corner: the body goes round it, along the way the player went, and never across it
            var c = new DraggableBodyRules.Trail();
            c.Begin(0f, 0f, 1f, 0f);
            bool cut = false;
            float cx = 0f, cz = 0f;
            for (float x = 1.2f; x <= 5.001f; x += 0.2f) { c.Add(x, 0f); c.Advance(0.1f, out cx, out cz); cut |= cz > 0.01f && cx < 4.99f; }
            for (float z = 0.2f; z <= 4.001f; z += 0.2f) { c.Add(5f, z); c.Advance(0.1f, out cx, out cz); cut |= cz > 0.01f && cx < 4.99f; }
            for (int i = 0; i < 100; i++) { c.Advance(0.1f, out cx, out cz); cut |= cz > 0.01f && cx < 4.99f; }
            Check("drag trail (T12): round a corner the body never cuts across it, and comes to rest the follow distance behind the player on his path",
                !cut && Math.Abs(cx - 5f) < 0.01f && Math.Abs(cz - (4f - DraggableBodyRules.FollowDistance)) < 0.01f, $"(cut the corner {cut}, at {cx:0.00}, {cz:0.00})");

            // ---- the player turns round and walks back: the body goes on out along the path to where he turned (it does not cut across to him) and never jumps
            var b = new DraggableBodyRules.Trail();
            b.Begin(0f, 0f, 1f, 0f);
            for (float x = 1.2f; x <= 5.001f; x += 0.2f) { b.Add(x, 0f); b.Advance(0.1f, out _, out _); }
            for (int i = 0; i < 60; i++) b.Advance(0.1f, out _, out _);
            b.Advance(0f, out float last, out _);
            float peak = last; bool jump = false;
            for (float x = 4.8f; x >= 2.0f; x -= 0.2f)
            {
                b.Add(x, 0f); b.Advance(0.1f, out float now, out _);
                peak = Math.Max(peak, now); jump |= Math.Abs(now - last) > DraggableBodyRules.FollowSpeed * 0.1f + 1e-3f; last = now;
            }
            for (int i = 0; i < 60; i++)
            {
                b.Advance(0.1f, out float now, out _);
                peak = Math.Max(peak, now); jump |= Math.Abs(now - last) > DraggableBodyRules.FollowSpeed * 0.1f + 1e-3f; last = now;
            }
            Check("drag trail (T12): when the player turns round and walks back, the body goes on along his path to where he turned and never jumps", peak > 4.5f && !jump, $"(the body got as far as {peak:0.00}, jumped {jump})");

            // ---- the memory stays small: a long walk keeps a short path
            var m = new DraggableBodyRules.Trail();
            m.Begin(0f, 0f, 1f, 0f);
            for (int i = 0; i < 2000; i++) { m.Add(1f + 0.2f * (i + 1), 0f); m.Advance(0.05f, out _, out _); }
            Check("drag trail (T12): after a walk of four hundred metres the path holds a few dozen points at the most, not thousands, and the body is close behind the player", m.Count < 60 && m.Lag < 4.5f, $"({m.Count} points, the body {m.Lag:0.0} m behind along the path)");

            // ---- points are added only once he has walked a step
            var p = new DraggableBodyRules.Trail();
            p.Begin(0f, 0f, 1f, 0f);
            int n0 = p.Count;
            p.Add(1.05f, 0f); p.Add(1.1f, 0f);
            bool small = p.Count == n0;
            int n1 = p.Count;
            p.Add(1.2f, 0f);
            Check("drag trail (T12): a step of ten centimetres adds no point, one of twenty does", small && p.Count == n1 + 1 && DraggableBodyRules.Trail.MinStep == 0.15f);
        }
    }
}
