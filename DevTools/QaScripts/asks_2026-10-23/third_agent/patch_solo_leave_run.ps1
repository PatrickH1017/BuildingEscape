# A LEAVE RUN button in the Esc menu of a SOLO run too (the owner, 2026-10-24: "in solo mode there is no leave run button, there should be one just like multiplayer" - "that feature should be added before this build").
# In a network session it leaves the session (as before); in a solo run it loads the menu scene, as the end screen's Exit does. Also fixes a window of the doorway test (the face ends at the doorway, the beacon keeps 0.9 m from the end: 13.1 and 16.9).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\EscapeMenu.cs')
Rep '        /// <summary>For the tests: the menu is up, on its Settings page or on its main page, as if the player had pressed Esc (and the button).</summary>' @'
        /// <summary>Does the menu have a LEAVE RUN button? In a network session, and in a solo run (any scene but the menu's): the owner, 2026-10-24: "in solo mode there is no leave run button, there should be one just like multiplayer".</summary>
        public static bool ShowsLeaveRun(bool online, bool onMenuScene) => online || !onMenuScene;

        /// <summary>LEAVE RUN: out of a network session (it ends and the menu comes back) or out of a solo run (back to the menu scene, as the end screen's Exit does).</summary>
        public static void LeaveRun()
        {
            Open = false;
            if (Net.NetSession.Online) Net.NetSession.Instance.Leave();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(Net.NetConst.MenuScene);
        }

        /// <summary>For the tests: the menu is up, on its Settings page or on its main page, as if the player had pressed Esc (and the button).</summary>
'@
Rep '            bool dev = DevAccess.Unlocked && !online;                    // (the dev UI: a DEV MENU button; the owner, 2026-10-24)' @'
            bool dev = DevAccess.Unlocked && !online;                    // (the dev UI: a DEV MENU button; the owner, 2026-10-24)
            bool leave = ShowsLeaveRun(online, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene);          // (LEAVE RUN: in a session or a solo run)
'@
Rep '(online ? 56f + 12f : 0f) + (dev ? 56f + 12f : 0f)' '(leave ? 56f + 12f : 0f) + (dev ? 56f + 12f : 0f)'
Rep @'
            if (online && MenuKit.Button(f.Row(56f, 12f), "LEAVE RUN", HudTheme.Warning, 26f))
            {
                Open = false;
                Net.NetSession.Instance.Leave();
            }
'@ @'
            if (leave && MenuKit.Button(f.Row(56f, 12f), "LEAVE RUN", HudTheme.Warning, 26f)) LeaveRun();
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevMenuLayout.cs')
Rep 'float need = 630f * s;                                                         // (the main page with LEAVE RUN: the tallest)' 'float need = 696f * s;                                                         // (the main page with LEAVE RUN and DEV MENU: the tallest)'
Rep '"menu: the main Esc card (630 tall at most) stands in the same band wherever it fits in it"' '"menu: the main Esc card (696 tall at most, with LEAVE RUN and DEV MENU) stands in the same band wherever it fits in it"'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevAlarmLooks.cs')
Rep 'if (Mathf.Abs(b.z) < 1e-3f && b.x > 13.05f && b.x < 16.95f) clearOfDoor = false;' 'if (Mathf.Abs(b.z) < 1e-3f && b.x > 13.11f && b.x < 16.89f) clearOfDoor = false;'
Rep '            // ---- the music: the existing intense loop plays while the backup is on or the Big Alarm is' @'
            // ---- the Esc menu's LEAVE RUN button: in a session and in a solo run, not on the menu scene (the owner, 2026-10-24)
            Check("esc menu: LEAVE RUN is there in a network session and in a solo run, and not on the menu scene", EscapeMenu.ShowsLeaveRun(true, false) && EscapeMenu.ShowsLeaveRun(false, false) && EscapeMenu.ShowsLeaveRun(true, true) && !EscapeMenu.ShowsLeaveRun(false, true));

            // ---- the music: the existing intense loop plays while the backup is on or the Big Alarm is
'@
Rep '            // ---- a new floor starts quiet again' @'
            // ---- the Esc menu in a solo run has a LEAVE RUN button (a picture), and it takes the player back to the menu scene (the owner, 2026-10-24: "in solo mode there is no leave run button")
            AlarmCheck("(solo run) the Esc menu shows LEAVE RUN on a floor", !Net.NetSession.Online && EscapeMenu.ShowsLeaveRun(Net.NetSession.Online, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene));
            EscapeMenu.OpenForTests(false);
            yield return new WaitForSeconds(0.5f);
            yield return AlarmSnap("4_esc_solo");
            EscapeMenu.CloseAll();

            // ---- a new floor starts quiet again
'@
Rep '            Log($"[AlarmLooks] DONE fails={alarmFails}");
            Quit(alarmFails == 0 ? 0 : 1);' @'
            // ---- the button itself: LEAVE RUN in a solo run goes back to the menu scene
            EscapeMenu.LeaveRun();
            yield return WaitFor(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene, 30, "the menu scene after LEAVE RUN");
            AlarmCheck("LEAVE RUN in a solo run takes the player back to the menu scene and closes the Esc menu", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Net.NetConst.MenuScene && !EscapeMenu.Open);

            Log($"[AlarmLooks] DONE fails={alarmFails}");
            Quit(alarmFails == 0 ? 0 : 1);
'@
Save-Src
"solo leave run patched"
