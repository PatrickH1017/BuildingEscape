# The built-player self-play (QaAutopilot) checks the alarm of 2026-10-24 in the BUILT game: the loudspeakers are flat discs on the ceiling, the alarm beacons are in the build (the pack's lamp via Resources/AlarmBeacon), dark on a quiet floor and lit with the Big Alarm, and the intense
# music is chosen. (The dev code is NOT typed here: the build holds only its hash, and a plain copy of the word in the game would defeat that.)
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\QaAutopilot.cs')
Rep '                yield return EtrFloorChecks(floor, gen, hm, tm);' @'
                {
                    var ceilSpeakers = FindObjectsByType<LoudSpeaker>(FindObjectsSortMode.None);
                    int ceilFlat = 0;
                    foreach (var cs in ceilSpeakers) { var dsk = cs.transform.Find("Disc"); var dskRend = dsk ? dsk.GetComponent<Renderer>() : null; if (dskRend && dskRend.bounds.max.y >= gen.CeilingUnderside - 0.08f && dskRend.bounds.max.y <= gen.CeilingUnderside) ceilFlat++; }
                    var cornerBeacons = gen.Beacons;
                    Check($"floor {floor}: the hallway loudspeakers are round discs flat on the ceiling and the alarm beacons hang dark in the corners (the owner, 2026-10-24)", ceilSpeakers.Length >= 1 && ceilFlat == ceilSpeakers.Length && cornerBeacons != null && cornerBeacons.Count >= 4 && cornerBeacons.Level == 0f && cornerBeacons.LiveLights == 0, $"({ceilFlat} of {ceilSpeakers.Length} speakers flat, {(cornerBeacons != null ? cornerBeacons.Count : -1)} beacons)");
                }
                yield return EtrFloorChecks(floor, gen, hm, tm);
'@
Rep ', $"(big {hm.BigAlarmActive}, cap {hm.GuardCap})");' @'
, $"(big {hm.BigAlarmActive}, cap {hm.GuardCap})");
                yield return Wait(1.3f);
                var alarmBeacons = gen.Beacons;
                Check($"floor {floor}: with the Big Alarm the beacons (the pack's lamp, in the build) are lit and cast red light, and the intense music is chosen", alarmBeacons != null && alarmBeacons.FromPack && alarmBeacons.Level > 0.99f && alarmBeacons.LiveLights >= 1 && MusicDirector.Instance != null && MusicDirector.Instance.Current == MusicDirector.Mood.Intense, $"(from pack {(alarmBeacons != null && alarmBeacons.FromPack)}, level {(alarmBeacons != null ? alarmBeacons.Level : -1f):0.00}, {(alarmBeacons != null ? alarmBeacons.LiveLights : -1)} lights, mood {(MusicDirector.Instance != null ? MusicDirector.Instance.Current.ToString() : "none")})");
'@
Save-Src
"autopilot alarm patched"
