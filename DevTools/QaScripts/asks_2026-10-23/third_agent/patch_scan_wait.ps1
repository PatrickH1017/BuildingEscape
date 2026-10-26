# The owner, 2026-10-24: "If I hold scan while moving the loading bar for cooldown is not accurate to when the scan reactivates. Instead the cooldown should say 'Wait x seconds' rounded to the nearest integer for seconds (either 1 or 2 since it's a 2 second cooldown)".
# The cause: crossing into another room reset the scan's clock (RefreshRoom: sinceLast = -1, "a new pulse at once"), so the cooldown was skipped and the bar jumped to full. The cooldown (sinceUse) is now its own clock that runs whatever the player does, and the bar is replaced by
# the words "Wait 2 seconds" / "Wait 1 second" (LootScanRules.WaitSeconds / WaitText).
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }

Open-Src ($Root + 'Scripts\LootScanRules.cs')
RepRegion '        public static float ReadyFraction(float sinceLast) =>' 'Math.Min(1f, Math.Max(0f, sinceLast / PulseGap));' @'
        public static float ReadyFraction(float sinceLast) => sinceLast < 0f || PulseGap <= 0f ? 1f : Math.Min(1f, Math.Max(0f, sinceLast / PulseGap));

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
'@.TrimEnd()
Save-Src

Open-Src ($Root + 'Scripts\LootScanner.cs')
Rep 'public static float ScanReady { get; private set; } = 1f;' @'
public static float ScanReady { get; private set; } = 1f;
        /// <summary>The whole seconds to wait for the next scan (<see cref="LootScanRules.WaitSeconds"/>): 0 when it can leave; the screen shows "Wait n seconds".</summary>
        public static int WaitSeconds { get; private set; }
'@
Rep 'float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck;' @'
float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck;
        float sinceUse = -1f;                       // the cooldown's own clock: seconds since the last scan left (-1: none), whatever room the player is in
'@
Rep 'Scanning = false; ScanReady = 1f; }' 'Scanning = false; ScanReady = 1f; WaitSeconds = 0; }'
Rep @'
            if (can && (Time.time >= nextRoomCheck || holding && sinceLast < 0f)) { nextRoomCheck = Time.time + 0.15f; RefreshRoom(me); }
            if (hm == null || hm.Floor < 1) { room = new RoomSpot { index = -1 }; ClearMarks(); StopPulse(); }

            if (sinceLast >= 0f) sinceLast += dt;
            if (LootScanRules.NextPulse(holding, sinceLast)) StartPulse(me);
            ScanReady = LootScanRules.ReadyFraction(sinceLast);
'@ @'
            // the cooldown runs on its own clock (the owner, 2026-10-24: "If I hold scan while moving the loading bar for cooldown is not accurate to when the scan reactivates"): crossing into another room used to reset it, so a scan left at once
            if (sinceUse >= 0f) sinceUse += dt;
            bool ready = LootScanRules.NextPulse(holding, sinceUse);
            if (can && (Time.time >= nextRoomCheck || ready)) { nextRoomCheck = Time.time + 0.15f; RefreshRoom(me); }
            if (hm == null || hm.Floor < 1) { room = new RoomSpot { index = -1 }; ClearMarks(); StopPulse(); sinceUse = -1f; }

            if (sinceLast >= 0f) sinceLast += dt;
            if (ready) StartPulse(me);
            ScanReady = LootScanRules.ReadyFraction(sinceUse);
            WaitSeconds = LootScanRules.WaitSeconds(sinceUse);
