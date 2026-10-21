# Round 12, batch G54: the built player's autopilot follows the Intro's new opening (the owner, 2026-10-20: a circle talking, then the walk to the line): a check that the five stand in a circle round one middle while the
# TV is dark, a picture of the circle and one of the walk to the line (the "hanging out" picture of before is the circle now).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\QaAutopilot.cs'
Rep @'
            yield return Until(() => IntroCinematic.Clock >= 1.8f, 20f, "the workers hanging out");
            yield return Shot("i0_workers_hanging_out");
'@ @'
            yield return Until(() => IntroCinematic.Clock >= 3.0f, 20f, "the workers talking in a circle");
            var introNow = IntroCinematic.Current;
            bool inACircle = introNow != null && introNow.WorkerCount == IntroRules.Workers && IntroCinematic.Clock < IntroRules.TvOnAt;
            for (int w = 0; inACircle && w < introNow.WorkerCount; w++) { var toMiddle = introNow.RingCentre - introNow.WorkerFoot(w); toMiddle.y = 0f; inACircle &= Mathf.Abs(toMiddle.magnitude - IntroRules.RingRadius) < 0.1f; }
            Check("the five workers stand in a circle round one middle while the TV is dark (the owner, 2026-10-20)", inACircle);
            yield return Shot("i0_workers_in_a_circle");
            yield return Until(() => IntroCinematic.Clock >= IntroRules.TvOnAt + 1.0f, 20f, "the workers walking to the line");
            yield return Shot("i0b_workers_walking_to_the_line");
'@
Save-Src
Write-Output 'round 12 batch G54 patched'
