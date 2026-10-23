using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The pure rules of the gun safe's hack (the owner, 2026-10-18: "The pistol should be in a gun safe in the Outpost that you need to hack to get into. This should be a hack where the player has to draw a
    /// line from one end to the other with their cursor and if the player hits a wall on the hack screen they set off an alarm"). A corridor winds across the screen (a zigzag in a 16 x 9 panel); the player
    /// presses on the start circle and drags the cursor along the corridor to the end circle without touching its walls. A wall is the alarm; letting go before the end only starts it again. In the spirit of
    /// the "trace the circuit" hacking mini-games of heist games.
    /// </summary>
    public static class GunSafeHackRules
    {
        public const float PanelWidth = 16f, PanelHeight = 9f;
        /// <summary>Half the width of the corridor, and the radius of the start and end circles (panel units).</summary>
        public const float HalfWidth = 0.55f, EndRadius = 0.8f;
        public const int Points = 7;

        /// <summary>The corridor's middle line for a safe: seven points from left to right, alternately low and high, the same for the same seed.</summary>
        public static Vector2[] MakePath(int seed)
        {
            var r = new System.Random(seed * 7919 + 13);
            var pts = new Vector2[Points];
            for (int i = 0; i < Points; i++)
            {
                float x = 1.0f + i * (14.0f / (Points - 1));
                float y = (i == 0 || i == Points - 1) ? 4.5f : (i % 2 == 1 ? 1.6f + 1.8f * (float)r.NextDouble() : 5.6f + 1.8f * (float)r.NextDouble());
                pts[i] = new Vector2(x, y);
            }
            return pts;
        }

        public static float Length(Vector2[] path)
        {
            float len = 0f;
            for (int i = 0; i + 1 < path.Length; i++) len += (path[i + 1] - path[i]).magnitude;
            return len;
        }

        /// <summary>How far a point is from the corridor's middle line, and how far along the line (from its start) the nearest point of it is.</summary>
        public static float DistanceToPath(Vector2 p, Vector2[] path, out float along)
        {
            float best = float.MaxValue, bestAlong = 0f, walked = 0f;
            for (int i = 0; i + 1 < path.Length; i++)
            {
                Vector2 a = path[i], b = path[i + 1], ab = b - a;
                float len2 = ab.sqrMagnitude, len = Mathf.Sqrt(len2);
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                float d = (p - (a + ab * t)).magnitude;
                if (d < best) { best = d; bestAlong = walked + t * len; }
                walked += len;
            }
            along = bestAlong;
            return best;
        }
    }

    /// <summary>One try at the gun safe's hack: press on the start circle, drag along the corridor to the end circle. Pure.</summary>
    public sealed class GunSafeHackSession
    {
        public Vector2[] Path { get; }
        public float Total { get; }
        /// <summary>The furthest point reached along the corridor in this stroke.</summary>
        public float Along { get; private set; }
        public bool Drawing { get; private set; }
        public bool Won { get; private set; }
        /// <summary>The cursor touched a wall: the alarm.</summary>
        public bool Hit { get; private set; }
        public bool Finished => Won || Hit;

        public GunSafeHackSession(int seed) { Path = GunSafeHackRules.MakePath(seed); Total = GunSafeHackRules.Length(Path); }

        /// <summary>The button went down at <paramref name="p"/>: a stroke starts if that is on the start circle.</summary>
        public void Press(Vector2 p)
        {
            if (Finished || Drawing) return;
            if ((p - Path[0]).sqrMagnitude <= GunSafeHackRules.EndRadius * GunSafeHackRules.EndRadius) { Drawing = true; Along = 0f; }
        }

        /// <summary>The cursor moved to <paramref name="p"/> with the button held.</summary>
        public void Move(Vector2 p)
        {
            if (!Drawing || Finished) return;
            float d = GunSafeHackRules.DistanceToPath(p, Path, out float along);
            if (d > GunSafeHackRules.HalfWidth) { Hit = true; Drawing = false; return; }                // a wall
            if (along > Along) Along = along;
            if (Along >= Total - 0.2f && (p - Path[Path.Length - 1]).sqrMagnitude <= GunSafeHackRules.EndRadius * GunSafeHackRules.EndRadius) { Won = true; Drawing = false; }
        }

        /// <summary>The button went up: before the end that is only giving the stroke up (no alarm).</summary>
        public void Release() { if (Drawing && !Finished) { Drawing = false; Along = 0f; } }
    }

    /// <summary>
    /// The gun safe's hack screen: a dark panel with the corridor on it; hold the left mouse button on the green start circle and drag to the red end circle. Purely local until it ends: only the result goes
    /// to the host (<see cref="GunSafeProp.SubmitResult"/>). While it is up the player stands still and the cursor is free (<see cref="OutpostUi.Kind.GunSafe"/>); Esc, walking off or somebody's alarm backs
    /// out of it with no alarm. Modelled on <see cref="HackPanel"/>.
    /// </summary>
    public sealed class GunSafeHackPanel : MonoBehaviour
    {
        const float ResultSeconds = 1.3f;
        public static GunSafeHackPanel Instance { get; private set; }
        public static bool Running => Instance != null && Instance.session != null && !Instance.session.Finished && Instance.safe != null;

        GunSafeHackSession session;
        GunSafeProp safe;
        WobblyPlayer player;
        bool wasDown;
        float resultUntil;
        bool resultWon;
        string resultText = "";
        Rect area;
        readonly System.Collections.Generic.List<Vector2> trail = new System.Collections.Generic.List<Vector2>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
#if UNITY_EDITOR
            TestPointer = null; TestButton = null;
#endif
        }

