# T29 (the owner, 2026-10-23): a developer joins somebody else's match as an invisible spectator, before Start only, via the party's lobby code, recognised by his Steam account (a list of Steam IDs), in the friends' builds and the Editor only
# (ETR_DEV_TOOLS), no body, no party slot, no notice, no voice; the dev menu works on that match and the host checks the sender. Patches the scratch copy C:\bd; exact matches only (patchlib), line endings kept; new files are written with CRLF.
# Needs T2 + T3 (the terminal and the unlock code), T12 and T5 already applied (their registration lines and the protocol comment are anchors here).
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 't29_new\DevAccessRules.cs') ($A + 'Scripts\Dev\DevAccessRules.cs')
Put-Crlf (Join-Path $here 't29_new\DevAccounts.cs') ($A + 'Scripts\Dev\DevAccounts.cs')
Put-Crlf (Join-Path $here 't29_new\DevCommandRules.cs') ($A + 'Scripts\Dev\DevCommandRules.cs')
Put-Crlf (Join-Path $here 't29_new\DevCommands.cs') ($A + 'Scripts\Dev\DevCommands.cs')
Put-Crlf (Join-Path $here 't29_new\DevSpectator.cs') ($A + 'Scripts\Dev\DevSpectator.cs')
Put-Crlf (Join-Path $here 't29_new\RunSync.Dev.cs') ($A + 'Scripts\Net\RunSync.Dev.cs')
Put-Crlf (Join-Path $here 't29_new\DevAccounts.txt') ($A + 'Resources\DevAccounts.txt')
Put-Crlf (Join-Path $here 't29_new\NetDevAsks1023HTest.cs') ($A + 'Editor\NetDev\NetDevAsks1023HTest.cs')
Put-Crlf (Join-Path $here 't29_new\NetDevAsks1023H.cs') ($A + 'Editor\NetDev\NetDevAsks1023H.cs')

# ---- the wire: the lobby names its host, the protocol moves on
$SC = $A + 'Scripts\Net\NetConst.cs'; Open-Src $SC
Rep '        public const string KeyState = "state";     // StateOpen / StateInRun' @'
        public const string KeyState = "state";     // StateOpen / StateInRun
        public const string KeyHost = "host";       // the lobby owner's Steam ID: a developer who watches (DevAccessRules) connects to it without joining the lobby
'@.TrimEnd()
Rep '// 28: (2026-10-23) dragging the body of an eliminated guard' '// 28: (2026-10-23) the developer spectator (a connection tag, the host''s Steam ID in the lobby data, one dev-menu message), dragging the body of an eliminated guard'
Save-Src

$SC = $A + 'Scripts\Net\LobbyService.cs'; Open-Src $SC
Rep '        public string Name, Version, Code;' @'
        public string Name, Version, Code;
        /// <summary>The Steam ID of the lobby's owner (the host); 0 for a lobby that does not name it (an older build).</summary>
        public ulong Host;
'@.TrimEnd()
Rep @'
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyState, NetConst.StateOpen);
                SteamMatchmaking.SetLobbyMemberData(Current, NetConst.MemberReady, "0");
'@.TrimEnd() @'
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyState, NetConst.StateOpen);
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyHost, SteamUser.GetSteamID().m_SteamID.ToString());          // (a developer who watches connects to the host without joining the lobby)
                SteamMatchmaking.SetLobbyMemberData(Current, NetConst.MemberReady, "0");
'@.TrimEnd()
Rep '        public void Search(string code, Action<List<LobbyListing>> done)' '        public void Search(string code, Action<List<LobbyListing>> done, bool includeFull = false)'
Rep '            SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);' '            if (!includeFull) SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);          // (a developer who watches takes no seat: he also finds a lobby that is full)'
Rep '                            Max = SteamMatchmaking.GetLobbyMemberLimit(id),' @'
                            Max = SteamMatchmaking.GetLobbyMemberLimit(id),
                            Host = ulong.TryParse(SteamMatchmaking.GetLobbyData(id, NetConst.KeyHost), out var hostId) ? hostId : 0UL,
'@.TrimEnd()
Save-Src

