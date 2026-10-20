# Round 4, batch 13 (A4): the end screen after a wipe gets an Exit button next to Restart (offline) and the host's "Leave with party / Leave solo" pop-up; NetSession.ReturnPartyToLobby brings the
# whole party back to the lobby, still connected. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Net\LobbyService.cs')
Rep '            SteamMatchmaking.SetLobbyJoinable(Current, false);
        }' '            SteamMatchmaking.SetLobbyJoinable(Current, false);
        }

        /// <summary>The run is over and the party is back in the lobby: the owner opens it again (friends may join, the state says the lobby is open).</summary>
        public void Unlock()
        {
            if (!IsOwner) return;
            SteamMatchmaking.SetLobbyData(Current, NetConst.KeyState, NetConst.StateOpen);
            SteamMatchmaking.SetLobbyJoinable(Current, true);
        }'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Net\NetSession.cs')
Rep '        /// <summary>Leave the lobby or run and return to the menu. Safe to call in any state.</summary>' '        /// <summary>
        /// Host: after a run (the end screen: Exit, "Leave with party") everybody goes back to the lobby together; the connection stays, the party can start another run. Anybody else, and a session that
        /// is not in a run, just leaves. The players and the run''s objects are spawned with their scene, so loading the menu scene removes them on every machine.
        /// </summary>
        public void ReturnPartyToLobby()
        {
            if (!IsHost || State != NetSessionState.InRun || nm == null || !nm.IsListening) { Leave(); return; }
            nm.SceneManager.LoadScene(NetConst.MenuScene, LoadSceneMode.Single);
        }

        /// <summary>The menu scene is on screen again while the session is still connected (the host brought the party back): the lobby it is.</summary>
        void BackInLobby()
        {
            State = NetSessionState.InLobby;
            Status = "";
            LocalReady = false;
            if (UsingSteam) { lobby?.SetReady(false); if (IsHost) lobby?.Unlock(); }
            RebuildRoster();
        }

        /// <summary>Leave the lobby or run and return to the menu. Safe to call in any state.</summary>'
Rep '            if (State == NetSessionState.Connecting && connectDeadline > 0 && Time.unscaledTime > connectDeadline)
                Leave("Could not reach the host (timed out).");
        }' '            if (State == NetSessionState.InRun && nm != null && nm.IsListening && SceneManager.GetActiveScene().name == NetConst.MenuScene) BackInLobby();       // (the host brought the party back)
            if (State == NetSessionState.Connecting && connectDeadline > 0 && Time.unscaledTime > connectDeadline)
                Leave("Could not reach the host (timed out).");
        }'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\HeistHud.cs')
Rep '        /// <summary>The way out of the end screen: back to the menu online, restart the scene offline.</summary>
        static void DrawEndButton()
        {
            if (endSince < 0f) endSince = Time.unscaledTime;
            if (Time.unscaledTime - endSince < EndButtonDelay) return;
            bool online = Net.NetSession.Online;
            float s = HudTheme.S;
            var r = EndButtonRect(Screen.width, Screen.height, s);
            if (!HudWidgets.Button(r, online ? "Back to menu" : "Restart", HudTheme.Info, 22f)) return;
            if (online) Net.NetSession.Instance.Leave();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }' '        /// <summary>The host clicked Exit online: the pop-up that asks whether the party goes with them.</summary>
        static bool leavePopup;

        /// <summary>
        /// The ways out of the end screen (the owner, 2026-10-17 night: "Add Exit next to restart after a wipe, which brings you back to the lobby. For multiplayer the host after clicking Exit should get a pop-up
        /// asking Leave with party or Leave solo"): offline Restart and Exit (the lobby: the menu scene); online Exit only; a member leaves, the host is asked.
        /// </summary>
        static void DrawEndButton()
        {
            if (endSince < 0f) endSince = Time.unscaledTime;
            if (Time.unscaledTime - endSince < EndButtonDelay) return;
            bool online = Net.NetSession.Online;
            float s = HudTheme.S;
            var r = EndButtonRect(Screen.width, Screen.height, s);
            if (leavePopup) { DrawLeavePopup(s); return; }
            bool exit;
            if (online) exit = HudWidgets.Button(r, "Exit", HudTheme.Info, 22f);
            else
            {
                float gap = 18f * s;
                var left = new Rect(r.x - (r.width + gap) * 0.5f, r.y, r.width, r.height);
                var right = new Rect(left.x + r.width + gap, r.y, r.width, r.height);
                if (HudWidgets.Button(left, "Restart", HudTheme.Info, 22f))
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                    return;
                }
                exit = HudWidgets.Button(right, "Exit", HudTheme.Info, 22f);
            }
            if (!exit) return;
            if (!online) UnityEngine.SceneManagement.SceneManager.LoadScene(Net.NetConst.MenuScene);
            else if (Net.NetSession.Instance.IsHost) leavePopup = true;
            else Net.NetSession.Instance.Leave();
        }

        /// <summary>"Leave with party" brings everybody back to the lobby, "Leave solo" leaves alone (the session ends: nobody else can host it), Stay closes the pop-up.</summary>
        static void DrawLeavePopup(float s)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            float w = 300f * s, h = 52f * s, x = (Screen.width - w) * 0.5f, y = Screen.height * 0.40f;
            HudWidgets.Banner("LEAVE THE RUN?", "You are the host", HudTheme.Info);
            if (HudWidgets.Button(new Rect(x, y, w, h), "Leave with party", HudTheme.Cash, 22f)) { leavePopup = false; Net.NetSession.Instance.ReturnPartyToLobby(); return; }
            if (HudWidgets.Button(new Rect(x, y + h + 12f * s, w, h), "Leave solo", HudTheme.Info, 22f)) { leavePopup = false; Net.NetSession.Instance.Leave(); return; }
            if (HudWidgets.Button(new Rect(x, y + 2f * (h + 12f * s), w, h), "Stay", HudTheme.Danger, 22f)) leavePopup = false;
        }'
Save-Src
Write-Output 'round 4 batch 13 patched'
