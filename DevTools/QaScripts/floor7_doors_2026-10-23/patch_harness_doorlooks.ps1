# Registers the DoorLooks picture scenario in the scratch copy's NetDevHarness (method, mode list, switch case). Exact matches only (patchlib).
. C:\bqs\patchlib.ps1
$SC = 'C:\bd\Assets\Editor\NetDev\NetDevHarness.cs'
Open-Src $SC
Rep '        public static void CoreLooks()' ('        public static void DoorLooks()' + "`n" + '        {' + "`n" + '            SessionState.SetString("netdev.mode", "doorlooks");' + "`n" + '            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");' + "`n" + '            EditorApplication.isPlaying = true;' + "`n" + '        }' + "`n" + '        public static void CoreLooks()')
Rep '&& mode != "citylook")' '&& mode != "citylook" && mode != "doorlooks")'
Rep '                case "corelooks": yield return CoreLooksScenario(); break;' ('                case "corelooks": yield return CoreLooksScenario(); break;' + "`n" + '                case "doorlooks": yield return DoorLooksScenario(); break;')
Save-Src
