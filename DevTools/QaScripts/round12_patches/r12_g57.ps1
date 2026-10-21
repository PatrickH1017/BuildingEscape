# Round 12, batch G57: the Intro's opening after the first real-window pictures (u5): the line-up was filmed end-on from the north-west corner (the row runs along x, the camera looked along it, so the five read as a
# column). The circle now stands on the WEST side of the break room, in front of the counters (2.2 m west of the TV's middle, 2.6 m out), and the opening camera stands in its north-east corner looking west-south-west:
# the circle on the right of the picture, the TV on the left, and the row they walk into seen side-on. They walk a little faster (1.7 m/s) so the last one is in the line as the boss starts.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\Intro\IntroRules.cs'
Rep 'public const float WalkSpeed = 1.6f, NoticeSeconds = 0.3f, ArrivalGap = 0.15f;' 'public const float WalkSpeed = 1.7f, NoticeSeconds = 0.3f, ArrivalGap = 0.15f;'
Save-Src
Open-Src 'C:\bqm\Assets\Scripts\Intro\IntroCinematic.cs'
Rep '/* Establish */ new Rig(new Vector3(-3.2f, 2.3f, 3.9f), new Vector3(-2.7f, 2.2f, 3.7f), new Vector3(1.1f, 1.2f, 2.3f), new Vector3(1.0f, 1.25f, 2.2f), 62f, 56f),          // (the break room from its north-west corner: the circle on the right, the TV on the wall beyond; one move over the two beats of the opening)' '/* Establish */ new Rig(new Vector3(2.9f, 2.3f, 3.6f), new Vector3(2.5f, 2.2f, 3.3f), new Vector3(-1.2f, 1.25f, 2.3f), new Vector3(-0.9f, 1.3f, 2.0f), 64f, 58f),          // (the break room from its north-east corner looking west-south-west: the circle on the right in front of the counters, the TV on the left, the row they walk into side-on; one move over the two beats of the opening)'
Rep '1.3 m east of the TV''s middle and 3.2 m' '2.2 m west of the TV''s middle and 2.6 m'
Rep 'in the free east half of the break room (the counters, the fridge and the desk are on its west side).' 'on the west side of the break room in front of the counters (the fridge, the desk and the chairs are further west; the camera of the opening looks at it from the north-east corner).'
Rep 'const float RingSide = 1.3f, RingOut = 3.2f;' 'const float RingSide = -2.2f, RingOut = 2.6f;'
Rep 'if (a.anim) a.anim.Locomote(walking, 0.75f);' 'if (a.anim) a.anim.Locomote(walking, 0.85f);'
Save-Src
Write-Output 'round 12 batch G57 patched'
