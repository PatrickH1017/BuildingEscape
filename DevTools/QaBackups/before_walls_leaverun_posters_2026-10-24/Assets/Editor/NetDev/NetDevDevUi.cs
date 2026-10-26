using System.Collections;
using System.Reflection;
using BuildingEscape.Net;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The dev UI (the owner, 2026-10-24): typing the unlock code in the terminal switches the UI to the dev one (a SPECTATE button on the lobby's main page that opens a page to watch a match by its lobby code, a DEV tab on the lobby and the Esc menu,
    /// a DEV MENU button in the Esc menu) and typing it again switches back; CTRL + F1 closes the terminal again (it did not). Run in a real window on the Menu scene: <c>ui.ps1 -Project C:\bd -Methods DevUi</c>; pictures %TEMP%\be_menu_dev_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        int devUiFails;

        void DevUiCheck(string name, bool ok, string detail = "")
        {
            if (!ok) devUiFails++;
            Log($"[DevUi] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static string LastTerminalLine()
        {
            var lines = DevTerminal.Lines;
            return lines.Count > 0 ? lines[lines.Count - 1] : "";
        }

        IEnumerator DevUiScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            ResetGameViewSize();
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;

            yield return WaitFor(() => NetSession.Instance != null && FindFirstObjectByType<LobbyMenu>() != null, 40, "the lobby menu");
            var menu = FindFirstObjectByType<LobbyMenu>();
            const BindingFlags inst = BindingFlags.NonPublic | BindingFlags.Instance;
            var pageField = typeof(LobbyMenu).GetField("page", inst);
            void Go(string page) => pageField.SetValue(menu, System.Enum.Parse(pageField.FieldType, page));
            yield return new WaitForSeconds(1.5f);
            DevAccess.Lock();
            DevUiCheck("(test) the terminal is there and the UI starts as the normal one", DevAccess.Compiled && !DevAccess.Unlocked && pageField.GetValue(menu).ToString() == "Main");
            yield return SnapMenu("dev_0_normal");

            // ---- typing the code switches to the dev UI
            DevTerminal.SetOpen(true);
            DevTerminal.RunLine("@Catdog813");
            DevUiCheck("typing the code unlocks the dev UI", DevAccess.Unlocked && LastTerminalLine().StartsWith("Unlocked"), $"('{LastTerminalLine()}')");
            DevTerminal.SetOpen(false);
            yield return new WaitForSeconds(0.6f);
            yield return SnapMenu("dev_1_lobby");
            Go("Spectate");
            yield return new WaitForSeconds(0.6f);
            DevUiCheck("the SPECTATE page opens while the dev UI is on", pageField.GetValue(menu).ToString() == "Spectate");
            yield return SnapMenu("dev_2_spectate");

            // ---- typing it again switches back, and the page it was on goes with it
            DevTerminal.SetOpen(true);
            DevTerminal.RunLine("@Catdog813");
            DevUiCheck("typing the code again switches back to the normal UI", !DevAccess.Unlocked && LastTerminalLine().StartsWith("Locked"), $"('{LastTerminalLine()}')");
            DevTerminal.SetOpen(false);
            yield return new WaitForSeconds(0.6f);
            DevUiCheck("the spectate page is gone with it: the lobby is on its main page", pageField.GetValue(menu).ToString() == "Main");
            yield return SnapMenu("dev_3_normal_again");

            // ---- a word that is not the code does nothing while locked
            DevTerminal.SetOpen(true);
            DevTerminal.RunLine("steamid");
            DevUiCheck("a command is not run while the UI is locked", !DevAccess.Unlocked && LastTerminalLine() == "Unknown command.", $"('{LastTerminalLine()}')");
            DevTerminal.SetOpen(false);
            yield return null;

            // ---- CTRL + F1 closes the terminal: the toggle runs once however many times it is asked in one frame
            DevTerminal.SetOpen(false);
            DevTerminal.Toggle();
            DevUiCheck("CTRL + F1 opens the terminal", DevTerminal.Open);
            yield return null;
            DevTerminal.Toggle(); DevTerminal.Toggle();                                       // (the key seen twice in one frame, by Update and by the GUI: one toggle)
            DevUiCheck("CTRL + F1 closes it again, once, even when the key is seen twice in the same frame", !DevTerminal.Open);
            yield return null;

            DevAccess.Lock();
            Log($"[DevUi] DONE fails={devUiFails}");
            Quit(devUiFails == 0 ? 0 : 1);
        }
    }
}
