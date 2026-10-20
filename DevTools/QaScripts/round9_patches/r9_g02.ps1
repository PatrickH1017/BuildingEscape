# Round 9, batch G02 (the owner, 2026-10-18: "Yes, add the SMG, rifle, shotgun and sniper guns from the Synty pack"): a picture scenario of the candidate guns (GunSheet), so one model can be chosen for each kind.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Ed = 'C:\bqm\Assets\Editor'

# the new file (LF, like NetDevWeaponSheet.cs next to it)
$draft = [IO.File]::ReadAllText('C:\bqs\gun_draft\NetDevGunSheet.cs').Replace("`r`n", "`n").TrimEnd("`n") + "`n"
$target = "$Ed\NetDev\NetDevGunSheet.cs"
if ($env:R2_DRY) { Write-Output "dry run: would write $target ($($draft.Length) chars)" }
else { [IO.File]::WriteAllText($target, $draft, (New-Object Text.UTF8Encoding($false))); Write-Output "written $target" }

Open-Src "$Ed\NetDev\NetDevHarness.cs"
Rep @'
        public static void MeleeLook()
        {
            SessionState.SetString("netdev.mode", "meleelook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@ @'
        public static void MeleeLook()
        {
            SessionState.SetString("netdev.mode", "meleelook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void GunSheet()
        {
            SessionState.SetString("netdev.mode", "gunsheet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
'@
Rep @'
                case "meleelook": yield return MeleeLookScenario(); break;
'@ @'
                case "meleelook": yield return MeleeLookScenario(); break;
                case "gunsheet": yield return GunSheetScenario(); break;
'@
Save-Src