# ---- the session: a connection that watches is no player
$SC = $A + 'Scripts\Net\NetSession.cs'; Open-Src $SC
# (the new methods first: their anchor is the end of the file)
Rep @'
            res.Pending = false;
            approving--;
        }
    }
}
'@.TrimEnd() @'
            res.Pending = false;
            approving--;
        }

        // ------------------------------------------------------------------ a developer who watches (DevAccessRules)

        /// <summary>
        /// Host: a connection that asks to watch the match as a developer (the owner, 2026-10-23). Steam says which account it is (the Editor's local test mode takes the claim), the list says whether that account is a developer's, and the rules say whether this build, the run, the room and the version
        /// allow it (<see cref="DevAccessRules.Judge"/>). A developer who is let in is remembered here and nowhere else: he has no player, no seat in the party list and no message goes to anybody about him.
        /// </summary>
        void ApproveSpectator(ulong clientId, string version, ulong claimed, NetworkManager.ConnectionApprovalResponse res)
        {
            var verdict = DevAccessRules.Judge(DevAccess.Compiled, State >= NetSessionState.StartingRun, SpectatorCount, version == NetConst.BuildVersion,
                ProvesAccount(clientId, claimed) && DevAccounts.IsDeveloper(claimed));
            if (verdict == SpectateVerdict.Allow) spectatorIds.Add(clientId);
            res.Approved = verdict == SpectateVerdict.Allow;
            res.Reason = DevAccessRules.Message(verdict);
            res.Pending = false;
        }

        /// <summary>Host: is the connection really the account it says? On Steam the transport knows who connected; the Editor's local test mode has no Steam, so there a test's claim is taken (no build has that line).</summary>
        bool ProvesAccount(ulong clientId, ulong claimed)
        {
            if (claimed == 0UL) return false;
            if (UsingSteam) return steamTransport != null && steamTransport.TryGetRemoteSteamId(clientId, out var id) && id.m_SteamID == claimed;
#if UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }

#if ETR_DEV_TOOLS || UNITY_EDITOR
        /// <summary>
        /// A developer joins the party with this lobby code as an invisible spectator (the owner, 2026-10-23; the terminal's <c>spectate CODE</c>). He does NOT join the Steam lobby - so he takes no seat in it and the party's list never shows him - but connects straight to the host, whose Steam ID the
        /// lobby names (<see cref="NetConst.KeyHost"/>). The host lets him in only if Steam says he is on the developer list, the run has not started and this is a build with the dev tools. Returns "" when the search was started, else the reason it was not.
        /// </summary>
        public string SpectateByCode(string code)
        {
            if (!DevAccess.Compiled) return DevAccessRules.Message(SpectateVerdict.NotInThisBuild);
            if (!CanBegin()) return Status;
            if (!EnsureSteam()) return Status;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (code.Length != NetConst.CodeLength) return $"Codes are {NetConst.CodeLength} characters.";
            if (!DevAccounts.IsDeveloper(steam.LocalId.m_SteamID)) return DevAccessRules.Message(SpectateVerdict.NotADeveloper);
            Begin("Looking for lobby " + code + "...");
            spectateNext = true;
            SearchByCode(code, 3);
            return "";
        }

        void ConnectSpectator(LobbyListing l)
        {
            if (!CanBegin()) return;
            if (l.Host == 0UL) { Status = "That lobby does not name its host (an older build)."; return; }
            Begin("Connecting to the host...");
            hostAtJoin = new CSteamID(l.Host);
            steamTransport.PrepareClient(l.Host);
            IsSpectator = true;                                            // (before the connection: the payload says so)
            if (!StartNetcode(false, steamTransport, Payload(true))) { IsSpectator = false; Leave("Could not start the connection."); return; }
            UsingSteam = true; IsHost = false;
            Status = "Connecting to host...";
        }
#endif

#if UNITY_EDITOR
        /// <summary>QA only (the Editor): the Steam account the next spectator connection over the local transport claims - the local transport has no Steam to say who connects, so a test names one.</summary>
        public static ulong QaAccount;

        /// <summary>QA only (the Editor): connects to a local host as a developer who watches, claiming <paramref name="account"/> (the host takes the claim in the Editor only).</summary>
        public void QaJoinLocalSpectator(string ip, ulong account)
        {
            if (!CanBegin()) return;
            QaAccount = account;
            Begin("Connecting to " + ip + "...");
            localTransport.SetConnectionData(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), localPort);
            IsSpectator = true;
            if (!StartNetcode(false, localTransport, Payload(true))) { IsSpectator = false; Leave("Could not start the connection."); return; }
            UsingSteam = false; IsHost = false;
        }
