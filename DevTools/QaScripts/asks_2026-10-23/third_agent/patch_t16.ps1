# Batch C, part 3: T16 (at the computer: the cursor over the monitor keeps the player at it; outside the monitor the view looks around, a little and very slowly; E at the microphone speaks into it).
# The owner's words (2026-10-23): "When using the computer make it so if the players cursor is over the monitor they stay viewing it but if their cursor goes outside the monitor screen they look around with a lot of deceleration on the movement and limit range
# for looking around( when looking at the microphone the player should be able to press E to speak into it".
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($Dry) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$bd = $Root.TrimEnd('\')
function Put-New([string]$from, [string]$to) {
  if ($Dry) { Write-Output "dry run: would copy $from -> $to"; return }
  New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "copied $to"
}
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t16_new\Assets\Scripts\Outpost\DeskLookRules.cs' "$bd\Scripts\Outpost\DeskLookRules.cs"

# ---------------------------------------------------------------- the camera: the view turns towards the cursor while it is outside the monitor
Open-Src "$bd\Scripts\CameraFollow.cs"
Rep '        float aimYaw, pitch = 12f, lookYaw, lookPitch;' @'
        float aimYaw, pitch = 12f, lookYaw, lookPitch;

        float deskYaw, deskPitch;
        /// <summary>The turn of the view towards the cursor at the security computer (degrees): sideways to the right and down. Tests read it.</summary>
        public Vector2 DeskLook => new Vector2(deskYaw, deskPitch);

        /// <summary>
        /// At the security computer (the owner, 2026-10-23): with the cursor over the monitor the view stays; with the cursor outside it the view turns towards the cursor, a little (<see cref="DeskLookRules.MaxYaw"/>, <see cref="DeskLookRules.MaxPitch"/> degrees)
        /// and very slowly (<see cref="DeskLookRules.Ease"/>); back over the monitor, or with the screen shut, it eases back. It drives the free look's own offsets, which nothing else touches while a screen has the cursor.
        /// </summary>
        void DeskLookStep()
        {
            if (!ComputerScreen.TryGetMonitor(out var mon)) { deskYaw = deskPitch = 0f; return; }          // (the screen is shut: the usual return takes over from wherever the view is)
            var cursor = ComputerScreen.CursorPosition;
            DeskLookRules.Target(cursor.x, Screen.height - cursor.y, mon.x, mon.y, mon.width, mon.height, Screen.width, Screen.height, out float wantYaw, out float wantPitch);
            float dt = Time.unscaledDeltaTime;
            deskYaw = DeskLookRules.Ease(deskYaw, wantYaw, dt);
            deskPitch = DeskLookRules.Ease(deskPitch, wantPitch, dt);
            if (wantYaw == 0f && Mathf.Abs(deskYaw) < 0.05f) deskYaw = 0f;
            if (wantPitch == 0f && Mathf.Abs(deskPitch) < 0.05f) deskPitch = 0f;
            lookYaw = deskYaw;
            lookPitch = deskPitch;
        }
'@
Rep @'
            var hm = HeistManager.Instance;
            bool menu = hm && (hm.Phase == HeistPhase.Won || hm.Phase == HeistPhase.Failed);
'@ @'
            DeskLookStep();
            var hm = HeistManager.Instance;
            bool menu = hm && (hm.Phase == HeistPhase.Won || hm.Phase == HeistPhase.Failed);
'@
Save-Src

# ---------------------------------------------------------------- the computer: it says where its monitor is, lets the keyboard go while the cursor is outside it, and E at the microphone is the PA
Open-Src "$bd\Scripts\Outpost\ComputerScreen.cs"
Rep '        public static ComputerScreen Instance { get; private set; }' @'
        public static ComputerScreen Instance { get; private set; }

        // ---- looking round at the computer (the owner, 2026-10-23, DeskLookRules): where the monitor is on the screen, and where the cursor is
        static Vector2? testCursor;
        bool cursorWasOutside;

        /// <summary>How big the screen is drawn: 1 on a 1080 pixel high screen.</summary>
        static float Scale => Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);

        /// <summary>Where the monitor is on the screen (GUI pixels, y down): the very rectangle the drawing uses, worked out from the screen's size alone (so the camera and the tests need no drawn frame to ask).</summary>
        public static Rect MonitorRect()
        {
            float s = Scale;
            float w = Mathf.Min(1160f * s, Screen.width - 60f), h = w * 0.74f;
            return new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f - 24f * s, w, h);
        }

        /// <summary>Where the monitor is while the computer's screen is up; false when it is not.</summary>
        public static bool TryGetMonitor(out Rect rect)
        {
            rect = MonitorRect();
            return Showing;
        }

        /// <summary>The cursor's place in screen pixels (y up, as Input.mousePosition): a test can put it where it likes.</summary>
        public static Vector2 CursorPosition => testCursor ?? (Vector2)Input.mousePosition;

        /// <summary>Tests: put the cursor at this screen place (y up), or null to give it back to the mouse.</summary>
        public static void SetTestCursor(Vector2? position) { testCursor = position; }

        /// <summary>Tests: the E key held down, as the keyboard would.</summary>
        public static bool TestKeyE;
