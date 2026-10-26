# The owner, 2026-10-24: "Scanning should only have a pop-up when it says 'Item Marked', if an item is not found while scanning there should not be a pop-up, when trying to scan on cooldown
# there should not be a pop-up but instead use one of our sound affect assets to make an error noise".
#  - LootScanRules: the pop-up's words and how long it stays (MarkedText / MarkedPopupAlpha), RefusedPress (a press while the cooldown holds the scan back); the "Wait n seconds" rules (2026-10-24 earlier) go.
#  - LootScanner: no status pill, no "Wait" pill, no "+N more out of view" pill; the one pop-up says "Item Marked"; a press during the cooldown plays SoundId.SafeWrong (our wrong-code blips), flat.
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

# ---- the rules
Open-Src ($A + 'Scripts\LootScanRules.cs')
Rep @'
        /// <summary>
        /// What the screen asks the player to wait (the owner, 2026-10-24: "the cooldown should say 'Wait x seconds' rounded to the nearest integer for seconds (either 1 or 2 since it's a 2 second cooldown)"): the time left of the cooldown rounded to the nearest whole second, never
        /// less than 1 while a scan is still held back; 0 when a scan can leave (<paramref name="sinceLast"/> negative: there has been none).
        /// </summary>
        public static int WaitSeconds(float sinceLast)
        {
            if (sinceLast < 0f) return 0;
            float left = PulseGap - sinceLast;
            if (left <= 0f) return 0;
            return Math.Max(1, (int)Math.Round(left, MidpointRounding.AwayFromZero));
        }

        /// <summary>"Wait 2 seconds" / "Wait 1 second"; "" when there is nothing to wait for.</summary>
        public static string WaitText(int seconds) => seconds <= 0 ? "" : "Wait " + seconds + (seconds == 1 ? " second" : " seconds");
'@ @'
        /// <summary>
        /// The words of the scan's one pop-up (the owner, 2026-10-24: "Scanning should only have a pop-up when it says 'Item Marked', if an item is not found while scanning there should not be a pop-up"): a scan that marks nothing shows nothing.
        /// </summary>
        public const string MarkedText = "Item Marked";
        /// <summary>How long the pop-up stays after a scan has marked something (s), and how much of that it spends fading out.</summary>
        public const float MarkedPopupSeconds = 2f, MarkedFadeSeconds = 0.4f;

        /// <summary>How visible the pop-up is <paramref name="sinceMark"/> seconds after a scan marked something: 1, fading to 0 at <see cref="MarkedPopupSeconds"/>; 0 before that (negative) and after it.</summary>
        public static float MarkedPopupAlpha(float sinceMark)
        {
            if (sinceMark < 0f || sinceMark >= MarkedPopupSeconds) return 0f;
            return sinceMark > MarkedPopupSeconds - MarkedFadeSeconds ? (MarkedPopupSeconds - sinceMark) / MarkedFadeSeconds : 1f;
        }

        /// <summary>
        /// Does this press of the scan key get the error sound (the owner, 2026-10-24: "when trying to scan on cooldown there should not be a pop-up but instead use one of our sound affect assets to make an error noise")? Only a press (not a key that stays down)
        /// that the cooldown holds back.
        /// </summary>
        public static bool RefusedPress(bool pressed, bool ready) => pressed && !ready;
'@
Save-Src

# ---- the scanner
Open-Src ($A + 'Scripts\LootScanner.cs')
Rep @'
    /// label (what, how much, how far) hung where the thing lies - seen through desks and cabinets, the point of a scan - and fades after a few seconds; the top of the screen says
    /// which room it is and what is in it ("nothing to steal here" is worth knowing, too). Another pulse leaves every couple of seconds while the key is held. It is purely local: every
'@ @'
    /// label (what, how much, how far) hung where the thing lies - seen through desks and cabinets, the point of a scan - and fades after a few seconds. The scan's only pop-up says
    /// "Item Marked" (the owner, 2026-10-24: a scan that finds nothing, and a press of the key during the cooldown, show nothing - the cooldown gets an error sound). Another pulse leaves every couple of seconds while the key is held. It is purely local: every
'@
Rep @'
        /// <summary>The whole seconds to wait for the next scan (<see cref="LootScanRules.WaitSeconds"/>): 0 when it can leave; the screen shows "Wait n seconds".</summary>
        public static int WaitSeconds { get; private set; }
