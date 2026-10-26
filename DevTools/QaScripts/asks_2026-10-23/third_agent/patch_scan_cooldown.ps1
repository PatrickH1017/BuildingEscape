# The owner, 2026-10-23, after the playtest with friends: "Scanning is supposed to have a 2 second cooldown between each use not a charge up". T21 had made the scan wait 3 seconds of holding the key (a charge-up: his earlier words were
# "Scanning should have a 3 second delay"). Now: Z scans AT ONCE; the next scan can leave 2 seconds after the last one (LootScanRules.PulseGap, which was the gap between repeated pulses while the key is held: now THE cooldown, and letting go
# and pressing again does not skip it); the bar over the scan's line shows the cooldown filling.
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }

# ---------------------------------------------------------------- the rules
Open-Src ($Root + 'Scripts\LootScanRules.cs')
Rep 'public const float PulseSpeed = 28f, PulseGap = 2.4f;' @'
public const float PulseSpeed = 28f;
        /// <summary>The cooldown of the scan (the owner, 2026-10-23: "Scanning is supposed to have a 2 second cooldown between each use not a charge up"): the next scan can leave this long after the last one, whether the key is held or pressed again.</summary>
        public const float PulseGap = 2f;
'@
RepRegion '        /// <summary>The scan goes off only after the key has been held this long' 'ChargeSeconds <= 0f ? 1f : Math.Min(1f, Math.Max(0f, heldSeconds / ChargeSeconds));' @'
        /// <summary>How far the cooldown has got, 0 to 1 (1 = ready: the bar over the scan's line): <paramref name="sinceLast"/> is the time since the last scan left; negative means there was none (ready).</summary>
        public static float ReadyFraction(float sinceLast) => sinceLast < 0f || PulseGap <= 0f ? 1f : Math.Min(1f, Math.Max(0f, sinceLast / PulseGap));
'@.TrimEnd()
Save-Src

# ---------------------------------------------------------------- the scanner
Open-Src ($Root + 'Scripts\LootScanner.cs')
Rep '        /// <summary>Tests: the scan key is held like the real key - the scan then waits its <see cref="LootScanRules.ChargeSeconds"/> (<see cref="TestHold"/> is a hold that has been held long enough already).</summary>' '        /// <summary>Tests: the scan key is held like the real key (the scan goes off at once, then every <see cref="LootScanRules.PulseGap"/> while it stays down).</summary>'
Rep @'
        /// <summary>How far the held key has got towards the scan, 0 to 1 (1 once the scan has gone off and while the key stays down); 0 when the key is up.</summary>
        public static float ScanCharge { get; private set; }
'@ @'
        /// <summary>How far the cooldown has got, 0 to 1: 1 when the next scan can leave at once (<see cref="LootScanRules.ReadyFraction"/>).</summary>
        public static float ScanReady { get; private set; } = 1f;
'@
Rep 'float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck, chargeHeld;' 'float pulseAge = -1f, sinceLast = -1f, reach, nextRoomCheck;'
Rep 'Scanning = false; ScanCharge = 0f; }' 'Scanning = false; ScanReady = 1f; }'
Rep @'
            // the scan goes off only after the key has been held for LootScanRules.ChargeSeconds (the owner, 2026-10-23: "Scanning should have a 3 second delay"); letting go starts the wait over; a scripted TestHold counts as held long enough
            bool pressed = can && (Input.GetKey(ScanKey) || TestKeyDown);
            chargeHeld = pressed ? chargeHeld + dt : 0f;
            bool holding = can && (pressed || TestHold);
            bool charged = TestHold || LootScanRules.Charged(chargeHeld);
            ScanCharge = !holding ? 0f : TestHold ? 1f : LootScanRules.ChargeFraction(chargeHeld);
            Scanning = holding;

            if (can && (Time.time >= nextRoomCheck || holding && charged && sinceLast < 0f)) { nextRoomCheck = Time.time + 0.15f; RefreshRoom(me); }
'@ @'
            // the scan goes off AT ONCE when the key goes down and then at most every LootScanRules.PulseGap (the owner, 2026-10-23: "Scanning is supposed to have a 2 second cooldown between each use not a charge up"); letting go and pressing again does not skip the cooldown
            bool holding = can && (Input.GetKey(ScanKey) || TestKeyDown || TestHold);
            Scanning = holding;

            if (can && (Time.time >= nextRoomCheck || holding && sinceLast < 0f)) { nextRoomCheck = Time.time + 0.15f; RefreshRoom(me); }
'@
Rep '            if (LootScanRules.NextPulse(holding && charged, sinceLast)) StartPulse(me);' @'
            if (LootScanRules.NextPulse(holding, sinceLast)) StartPulse(me);
            ScanReady = LootScanRules.ReadyFraction(sinceLast);
