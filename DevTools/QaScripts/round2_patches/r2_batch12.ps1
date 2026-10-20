# Round 2, batch 12: the ComputerUi picture scenario (the computer's screen). Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'
if (-not $env:R2_DRY) { Copy-Item (Join-Path $PSScriptRoot 'stage_glass\NetDevComputerUi.cs') (Join-Path $Dir 'Editor\NetDev\NetDevComputerUi.cs') -Force }

Open-Src (Join-Path $Dir 'Scripts\Outpost\ComputerScreen.cs')
Rep '        /// <summary>Clicks a tab (the tests; the screen''s own button does the same). Only a player who is logged in can.</summary>' @'
        /// <summary>Puts words in the two boxes without pressing Log in (the picture tests).</summary>
        public static void SetTyped(string typedUser, string typedPass) { if (Showing) { Instance.user = typedUser ?? ""; Instance.pass = typedPass ?? ""; } }

        /// <summary>Clicks a tab (the tests; the screen's own button does the same). Only a player who is logged in can.</summary>
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevHarness.cs')
Rep @'
        public static void FloatQa()
        {
'@ @'
        public static void ComputerUi()
        {
            SessionState.SetString("netdev.mode", "computerui");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void FloatQa()
        {
'@
Rep '                case "floatqa": yield return FloatQaScenario(); break;' @'
                case "floatqa": yield return FloatQaScenario(); break;
                case "computerui": yield return ComputerUiScenario(); break;
'@
Rep 'mode != "floatqa" && mode != "doors"' 'mode != "floatqa" && mode != "computerui" && mode != "doors"'
Save-Src
Write-Output 'batch 12 patched'