'@ @'
        /// <summary>How many times the error sound has played: a press of the scan key while the cooldown holds the scan back (for tests).</summary>
        public static int ErrorBeeps { get; private set; }
'@
Rep 'Scanning = false; ScanReady = 1f; WaitSeconds = 0; }' 'Scanning = false; ScanReady = 1f; ErrorBeeps = 0; }'
Rep '        bool pulsing, inCar, scannedRoom;' @'
        bool pulsing, inCar, scannedRoom, testKeyWas;
        float lastMarkAt = -100f;                   // (Time.time of the last mark: the pop-up counts from it)
'@
Rep '        public int MarkCount => marks.Count;' @'
        public int MarkCount => marks.Count;
        /// <summary>The words of the pop-up on screen now ("Item Marked" for a couple of seconds after a scan marked something, otherwise "": for the HUD and the tests).</summary>
        public string PopupText => marks.Count > 0 && LootScanRules.MarkedPopupAlpha(Time.time - lastMarkAt) > 0f ? LootScanRules.MarkedText : "";
'@
Rep '            bool ready = LootScanRules.NextPulse(holding, sinceUse);' @'
            bool ready = LootScanRules.NextPulse(holding, sinceUse);
            // a press of the scan key that the cooldown holds back: no pop-up, an error sound (the owner, 2026-10-24)
            bool pressed = can && (Input.GetKeyDown(ScanKey) || (TestKeyDown && !testKeyWas));
            testKeyWas = TestKeyDown;
            if (me && LootScanRules.RefusedPress(pressed, ready)) RefusePress(me);
'@
Rep '            WaitSeconds = LootScanRules.WaitSeconds(sinceUse);
' ''
Rep '        /// <summary>Which room is the player in (the elevator car is none: it lies inside its lobby, but it is a room of its own to the player)? Another room: the old marks go.</summary>' @'
        /// <summary>The error noise of a scan the cooldown refuses: our wrong-code blips (<see cref="SoundId.SafeWrong"/>), flat in the player's ears.</summary>
        void RefusePress(Transform me)
        {
            ErrorBeeps++;
            GameAudio.PlaySound(SoundId.SafeWrong, me.position, 0.7f, 1f, false);
        }

        /// <summary>Which room is the player in (the elevator car is none: it lies inside its lobby, but it is a room of its own to the player)? Another room: the old marks go.</summary>
'@
Rep '            m.hitAt = Time.time;' @'
            m.hitAt = Time.time;
            lastMarkAt = Time.time;
'@
Rep '        /// <summary>What the top of the screen says (also for tests): which room, and what is in it.</summary>' '        /// <summary>What the last scan read of the room: which room, and what is in it. Not drawn any more (the owner, 2026-10-24: the scan''s only pop-up says "Item Marked"); the tests read it.</summary>'
Rep '            if (!any && !Scanning) return;' '            if (!any) return;'
Rep '            int offScreen = 0, shown = 0;' '            int shown = 0;'
Rep @'
                    offScreen++;
                    if (edges.Count < 14)
'@ @'
                    if (edges.Count < 14)
