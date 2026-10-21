# Round 12, batch G58: the Intro's opening camera after the second real-window pictures (u6): from the north-east corner the five walked TOWARD the camera in a bunch (the row lies along the line of sight again). The opening shot now
# starts closer on the circle (from the north side of the break room, the counters on the right, the dark TV on the left), holds there while they talk, and from a second before the TV comes on eases over to the "behind the workers"
# set-up (exactly where the next shot, DockShot.Behind, begins, so the cut to the boss's first line is seamless): the TV clicks on and the five walk across the picture into the line in front of it, seen from behind.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\Intro\IntroCinematic.cs'
RepRx '/\* Establish \*/ new Rig\([^\r\n]*' @'
/* Establish */ new Rig(new Vector3(0.9f, 2.0f, 3.9f), new Vector3(1.2f, 2.0f, 4.1f), new Vector3(-1.2f, 1.3f, 2.2f), new Vector3(-0.15f, 1.9f, 0.3f), 66f, 46f),          // (the break room from its north side: the circle on the right in front of the counters, the dark TV on the left; it holds there while they talk and eases over to the Behind set-up - its first pose - while the TV comes on and they line up)
'@
RepRx 'if \(beat\.dock == DockShot\.Establish\) p = [^\r\n]*' @'
if (beat.dock == DockShot.Establish) p = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(IntroRules.TvOnAt - 1.2f, EstablishSeconds, t));          // (it holds on the circle, then eases over to the Behind set-up from a second before the TV comes on to the end of the TV's first beat)
'@
Save-Src
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevEtr.cs'
Rep @'
            yield return Snap("intro_tv_opening_talk");
'@ @'
            yield return Snap("intro_tv_opening_talk");
            intro.TestSeek(IntroRules.TvOnAt - 0.35f);
            yield return new WaitForSeconds(0.25f);
            yield return Snap("intro_tv_opening_last_moment");
'@
Rep @'
            yield return Snap("intro_tv_gather_arriving");
'@ @'
            yield return Snap("intro_tv_gather_arriving");
            intro.TestSeek(IntroRules.Beats[IntroRules.FirstBossBeat].start + 0.35f);
            yield return new WaitForSeconds(0.25f);
            yield return Snap("intro_tv_gather_lined_up");
'@
Save-Src
Write-Output 'round 12 batch G58 patched'
