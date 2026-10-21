using System.Collections;
using BuildingEscape.Net;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The run-start cinematic in an OFFLINE run, the way a person gets one in the Editor: <c>IntroOffline</c> opens the Greybox scene and presses Play
    /// (no network session at all), <c>IntroSolo</c> starts at Boot, waits for the menu and loads the run scene the way the PLAY SOLO button does.
    /// Both need a real, non-batch Editor window (IMGUI is not drawn in batch mode): run them on the hidden desktop, DEVELOPER_NOTES 8.6.
    /// Checks: the Intro starts by itself, the player is held still, a held Space skips it (1 of 1), the player is free again; a picture of the first
    /// line is saved to %TEMP%\be_intro_*.png. (Every other scenario switches the Intro off, see NetDevHarness.)
    /// Run: <c>Unity.exe -projectPath &lt;copy&gt; -executeMethod BuildingEscape.EditorTools.NetDevHarness.IntroOffline</c> (no -batchmode); the same with IntroSolo.
    /// </summary>
    public partial class NetDevRunner
    {
        int introFails;

        void IntroCheck(string name, bool ok, string detail = "")
        {
            if (!ok) introFails++;
            Log($"[Intro] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        IEnumerator SnapIntro(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_intro_" + name + ".png");
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Log($"[Intro] saved {path} ({tex.width}x{tex.height}, screen {Screen.width}x{Screen.height})");
            Object.Destroy(tex);
        }

        IEnumerator IntroOfflineScenario(bool fromMenu)
        {
            ResetGameViewSize();
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            if (fromMenu)
            {
                yield return WaitFor(() => NetSession.Instance != null && SceneManager.GetActiveScene().name == NetConst.MenuScene, 30, "the menu");
                yield return new WaitForSeconds(1f);
                SceneManager.LoadScene(NetConst.RunScene);                      // exactly what the PLAY SOLO button does
            }
            Log($"[Intro] offline run: online={NetSession.Online}, scene {SceneManager.GetActiveScene().name}, screen {Screen.width}x{Screen.height}");

            // the Intro must come by itself once the docks are built; a run without it never gets here
            float waited = 0f;
            while (!IntroCinematic.Playing && waited < 90f) { waited += Time.unscaledDeltaTime; yield return null; }
            IntroCheck("the Intro starts by itself in an offline run", IntroCinematic.Playing, $"(waited {waited:0.0} s)");
            if (!IntroCinematic.Playing)
            {
                var hm0 = HeistManager.Instance; var tm0 = TowerManager.Instance;
                Log($"[Intro] state: director {(IntroCinematic.Current != null)}, docks ready {(hm0 != null && tm0 != null && !tm0.Loading && hm0.Floor == 0 && hm0.Phase != HeistPhase.Lobby)}, players {WobblyPlayerRegistry.All.Count}");
                Log($"[Intro] DONE fails={introFails}");
                Quit(1);
                yield break;
            }
            IntroCheck("it is a run without a network session", !NetSession.Online && RunSync.Instance == null);
            IntroCheck("the player is held still while it plays", OutpostUi.Open && OutpostUi.Current == OutpostUi.Kind.Intro);
            yield return WaitFor(() => IntroCinematic.Clock >= 3.8f, 20, "the first line");
            yield return SnapIntro(fromMenu ? "solo_first_line" : "offline_first_line");

            // a held Space skips it (1 of 1)
            float at = Time.realtimeSinceStartup;
            IntroCinematic.TestHold = true;
            yield return WaitFor(() => !IntroCinematic.Playing, 10, "the Intro to end after Space is held");
            float took = Time.realtimeSinceStartup - at;
            IntroCinematic.TestHold = false;
            IntroCheck("holding Space for a second skips it", took >= 0.9f && took < 5f, $"({took:0.0} s)");
            IntroCheck("the player is free again", !OutpostUi.Open);
            yield return new WaitForSeconds(1.5f);
            yield return SnapIntro(fromMenu ? "solo_docks_after" : "offline_docks_after");
            IntroCheck("the cinematic was shown once", IntroCinematic.TimesPlayed == 1, $"({IntroCinematic.TimesPlayed})");

            Log($"[Intro] DONE fails={introFails}");
            Quit(introFails == 0 ? 0 : 1);
        }
    }
}
