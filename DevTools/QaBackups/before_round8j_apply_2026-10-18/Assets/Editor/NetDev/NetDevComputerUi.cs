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
            foreach (var g in GuardAI.All) if (g) { g.sightRange = 0.1f; g.hearingMultiplier = 0f; }     // (the officers are only to be looked at: the outpost's own guard would arrest the player in the middle of the pictures)
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
            // the Nav tab: the map with the players and officers on it, and the little terminal under it
            ComputerScreen.SelectTab(ComputerScreen.TabNav);
            yield return new WaitForSeconds(0.3f);
            yield return Snap("computer_nav");
            // a room number on its own, typed on the Nav page, locks the room (the red mark on the map); the same number again unlocks it
            var dir = FloorDirectory.Current(1);
            string room = dir != null && dir.rooms.Count > 0 ? dir.rooms[0].label : "101";
            ComputerScreen.TypeTerminal("999");
            ComputerScreen.TypeTerminal(room);
            yield return new WaitForSeconds(0.3f);
            bool locked = false;
            foreach (var di in dir.rooms[0].doors) if (DoorManager.Active.Get(di).ComputerLocked) locked = true;
            UiCheck("typing a room number on the Nav page locks its doors", locked);
            yield return Snap("computer_nav_locked");
            // the Hacked tab is the same terminal, bigger
            ComputerScreen.SelectTab(ComputerScreen.TabHacked);
            yield return new WaitForSeconds(0.2f);
            yield return Snap("computer_hacked_locked");
            ComputerScreen.TypeTerminal(room);
            yield return new WaitForSeconds(0.3f);
            locked = false;
            foreach (var di in dir.rooms[0].doors) if (DoorManager.Active.Get(di).ComputerLocked) locked = true;
            UiCheck("typing the number again unlocks them", !locked);
            yield return Snap("computer_hacked_unlocked");
            // the Nav map: a party that is not there, a few locked rooms and the office, things that move (pings and trails), a click on a room, a number typed
            var dirT = FloorDirectory.Current(1);
            ComputerScreen.SelectTab(ComputerScreen.TabNav);
            yield return new WaitForSeconds(0.2f);
            string suiteL = dirT.rooms.Find(r => r.role == "suite")?.label, serverL = dirT.rooms.Find(r => r.role == "server")?.label;
            var plainL = dirT.rooms.FindAll(r => r.role == null && r.doors.Count > 0);
            foreach (var lab in new[] { suiteL, serverL, plainL.Count > 3 ? plainL[3].label : null, plainL.Count > 9 ? plainL[9].label : null, plainL.Count > 15 ? plainL[15].label : null, FloorDirectory.OutpostName })
                if (!string.IsNullOrEmpty(lab)) ComputerScreen.TypeTerminal(lab);
            bool suiteLocked = false;
            var suiteRoom = dirT.rooms.Find(r => r.label == suiteL);
            if (suiteRoom != null) foreach (var di in suiteRoom.doors) if (DoorManager.Active.Get(di).ComputerLocked) suiteLocked = true;
            UiCheck("the Nav map is up and typing room numbers on it locks them (the suite)", ComputerScreen.Tab == ComputerScreen.TabNav && suiteLocked);
            var halls = dirT.all.FindAll(r => r.kind == 2 && r.theme != "outpost" && Mathf.Max(r.bounds.W, r.bounds.D) > 7f);
            ComputerScreen.DebugPlayers.Clear();
            for (int i = 0; i < 3 && i < halls.Count; i++) ComputerScreen.DebugPlayers.Add(new ComputerScreen.NavDebugPlayer { number = i + 2, pos = new Vector3(halls[i].bounds.Cx, 0f, halls[i].bounds.Cz) });
            float tm0 = Time.unscaledTime;
            bool snapped1 = false, snapped2 = false, snapped3 = false, reset = false;
            while (Time.unscaledTime - tm0 < 6f)
            {
                float tt = Time.unscaledTime - tm0;
                for (int i = 0; i < ComputerScreen.DebugPlayers.Count; i++)
                {
                    var hb = halls[i].bounds; bool wide = hb.W >= hb.D; float half = (wide ? hb.W : hb.D) * 0.5f - 1f;
                    float off = Mathf.Sin(tt * (0.9f + 0.15f * i) + i) * half;
                    ComputerScreen.DebugPlayers[i].pos = wide ? new Vector3(hb.Cx + off, 0f, hb.Cz) : new Vector3(hb.Cx, 0f, hb.Cz + off);
                }
                if (!snapped1 && tt > 2.4f) { snapped1 = true; yield return Snap("computer_navmap"); }
                if (!snapped2 && tt > 2.6f && suiteRoom != null)
                {
                    snapped2 = true;
                    ComputerScreen.NavMapSet(3.2f, new Vector3(suiteRoom.bounds.Cx, 0f, suiteRoom.bounds.Cz), suiteL, true);
                }
                if (snapped2 && !snapped3 && tt > 4.2f) { snapped3 = true; yield return Snap("computer_navmap_zoom"); }
                if (snapped3 && !reset && tt > 4.4f) { reset = true; ComputerScreen.NavMapSet(1f, Vector3.zero, null, true); ComputerScreen.SetTerminalText("10"); }
                yield return null;
            }
            yield return Snap("computer_navmap_find");
            ComputerScreen.SetTerminalText("");
            var serverRoom = dirT.rooms.Find(r => r.label == serverL);
            if (serverRoom != null) ComputerScreen.NavMapHover(new Vector3(serverRoom.bounds.Cx, 0f, serverRoom.bounds.Cz));
            yield return new WaitForSeconds(0.4f);
            yield return Snap("computer_navmap_hover");
            ComputerScreen.NavMapHover(null);
            ComputerScreen.DebugPlayers.Clear();
            Log($"[Ui] DONE fails={uiFails}");
            Quit(uiFails == 0 ? 0 : 1);
        }
    }
}
