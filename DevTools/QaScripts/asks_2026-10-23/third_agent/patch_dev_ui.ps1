# The owner, 2026-10-24: "Clicking CTRL + F1 with the terminal open does not close it and it should"; "If the player types [the code] into the terminal again it should change back to the non-dev UI"; "when a dev types [the code] it not only opens up that dev menu but it changes
# the UI by adding extra features for devs, such as a spectate button on the lobby where we can access that spectate menu feature"; his answers: the code stays @Catdog813 (ETRDevMenu was only a name for it); a DEV badge, a dev menu button in the Esc menu, and NO Steam ID line on
# the spectate page ("that might make it easy for a hacker to get dev perms").
# Patches the scratch copy C:\bd; exact matches only (patchlib); the new file is written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'dev_new\NetDevDevUi.cs') ($A + 'Editor\NetDev\NetDevDevUi.cs')

# ---------------------------------------------------------------- the terminal: CTRL + F1 closes it; the code again locks
Open-Src ($A + 'Scripts\Dev\DevTerminal.cs')
Rep 'static void ResetStatics() { Open = false; instance = null; }' 'static void ResetStatics() { Open = false; instance = null; lastToggleFrame = -1; }'
Rep '        /// <summary>Tests: set it and the terminal opens or closes on the next frame, as if CTRL + F1 was pressed.</summary>' @'
        static int lastToggleFrame = -1;

        /// <summary>
        /// CTRL + F1: opens the terminal, or closes it when it is open (the owner, 2026-10-24: "Clicking CTRL + F1 with the terminal open does not close it and it should"). The key is looked for twice - in Update (the Input
        /// class) and in OnGUI (the GUI's own key event, which still arrives while the text field has the keyboard) - and however many times it is seen in one frame it is one press.
        /// </summary>
        public static void Toggle()
        {
            if (instance == null || lastToggleFrame == Time.frameCount) return;
            lastToggleFrame = Time.frameCount;
            SetOpen(!Open);
        }

        /// <summary>Tests: set it and the terminal opens or closes on the next frame, as if CTRL + F1 was pressed.</summary>
'@
Rep @'
            if (TestToggle) { TestToggle = false; SetOpen(!Open); return; }
            if (ctrl && Input.GetKeyDown(KeyCode.F1)) { SetOpen(!Open); return; }
'@ @'
            if (TestToggle) { TestToggle = false; Toggle(); return; }
            if (ctrl && Input.GetKeyDown(KeyCode.F1)) { Toggle(); return; }
'@
Rep @'
                if (DevAccess.Unlocked) { Print("Already unlocked."); return; }
                DevAccess.Unlock();
                TestMode.EnsureExists();
                Print("Unlocked. F1 opens the dev menu.");
                return;
'@ @'
                if (DevAccess.Unlocked)                                                    // (the owner, 2026-10-24: typing the code again switches back to the normal UI)
                {
                    DevAccess.Lock();
                    TestMode.CloseMenu();
                    Print("Locked. The normal UI is back.");
                    return;
                }
                DevAccess.Unlock();
                TestMode.EnsureExists();
                Print("Unlocked. F1 opens the dev menu; the lobby has a SPECTATE button.");
                return;
'@
Rep @'
            if (!Open) return;
            var ev = Event.current;
'@ @'
            if (!Open) return;
            var ev = Event.current;
            if (ev.type == EventType.KeyDown && ev.keyCode == KeyCode.F1 && ev.control) { Toggle(); ev.Use(); return; }          // (CTRL + F1 as the GUI sees it: it closes the terminal while the text field has the keyboard)
'@
Save-Src

# ---------------------------------------------------------------- TestMode: the Esc menu's DEV MENU button opens the dev menu
Open-Src ($A + 'Scripts\TestMode.cs')
Rep @'
        /// <summary>Closes the dev menu (the terminal's "lock").</summary>
'@ @'
        /// <summary>Opens the dev menu (the Esc menu's DEV MENU button, the owner, 2026-10-24); not in a network session (the cheats create things the sync does not know about).</summary>
        public static void OpenMenu()
        {
            if (!DevAccess.Unlocked || Net.NetSession.Online) return;
            EnsureExists();
            var t = FindFirstObjectByType<TestMode>();
            if (t) { t.open = true; panelOpen = true; }
        }

        /// <summary>Closes the dev menu (the terminal's "lock").</summary>
