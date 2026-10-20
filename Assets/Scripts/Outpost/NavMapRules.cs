using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The view of the TEST version of the computer's map (the owner, 2026-10-18): the floor in the middle of a frame, zoomed and dragged. Pure arithmetic, so it is tested outside the game.
    /// World x runs to the right and world z up the map (the screen's y runs the other way).
    /// </summary>
    public struct NavView
    {
        public const float MinZoom = 1f, MaxZoom = 8f;

        public Rc extent;          // the floor, in metres
        public Vector2 size;       // the frame, in pixels
        public float margin;       // pixels left empty round the floor while all of it is in view
        public float zoom;         // 1 = the whole floor fits
        public Vector2 pan;        // how far the picture has been dragged, in pixels

        public static NavView Fit(Rc extent, Vector2 size, float margin = 14f) => new NavView { extent = extent, size = size, margin = margin, zoom = 1f, pan = Vector2.zero };

        /// <summary>Pixels to a metre while the whole floor fits.</summary>
        public float K0 => Mathf.Max(0.01f, Mathf.Min((size.x - 2f * margin) / Mathf.Max(1f, extent.W), (size.y - 2f * margin) / Mathf.Max(1f, extent.D)));
        /// <summary>Pixels to a metre now.</summary>
        public float K => K0 * zoom;
        Vector2 Middle => size * 0.5f + pan;

        public Vector2 ToScreen(float x, float z)
        {
            float k = K; var m = Middle;
            return new Vector2(m.x + (x - extent.Cx) * k, m.y - (z - extent.Cz) * k);
        }

        /// <summary>The world spot (x, z) under a screen point.</summary>
        public Vector2 ToWorld(Vector2 p)
        {
            float k = K; var m = Middle;
            return new Vector2(extent.Cx + (p.x - m.x) / k, extent.Cz - (p.y - m.y) / k);
        }

        public NavView WithSize(Vector2 newSize) { var v = this; v.size = newSize; return v.Clamped(); }

        /// <summary>Zoom by a factor, the spot under <paramref name="anchor"/> (a point of the frame) staying where it is.</summary>
        public NavView ZoomAt(float factor, Vector2 anchor)
        {
            var v = this;
            Vector2 world = ToWorld(anchor);
            v.zoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            v.pan += anchor - v.ToScreen(world.x, world.y);
            return v.Clamped();
        }

        public NavView ZoomTo(float newZoom) => ZoomAt(newZoom / Mathf.Max(0.01f, zoom), size * 0.5f);

        /// <summary>Move the picture so that this world spot is in the middle of the frame (as far as the frame's limits allow).</summary>
        public NavView CentreOn(float x, float z)
        {
            var v = this;
            v.pan += size * 0.5f - ToScreen(x, z);
            return v.Clamped();
        }

        public NavView Dragged(Vector2 panFrom, Vector2 delta) { var v = this; v.pan = panFrom + delta; return v.Clamped(); }

        /// <summary>The picture cannot be dragged off the frame: with everything in view it stays put, zoomed in it may go a little past its edges.</summary>
        public NavView Clamped()
        {
            var v = this;
            v.zoom = Mathf.Clamp(v.zoom, MinZoom, MaxZoom);
            if (v.zoom <= 1.001f) { v.pan = Vector2.zero; return v; }
            float cw = extent.W * v.K, ch = extent.D * v.K;
            float mx = Mathf.Max(0f, (cw - size.x) * 0.5f) + 40f, my = Mathf.Max(0f, (ch - size.y) * 0.5f) + 40f;
            v.pan = new Vector2(Mathf.Clamp(v.pan.x, -mx, mx), Mathf.Clamp(v.pan.y, -my, my));
            return v;
        }
    }

    /// <summary>
    /// Who is moving on the TEST map, and when a moving icon gives off a ping (the owner, 2026-10-18: "when an officer or player is moving their icon should have a slight ping going off"): each icon is followed
    /// frame by frame; while it moves it pings every <see cref="PingEvery"/> seconds (the first a moment after it sets off) and leaves a dot of a trail every <see cref="TrailEvery"/> seconds. Pure.
    /// </summary>
    public sealed class NavMotion
    {
        public const float MovingSpeed = 0.6f;      // metres a second above which an icon counts as moving
        public const float PingEvery = 0.9f;        // seconds of movement between two pings
        public const float FirstPing = 0.25f;       // seconds of movement before the first one
        public const float PingLife = 1.2f;         // seconds a ping takes to spread and fade
        public const float TrailEvery = 0.25f;      // seconds between two dots of a trail
        public const float TrailLife = 4f;          // seconds a dot stays
        public const float TeleportSpeed = 12f;     // faster than anybody walks: a jump (the elevator, a new floor), not movement

        public sealed class Track
        {
            public float x, z, lastT, speed, moving, nextPingAt = FirstPing, lastDotAt = -99f;
            public bool has;
            public int emitted;                                                    // pings given off so far (for the tests)
            public readonly List<float> pings = new List<float>();                 // when the pings still on show started
            public readonly List<Vector3> trail = new List<Vector3>();             // x, z and the time of each dot still on show
            public bool IsMoving => speed >= MovingSpeed;
        }

        readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
        readonly List<int> gone = new List<int>();

        public int Count => tracks.Count;
        public Track Get(int id) => tracks.TryGetValue(id, out var t) ? t : null;

        /// <summary>Tell it where an icon is now (once a frame). A second call in the same frame changes nothing.</summary>
        public Track Step(int id, float x, float z, float now)
        {
            if (!tracks.TryGetValue(id, out var t)) { t = new Track(); tracks[id] = t; }
            if (!t.has) { t.x = x; t.z = z; t.lastT = now; t.has = true; return t; }
            float dt = now - t.lastT;
            if (dt <= 1e-4f) return t;
            float dx = x - t.x, dz = z - t.z;
            float inst = Mathf.Sqrt(dx * dx + dz * dz) / dt;
            if (inst > TeleportSpeed) inst = 0f;
            t.speed = Mathf.Lerp(t.speed, inst, 1f - Mathf.Exp(-dt * 8f));
            t.x = x; t.z = z; t.lastT = now;
            if (t.IsMoving && inst >= MovingSpeed * 0.5f)                         // (it has to be moving this very frame too: no last ping while it slows to a stop)
            {
                t.moving += dt;
                if (t.moving >= t.nextPingAt) { t.pings.Add(now); t.emitted++; t.nextPingAt = t.moving + PingEvery; }
                if (now - t.lastDotAt >= TrailEvery) { t.trail.Add(new Vector3(x, z, now)); t.lastDotAt = now; }
            }
            else { t.moving = 0f; t.nextPingAt = FirstPing; }
            for (int i = t.pings.Count - 1; i >= 0; i--) if (now - t.pings[i] > PingLife) t.pings.RemoveAt(i);
            for (int i = t.trail.Count - 1; i >= 0; i--) if (now - t.trail[i].z > TrailLife) t.trail.RemoveAt(i);
            return t;
        }

        /// <summary>Forget the icons that have not been seen for a second (a player who left, an officer who is gone).</summary>
        public void Sweep(float now)
        {
            gone.Clear();
            foreach (var kv in tracks) if (now - kv.Value.lastT > 1f) gone.Add(kv.Key);
            for (int i = 0; i < gone.Count; i++) tracks.Remove(gone[i]);
        }

        public void Clear() => tracks.Clear();
    }

    /// <summary>What the TEST map decides without drawing anything: the look of a room, what a typed number points at, how a ping spreads. Pure.</summary>
    public static class NavMapRules
    {
        public enum RoomKind { Corridor, Zone, Room, Suite, Server, Outpost }

        public const float ZoomStep = 1.35f;

        /// <summary>How the map draws a room of the layout.</summary>
        public static RoomKind KindOf(string theme, string role, int kind)
        {
            if (theme == "outpost") return RoomKind.Outpost;
            if (role == "suite") return RoomKind.Suite;
            if (role == "server") return RoomKind.Server;
            if (kind == 2) return RoomKind.Corridor;
            if (kind == 1) return RoomKind.Zone;
            return RoomKind.Room;
        }

        /// <summary>The rooms that a text typed at the terminal points at: the one with exactly this number first, then those whose number starts with it (capitals and spaces round it do not matter), at most <paramref name="max"/>.</summary>
        public static List<int> Matches(IList<string> labels, string typed, int max = 8)
        {
            var found = new List<int>();
            string t = (typed ?? "").Trim();
            if (t.Length == 0 || labels == null) return found;
            for (int i = 0; i < labels.Count; i++) if (string.Equals(labels[i], t, System.StringComparison.OrdinalIgnoreCase)) { found.Add(i); break; }
            for (int i = 0; i < labels.Count && found.Count < max; i++)
                if (!found.Contains(i) && labels[i] != null && labels[i].StartsWith(t, System.StringComparison.OrdinalIgnoreCase)) found.Add(i);
            return found;
        }

        /// <summary>How far through its life a ping is, 0 (just given off) to 1 (gone).</summary>
        public static float PingT(float started, float now) => Mathf.Clamp01((now - started) / NavMotion.PingLife);

        /// <summary>The radius of a ping at <paramref name="t"/>: it leaves the icon's own edge and spreads to 3.3 times that, fast at first.</summary>
        public static float PingRadius(float t, float iconRadius) => iconRadius * Mathf.Lerp(1.1f, 3.3f, 1f - (1f - t) * (1f - t));

        /// <summary>How strong a ping still is: a slight one (never more than 0.6) that fades out.</summary>
        public static float PingAlpha(float t) => 0.6f * Mathf.Pow(1f - t, 1.6f);
    }
}