'@
Rep 'if (Scanning && ScanCharge < 1f) HudTheme.Bar(new Rect(r.x, r.y - 12f * s, r.width, 8f * s), ScanCharge, new Color(0.35f, 0.85f, 1f), 2f);          // (the scan''s wait of 3 s: the bar fills over the status line)' 'if (ScanReady < 1f) HudTheme.Bar(new Rect(r.x, r.y - 12f * s, r.width, 8f * s), ScanReady, new Color(0.35f, 0.85f, 1f), 2f);          // (the cooldown of 2 s: the bar fills over the status line, the next scan is ready when it is full)'
Save-Src

# ---------------------------------------------------------------- the tests
Open-Src ($Root + 'Editor\NetDev\NetDevAsks1023Test.cs')
RepRegion '            // ---- T21: the scan waits 3 seconds of holding the key' 'LootScanRules.ChargeFraction(-1f) == 0f && LootScanRules.ChargeFraction(3f) == 1f);' @'
            // ---- T21 (changed 2026-10-23, after the playtest: "a 2 second cooldown between each use not a charge up"): the scan goes off at once, then waits its cooldown
            Check("scan (T21): the cooldown between two scans is 2 seconds, and there is no charge-up (the first scan leaves at once)", LootScanRules.PulseGap == 2f && LootScanRules.NextPulse(true, -1f) && !LootScanRules.NextPulse(false, -1f));
            Check("scan (T21): 1.99 s after the last scan the next one cannot leave, 2 s after it can, and a key not held scans never", !LootScanRules.NextPulse(true, 1.99f) && LootScanRules.NextPulse(true, 2f) && LootScanRules.NextPulse(true, 7f) && !LootScanRules.NextPulse(false, 7f));
            Check("scan (T21): the bar of the cooldown fills evenly, never below 0 or above 1, and is full when there has been no scan", Math.Abs(LootScanRules.ReadyFraction(1f) - 0.5f) < 1e-5f && LootScanRules.ReadyFraction(9f) == 1f && LootScanRules.ReadyFraction(-1f) == 1f && LootScanRules.ReadyFraction(0f) == 0f);
'@.TrimEnd()
Save-Src
Open-Src ($Root + 'Editor\NetDev\NetDevAsks1023.cs')
RepRegion '            // ---- T21: the scan waits 3 seconds of holding the key' 'scanner.Pulses == pulses1, $"(pulses {scanner.Pulses - pulses1})");' @'
            // ---- T21 (changed 2026-10-23, after the playtest): the scan goes off at once and has a 2 second cooldown
            {
                var scanner = LootScanner.Instance;
                A23("T21: the scanner exists and there is a room to stand in", scanner != null && room != null);
                if (scanner != null && room != null)
                {
                    player.TeleportTo(new Vector3(room.bounds.Cx, 1.1f, room.bounds.Cz), Quaternion.Euler(0f, 90f, 0f));
                    yield return new WaitForSeconds(1.2f);
                    int pulses0 = scanner.Pulses;
                    LootScanner.TestKeyDown = true;
                    yield return new WaitForSeconds(0.4f);
                    A23("T21: the scan goes off at once when the key goes down (no charge-up)", scanner.Pulses == pulses0 + 1 && LootScanner.Scanning, $"(pulses {scanner.Pulses - pulses0})");
                    yield return new WaitForSeconds(1.0f);
                    A23("T21: 1.4 s after it the key is still held but the cooldown holds the next scan back (the bar is not full)", scanner.Pulses == pulses0 + 1 && LootScanner.ScanReady < 0.9f, $"(pulses {scanner.Pulses - pulses0}, ready {LootScanner.ScanReady:0.00})");
                    yield return new WaitForSeconds(1.0f);
                    A23("T21: 2.4 s after the first scan, still held, the second leaves", scanner.Pulses == pulses0 + 2, $"(pulses {scanner.Pulses - pulses0})");
                    LootScanner.TestKeyDown = false;
                    yield return new WaitForSeconds(0.3f);
                    A23("T21: letting go stops the scanning", !LootScanner.Scanning);
                    int pulses1 = scanner.Pulses;
                    LootScanner.TestKeyDown = true;
                    yield return new WaitForSeconds(0.5f);
                    A23("T21: pressing again straight after a scan does not skip the cooldown", scanner.Pulses == pulses1, $"(pulses {scanner.Pulses - pulses1}, ready {LootScanner.ScanReady:0.00})");
                    yield return new WaitForSeconds(1.6f);
                    A23("T21: once the cooldown is over the held key scans again", scanner.Pulses == pulses1 + 1, $"(pulses {scanner.Pulses - pulses1})");
'@.TrimEnd()
Save-Src
"scan cooldown patched"
