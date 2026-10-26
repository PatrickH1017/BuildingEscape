# The dev menu (F1 panel) has a leave-run button too (the owner, 2026-10-24: "the dev mode should also have a leave run button"): in a run it does what the Esc menu's LEAVE RUN does (EscapeMenu.LeaveRun) and closes the panel.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Scripts\TestMode.cs')
Rep '            GUILayout.Label($"Cash: ${(hm ? hm.Cash : 0)}");' @'
            if (EscapeMenu.ShowsLeaveRun(false, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene) && GUILayout.Button("Leave run"))          // (the owner, 2026-10-24: the dev menu has a leave-run button too)
            {
                open = false; panelOpen = false;
                EscapeMenu.LeaveRun();
                GUIUtility.ExitGUI();
            }
            GUILayout.Label($"Cash: ${(hm ? hm.Cash : 0)}");
'@
Save-Src
"dev panel leave run patched"
