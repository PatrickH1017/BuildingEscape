using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The screen of a guard outpost's computer (E on it, see <see cref="ComputerProp"/>), the owner's request of 2026-10-17: first a log-in (a user name and a password to type), whose
    /// answer is on the post-it stuck to the monitor - the post-it is drawn on the monitor's frame here too, so it is in view while the player types - then, once the right words are in, a page
    /// with a Safes tab: click it and it lists the room number and the code of the safe the computer has a record of (and everything the player has learned so far). What is learned goes
    /// into the journal (J). The cursor is free while the screen is up (<see cref="OutpostUi"/>), the body stands still, and Esc, the button, walking away or any other screen closes it.
    /// Purely local: every player logs in for himself. The wording on the screen is a plain placeholder.
    /// </summary>
    public sealed partial class ComputerScreen : MonoBehaviour
    {
        public static ComputerScreen Instance { get; private set; }

        /// <summary>The player may stand this much farther from the computer than its reach before the screen closes.</summary>
        const float WalkAwaySlack = 1.5f;
        public const int TabNone = 0, TabSafes = 1, TabNav = 2, TabHacked = 3;

        // the door terminal (a few lines under the map on the Nav page, the whole page on the Hacked tab): a log and the line being typed
        readonly List<string> termLog = new List<string>();
        string termInput = "";
        bool paHeld;                                  // V is held at the computer: the PA is live
        static readonly char[] PaKeys = { 'v', 'V' };
        bool focusTerm;

        ComputerProp prop;
        WobblyPlayer player;
        string user = "", pass = "", message = "";
        float messageUntil;
        bool loggedIn, learned, focusUser;
        int tab;

        // ------------------------------------------------------------------ for the tests and the other screens

        public static bool Showing => OutpostUi.Current == OutpostUi.Kind.Computer && Instance != null && Instance.prop != null;
        public static bool LoggedIn => Showing && Instance.loggedIn;
        public static int Tab => Showing ? Instance.tab : TabNone;
        /// <summary>Did opening the Safes tab teach the player something new (so the journal grew)?</summary>
        public static bool LearnedSomething => Showing && Instance.learned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("ComputerScreen");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ComputerScreen>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OutpostUi.Hide(OutpostUi.Kind.Computer);
        }

        // ------------------------------------------------------------------ opening, logging in, tabs, closing

        public static void Open(ComputerProp p, WobblyPlayer who)
        {
            if (Instance == null) Boot();
            if (Instance == null || OutpostUi.Open || !p || !who || who.InCustody || who.IsHidden) return;
            Instance.Begin(p, who);
        }

        void Begin(ComputerProp p, WobblyPlayer who)
        {
            prop = p;
            player = who;
            user = pass = message = "";
            messageUntil = 0f;
            learned = false;
            tab = TabNone;
            loggedIn = SafeIntel.IsLoggedIn(p.outpostFloor);          // (a player who logged in on this floor stays logged in until the run ends)
            focusUser = true;
            OutpostUi.Show(OutpostUi.Kind.Computer);
            Beep(1f, 0.5f);
        }

        void Close()
        {
            OutpostUi.Hide(OutpostUi.Kind.Computer);
            prop = null;
            player = null;
        }

        /// <summary>Types the words in and presses Log in (the tests; the screen's own button does the same). True when the computer let the player in.</summary>
        public static bool TryLogin(string typedUser, string typedPass) => Showing && Instance.Submit(typedUser, typedPass);

        /// <summary>Puts words in the two boxes without pressing Log in (the picture tests).</summary>
        public static void SetTyped(string typedUser, string typedPass) { if (Showing) { Instance.user = typedUser ?? ""; Instance.pass = typedPass ?? ""; } }

        /// <summary>Clicks a tab (the tests; the screen's own button does the same). Only a player who is logged in can.</summary>
        public static void SelectTab(int t) { if (Showing && Instance.loggedIn) Instance.ChooseTab(t); }

        bool Submit(string typedUser, string typedPass)
        {
            if (loggedIn) return true;
            if (ComputerRules.Accepts(prop.Credentials, typedUser, typedPass))
            {
                loggedIn = true;
                SafeIntel.LogIn(prop.outpostFloor);
                user = pass = message = "";
                tab = TabNone;
                Beep(1.3f, 0.5f);
                return true;
            }
            pass = "";
            Say("Incorrect user name or password.", 2.5f);
            Beep(0.7f, 0.5f);
            return false;
        }

        void ChooseTab(int t)
        {
            tab = t;
            if (t == TabSafes) RevealRecord();
            if (t == TabNav || t == TabHacked || t == TabMapTest)
            {
                if (termLog.Count == 0)
                {
                    termLog.Add("SECURITY TERMINAL  -  DOOR CONTROL");
                    termLog.Add("Type a room number: its doors lock, or unlock if they are locked.");
                    termLog.Add("SECURITY is this office's own door (locked, the officers cannot open it).");
                }
                focusTerm = true;
            }
            Beep(1.1f, 0.35f);
        }

        /// <summary>The computer's record of a safe goes into the player's journal the first time he opens the Safes tab (what the note used to do when it was read).</summary>
        void RevealRecord()
        {
            if (prop == null || prop.safeFloor <= 0) return;
            var gen = TowerManager.Instance != null ? TowerManager.Instance.generator : null;
            string room = gen != null ? gen.SafeRoomLabel(prop.safeFloor) : null;
            int code = prop.givesCode ? SafeRules.Code(prop.intelSeed, prop.safeFloor) : 0;
            if (SafeIntel.Add(prop.safeFloor, room, code)) learned = true;
        }

        void LogOut()
        {
            SafeIntel.LogOut(prop.outpostFloor);
            loggedIn = false;
            tab = TabNone;
            user = pass = "";
            focusUser = true;
        }

        void Say(string text, float seconds) { message = text; messageUntil = Time.unscaledTime + seconds; }

        void Beep(float pitch, float volume)
        {
            if (prop) GameAudio.PlaySound(SoundId.KeypadBeep, prop.transform.position + Vector3.up * 0.3f, volume, pitch, true, 10f, 2f);
        }

        static WobblyPlayer LocalPlayer()
        {
            var np = Net.NetPlayer.Local;
            if (np && np.TryGetComponent(out WobblyPlayer w)) return w;
            return WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null;
        }

        static bool OtherScreenUp => Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open || TestMode.PanelOpen;

        /// <summary>
        /// Holding V while signed in at the computer is the PA (the owner, 2026-10-18: "click V to talk into"): the host / this machine is told when the key goes down and when it comes up (or the screen goes away),
        /// and while it is down the speakers that are switched on carry the voice (<see cref="PaState"/>, <see cref="PaRules"/>).
        /// </summary>
        void UpdatePa()
        {
            bool want = OutpostUi.Current == OutpostUi.Kind.Computer && loggedIn && Input.GetKey(KeyCode.V) && !OtherScreenUp;
            if (want == paHeld) return;
            paHeld = want;
            PaState.RequestLive(want);
        }

        /// <summary>For the tests: the PA key held down or let go, as the keyboard would.</summary>
        public static void TestHoldPa(bool on)
        {
            if (Instance == null) return;
            Instance.paHeld = on;
            PaState.RequestLive(on);
        }

        /// <summary>The screen goes away late in the frame (see IntelScreens): nothing that answers E or Esc has had its say yet.</summary>
        void LateUpdate()
        {
            UpdatePa();
            if (OutpostUi.Current != OutpostUi.Kind.Computer) { prop = null; return; }
            var me = player ? player : LocalPlayer();
            bool far = prop != null && me != null && prop.DistanceFrom(me.transform.position) > prop.reach + WalkAwaySlack;
            bool leave = Input.GetKeyDown(KeyCode.Escape) || OtherScreenUp || !me || me.InCustody || me.IsHidden || prop == null || far;
            if (leave) Close();
        }

        // ------------------------------------------------------------------ drawing

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static GUIStyle textStyle, fieldStyle, buttonStyle;

        static GUIStyle Text(int size, TextAnchor anchor, FontStyle fontStyle, Color colour)
        {
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { wordWrap = false, richText = false, clipping = TextClipping.Overflow };
            textStyle.fontSize = size; textStyle.alignment = anchor; textStyle.fontStyle = fontStyle; textStyle.normal.textColor = colour;
            return textStyle;
        }

        static void Label(Rect r, string text, int size, TextAnchor anchor, FontStyle fontStyle, Color colour) => GUI.Label(r, text, Text(size, anchor, fontStyle, colour));

        static GUIStyle Field(int size)
        {
            if (fieldStyle == null)
            {
                fieldStyle = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 14, 4, 4) };
                fieldStyle.normal.background = fieldStyle.focused.background = fieldStyle.hover.background = fieldStyle.active.background = null;
                fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = fieldStyle.active.textColor = new Color(0.92f, 0.97f, 1f);
            }
            fieldStyle.fontSize = size;
            return fieldStyle;
        }

        static GUIStyle Button(int size)
        {
            if (buttonStyle == null) buttonStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
            buttonStyle.fontSize = size;
            return buttonStyle;
        }

        static readonly Color Bezel = new Color(0.055f, 0.060f, 0.070f), Desktop = new Color(0.075f, 0.090f, 0.125f), Bar = new Color(0.050f, 0.060f, 0.085f);
        static readonly Color Light = new Color(0.93f, 0.95f, 0.98f), Dim = new Color(0.62f, 0.67f, 0.76f);

        void OnGUI()
        {
            if (OutpostUi.Current != OutpostUi.Kind.Computer || prop == null) return;
            GUI.depth = -100;
            MenuKit.Begin();
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float w = Mathf.Min(1160f * s, Screen.width - 60f), h = w * 0.74f;
            var mon = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f - 24f * s, w, h);
            Fill(new Rect(mon.center.x - 70f * s, mon.yMax - 4f * s, 140f * s, 34f * s), Bezel);                        // the stand
            Fill(new Rect(mon.center.x - 190f * s, mon.yMax + 26f * s, 380f * s, 14f * s), Bezel);
            Fill(new Rect(mon.x - 5f * s, mon.y - 5f * s, mon.width + 10f * s, mon.height + 10f * s), new Color(0f, 0f, 0f, 0.55f));
            Fill(mon, Bezel);
            var scr = new Rect(mon.x + 26f * s, mon.y + 22f * s, mon.width - 52f * s, mon.height - 44f * s);
            Fill(scr, Desktop);

            var bar = new Rect(scr.x, scr.y, scr.width, 56f * s);
            Fill(bar, Bar);
            Label(new Rect(bar.x + 20f * s, bar.y, bar.width * 0.62f, bar.height), "SECURITY TERMINAL  -  FLOOR " + prop.outpostFloor, Mathf.RoundToInt(26 * s), TextAnchor.MiddleLeft, FontStyle.Bold, Light);
            if (loggedIn)
            {
                Label(new Rect(bar.xMax - 420f * s, bar.y, 250f * s, bar.height), prop.Credentials.user, Mathf.RoundToInt(22 * s), TextAnchor.MiddleRight, FontStyle.Normal, Dim);
                if (MenuKit.Button(new Rect(bar.xMax - 160f * s, bar.y + 10f * s, 140f * s, 36f * s), "LOG OUT", HudTheme.Info, 16f)) LogOut();
            }

            if (!loggedIn) DrawLogin(scr, bar, s); else DrawDesktop(scr, bar, s);
            if (!loggedIn) DrawPostIt(mon, s);                     // (the post-it comes off the monitor once the player is in: the Nav legend needs the room)
            if (MenuKit.Button(new Rect(mon.xMax - 200f * s, mon.yMax + 10f * s, 190f * s, 40f * s), "CLOSE  (ESC)", HudTheme.Info, 17f)) Close();
        }

        void DrawLogin(Rect scr, Rect bar, float s)
        {
            var ev = Event.current;
            bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            float x = scr.x + scr.width * 0.07f, y = bar.yMax + 56f * s, fw = Mathf.Min(560f * s, scr.width * 0.55f), fh = 56f * s;
            Label(new Rect(x, y, fw, 34f * s), "User name", Mathf.RoundToInt(22 * s), TextAnchor.LowerLeft, FontStyle.Normal, Dim);
            var userBox = new Rect(x, y + 38f * s, fw, fh);
            Fill(userBox, new Color(0.02f, 0.04f, 0.08f)); Fill(new Rect(userBox.x, userBox.yMax - 3f * s, userBox.width, 3f * s), HudTheme.Quota);
            GUI.SetNextControlName("cs_user");
            user = GUI.TextField(userBox, user, 16, Field(Mathf.RoundToInt(28 * s)));
            Label(new Rect(x, y + 118f * s, fw, 34f * s), "Password", Mathf.RoundToInt(22 * s), TextAnchor.LowerLeft, FontStyle.Normal, Dim);
            var passBox = new Rect(x, y + 156f * s, fw, fh);
            Fill(passBox, new Color(0.02f, 0.04f, 0.08f)); Fill(new Rect(passBox.x, passBox.yMax - 3f * s, passBox.width, 3f * s), HudTheme.Quota);
            GUI.SetNextControlName("cs_pass");
            pass = GUI.PasswordField(passBox, pass, '*', 16, Field(Mathf.RoundToInt(28 * s)));
            if (enter) { Submit(user, pass); ev.Use(); }
            if (MenuKit.Button(new Rect(x, y + 240f * s, 240f * s, 58f * s), "LOG IN", HudTheme.Cash, 26f)) Submit(user, pass);
            if (Time.unscaledTime < messageUntil)
                Label(new Rect(x, y + 306f * s, fw + 200f * s, 36f * s), message, Mathf.RoundToInt(22 * s), TextAnchor.MiddleLeft, FontStyle.Bold, new Color(1f, 0.45f, 0.40f));
            if (focusUser && ev.type == EventType.Repaint) { GUI.FocusControl("cs_user"); focusUser = false; }
        }

        void DrawDesktop(Rect scr, Rect bar, float s)
        {
            var strip = new Rect(scr.x + 20f * s, bar.yMax + 16f * s, 190f * s, 50f * s);
            string[] names = { "Safes", "Nav", "Hacked", "Map test" };
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(strip.x + i * (strip.width + 14f * s), strip.y, strip.width, strip.height);
                if (MenuKit.Pick(b, names[i].ToUpperInvariant(), tab == i + 1, HudTheme.Quota, 22f)) ChooseTab(i + 1);
            }
            var body = new Rect(scr.x + 24f * s, strip.yMax + 22f * s, scr.width - 48f * s, scr.yMax - strip.yMax - 40f * s);
            if (tab == TabNone)
            {
                Label(body, "Select a tab.", Mathf.RoundToInt(24 * s), TextAnchor.UpperLeft, FontStyle.Italic, Dim);
                return;
            }
            if (tab == TabNav) { DrawNavPage(body, s); return; }
            if (tab == TabMapTest) { DrawMapTestPage(body, s); return; }
            if (tab == TabHacked) { DrawTerminal(body, s); return; }

            // the Safes tab: floor, room and code of every safe the player knows about
            float c1 = body.x, c2 = body.x + body.width * 0.22f, c3 = body.x + body.width * 0.50f;
            int size = Mathf.RoundToInt(26 * s);
            Fill(new Rect(body.x, body.y, body.width * 0.62f, 38f * s), Bar);
            Label(new Rect(c1 + 12f * s, body.y, 200f * s, 38f * s), "FLOOR", Mathf.RoundToInt(20 * s), TextAnchor.MiddleLeft, FontStyle.Bold, Dim);
            Label(new Rect(c2 + 12f * s, body.y, 200f * s, 38f * s), "ROOM", Mathf.RoundToInt(20 * s), TextAnchor.MiddleLeft, FontStyle.Bold, Dim);
            Label(new Rect(c3 + 12f * s, body.y, 300f * s, 38f * s), "CODE", Mathf.RoundToInt(20 * s), TextAnchor.MiddleLeft, FontStyle.Bold, Dim);
            var entries = SafeIntel.Entries;
            float y = body.y + 46f * s;
            if (entries.Count == 0)
            {
                Label(new Rect(c1 + 12f * s, y, body.width, 40f * s), "No safe records.", size, TextAnchor.MiddleLeft, FontStyle.Italic, Dim);
                return;
            }
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if ((i & 1) == 0) Fill(new Rect(body.x, y, body.width * 0.62f, 44f * s), new Color(1f, 1f, 1f, 0.05f));
                Label(new Rect(c1 + 12f * s, y, 200f * s, 44f * s), e.floor.ToString(), size, TextAnchor.MiddleLeft, FontStyle.Bold, Light);
                Label(new Rect(c2 + 12f * s, y, 200f * s, 44f * s), string.IsNullOrEmpty(e.room) ? "not on record" : e.room, size, TextAnchor.MiddleLeft, FontStyle.Bold, Light);
                Label(new Rect(c3 + 12f * s, y, 400f * s, 44f * s), e.KnowsCode ? SafeRules.CodeText(e.code) : "not on record (needs a crowbar)", size, TextAnchor.MiddleLeft, FontStyle.Bold,
                      e.KnowsCode ? new Color(0.55f, 0.95f, 0.60f) : new Color(1f, 0.80f, 0.45f));
                y += 48f * s;
            }
            if (learned) Label(new Rect(c1, y + 10f * s, body.width, 30f * s), "Added to your journal  (J)", Mathf.RoundToInt(20 * s), TextAnchor.MiddleLeft, FontStyle.Italic, new Color(0.55f, 0.95f, 0.60f));
        }

        // ------------------------------------------------------------------ the Nav tab: a 2D map of the floor with everybody on it

        static readonly Color MapCorridor = new Color(0.16f, 0.22f, 0.32f), MapRoom = new Color(0.24f, 0.34f, 0.50f), MapSuite = new Color(0.85f, 0.68f, 0.18f),
                              MapServer = new Color(0.25f, 0.55f, 0.95f), MapOutpost = new Color(0.80f, 0.22f, 0.20f), MapLocked = new Color(1f, 0.25f, 0.2f),
                              MapElevator = new Color(0.30f, 0.90f, 0.85f), MapGuardLift = new Color(0.95f, 0.55f, 0.20f), MapStairs = new Color(0.85f, 0.85f, 0.35f);

        static readonly Color MapSpeaker = new Color(0.78f, 0.80f, 0.84f), MapSpeakerLive = new Color(0.35f, 0.95f, 0.45f), MapSpeakerOff = new Color(0.30f, 0.12f, 0.12f);

        /// <summary>The Nav page (the owner, 2026-10-18): the map with its legend, and under it a small terminal - two lines of log and the line being typed - so the doors can be locked and unlocked while the map is in view.</summary>
        void DrawNavPage(Rect body, float s)
        {
            float termH = 106f * s, gap = 8f * s;
            DrawNav(new Rect(body.x, body.y, body.width, body.height - termH - gap), s);
            DrawTerminal(new Rect(body.x, body.yMax - termH, body.width, termH), s, 17f);
        }

        void DrawNav(Rect body, float s)
        {
            var dir = FloorDirectory.Current(prop.outpostFloor);
            if (dir == null || dir.all.Count == 0) { Label(body, "No map of this floor.", Mathf.RoundToInt(24 * s), TextAnchor.UpperLeft, FontStyle.Italic, Dim); return; }
            var map = new Rect(body.x, body.y, body.width * 0.78f, body.height);
            Fill(map, new Color(0.02f, 0.04f, 0.08f));
            var ex = dir.extent;
            float k = Mathf.Min((map.width - 16f * s) / Mathf.Max(1f, ex.W), (map.height - 16f * s) / Mathf.Max(1f, ex.D));
            float ox = map.x + (map.width - ex.W * k) * 0.5f, oy = map.y + (map.height - ex.D * k) * 0.5f;
            // (world z grows up the map: the screen y runs the other way)
            Vector2 P(float x, float z) => new Vector2(ox + (x - ex.x0) * k, oy + (ex.z1 - z) * k);
            Rect R(Rc c) { var a = P(c.x0, c.z1); return new Rect(a.x, a.y, c.W * k, c.D * k); }

            foreach (var ri in dir.all)
            {
                var col = ri.theme == "outpost" ? MapOutpost : ri.role == "suite" ? MapSuite : ri.role == "server" ? MapServer : ri.kind == 0 ? MapRoom : MapCorridor;
                var rr = R(ri.bounds);
                Fill(new Rect(rr.x + 1f, rr.y + 1f, rr.width - 2f, rr.height - 2f), col);
            }
            int small = Mathf.Max(9, Mathf.RoundToInt(13 * s));
            foreach (var room in dir.rooms)
            {
                var rr = R(room.bounds);
                if (rr.width > small * 2.2f && rr.height > small * 1.4f)
                    Label(rr, room.label, small, TextAnchor.MiddleCenter, FontStyle.Bold, room.role == "suite" ? new Color(0.15f, 0.1f, 0f) : Light);
            }
            // the elevators (the players' one and the guards' own) and the two stairwells: a labelled tile on the map
            var gen = TowerManager.Instance != null ? TowerManager.Instance.generator : null;
            void Tile(Vector3 world, string letter, Color c)
            {
                var p = P(world.x, world.z);
                float z = 20f * s;
                Fill(new Rect(p.x - z * 0.5f - 1.5f, p.y - z * 0.5f - 1.5f, z + 3f, z + 3f), Color.black);
                Fill(new Rect(p.x - z * 0.5f, p.y - z * 0.5f, z, z), c);
                Label(new Rect(p.x - z, p.y - z * 0.5f, z * 2f, z), letter, Mathf.RoundToInt(13 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Color.black);
            }
            if (gen != null)
            {
                Tile(gen.PlayerElevatorCentre, "E", MapElevator);
                if (gen.HasGuardShaft) Tile(gen.GuardShaftCentre, "G", MapGuardLift);
                for (int i = 0; i < gen.StairCount; i++) Tile(gen.StairCentre(i), "S", MapStairs);
            }
            var doors = DoorManager.Active;
            if (doors != null)
                for (int i = 0; i < doors.Count; i++)
                {
                    var d = doors.Get(i);
                    if (d == null || !d.ComputerLocked) continue;
                    var p = P(d.Centre.x, d.Centre.z);
                    Fill(new Rect(p.x - 4f * s, p.y - 4f * s, 8f * s, 8f * s), MapLocked);
                }
            // the hallway loudspeakers (the owner, 2026-10-18): a grey tile with an M; a click switches one off or on, and the ones that are on are the ones the PA sounds from
            var clickEv = Event.current;
            for (int i = 0; i < PaState.Speakers.Count; i++)
            {
                var sp = PaState.Speakers[i];
                var spp = P(sp.x, sp.y);
                float sz = 15f * s;
                bool on = PaRules.IsOn(PaState.Mask, i);
                var tile = new Rect(spp.x - sz * 0.5f, spp.y - sz * 0.5f, sz, sz);
                Fill(new Rect(tile.x - 1.5f, tile.y - 1.5f, sz + 3f, sz + 3f), Color.black);
                Fill(tile, on ? (PaState.Live ? MapSpeakerLive : MapSpeaker) : MapSpeakerOff);
                Label(tile, "M", Mathf.RoundToInt(11 * s), TextAnchor.MiddleCenter, FontStyle.Bold, on ? Color.black : Color.white);
                if (clickEv.type == EventType.MouseDown && clickEv.button == 0 && tile.Contains(clickEv.mousePosition)) { PaState.RequestMask(PaRules.Toggle(PaState.Mask, i)); clickEv.Use(); }
            }
            // every officer: a red square; every player: a coloured disc with a number
            foreach (var g in GuardAI.All)
            {
                if (!g) continue;
                var p = P(g.transform.position.x, g.transform.position.z);
                float z = 11f * s;
                Fill(new Rect(p.x - z * 0.5f, p.y - z * 0.5f, z, z), new Color(0.95f, 0.15f, 0.12f));
                Label(new Rect(p.x - z, p.y - z * 0.5f, z * 2f, z), "O", Mathf.RoundToInt(10 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Color.white);
            }
            int order = 0;
            foreach (var wp in WobblyPlayerRegistry.All)
            {
                order++;
                if (!wp) continue;
                int n = wp.TryGetComponent(out Net.NetPlayer slotOf) && slotOf.IsSpawned ? slotOf.Slot + 1 : order;       // (the same "Player n" the vendor and the cutscene use)
                var p = P(wp.transform.position.x, wp.transform.position.z);
                float z = 16f * s;
                var c = HudTheme.PlayerColour(n);
                Fill(new Rect(p.x - z * 0.5f, p.y - z * 0.5f, z, z), new Color(0f, 0f, 0f, 0.7f));
                Fill(new Rect(p.x - z * 0.5f + 2f, p.y - z * 0.5f + 2f, z - 4f, z - 4f), c);
                Label(new Rect(p.x - z, p.y - z * 0.5f, z * 2f, z), n.ToString(), Mathf.RoundToInt(11 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Color.black);
                Label(new Rect(p.x - 60f * s, p.y + z * 0.5f, 120f * s, 18f * s), "Player " + n, Mathf.RoundToInt(11 * s), TextAnchor.UpperCenter, FontStyle.Bold, c);
            }

            // legend: every swatch is drawn like its tile on the map, the letter on it too, so a player can tell which square is the elevator (small rows: the terminal takes the bottom of the page)
            float lx = map.xMax + 14f * s, ly = body.y;
            float legendW = body.xMax - lx, row = 22f * s, sq = 17f * s;
            int ls = Mathf.RoundToInt(14 * s);
            void Key(Color c, string text, string glyph = null, Color? glyphColour = null)
            {
                var sw = new Rect(lx, ly + 2f * s, sq, sq);
                if (glyph != null) Fill(new Rect(sw.x - 1.5f, sw.y - 1.5f, sw.width + 3f, sw.height + 3f), Color.black);
                Fill(sw, c);
                if (glyph != null) Label(sw, glyph, Mathf.RoundToInt(12 * s), TextAnchor.MiddleCenter, FontStyle.Bold, glyphColour ?? Color.black);
                Label(new Rect(lx + sq + 10f * s, ly, legendW - sq - 10f * s, row), text, ls, TextAnchor.MiddleLeft, FontStyle.Normal, Light);
                ly += row;
            }
            Key(MapRoom, "Room"); Key(MapCorridor, "Corridor"); Key(MapSuite, "Executive suite"); Key(MapServer, "Server room"); Key(MapOutpost, "Security office");
            Key(MapElevator, "Elevator", "E"); Key(MapGuardLift, "Guards' elevator", "G"); Key(MapStairs, "Stairwell", "S");
            Key(new Color(0.95f, 0.15f, 0.12f), "Officer", "O", Color.white); Key(HudTheme.PlayerColour(1), "Player", "1"); Key(MapLocked, "Locked door");
            Key(MapSpeaker, "Speaker (click: on / off)", "M");
            ly += 4f * s;
            if (GUI.Button(new Rect(lx, ly, legendW, 26f * s), "SELECT ALL SPEAKERS", Button(Mathf.RoundToInt(12 * s)))) PaState.RequestMask(PaRules.MaskOf(PaState.Speakers.Count));
            ly += 30f * s;
            if (GUI.Button(new Rect(lx, ly, legendW, 26f * s), "DESELECT ALL", Button(Mathf.RoundToInt(12 * s)))) PaState.RequestMask(0UL);
            ly += 32f * s;
            Label(new Rect(lx, ly, legendW + 20f * s, 22f * s), paHeld ? "ON AIR" : "Hold V to speak through the speakers", Mathf.RoundToInt((paHeld ? 18 : 12) * s), TextAnchor.MiddleLeft, paHeld ? FontStyle.Bold : FontStyle.Normal, paHeld ? MapSpeakerLive : Dim);
        }
        // ------------------------------------------------------------------ the door terminal: under the Nav map (a few lines) and on the Hacked tab (the whole page)

        /// <summary>
        /// The terminal: the last lines of the log that fit, then the line being typed. A room number and Enter switch its doors the other way (see <see cref="TermEnter"/>).
        /// <paramref name="fontDesign"/> is the size of the lettering on a 1080-line screen.
        /// </summary>
        void DrawTerminal(Rect body, float s, float fontDesign = 22f)
        {
            var ev = Event.current;
            Fill(body, new Color(0.01f, 0.03f, 0.01f));
            int size = Mathf.RoundToInt(fontDesign * s);
            var green = new Color(0.35f, 1f, 0.45f);
            float lineH = fontDesign * 1.36f * s, inputH = fontDesign * 2f * s, pad = 8f * s;
            int maxLines = Mathf.Max(0, Mathf.FloorToInt((body.height - inputH - pad * 3f) / lineH));
            int first = Mathf.Max(0, termLog.Count - maxLines);
            float y = body.y + pad;
            for (int i = first; i < termLog.Count; i++) { Label(new Rect(body.x + 16f * s, y, body.width - 32f * s, lineH), termLog[i], size, TextAnchor.MiddleLeft, FontStyle.Normal, green); y += lineH; }
            const string prompt = "ROOM NUMBER > ";
            var line = new Rect(body.x + 16f * s, body.yMax - inputH - pad, body.width - 32f * s, inputH);
            Fill(line, new Color(0.02f, 0.08f, 0.02f));
            float pw = size * 0.75f * prompt.Length + 12f * s;
            Label(new Rect(line.x + 8f * s, line.y, pw, line.height), prompt, size, TextAnchor.MiddleLeft, FontStyle.Bold, green);
            bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            GUI.SetNextControlName("cs_term");
            termInput = GUI.TextField(new Rect(line.x + 8f * s + pw, line.y, line.width - pw - 16f * s, line.height), termInput, 24, Field(size));
            if (termInput.IndexOfAny(PaKeys) >= 0) termInput = termInput.Replace("v", "").Replace("V", "");        // (V is the PA key: it types nothing here)
            if (enter) { TermEnter(); ev.Use(); }
            if (focusTerm && ev.type == EventType.Repaint) { GUI.FocusControl("cs_term"); focusTerm = false; }
        }

        void TermLine(string text) { termLog.Add(text); if (termLog.Count > 60) termLog.RemoveAt(0); }

        /// <summary>
        /// One typed line (the owner, 2026-10-18): a room number on its own switches its doors the other way - locked doors unlock, open ones lock - and so do SECURITY and OUTPOST for this office's own door.
        /// LOCK or UNLOCK with it still works ("507 unlock", "lock 507") for anybody who wants to be sure which way it goes (capitals do not matter).
        /// </summary>
        void TermEnter()
        {
            string typed = (termInput ?? "").Trim();
            termInput = "";
            focusTerm = true;
            if (typed.Length == 0) return;
            TermLine("> " + typed);
            FloorDirectory.ParseTerminal(typed, out var room, out var verb);               // ("lock 507" works as well as "507 lock", and SECURITY is this office's own door)
            if (string.IsNullOrEmpty(room)) return;
            RunDoor(room, verb);
        }

        void RunDoor(string room, string verb)
        {
            var doors = DoorManager.Active;
            if (!ComputerRules.WantLocked(verb, doors != null && doors.ComputerIsLocked(prop.outpostFloor, room), out bool lockIt)) { TermLine("UNKNOWN COMMAND - JUST TYPE THE ROOM NUMBER"); Beep(0.7f, 0.4f); return; }
            if (doors == null || !doors.ComputerLock(prop.outpostFloor, room, lockIt)) { TermLine("NO SUCH ROOM"); Beep(0.7f, 0.4f); return; }
            TermLine((FloorDirectory.IsOutpostName(room) ? "SECURITY OFFICE" : "ROOM " + room.ToUpperInvariant()) + (lockIt ? " LOCKED" : " UNLOCKED"));
            Beep(lockIt ? 0.8f : 1.3f, 0.5f);
        }
        /// <summary>For the tests: types a line into the terminal as the player would.</summary>
        public static void TypeTerminal(string line) { if (Showing && Instance.loggedIn && (Instance.tab == TabHacked || Instance.tab == TabNav || Instance.tab == TabMapTest)) { Instance.termInput = line; Instance.TermEnter(); } }
        public static IReadOnlyList<string> TerminalLog => Showing ? Instance.termLog : null;

        /// <summary>The size of the post-it's big lettering: 36 for a short word, smaller for a long one so it stays on the paper.</summary>
        static int PostItSize(string text, float s) => Mathf.RoundToInt(Mathf.Min(36f, 205f / (0.65f * Mathf.Max(1, text.Length))) * s);

        /// <summary>The post-it on the monitor's frame, with the user name and the password on it, in the lower right corner: it stays in view whatever the screen shows.</summary>
        void DrawPostIt(Rect mon, float s)
        {
            float size = 250f * s;
            var note = new Rect(mon.xMax - size * 0.86f, mon.yMax - size * 0.84f, size, size * 0.90f);
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(-4f, note.center);
            Fill(new Rect(note.x + 6f * s, note.y + 8f * s, note.width, note.height), new Color(0f, 0f, 0f, 0.35f));
            Fill(note, new Color(1f, 0.91f, 0.38f));
            Fill(new Rect(note.x, note.y, note.width, 12f * s), new Color(0.93f, 0.80f, 0.25f));
            var ink = new Color(0.14f, 0.12f, 0.30f);
            Label(new Rect(note.x + 18f * s, note.y + 18f * s, note.width - 30f * s, 28f * s), "USER", Mathf.RoundToInt(18 * s), TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.45f, 0.38f, 0.12f));
            Label(new Rect(note.x + 18f * s, note.y + 42f * s, note.width - 30f * s, 46f * s), prop.Credentials.user, PostItSize(prop.Credentials.user, s), TextAnchor.MiddleLeft, FontStyle.Bold, ink);
            Label(new Rect(note.x + 18f * s, note.y + 100f * s, note.width - 30f * s, 28f * s), "PASS", Mathf.RoundToInt(18 * s), TextAnchor.MiddleLeft, FontStyle.Normal, new Color(0.45f, 0.38f, 0.12f));
            Label(new Rect(note.x + 18f * s, note.y + 124f * s, note.width - 30f * s, 46f * s), prop.Credentials.pass, PostItSize(prop.Credentials.pass, s), TextAnchor.MiddleLeft, FontStyle.Bold, ink);
            GUI.matrix = saved;
        }
    }
}