#endif
    }
}
'@.TrimEnd()
Rep @'
        int approving;
        bool leaving;
'@.TrimEnd() @'
        int approving;
        bool leaving;
        bool spectateNext;                                    // (the next lobby search is a developer's: SpectateByCode)
        readonly HashSet<ulong> spectatorIds = new();         // host: the connections let in as developers who watch (DevAccessRules); nobody but the host knows them
'@.TrimEnd()
Rep '        public event Action RosterChanged;' @'
        public event Action RosterChanged;
        /// <summary>This machine is a developer who watches somebody else's match (the owner, 2026-10-23): it took no seat in the Steam lobby, has no player and is invisible to the party. See <see cref="DevAccessRules"/>.</summary>
        public bool IsSpectator { get; private set; }
        /// <summary>Host: is this connection a developer who watches (and no player)?</summary>
        public bool IsSpectatorClient(ulong clientId) => spectatorIds.Contains(clientId);
        /// <summary>Host: how many developers watch now.</summary>
        public int SpectatorCount
        {
            get
            {
                int n = 0;
                if (nm != null && nm.IsListening && nm.IsServer) foreach (var id in nm.ConnectedClientsIds) if (spectatorIds.Contains(id)) n++;
                return n;
            }
        }
        /// <summary>Host: the players connected - everybody but the developers who watch. The party size, the player limit, the party list, the intro's vote and the spawns count these.</summary>
        public int PlayersConnected => nm != null && nm.IsListening && nm.IsServer ? DevAccessRules.PlayersAmong(nm.ConnectedClientsIds.Count, SpectatorCount) : 0;
'@.TrimEnd()
Rep '            if (steamTransport != null) steamTransport.MaxConnections = NetConst.MaxPlayers;   // the other players (MaxPlayers - 1) and one more being checked' '            if (steamTransport != null) steamTransport.MaxConnections = NetConst.MaxPlayers + (DevAccess.Compiled ? DevAccessRules.MaxSpectators : 0);   // the other players (MaxPlayers - 1) and one more being checked, and the developers who watch (only a build with the dev tools lets them in)'
Rep '                if (nm.ConnectedClientsIds.Count != roster.Count) return false; // someone is still connecting' '                if (PlayersConnected != roster.Count) return false; // someone is still connecting (a developer who watches is no player)'
Rep @'
            int slot = 0, count = completed.Count;
            var tower = TowerManager.Instance;
            foreach (var id in completed)
'@.TrimEnd() @'
            var players = new List<ulong>(completed.Count);
            foreach (var id in completed) if (!spectatorIds.Contains(id)) players.Add(id);          // (a developer who watches gets no body and no slot)
            int slot = 0, count = players.Count;
            var tower = TowerManager.Instance;
            foreach (var id in players)
'@.TrimEnd()
Rep '            sync.GetComponent<RunSync>().Begin(PendingSeed, completed.Count);' '            sync.GetComponent<RunSync>().Begin(PendingSeed, players.Count);'
Rep '            IsHost = UsingSteam = LocalReady = false;' '            IsHost = UsingSteam = LocalReady = IsSpectator = false;
            spectatorIds.Clear();'
Rep '            var ids = new List<ulong>(nm.ConnectedClientsIds);' @'
            var ids = new List<ulong>(nm.ConnectedClientsIds);
            ids.RemoveAll(spectatorIds.Contains);                 // (a developer who watches is in nobody's list)
