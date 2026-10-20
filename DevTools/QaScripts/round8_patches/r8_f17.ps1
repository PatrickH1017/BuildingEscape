# Round 8, batch F17 (the owner, 2026-10-18: "Please also make the UI main color blue instead of purple and make it a bit cleaner/less cartooney but don't stray from the current look too much as it looks good just not quite there.
# Please create this UI as a separate test UI, don't fully implement it yet until we confirm we like it"):
# the BLUE look is built next to the classic one and switched by HudTheme.Style (UiStyle.Classic = the code as it was, the default; UiStyle.Blue = the test): HudTheme, MenuKit and HudWidgets draw both, so one switch changes
# every screen built from them (the HUD, the lobby and Esc menus, the inventory, the vendor...). The switch is in Settings ("UI LOOK", lobby and Esc menu) and in the F1 test menu; it is saved in PlayerPrefs. The blue look is the
# earlier "cleaner UI" pass (DevTools\CleanerUI_2026-10-17: thinner ink, a smaller fainter shadow, a quieter gloss, a slimmer rim, a thin even text outline, quiet section tags) in a blue palette instead of purple.
# The pictures: the harness scenarios with a Blue entry (UiBlue, InvUiBlue, DemoUiBlue, MenusBlue, MenusLobbyBlue, MenusRunBlue) preview the blue look without saving it as the player's choice.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}

FromDraft "$Src\Hud\HudTheme.cs" 'C:\bqs\ui_draft\HudTheme.cs'
FromDraft "$Src\Hud\MenuKit.cs" 'C:\bqs\ui_draft\MenuKit.cs'

Open-Src "$Src\Hud\HudWidgets.cs"
Rep @'
                HudTheme.Text(bar, m.nextQuota > 0 ? nextQuotaText.Get(m.nextQuota) : m.demoDone ? "DEMO COMPLETE" : "BUY AND GET READY", 14f, HudTheme.Bright, TextAnchor.MiddleCenter);
'@ @'
                string dockText = m.nextQuota > 0 ? nextQuotaText.Get(m.nextQuota) : m.demoDone ? "DEMO COMPLETE" : "BUY AND GET READY";
                if (HudTheme.Blue) HudTheme.Text(bar, dockText, 14f, HudTheme.Ink, TextAnchor.MiddleCenter, false);        // (the Blue look: ink on the yellow bar reads better than outlined white)
                else HudTheme.Text(bar, dockText, 14f, HudTheme.Bright, TextAnchor.MiddleCenter);
'@
Rep @'
                HudTheme.Text(bar, label, 14f, HudTheme.Bright, TextAnchor.MiddleCenter);
'@ @'
                HudTheme.BarLabel(bar, m.met ? 1f : f, label, 14f, 2.5f);
'@
Rep @'
            Color wc = f > 0.9f ? HudTheme.Danger : f > 0.65f ? HudTheme.Warning : HudTheme.Good;
'@ @'
            Color wc = f > 0.9f ? HudTheme.Danger : f > 0.65f ? HudTheme.Warning : HudTheme.Good;
            Color wt = HudTheme.Blue && f > 0.9f ? HudTheme.Light(HudTheme.Danger, 0.4f) : wc;            // (the Blue look: on the dark card a lighter red reads, the bar keeps the strong one)
'@
Rep '15f, wc, TextAnchor.MiddleRight);' '15f, wt, TextAnchor.MiddleRight);'
Rep 'm.unlimited ? HudTheme.Good : accent, TextAnchor.MiddleRight);' 'm.unlimited ? HudTheme.Good : HudTheme.Blue && accent == HudTheme.Danger ? HudTheme.Light(accent, 0.4f) : accent, TextAnchor.MiddleRight);'
Rep 'HudTheme.Circle(centre, R * 2f, new Color32(0x2B, 0x20, 0x68, 255));' 'HudTheme.Circle(centre, R * 2f, HudTheme.Track);'
Save-Src

Open-Src "$Src\Hud\HudIcons.cs"
Rep 'HudTheme.Circle(c, hole * 2f, new Color32(0x2B, 0x20, 0x68, 255));' 'HudTheme.Circle(c, hole * 2f, HudTheme.Track);'
Save-Src

Open-Src "$Src\Hud\SettingsPanel.cs"
Rep 'public static readonly Color Violet = new Color32(0x8A, 0x5C, 0xFF, 255);' 'public static Color Violet => HudTheme.Violet;'
Rep @'
        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };
'@ @'
        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };
        /// <summary>The two looks of the UI (the owner, 2026-10-18: the blue one is a test; Classic is what the game has had).</summary>
        static readonly string[] LookNames = { "CLASSIC", "BLUE (TEST)" };
'@
Rep @'
            return MenuKit.Button(f.Row(56f, 8f), "KEYBINDS", HudTheme.Info, 24f);
'@ @'
            f.Gap(6f);
            MenuKit.Header(f.Row(28f, 8f), "UI LOOK", HudTheme.Info);
            int look = MenuKit.Choice(f, LookNames, (int)HudTheme.Style, 2, HudTheme.Quota, 44f, 18f);
            if (look != (int)HudTheme.Style) HudTheme.Style = (UiStyle)look;
            f.Gap(4f);

            return MenuKit.Button(f.Row(56f, 8f), "KEYBINDS", HudTheme.Info, 24f);
'@
Save-Src

Open-Src "$Src\Net\LobbyMenu.cs"
Rep 'static readonly Color Violet = new Color32(0x8A, 0x5C, 0xFF, 255);' 'static Color Violet => HudTheme.Violet;'
Save-Src

Open-Src "$Src\TestMode.cs"
Rep @'
            Header("Weapons (into your pack, equipped)");
'@ @'
            if (GUILayout.Button(HudTheme.Blue ? "UI look: BLUE (test) - click for Classic" : "UI look: CLASSIC - click for the Blue test look")) HudTheme.Style = HudTheme.Blue ? UiStyle.Classic : UiStyle.Blue;
            Header("Weapons (into your pack, equipped)");
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        public static void Armed() => Start("armed");
'@ @'
        // the same picture scenarios with the Blue look on trial (the owner, 2026-10-18): HudTheme.Preview, never saved as the player's choice
        static void WithBlue(System.Action scenario) { SessionState.SetInt("netdev.uistyle", 1); scenario(); }
        public static void UiBlue() => WithBlue(Ui);
        public static void InvUiBlue() => WithBlue(InvUi);
        public static void DemoUiBlue() => WithBlue(DemoUi);
        public static void MenusBlue() => WithBlue(Menus);
        public static void MenusLobbyBlue() => WithBlue(MenusLobby);
        public static void MenusRunBlue() => WithBlue(MenusRun);
        public static void Armed() => Start("armed");
'@
Rep @'
            started = Time.realtimeSinceStartup;
'@ @'
            started = Time.realtimeSinceStartup;
            { int uiStyle = SessionState.GetInt("netdev.uistyle", -1); if (uiStyle >= 0) HudTheme.Preview(uiStyle); }          // (the Blue entries: the look on trial, for the pictures)
'@
Save-Src
Write-Output 'round 8 batch F17 (the blue test look next to the classic one) patched'