'@
Save-Src

# ---------------------------------------------------------------- the lobby: SPECTATE page + DEV tab
Open-Src ($A + 'Scripts\Net\LobbyMenu.cs')
Rep '        enum Page { Main, Play, Browse, Customize, Settings, HowTo, Invite, Keybinds }' '        enum Page { Main, Play, Browse, Customize, Settings, HowTo, Invite, Keybinds, Spectate }'
Rep '        string codeInput = "";' @'
        string codeInput = "";
        string spectateCode = "", spectateStatus = "";          // (the dev UI's spectate page)
'@
Rep @'
                case Page.HowTo:
                case Page.Invite: page = Page.Main; break;
'@ @'
                case Page.HowTo:
                case Page.Spectate:
                case Page.Invite: page = Page.Main; break;
'@
Rep '            if (net.State != NetSessionState.Offline && (page == Page.Play || page == Page.Browse)) page = Page.Main;' @'
            if (net.State != NetSessionState.Offline && (page == Page.Play || page == Page.Browse || page == Page.Spectate)) page = Page.Main;
            if (page == Page.Spectate && !DevAccess.Unlocked) page = Page.Main;                 // (the code was typed again: back to the normal UI, the owner, 2026-10-24)
'@
Rep '                case Page.Invite: return "INVITE FRIENDS";' @'
                case Page.Invite: return "INVITE FRIENDS";
                case Page.Spectate: return "SPECTATE";
'@
Rep '            else if (page == Page.Invite) DrawInvite(net, f);' @'
            else if (page == Page.Invite) DrawInvite(net, f);
            else if (page == Page.Spectate) DrawSpectate(net, f);
'@
Rep '            if (MenuKit.Button(f.Row(62f, 14f), "SETTINGS", Violet, 28f)) page = Page.Settings;' @'
            if (MenuKit.Button(f.Row(62f, 14f), "SETTINGS", Violet, 28f)) page = Page.Settings;
            if (DevAccess.Unlocked && MenuKit.Button(f.Row(62f, 14f), "SPECTATE", HudTheme.Danger, 28f)) { page = Page.Spectate; GUI.FocusControl(null); }          // (the dev UI: the owner, 2026-10-24)
'@
Rep @'
            DrawFooter(net, new Rect(panel.x + pad, panel.yMax - pad - footerH, panel.width - pad * 2f, footerH));
            HudAnim.End(mat);
'@ @'
            DrawFooter(net, new Rect(panel.x + pad, panel.yMax - pad - footerH, panel.width - pad * 2f, footerH));
            if (DevAccess.Unlocked) HudTheme.Chip(new Rect(panel.xMax - 86f * s, panel.yMax - 14f * s, 74f * s, 28f * s), "DEV", HudTheme.Danger, 15f);          // (the dev UI is on: a tab on the card's lower edge)
            HudAnim.End(mat);
'@
Rep '        static void Quit()' @'
        /// <summary>
        /// The dev UI's spectate page (the owner, 2026-10-24: "a spectate button on the lobby where we can access that spectate menu feature"): the lobby code of the match to watch and a button; the terminal's <c>spectate CODE</c> does the same. The host lets a
        /// developer in only when Steam says his account is on the list (<c>DevAccounts</c>), before the run starts. NO Steam ID and no list answer is shown here (the owner: that might make it easy for a hacker to get dev permissions).
        /// </summary>
        void DrawSpectate(NetSession net, MenuKit.Flow f)
        {
#if ETR_DEV_TOOLS || UNITY_EDITOR
            MenuKit.Header(f.Row(28f, 8f), "WATCH A MATCH", HudTheme.Danger);
            MenuKit.Label(f.Row(22f, 6f), "the lobby code of the match, before its run starts", 15f, HudTheme.Muted);
            var row = f.Row(52f, 12f);
            var box = MenuKit.Cell(row, 0, 2, 10f);
            spectateCode = MenuKit.TextBox(box, spectateCode, NetConst.CodeLength, "spectatecode", "LOBBY CODE").ToUpperInvariant();
            if (MenuKit.Button(MenuKit.Cell(row, 1, 2, 10f), "SPECTATE", HudTheme.Danger, 19f)) spectateStatus = net.SpectateByCode(spectateCode);
            if (spectateStatus.Length > 0)
            {
                float s = HudTheme.S;
                float hh = MenuKit.WrappedHeight(spectateStatus, 15f * s, f.w - 24f * s) + 14f * s;
                var plate = f.Row(hh / s, 8f);
                HudTheme.Rounded(plate, HudTheme.Alpha(HudTheme.Warning, 0.3f), 12f * s);
                MenuKit.Wrapped(new Rect(plate.x + 12f * s, plate.y + 7f * s, plate.width - 24f * s, plate.height), spectateStatus, 15f * s, HudTheme.Bright);
            }
#endif
        }

        static void Quit()