'@.TrimEnd()
Rep @'
        void OnClientDisconnected(ulong id)
        {
'@.TrimEnd() @'
        void OnClientDisconnected(ulong id)
        {
            spectatorIds.Remove(id);
'@.TrimEnd()
Rep '            else if (nm.IsListening)' '            else if (nm.IsListening && !IsSpectator)'
Rep @'
                foreach (var id in ids)
                    roster.Add(new RosterEntry { Name = "Player " + n++, Ready = true, IsHost = id == NetworkManager.ServerClientId, IsLocal = id == nm.LocalClientId, Look = "" });
'@.TrimEnd() @'
                foreach (var id in ids)
                {
                    if (IsHost && spectatorIds.Contains(id)) continue;          // (a developer who watches is no player and no name in the list)
                    roster.Add(new RosterEntry { Name = "Player " + n++, Ready = true, IsHost = id == NetworkManager.ServerClientId, IsLocal = id == nm.LocalClientId, Look = "" });
                }
'@.TrimEnd()
Rep @'
        static byte[] Payload() =>
            Encoding.UTF8.GetBytes(NetConst.BuildVersion + "|" + (SteamService.Instance != null && SteamService.Instance.Ready
                ? SteamService.Instance.LocalId.m_SteamID : 0UL));
'@.TrimEnd() @'
        static byte[] Payload(bool spectator = false)
        {
            ulong id = SteamService.Instance != null && SteamService.Instance.Ready ? SteamService.Instance.LocalId.m_SteamID : 0UL;
#if UNITY_EDITOR
            if (spectator && QaAccount != 0UL) id = QaAccount;
#endif
            return Encoding.UTF8.GetBytes(DevAccessRules.Payload(NetConst.BuildVersion, id, spectator));
        }
'@.TrimEnd()
Rep @'
            int bar = text.LastIndexOf('|');
            string version = bar < 0 ? text : text.Substring(0, bar);
            ulong claimed = bar >= 0 && ulong.TryParse(text.Substring(bar + 1), out var c) ? c : 0;
'@.TrimEnd() @'
            DevAccessRules.ParsePayload(text, out string version, out ulong claimed, out bool spectator);
            if (spectator)
            {
                ApproveSpectator(clientId, version, claimed, res);          // (a developer who asks to watch: no lobby seat to check, the account list decides)
                approving--;
                yield break;
            }
'@.TrimEnd()
Rep '            int others = nm.ConnectedClientsIds.Count + (approving - 1); // connected + other joins still being checked' '            int others = PlayersConnected + (approving - 1); // players connected + other joins still being checked (a developer who watches takes no seat)'
# (the lobby search: the code search of a developer also finds a full lobby, and ends in a spectator connection instead of a lobby join)
Rep @'
                State = NetSessionState.Offline;
                if (list.Count == 0) { Status = "No open lobby with code " + code + "."; return; }
                if (!list[0].VersionOk) { Status = JoinPolicy.Message(JoinVerdict.WrongVersion); return; }
                JoinSteam(list[0].Id);
            });
'@.TrimEnd() @'
                State = NetSessionState.Offline;
                bool asSpectator = spectateNext; spectateNext = false;
                if (list.Count == 0) { Status = "No open lobby with code " + code + "."; return; }
                if (!list[0].VersionOk) { Status = JoinPolicy.Message(JoinVerdict.WrongVersion); return; }
#if ETR_DEV_TOOLS || UNITY_EDITOR
                if (asSpectator) { ConnectSpectator(list[0]); return; }
#endif
                JoinSteam(list[0].Id);
            }, spectateNext);
'@.TrimEnd()
Save-Src

# ---- the run: the players are counted without the watchers
$SC = $A + 'Scripts\Net\RunSync.cs'; Open-Src $SC
Rep '        public int ExpectedPlayers => expected.Value;' @'
        public int ExpectedPlayers => expected.Value;
        /// <summary>Host: the players connected - everybody but the developers who watch (<see cref="NetSession.PlayersConnected"/>).</summary>
        int PlayerCount => NetSession.Instance != null ? NetSession.Instance.PlayersConnected : NetworkManager.ConnectedClientsIds.Count;
'@.TrimEnd()
Rep '                if (started.Value && expected.Value != NetworkManager.ConnectedClientsIds.Count) expected.Value = NetworkManager.ConnectedClientsIds.Count;' @'
                int players = PlayerCount;
                if (started.Value && expected.Value != players) expected.Value = players;
'@.TrimEnd()
Save-Src
$SC = $A + 'Scripts\Net\RunSync.Intro.cs'; Open-Src $SC
Rep '            int party = NetworkManager.ConnectedClientsIds.Count, holders = introHolders.Count;' '            int party = PlayerCount, holders = introHolders.Count;'
Rep '            if (introSkipped.Value || introStart.Value < 0.0) return;           // nothing to vote on' @'
            if (introSkipped.Value || introStart.Value < 0.0) return;           // nothing to vote on
            if (holding && NetSession.Instance != null && NetSession.Instance.IsSpectatorClient(who)) return;           // (a developer who watches has no vote)
