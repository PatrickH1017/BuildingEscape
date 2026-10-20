# Round 8, batch F15: the melee-in-hand close-up scenario (MeleeLook) is registered in the harness (ui.ps1 -Methods MeleeLook).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep @'
        public static void PewLook()
        {
            SessionState.SetString("netdev.mode", "pewlook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@ @'
        public static void PewLook()
        {
            SessionState.SetString("netdev.mode", "pewlook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void MeleeLook()
        {
            SessionState.SetString("netdev.mode", "meleelook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@
Rep '&& mode != "weaponsheet" &&' '&& mode != "weaponsheet" && mode != "meleelook" &&'
Rep @'
                case "weaponsheet": yield return WeaponSheetScenario(); break;
'@ @'
                case "weaponsheet": yield return WeaponSheetScenario(); break;
                case "meleelook": yield return MeleeLookScenario(); break;
'@
Save-Src
Write-Output 'round 8 batch F15 (MeleeLook scenario registered) patched'
