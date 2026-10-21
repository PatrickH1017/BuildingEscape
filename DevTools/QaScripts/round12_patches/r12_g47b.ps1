# Round 12, batch G47b: the first-person look scenario (the player in a plain arena, the real camera photographed with each gun, the crowbar and empty hands) - harness entry, mode, no-menu list.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep @"
        public static void ThemeSheet()
        {
"@ @"
        public static void FpLook()
        {
            SessionState.SetString("netdev.mode", "fplook");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void ThemeSheet()
        {
"@
Rep '                case "themesheet": yield return ThemeSheetScenario(); break;' @"
                case "fplook": yield return FpLookScenario(); break;
                case "themesheet": yield return ThemeSheetScenario(); break;
"@
Rep 'mode != "themesheet" && mode != "gunitems"' 'mode != "themesheet" && mode != "fplook" && mode != "gunitems"'
Save-Src