#if UNITY_EDITOR
        /// <summary>Editor tests only (a batch run has no mouse): the cursor in panel units (null = none), and whether the button is down. A test draws through the very <see cref="Update"/> a player's mouse goes through.</summary>
        public static System.Func<Vector2?> TestPointer;
        public static System.Func<bool> TestButton;
        /// <summary>The panel's current session (for the tests).</summary>
        public GunSafeHackSession Session => session;
#endif

        public static void Begin(GunSafeProp s, WobblyPlayer who)
        {
            if (!s || !who || s.IsOpen) return;
            if (who.InCustody || who.IsHidden || OutpostUi.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open) return;
            if (Instance == null)
            {
                var go = new GameObject("GunSafeHackPanel");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<GunSafeHackPanel>();
            }
            Instance.Start(s, who);
        }

        void Start(GunSafeProp s, WobblyPlayer who)
        {
            if (Running) return;
            safe = s; player = who;
            session = new GunSafeHackSession(s.Seed);
            trail.Clear(); wasDown = false; resultUntil = 0f;
            OutpostUi.Show(OutpostUi.Kind.GunSafe);
            GameAudio.PlaySound(SoundId.KeypadBeep, s.transform.position, 0.5f, 0.8f, true, 14f, 2f, 0);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OutpostUi.Hide(OutpostUi.Kind.GunSafe);
        }

        // ------------------------------------------------------------------ the mouse

        Vector2? Pointer()
        {
#if UNITY_EDITOR
            if (TestPointer != null) return TestPointer();
#endif
            if (area.width < 1f) return null;
            var m = Input.mousePosition;
            float gx = m.x, gy = Screen.height - m.y;
            return new Vector2((gx - area.x) / area.width * GunSafeHackRules.PanelWidth, (1f - (gy - area.y) / area.height) * GunSafeHackRules.PanelHeight);
        }

        bool ButtonDown()
        {
#if UNITY_EDITOR
            if (TestButton != null) return TestButton();
#endif
            return Input.GetMouseButton(0);
        }

        void Update()
        {
            if (session == null || session.Finished) return;
            string reason = BackOutReason();
            if (reason != null) { session.Release(); End(false, reason, false); return; }
            var p = Pointer();
            bool down = ButtonDown() && p.HasValue;
            if (down && !wasDown) { session.Press(p.Value); trail.Clear(); if (session.Drawing) trail.Add(p.Value); }
            else if (down) { session.Move(p.Value); if (session.Drawing || session.Finished) trail.Add(p.Value); }
            else if (wasDown) { session.Release(); trail.Clear(); }
            wasDown = down;
            if (session.Won) End(true, "ACCESS GRANTED", true);
            else if (session.Hit) End(false, "WALL HIT - ALARM!", true);
        }

        string BackOutReason()
        {
#if UNITY_EDITOR
            if (TestPointer == null && Input.GetKeyDown(KeyCode.Escape)) return "";
#else
            if (Input.GetKeyDown(KeyCode.Escape)) return "";
#endif
            if (!safe || safe.IsOpen) return "";
            if (!player || player.InCustody || player.IsHidden) return "";
            if (EscapeMenu.Open) return "";
            if (safe.Jammed) return "The safe jammed";
            if (safe.DistanceFrom(player.transform.position) > GunSafeProp.HackReach) return "Too far from the safe";
            return null;
        }

        void End(bool won, string text, bool report)
        {
            OutpostUi.Hide(OutpostUi.Kind.GunSafe);
            resultWon = won; resultText = text;
            resultUntil = report ? Time.unscaledTime + ResultSeconds : 0f;
            if (report && safe) safe.SubmitResult(won);
            else if (text.Length > 0) Backpack.NoticeToLocal(text);
        }

        // ------------------------------------------------------------------ the screen

        static readonly Color Back = new Color(0.04f, 0.05f, 0.07f, 0.92f);
        static readonly Color Green = new Color(0.25f, 0.9f, 0.45f, 1f);
        static readonly Color Red = new Color(1f, 0.3f, 0.25f, 1f);
        static readonly Color Wall = new Color(0.16f, 0.2f, 0.24f, 1f);
        static readonly Color Floor = new Color(0.08f, 0.12f, 0.14f, 1f);

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old;
        }

        Vector2 ToGui(Vector2 u) => new Vector2(area.x + u.x / GunSafeHackRules.PanelWidth * area.width, area.y + (1f - u.y / GunSafeHackRules.PanelHeight) * area.height);

        void Segment(Vector2 a, Vector2 b, float thickness, Color c)
        {
            var ga = ToGui(a); var gb = ToGui(b);
            var mid = (ga + gb) * 0.5f;
            float len = (gb - ga).magnitude;
            float angle = Mathf.Atan2(gb.y - ga.y, gb.x - ga.x) * Mathf.Rad2Deg;
            var old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, mid);
            Fill(new Rect(mid.x - len * 0.5f, mid.y - thickness * 0.5f, len, thickness), c);
            GUI.matrix = old;
        }

        void Dot(Vector2 u, float diameter, Color c)
        {
            var g = ToGui(u);
            Fill(new Rect(g.x - diameter * 0.5f, g.y - diameter * 0.5f, diameter, diameter), c);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || session == null) return;
            bool live = !session.Finished;
            if (!live && Time.unscaledTime >= resultUntil) return;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float w = Mathf.Min(1000f * s, Screen.width * 0.82f), h = w * GunSafeHackRules.PanelHeight / GunSafeHackRules.PanelWidth + 90f * s;
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            area = new Rect(box.x + 12f * s, box.y + 70f * s, box.width - 24f * s, box.height - 82f * s);
            area.height = area.width * GunSafeHackRules.PanelHeight / GunSafeHackRules.PanelWidth;
            Fill(box, Back);
            HudText.Label(new Rect(box.x, box.y + 8f * s, w, 34f * s), "GUN SAFE LOCK", HudStyles.Label(Mathf.RoundToInt(26 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Green));
            HudText.Label(new Rect(box.x, box.y + 38f * s, w, 26f * s), live ? "Hold the mouse button on the green circle and drag to the red one. Do not touch the walls." : "",
                HudStyles.Label(Mathf.RoundToInt(17 * s), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.8f, 0.85f, 0.85f)));
            Fill(area, Wall);
            // the corridor: a thick line along the middle path, round at the joints
            float thick = 2f * GunSafeHackRules.HalfWidth / GunSafeHackRules.PanelWidth * area.width;
            var path = session.Path;
            for (int i = 0; i + 1 < path.Length; i++) Segment(path[i], path[i + 1], thick, Floor);
            for (int i = 0; i < path.Length; i++) Dot(path[i], thick, Floor);
            // the part drawn so far
            for (int i = 0; i + 1 < trail.Count; i++) Segment(trail[i], trail[i + 1], Mathf.Max(3f, 5f * s), session.Hit ? Red : Green);
            Dot(path[0], 2f * GunSafeHackRules.EndRadius / GunSafeHackRules.PanelWidth * area.width, new Color(0.2f, 0.8f, 0.4f, 0.9f));
            Dot(path[path.Length - 1], 2f * GunSafeHackRules.EndRadius / GunSafeHackRules.PanelWidth * area.width, new Color(0.9f, 0.25f, 0.2f, 0.9f));
            if (!live) HudText.Label(new Rect(box.x, area.center.y - 30f * s, w, 60f * s), resultText, HudStyles.Label(Mathf.RoundToInt(44 * s), TextAnchor.MiddleCenter, FontStyle.Bold, resultWon ? Green : Red));
            else HudText.Label(new Rect(box.x, box.yMax - 28f * s, w, 24f * s), "Esc - give up", HudStyles.Label(Mathf.RoundToInt(16 * s), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.7f, 0.75f, 0.75f)));
        }
    }
}
