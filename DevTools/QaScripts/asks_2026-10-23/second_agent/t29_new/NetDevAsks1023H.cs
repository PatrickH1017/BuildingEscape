using System.Collections;
using BuildingEscape.Net;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The Play Mode half of batch H, T29, of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63): the developer spectator over the network, two machines on the loopback (the host and the developer; the local transport has no Steam, so the developer's account is claimed and the Editor's
    /// host takes the claim). <c>DevSpectate</c>: the developer joins before Start, is no player (no party seat, no body, the run does not wait for him or count him), watches through a camera that follows a player, and his dev menu changes the host's match (cash, a level) - while commands that are not valid, or
    /// that come from a sender who is no spectator, are refused. <c>DevDenied</c>: an account that is not on the list is refused, and so is a developer who comes after Start. Run: <c>qa_mp_bd.ps1 -Method DevSpectate</c> and <c>-Method DevDenied</c>; both logs end with <c>[Asks1023H] ... DONE fails=N</c>.
    /// </summary>
    public partial class NetDevRunner
    {
        int a23hFails;
        const ulong DevWatcher = 76561198000000042UL, DevStranger = 76561198000000099UL;

        void A23H(string name, bool ok, string detail = "")
        {
            if (!ok) a23hFails++;
            Log($"[Asks1023H] {(ok ? "PASS" : "FAIL")} {name} {detail}");
        }

        // ------------------------------------------------------------------ DevSpectate

        IEnumerator DevSpectateHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            DevAccounts.AddForTests(DevWatcher);
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitFor(() => net.SpectatorCount >= 1, 200, "the developer");
            var nm = NetworkManager.Singleton;
            A23H("T29 host: the developer is connected but takes no seat: the party list holds only the host", nm.ConnectedClientsIds.Count == 2 && net.Roster.Count == 1 && net.PlayersConnected == 1 && net.SpectatorCount == 1,
                $"(connections {nm.ConnectedClientsIds.Count}, party list {net.Roster.Count}, players {net.PlayersConnected}, watching {net.SpectatorCount})");
            A23H("T29 host: the run can start with the developer connected (he is not waited for)", net.CanStart);
            Signal("h_lobby");
            yield return WaitSig("c_lobby", 90);

            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            var hm = HeistManager.Instance; var tm = TowerManager.Instance; var sync = RunSync.Instance;
            hm.godMode = true; hm.freezeTimer = true;
            yield return WaitFor(() => sync != null && sync.EveryoneHere, 60, "every player");
            A23H("T29 host: one player body in the run (the developer has none) and the run expects one player", NetPlayer.All.Count == 1 && sync.ExpectedPlayers == 1 && Mine != null && Mine.Slot == 0, $"(bodies {NetPlayer.All.Count}, expected {sync.ExpectedPlayers})");
            A23H("T29 host: the developer is still connected and still no player", net.SpectatorCount == 1 && net.PlayersConnected == 1 && nm.ConnectedClientsIds.Count == 2);
            Signal("h_run");
            yield return WaitSig("c_run", 120);

            // ---- the dev menu: cash, from the developer
            int cash0 = hm.Cash;
            Signal("h_cash0", cash0.ToString());
            yield return WaitSig("c_cash_sent", 60);
            yield return WaitFor(() => hm.Cash >= cash0 + 10000, 20, "the developer's cash");
            A23H("T29 host: the developer's +$10,000 arrived", hm.Cash == cash0 + 10000, $"(cash {hm.Cash}, was {cash0})");
            Signal("h_cash_ok");

            // ---- commands the host must refuse: an amount the menu does not offer, a level outside the tower, no command at all, and a sender who is no spectator
            yield return WaitSig("c_bad_sent", 60);
            yield return new WaitForSeconds(1.5f);
            A23H("T29 host: an amount the menu does not offer, a level outside the tower and a command that is none are refused", hm.Cash == cash0 + 10000 && hm.Floor == 0 && !tm.Loading, $"(cash {hm.Cash}, floor {hm.Floor})");
            bool asHost = sync.HostDevCommand(nm.LocalClientId, DevCommand.Cash, 100);
            A23H("T29 host: a command from a sender who is no spectator (the host itself) is refused", !asHost && hm.Cash == cash0 + 10000);
            Signal("h_bad_ok");

            // ---- a level
            yield return WaitSig("c_level_sent", 60);
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 180);
            hm.godMode = true; hm.freezeTimer = true;
            A23H("T29 host: the developer's 'F1' took the party to floor 1", hm.Floor == 1);
            Signal("h_floor1");
            yield return WaitSig("c_done", 120);
            Log($"[Asks1023H] host DONE fails={a23hFails}");
            Log("DONE devspectatehost");
            Quit(a23hFails == 0 ? 0 : 1);
        }

        IEnumerator DevSpectateClientScenario()
        {
            var net = NetSession.Instance;
            DevAccounts.AddForTests(DevWatcher);
            DevAccess.Unlock();
            float deadline = Time.realtimeSinceStartup + 150f;
            while (net.State != NetSessionState.InLobby && Time.realtimeSinceStartup < deadline)
            {
                net.QaJoinLocalSpectator("127.0.0.1", DevWatcher);
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 8f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State != NetSessionState.InLobby) yield return new WaitForSeconds(2f);
            }
            A23H("T29 developer: connected, in the lobby, as a spectator", net.State == NetSessionState.InLobby && net.IsSpectator && NetSession.Online);
            A23H("T29 developer: there is no party list for him (he has no seat, and the host's list does not carry him)", net.Roster.Count == 0, $"({net.Roster.Count})");
            Signal("c_lobby");

            yield return WaitSig("h_run", 240);
            yield return FloorReady(0, 180);
            var hm = HeistManager.Instance; var sync = RunSync.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => sync != null && sync.IsSpawned && NetPlayer.All.Count >= 1, 90, "the host's player");
            A23H("T29 developer: no body of his own, one player on show and the run expects one", NetPlayer.Local == null && NetPlayer.All.Count == 1 && sync.ExpectedPlayers == 1, $"(bodies {NetPlayer.All.Count}, expected {sync.ExpectedPlayers}, mine {(NetPlayer.Local != null)})");
            yield return WaitFor(() => DevSpectator.Active, 30, "the camera on a player");
            var cam = FindFirstObjectByType<CameraFollow>();
            A23H("T29 developer: the camera follows the host's player (player 1)", cam != null && cam.spectate != null && DevSpectator.Watching != null && cam.spectate == DevSpectator.Watching.transform && DevSpectator.WatchingNumber == 1,
                $"(watching {DevSpectator.WatchingNumber})");
            yield return new WaitForSeconds(1.5f);
            float d = cam != null && DevSpectator.Watching != null ? Vector3.Distance(cam.transform.position, DevSpectator.Watching.transform.position) : 999f;
            A23H("T29 developer: the picture is taken close to the player he watches", d < 6f, $"({d:0.0} m)");
            DevSpectator.TestStep = 1;
            yield return null; yield return null;
            A23H("T29 developer: stepping to the next player with only one player stays on him", DevSpectator.Active && DevSpectator.WatchingNumber == 1);
            DevSpectator.TestMenuToggle = true;
            yield return null; yield return null;
            A23H("T29 developer: F1 opens the dev menu (unlocked) and the cursor is free", DevSpectator.MenuOpen && TestMode.PanelOpen);
            DevSpectator.TestMenuToggle = true;
            yield return null; yield return null;
            A23H("T29 developer: F1 closes it again", !DevSpectator.MenuOpen && !TestMode.PanelOpen);
            A23H("T29 developer: his microphone is never open", VoiceChat.Instance == null || (!VoiceChat.Instance.Speaking && VoiceChat.Instance.PacketsSent == 0));
            Signal("c_run");

            // ---- cash
            yield return WaitSig("h_cash0", 60);
            int cash0 = int.Parse(ReadSig("h_cash0"));
            sync.RequestDevCommand(DevCommand.Cash, 10000);
            Signal("c_cash_sent");
            yield return WaitFor(() => hm.Cash >= cash0 + 10000, 20, "the cash shown here");
            A23H("T29 developer: the +$10,000 shows here too (it is the host's cash)", hm.Cash == cash0 + 10000, $"(cash {hm.Cash})");
            yield return WaitSig("h_cash_ok", 60);

            // ---- commands the host must refuse
            sync.RequestDevCommand(DevCommand.Cash, 12345);
            sync.RequestDevCommand(DevCommand.GoToLevel, 99);
            sync.RequestDevCommand((DevCommand)200, 0);
            Signal("c_bad_sent");
            yield return WaitSig("h_bad_ok", 60);

            // ---- a level
            sync.RequestDevCommand(DevCommand.GoToLevel, 1);
            Signal("c_level_sent");
            yield return new WaitUntil(() => tm.Loading || hm.Floor == 1);
            yield return FloorReady(1, 240);
            yield return WaitSig("h_floor1", 60);
            yield return WaitFor(() => DevSpectator.Active, 30, "the camera on a player again");
            A23H("T29 developer: floor 1 is built here too and the camera is on the player again", hm.Floor == 1 && DevSpectator.Active && NetPlayer.Local == null);
            Signal("c_done");
            Log($"[Asks1023H] developer DONE fails={a23hFails}");
            Log("DONE devspectateclient");
            Quit(a23hFails == 0 ? 0 : 1);
        }

        // ------------------------------------------------------------------ DevDenied

        IEnumerator DevDeniedHostScenario()
        {
            var net = NetSession.Instance;
            ClearSignals();
            DevAccounts.AddForTests(DevWatcher);                        // (the one account on the list; the stranger is not)
            net.HostLocal();
            yield return WaitFor(() => net.State == NetSessionState.InLobby, 20, "host lobby");
            yield return WaitSig("c_refused1", 240);
            var nm = NetworkManager.Singleton;
            yield return new WaitForSeconds(1f);
            A23H("T29 host: the stranger's account was refused: nobody connected, nobody watches, the party list holds the host", net.SpectatorCount == 0 && nm.ConnectedClientsIds.Count == 1 && net.Roster.Count == 1,
                $"(connections {nm.ConnectedClientsIds.Count}, watching {net.SpectatorCount})");

            net.StartRun();
            yield return WaitFor(() => net.State == NetSessionState.InRun, 60, "run start");
            yield return FloorReady(0, 90);
            Signal("h_started");
            yield return WaitSig("c_refused2", 240);
            yield return new WaitForSeconds(1f);
            A23H("T29 host: a developer who comes after Start was refused as well", net.SpectatorCount == 0 && nm.ConnectedClientsIds.Count == 1 && NetPlayer.All.Count == 1, $"(connections {nm.ConnectedClientsIds.Count}, watching {net.SpectatorCount})");
            Log($"[Asks1023H] host DONE fails={a23hFails}");
            Log("DONE devdeniedhost");
            Quit(a23hFails == 0 ? 0 : 1);
        }

        IEnumerator DevDeniedClientScenario()
        {
            var net = NetSession.Instance;
            DevAccounts.AddForTests(DevWatcher);
            string notDev = DevAccessRules.Message(SpectateVerdict.NotADeveloper), started = DevAccessRules.Message(SpectateVerdict.RunStarted);

            // ---- a stranger: refused with the reason
            float deadline = Time.realtimeSinceStartup + 150f;
            while (Time.realtimeSinceStartup < deadline)
            {
                net.QaJoinLocalSpectator("127.0.0.1", DevStranger);
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State == NetSessionState.Offline && net.Status == notDev) break;
                if (net.State == NetSessionState.InLobby) break;                           // (let in: the check below fails)
                yield return new WaitForSeconds(2f);
            }
            A23H("T29 stranger: an account that is not on the list is refused, with the reason, and is no spectator after it", net.State == NetSessionState.Offline && net.Status == notDev && !net.IsSpectator, $"(state {net.State}, '{net.Status}')");
            Signal("c_refused1");

            // ---- a developer after Start: refused too
            yield return WaitSig("h_started", 240);
            yield return new WaitForSeconds(2f);
            deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                net.QaJoinLocalSpectator("127.0.0.1", DevWatcher);
                float t = 0;
                while (net.State == NetSessionState.Connecting && t < 10f) { t += Time.unscaledDeltaTime; yield return null; }
                if (net.State == NetSessionState.Offline && net.Status == started) break;
                if (net.State == NetSessionState.InLobby) break;
                yield return new WaitForSeconds(2f);
            }
            A23H("T29 developer after Start: refused, with the reason (a spectator joins before Start)", net.State == NetSessionState.Offline && net.Status == started && !net.IsSpectator, $"(state {net.State}, '{net.Status}')");
            Signal("c_refused2");
            Log($"[Asks1023H] developer DONE fails={a23hFails}");
            Log("DONE devdeniedclient");
            Quit(a23hFails == 0 ? 0 : 1);
        }
    }
}
