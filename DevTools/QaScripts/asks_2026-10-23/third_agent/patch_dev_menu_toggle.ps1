# The Esc menu's DEV MENU button opens the dev menu and now closes it again when it is pressed while the menu is open (the owner, 2026-10-24: "if a developer clicks the dev menu button in the escape menu to open the dev panel then while that menu is open
# the developer clicks the dev menu button in the escape menu again it should close the dev menu (similar to how clicking f1 opens and closes it)"). Test: scenario DevUi.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\TestMode.cs')
Rep '        /// <summary>Closes the dev menu (the terminal''s "lock").</summary>' @'
        /// <summary>The Esc menu's DEV MENU button (the owner, 2026-10-24): opens the dev menu, or closes it when it is open already, like F1 does.</summary>
        public static void ToggleMenu()
        {
            if (!DevAccess.Unlocked || Net.NetSession.Online) return;
            if (MenuOpen) CloseMenu(); else OpenMenu();
        }

        /// <summary>The dev menu is up (for the tests and the Esc menu).</summary>
        public static bool MenuOpen { get { var t = FindFirstObjectByType<TestMode>(); return t != null && t.open; } }

        /// <summary>Closes the dev menu (the terminal's "lock").</summary>
'@
Save-Src

Open-Src ($A + 'Scripts\EscapeMenu.cs')
Rep 'if (dev && MenuKit.Button(f.Row(56f, 12f), "DEV MENU", HudTheme.Danger, 26f)) { Open = false; TestMode.OpenMenu(); }' 'if (dev && MenuKit.Button(f.Row(56f, 12f), "DEV MENU", HudTheme.Danger, 26f)) { Open = false; TestMode.ToggleMenu(); }          // (opens the dev menu, or closes it when it is up: like F1)'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevDevUi.cs')
Rep '            // ---- typing it again switches back, and the page it was on goes with it' @'
            // ---- the Esc menu's DEV MENU button opens the dev menu and closes it again (the owner, 2026-10-24: "similar to how clicking f1 opens and closes it")
            TestMode.ToggleMenu();
            DevUiCheck("the DEV MENU button opens the dev menu", TestMode.MenuOpen && TestMode.PanelOpen);
            TestMode.ToggleMenu();
            DevUiCheck("the DEV MENU button again closes it", !TestMode.MenuOpen && !TestMode.PanelOpen);
            TestMode.ToggleMenu();
            DevUiCheck("and again opens it", TestMode.MenuOpen && TestMode.PanelOpen);
            yield return new WaitForSeconds(0.3f);
            DevUiCheck("the menu stays open while the Esc menu is shut (it is open until the button, F1 or the lock closes it)", TestMode.MenuOpen);
            EscapeMenu.OpenForTests(false);
            yield return new WaitForSeconds(0.4f);
            yield return SnapMenu("dev_2b_menu_open_esc_over");
            EscapeMenu.CloseAll();

            // ---- typing it again switches back, and the page it was on goes with it
'@
Rep '            DevUiCheck("typing the code again switches back to the normal UI", !DevAccess.Unlocked && LastTerminalLine().StartsWith("Locked"), $"(''{LastTerminalLine()}'')");' @'
            DevUiCheck("typing the code again switches back to the normal UI", !DevAccess.Unlocked && LastTerminalLine().StartsWith("Locked"), $"('{LastTerminalLine()}')");
            DevUiCheck("and locking closes an open dev menu", !TestMode.MenuOpen && !TestMode.PanelOpen);
'@
Save-Src
"dev menu toggle patched"
