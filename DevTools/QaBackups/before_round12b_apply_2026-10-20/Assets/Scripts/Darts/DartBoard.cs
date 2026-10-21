using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The dart board of the Loading Docks (the owner, 2026-10-19 night: the Synty dart board on the wall across from the break room's doorway; "the darts game should work as point and click but there is drop off and how
    /// long you hold is the power of your throw"; the modes are 301 and Cricket). E at the board asks which game; then the crosshair is the aim, holding the left button winds the throw up (<see cref="DartsRules.Power"/>) and
    /// letting go throws: the dart flies in an arc (<see cref="DartsRules.Gravity"/>), so a weak throw falls short and a hard one is a little wilder, and sticks in the board where it lands. While a game is on the left button
    /// throws instead of fighting. The darts themselves are local (a team mate does not see them fly), but every dart's score goes on the little scoreboard over the board, which everybody sees (<see cref="DartScoreboard"/>, <c>RunSync.RequestDartScore</c>). A strip of tape on the floor marks the throw line, 2.37 m from the board; standing at it "[E] Throw darts" brings up the game's screen. The rules are <see cref="DartsRules"/>.
    /// </summary>
    public sealed class DartBoard : Interactable
    {
        // the words (placeholders: the owner writes the game's text)
        public const string PromptPlay = "[E] Throw darts", PromptLeave = "[E] Leave", Title = "DARTS", Name301 = "301", NameCricket = "CRICKET", Bust = "BUST", Miss = "MISS", Finished = "FINISHED", BullText = "BULL", OuterBullText = "OUTER BULL",
                             DoubleText = "DOUBLE", TripleText = "TRIPLE", DartsText = "DARTS", PointsText = "PTS", FinishedHint = "[R] again   [E] leave";

        public enum State { Idle, Choosing, Playing }

        /// <summary>The board that is being played (or asked about).</summary>
        public static DartBoard Current { get; private set; }
        /// <summary>Is a game on? The left button throws darts then (the guns and the fists leave it alone).</summary>
        public static bool Playing => Current != null && Current.state == State.Playing;

        /// <summary>The dart board of the docks (there is one), whether or not a game is on: the network tells it the scores of everybody's darts.</summary>
        public static DartBoard Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = null; Instance = null; }

        /// <summary>How far in front of the model's back the board's face is, and the radius (m) of the scoring area (the double ring's outer edge) on the Synty board, which is 0.7 m across with a black rim.</summary>
        public const float FaceDepth = 0.05f, ScoreRadius = 0.31f;
        /// <summary>The game ends for a player who walks this far from the board.</summary>
        public const float MaxDistance = 14f;
        const int MaxDartsOnBoard = 18;

        State state;
        DartsRules.Match match;
        Vector3 faceCentre, normal, viewerRight;
        bool charging;
        float held;
        string lastText; float lastTextAt = -10f;
        CameraFollow rig;
        float nextRigLookup;

        sealed class Flying { public Transform t; public Vector3 pos, vel; public float age; public bool done; }
        readonly List<Flying> flying = new List<Flying>();
        readonly List<Transform> stuck = new List<Transform>();
        Transform holder;
        // the throw line, the E prompt at it and the little scoreboard on the wall (the owner, 2026-10-19 night)
        Vector3 lineCentre, flatNormal;
        Transform throwLine;
        DartScoreboard scoreboard;
        readonly DartsRules.Scoreboard scores = new DartsRules.Scoreboard();
        bool atLine;
        int lastToggleFrame = -1;
#if UNITY_EDITOR
        [System.NonSerialized] public bool testPressE;          // (a test presses E here: the Input System cannot be driven from a test)
#endif
        /// <summary>The tape's size (m): across the way to the board and along it. The scoreboard hangs this high above the board's middle and this far off the wall.</summary>
        public const float TapeLength = 1.0f, TapeWidth = 0.06f, ScoreboardHeight = 0.88f, ScoreboardOut = 0.03f;

        // for tests and tools
        public State Phase => state;
        public DartsRules.Match Match => match;
        public Vector3 FaceCentre => faceCentre;
        public Vector3 Normal => normal;
        public int DartsInFlight => flying.Count;
        public int DartsOnBoard => stuck.Count;
        public IReadOnlyList<Transform> StuckDarts => stuck;
        public DartsRules.Hit LastHit { get; private set; }
        public bool Charging => charging;
        public float Held => held;
        public DartsRules.Scoreboard Scores => scores;
        public DartScoreboard ScoreboardView => scoreboard;
        public Transform ThrowLine => throwLine;
        public Vector3 LineCentre => lineCentre;
        public bool AtLine => atLine;

        public override string Prompt => state == State.Playing ? PromptLeave : PromptPlay;

        void Start()
        {
            FitAimBoxToChildren();
            reach = 8f;                                   // (a throw is made from a few metres back: the game can be started from there)
            faceCentre = transform.TransformPoint(new Vector3(0f, 0f, FaceDepth));
            normal = transform.forward;
            viewerRight = Vector3.Cross(Vector3.up, -normal).normalized;
            holder = new GameObject("Darts").transform;
            holder.SetParent(transform, true);
            BuildThrowLine();
            BuildScoreboard();
        }

        /// <summary>A strip of tape on the floor across the way to the board, 2.37 m from its face: where a player stands to throw (the owner, 2026-10-19 night: "a line on the ground that looks like tape ... like it would be in the real world").</summary>
        void BuildThrowLine()
        {
            flatNormal = new Vector3(normal.x, 0f, normal.z).normalized;
            lineCentre = new Vector3(faceCentre.x, 0f, faceCentre.z) + flatNormal * DartsRules.ThrowDistance;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DartThrowLine";
            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            var tr = go.transform;
            tr.SetParent(transform, true);
            tr.SetPositionAndRotation(lineCentre + Vector3.up * 0.007f, Quaternion.LookRotation(flatNormal, Vector3.up));       // (its z runs along the way to the board, its x across it)
            var ls = transform.lossyScale;
            tr.localScale = new Vector3(TapeLength / Mathf.Max(0.01f, ls.x), 0.014f / Mathf.Max(0.01f, ls.y), TapeWidth / Mathf.Max(0.01f, ls.z));
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(new Color(0.94f, 0.92f, 0.82f), 0.25f);
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            throwLine = tr;
        }

        /// <summary>The little scoreboard on the wall above the board (the owner, 2026-10-19 night).</summary>
        void BuildScoreboard()
        {
            var centre = transform.TransformPoint(new Vector3(0f, ScoreboardHeight, ScoreboardOut));
            scoreboard = DartScoreboard.Build(transform, centre, normal);
            scoreboard.Show(scores.Lines(), scores.Bust, scores.Won);
        }

        protected override void OnEnable() { base.OnEnable(); Instance = this; }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (Instance == this) Instance = null;
            if (Current == this) { Current = null; OutpostUi.Hide(OutpostUi.Kind.Darts); }
        }

        public override void Use(WobblyPlayer who)
        {
            lastToggleFrame = Time.frameCount;
            if (state == State.Playing) { Leave(); return; }
            if (state != State.Idle) return;
            state = State.Choosing;
            Current = this;
            OutpostUi.Show(OutpostUi.Kind.Darts);
        }

        /// <summary>Starts a game (the chooser's buttons, and tests).</summary>
        public void Begin(DartsRules.Mode mode)
        {
            ClearDarts();
            match = new DartsRules.Match(mode);
            state = State.Playing;
            Current = this;
            charging = false; held = 0f; lastText = null;
            OutpostUi.Hide(OutpostUi.Kind.Darts);
        }

        public void Leave()
        {
            lastToggleFrame = Time.frameCount;
            state = State.Idle;
            charging = false;
            if (Current == this) Current = null;
            OutpostUi.Hide(OutpostUi.Kind.Darts);
        }

        void ClearDarts()
        {
            foreach (var f in flying) if (f.t) Destroy(f.t.gameObject);
            flying.Clear();
            foreach (var t in stuck) if (t) Destroy(t.gameObject);
            stuck.Clear();
        }

        // ------------------------------------------------------------------ the throw

        Transform Body()
        {
            if (!rig && Time.unscaledTime >= nextRigLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextRigLookup = Time.unscaledTime + 0.5f; }
            return rig ? rig.target : null;
        }

        void Update()
        {
            if (state == State.Choosing && OutpostUi.Current != OutpostUi.Kind.Darts)
            {
                state = State.Idle;                       // (Esc closed the screen)
                if (Current == this) Current = null;
            }
            if (state == State.Playing) UpdatePlaying();
            UpdateLine();
            StepDarts(Time.deltaTime);
        }

        /// <summary>
        /// Standing at the throw line (<see cref="DartsRules.AtThrowLine"/>) with no game on, "[E] Throw darts" shows and E brings up the game's screen (the owner, 2026-10-19 night: "When a player stands at the line it should say
        /// throw darts and if they click E it should put them into the throwing screen"). E on the board itself (the crosshair on it) works as before; the two never act in the same frame.
        /// </summary>
        void UpdateLine()
        {
            atLine = false;
            if (state != State.Idle || !isActiveAndEnabled) return;
            var body = Body();
            if (!body) return;
            var flat = body.position - lineCentre; flat.y = 0f;
            if (!DartsRules.AtThrowLine(Vector3.Dot(flat, flatNormal), Vector3.Dot(flat, viewerRight))) return;
            if (EscapeMenu.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || TestMode.PanelOpen || OutpostUi.Open) return;
            body.TryGetComponent(out WobblyPlayer wob);
            if (wob && wob.InCustody) return;
            atLine = true;
            bool e = Input.GetKeyDown(KeyCode.E);
#if UNITY_EDITOR
            if (testPressE) { e = true; testPressE = false; }
#endif
            if (e && lastToggleFrame != Time.frameCount) Use(wob);
        }

        // ------------------------------------------------------------------ the scoreboard on the wall

        static int ThrowerSlot() => Net.NetRole.Synced && Net.NetPlayer.Local != null ? Net.NetPlayer.Local.Slot : 0;

        /// <summary>
        /// A dart landed (this machine's thrower: at once; anybody else's: the host says so, see <c>RunSync.DartScoreRpc</c>): the scoreboard on the wall shows it. <paramref name="dartNo"/> is which of the three darts of the turn it
        /// was (0, 1 or 2).
        /// </summary>
        public void ShowScore(int slot, int dartNo, int score, int turn, int remaining, int points, bool bust, bool won, DartsRules.Mode mode)
        {
            if (!scores.Apply(slot, dartNo, score, turn, remaining, points, bust, won, mode)) return;
            if (scoreboard) scoreboard.Show(scores.Lines(), scores.Bust, scores.Won);
        }

        void UpdatePlaying()
        {
            var body = Body();
            if (!body || (body.position - faceCentre).sqrMagnitude > MaxDistance * MaxDistance) { Leave(); return; }
            bool blocked = EscapeMenu.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || TestMode.PanelOpen || OutpostUi.Open;
            if (blocked) { charging = false; return; }
            if (match.Won)
            {
                charging = false;
                if (Input.GetKeyDown(KeyCode.R)) Begin(match.mode);
                return;
            }
            if (Input.GetMouseButtonDown(0)) { charging = true; held = 0f; }
            if (charging)
            {
                held += Time.unscaledDeltaTime;
                if (Input.GetMouseButtonUp(0)) { charging = false; ThrowFromView(DartsRules.Power(held)); }
            }
        }

        /// <summary>Where the crosshair points on the board's plane, or null.</summary>
        Vector3? AimPointOnBoard(Ray ray)
        {
            float denom = Vector3.Dot(ray.direction, normal);
            if (denom >= -0.001f) return null;
            float t = Vector3.Dot(faceCentre - ray.origin, normal) / denom;
            if (t <= 0f || t > 40f) return null;
            return ray.GetPoint(t);
        }

        void ThrowFromView(float power)
        {
            var cam = Camera.main;
            var body = Body();
            if (!cam || !body) return;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            var aim = AimPointOnBoard(ray) ?? ray.GetPoint(30f);
            Launch(body.position + Vector3.up * 0.38f + body.forward * 0.35f, aim, power, true);
        }

        /// <summary>A dart leaves <paramref name="origin"/> for <paramref name="aim"/> (where it would go with no drop) at the speed of <paramref name="power"/>, in a cone that is wider for a hard throw if <paramref name="spread"/>.</summary>
        public void Launch(Vector3 origin, Vector3 aim, float power, bool spread)
        {
            var dir = (aim - origin).normalized;
            if (spread)
            {
                var off = Random.insideUnitCircle * DartsRules.Spread(power);
                dir = Quaternion.LookRotation(dir) * Quaternion.Euler(off.y, off.x, 0f) * Vector3.forward;
            }
            var dart = MakeDart(holder ? holder : transform);
            dart.position = origin; dart.rotation = Quaternion.LookRotation(dir);
            flying.Add(new Flying { t = dart, pos = origin, vel = dir * DartsRules.Speed(power) });
        }

        void StepDarts(float dt)
        {
            if (flying.Count == 0) return;
            dt = Mathf.Min(dt, 0.1f);
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var f = flying[i];
                float left = dt;
                while (left > 0f && !f.done) { float h = Mathf.Min(left, 0.008f); left -= h; Advance(f, h); }
                if (f.done || !f.t) flying.RemoveAt(i);
            }
        }

        void Advance(Flying f, float h)
        {
            var prev = f.pos;
            f.vel += Vector3.down * DartsRules.Gravity * h;
            var next = prev + f.vel * h;
            float sPrev = Vector3.Dot(prev - faceCentre, normal), sNext = Vector3.Dot(next - faceCentre, normal);
            if (sPrev > 0f && sNext <= 0f) { Land(f, Vector3.Lerp(prev, next, sPrev / Mathf.Max(0.0001f, sPrev - sNext))); return; }
            var seg = next - prev; float len = seg.magnitude;
            if (len > 1e-5f && Physics.Raycast(prev, seg / len, out var rh, len, ~0, QueryTriggerInteraction.Ignore) && !rh.collider.transform.IsChildOf(transform) && rh.rigidbody == null)
            { Stick(f, rh.point, f.vel, null); Result(new DartsRules.Hit(DartsRules.Zone.Miss, 0)); return; }
            if (next.y <= 0.02f) { Stick(f, new Vector3(next.x, 0.02f, next.z), f.vel, null); Result(new DartsRules.Hit(DartsRules.Zone.Miss, 0)); return; }
            f.pos = next;
            f.t.position = next;
            f.t.rotation = Quaternion.LookRotation(f.vel);
            f.age += h;
            if (f.age > 6f) { Destroy(f.t.gameObject); f.done = true; }
        }

        void Land(Flying f, Vector3 p)
        {
            var off = p - faceCentre;
            float x = Vector3.Dot(off, viewerRight) / ScoreRadius, y = Vector3.Dot(off, Vector3.up) / ScoreRadius;
            var hit = DartsRules.Score(x, y);
            if (x * x + y * y > 1.25f * 1.25f)            // past the rim: into the wall behind
            {
                Stick(f, p - normal * FaceDepth, f.vel, null);
                Result(new DartsRules.Hit(DartsRules.Zone.Miss, 0));
                return;
            }
            Stick(f, p, f.vel, transform);
            Result(hit);
        }

        void Stick(Flying f, Vector3 point, Vector3 dir, Transform parent)
        {
            f.done = true;
            if (!f.t) return;
            f.t.position = point;
            f.t.rotation = Quaternion.LookRotation(dir.sqrMagnitude > 1e-6f ? dir : -normal);
            f.t.SetParent(parent ? parent : holder, true);
            stuck.Add(f.t);
            while (stuck.Count > MaxDartsOnBoard) { if (stuck[0]) Destroy(stuck[0].gameObject); stuck.RemoveAt(0); }
        }

        void Result(DartsRules.Hit hit)
        {
            LastHit = hit;
            if (match == null || state != State.Playing) return;
            int dartNo = DartsRules.Match.DartsPerTurn - match.DartsLeft, turn = match.Turn;       // (before the dart is counted: which of the three it is)
            match.Throw(hit);
            lastText = match.LastBust ? Bust : TextOf(hit);
            lastTextAt = Time.time;
            // the scoreboard on the wall: here at once, and the host tells the others (the owner, 2026-10-19 night: "everyone should see the results")
            ShowScore(ThrowerSlot(), dartNo, hit.Score, turn, match.Remaining, match.Points, match.LastBust, match.Won, match.mode);
            if (Net.NetRole.Synced && Net.RunSync.Instance != null)
                Net.RunSync.Instance.RequestDartScore(dartNo, hit.Score, turn, match.Remaining, match.Points, match.LastBust, match.Won, match.mode == DartsRules.Mode.Cricket);
        }

        static string TextOf(DartsRules.Hit h) => h.zone switch
        {
            DartsRules.Zone.Miss => Miss, DartsRules.Zone.Bull => BullText, DartsRules.Zone.OuterBull => OuterBullText,
            DartsRules.Zone.Double => DoubleText + " " + h.number, DartsRules.Zone.Triple => TripleText + " " + h.number, _ => h.number.ToString(),
        };

        // ------------------------------------------------------------------ the dart

        static Material steel, tipMat, flightMat;

        static Material Mat(Color c, float gloss)
        {
            var m = new Material(Shader.Find("Standard")) { color = c, hideFlags = HideFlags.HideAndDontSave };
            m.SetFloat("_Glossiness", gloss);
            return m;
        }

        static void Part(Transform root, PrimitiveType type, Vector3 at, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col) DestroyImmediate(col);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at; go.transform.localRotation = rot; go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        }

        /// <summary>A dart: its tip is the origin and it points along +z; the barrel, the shaft and two crossed flights behind it. A little bigger than a real one so that it can be seen from across the room.</summary>
        static Transform MakeDart(Transform parent)
        {
            if (!steel) { steel = Mat(new Color(0.72f, 0.74f, 0.78f), 0.7f); tipMat = Mat(new Color(0.85f, 0.85f, 0.88f), 0.8f); flightMat = Mat(new Color(0.85f, 0.15f, 0.12f), 0.2f); }
            var root = new GameObject("Dart").transform;
            root.SetParent(parent, false);
            var along = Quaternion.Euler(90f, 0f, 0f);                    // (a cylinder stands along y: turned to lie along z)
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.012f), new Vector3(0.003f, 0.012f, 0.003f), along, tipMat);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.05f), new Vector3(0.011f, 0.05f, 0.011f), along, steel);
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.125f), new Vector3(0.004f, 0.03f, 0.004f), along, steel);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.17f), new Vector3(0.045f, 0.002f, 0.05f), Quaternion.identity, flightMat);
            Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.17f), new Vector3(0.002f, 0.045f, 0.05f), Quaternion.identity, flightMat);
            root.localScale = Vector3.one * 1.6f;
            return root;
        }

        // ------------------------------------------------------------------ the screens

        void OnGUI()
        {
            if (state == State.Choosing) DrawChooser();
            else if (state == State.Playing && Event.current.type == EventType.Repaint) DrawHud();
            else if (atLine && state == State.Idle && Event.current.type == EventType.Repaint)
            {
                var aim = Crosshair.Aim;
                if (!(aim.Kind == AimKind.Interact && aim.Interactable == this)) HudWidgets.Prompt(PromptPlay, Screen.height * 0.62f);          // (aiming at the board, the crosshair shows the same words)
            }
        }

        void DrawChooser()
        {
            MenuKit.Begin();
            float s = HudTheme.S;
            float pw = Mathf.Min(520f * s, Screen.width - 40f * s), ph = 230f * s;
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
            MenuKit.Dim(0.4f);
            MenuKit.Sheet(panel);
            MenuKit.Ribbon(panel, Title, HudTheme.Quota);
            float bw = (pw - 3f * 24f * s) * 0.5f, bh = 74f * s, by = panel.y + 100f * s;
            if (MenuKit.Button(new Rect(panel.x + 24f * s, by, bw, bh), Name301, HudTheme.Info, 34f)) Begin(DartsRules.Mode.Game301);
            if (MenuKit.Button(new Rect(panel.x + 48f * s + bw, by, bw, bh), NameCricket, HudTheme.Good, 30f)) Begin(DartsRules.Mode.Cricket);
        }

        void DrawHud()
        {
            if (match == null) return;
            float s = HudTheme.S;
            bool cricket = match.mode == DartsRules.Mode.Cricket;
            float w = (cricket ? 470f : 330f) * s, h = (cricket ? 128f : 112f) * s;
            var r = new Rect((Screen.width - w) * 0.5f, 104f * s, w, h);
            HudTheme.Plate(r);
            float x = r.x + 12f * s, y = r.y + 8f * s, iw = r.width - 24f * s;
            HudWidgets.ChipAt(x, y + 2f * s, 22f * s, cricket ? NameCricket : Name301, HudTheme.Quota, 13f);
            // the darts of this turn
            for (int i = 0; i < DartsRules.Match.DartsPerTurn; i++)
                HudTheme.Circle(new Vector2(r.xMax - 22f * s - i * 22f * s, y + 13f * s), 16f * s, i < match.DartsLeft ? HudTheme.Quota : HudTheme.Dim);
            if (!cricket)
                HudTheme.Text(new Rect(x, y + 28f * s, iw, 48f * s), match.Remaining.ToString(), 40f, match.Won ? HudTheme.Good : HudTheme.Bright, TextAnchor.MiddleCenter);
            else
            {
                float cw = iw / 7f;
                for (int k = 0; k < 7; k++)
                {
                    string label = DartsRules.CricketTargets[k] == 25 ? "B" : DartsRules.CricketTargets[k].ToString();
                    var cell = new Rect(x + k * cw, y + 28f * s, cw, 22f * s);
                    bool closed = match.marks[k] >= 3;
                    HudTheme.Text(cell, label, 16f, closed ? HudTheme.Good : HudTheme.Bright, TextAnchor.MiddleCenter);
                    for (int m = 0; m < 3; m++)
                        HudTheme.Circle(new Vector2(cell.center.x + (m - 1) * 15f * s, cell.yMax + 12f * s), 11f * s, m < match.marks[k] ? (closed ? HudTheme.Good : HudTheme.Quota) : HudTheme.Dim);
                }
                HudTheme.Text(new Rect(x, y + 74f * s, iw, 22f * s), match.Points + " " + PointsText, 16f, HudTheme.Muted, TextAnchor.MiddleRight);
            }
            // what the last dart did, or the end of the game
            float line = r.yMax - 28f * s;
            if (match.Won) HudTheme.Text(new Rect(x, line, iw, 24f * s), Finished + "  " + match.DartsThrown + " " + DartsText + "    " + FinishedHint, 15f, HudTheme.Good, TextAnchor.MiddleCenter);
            else if (lastText != null && Time.time - lastTextAt < 4f) HudTheme.Text(new Rect(x, line, iw, 24f * s), lastText, 18f, match.LastBust ? HudTheme.Danger : HudTheme.Warning, TextAnchor.MiddleLeft);
            // the throw's power, under the crosshair while the button is held
            if (charging)
            {
                float p = DartsRules.Power(held);
                var bar = new Rect(Screen.width * 0.5f - 90f * s, Screen.height * 0.5f + 70f * s, 180f * s, 14f * s);
                HudTheme.Bar(bar, p, Color.Lerp(HudTheme.Good, HudTheme.Danger, p), 2.5f);
            }
        }
    }
}
