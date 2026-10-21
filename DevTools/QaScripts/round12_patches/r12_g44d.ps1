# Round 12, batch G44d: the theme sheet scenario (a cap, a hard hat and goggles in each of the Construction pack's four colour themes) - harness entry, mode, no-menu list.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep @"
        public static void DressSheet()
        {
"@ @"
        public static void ThemeSheet()
        {
            SessionState.SetString("netdev.mode", "themesheet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void DressSheet()
        {
"@
Rep '                case "dresssheet": yield return DressSheetScenario(); break;' @"
                case "themesheet": yield return ThemeSheetScenario(); break;
                case "dresssheet": yield return DressSheetScenario(); break;
"@
Rep 'mode != "dresssheet" && mode != "gunitems"' 'mode != "dresssheet" && mode != "themesheet" && mode != "gunitems"'
Save-Src
