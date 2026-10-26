# The owner, 2026-10-24: "Add a Press tab for inventory above the Press J for journal": the hint at the bottom left gets a second line, "Press Tab for inventory", above "Press J for journal".
# Patches the scratch copy C:\bd; exact matches only (patchlib).
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

Open-Src ($A + 'Scripts\Outpost\IntelScreens.cs')
Rep '        /// <summary>"Press J for journal" at the bottom left (the owner, 2026-10-24) while a floor is being played and no screen is up.</summary>' '        /// <summary>"Press J for journal" at the bottom left (the owner, 2026-10-24), with "Press Tab for inventory" above it, while a floor is being played and no screen is up.</summary>'
Rep '            HudTheme.Text(new Rect(18f * s, Screen.height - 46f * s, 360f * s, 30f * s), "Press J for journal", 16f, HudTheme.Muted, TextAnchor.MiddleLeft);' @'
            HudTheme.Text(new Rect(18f * s, Screen.height - 72f * s, 360f * s, 30f * s), "Press Tab for inventory", 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
            HudTheme.Text(new Rect(18f * s, Screen.height - 46f * s, 360f * s, 30f * s), "Press J for journal", 16f, HudTheme.Muted, TextAnchor.MiddleLeft);
'@
Save-Src
"inventory hint patched"
