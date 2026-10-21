# Round 12, batch G50: the built-player autopilot (solo) also goes into first person in the docks and back (the owner, 2026-10-20: first person): the arms shader must be IN the build (it lives in Resources), the body must be drawn as its arms and
# third person must bring it back; the picture 02b_first_person of the built game is looked at by a person. The start line of the report says whether the shader is there.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Scripts\QaAutopilot.cs'
Rep 'elevator mirror {(Shader.Find("BuildingEscape/ElevatorMirror") != null)}");' 'elevator mirror {(Shader.Find("BuildingEscape/ElevatorMirror") != null)}, first-person arms {(Shader.Find("BuildingEscape/FirstPersonArms") != null)}");'
Rep '            yield return Shot("02_docks_arrival");' @'
            yield return Shot("02_docks_arrival");
            {
                // first person (the owner, 2026-10-20): the arms shader is in the build and the body is drawn as its arms; third person brings it back
                var fpRig = FindFirstObjectByType<CameraFollow>();
                bool fpShader = Shader.Find("BuildingEscape/FirstPersonArms") != null;
                if (fpRig != null)
                {
                    fpRig.SetFirstPerson(true);
                    yield return Wait(1.8f);
                    bool fpArms = fpRig.IsFirstPerson && fpRig.ArmsShown;
                    yield return Shot("02b_first_person");
                    fpRig.SetFirstPerson(false);
                    yield return Wait(1.2f);
                    Check("first person works in the built game: the arms shader is in the build, the body is drawn as its arms; third person brings it back", fpShader && fpArms && !fpRig.IsFirstPerson && !fpRig.ArmsShown, $"(shader {fpShader}, arms shown in first person {fpArms}, back in third person {!fpRig.IsFirstPerson && !fpRig.ArmsShown})");
                }
                else Check("first person works in the built game: there is a camera rig", false);
            }
'@
Save-Src
Write-Output 'round 12 batch G50 patched'