'@
Save-Src

# ---------------------------------------------------------------- the Esc menu: DEV tab + DEV MENU button
Open-Src ($A + 'Scripts\EscapeMenu.cs')
Rep '            bool online = Net.NetSession.Online;' @'
            bool online = Net.NetSession.Online;
            bool dev = DevAccess.Unlocked && !online;                    // (the dev UI: a DEV MENU button; the owner, 2026-10-24)
'@
Rep '(online ? 56f + 12f : 0f) + (56f + 16f) + 28f + 26f;' '(online ? 56f + 12f : 0f) + (dev ? 56f + 12f : 0f) + (56f + 16f) + 28f + 26f;'
Rep '            MenuKit.Ribbon(panel, "MENU", HudTheme.Info);' @'
            MenuKit.Ribbon(panel, "MENU", HudTheme.Info);
            if (DevAccess.Unlocked) HudTheme.Chip(new Rect(panel.xMax - 86f * s, panel.yMax - 14f * s, 74f * s, 28f * s), "DEV", HudTheme.Danger, 15f);          // (the dev UI is on: a tab on the card's lower edge)
'@
Rep '            if (MenuKit.Button(f.Row(56f, 12f), "KEYBINDS", HudTheme.Info, 26f)) controlsOpen = true;' @'
            if (MenuKit.Button(f.Row(56f, 12f), "KEYBINDS", HudTheme.Info, 26f)) controlsOpen = true;
#if ETR_DEV_TOOLS || UNITY_EDITOR
            if (dev && MenuKit.Button(f.Row(56f, 12f), "DEV MENU", HudTheme.Danger, 26f)) { Open = false; TestMode.OpenMenu(); }
#endif
'@
Rep '            MenuKit.Ribbon(panel, "SETTINGS", HudTheme.Info);' @'
            MenuKit.Ribbon(panel, "SETTINGS", HudTheme.Info);
            if (DevAccess.Unlocked) HudTheme.Chip(new Rect(panel.xMax - 86f * s, panel.yMax - 14f * s, 74f * s, 28f * s), "DEV", HudTheme.Danger, 15f);
'@
Save-Src

# ---------------------------------------------------------------- the scenario
Open-Src ($A + 'Editor\NetDev\NetDevHarness.cs')
Rep '        public static void MenusLobby() => Start("menuslobby");' @'
        public static void MenusLobby() => Start("menuslobby");
        public static void DevUi() => Start("devui");
'@
Rep '                case "menuslobby": yield return MenusLobbyScenario(); break;' @'
                case "menuslobby": yield return MenusLobbyScenario(); break;
                case "devui": yield return DevUiScenario(); break;
'@
Save-Src

# the Esc menu in dev mode: a picture of it in the AudioDevicesUi scenario
Open-Src ($A + 'Editor\NetDev\NetDevAudioDevices.cs')
Rep '            yield return Snap("esc_main");' @'
            yield return Snap("esc_main");
            DevAccess.Unlock();                                                          // (the dev UI: the DEV tab and the DEV MENU button)
            yield return new WaitForSeconds(0.6f);
            yield return Snap("esc_dev");
            DevAccess.Lock();
'@
Save-Src
"dev ui patched"
