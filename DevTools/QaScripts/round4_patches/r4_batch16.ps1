# Round 4, batch 16: "The old stairs signs can go and be replaced by the asset stair signs" (the owner, 2026-10-17 night): the dark dot-matrix STAIRS plate the generator hung beside each stairwell door is gone,
# the pack's STAIRS sign (twice its size, so it reads from the corridor) takes its place. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\PropTable.cs')
Rep '            Add("sign_stairs", Syn + "Props/Wall Props/SM_Prop_Sign_Stairs_01", .4f, .16f, .04f, solid: false, cat: PropCat.Mount);' '            Add("sign_stairs", Syn + "Props/Wall Props/SM_Prop_Sign_Stairs_01", .4f, .16f, .04f, solid: false, cat: PropCat.Mount);
            Add("sign_stairs_big", Syn + "Props/Wall Props/SM_Prop_Sign_Stairs_01", .4f, .16f, .04f, 2f, solid: false, cat: PropCat.Mount);        // (the stairwells'' sign: the pack''s own, twice its size)'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.cs')
Rep '                // the sign on the outside of the wall segment on the left of the door (seen from the corridor), the lamp inside on the right
                BuildStairSign(room.transform, center + wallPos + st.outDir * (t * 0.5f + 0.02f) - axis * (doorHalf + seg / 2f) + Vector3.up * (2.35f - wallHeight / 2f), st.outDir);
' '                // (the STAIRS sign on the outside of the wall segment on the left of the door is the pack''s own now: OfficePlanner.StairKit; the lamp inside on the right stays)
'
Rep '        /// <summary>The word STAIRS on a small dark plate, facing <paramref name="outDir"/> (away from the wall, into the corridor).</summary>
        void BuildStairSign(Transform parent, Vector3 pos, Vector3 outDir)
        {
            var root = new GameObject("StairSign");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(outDir, Vector3.up));       // (its +z points into the corridor)
            Part(root.transform, "Plate", new Vector3(0f, 0f, 0.015f), new Vector3(1.08f, 0.34f, 0.03f), OMat("dark"));
            DotLabel(root.transform, "Label", "STAIRS", 0.042f, new Vector3(0f, 0f, 0.034f), OMat("label"));
        }

' ''
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeThemes.cs')
Rep '            Put("emergency_axe", -1.63f, 0.95f);
            Put("sign_exit", 1.63f, 2.5f);
            Put("sign_stairs", 1.63f, 2.2f);
            Put("fire_alarm", 1.63f, 1.15f);' '            Put("sign_stairs_big", -1.63f, 2.2f);
            Put("emergency_axe", -1.63f, 0.95f);
            Put("sign_exit", 1.63f, 2.5f);
            Put("fire_alarm", 1.63f, 1.15f);'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevShaft.cs')
Rep 'Count(t => t.name == "StairSign") == 2' 'Count(t => t.name == "sign_stairs_big") == 2'
Save-Src
Write-Output 'round 4 batch 16 patched'