'@.TrimEnd()
Save-Src

# ---- the voice: a developer who watches neither speaks nor hears
$SC = $A + 'Scripts\Net\Voice\VoiceChat.cs'; Open-Src $SC
Rep '        static bool HasRadio(ulong clientId) => RadioOf != null && RadioOf(clientId);' @'
        static bool HasRadio(ulong clientId) => RadioOf != null && RadioOf(clientId);
        /// <summary>This machine is a developer who watches the match (the owner, 2026-10-23: nobody hears him): his microphone is never opened and no voice is played for him.</summary>
        static bool Watching => NetSession.Instance != null && NetSession.Instance.IsSpectator;
        /// <summary>Host: is this connection a developer who watches? His voice is never relayed and none is sent to him.</summary>
        static bool IsWatcher(ulong clientId) => NetSession.Instance != null && NetSession.Instance.IsSpectatorClient(clientId);
'@.TrimEnd()
Rep '            bool wantMic = (online || solo) && soloMicOk && !Muted && mode != Mode.Off' '            bool wantMic = (online || solo) && soloMicOk && !Muted && !Watching && mode != Mode.Off'
Rep '            if (nm == null) return;
            r.ReadValueSafe(out ulong claimed);' '            if (nm == null) return;
            if (nm.IsServer && IsWatcher(sender)) return;           // (a developer who watches is never heard)
            r.ReadValueSafe(out ulong claimed);'
Rep '                if (id == speaker || id == self) continue;' '                if (id == speaker || id == self || IsWatcher(id)) continue;           // (nor does a developer who watches hear anybody)'
Rep '            var vn = me ? me.GetComponent<VoiceNoise>() : SoloVoiceNoise();' '            if (Watching) { HearingRadius = 0f; return; }           // (a developer who watches makes no noise: he has no body and no microphone)
            var vn = me ? me.GetComponent<VoiceNoise>() : SoloVoiceNoise();'