'@
Rep @'
            // the readout at the top: which room and what is in it; and what lies behind you
            if (Scanning || any)
            {
                string line = Scanning ? StatusLine : $"{marks.Count} thing{(marks.Count == 1 ? "" : "s")} marked";
                float w = HudTheme.Width(line, 17f) + 44f * s;
                var r = new Rect((Screen.width - w) * 0.5f, 86f * s, w, 34f * s);
                bool nothing = Scanning && (inCar || !room.Valid || (scannedRoom && roomCount + roomOther == 0));
                HudTheme.Pill(r, nothing ? new Color(0.35f, 0.30f, 0.50f, 0.95f) : new Color(0.10f, 0.45f, 0.62f, 0.95f), 2.5f, 2.5f);
                HudTheme.Text(r, line, 17f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
                if (Scanning && WaitSeconds > 0)                                                 // (the cooldown, in words: "Wait 2 seconds" / "Wait 1 second", under the scan's line; the owner, 2026-10-24)
                {
                    string wait = LootScanRules.WaitText(WaitSeconds);
                    float ww = HudTheme.Width(wait, 17f) + 44f * s;
                    var wr = new Rect((Screen.width - ww) * 0.5f, r.yMax + 6f * s + (offScreen > 0 ? 32f * s : 0f), ww, 32f * s);
                    HudTheme.Pill(wr, new Color(0.62f, 0.34f, 0.08f, 0.95f), 2.5f, 2.5f);
                    HudTheme.Text(wr, wait, 17f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
                }
                if (offScreen > 0)
                {
                    string more = $"+{offScreen} more out of view";
                    float mw = HudTheme.Width(more, 14f) + 30f * s;
                    var rr = new Rect((Screen.width - mw) * 0.5f, r.yMax + 6f * s, mw, 26f * s);
                    HudTheme.Pill(rr, new Color(0.12f, 0.10f, 0.30f, 0.9f), 2f, 2f);
                    HudTheme.Text(rr, more, 14f, HudTheme.Cream, TextAnchor.MiddleCenter, false);
                }
            }
'@ @'
            // the scan's one pop-up (the owner, 2026-10-24: "Scanning should only have a pop-up when it says 'Item Marked'"): nothing for a scan that finds nothing, nothing for a press during the cooldown (that gets an error sound)
            float popup = LootScanRules.MarkedPopupAlpha(Time.time - lastMarkAt);
            if (popup > 0f)
            {
                string line = LootScanRules.MarkedText;
                float w = HudTheme.Width(line, 17f) + 44f * s;
                var r = new Rect((Screen.width - w) * 0.5f, 86f * s, w, 34f * s);
                var bright = HudTheme.Bright; bright.a *= popup;
                HudTheme.Pill(r, new Color(0.10f, 0.45f, 0.62f, 0.95f * popup), 2.5f, 2.5f);
                HudTheme.Text(r, line, 17f, bright, TextAnchor.MiddleCenter, false);
            }
'@
Save-Src

# ---- the tests
Open-Src ($A + 'Editor\NetDev\NetDevAsks1023Test.cs')
Rep @'
            Check("scan (T21): the screen asks to wait 2 seconds right after a scan, 1 second in the second half, nothing when a scan can leave (the owner, 2026-10-24: rounded to the nearest whole second, 1 or 2)",
                LootScanRules.WaitSeconds(0f) == 2 && LootScanRules.WaitSeconds(0.4f) == 2 && LootScanRules.WaitSeconds(0.5f) == 2 && LootScanRules.WaitSeconds(0.6f) == 1 && LootScanRules.WaitSeconds(1.4f) == 1 && LootScanRules.WaitSeconds(1.99f) == 1 && LootScanRules.WaitSeconds(2f) == 0 && LootScanRules.WaitSeconds(9f) == 0 && LootScanRules.WaitSeconds(-1f) == 0);
            Check("scan (T21): the words are 'Wait 2 seconds' and 'Wait 1 second' (nothing to wait: no words)", LootScanRules.WaitText(2) == "Wait 2 seconds" && LootScanRules.WaitText(1) == "Wait 1 second" && LootScanRules.WaitText(0) == "");
'@ @'
            Check("scan (the owner, 2026-10-24): the one pop-up says 'Item Marked', full for the first 1.6 s after a scan marked something, fading out to 2 s, none before a mark or after it",
                LootScanRules.MarkedText == "Item Marked" && LootScanRules.MarkedPopupAlpha(-1f) == 0f && LootScanRules.MarkedPopupAlpha(0f) == 1f && LootScanRules.MarkedPopupAlpha(1.5f) == 1f && Math.Abs(LootScanRules.MarkedPopupAlpha(1.8f) - 0.5f) < 1e-4f && LootScanRules.MarkedPopupAlpha(2f) == 0f && LootScanRules.MarkedPopupAlpha(100f) == 0f);
            Check("scan (the owner, 2026-10-24): a press the cooldown holds back gets the error sound; a press when the scan can leave, and a key that is not pressed, do not", LootScanRules.RefusedPress(true, false) && !LootScanRules.RefusedPress(true, true) && !LootScanRules.RefusedPress(false, false) && !LootScanRules.RefusedPress(false, true));
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevAsks1023.cs')
Rep '                    int pulses0 = scanner.Pulses;' @'
                    int pulses0 = scanner.Pulses, beeps0 = LootScanner.ErrorBeeps;
'@
Rep '"T21: the scan goes off at once when the key goes down (no charge-up)", scanner.Pulses == pulses0 + 1 && LootScanner.Scanning, $"(pulses {scanner.Pulses - pulses0})");' '"T21: the scan goes off at once when the key goes down (no charge-up), with no error sound", scanner.Pulses == pulses0 + 1 && LootScanner.Scanning && LootScanner.ErrorBeeps == beeps0, $"(pulses {scanner.Pulses - pulses0}, beeps {LootScanner.ErrorBeeps - beeps0})");
                    A23("T21: the pop-up says Item Marked when the scan marked something, and nothing when it marked nothing", scanner.PopupText == (scanner.MarkCount > 0 ? "Item Marked" : ""), $"(marks {scanner.MarkCount}, pop-up ''{scanner.PopupText}'')");'
Rep '"T21: 2.4 s after the first scan, still held, the second leaves", scanner.Pulses == pulses0 + 2, $"(pulses {scanner.Pulses - pulses0})");' '"T21: 2.4 s after the first scan, still held, the second leaves (holding the key down through the cooldown makes no error sound: only a press does)", scanner.Pulses == pulses0 + 2 && LootScanner.ErrorBeeps == beeps0, $"(pulses {scanner.Pulses - pulses0}, beeps {LootScanner.ErrorBeeps - beeps0})");'
Rep '                    int pulses1 = scanner.Pulses;' '                    int pulses1 = scanner.Pulses, beeps1 = LootScanner.ErrorBeeps;'
Rep '"T21: pressing again straight after a scan does not skip the cooldown", scanner.Pulses == pulses1, $"(pulses {scanner.Pulses - pulses1}, ready {LootScanner.ScanReady:0.00})");' '"T21: pressing again straight after a scan does not skip the cooldown and plays the error sound once", scanner.Pulses == pulses1 && LootScanner.ErrorBeeps == beeps1 + 1, $"(pulses {scanner.Pulses - pulses1}, beeps {LootScanner.ErrorBeeps - beeps1}, ready {LootScanner.ScanReady:0.00})");'
Rep '                    int pulses2 = scanner.Pulses;' '                    int pulses2 = scanner.Pulses, beeps2 = LootScanner.ErrorBeeps;'
Rep '"T21: the first press after a rest scans at once and the screen asks to wait 2 seconds", scanner.Pulses == pulses2 + 1 && LootScanner.WaitSeconds == 2, $"(pulses {scanner.Pulses - pulses2}, wait {LootScanner.WaitSeconds})");' '"T21: the first press after a rest scans at once, with no error sound", scanner.Pulses == pulses2 + 1 && LootScanner.ErrorBeeps == beeps2, $"(pulses {scanner.Pulses - pulses2}, beeps {LootScanner.ErrorBeeps - beeps2})");'
Rep '"T21: crossing into another room with the key held does not skip the cooldown: still one scan, and the screen asks to wait 1 second", scanner.Pulses == pulses2 + 1 && LootScanner.WaitSeconds == 1 && LootScanner.ScanReady < 0.9f, $"(pulses {scanner.Pulses - pulses2}, wait {LootScanner.WaitSeconds}, ready {LootScanner.ScanReady:0.00})");' '"T21: crossing into another room with the key held does not skip the cooldown: still one scan, no pop-up (the old marks went with the room) and no error sound (the key was not pressed again)", scanner.Pulses == pulses2 + 1 && scanner.PopupText == "" && LootScanner.ErrorBeeps == beeps2 && LootScanner.ScanReady < 0.9f, $"(pulses {scanner.Pulses - pulses2}, pop-up ''{scanner.PopupText}'', beeps {LootScanner.ErrorBeeps - beeps2}, ready {LootScanner.ScanReady:0.00})");'
Rep '"T21: when the 2 seconds are over the next scan leaves", scanner.Pulses == pulses2 + 2, $"(pulses {scanner.Pulses - pulses2}, wait {LootScanner.WaitSeconds})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(0.2f);' '"T21: when the 2 seconds are over the next scan leaves", scanner.Pulses == pulses2 + 2, $"(pulses {scanner.Pulses - pulses2}, ready {LootScanner.ScanReady:0.00})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(2.6f);
                    A23("T21: the pop-up is gone 2 seconds after the last scan, though its marks last longer", scanner.PopupText == "", $"(pop-up ''{scanner.PopupText}'')");'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevEtr.cs')
Rep 'EtrCheck("scan: the top of the screen names the room and counts what is in it"' 'EtrCheck("scan: the scan reads the room and counts what is in it (not drawn: the only pop-up is Item Marked)"'
Rep 'EtrCheck("scan: in a hallway it says so and finds nothing in it' 'EtrCheck("scan: in a hallway it reads as the hallway and finds nothing in it'
Save-Src
"scan popup patched"
