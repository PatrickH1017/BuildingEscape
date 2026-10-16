using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The quick hack on the guard outpost's door lock. E on the keypad (or on the door) brings it up: after a short "hacking" wait a key
    /// (W, A, S or D) pops up in the middle of the screen and the player has one second to press it; then the next one, four in all. All
    /// four in time opens the lock; a wrong key or a missed second shorts it out and sets the alarm off. Esc, stepping away from the
    /// keypad, a guard's grab or somebody else's alarm backs out of it with no alarm. The rules are <see cref="HackSession"/>; this is
    /// the screen, the keys and the sounds. Purely local until it ends: only the result (won / lost) goes to the host.
    ///
    /// While the panel is up the player stands still (<see cref="OutpostUi"/>), so the keys do not walk him away from the keypad.
    /// </summary>
    public sealed class HackPanel : MonoBehaviour
    {
        /// <summary>How long the result stays on screen after the hack ended.</summary>
        const float ResultSeconds = 1.3f;

        public static HackPanel Instance { get; private set; }

        /// <summary>Is a hack going on right now (the keys are being asked for)?</summary>
        public static bool Running => Instance != null && Instance.session != null && !Instance.session.Finished;

        static readonly KeyCode[] Keys = { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D };

        HackSession session;
        OutpostController controller;
        WobblyPlayer player;
        int lastStep;
        // what is on screen after the hack ended
        float resultUntil;
        bool resultWon;
        string resultText = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Instance = null; }

        // ------------------------------------------------------------------ starting

        /// <summary>The local player starts hacking the lock of <paramref name="c"/>.</summary>
        public static void Begin(OutpostController c, WobblyPlayer who)
        {
            if (!c || !who || c.Unlocked || c.AlarmOn) return;
            if (who.InCustody || who.IsHidden || OutpostUi.Open || Backpack.UiOpen || Vendor.ShopOpen || ElevatorPanel.Open || EscapeMenu.Open) return;
            if (Instance == null)
            {
                var go = new GameObject("HackPanel");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<HackPanel>();
            }
            Instance.StartHack(c, who);
        }

        void StartHack(OutpostController c, WobblyPlayer who)
        {
            if (Running) return;
            controller = c;
            player = who;
            session = new HackSession(HackSession.MakeKeys(new System.Random()), Time.unscaledTime);
            lastStep = 0;
            resultUntil = 0f;
            OutpostUi.Show(OutpostUi.Kind.Hack);
            GameAudio.PlaySound(SoundId.KeypadBeep, c.KeypadPosition, 0.5f, 0.8f, true, 14f, 2f, 0);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (session != null && !session.Finished) session.Cancel();
            OutpostUi.Hide(OutpostUi.Kind.Hack);
        }

        // ------------------------------------------------------------------ the keys

        void Update()
        {
            if (session == null) return;
            if (session.Finished) return;
            float now = Time.unscaledTime;

            string reason = BackOutReason();
            if (reason != null)
            {
                session.Cancel();
                End(false, reason, false);
                return;
            }

            for (int i = 0; i < Keys.Length && !session.Finished; i++)
            {
                if (!Input.GetKeyDown(Keys[i])) continue;
                char ch = HackSession.KeyPool[i];
                session.Press(ch, now);
                if (session.Phase == HackPhase.Prompt && session.Step > lastStep)
                {
                    lastStep = session.Step;
                    GameAudio.PlaySound(SoundId.KeypadBeep, controller.KeypadPosition, 0.6f, 1f + 0.1f * lastStep, true, 14f, 2f, lastStep % OutpostSound.KeypadVariants);
                }
            }
            if (!session.Finished) session.Tick(now);

            if (session.Phase == HackPhase.Won) End(true, "ACCESS GRANTED", true);
            else if (session.Phase == HackPhase.Lost) End(false, session.Reason.ToUpperInvariant() + " - ALARM!", true);
        }

        /// <summary>Why the hack has to stop with no result (null while it can go on).</summary>
        string BackOutReason()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) return "";
            if (!controller) return "";
            if (controller.Unlocked) return "";
            if (controller.AlarmOn) return "The lock jammed";
            if (!player || player.InCustody || player.IsHidden) return "";
            if (EscapeMenu.Open) return "";
            if ((player.transform.position - controller.KeypadPosition).sqrMagnitude > OutpostController.HackReach * OutpostController.HackReach) return "Too far from the lock";
            return null;
        }

        /// <summary>The hack is over: let go of the player, report the result (a back-out reports nothing) and keep the message up for a moment.</summary>
        void End(bool won, string text, bool report)
        {
            OutpostUi.Hide(OutpostUi.Kind.Hack);
            resultWon = won;
            resultText = text;
            resultUntil = report ? Time.unscaledTime + ResultSeconds : 0f;      // (a back-out shows no panel; a reason, if any, goes to the notice line)
            if (report && controller) controller.SubmitHack(won);
            else if (text.Length > 0) Backpack.NoticeToLocal(text);
        }

        // ------------------------------------------------------------------ the screen

        static readonly Color Back = new Color(0.04f, 0.05f, 0.07f, 0.88f);
        static readonly Color Edge = new Color(0.25f, 0.9f, 0.45f, 0.9f);
        static readonly Color Dim = new Color(0.2f, 0.24f, 0.26f, 1f);
        static readonly Color Green = new Color(0.25f, 0.9f, 0.45f, 1f);
        static readonly Color Amber = new Color(1f, 0.78f, 0.2f, 1f);
        static readonly Color Red = new Color(1f, 0.3f, 0.25f, 1f);

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Frame(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || session == null) return;
            bool live = !session.Finished;
            if (!live && Time.unscaledTime >= resultUntil) return;

            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float w = 560f * s, h = 300f * s;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - h * 0.55f, w, h);
            Fill(box, Back);
            Frame(box, live ? Edge : resultWon ? Green : Red, Mathf.Max(2f, 3f * s));

            HudText.Label(new Rect(box.x, box.y + 10f * s, w, 34f * s), "SHORT-CIRCUIT THE LOCK",
                HudStyles.Label(Mathf.RoundToInt(24 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Green));

            // the four steps
            float chip = 58f * s, gap = 14f * s;
            float rowW = 4 * chip + 3 * gap, rowX = box.x + (w - rowW) * 0.5f, rowY = box.y + 56f * s;
            for (int i = 0; i < HackSession.Steps; i++)
            {
                var cr = new Rect(rowX + i * (chip + gap), rowY, chip, chip);
                bool done = i < session.Step || session.Phase == HackPhase.Won;
                bool current = live && session.Phase == HackPhase.Prompt && i == session.Step;
                Fill(cr, done ? new Color(0.1f, 0.4f, 0.2f, 1f) : Dim);
                Frame(cr, done ? Green : current ? Amber : new Color(0.35f, 0.4f, 0.42f, 1f), Mathf.Max(2f, 2f * s));
                string glyph = done ? session.Keys[i].ToString() : "?";
                HudText.Label(cr, glyph, HudStyles.Label(Mathf.RoundToInt(30 * s), TextAnchor.MiddleCenter, FontStyle.Bold, done ? Green : Color.white));
            }

            // the key to press, big
            var bigArea = new Rect(box.x, rowY + chip + 6f * s, w, 90f * s);
            if (live)
            {
                if (session.Phase == HackPhase.Prompt)
                {
                    HudText.Label(bigArea, session.Expected.ToString(), HudStyles.Label(Mathf.RoundToInt(92 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Amber));
                    float left = session.TimeLeft(Time.unscaledTime) / HackSession.Window;
                    var bar = new Rect(box.x + 40f * s, bigArea.yMax + 8f * s, w - 80f * s, 14f * s);
                    Fill(bar, Dim);
                    Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(left), bar.height), left > 0.35f ? Green : Red);
                }
                else
                {
                    int dots = 1 + (int)(Time.unscaledTime * 4f) % 3;
                    HudText.Label(bigArea, "Hacking" + new string('.', dots), HudStyles.Label(Mathf.RoundToInt(40 * s), TextAnchor.MiddleCenter, FontStyle.Bold, Green));
                    HudText.Label(new Rect(box.x, bigArea.yMax - 8f * s, w, 30f * s), "Get ready - press each key the moment it appears",
                        HudStyles.Label(Mathf.RoundToInt(18 * s), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.8f, 0.85f, 0.85f)));
                }
                HudText.Label(new Rect(box.x, box.yMax - 34f * s, w, 26f * s), "Esc - give up",
                    HudStyles.Label(Mathf.RoundToInt(16 * s), TextAnchor.MiddleCenter, FontStyle.Normal, new Color(0.7f, 0.75f, 0.75f)));
            }
            else
            {
                HudText.Label(new Rect(box.x, bigArea.y, w, bigArea.height + 24f * s), resultText,
                    HudStyles.Label(Mathf.RoundToInt(34 * s), TextAnchor.MiddleCenter, FontStyle.Bold, resultWon ? Green : Red));
            }
        }
    }
}
