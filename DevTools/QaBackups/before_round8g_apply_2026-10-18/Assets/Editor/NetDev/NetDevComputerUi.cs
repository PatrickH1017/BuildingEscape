using System.Collections;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 2 (2026-10-17): pictures of the outpost computer's screen as a player sees it - the log-in, a wrong password, the page after the log-in and the Safes tab. IMGUI is only drawn in a
    /// real (not batch) Editor window: run it on the hidden desktop, <c>ui_bq.ps1 -Method ComputerUi</c>. Written to %TEMP%\be_ui_computer_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        IEnumerator ComputerUiScenario()
        {
            var gameView = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).maximized = true;
            var hm = HeistManager.Instance;
            yield return WaitFor(() => hm != null && TowerManager.Instance != null && !TowerManager.Instance.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            hm.godMode = true; hm.freezeTimer = true;
            TowerManager.Instance.GoToLevel(1);
            yield return new WaitUntil(() => TowerManager.Instance.Loading || hm.Floor == 1);
            yield return WaitFor(() => !TowerManager.Instance.Loading && hm.Floor == 1, 90, "floor 1");
            yield return new WaitForSeconds(1.5f);
            hm.godMode = true; hm.freezeTimer = true;
            var player = WobblyPlayerRegistry.All[0];
            var computer = FindFirstObjectByType<ComputerProp>();
            UiCheck("floor 1 has an outpost computer", computer != null);
            if (computer == null) { Quit(1); yield break; }
            var front = computer.transform.rotation * Vector3.forward;
            player.TeleportTo(computer.transform.position + front * 1.1f + Vector3.up * 0.2f, Quaternion.LookRotation(-front));
            yield return new WaitForSeconds(0.6f);
            computer.Use(player);
            yield return new WaitForSeconds(0.4f);
            UiCheck("using it brings the screen up", ComputerScreen.Showing && !ComputerScreen.LoggedIn);
            yield return Snap("computer_login");
            var login = computer.Credentials;
            ComputerScreen.SetTyped(login.user, "NOTIT");
            ComputerScreen.TryLogin(login.user, "NOTIT");
            ComputerScreen.SetTyped(login.user, "");
            yield return new WaitForSeconds(0.2f);
            yield return Snap("computer_wrong");
            ComputerScreen.SetTyped(login.user, login.pass);
            yield return new WaitForSeconds(0.2f);
            yield return Snap("computer_typed");
            ComputerScreen.TryLogin(login.user, login.pass);
            yield return new WaitForSeconds(0.2f);
            yield return Snap("computer_home");
            ComputerScreen.SelectTab(ComputerScreen.TabSafes);
            yield return new WaitForSeconds(0.2f);
            yield return Snap("computer_safes");
            UiCheck("the Safes tab is open", ComputerScreen.Tab == ComputerScreen.TabSafes);
            // the Nav tab: the map with the players and officers on it
            ComputerScreen.SelectTab(ComputerScreen.TabNav);
            yield return new WaitForSeconds(0.3f);
            yield return Snap("computer_nav");
            // the Hacked tab: lock a room, then unlock it
            ComputerScreen.SelectTab(ComputerScreen.TabHacked);
            yield return new WaitForSeconds(0.2f);
            var dir = FloorDirectory.Current(1);
            string room = dir != null && dir.rooms.Count > 0 ? dir.rooms[0].label : "101";
            ComputerScreen.TypeTerminal(room);
            ComputerScreen.TypeTerminal("LoCk");
            ComputerScreen.TypeTerminal("999");
            yield return new WaitForSeconds(0.3f);
            bool locked = false;
            foreach (var di in dir.rooms[0].doors) if (DoorManager.Active.Get(di).ComputerLocked) locked = true;
            UiCheck("typing a room number and LOCK locks its doors", locked);
            yield return Snap("computer_hacked_locked");
            ComputerScreen.SelectTab(ComputerScreen.TabNav);
            yield return new WaitForSeconds(0.3f);
            yield return Snap("computer_nav_locked");
            ComputerScreen.SelectTab(ComputerScreen.TabHacked);
            ComputerScreen.TypeTerminal(room + " unlock");
            yield return new WaitForSeconds(0.3f);
            yield return Snap("computer_hacked_unlocked");
            Log($"[Ui] DONE fails={uiFails}");
            Quit(uiFails == 0 ? 0 : 1);
        }
    }
}
