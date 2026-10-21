# Round 12, batch G44c: the dress sheet scenario (the Construction pack's head attachments on a bare builder) - harness entry, mode and the no-menu list.
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep @"
        public static void CharSheet()
        {
"@ @"
        public static void DressSheet()
        {
            SessionState.SetString("netdev.mode", "dresssheet");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void CharSheet()
        {
"@
Rep '                case "charsheet": yield return CharSheetScenario(); break;' @"
                case "dresssheet": yield return DressSheetScenario(); break;
                case "charsheet": yield return CharSheetScenario(); break;
"@
Rep 'mode != "charsheet" && mode != "gunitems"' 'mode != "charsheet" && mode != "dresssheet" && mode != "gunitems"'
Save-Src
