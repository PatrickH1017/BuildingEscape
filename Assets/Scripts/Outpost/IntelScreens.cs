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

        string noteTitle = "", noteBody = "";
        bool noteLearned;
        int noteFloor;
        int openedFrame = -1;           // (the frame a screen came up in: the key that opened it must not also close it)

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
            OutpostUi.Hide(OutpostUi.Kind.Note);
            OutpostUi.Hide(OutpostUi.Kind.Journal);
        }

        // ------------------------------------------------------------------ opening

        /// <summary>The local player reads a note (<paramref name="learned"/>: it taught him something new, so the journal grew).</summary>
        public static void ShowNote(string title, string body, bool learned, int safeFloor)
        {
            if (Instance == null) Boot();
            if (Instance == null || OutpostUi.Open) return;
            Instance.noteTitle = title;
            Instance.noteBody = body;
            Instance.noteLearned = learned;
            Instance.noteFloor = safeFloor;
            Instance.openedFrame = Time.frameCount;
            OutpostUi.Show(OutpostUi.Kind.Note);
        }

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
                    openedFrame = Time.frameCount;
                    OutpostUi.Show(OutpostUi.Kind.Journal);
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
            if (current != OutpostUi.Kind.Note && current != OutpostUi.Kind.Journal) return;

            var me = LocalPlayer();
            bool fresh = Time.frameCount == openedFrame;       // (the key that opened a screen must not also close it)
            bool leave = Input.GetKeyDown(KeyCode.Escape)
                         || (!fresh && Input.GetKeyDown(KeyCode.E))
                         || (!fresh && current == OutpostUi.Kind.Journal && Input.GetKeyDown(journalKey))
                         || OtherScreenUp || !me || me.InCustody || me.IsHidden;
            if (leave) OutpostUi.Hide(current);
        }

        // ------------------------------------------------------------------ drawing

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static GUIStyle paper;

        static GUIStyle Paper(int size, FontStyle fontStyle, Color colour)
        {
            if (paper == null) paper = new GUIStyle(GUI.skin.label) { wordWrap = true, richText = false, alignment = TextAnchor.UpperLeft };
            paper.fontSize = size;
            paper.fontStyle = fontStyle;
            paper.normal.textColor = colour;
            return paper;
        }

        void OnGUI()
        {
            var current = OutpostUi.Current;
            if (current == OutpostUi.Kind.Note) DrawNote();
            else if (current == OutpostUi.Kind.Journal) DrawJournal();
        }

        void DrawNote()
        {
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float w = 560f * s, h = 420f * s;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(new Rect(r.x - 5f * s, r.y - 5f * s, r.width + 10f * s, r.height + 10f * s), new Color(0.08f, 0.06f, 0.04f, 0.92f));
            Fill(r, new Color(0.97f, 0.92f, 0.60f));
            Fill(new Rect(r.x, r.y + 56f * s, r.width, 2f * s), new Color(0.75f, 0.35f, 0.30f, 0.9f));       // the red rule of a memo pad

            var ink = new Color(0.16f, 0.12f, 0.08f);
            GUI.Label(new Rect(r.x + 24f * s, r.y + 12f * s, w - 48f * s, 40f * s), noteTitle, Paper(Mathf.RoundToInt(28 * s), FontStyle.Bold, new Color(0.55f, 0.12f, 0.10f)));
            GUI.Label(new Rect(r.x + 24f * s, r.y + 72f * s, w - 48f * s, h - 150f * s), noteBody, Paper(Mathf.RoundToInt(24 * s), FontStyle.Normal, ink));
            if (noteLearned)
                GUI.Label(new Rect(r.x + 24f * s, r.yMax - 66f * s, w - 200f * s, 30f * s), "Added to your journal  (J)", Paper(Mathf.RoundToInt(18 * s), FontStyle.Italic, new Color(0.15f, 0.45f, 0.20f)));
            if (GUI.Button(new Rect(r.xMax - 150f * s, r.yMax - 56f * s, 130f * s, 38f * s), "Close  (Esc)")) OutpostUi.Hide(OutpostUi.Kind.Note);
        }

        void DrawJournal()
        {
            var entries = SafeIntel.Entries;
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float w = 640f * s;
            float h = Mathf.Min(Screen.height - 80f, (210f + Mathf.Max(1, entries.Count) * 44f) * s);
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Fill(new Rect(r.x - 4f * s, r.y - 4f * s, r.width + 8f * s, r.height + 8f * s), new Color(0.02f, 0.03f, 0.04f, 0.92f));
            Fill(r, new Color(0.09f, 0.11f, 0.14f, 0.97f));

            var title = Paper(Mathf.RoundToInt(26 * s), FontStyle.Bold, new Color(0.95f, 0.85f, 0.45f));
            GUI.Label(new Rect(r.x + 24f * s, r.y + 14f * s, w - 48f * s, 38f * s), "NOTES - FLOOR SAFES", title);

            var body = Paper(Mathf.RoundToInt(21 * s), FontStyle.Normal, new Color(0.90f, 0.92f, 0.94f));
            float y = r.y + 64f * s;
            if (entries.Count == 0)
            {
                GUI.Label(new Rect(r.x + 24f * s, y, w - 48f * s, 110f * s),
                    "Nothing yet. Some floors have a safe full of cash. The guard outposts (the rooms with a long dark window, locked with a keypad) " +
                    "sometimes keep a note about one: hack the lock, then read what is on the desks.", body);
            }
            else
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var line = Paper(Mathf.RoundToInt(22 * s), FontStyle.Bold, e.KnowsCode ? new Color(0.55f, 0.95f, 0.60f) : new Color(1f, 0.80f, 0.45f));
                    GUI.Label(new Rect(r.x + 24f * s, y, w - 48f * s, 40f * s), SafeNotes.JournalLine(e.floor, e.room, e.code), line);
                    y += 44f * s;
                }
                GUI.Label(new Rect(r.x + 24f * s, y + 6f * s, w - 48f * s, 70f * s),
                    "A safe with no code is pried open with a crowbar (the crowbar may snap).", Paper(Mathf.RoundToInt(18 * s), FontStyle.Italic, new Color(0.7f, 0.75f, 0.8f)));
            }
            if (GUI.Button(new Rect(r.xMax - 170f * s, r.yMax - 52f * s, 150f * s, 36f * s), "Close  (J / Esc)")) OutpostUi.Hide(OutpostUi.Kind.Journal);
        }
    }
}
