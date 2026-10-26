using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The two paper screens of the guard outposts: a note that was picked up from a desk (read with E, it stays where it was) and the
    /// journal (J) that lists everything the player has learned from notes: where the floor safes are and, if a note gave it, their codes
    /// (<see cref="SafeIntel"/>). The cursor is free while one is up (<see cref="OutpostUi"/>); Esc, the same key again or the button closes it.
    /// Purely local.
    /// </summary>
    public sealed class IntelScreens : MonoBehaviour
    {
        public static IntelScreens Instance { get; private set; }

        public KeyCode journalKey = KeyCode.J;

        int openedFrame = -1;           // (the frame a screen came up in: the key that opened it must not also close it)
        int journalTab;                 // (0 the safe notes, 1 the checklist: the owner, 2026-10-19)
        /// <summary>Which tab of the journal is up (for tests).</summary>
        public int JournalTab { get => journalTab; set => journalTab = value; }

        /// <summary>Opens the journal on its checklist (the owner, 2026-10-24: "the Checklist tab should be first ... when clicking J it should open directly to the checklist").</summary>
        public void OpenJournal()
        {
            openedFrame = Time.frameCount;
            journalTab = 1;
            OutpostUi.Show(OutpostUi.Kind.Journal);
        }
        readonly System.Collections.Generic.List<SafeIntel.Entry> journalShown = new System.Collections.Generic.List<SafeIntel.Entry>();      // the records of the safes of the floor the player is on

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("IntelScreens");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<IntelScreens>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OutpostUi.Hide(OutpostUi.Kind.Journal);
        }

        // ------------------------------------------------------------------ opening

        static WobblyPlayer LocalPlayer()
        {
            var np = Net.NetPlayer.Local;
            if (np && np.TryGetComponent(out WobblyPlayer w)) return w;
            return WobblyPlayerRegistry.All.Count > 0 ? WobblyPlayerRegistry.All[0] : null;
        }

        static bool OtherScreenUp => Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open || TestMode.PanelOpen;

        void Update()
        {
            if (Input.GetKeyDown(journalKey) && !OutpostUi.Open && !OtherScreenUp)
            {
                var me = LocalPlayer();
                if (me && !me.InCustody && !me.IsHidden)
                {
                    OpenJournal();
                }
            }
        }

        /// <summary>
        /// A screen goes away here, late in the frame: everything that answers E (the crosshair, the doors, picking up) has had its Update by
        /// now and found the screen still up, so the E that puts a note down cannot also open the door or note the crosshair is on.
        /// </summary>
        void LateUpdate()
        {
            var current = OutpostUi.Current;
            if (current != OutpostUi.Kind.Journal) return;

            var me = LocalPlayer();
            bool fresh = Time.frameCount == openedFrame;       // (the key that opened a screen must not also close it)
            bool leave = Input.GetKeyDown(KeyCode.Escape)
                         || (!fresh && Input.GetKeyDown(KeyCode.E))
                         || (!fresh && current == OutpostUi.Kind.Journal && Input.GetKeyDown(journalKey))
                         || OtherScreenUp || !me || me.InCustody || me.IsHidden;
            if (leave) OutpostUi.Hide(current);
        }

        // ------------------------------------------------------------------ drawing

        void OnGUI()
        {
            var current = OutpostUi.Current;
            if (current == OutpostUi.Kind.Journal) DrawJournal();
            else DrawJournalHint(current);
        }

        /// <summary>"Press J for journal" at the bottom left (the owner, 2026-10-24) while a floor is being played and no screen is up.</summary>
        void DrawJournalHint(OutpostUi.Kind current)
        {
            if (Event.current.type != EventType.Repaint || current != OutpostUi.Kind.None || OtherScreenUp) return;
            var hm = HeistManager.Instance;
            if (hm == null || hm.Floor < 1 || IntroCinematic.Playing) return;
            float s = HudTheme.S;
            HudTheme.Text(new Rect(18f * s, Screen.height - 46f * s, 360f * s, 30f * s), "Press J for journal", 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
        }

        void DrawJournal()
        {
            MenuKit.Begin();
            SafeIntel.OnFloor(HeistManager.Instance != null ? HeistManager.Instance.Floor : 0, journalShown);          // (the floor he is on and no other: the owner, 2026-10-18)
            var entries = journalShown;
            float s = HudTheme.S;
            float w = 760f * s;
            bool checklist = journalTab == 1;
            int rowsShown = checklist ? ChecklistRules.Count : Mathf.Max(1, entries.Count);
            float h = Mathf.Min(Screen.height - 80f, (276f + rowsShown * 46f) * s);
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            if (Event.current.type == EventType.Repaint)
            {
                MenuKit.Dim(0.35f);
                HudTheme.Panel(r, 16f);
                HudTheme.Text(new Rect(r.x + 28f * s, r.y + 18f * s, w - 56f * s, 40f * s), checklist ? "CHECKLIST  -  THIS FLOOR" : "NOTES  -  FLOOR SAFES", 26f, HudTheme.Bright, TextAnchor.MiddleLeft);
                HudTheme.Rounded(new Rect(r.x + 30f * s, r.y + 62f * s, 56f * s, Mathf.Max(3f, 4f * s)), HudTheme.Quota, 1f);
                float y = r.y + 126f * s;
                if (checklist) DrawChecklist(r, y, s);
                else if (entries.Count == 0)
                {
                    MenuKit.Wrapped(new Rect(r.x + 28f * s, y, w - 56f * s, 110f * s),
                        "Nothing yet. Every floor has a safe. The guard outposts (the rooms with a long dark window, locked with a keypad) " +
                        "keep a record of the floor's safes on their computer: hack the lock, then log in at the computer (the log-in is on a post-it on its monitor).", 20f * s, HudTheme.Muted);
                }
                else
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        var row = new Rect(r.x + 24f * s, y, w - 48f * s, 42f * s);
                        MenuKit.Plate(row, 0.22f);
                        HudTheme.Text(new Rect(row.x + 14f * s, row.y, row.width - 28f * s, row.height), SafeNotes.JournalLine(e.floor, e.index, e.count, e.room, e.code), 21f,
                            e.KnowsCode ? HudTheme.Cash : HudTheme.Quota, TextAnchor.MiddleLeft, false);
                        y += 46f * s;
                    }
                    MenuKit.Wrapped(new Rect(r.x + 28f * s, y + 8f * s, w - 56f * s, 70f * s), "A safe with no code is pried open with a crowbar (the crowbar may snap).", 18f * s, HudTheme.Dim);
                }
            }
            // the two tabs (a button answers in every kind of event, so they sit outside the paint)
            if (MenuKit.Button(new Rect(r.x + 226f * s, r.y + 74f * s, 190f * s, 36f * s), "SAFE NOTES", checklist ? HudTheme.Dim : HudTheme.Info, 16f)) journalTab = 0;
            if (MenuKit.Button(new Rect(r.x + 28f * s, r.y + 74f * s, 190f * s, 36f * s), "CHECKLIST", checklist ? HudTheme.Info : HudTheme.Dim, 16f)) journalTab = 1;
            if (MenuKit.Button(new Rect(r.xMax - 226f * s, r.yMax - 62f * s, 200f * s, 44f * s), "CLOSE  (J / ESC)", HudTheme.Info, 17f)) OutpostUi.Hide(OutpostUi.Kind.Journal);
        }

        /// <summary>
        /// The checklist (the owner, 2026-10-19): a line for each kind of thing on the floor - the text, then a check box. When all of a kind is collected the box is ticked in green and the text goes green with a green line through it. Nothing is tracked at the docks.
        /// </summary>
        void DrawChecklist(Rect r, float y, float s)
        {
            int floor = HeistManager.Instance != null ? HeistManager.Instance.Floor : 0;
            for (int i = 0; i < ChecklistRules.Count; i++)
            {
                var kind = (ScanKind)i;
                bool done = floor >= 1 && FloorChecklistTracker.IsDone(kind);
                string label = ChecklistRules.Label(kind);
                var row = new Rect(r.x + 24f * s, y, r.width - 48f * s, 42f * s);
                MenuKit.Plate(row, 0.22f);
                var text = new Rect(row.x + 14f * s, row.y, row.width - 28f * s, row.height);
                HudTheme.Text(text, label, 21f, done ? HudTheme.Cash : HudTheme.Bright, TextAnchor.MiddleLeft, false);
                float tw = HudTheme.Width(label, 21f);
                if (done) DrawLine(new Vector2(text.x, row.center.y), new Vector2(text.x + tw, row.center.y), 3f * s, HudTheme.Cash);
                var box = new Rect(text.x + tw + 18f * s, row.center.y - 15f * s, 30f * s, 30f * s);
                HudTheme.Rounded(box, done ? HudTheme.Cash : HudTheme.Ink, 7f * s);
                HudTheme.Rounded(new Rect(box.x + 3f * s, box.y + 3f * s, box.width - 6f * s, box.height - 6f * s), HudTheme.Track, 5f * s);
                if (done)
                {
                    var c = box.center;
                    DrawLine(new Vector2(c.x - 8f * s, c.y + 0f * s), new Vector2(c.x - 2.5f * s, c.y + 6f * s), 4f * s, HudTheme.Cash);
                    DrawLine(new Vector2(c.x - 2.5f * s, c.y + 6f * s), new Vector2(c.x + 8.5f * s, c.y - 7f * s), 4f * s, HudTheme.Cash);
                }
                y += 46f * s;
            }
        }

        /// <summary>A straight line of a given thickness between two points on screen.</summary>
        static void DrawLine(Vector2 a, Vector2 b, float thickness, Color c)
        {
            var mid = (a + b) * 0.5f;
            float len = (b - a).magnitude, angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, mid);
            HudTheme.Rounded(new Rect(mid.x - len * 0.5f, mid.y - thickness * 0.5f, len, thickness), c, thickness * 0.5f);
            GUI.matrix = saved;
        }
    }
}