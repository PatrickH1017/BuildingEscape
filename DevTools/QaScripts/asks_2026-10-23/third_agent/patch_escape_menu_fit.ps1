# The owner, 2026-10-24: "Please scale down the size of the Escape menu UI so that it doesn't overlay with the other UIs". The Esc menu's cards (the main page and the Settings page) stand in the band of the screen that the run's HUD keeps clear:
# below the floor's banner and the line under it, above the hotbar and the stamina bar (MenuLayoutRules); the Settings card is 780 tall at most (it was 880) and scrolls what does not fit.
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
Put-Crlf (Join-Path $here 'menu_new\MenuLayoutRules.cs') ($A + 'Scripts\Hud\MenuLayoutRules.cs')
Put-Crlf (Join-Path $here 'menu_new\NetDevMenuLayout.cs') ($A + 'Editor\NetDev\NetDevMenuLayout.cs')

Open-Src ($A + 'Scripts\EscapeMenu.cs')
Rep @'
            float pw = Mathf.Min(540f * s, Screen.width - 40f * s), ph = Mathf.Min(need * s, Screen.height - 40f * s);
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
'@ @'
            float pw = Mathf.Min(540f * s, Screen.width - 40f * s), ph = Mathf.Min(need * s, Screen.height - 40f * s);
            MenuLayoutRules.Band(Screen.height, s, out float bandTop, out float bandH);          // (the owner, 2026-10-24: the card must not overlay the floor's banner or the hotbar)
            var panel = new Rect((Screen.width - pw) * 0.5f, MenuLayoutRules.CardY(bandTop, bandH, ph, Screen.height), pw, ph);
'@
Rep @'
            float pw = Mathf.Min(540f * s, Screen.width - 40f * s), ph = Mathf.Min(880f * s, Screen.height - 40f * s);
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f, pw, ph);
'@ @'
            float pw = Mathf.Min(540f * s, Screen.width - 40f * s);
            MenuLayoutRules.Band(Screen.height, s, out float bandTop, out float bandH);          // (the owner, 2026-10-24: the card must not overlay the floor's banner or the hotbar; what does not fit scrolls)
            float ph = MenuLayoutRules.SettingsHeight(s, bandH);
            var panel = new Rect((Screen.width - pw) * 0.5f, MenuLayoutRules.CardY(bandTop, bandH, ph, Screen.height), pw, ph);
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            AudioDevicesTest();' @'
            AudioDevicesTest();
            MenuLayoutTest();
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevAudioDevices.cs')
Rep @'
            EscapeMenu.CloseAll();
            AudioDevices.TestInputs = null;
'@ @'
            EscapeMenu.OpenForTests(false);                                              // (the main page: the card in the band between the banner and the hotbar)
            yield return new WaitForSeconds(0.6f);
            yield return Snap("esc_main");
            EscapeMenu.CloseAll();
            AudioDevices.TestInputs = null;
'@
Save-Src
"escape menu fit patched"
