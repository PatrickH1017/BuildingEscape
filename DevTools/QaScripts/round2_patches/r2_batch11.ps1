# Round 2, batch 11: the computer's screen and post-it colours; protocol 13. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Outpost.cs')
Rep 'case "postit": m = Surface("OpPostIt", new Color(1f, 0.90f, 0.35f), 0.05f, false); break;' 'case "postit": m = Surface("OpPostIt", new Color(0.90f, 0.80f, 0.30f), 0.05f, true); break;       // (it glows a little: a post-it is meant to be seen in a dim room)'
Rep 'case "screen": m = Surface("OpScreen", new Color(0.30f, 0.55f, 0.95f), 0f, true); break;' 'case "screen": m = Surface("OpScreen", new Color(0.10f, 0.22f, 0.40f), 0f, true); break;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Net\NetConst.cs')
Rep 'public const string ProtocolVersion = "12";   // 12: Employees Take Revenge' 'public const string ProtocolVersion = "13";   // 13: the pew lies in the outpost of floor 3 (not 4) and the vendor sells it after floor 3, the outposts have a computer instead of a note, the floors have carpet, windows, a city and glass fronts (a floor is built differently, so old and new cannot share a run); 12: Employees Take Revenge'
Save-Src
Write-Output 'batch 11 patched'
