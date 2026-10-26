using System.Collections;
using System.Linq;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch C of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63): the developer terminal that CTRL + F1 opens (T2) and the code that unlocks the dev menu (T3). The keys themselves cannot be pressed in a test: the terminal has a hook
    /// that stands for CTRL + F1, and lines are run as Enter runs them. Run: <c>qa_regress_bd.ps1 -Methods Asks1023D</c>; the log ends with <c>[Asks1023D] DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23dFails;

        void A23D(string name, bool ok, string detail = "")
        {
            if (!ok) a23dFails++;
            Log($"[Asks1023D] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        static string LastLine() => DevTerminal.Lines.Count > 0 ? DevTerminal.Lines[DevTerminal.Lines.Count - 1] : "";

        IEnumerator Asks1023DScenario()
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            yield return new WaitForSeconds(0.5f);

            // ---- T2: CTRL + F1 opens the terminal, and while it is up the game keys are blocked like in any other screen
            A23D("T2: the terminal exists and is shut at the start; the dev menu is locked", DevAccess.Compiled && !DevTerminal.Open && !DevAccess.Unlocked && !TestMode.PanelOpen);
            DevTerminal.TestToggle = true;
            yield return null; yield return null;
            A23D("T2: CTRL + F1 opens it, and it counts as a screen that is up (the keys are blocked, the cursor is free)", DevTerminal.Open && TestMode.PanelOpen && Crosshair.MenuOpen);
            DevTerminal.TestToggle = true;
            yield return null; yield return null;
            A23D("T2: CTRL + F1 again shuts it", !DevTerminal.Open && !TestMode.PanelOpen && !Crosshair.MenuOpen);
            DevTerminal.TestToggle = true;
            yield return null; yield return null;

            // ---- T3: locked, every line is not a command; the wrong code unlocks nothing; the right one does, is never shown, and makes the dev menu
            DevTerminal.RunLine("help");
            A23D("T3: before the code even 'help' is an unknown command (nothing is given away)", LastLine() == "Unknown command." && !DevAccess.Unlocked);
            DevTerminal.RunLine("@catdog813");
            A23D("T3: a wrong code (the capital missing) unlocks nothing", LastLine() == "Unknown command." && !DevAccess.Unlocked);
            DevTerminal.RunLine("steamid");
            A23D("T3: the commands are not available while locked", LastLine() == "Unknown command." && !DevAccess.Unlocked);
            DevTerminal.RunLine("@Catdog813");
            yield return null;
            bool echoed = DevTerminal.Lines.Any(l => l.Contains("@Catdog813"));          // (the secret itself; the echo of the wrong guess "@catdog813" above is only what was typed)
            A23D("T3: the right code unlocks the dev menu, which is made, and the code is never shown on the screen", DevAccess.Unlocked && FindFirstObjectByType<TestMode>() != null && !echoed && LastLine().StartsWith("Unlocked"), $"(last line '{LastLine()}')");
            DevTerminal.RunLine("help");
            A23D("T3: unlocked, 'help' lists the commands", LastLine().Contains("steamid") && LastLine().Contains("lock"), $"('{LastLine()}')");
            DevTerminal.RunLine("steamid");
            A23D("T3: unlocked, 'steamid' says the Steam account's ID (or that Steam is not running)", LastLine().StartsWith("Steam"), $"('{LastLine()}')");
            DevTerminal.RunLine("close");
            yield return null;
            A23D("T3: 'close' shuts the terminal", !DevTerminal.Open && !TestMode.PanelOpen);
            DevTerminal.RunLine("lock");
            A23D("T3: 'lock' locks the dev menu again: F1 does nothing", !DevAccess.Unlocked && LastLine() == "Locked." && !TestMode.PanelOpen);
            DevTerminal.RunLine("steamid");
            A23D("T3: locked again, the commands are gone", LastLine() == "Unknown command.");

            Log($"[Asks1023D] DONE fails={a23dFails}");
            Quit(a23dFails == 0 ? 0 : 1);
        }
    }
}
