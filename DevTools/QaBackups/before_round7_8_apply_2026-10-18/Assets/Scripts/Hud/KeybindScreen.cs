using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The Keybinds screen: every control of the game on one page, opened from Settings in the lobby menu and from the Esc menu in a run. It
    /// replaces the key hints that used to run along the bottom of the HUD (the owner wanted the HUD to take less of the screen, 2026-10-16).
    /// The list mirrors the real bindings in the scripts (<see cref="HowToPlayContent.Controls"/> is the long version for the How to Play
    /// page); keep both in step when a binding changes. It only lists the keys: nothing can be rebound here yet.
    /// Drawn with the HUD's toon kit on top of a dimmed screen; call <see cref="Draw"/> from OnGUI on every event.
    /// </summary>
    public static class KeybindScreen
    {
        struct Entry
        {
            public string key, label;
            public Entry(string k, string l) { key = k; label = l; }
        }

        struct Group
        {
            public string title;
            public Color accent;
            public Entry[] rows;
        }

        static readonly Group[] groups =
        {
            new Group
            {
                title = "MOVE", accent = HudTheme.Info, rows = new[]
                {
                    new Entry("WASD", "move"),
                    new Entry("MOUSE", "look"),
                    new Entry("ALT / V", "look around (hold)"),
                    new Entry("SHIFT", "sprint"),
                    new Entry("SPACE", "jump"),
                    new Entry("CTRL / C", "crouch (hold): slower, quieter"),
                },
            },
            new Group
            {
                title = "USE AND CARRY", accent = HudTheme.Quota, rows = new[]
                {
                    new Entry("E", "use what the crosshair is on"),
                    new Entry("TAB", "inventory"),
                    new Entry("Q", "grab / drop heavy loot"),
                    new Entry("LMB", "throw / swing weapon / fire"),
                    new Entry("R", "reload the pew"),
                    new Entry("H", "hide at a desk"),
                    new Entry("J", "journal (safes you know about)"),
                    new Entry("Z", "scan this room for loot (hold)"),
                    new Entry("WASD", "at a lock: press the key shown"),
                },
            },
            new Group
            {
                title = "PLACES AND TEAM", accent = HudTheme.Cash, rows = new[]
                {
                    new Entry("F", "elevator panel / sell (docks)"),
                    new Entry("G", "hidden elevator to the boss: go"),
                    new Entry("M", "mute your microphone"),
                    new Entry("T", "push to talk (if set in Settings)"),
                    new Entry("ESC", "menu: settings, keybinds, leave"),
                    new Entry("F1", "test menu (test mode only)"),
                },
            },
        };

        /// <summary>A tip printed under the lists (it used to sit at the end of the HUD's key hints).</summary>
        const string Tip = "Drop loot in the elevator to bank it";

        // ---- for the tests: the list without drawing it
        public static int GroupCount => groups.Length;
        public static string GroupTitle(int g) => groups[g].title;
        public static int EntryCount(int g) => groups[g].rows.Length;
        public static string KeyAt(int g, int i) => groups[g].rows[i].key;
        public static string LabelAt(int g, int i) => groups[g].rows[i].label;

        static int MaxRows
        {
            get
            {
                int n = 0;
                foreach (var g in groups) n = Mathf.Max(n, g.rows.Length);
                return n;
            }
        }

        /// <summary>
        /// Draws the screen (a centred panel over a dimmed screen) and handles its Back button and the Esc key. Call it on EVERY event, not only
        /// Repaint. Returns true when the player closed it.
        /// </summary>
        public static bool Draw()
        {
            var ev = Event.current;
            float s = HudTheme.S;
            int cols = groups.Length;
            float pw = Mathf.Min(Screen.width - 40f * s, 1000f * s);
            float pad = 22f * s, colGap = 16f * s, rowH = 38f * s, headH = 30f * s, tipH = 34f * s, btnH = 46f * s, btnW = 200f * s;
            float ph = 44f * s + headH + 8f * s + MaxRows * rowH + 10f * s + tipH + 12f * s + btnH + pad;
            var panel = new Rect((Screen.width - pw) * 0.5f, Mathf.Max(8f * s, (Screen.height - ph) * 0.5f), pw, ph);

            float k = HudAnim.Pop(HudAnim.Id.Controls, true);
            bool closed = false;
            if (ev.type == EventType.Repaint) HudTheme.Rounded(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.05f, 0.03f, 0.16f, 0.62f), 0f);
            var mat = HudAnim.Begin(k, panel.center);
            if (ev.type == EventType.Repaint)
            {
                HudTheme.Body(panel, HudTheme.Card, 22f, 4f, 6f, true, 2.2f);
                string title = "KEYBINDS";
                float rw = HudTheme.Width(title, 34f) + 90f * s;
                var ribbon = new Rect(panel.center.x - rw * 0.5f, panel.y - 24f * s, rw, 52f * s);
                HudTheme.Body(ribbon, HudTheme.Info, 20f, 3.5f, 4f, true, 1.8f);
                HudTheme.Text(ribbon, title, 34f, HudTheme.Bright, TextAnchor.MiddleCenter);

                float colW = (pw - pad * 2f - colGap * (cols - 1)) / cols;
                float keyCol = 98f * s;
                for (int c = 0; c < cols; c++)
                {
                    var g = groups[c];
                    float cx = panel.x + pad + c * (colW + colGap), y = panel.y + 44f * s;
                    float hw = HudTheme.Width(g.title, 15f) + 34f * s;
                    HudTheme.Chip(new Rect(cx, y, hw, headH - 2f * s), g.title, g.accent, 15f);
                    y += headH + 8f * s;
                    for (int i = 0; i < g.rows.Length; i++)
                    {
                        var row = new Rect(cx, y + i * rowH, colW, rowH - 4f * s);
                        if (i % 2 == 0) HudTheme.Rounded(row, HudTheme.Alpha(HudTheme.Ink, 0.16f), 9f * s);
                        float kh = 28f * s;
                        HudTheme.KeyCap(row.x + 6f * s, row.y + (row.height - kh) * 0.5f, g.rows[i].key, kh, 15f);
                        HudTheme.Text(new Rect(row.x + keyCol + 8f * s, row.y, row.width - keyCol - 12f * s, row.height), g.rows[i].label, 16f, HudTheme.Bright, TextAnchor.MiddleLeft, false);
                    }
                }
                float tipY = panel.y + 44f * s + headH + 8f * s + MaxRows * rowH + 10f * s;
                var tip = new Rect(panel.center.x - (HudTheme.Width(Tip, 16f) + 44f * s) * 0.5f, tipY, HudTheme.Width(Tip, 16f) + 44f * s, tipH);
                HudTheme.Pill(tip, HudTheme.Quota, 2.5f, 2.5f);
                HudTheme.Text(tip, Tip, 16f, HudTheme.Ink, TextAnchor.MiddleCenter, false);
            }
            var back = new Rect(panel.center.x - btnW * 0.5f, panel.yMax - pad - btnH, btnW, btnH);
            if (HudWidgets.Button(back, "BACK", HudTheme.Info, 24f)) closed = true;
            HudAnim.End(mat);

            if (Input.GetKeyDown(KeyCode.Escape)) closed = true;
            return closed;
        }
    }
}