'@
Rep '        static void ResetStatics() { Instance = null; }' '        static void ResetStatics() { Instance = null; testCursor = null; TestKeyE = false; }'
Rep '            bool want = OutpostUi.Current == OutpostUi.Kind.Computer && loggedIn && Input.GetKey(KeyCode.V) && !OtherScreenUp;' '            bool want = OutpostUi.Current == OutpostUi.Kind.Computer && loggedIn && (Input.GetKey(KeyCode.V) || MicKeyHeld()) && !OtherScreenUp;'
Rep '        /// <summary>For the tests: the PA key held down or let go, as the keyboard would.</summary>' @'
        /// <summary>
        /// E at the microphone (the owner, 2026-10-23: "when looking at the microphone the player should be able to press E to speak into it"): the cursor is outside the monitor (the view is looking round, nothing is typed) and the middle of the view is on the
        /// microphone's button (<see cref="DeskLookRules.AtMicrophone"/>). It is the PA, like V.
        /// </summary>
        bool MicKeyHeld()
        {
            if (!(Input.GetKey(KeyCode.E) || TestKeyE)) return false;
            if (!TryGetMonitor(out var mon)) return false;
            var c = CursorPosition;
            if (!DeskLookRules.Outside(c.x, Screen.height - c.y, mon.x, mon.y, mon.width, mon.height)) return false;          // (the cursor is over the monitor: E is a letter)
            return LookingAtMicrophone();
        }

        /// <summary>Is the middle of the view on the microphone on the desk?</summary>
        public static bool LookingAtMicrophone()
        {
            var mic = MicProp.Active; var cam = Camera.main;
            if (!mic || !cam) return false;
            var to = mic.ButtonWorld - cam.transform.position;
            return DeskLookRules.AtMicrophone(Vector3.Angle(cam.transform.forward, to), to.magnitude);
        }

        /// <summary>For the tests: the PA key held down or let go, as the keyboard would.</summary>
'@
Rep @'
            float s = Mathf.Clamp(Screen.height / 1080f, 0.75f, 2f);
            float w = Mathf.Min(1160f * s, Screen.width - 60f), h = w * 0.74f;
            var mon = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f - 24f * s, w, h);
'@ @'
            float s = Scale;
            var mon = MonitorRect();
            var cur = CursorPosition;
            bool outsideNow = DeskLookRules.Outside(cur.x, Screen.height - cur.y, mon.x, mon.y, mon.width, mon.height);
            if (outsideNow) { focusUser = false; focusTerm = false; GUI.FocusControl(null); }                  // (looking round: the keyboard goes nowhere, so E is the microphone's and not a letter)
            else if (cursorWasOutside) { focusUser = !loggedIn; focusTerm = loggedIn; }                          // (back over the monitor: the box that was being typed in takes the keyboard again)
            cursorWasOutside = outsideNow;
'@
Save-Src

# ---------------------------------------------------------------- the tests
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t16_new\Assets\Editor\NetDev\NetDevAsks1023WTest.cs' "$bd\Editor\NetDev\NetDevAsks1023WTest.cs"
Put-New 'C:\Users\phick\OneDrive\Desktop\BuildingEscape\DevTools\QaScripts\asks_2026-10-23\third_agent\t16_new\Assets\Editor\NetDev\NetDevAsks1023W.cs' "$bd\Editor\NetDev\NetDevAsks1023W.cs"
Open-Src "$bd\Editor\NetDev\NetDevTest.cs"
Rep '            Asks1023VTest();' @'
            Asks1023VTest();
            Asks1023WTest();
'@
Save-Src
Open-Src "$bd\Editor\NetDev\NetDevHarness.cs"
Rep '        public static void Asks1023V()' @'
        public static void Asks1023W()
        {
            SessionState.SetString("netdev.mode", "asks1023w");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Asks1023V()
'@
Rep '&& mode != "asks1023vui")' '&& mode != "asks1023vui" && mode != "asks1023w")'
Rep '                case "asks1023v": yield return Asks1023VScenario(); break;' @'
                case "asks1023v": yield return Asks1023VScenario(); break;
                case "asks1023w": yield return Asks1023WScenario(); break;
'@
Save-Src
Write-Output 'batch C part 3 (T16) patched'
