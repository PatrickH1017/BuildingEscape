# Two asks of 2026-10-24 (the owner): (1) "the dev menu button doesn't have to be in the button list it can be off to the side if it has to as the dev menu is not normally there anyway ... whichever seems to work better please implement":
# the DEV MENU button stands BESIDE the Esc card now (right of it, left of it when the window is too narrow), so the card is as tall as it was with LEAVE RUN only. (2) "please put a poster or two up in the Loading Docks break room, reference image attached for which spots":
# DockPosterRules + two posters in BuildDockRooms (the west wall over the desk, the south wall west of the TV). New files: dock_posters\DockPosterRules.cs (Scripts) and dock_posters\NetDevDockPosters.cs (Editor\NetDev).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

foreach ($pair in @(@('DockPosterRules.cs', 'Scripts\DockPosterRules.cs'), @('NetDevDockPosters.cs', 'Editor\NetDev\NetDevDockPosters.cs'))) {
  $src = Join-Path $here ('dock_posters\' + $pair[0]); $dst = $A + $pair[1]
  if ($Dry) { Write-Output "dry ok would copy $src -> $dst" } else { Copy-Item $src $dst -Force; Write-Output "copied $($pair[0])" }
}

# ---- the Esc menu: the DEV MENU button beside the card
Open-Src ($A + 'Scripts\EscapeMenu.cs')
Rep '(leave ? 56f + 12f : 0f) + (dev ? 56f + 12f : 0f)' '(leave ? 56f + 12f : 0f)'
Rep @'
#if ETR_DEV_TOOLS || UNITY_EDITOR
            if (dev && MenuKit.Button(f.Row(56f, 12f), "DEV MENU", HudTheme.Danger, 26f)) { Open = false; TestMode.ToggleMenu(); }          // (opens the dev menu, or closes it when it is up: like F1)
#endif
'@ ''
Rep '            MenuKit.Notice(f.Row(28f, 0f), "THE GAME KEEPS RUNNING", HudTheme.Shade(HudTheme.Card, 0.3f), 13f);
            HudAnim.End(mat);' @'
            MenuKit.Notice(f.Row(28f, 0f), "THE GAME KEEPS RUNNING", HudTheme.Shade(HudTheme.Card, 0.3f), 13f);
#if ETR_DEV_TOOLS || UNITY_EDITOR
            if (dev)
            {
                // the dev menu's button stands beside the card, not in its list (the owner, 2026-10-24: "it can be off to the side ... the dev menu is not normally there anyway"): right of the card, left of it when the window is too narrow
                float bw = 200f * s, bh = 56f * s, gap = 16f * s;
                float bx = panel.xMax + gap;
                if (bx + bw > Screen.width - 8f * s) bx = Mathf.Max(8f * s, panel.x - gap - bw);
                if (MenuKit.Button(new Rect(bx, panel.y + 70f * s, bw, bh), "DEV MENU", HudTheme.Danger, 26f)) { Open = false; TestMode.ToggleMenu(); }          // (opens the dev menu, or closes it when it is up: like F1)
            }
#endif
            HudAnim.End(mat);
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevMenuLayout.cs')
Rep 'float need = 696f * s;                                                         // (the main page with LEAVE RUN and DEV MENU: the tallest)' 'float need = 630f * s;                                                         // (the main page with LEAVE RUN: the tallest; the DEV MENU button stands beside the card)'
Rep '"menu: the main Esc card (696 tall at most, with LEAVE RUN and DEV MENU) stands in the same band wherever it fits in it"' '"menu: the main Esc card (630 tall at most, with LEAVE RUN; DEV MENU stands beside it) stands in the same band wherever it fits in it"'
Save-Src

# ---- the posters
Open-Src ($A + 'Scripts\FloorGenerator.DockRooms.cs')
Rep '            PlaceKey("sign_wc", rdx, BreakRoom.yMax - 0.02f, 180f, parent, 2.25f, false);' @'
            PlaceKey("sign_wc", rdx, BreakRoom.yMax - 0.02f, 180f, parent, 2.25f, false);
            // two posters (the owner, 2026-10-24: "please put a poster or two up in the Loading Docks break room, reference image attached for which spots"): the west wall over the desk and the south wall between the TV and the corner (DockPosterRules)
            foreach (var poster in DockPosterRules.BreakRoom(BreakRoom.xMin, BreakRoom.yMin, BreakRoom.xMax, BreakRoom.yMax))
            {
                if (!PropTable.TryGet(poster.key, out var pd)) continue;
                float outX = Mathf.Sin(poster.yaw * Mathf.Deg2Rad), outZ = Mathf.Cos(poster.yaw * Mathf.Deg2Rad);          // (the way it faces: into the room)
                PlaceKey(poster.key, poster.x + outX * (pd.D * 0.5f + 0.005f), poster.z + outZ * (pd.D * 0.5f + 0.005f), poster.yaw, parent, poster.centreY - pd.H * 0.5f, false);
            }
'@
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            AlarmRulesTest();' '            AlarmRulesTest();
            DockPosterRulesTest();'
Save-Src

Open-Src ($A + 'Editor\NetDev\NetDevAlarmLooks.cs')
Rep '            HeistManager.TestPartySize = 0;
            yield return LoadLevel(hm, 1);' '            HeistManager.TestPartySize = 0;
            yield return DockPosterChecks();                                                                // (the basement''s break room: its two posters, the owner, 2026-10-24)
            yield return LoadLevel(hm, 1);'
Save-Src
"esc dev side button and dock posters patched"