'@
Rep @'
        void StartPulse(Transform me)
        {
            sinceLast = 0f;
'@ @'
        void StartPulse(Transform me)
        {
            sinceLast = 0f; sinceUse = 0f;
'@
RepRegion '                if (ScanReady < 1f) HudTheme.Bar(' 'the next scan is ready when it is full)' @'
                if (Scanning && WaitSeconds > 0)                                                 // (the cooldown, in words: "Wait 2 seconds" / "Wait 1 second", under the scan's line; the owner, 2026-10-24)
                {
                    string wait = LootScanRules.WaitText(WaitSeconds);
                    float ww = HudTheme.Width(wait, 17f) + 44f * s;
                    var wr = new Rect((Screen.width - ww) * 0.5f, r.yMax + 6f * s + (offScreen > 0 ? 32f * s : 0f), ww, 32f * s);
                    HudTheme.Pill(wr, new Color(0.62f, 0.34f, 0.08f, 0.95f), 2.5f, 2.5f);
                    HudTheme.Text(wr, wait, 17f, HudTheme.Bright, TextAnchor.MiddleCenter, false);
                }
'@.TrimEnd()
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src ($Root + 'Editor\NetDev\NetDevAsks1023Test.cs')
Rep '            Check("scan (T21): the bar of the cooldown fills evenly' @'
            Check("scan (T21): the screen asks to wait 2 seconds right after a scan, 1 second in the second half, nothing when a scan can leave (the owner, 2026-10-24: rounded to the nearest whole second, 1 or 2)",
                LootScanRules.WaitSeconds(0f) == 2 && LootScanRules.WaitSeconds(0.4f) == 2 && LootScanRules.WaitSeconds(0.5f) == 2 && LootScanRules.WaitSeconds(0.6f) == 1 && LootScanRules.WaitSeconds(1.4f) == 1 && LootScanRules.WaitSeconds(1.99f) == 1 && LootScanRules.WaitSeconds(2f) == 0 && LootScanRules.WaitSeconds(9f) == 0 && LootScanRules.WaitSeconds(-1f) == 0);
            Check("scan (T21): the words are 'Wait 2 seconds' and 'Wait 1 second' (nothing to wait: no words)", LootScanRules.WaitText(2) == "Wait 2 seconds" && LootScanRules.WaitText(1) == "Wait 1 second" && LootScanRules.WaitText(0) == "");
            Check("scan (T21): the bar of the cooldown fills evenly
'@.TrimEnd()
Save-Src
Open-Src ($Root + 'Editor\NetDev\NetDevAsks1023.cs')
Rep @'
                    A23("T21: once the cooldown is over the held key scans again", scanner.Pulses == pulses1 + 1, $"(pulses {scanner.Pulses - pulses1})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(0.2f);
'@ @'
                    A23("T21: once the cooldown is over the held key scans again", scanner.Pulses == pulses1 + 1, $"(pulses {scanner.Pulses - pulses1})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(2.2f);                                           // (rested: the cooldown is over)

                    // the owner, 2026-10-24: holding the scan key while MOVING: crossing into another room must not skip the cooldown, and the screen says how long to wait
                    var roomPoint = new Vector3(room.bounds.Cx, 1.1f, room.bounds.Cz);
                    int pulses2 = scanner.Pulses;
                    LootScanner.TestKeyDown = true;
                    yield return new WaitForSeconds(0.3f);
                    A23("T21: the first press after a rest scans at once and the screen asks to wait 2 seconds", scanner.Pulses == pulses2 + 1 && LootScanner.WaitSeconds == 2, $"(pulses {scanner.Pulses - pulses2}, wait {LootScanner.WaitSeconds})");
                    player.TeleportTo(TowerManager.Instance.CurrentInfo.spawn + Vector3.up * 1.1f, Quaternion.identity);          // (the elevator car: another "room")
                    yield return new WaitForSeconds(0.5f);
                    player.TeleportTo(roomPoint, Quaternion.Euler(0f, 90f, 0f));                      // (and back into the room: a room change twice, the key held all the time)
                    yield return new WaitForSeconds(0.5f);
                    A23("T21: crossing into another room with the key held does not skip the cooldown: still one scan, and the screen asks to wait 1 second", scanner.Pulses == pulses2 + 1 && LootScanner.WaitSeconds == 1 && LootScanner.ScanReady < 0.9f, $"(pulses {scanner.Pulses - pulses2}, wait {LootScanner.WaitSeconds}, ready {LootScanner.ScanReady:0.00})");
                    yield return new WaitForSeconds(1.0f);
                    A23("T21: when the 2 seconds are over the next scan leaves", scanner.Pulses == pulses2 + 2, $"(pulses {scanner.Pulses - pulses2}, wait {LootScanner.WaitSeconds})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(0.2f);
'@
Save-Src
"scan wait patched"
