using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A TEST version of the computer's Nav map, a fourth tab (MAP TEST) next to the old pages, which are untouched (the owner, 2026-10-18: "make the Nav map easier to read ... a slight ping when an officer or
    /// a player is moving ... use the reference pictures but do not copy them ... do not fully implement it yet, just show it as a test so that we can decide"). What it takes from the pictures: a clean plan with
    /// strong walls and every door drawn (the plans), icons that cannot be mixed up - a round numbered badge for a player, a red diamond for an officer, an orange bar with a padlock for a locked door and an orange
    /// tint on its room (the desk-booking plan's status colours) -, zoom buttons and a dragged, zoomed plan (the tracking plan), a trail behind moving icons (the tracking plan), colour-coded rooms with a legend (the
    /// "areas of interest" plan), a typed room number that lights up its room (the FIND box) and a click on a room that opens a small card (the pop-up card). A ping spreads from every icon that is moving.
    /// Nothing here is wired to anything else: it reads the same things the Nav page reads, and the terminal under it is the same terminal.
    /// </summary>
    public sealed partial class ComputerScreen
    {
        public const int TabMapTest = 4;

        // ---- the test map's own state
        NavView tmView;
        bool tmViewSet;
        int tmFloor = -1;
        string tmSelected;                          // number of the room that was clicked, or FloorDirectory.OutpostName for the security office
        bool tmTrails = true, tmPings = true;
        int tmDragId;
        bool tmMoved;
        Vector2 tmDragFrom, tmPanFrom;
        bool tmReq; float tmReqZoom; Vector3 tmReqCentre;
        readonly NavMotion tmMotion = new NavMotion();
        readonly List<string> tmLocked = new List<string>();
        static Vector3? tmHoverWorld;

        /// <summary>For the picture tests: pretend the pointer is over this world spot (null = the real pointer).</summary>
        public static void MapTestHover(Vector3? world) { tmHoverWorld = world; }

        public sealed class NavDebugPlayer { public Vector3 pos; public int number; }

        /// <summary>For the picture tests: players that are not there, so that a picture can show a whole party (a position and a number each).</summary>
        public static readonly List<NavDebugPlayer> DebugPlayers = new List<NavDebugPlayer>();

        /// <summary>For the picture tests: zoom the test map, centre it on a spot, select a room (null = none) and switch the trails on or off.</summary>
        public static void MapTestSet(float zoom, Vector3 centreWorld, string selectRoom, bool trails)
        {
            if (!Showing) return;
            var c = Instance;
            c.tmReq = true; c.tmReqZoom = zoom; c.tmReqCentre = centreWorld;
            c.tmSelected = selectRoom; c.tmTrails = trails;
        }

        /// <summary>For the picture tests: text in the terminal's input line, without Enter (the test map lights up the rooms it points at).</summary>
        public static void SetTerminalText(string text) { if (Showing) Instance.termInput = text ?? ""; }

        // ------------------------------------------------------------------ the look

        static readonly Color NvBg = new Color(0.022f, 0.038f, 0.070f), NvCorridor = new Color(0.105f, 0.165f, 0.260f), NvZone = new Color(0.150f, 0.235f, 0.360f), NvRoom = new Color(0.215f, 0.330f, 0.490f),
                              NvSuite = new Color(0.45f, 0.32f, 0.74f), NvServer = new Color(0.12f, 0.56f, 0.72f), NvOutpost = new Color(0.14f, 0.64f, 0.46f),
                              NvWall = new Color(0.66f, 0.76f, 0.90f), NvDoor = new Color(0.90f, 0.96f, 1f), NvDoorLocked = new Color(1f, 0.62f, 0.10f), NvDoorDead = new Color(0.34f, 0.40f, 0.52f),
                              NvOfficer = new Color(1f, 0.23f, 0.19f), NvFind = new Color(0.22f, 0.92f, 1f), NvLift = new Color(0.30f, 0.90f, 0.85f), NvGuardLift = new Color(0.94f, 0.43f, 0.66f),
                              NvStairs = new Color(0.95f, 0.82f, 0.30f), NvInk = new Color(0.05f, 0.07f, 0.11f);

        static Texture2D nvDisc, nvRing, nvRingThick, nvTri, nvDiamond;

        /// <summary>A white shape with soft edges: <paramref name="signedDistance"/> says how far inside the shape a point (x right, y up, both -1..1) is, in the same units, negative outside.</summary>
        static Texture2D MakeShape(int n, System.Func<float, float, float> signedDistance)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            float texel = 2f / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, w = (y + 0.5f) / n * 2f - 1f;           // (y = 0 is the bottom row)
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f + signedDistance(u, w) / texel) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        static float TriEdge(float ax, float az, float bx, float bz, float px, float pz) => ((bx - ax) * (pz - az) - (bz - az) * (px - ax)) / Mathf.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az));

        static void MakeShapes()
        {
            if (nvDisc != null) return;
            nvDisc = MakeShape(64, (u, w) => 0.94f - Mathf.Sqrt(u * u + w * w));
            nvRing = MakeShape(128, (u, w) => { float r = Mathf.Sqrt(u * u + w * w); return Mathf.Min(r - 0.76f, 0.95f - r); });
            nvRingThick = MakeShape(64, (u, w) => { float r = Mathf.Sqrt(u * u + w * w); return Mathf.Min(r - 0.58f, 0.94f - r); });
            nvTri = MakeShape(64, (u, w) => Mathf.Min(TriEdge(0f, 0.9f, -0.85f, -0.8f, u, w), Mathf.Min(TriEdge(-0.85f, -0.8f, 0.85f, -0.8f, u, w), TriEdge(0.85f, -0.8f, 0f, 0.9f, u, w))));
            nvDiamond = MakeShape(64, (u, w) => (0.93f - (Mathf.Abs(u) + Mathf.Abs(w))) * 0.7071f);
        }

        static void Tex(Texture2D t, Vector2 c, float w, float h, Color col)
        {
            var old = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h), t);
            GUI.color = old;
        }

        static void Disc(Vector2 c, float r, Color col) => Tex(nvDisc, c, 2f * r, 2f * r, col);
        static void RingAt(Vector2 c, float r, Color col) => Tex(nvRing, c, 2f * r, 2f * r, col);

        /// <summary>A little triangle pointing up turned <paramref name="degrees"/> clockwise.</summary>
        static void Pointer(Vector2 c, float size, float degrees, Color col)
        {
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(degrees, c);
            Tex(nvTri, c, size, size, col);
            GUI.matrix = saved;
        }

        static void Outline(Rect r, float t, Color c)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c); Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y + t, t, r.height - 2f * t), c); Fill(new Rect(r.xMax - t, r.y + t, t, r.height - 2f * t), c);
        }

        /// <summary>Text with a dark edge, so that it can be read on any colour of the plan.</summary>
        static void Halo(Rect r, string text, int size, TextAnchor anchor, FontStyle style, Color col)
        {
            var edge = new Color(0.02f, 0.04f, 0.08f, 0.9f);
            Label(new Rect(r.x - 1f, r.y, r.width, r.height), text, size, anchor, style, edge);
            Label(new Rect(r.x + 1f, r.y, r.width, r.height), text, size, anchor, style, edge);
            Label(new Rect(r.x, r.y - 1f, r.width, r.height), text, size, anchor, style, edge);
            Label(new Rect(r.x, r.y + 1f, r.width, r.height), text, size, anchor, style, edge);
            Label(r, text, size, anchor, style, col);
        }

        /// <summary>A padlock on a dark round badge: the thing that says "locked" on the plan (an officer is a red diamond, never this).</summary>
        static void LockGlyph(Vector2 c, float size)
        {
            Disc(c, size * 0.62f, new Color(0.03f, 0.05f, 0.09f, 0.95f));
            Tex(nvRingThick, new Vector2(c.x, c.y - size * 0.14f), size * 0.54f, size * 0.60f, Color.white);                                  // the shackle
            Fill(new Rect(c.x - size * 0.27f, c.y - size * 0.04f, size * 0.54f, size * 0.38f), Color.white);                                  // the body
            Fill(new Rect(c.x - size * 0.045f, c.y + size * 0.07f, size * 0.09f, size * 0.15f), NvInk);                                       // the keyhole
        }

        static void LiftTile(Vector2 c, float z, Color col)
        {
            Fill(new Rect(c.x - z * 0.5f - 1.5f, c.y - z * 0.5f - 1.5f, z + 3f, z + 3f), Color.black);
            Fill(new Rect(c.x - z * 0.5f, c.y - z * 0.5f, z, z), col);
            Pointer(new Vector2(c.x, c.y - z * 0.2f), z * 0.55f, 0f, NvInk);
            Pointer(new Vector2(c.x, c.y + z * 0.2f), z * 0.55f, 180f, NvInk);
        }

        static void StairsTile(Vector2 c, float z)
        {
            Fill(new Rect(c.x - z * 0.5f - 1.5f, c.y - z * 0.5f - 1.5f, z + 3f, z + 3f), Color.black);
            Fill(new Rect(c.x - z * 0.5f, c.y - z * 0.5f, z, z), NvStairs);
            for (int i = 0; i < 3; i++) Fill(new Rect(c.x - z * 0.34f + i * z * 0.17f, c.y + z * 0.28f - (i + 1) * z * 0.2f, z * 0.68f - i * z * 0.17f, z * 0.13f), NvInk);
        }

        static void SpeakerGlyph(Vector2 c, float r, bool on, bool live)
        {
            Disc(c, r + 1.5f, Color.black);
            Disc(c, r, !on ? new Color(0.36f, 0.14f, 0.14f) : live ? new Color(0.30f, 0.92f, 0.42f) : new Color(0.80f, 0.84f, 0.90f));
            if (r < 6f) return;                                                                              // (zoomed out: a plain dot, the cone is too small to see)
            var ink = on ? NvInk : new Color(0.95f, 0.75f, 0.75f);
            Fill(new Rect(c.x - r * 0.62f, c.y - r * 0.22f, r * 0.42f, r * 0.44f), ink);                    // the box
            Pointer(new Vector2(c.x + r * 0.05f, c.y), r * 0.95f, 270f, ink);                               // the cone, opening to the right
            if (!on) Fill(new Rect(c.x - r * 0.1f, c.y - r * 0.9f, r * 0.2f, r * 1.8f), new Color(0.95f, 0.45f, 0.45f));
        }

        static void ComputerGlyph(Vector2 c, float z)
        {
            Fill(new Rect(c.x - z * 0.56f, c.y - z * 0.5f, z * 1.12f, z * 0.82f), Color.black);
            Fill(new Rect(c.x - z * 0.46f, c.y - z * 0.4f, z * 0.92f, z * 0.62f), new Color(0.35f, 0.95f, 1f));
            Fill(new Rect(c.x - z * 0.12f, c.y + z * 0.32f, z * 0.24f, z * 0.14f), Color.black);
            Fill(new Rect(c.x - z * 0.36f, c.y + z * 0.44f, z * 0.72f, z * 0.1f), Color.black);
        }

        static void PlayerBadge(Vector2 c, float r, Color col, int n, float facingDegrees, bool you)
        {
            Disc(c, r + 3.2f, new Color(0f, 0f, 0f, 0.55f));
            Disc(c, r + 2f, Color.white);
            Disc(c, r, col);
            float rad = facingDegrees * Mathf.Deg2Rad;
            Pointer(c + new Vector2(Mathf.Sin(rad), -Mathf.Cos(rad)) * (r + 5.5f), r * 0.85f, facingDegrees, Color.white);
            Label(new Rect(c.x - r * 2f, c.y - r, r * 4f, r * 2f), n.ToString(), Mathf.RoundToInt(r * 1.35f), TextAnchor.MiddleCenter, FontStyle.Bold, NvInk);
            if (you) Halo(new Rect(c.x - 30f, c.y + r + 4f, 60f, 14f), "YOU", Mathf.RoundToInt(Mathf.Max(9f, r * 0.9f)), TextAnchor.UpperCenter, FontStyle.Bold, Color.white);
        }

        static void OfficerMark(Vector2 c, float r)
        {
            Tex(nvDiamond, c, 2f * r + 5f, 2f * r + 5f, new Color(0.16f, 0.03f, 0.03f, 0.95f));
            Tex(nvDiamond, c, 2f * r, 2f * r, NvOfficer);
        }

        static Color KindColour(NavMapRules.RoomKind k) => k == NavMapRules.RoomKind.Corridor ? NvCorridor : k == NavMapRules.RoomKind.Zone ? NvZone : k == NavMapRules.RoomKind.Suite ? NvSuite
                                                         : k == NavMapRules.RoomKind.Server ? NvServer : k == NavMapRules.RoomKind.Outpost ? NvOutpost : NvRoom;

        static Color Brighter(Color c, float f) => new Color(Mathf.Min(1f, c.r * f), Mathf.Min(1f, c.g * f), Mathf.Min(1f, c.b * f), 1f);

        static string KindName(NavMapRules.RoomKind k) => k == NavMapRules.RoomKind.Suite ? "Executive suite" : k == NavMapRules.RoomKind.Server ? "Server room" : k == NavMapRules.RoomKind.Outpost ? "Security office" : k == NavMapRules.RoomKind.Zone ? "Open area" : k == NavMapRules.RoomKind.Corridor ? "Corridor" : "Office";

        // ------------------------------------------------------------------ the page

        /// <summary>The test page: the plan with a panel at its right (zoom, options, the key), and the terminal under both.</summary>
        void DrawMapTestPage(Rect body, float s)
        {
            float termH = 106f * s, gap = 8f * s;
            DrawMapTest(new Rect(body.x, body.y, body.width, body.height - termH - gap), s);
            DrawTerminal(new Rect(body.x, body.yMax - termH, body.width, termH), s, 17f);
        }

        int PlayerNumber(WobblyPlayer wp, int order) => wp.TryGetComponent(out Net.NetPlayer slotOf) && slotOf.IsSpawned ? slotOf.Slot + 1 : order;

        void UpdateTestMotion(float now)
        {
            foreach (var g in GuardAI.All) if (g && !g.Dead) tmMotion.Step(g.GetInstanceID(), g.transform.position.x, g.transform.position.z, now);
            foreach (var wp in WobblyPlayerRegistry.All) if (wp) tmMotion.Step(wp.GetInstanceID(), wp.transform.position.x, wp.transform.position.z, now);
            for (int i = 0; i < DebugPlayers.Count; i++) tmMotion.Step(-1 - i, DebugPlayers[i].pos.x, DebugPlayers[i].pos.z, now);
            tmMotion.Sweep(now);
        }

        void DrawPings(NavMotion.Track tr, Vector2 p, float iconRadius, Color col, float now)
        {
            if (tr == null) return;
            for (int i = 0; i < tr.pings.Count; i++)
            {
                float t = NavMapRules.PingT(tr.pings[i], now);
                if (t >= 1f) continue;
                var c = Color.Lerp(col, Color.white, 0.35f); c.a = NavMapRules.PingAlpha(t);
                float pr = NavMapRules.PingRadius(t, iconRadius);
                Disc(p, pr, new Color(c.r, c.g, c.b, c.a * 0.30f));                                         // (a soft glow inside the ring, so that it shows on the dark plan)
                RingAt(p, pr, new Color(c.r, c.g, c.b, Mathf.Min(1f, c.a * 1.25f)));
            }
        }

        void DrawMapTest(Rect body, float s)
        {
            var dir = FloorDirectory.Current(prop.outpostFloor);
            if (dir == null || dir.all.Count == 0) { Label(body, "No map of this floor.", Mathf.RoundToInt(24 * s), TextAnchor.UpperLeft, FontStyle.Italic, Dim); return; }
            var ev = Event.current;
            MakeShapes();
            float now = Time.unscaledTime;
            float panelW = Mathf.Min(body.width * 0.22f, 240f * s);
            var map = new Rect(body.x, body.y, body.width - panelW - 12f * s, body.height);
            var panel = new Rect(map.xMax + 12f * s, body.y, panelW, body.height);

            // the view: fitted when the floor is new, kept while it is on show
            if (!tmViewSet || tmFloor != prop.outpostFloor)
            {
                tmView = NavView.Fit(dir.extent, map.size, 14f);
                tmViewSet = true; tmFloor = prop.outpostFloor; tmSelected = null; tmMotion.Clear();
            }
            tmView = tmView.WithSize(map.size);
            if (tmReq) { tmView = tmView.ZoomTo(tmReqZoom).CentreOn(tmReqCentre.x, tmReqCentre.z); tmReq = false; }
            if (ev.type == EventType.Repaint) UpdateTestMotion(now);
            if (ev.type == EventType.ScrollWheel && map.Contains(ev.mousePosition))
            {
                tmView = tmView.ZoomAt(Mathf.Pow(1.12f, -ev.delta.y), ev.mousePosition - map.position);
                ev.Use();
            }

            var gen = TowerManager.Instance != null ? TowerManager.Instance.generator : null;
            var doors = DoorManager.Active;
            var oc = OutpostController.Active;
            var me = player ? player : LocalPlayer();
            var rooms = dir.rooms;

            // what the terminal's line points at, and which rooms are locked
            var labels = new List<string>(rooms.Count);
            for (int i = 0; i < rooms.Count; i++) labels.Add(rooms[i].label);
            var found = NavMapRules.Matches(labels, termInput);
            bool findOffice = FloorDirectory.IsOutpostName(termInput);
            var lockedRoom = new bool[rooms.Count];
            if (doors != null) for (int i = 0; i < rooms.Count; i++) for (int j = 0; j < rooms[i].doors.Count; j++) { var d = doors.Get(rooms[i].doors[j]); if (d != null && d.ComputerLocked) lockedRoom[i] = true; }
            bool officeLocked = oc != null && oc.Door != null && oc.Door.ComputerLocked;
            tmLocked.Clear();
            for (int i = 0; i < rooms.Count; i++) if (lockedRoom[i]) tmLocked.Add(rooms[i].label);
            if (officeLocked) tmLocked.Add(FloorDirectory.OutpostName);
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);
            int outsideSize = Mathf.RoundToInt(s * Mathf.Lerp(13f, 20f, Mathf.InverseLerp(1f, 5f, tmView.zoom)));

            GUI.BeginClip(map);
            try
            {
                var v = tmView;
                var frame = new Rect(0f, 0f, map.width, map.height);
                bool InView(Vector2 p, float m) => p.x > -m && p.y > -m && p.x < frame.width + m && p.y < frame.height + m;
                Rect Rr(Rc c) { var a = v.ToScreen(c.x0, c.z1); var b = v.ToScreen(c.x1, c.z0); return new Rect(a.x, a.y, b.x - a.x, b.y - a.y); }
                Fill(frame, NvBg);

                // the rooms, each in the colour of its kind, with a dark wall between them that grows with the zoom
                float wall = Mathf.Clamp(0.14f * v.K, 1.2f, 7f);
                Rect Inset(Rect r) => new Rect(r.x + wall * 0.5f, r.y + wall * 0.5f, Mathf.Max(1f, r.width - wall), Mathf.Max(1f, r.height - wall));
                bool Seen(Rect r) => !(r.xMax < 0f || r.yMax < 0f || r.x > frame.width || r.y > frame.height);
                Rect officeRect = default; bool hasOffice = false;
                for (int i = 0; i < dir.all.Count; i++)
                {
                    var ri = dir.all[i];
                    var rr = Inset(Rr(ri.bounds));
                    var kind = NavMapRules.KindOf(ri.theme, ri.role, ri.kind);
                    if (kind == NavMapRules.RoomKind.Outpost) { officeRect = rr; hasOffice = true; }
                    if (Seen(rr)) Fill(rr, KindColour(kind));
                }
                // a locked room: an orange tint and an orange edge (and a padlock badge, below); the rooms the terminal's line points at: a cyan tint
                void LockedEdge(Rect r) { Fill(r, new Color(1f, 0.55f, 0.10f, 0.20f)); Outline(r, Mathf.Max(2f, 2.5f * s), NvDoorLocked); }
                void CornerLock(Rect r) { float g = Mathf.Clamp(Mathf.Min(r.width, r.height) * 0.55f, 12f * s, 22f * s); LockGlyph(new Vector2(r.xMax - g * 0.15f, r.y + g * 0.15f), g); }
                for (int i = 0; i < rooms.Count; i++) { var rr = Inset(Rr(rooms[i].bounds)); if (lockedRoom[i] && Seen(rr)) LockedEdge(rr); }
                if (officeLocked && hasOffice && Seen(officeRect)) LockedEdge(officeRect);
                for (int k = 0; k < found.Count; k++) Fill(Inset(Rr(rooms[found[k]].bounds)), new Color(NvFind.r, NvFind.g, NvFind.b, k == 0 ? 0.22f + 0.18f * pulse : 0.12f));
                if (findOffice && hasOffice) Fill(officeRect, new Color(NvFind.r, NvFind.g, NvFind.b, 0.22f + 0.18f * pulse));

                // every door: a light tick across the wall; locked at the computer, an orange bar; one that never opens, dim
                if (doors != null)
                    for (int i = 0; i < doors.Count; i++)
                    {
                        var d = doors.Get(i);
                        if (d == null) continue;
                        var p = v.ToScreen(d.Centre.x, d.Centre.z);
                        if (!InView(p, 20f)) continue;
                        bool locked = d.ComputerLocked;
                        float half = Mathf.Max(5f * s, d.HalfGap * v.K * 0.9f), th = Mathf.Max(wall + 1f, (locked ? 5f : d.Permanent ? 3f : 2.5f) * s);
                        var bar = d.AlongX ? new Rect(p.x - half, p.y - th * 0.5f, 2f * half, th) : new Rect(p.x - th * 0.5f, p.y - half, th, 2f * half);
                        if (locked) Fill(new Rect(bar.x - 1.5f, bar.y - 1.5f, bar.width + 3f, bar.height + 3f), new Color(0.1f, 0.03f, 0f, 0.9f));
                        Fill(bar, locked ? NvDoorLocked : d.Permanent ? NvDoorDead : NvDoor);
                    }

                // the padlock badges of the locked rooms, pinned to the top right corner of each, half outside it (under the numbers, so that a number is never hidden)
                for (int i = 0; i < rooms.Count; i++) { var rr = Inset(Rr(rooms[i].bounds)); if (lockedRoom[i] && Seen(rr)) CornerLock(rr); }
                if (officeLocked && hasOffice && Seen(officeRect)) CornerLock(officeRect);

                // the room numbers (and what kind of room it is, once there is plenty of room for it)
                for (int i = 0; i < rooms.Count; i++)
                {
                    var rr = Rr(rooms[i].bounds);
                    if (!Seen(rr)) continue;
                    bool special = found.Contains(i) || rooms[i].label == tmSelected;
                    var kind = NavMapRules.KindOf(rooms[i].info.theme, rooms[i].role, rooms[i].info.kind);
                    bool roomy = rr.width > outsideSize * 2.3f && rr.height > outsideSize * 1.5f;
                    if (!roomy && !special) continue;
                    bool caption = roomy && rr.height > outsideSize * 4f && (kind == NavMapRules.RoomKind.Suite || kind == NavMapRules.RoomKind.Server);
                    var centre = new Rect(rr.x, rr.y + (caption ? -outsideSize * 0.6f : 0f), rr.width, rr.height);
                    Halo(centre, rooms[i].label, outsideSize, TextAnchor.MiddleCenter, FontStyle.Bold, special ? NvFind : Color.white);
                    if (caption) Halo(new Rect(rr.x, rr.y + outsideSize * 0.7f, rr.width, rr.height), kind == NavMapRules.RoomKind.Suite ? "SUITE" : "SERVER", Mathf.Max(8, outsideSize - 4), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(1f, 1f, 1f, 0.8f));
                }
                for (int i = 0; i < dir.all.Count; i++)
                {
                    if (dir.all[i].theme != "outpost") continue;
                    var rr = Rr(dir.all[i].bounds);
                    if (!Seen(rr)) continue;
                    if (rr.width > outsideSize * 3f) Halo(new Rect(rr.x, rr.y + rr.height * 0.5f - outsideSize * 1.4f, rr.width, outsideSize * 1.4f), "SECURITY", Mathf.Max(8, outsideSize - 2), TextAnchor.LowerCenter, FontStyle.Bold, findOffice ? NvFind : Color.white);
                }


                // the selected room, and the rooms the terminal's line points at: an outline
                for (int k = 0; k < found.Count; k++) Outline(Inset(Rr(rooms[found[k]].bounds)), k == 0 ? 3f : 2f, new Color(NvFind.r, NvFind.g, NvFind.b, k == 0 ? 0.6f + 0.4f * pulse : 0.5f));
                if (tmSelected != null)
                {
                    if (FloorDirectory.IsOutpostName(tmSelected)) { if (hasOffice) Outline(officeRect, 3f, Color.white); }
                    else { var sel = dir.Find(tmSelected); if (sel != null) Outline(Inset(Rr(sel.bounds)), 3f, Color.white); }
                }
                // the lifts and stairs, and this computer
                if (gen != null)
                {
                    float z = 21f * s;
                    var pe = v.ToScreen(gen.PlayerElevatorCentre.x, gen.PlayerElevatorCentre.z); if (InView(pe, 20f)) LiftTile(pe, z, NvLift);
                    if (gen.HasGuardShaft) { var pg = v.ToScreen(gen.GuardShaftCentre.x, gen.GuardShaftCentre.z); if (InView(pg, 20f)) LiftTile(pg, z, NvGuardLift); }
                    for (int i = 0; i < gen.StairCount; i++) { var ps = v.ToScreen(gen.StairCentre(i).x, gen.StairCentre(i).z); if (InView(ps, 20f)) StairsTile(ps, z); }
                }
                var pc = v.ToScreen(prop.transform.position.x, prop.transform.position.z);
                if (InView(pc, 20f)) ComputerGlyph(pc, 14f * s);

                // the hallway speakers: a click switches one off or on
                for (int i = 0; i < PaState.Speakers.Count; i++)
                {
                    var sp = PaState.Speakers[i];
                    var spp = v.ToScreen(sp.x, sp.y);
                    if (!InView(spp, 20f)) continue;
                    float r = Mathf.Lerp(4.2f, 7f, Mathf.InverseLerp(1f, 2.2f, v.zoom)) * s;                // (small dots while zoomed out: there are a lot of them)
                    bool on = PaRules.IsOn(PaState.Mask, i);
                    SpeakerGlyph(spp, r, on, PaState.Live);
                    if (ev.type == EventType.MouseDown && ev.button == 0 && new Rect(spp.x - r - 3f, spp.y - r - 3f, 2f * r + 6f, 2f * r + 6f).Contains(ev.mousePosition))
                    {
                        PaState.RequestMask(PaRules.Toggle(PaState.Mask, i));
                        ev.Use();
                    }
                }

                // the trails (small fading dots behind what moves), then the pings, then the icons themselves
                if (tmTrails)
                {
                    foreach (var g in GuardAI.All) if (g && !g.Dead) DrawTrail(tmMotion.Get(g.GetInstanceID()), v, NvOfficer, 2.4f * s, now);
                    int order0 = 0;
                    foreach (var wp in WobblyPlayerRegistry.All) { order0++; if (wp) DrawTrail(tmMotion.Get(wp.GetInstanceID()), v, HudTheme.PlayerColour(PlayerNumber(wp, order0)), 2.6f * s, now); }
                    for (int i = 0; i < DebugPlayers.Count; i++) DrawTrail(tmMotion.Get(-1 - i), v, HudTheme.PlayerColour(DebugPlayers[i].number), 2.6f * s, now);
                }
                if (tmPings)
                {
                    foreach (var g in GuardAI.All) if (g && !g.Dead) { var p = v.ToScreen(g.transform.position.x, g.transform.position.z); if (InView(p, 60f)) DrawPings(tmMotion.Get(g.GetInstanceID()), p, 9f * s, new Color(1f, 0.35f, 0.30f), now); }
                    int order1 = 0;
                    foreach (var wp in WobblyPlayerRegistry.All)
                    {
                        order1++;
                        if (!wp) continue;
                        var p = v.ToScreen(wp.transform.position.x, wp.transform.position.z);
                        if (InView(p, 60f)) DrawPings(tmMotion.Get(wp.GetInstanceID()), p, 10f * s, HudTheme.PlayerColour(PlayerNumber(wp, order1)), now);
                    }
                    for (int i = 0; i < DebugPlayers.Count; i++)
                    {
                        var p = v.ToScreen(DebugPlayers[i].pos.x, DebugPlayers[i].pos.z);
                        if (InView(p, 60f)) DrawPings(tmMotion.Get(-1 - i), p, 10f * s, HudTheme.PlayerColour(DebugPlayers[i].number), now);
                    }
                }
                foreach (var g in GuardAI.All)
                {
                    if (!g || g.Dead) continue;
                    var p = v.ToScreen(g.transform.position.x, g.transform.position.z);
                    if (InView(p, 20f)) OfficerMark(p, 9.5f * s);
                }
                int order = 0;
                foreach (var wp in WobblyPlayerRegistry.All)
                {
                    order++;
                    if (!wp) continue;
                    var p = v.ToScreen(wp.transform.position.x, wp.transform.position.z);
                    if (!InView(p, 24f)) continue;
                    PlayerBadge(p, 10f * s, HudTheme.PlayerColour(PlayerNumber(wp, order)), PlayerNumber(wp, order), Mathf.Atan2(wp.transform.forward.x, wp.transform.forward.z) * Mathf.Rad2Deg, me != null && wp == me);
                }
                for (int i = 0; i < DebugPlayers.Count; i++)
                {
                    var p = v.ToScreen(DebugPlayers[i].pos.x, DebugPlayers[i].pos.z);
                    if (InView(p, 24f)) PlayerBadge(p, 10f * s, HudTheme.PlayerColour(DebugPlayers[i].number), DebugPlayers[i].number, 0f, false);
                }

                // the room under the pointer: an outline and a tag with its number, its kind and whether it is locked (the pop-up of the plans, without a click)
                {
                    var mouse = ev.mousePosition;
                    bool hovering = tmHoverWorld.HasValue || (frame.Contains(mouse) && GUIUtility.hotControl == 0);
                    if (tmHoverWorld.HasValue) mouse = v.ToScreen(tmHoverWorld.Value.x, tmHoverWorld.Value.z);
                    string hover = hovering && ev.type == EventType.Repaint ? RoomAt(dir, v.ToWorld(mouse)) : null;
                    if (hover != null)
                    {
                        bool hOffice = FloorDirectory.IsOutpostName(hover);
                        var hRoom = hOffice ? null : dir.Find(hover);
                        Rc hb = default; RoomInfo hInfo = null;
                        if (hOffice) { foreach (var ri in dir.all) if (ri.theme == "outpost") { hb = ri.bounds; hInfo = ri; break; } }
                        else if (hRoom != null) { hb = hRoom.bounds; hInfo = hRoom.info; }
                        if (hInfo != null)
                        {
                            Outline(Inset(Rr(hb)), 2f, new Color(1f, 1f, 1f, 0.6f));
                            bool hLocked = doors != null && doors.ComputerIsLocked(prop.outpostFloor, hOffice ? FloorDirectory.OutpostName : hRoom.label);
                            string tag = (hOffice ? "SECURITY OFFICE" : "ROOM " + hRoom.label) + "  -  " + KindName(NavMapRules.KindOf(hInfo.theme, hInfo.role, hInfo.kind)) + (hLocked ? "  -  LOCKED" : "");
                            int ts = Mathf.RoundToInt(13 * s);
                            float tw = tag.Length * ts * 0.58f + 16f * s, th2 = 24f * s;
                            var tr = new Rect(Mathf.Clamp(mouse.x + 14f * s, 2f, Mathf.Max(2f, frame.width - tw - 2f)), Mathf.Clamp(mouse.y + 18f * s, 2f, Mathf.Max(2f, frame.height - th2 - 2f)), tw, th2);
                            Fill(new Rect(tr.x - 1.5f, tr.y - 1.5f, tr.width + 3f, tr.height + 3f), hLocked ? NvDoorLocked : new Color(0.66f, 0.76f, 0.90f, 0.9f));
                            Fill(tr, new Color(0.04f, 0.07f, 0.12f, 0.97f));
                            Label(tr, tag, ts, TextAnchor.MiddleCenter, FontStyle.Bold, hLocked ? NvDoorLocked : Light);
                        }
                    }
                }

                // the plan can be dragged and a click on a room selects it (a click on a speaker was taken above)
                var inFrame = new Rect(0f, 0f, map.width, map.height);
                int id = GUIUtility.GetControlID(FocusType.Passive, inFrame);
                if (ev.type == EventType.MouseDown && ev.button == 0 && inFrame.Contains(ev.mousePosition)) { GUIUtility.hotControl = id; tmDragId = id; tmMoved = false; tmDragFrom = ev.mousePosition; tmPanFrom = tmView.pan; ev.Use(); }
                else if (ev.type == EventType.MouseDrag && GUIUtility.hotControl == id)
                {
                    var delta = ev.mousePosition - tmDragFrom;
                    if (delta.magnitude > 4f) tmMoved = true;
                    if (tmMoved) tmView = tmView.Dragged(tmPanFrom, delta);
                    ev.Use();
                }
                else if (ev.type == EventType.MouseUp && GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    if (!tmMoved) tmSelected = RoomAt(dir, v.ToWorld(ev.mousePosition));
                    ev.Use();
                }
            }
            finally { GUI.EndClip(); }

            // ---- over the plan: the ribbon, the hint, the card of the selected room
            var ribbon = new Rect(map.xMax - 96f * s, map.y + 6f * s, 90f * s, 22f * s);
            Fill(ribbon, new Color(1f, 0.62f, 0.10f, 0.95f));
            Label(ribbon, "TEST MAP", Mathf.RoundToInt(13 * s), TextAnchor.MiddleCenter, FontStyle.Bold, NvInk);

            if (tmSelected != null) DrawRoomCard(map, dir, s);

            DrawMapTestPanel(panel, map, s, now);
        }

        void DrawTrail(NavMotion.Track tr, NavView v, Color col, float r, float now)
        {
            if (tr == null) return;
            for (int i = 0; i < tr.trail.Count; i++)
            {
                var d = tr.trail[i];
                float a = 1f - Mathf.Clamp01((now - d.z) / NavMotion.TrailLife);
                var p = v.ToScreen(d.x, d.y);
                Disc(p, r * (0.55f + 0.45f * a), new Color(col.r, col.g, col.b, 0.55f * a));
            }
        }

        /// <summary>The number of the room under a world spot, FloorDirectory.OutpostName for the security office, or null.</summary>
        static string RoomAt(FloorDirectory dir, Vector2 world)
        {
            FloorDirectory.Room best = null; float bestArea = float.MaxValue;
            foreach (var r in dir.rooms)
            {
                if (!r.bounds.Contains(world.x, world.y)) continue;
                float a = r.bounds.W * r.bounds.D;
                if (a < bestArea) { best = r; bestArea = a; }
            }
            if (best != null) return best.label;
            foreach (var ri in dir.all) if (ri.theme == "outpost" && ri.bounds.Contains(world.x, world.y)) return FloorDirectory.OutpostName;
            return null;
        }

        void DrawRoomCard(Rect map, FloorDirectory dir, float s)
        {
            bool office = FloorDirectory.IsOutpostName(tmSelected);
            FloorDirectory.Room room = office ? null : dir.Find(tmSelected);
            if (!office && room == null) { tmSelected = null; return; }
            Rc bounds = default;
            RoomInfo info = null;
            if (office) { foreach (var ri in dir.all) if (ri.theme == "outpost") { info = ri; bounds = ri.bounds; break; } }
            else { info = room.info; bounds = room.bounds; }
            var kind = info != null ? NavMapRules.KindOf(info.theme, info.role, info.kind) : NavMapRules.RoomKind.Room;
            var doors = DoorManager.Active;
            bool locked = doors != null && doors.ComputerIsLocked(prop.outpostFloor, office ? FloorDirectory.OutpostName : room.label);
            int players = 0, officers = 0;
            foreach (var wp in WobblyPlayerRegistry.All) if (wp && bounds.Contains(wp.transform.position.x, wp.transform.position.z)) players++;
            for (int i = 0; i < DebugPlayers.Count; i++) if (bounds.Contains(DebugPlayers[i].pos.x, DebugPlayers[i].pos.z)) players++;
            foreach (var g in GuardAI.All) if (g && !g.Dead && bounds.Contains(g.transform.position.x, g.transform.position.z)) officers++;

            var card = new Rect(map.x + 10f * s, map.y + 10f * s, 268f * s, 132f * s);
            Fill(new Rect(card.x - 2f, card.y - 2f, card.width + 4f, card.height + 4f), new Color(0f, 0f, 0f, 0.6f));
            Fill(card, new Color(0.05f, 0.08f, 0.14f, 0.96f));
            Fill(new Rect(card.x, card.y, 7f * s, card.height), Brighter(KindColour(kind), 1.35f));
            float x = card.x + 18f * s;
            Label(new Rect(x, card.y + 6f * s, card.width - 60f * s, 30f * s), office ? "SECURITY OFFICE" : "ROOM " + room.label, Mathf.RoundToInt(22 * s), TextAnchor.MiddleLeft, FontStyle.Bold, Light);
            Label(new Rect(x, card.y + 36f * s, card.width - 24f * s, 22f * s), KindName(kind) + (office ? "" : "  -  " + room.doors.Count + (room.doors.Count == 1 ? " door" : " doors")), Mathf.RoundToInt(15 * s), TextAnchor.MiddleLeft, FontStyle.Normal, Dim);
            var pill = new Rect(x, card.y + 62f * s, 92f * s, 24f * s);
            Fill(pill, locked ? NvDoorLocked : new Color(0.30f, 0.80f, 0.45f));
            Label(pill, locked ? "LOCKED" : "OPEN", Mathf.RoundToInt(14 * s), TextAnchor.MiddleCenter, FontStyle.Bold, NvInk);
            Label(new Rect(x + 102f * s, card.y + 62f * s, card.width - 120f * s, 24f * s), "inside: " + players + (players == 1 ? " player, " : " players, ") + officers + (officers == 1 ? " officer" : " officers"), Mathf.RoundToInt(13 * s), TextAnchor.MiddleLeft, FontStyle.Normal, Light);
            if (MenuKit.Button(new Rect(x, card.y + 94f * s, 130f * s, 30f * s), locked ? "UNLOCK" : "LOCK", locked ? HudTheme.Cash : HudTheme.Quota, 15f)) RunDoor(office ? FloorDirectory.OutpostName : room.label, null);
            if (MenuKit.Button(new Rect(card.xMax - 44f * s, card.y + 6f * s, 36f * s, 28f * s), "X", HudTheme.Info, 14f)) tmSelected = null;
        }

        void DrawMapTestPanel(Rect panel, Rect map, float s, float now)
        {
            float px = panel.x, py = panel.y, pw = panel.width, gap = 6f * s;
            float bw = (pw - 2f * gap) / 3f;
            if (GUI.Button(new Rect(px, py, bw, 28f * s), "+", Button(Mathf.RoundToInt(18 * s)))) tmView = tmView.ZoomAt(NavMapRules.ZoomStep, map.size * 0.5f);
            if (GUI.Button(new Rect(px + bw + gap, py, bw, 28f * s), "-", Button(Mathf.RoundToInt(18 * s)))) tmView = tmView.ZoomAt(1f / NavMapRules.ZoomStep, map.size * 0.5f);
            if (GUI.Button(new Rect(px + 2f * (bw + gap), py, bw, 28f * s), "FIT", Button(Mathf.RoundToInt(13 * s)))) tmView = NavView.Fit(tmView.extent, map.size, 14f);
            py += 34f * s;
            float hw = (pw - gap) * 0.5f;
            if (GUI.Button(new Rect(px, py, hw, 26f * s), tmTrails ? "TRAILS: ON" : "TRAILS: OFF", Button(Mathf.RoundToInt(11 * s)))) tmTrails = !tmTrails;
            if (GUI.Button(new Rect(px + hw + gap, py, hw, 26f * s), tmPings ? "PINGS: ON" : "PINGS: OFF", Button(Mathf.RoundToInt(11 * s)))) tmPings = !tmPings;
            py += 34f * s;

            // the locked rooms as a list (a plan is hard to scan for them)
            Label(new Rect(px, py, pw, 18f * s), tmLocked.Count == 0 ? "No locked rooms" : "LOCKED (" + tmLocked.Count + ")", Mathf.RoundToInt(13 * s), TextAnchor.MiddleLeft, FontStyle.Bold, tmLocked.Count == 0 ? Dim : NvDoorLocked);
            py += 19f * s;
            if (tmLocked.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                int shown = 0, lineLen = 0, lines = 1;
                foreach (var lab in tmLocked)
                {
                    int add = lab.Length + 2;
                    if (lineLen + add > 26) { if (lines == 2) break; sb.Append('\n'); lines++; lineLen = 0; }
                    sb.Append(lab).Append("  "); lineLen += add; shown++;
                }
                if (shown < tmLocked.Count) sb.Append("+").Append(tmLocked.Count - shown);
                Label(new Rect(px, py, pw, 36f * s), sb.ToString(), Mathf.RoundToInt(13 * s), TextAnchor.UpperLeft, FontStyle.Normal, Light);
                py += 38f * s;
            }
            else py += 6f * s;

            // the key: every row is drawn like the thing on the plan
            float row = 22f * s, gx = px + 14f * s, tx = px + 34f * s;
            int ls = Mathf.RoundToInt(14 * s);
            void Key(string text) { Label(new Rect(tx, py, pw - 34f * s, row), text, ls, TextAnchor.MiddleLeft, FontStyle.Normal, Light); py += row; }
            void Swatch(Color c) { Fill(new Rect(gx - 7f * s, py + row * 0.5f - 6f * s, 14f * s, 12f * s), c); Outline(new Rect(gx - 7f * s, py + row * 0.5f - 6f * s, 14f * s, 12f * s), 1f, new Color(NvWall.r, NvWall.g, NvWall.b, 0.8f)); }
            PlayerBadge(new Vector2(gx, py + row * 0.5f), 7f * s, HudTheme.PlayerColour(1), 1, 0f, false); Key("Player (arrow: facing)");
            OfficerMark(new Vector2(gx, py + row * 0.5f), 7f * s); Key("Officer");
            Fill(new Rect(gx - 7f * s, py + row * 0.5f - 2.5f * s, 14f * s, 5f * s), NvDoorLocked); LockGlyph(new Vector2(gx, py + row * 0.5f), 12f * s); Key("Locked door / room");
            Fill(new Rect(gx - 7f * s, py + row * 0.5f - 1.5f * s, 14f * s, 3f * s), NvDoor); Key("Door");
            ComputerGlyph(new Vector2(gx, py + row * 0.5f), 11f * s); Key("This computer");
            LiftTile(new Vector2(gx, py + row * 0.5f), 15f * s, NvLift); Key("Elevator");
            LiftTile(new Vector2(gx, py + row * 0.5f), 15f * s, NvGuardLift); Key("Guards' elevator");
            StairsTile(new Vector2(gx, py + row * 0.5f), 15f * s); Key("Stairwell");
            SpeakerGlyph(new Vector2(gx, py + row * 0.5f), 6.5f * s, true, false); Key("Speaker (click: on / off)");
            Swatch(NvSuite); Key("Executive suite");
            Swatch(NvServer); Key("Server room");
            Swatch(NvOutpost); Key("Security office");
            py += 6f * s;
            if (GUI.Button(new Rect(px, py, pw, 26f * s), "SELECT ALL SPEAKERS", Button(Mathf.RoundToInt(11 * s)))) PaState.RequestMask(PaRules.MaskOf(PaState.Speakers.Count));
            py += 30f * s;
            if (GUI.Button(new Rect(px, py, pw, 26f * s), "DESELECT ALL", Button(Mathf.RoundToInt(11 * s)))) PaState.RequestMask(0UL);
            py += 32f * s;
            Label(new Rect(px, py, pw + 20f * s, 22f * s), paHeld ? "ON AIR" : "Hold V to speak through the speakers", Mathf.RoundToInt((paHeld ? 18 : 11) * s), TextAnchor.MiddleLeft, paHeld ? FontStyle.Bold : FontStyle.Normal, paHeld ? MapSpeakerLive : Dim);
            py += 26f * s;
            Label(new Rect(px, py, pw + 20f * s, 18f * s), "wheel: zoom   drag: move", Mathf.RoundToInt(11 * s), TextAnchor.MiddleLeft, FontStyle.Normal, Dim);
            py += 17f * s;
            Label(new Rect(px, py, pw + 20f * s, 18f * s), "point at a room: its tag; click: its card", Mathf.RoundToInt(11 * s), TextAnchor.MiddleLeft, FontStyle.Normal, Dim);
        }
    }
}
