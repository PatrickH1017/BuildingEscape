using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The screen of the floor safe (E on it): a four-digit keypad - click the keys or type the digits, Backspace takes one back, Enter or OK
    /// tries the code - and, for a safe whose code nobody knows, a button that pries it open with a crowbar. Prying takes a few seconds and
    /// is noisy (guards in earshot come to look); the crowbar may snap when it is done. The host decides every outcome
    /// (<see cref="FloorSafe.HostAttempt"/>); this is the screen, the keys and the clock. Esc, walking away or the safe opening closes it.
    /// </summary>
    public sealed class SafePanel : MonoBehaviour
    {
        /// <summary>How long prying takes, how loud it is (a guard this many metres away hears it) and how often it makes a noise.</summary>
        public const float PrySeconds = 5f, PryNoise = 16f, PryNoiseEvery = 1f;
        /// <summary>The player may stand this much farther from the safe than its reach before the panel closes.</summary>
        const float WalkAwaySlack = 1.5f;

        public static SafePanel Instance { get; private set; }

        FloorSafe safe;
        WobblyPlayer player;
        string typed = "";
        bool waiting;
        float waitingSince;
        string message = "";
        Color messageColour = Color.white;
        float messageUntil;
        bool prying;
        float pryStart, nextNoise;
        int pryPlays;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // ------------------------------------------------------------------ opening and closing

        public static void Begin(FloorSafe s, WobblyPlayer who)
        {
            if (!s || !who || s.IsOpen) return;
            if (who.InCustody || who.IsHidden || OutpostUi.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open) return;
            if (Instance == null)
            {
                var go = new GameObject("SafePanel");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<SafePanel>();
            }
            Instance.Open(s, who);
        }

        void Open(FloorSafe s, WobblyPlayer who)
        {
            safe = s;
            player = who;
            typed = "";
            waiting = prying = false;
            message = "";
            messageUntil = 0f;
            OutpostUi.Show(OutpostUi.Kind.Safe);
            GameAudio.PlaySound(SoundId.KeypadBeep, s.transform.position + Vector3.up * 0.5f, 0.5f, 0.8f, true, 10f, 2f, 0);
        }

        void Close()
        {
            prying = false;
            waiting = false;
            safe = null;
            OutpostUi.Hide(OutpostUi.Kind.Safe);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            OutpostUi.Hide(OutpostUi.Kind.Safe);
        }

        bool Showing => OutpostUi.Current == OutpostUi.Kind.Safe && safe != null;

        // ------------------------------------------------------------------ the keys and the clock

        void Update()
        {
            if (!Showing)
            {
                if (safe != null) { prying = false; safe = null; }       // (another screen took the place of this one)
                return;
            }
            float now = Time.unscaledTime;

            bool leave = Input.GetKeyDown(KeyCode.Escape) || EscapeMenu.Open || !safe || safe.IsOpen || !player || player.InCustody || player.IsHidden
                         || Backpack.UiOpen || Vendor.ShopOpen;
            if (!leave && safe.DistanceFrom(player.transform.position) > safe.reach + WalkAwaySlack) leave = true;
            if (leave)
            {
                if (prying && Input.GetKeyDown(KeyCode.Escape)) { prying = false; return; }      // the first Esc only stops prying
                Close();
                return;
            }

            if (waiting && now - waitingSince > 1.2f) waiting = false;          // (an answer that never came: let the player try again)

            if (prying)
            {
                if (now >= nextNoise)
                {
                    nextNoise = now + PryNoiseEvery;
                    NoiseBus.Emit(safe.transform.position, PryNoise, player.gameObject);
                    GameAudio.PlaySound(SoundId.Pry, safe.transform.position + Vector3.up * 0.5f, 0.9f, 1f, true, 30f, 2f, pryPlays++ % OutpostSound.PryVariants);
                }
                if (now - pryStart >= PrySeconds)
                {
                    prying = false;
                    waiting = true;
                    waitingSince = now;
                    safe.SubmitPry(player);
                }
                return;
            }
            if (waiting) return;

            for (int d = 0; d <= 9; d++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + d) || Input.GetKeyDown(KeyCode.Keypad0 + d)) Press((char)('0' + d));
            if (Input.GetKeyDown(KeyCode.Backspace)) Back();
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Enter();
        }

        void Press(char digit)
        {
            if (typed.Length >= 4) return;
            typed = SafeRules.AddDigit(typed, digit);
            Click(1f + 0.05f * typed.Length);
        }

        void Back()
        {
            if (typed.Length == 0) return;
            typed = SafeRules.Backspace(typed);
            Click(0.85f);
        }

        void Enter()
        {
            if (typed.Length < 4)
            {
                Say("Four digits", new Color(1f, 0.8f, 0.4f), 1f);
                return;
            }
            if (!int.TryParse(typed, out int code)) { typed = ""; return; }
            typed = "";
            waiting = true;
            waitingSince = Time.unscaledTime;
            Click(1.3f);
            safe.SubmitCode(code, player);
        }

        void Click(float pitch)
        {
            if (safe) GameAudio.PlaySound(SoundId.KeypadBeep, safe.transform.position + Vector3.up * 0.5f, 0.45f, pitch, true, 10f, 2f);
        }

        void Say(string text, Color colour, float seconds)
        {
            message = text;
            messageColour = colour;
            messageUntil = Time.unscaledTime + seconds;
        }

        /// <summary>The host said no (wrong code, keypad locked out, no crowbar).</summary>
        public void OnReply(SafeReply reply)
        {
            waiting = false;
            if (!Showing) return;
            switch (reply)
            {
                case SafeReply.Wrong: Say("WRONG CODE", new Color(1f, 0.35f, 0.3f), 1.4f); break;
                case SafeReply.LockedOut: Say("KEYPAD LOCKED - WAIT", new Color(1f, 0.6f, 0.25f), 1.4f); break;
                case SafeReply.NoCrowbar: Say("YOU NEED A CROWBAR", new Color(1f, 0.6f, 0.25f), 1.8f); break;
            }
        }

        // ------------------------------------------------------------------ the screen

        void OnGUI()
        {
            if (!Showing) return;
            MenuKit.Begin();
            float s = HudTheme.S;
            float w = 460f * s, h = 720f * s;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint)
            {
                MenuKit.Dim(0.35f);
                HudTheme.Panel(r, 16f);
                HudTheme.Text(new Rect(r.x, r.y + 16f * s, w, 40f * s), (FloorSafe.OnShow.Count > 1 && safe ? "FLOOR SAFE " + (safe.Index + 1) : "FLOOR SAFE"), 26f, HudTheme.Bright, TextAnchor.MiddleCenter);
            }

            // the four digits
            float cw = 72f * s, gap = 14f * s;
            float rowW = 4 * cw + 3 * gap, rowX = r.x + (w - rowW) * 0.5f, rowY = r.y + 74f * s;
            if (repaint)
            {
                bool flashing = Time.unscaledTime < messageUntil;
                for (int i = 0; i < 4; i++)
                {
                    var cr = new Rect(rowX + i * (cw + gap), rowY, cw, 88f * s);
                    HudTheme.Rounded(cr, HudTheme.Ink, 9f * s);
                    HudTheme.Rounded(new Rect(cr.x + 3f * s, cr.y + 3f * s, cr.width - 6f * s, cr.height - 6f * s), HudTheme.Shade(HudTheme.Card, 0.55f), 7f * s);
                    HudTheme.Rounded(new Rect(cr.x + 1.5f * s, cr.yMax - 5f * s, cr.width - 3f * s, 3.5f * s), flashing ? messageColour : HudTheme.Good, 1.5f * s);
                    string glyph = i < typed.Length ? typed[i].ToString() : (waiting || prying ? "." : "_");
                    HudTheme.Text(cr, glyph, 54f, HudTheme.Good, TextAnchor.MiddleCenter, false);
                }
                string status = Time.unscaledTime < messageUntil ? message : waiting ? "Checking..." : prying ? "Prying..." : "Type the 4-digit code";
                var sc = Time.unscaledTime < messageUntil ? messageColour : HudTheme.Muted;
                HudTheme.Text(new Rect(r.x, rowY + 98f * s, w, 32f * s), status, 21f, sc, TextAnchor.MiddleCenter, false);
            }

            // the keypad
            bool canType = !waiting && !prying;
            float bw = 106f * s, bh = 66f * s, bg = 12f * s;
            float padW = 3 * bw + 2 * bg, padX = r.x + (w - padW) * 0.5f, padY = r.y + 218f * s;
            for (int i = 0; i < 12; i++)
            {
                int col = i % 3, row = i / 3;
                var br = new Rect(padX + col * (bw + bg), padY + row * (bh + bg), bw, bh);
                string label;
                if (i < 9) label = ((char)('1' + i)).ToString();
                else label = i == 9 ? "<" : i == 10 ? "0" : "OK";
                if (MenuKit.Button(br, label, i == 11 ? HudTheme.Cash : HudTheme.Dim, 30f, canType, TextAnchor.MiddleCenter))
                {
                    if (i < 9) Press((char)('1' + i));
                    else if (i == 9) Back();
                    else if (i == 10) Press('0');
                    else Enter();
                }
            }

            // prying
            float py = padY + 4 * (bh + bg) + 10f * s;
            var pack = player ? player.GetComponent<Backpack>() : null;
            bool hasCrowbar = FloorSafe.FindCrowbar(pack) != null;
            if (prying)
            {
                float k = Mathf.Clamp01((Time.unscaledTime - pryStart) / PrySeconds);
                if (repaint) HudTheme.Bar(new Rect(r.x + 30f * s, py + 6f * s, w - 60f * s, 22f * s), k, HudTheme.Warning, 2.5f);
                if (MenuKit.Button(new Rect(r.x + 30f * s, py + 38f * s, w - 60f * s, 38f * s), "STOP PRYING  (ESC)", HudTheme.Warning, 17f)) prying = false;
            }
            else
            {
                var label = hasCrowbar ? "PRY IT OPEN WITH YOUR CROWBAR  (NOISY, " + Mathf.RoundToInt(PrySeconds) + " S)" : "PRY IT OPEN  (NEEDS A CROWBAR)";
                if (MenuKit.Button(new Rect(r.x + 30f * s, py, w - 60f * s, 42f * s), label, HudTheme.Warning, 16f, canType && hasCrowbar))
                {
                    prying = true;
                    pryStart = Time.unscaledTime;
                    nextNoise = Time.unscaledTime;
                    typed = "";
                }
            }

            if (repaint)
            {
                string note = null;
                Color noteColour = HudTheme.Muted;
                if (safe && SafeIntel.TryGet(safe.Floor, safe.Index, out var e))
                {
                    note = e.KnowsCode ? "Your notes say the code is " + SafeRules.CodeText(e.code) : "Your notes: nobody knows the code - use a crowbar";
                    noteColour = e.KnowsCode ? HudTheme.Cash : HudTheme.Quota;
                }
                if (note != null) HudTheme.Text(new Rect(r.x, r.yMax - 66f * s, w, 26f * s), note, 18f, noteColour, TextAnchor.MiddleCenter, false);
                HudTheme.Text(new Rect(r.x, r.yMax - 36f * s, w, 26f * s), "Esc - step back", 16f, HudTheme.Dim, TextAnchor.MiddleCenter, false);
            }
        }
    }
}