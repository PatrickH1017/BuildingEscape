# Round 4, batch 3: the elevator's closing pop-up counts down (A1) and its doors take 2 seconds to open (A2). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\ElevatorExtraction.cs')
Rep @'
            Announce("Doors closing in 2 seconds...");
            yield return new WaitForSeconds(CloseDelay);
'@ @'
            int steps = Mathf.Max(1, Mathf.CeilToInt(CloseDelay));            // a countdown on every machine (the owner, 2026-10-17 night: "elevator door closing pop-up should be a countdown")
            for (int left = steps; left >= 1; left--)
            {
                Announce($"Doors closing in {left}");
                yield return new WaitForSeconds(CloseDelay / steps);
            }
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\ElevatorDoor.cs')
Rep '        public float speed = 1.6f; // 1 = fully open per second' @'
        public float speed = 1.6f; // 1 = fully open per second (the doors CLOSE this fast; opening takes OpenSeconds)
        /// <summary>How long the doors take to open (the owner, 2026-10-17 night: "elevator door opening should also take 2 seconds").</summary>
        public const float OpenSeconds = 2f;

        /// <summary>How far open (0..1) a door is after <paramref name="dt"/> seconds of moving from <paramref name="k"/> towards <paramref name="target"/>: opening takes <see cref="OpenSeconds"/>, closing runs at <paramref name="closeSpeed"/> per second.</summary>
        public static float Advance(float k, float target, float dt, float closeSpeed) => Mathf.MoveTowards(k, target, (target > k ? 1f / OpenSeconds : closeSpeed) * dt);
'@
Rep '            k = Mathf.MoveTowards(k, target, speed * Time.deltaTime);' '            k = Advance(k, target, Time.deltaTime, speed);'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep '        static bool PaintedNumbersCheck()' @'
        /// <summary>A1 / A2 of round 4: the elevator doors take 2 seconds to open (and close at their old speed), the pop-up before they close counts down over 2 seconds.</summary>
        public static void ElevatorTimingTest()
        {
            float k = 0f; int frames = 0;
            while (k < 1f && frames < 1000) { k = ElevatorDoor.Advance(k, 1f, 1f / 60f, 1.6f); frames++; }
            float kAt19 = 0f; for (int i = 0; i < 114; i++) kAt19 = ElevatorDoor.Advance(kAt19, 1f, 1f / 60f, 1.6f);       // 1.9 s
            float c = 1f; int closeFrames = 0;
            while (c > 0f && closeFrames < 1000) { c = ElevatorDoor.Advance(c, 0f, 1f / 60f, 1.6f); closeFrames++; }
            Check("elevator: the doors take 2 seconds to open (not open yet after 1.9 s, open at 2 s) and the same 0.63 s as before to close",
                ElevatorDoor.OpenSeconds == 2f && frames >= 119 && frames <= 121 && kAt19 > 0.9f && kAt19 < 0.96f && closeFrames >= 37 && closeFrames <= 39, $"(open in {frames} frames, 1.9 s: {kAt19:0.00}, close in {closeFrames} frames)");
            Check("elevator: the pop-up before the doors close counts down over the 2 second delay", ElevatorExtraction.CloseDelay == 2f);
        }

        static bool PaintedNumbersCheck()
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevTest.cs')
Rep '            RolesTest();' @'
            RolesTest();
            ElevatorTimingTest();
'@
Save-Src
Write-Output 'round 4 batch 3 patched'
