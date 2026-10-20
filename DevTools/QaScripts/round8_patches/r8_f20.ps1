# Round 8, batch F20 (the owner, 2026-10-18: "The UI looks good please implement it"): the blue look becomes THE look. The test scaffolding of batch F17 goes: UiStyle, HudTheme.Style / Preview / Blue, the Classic branches of
# HudTheme / MenuKit / HudWidgets, the "UI LOOK" switch in Settings, the F1 button, the harness's *Blue picture entries, and the pure test of the two looks (replaced by a test of the one palette).
# The earlier look is in the repository's git history and in DevTools\QaBackups\before_round8m_apply_2026-10-18 (the files ship.ps1 replaces).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'
$Ed = 'C:\bqm\Assets\Editor'

function RepBetween([string]$startMarker, [string]$endMarker, [string]$replacement) {
  $a = $script:s.IndexOf($startMarker)
  if ($a -lt 0) { throw "start marker not found: $startMarker" }
  if ($script:s.IndexOf($startMarker, $a + 1) -ge 0) { throw "start marker twice: $startMarker" }
  $b = $script:s.IndexOf($endMarker, $a)
  if ($b -lt 0) { throw "end marker not found: $endMarker" }
  $new = $replacement.Replace("`r`n", "`n").Replace("`n", $script:eol)
  $script:s = $script:s.Substring(0, $a) + $new + $script:s.Substring($b)
}
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}

FromDraft "$Src\Hud\HudTheme.cs" 'C:\bqs\ui_final\HudTheme.cs'
FromDraft "$Src\Hud\MenuKit.cs" 'C:\bqs\ui_final\MenuKit.cs'

Open-Src "$Src\Hud\HudWidgets.cs"
Rep @'
                if (HudTheme.Blue) HudTheme.Text(bar, dockText, 14f, HudTheme.Ink, TextAnchor.MiddleCenter, false);        // (the Blue look: ink on the yellow bar reads better than outlined white)
                else HudTheme.Text(bar, dockText, 14f, HudTheme.Bright, TextAnchor.MiddleCenter);
'@ @'
                HudTheme.Text(bar, dockText, 14f, HudTheme.Ink, TextAnchor.MiddleCenter, false);        // (ink on the yellow bar reads better than outlined white)
'@
Rep @'
            Color wt = HudTheme.Blue && f > 0.9f ? HudTheme.Light(HudTheme.Danger, 0.4f) : wc;            // (the Blue look: on the dark card a lighter red reads, the bar keeps the strong one)
'@ @'
            Color wt = f > 0.9f ? HudTheme.Light(HudTheme.Danger, 0.4f) : wc;            // (on the dark card a lighter red reads, the bar keeps the strong one)
'@
Rep 'm.unlimited ? HudTheme.Good : HudTheme.Blue && accent == HudTheme.Danger ? HudTheme.Light(accent, 0.4f) : accent, TextAnchor.MiddleRight);' 'm.unlimited ? HudTheme.Good : accent == HudTheme.Danger ? HudTheme.Light(accent, 0.4f) : accent, TextAnchor.MiddleRight);'
Save-Src

Open-Src "$Src\Hud\SettingsPanel.cs"
Rep @'
        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };
        /// <summary>The two looks of the UI (the owner, 2026-10-18: the blue one is a test; Classic is what the game has had).</summary>
        static readonly string[] LookNames = { "CLASSIC", "BLUE (TEST)" };
'@ @'
        static readonly string[] VoiceModes = { "OPEN MIC", "PUSH TO TALK (T)", "OFF" };
'@
Rep @'
            f.Gap(6f);
            MenuKit.Header(f.Row(28f, 8f), "UI LOOK", HudTheme.Info);
            int look = MenuKit.Choice(f, LookNames, (int)HudTheme.Style, 2, HudTheme.Quota, 44f, 18f);
            if (look != (int)HudTheme.Style) HudTheme.Style = (UiStyle)look;
            f.Gap(4f);

            return MenuKit.Button(f.Row(56f, 8f), "KEYBINDS", HudTheme.Info, 24f);
'@ @'
            return MenuKit.Button(f.Row(56f, 8f), "KEYBINDS", HudTheme.Info, 24f);
'@
Save-Src

Open-Src "$Src\TestMode.cs"
Rep @'
            if (GUILayout.Button(HudTheme.Blue ? "UI look: BLUE (test) - click for Classic" : "UI look: CLASSIC - click for the Blue test look")) HudTheme.Style = HudTheme.Blue ? UiStyle.Classic : UiStyle.Blue;
            Header("Weapons (into your pack, equipped)");
'@ @'
            Header("Weapons (into your pack, equipped)");
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        // the same picture scenarios with the Blue look on trial (the owner, 2026-10-18): HudTheme.Preview, never saved as the player's choice
        static void WithBlue(System.Action scenario) { SessionState.SetInt("netdev.uistyle", 1); scenario(); }
        public static void UiBlue() => WithBlue(Ui);
        public static void InvUiBlue() => WithBlue(InvUi);
        public static void DemoUiBlue() => WithBlue(DemoUi);
        public static void MenusBlue() => WithBlue(Menus);
        public static void MenusLobbyBlue() => WithBlue(MenusLobby);
        public static void MenusRunBlue() => WithBlue(MenusRun);
        public static void Armed() => Start("armed");
'@ @'
        public static void Armed() => Start("armed");
'@
Rep @'
            started = Time.realtimeSinceStartup;
            { int uiStyle = SessionState.GetInt("netdev.uistyle", -1); if (uiStyle >= 0) HudTheme.Preview(uiStyle); }          // (the Blue entries: the look on trial, for the pictures)
'@ @'
            started = Time.realtimeSinceStartup;
'@
Save-Src

Open-Src "$Ed\NetDev\NetDevR7Test.cs"
RepBetween '            // ---- the two looks of the UI' '            // ---- the terminal: OUTPOST LOCK / OUTPOST UNLOCK work the door of the outpost itself (the owner, 2026-10-18)' @'
            // ---- the look of the UI (the owner, 2026-10-18: "make the UI main color blue instead of purple ... The UI looks good please implement it")
            {
                float HueOf(Color c) { Color.RGBToHSV(c, out float h, out _, out _); return h * 360f; }
                bool InBlue(Color c) { float h = HueOf(c); return h >= 195f && h <= 245f; }
                bool Is(Color c, int rgb) { var k = (Color32)c; return k.r == ((rgb >> 16) & 255) && k.g == ((rgb >> 8) & 255) && k.b == (rgb & 255); }
                bool blueIsBlue = InBlue(HudTheme.Card) && InBlue(HudTheme.Ink) && InBlue(HudTheme.Muted) && InBlue(HudTheme.Dim) && InBlue(HudTheme.Track) && InBlue(HudTheme.Tag) && InBlue(HudTheme.Violet);
                bool meaningsKept = Is(HudTheme.Cash, 0x45E07C) && Is(HudTheme.Danger, 0xFF4046) && Is(HudTheme.Quota, 0xFFC61F);       // (money stays green, danger red, the quota gold)
                Check("ui: the UI is blue - the card, the ink, the muted and dim text, the bar track, the tags and the secondary buttons all have a blue hue, none is purple - and money / danger / the quota keep their colours",
                    blueIsBlue && meaningsKept, $"(blue {blueIsBlue}, meanings {meaningsKept})");
            }

'@
Save-Src
Write-Output 'round 8 batch F20 (the blue look is the only look) patched'