Rep '            var decoder = VoiceCodecs.DecoderFor(codec);
            if (decoder == null) return;
            if (codec == VoiceCodecs.Steam' '            if (Watching) return;
            var decoder = VoiceCodecs.DecoderFor(codec);
            if (decoder == null) return;
            if (codec == VoiceCodecs.Steam'
Save-Src

# ---- the camera follows a player without a body of his own; the busted-player spectator leaves the camera to it
$SC = $A + 'Scripts\CameraFollow.cs'; Open-Src $SC
Rep '            if (!target) return;' '            if (!target && !spectate) return;           // (a developer who watches has no body of his own: DevSpectator)'
Save-Src
$SC = $A + 'Scripts\Spectator.cs'; Open-Src $SC
Rep '            if (!rig && Time.unscaledTime >= nextLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextLookup = Time.unscaledTime + 0.5f; }' '            if (Net.NetSession.Instance != null && Net.NetSession.Instance.IsSpectator) return;           // (a developer who watches has the camera: DevSpectator)
            if (!rig && Time.unscaledTime >= nextLookup) { rig = FindFirstObjectByType<CameraFollow>(); nextLookup = Time.unscaledTime + 0.5f; }'
Save-Src

# ---- the screens: the cursor is free while the spectator's menu is up; no player badge for a watcher; the lobby page of a watcher
$SC = $A + 'Scripts\TestMode.cs'; Open-Src $SC
Rep '        public static bool PanelOpen => panelOpen || DevTerminal.Open;' '        public static bool PanelOpen => panelOpen || DevTerminal.Open || DevSpectator.MenuOpen;'
Rep '/// <summary>True while the panel is open (the camera frees the cursor) or the developer terminal is up (<see cref="DevTerminal"/>): both are screens over the game, and everything that asks whether a screen is up asks this.</summary>' '/// <summary>True while the panel is open (the camera frees the cursor), the developer terminal is up (<see cref="DevTerminal"/>) or the spectator''s dev menu is (<see cref="DevSpectator"/>): all are screens over the game, and everything that asks whether a screen is up asks this.</summary>'
Save-Src
$SC = $A + 'Scripts\HeistHud.cs'; Open-Src $SC
Rep '            HudWidgets.PlayerBadge(me && me.IsSpawned ? me.Slot + 1 : 1);          // (alone, offline: Player 1)' '            if (!(Net.NetSession.Instance != null && Net.NetSession.Instance.IsSpectator)) HudWidgets.PlayerBadge(me && me.IsSpawned ? me.Slot + 1 : 1);          // (alone, offline: Player 1; a developer who watches is no player)'
Save-Src
$SC = $A + 'Scripts\Net\LobbyMenu.cs'; Open-Src $SC
Rep @'
        void DrawLobby(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
'@.TrimEnd() @'
        void DrawLobby(NetSession net, MenuKit.Flow f)
        {
            float s = HudTheme.S;
            if (net.IsSpectator) { DrawSpectatorLobby(f); return; }
'@.TrimEnd()
Rep '        // ------------------------------------------------------------------ customize' @'
        /// <summary>The lobby page of a developer who watches (the owner, 2026-10-23): no party list, nothing to get ready; he waits for the host to start the run. Plain tool words.</summary>
        void DrawSpectatorLobby(MenuKit.Flow f)
        {
            f.Gap(18f);
            MenuKit.Header(f.Row(28f, 8f), "DEV SPECTATOR", HudTheme.Info);
            MenuKit.Notice(f.Row(34f, 8f), "YOU ARE WATCHING THIS LOBBY. THE PARTY CANNOT SEE YOU.", HudTheme.Shade(HudTheme.Card, 0.3f), 15f);
            MenuKit.Notice(f.Row(34f, 0f), "WAITING FOR THE HOST TO START...", HudTheme.Shade(HudTheme.Card, 0.3f), 16f);
        }

        // ------------------------------------------------------------------ customize
'@.TrimEnd()
Save-Src

# ---- the terminal: spectate CODE, and the Steam ID line says whether the account is on the list
$SC = $A + 'Scripts\Dev\DevTerminal.cs'; Open-Src $SC
Rep '                    Print("help  clear  close  lock  steamid");' '                    Print("help  clear  close  lock  steamid  spectate CODE");'
Rep '                        Print(steam != null && steam.Ready ? "Steam ID: " + Steamworks.SteamUser.GetSteamID().m_SteamID : "Steam is not running.");' '                        ulong mine = steam != null && steam.Ready ? Steamworks.SteamUser.GetSteamID().m_SteamID : 0UL;
                        Print(mine != 0UL ? "Steam ID: " + mine + (DevAccounts.IsDeveloper(mine) ? "   (on the developer list)" : "   (not on the developer list: add it to Resources/DevAccounts.txt)") : "Steam is not running.");'
Rep '                default: Print("Unknown command."); break;' @'
                case "spectate":
                    {
                        var net = Net.NetSession.Instance;
                        if (net == null) { Print("There is no network session."); break; }
                        string why = net.SpectateByCode(rest);
                        Print(why.Length == 0 ? "Looking for the lobby. Close the terminal (CTRL + F1) to watch the menu." : why);
                        break;
                    }
                default: Print("Unknown command."); break;
'@.TrimEnd()
Save-Src

# ---- the scenarios and the pure test are registered
$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void PingNet() => Start("pingnet");' ('        public static void PingNet() => Start("pingnet");' + "`n" + '        public static void DevSpectate() => Start("devspectate");' + "`n" + '        public static void DevDenied() => Start("devdenied");')
Rep '                case "pingnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? PingNetHostScenario() : PingNetClientScenario(); break;' ('                case "pingnet": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? PingNetHostScenario() : PingNetClientScenario(); break;' + "`n" + '                case "devspectate": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? DevSpectateHostScenario() : DevSpectateClientScenario(); break;' + "`n" + '                case "devdenied": yield return System.Environment.GetEnvironmentVariable("BE_QA_ROLE") == "host" ? DevDeniedHostScenario() : DevDeniedClientScenario(); break;')
Save-Src
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            Asks1023GTest();' ('            Asks1023GTest();' + "`n" + '            Asks1023HTest();')
Save-Src
"T29 patched"
