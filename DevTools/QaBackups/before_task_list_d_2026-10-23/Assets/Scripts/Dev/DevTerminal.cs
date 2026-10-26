#if ETR_DEV_TOOLS || UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The developer terminal (the owner, 2026-10-23: "CTRL + F1 should open a terminal"): a line to type in and the lines it printed, over the game, in the Editor and in the friends' builds (the scripting define <c>ETR_DEV_TOOLS</c>; a release build has
    /// no terminal at all: see the stub at the bottom of this file). CTRL + F1 opens and closes it, Enter runs the line, the up and down arrows bring back an earlier line, Esc closes it. While it is up the game keys are blocked and the cursor is free like
    /// in any other screen (<see cref="TestMode.PanelOpen"/> counts the terminal). Typing the unlock code (<see cref="DevTerminalRules.Accepts"/>) unlocks the dev menu (<see cref="DevAccess"/>); until then every line answers "Unknown command." and the
    /// code is never shown on the screen. Its words are plain tool output, no game text.
    /// </summary>
    public sealed class DevTerminal : MonoBehaviour
    {
        /// <summary>True while the terminal is up (the camera frees the cursor and the game keys are blocked).</summary>
        public static bool Open { get; private set; }

        static DevTerminal instance;
        readonly List<string> log = new List<string>();
        readonly List<string> history = new List<string>();
        int historyAt = -1;
        string input = "";
        bool focusNext;
        GUIStyle fieldStyle, lineStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Open = false; instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindFirstObjectByType<DevTerminal>()) return;
            var go = new GameObject("DevTerminal");
            DontDestroyOnLoad(go);
            go.AddComponent<DevTerminal>();
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        void OnDestroy() { if (instance == this) { instance = null; Open = false; } }

        /// <summary>The lines the terminal has printed (the tests read them).</summary>
        public static IReadOnlyList<string> Lines => instance != null ? instance.log : (IReadOnlyList<string>)System.Array.Empty<string>();

        /// <summary>Opens or closes the terminal, as CTRL + F1 does (the tests use it).</summary>
        public static void SetOpen(bool open)
        {
            if (instance == null) return;
            if (open == Open) return;
            Open = open;
            if (open) { instance.focusNext = true; instance.input = ""; instance.historyAt = -1; }
            else GUIUtility.keyboardControl = 0;
        }

        /// <summary>Tests: set it and the terminal opens or closes on the next frame, as if CTRL + F1 was pressed.</summary>
        public static bool TestToggle;

        /// <summary>Runs one typed line as Enter does (the tests use it).</summary>
        public static void RunLine(string line) { if (instance != null) instance.Submit(line); }

        void Update()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (TestToggle) { TestToggle = false; SetOpen(!Open); return; }
            if (ctrl && Input.GetKeyDown(KeyCode.F1)) { SetOpen(!Open); return; }
            if (Open && Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
        }

        void Print(string line)
        {
            log.Add(line);
            if (log.Count > DevTerminalRules.MaxLines) log.RemoveAt(0);
        }

        /// <summary>One typed line: the code unlocks, the commands run (only once unlocked), anything else is not a command.</summary>
        void Submit(string raw)
        {
            string line = DevTerminalRules.Clean(raw);
            input = "";
            focusNext = true;
            if (line.Length == 0) return;
            if (history.Count == 0 || history[history.Count - 1] != line) history.Add(line);
            if (history.Count > DevTerminalRules.MaxHistory) history.RemoveAt(0);
            historyAt = -1;
            if (DevTerminalRules.Accepts(line))
            {
                Print("> " + new string('*', line.Length));                              // (the code is never shown)
                if (DevAccess.Unlocked) { Print("Already unlocked."); return; }
                DevAccess.Unlock();
                TestMode.EnsureExists();
                Print("Unlocked. F1 opens the dev menu.");
                return;
            }
            Print("> " + line);
            if (!DevAccess.Unlocked) { Print("Unknown command."); return; }
            string verb = DevTerminalRules.Verb(line, out string rest);
            Run(verb, rest);
        }

        void Run(string verb, string rest)
        {
            switch (verb)
            {
                case "help":
                case "?":
                    Print("help  clear  close  lock  steamid");
                    break;
                case "clear": log.Clear(); break;
                case "close": SetOpen(false); break;
                case "lock":
                    DevAccess.Lock();
                    TestMode.CloseMenu();
                    Print("Locked.");
                    break;
                case "steamid":
                    {
                        var steam = Net.SteamService.Instance;
                        Print(steam != null && steam.Ready ? "Steam ID: " + Steamworks.SteamUser.GetSteamID().m_SteamID : "Steam is not running.");
                        break;
                    }
                default: Print("Unknown command."); break;
            }
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (!Open) return;
            var ev = Event.current;
            float s = HudTheme.S;
            float w = Mathf.Min(Screen.width - 24f, 960f * s), h = Mathf.Min(Screen.height * 0.55f, 560f * s);
            var panel = new Rect(12f, 12f, w, h);
            int size = Mathf.Max(10, Mathf.RoundToInt(19f * s));
            float lineH = size * 1.4f, inputH = size * 2.0f, pad = 8f * s;
            var green = new Color(0.4f, 1f, 0.5f);

            if (lineStyle == null) lineStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false, richText = false };
            lineStyle.fontSize = size; lineStyle.normal.textColor = green;
            if (fieldStyle == null) fieldStyle = new GUIStyle { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, wordWrap = false, richText = false };
            fieldStyle.fontSize = size; fieldStyle.normal.textColor = fieldStyle.focused.textColor = fieldStyle.hover.textColor = fieldStyle.active.textColor = Color.white;
            GUI.skin.settings.cursorColor = green;

            if (ev.type == EventType.Repaint)
            {
                Fill(panel, new Color(0.01f, 0.03f, 0.01f, 0.92f));
                Fill(new Rect(panel.x, panel.y, panel.width, 2f * s), green);
            }
            int maxLines = Mathf.Max(0, Mathf.FloorToInt((panel.height - inputH - pad * 3f) / lineH));
            int first = Mathf.Max(0, log.Count - maxLines);
            float y = panel.y + pad + 2f * s;
            for (int i = first; i < log.Count; i++) { GUI.Label(new Rect(panel.x + 14f * s, y, panel.width - 28f * s, lineH), log[i], lineStyle); y += lineH; }

            var line = new Rect(panel.x + 10f * s, panel.yMax - inputH - pad, panel.width - 20f * s, inputH);
            if (ev.type == EventType.Repaint) { Fill(line, new Color(0.02f, 0.09f, 0.03f, 1f)); }
            GUI.Label(new Rect(line.x + 6f * s, line.y, size * 1.2f, line.height), ">", lineStyle);

            bool enter = ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter);
            bool up = ev.type == EventType.KeyDown && ev.keyCode == KeyCode.UpArrow;
            bool down = ev.type == EventType.KeyDown && ev.keyCode == KeyCode.DownArrow;
            GUI.SetNextControlName("dev_terminal");
            input = GUI.TextField(new Rect(line.x + size * 1.4f, line.y, line.width - size * 1.6f, line.height), input, DevTerminalRules.MaxLine, fieldStyle);
            if (enter) { Submit(input); ev.Use(); }
            else if (up || down)
            {
                historyAt = DevTerminalRules.HistoryStep(historyAt, history.Count, up ? -1 : 1);
                input = historyAt < 0 ? "" : history[historyAt];
                ev.Use();
            }
            if (ev.type == EventType.Repaint && (focusNext || GUI.GetNameOfFocusedControl() != "dev_terminal")) { GUI.FocusControl("dev_terminal"); focusNext = false; }
        }
    }
}
#else
namespace BuildingEscape
{
    /// <summary>A release build has no developer terminal (see the real one: <c>DevTerminal.cs</c> under <c>ETR_DEV_TOOLS</c>): this stub is always closed, so the places that ask whether a screen is up need no switch of their own.</summary>
    public static class DevTerminal
    {
        public static bool Open => false;
    }
}
#endif
