# Round 12, batch G01: the asset preview scenario is registered (contact sheets of the plane, car and city prefabs the owner added on 2026-10-19).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$p = 'C:\bqm\Assets\Editor\NetDev\NetDevAssetPreview.cs'
if (-not $env:R2_DRY) { $t = [IO.File]::ReadAllText($p); [IO.File]::WriteAllText($p, ($t -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false))) }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep 'mode != "armour" && mode != "checklist"' 'mode != "armour" && mode != "assetpreview" && mode != "checklist"'
Rep @'
        public static void Checklist()
'@ @'
        public static void AssetPreview()
        {
            SessionState.SetString("netdev.mode", "assetpreview");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void Checklist()
'@
Rep @'
                case "checklist": yield return ChecklistScenario(); break;
'@ @'
                case "checklist": yield return ChecklistScenario(); break;
                case "assetpreview": yield return AssetPreviewScenario(); break;
'@
Save-Src
