# Networking (Steam lobbies + Netcode for GameObjects)

Up to **4 players**. One player hosts (listen server); everyone else connects to the host through
Steam's relay, so there is no port forwarding and no dedicated server.

## Flow

```
Boot scene            persistent "Net" object: NetworkManager + transports + SteamService + NetSession
  -> Menu scene       LobbyMenu (IMGUI placeholder) calls NetSession
       Host / Join    Steam lobby (cap 4)  ->  SteamNetworkTransport  ->  Netcode host / client
       Start run      host locks the lobby, loads the Greybox scene for everyone
  -> Greybox scene    host spawns one NetPlayer per client + a RunSync (seed, player count)
                      every machine builds the same floor from the seed (TowerManager)
```

## Files (Assets/Scripts/Net)

| File | Job |
|---|---|
| `NetConst.cs` | Every shared constant: max players, game tag, scene names, lobby data keys |
| `SteamService.cs` | Steam API lifetime: init, callback pump, shutdown, local name/id |
| `LobbyService.cs` | One Steam lobby: create / search / join / leave, members, ready flag, invite, lock |
| `SteamNetworkTransport.cs` | Netcode transport over Steam relay sockets (host listens on its SteamID) |
| `NetSession.cs` | The orchestrator and the only API the menu uses. State machine, join approval, run start, leave |
| `JoinPolicy.cs` | The host's join rules as one pure function (full / run started / version / not in lobby) |
| `NetPlayer.cs` | Owner runs the normal player scripts; everyone else sees a kinematic puppet |
| `OwnerNetworkTransform.cs` | Owner-authoritative transform sync (half precision, yaw only) |
| `RunSync.cs` | Host-spawned: run seed + how many players to wait for |
| `OfflinePlayerOnly.cs` | The scene's solo player removes itself in a networked run |
| `LobbyStage.cs` | The 3D lobby: your character and friends' characters on pads, framed to the right of the menu; close-up while customizing |
| `LobbyMenu.cs`, `BootLoader.cs` | Placeholder menu (Play / Customize / Settings / Quit, browser, lobby) and the Boot -> Menu hop |
| `../PlayerAppearance.cs` | Chosen colour / face / hat / build: saved to PlayerPrefs, shared as a short string, validated on receipt |

## Character customization

Saved in PlayerPrefs (`OvertimeEscape.Appearance`). Friends see your look on the lobby stage through your Steam
lobby member data (`look`), and in the run through an owner-written NetworkVariable on `NetPlayer`. Solo play
uses the saved look too. Local test lobbies show default looks (there is no Steam lobby to carry them).

## Rules the host enforces (JoinPolicy + Steam lobby)

* max 4 players; the Steam lobby is also created with 4 slots
* no joining once the run has started (the lobby is locked and de-listed)
* same build version only (`NetConst.BuildVersion`, bump `ProtocolVersion` when the wire format changes)
* the joiner must be a member of the Steam lobby, and the Steam identity comes from the transport, not from
  anything the client claims
* a client that floods the host (over 128 KB/s sustained) is disconnected

## Lobby kinds

* **Public**: listed in the browser, joinable by code and invite
* **Code only**: not listed, joinable by code and invite (a new lobby takes ~2 s to become searchable)
* **Friends only**: friends use Join Game in their Steam friends list, or an invite

## Testing

* `Play solo` (Menu) or pressing Play in the Greybox scene directly: no networking at all.
* `Host local` / `Join local IP` (Menu): Netcode over UDP on localhost, no Steam needed.
  Two Editors need two project copies (Unity locks a project to one Editor).
* Steam needs the Steam client running; `steam_appid.txt` (480 = Spacewar) sits in the project root.
  Steam refuses a P2P connection to your own account, so a real P2P test needs a second Steam account.
* `Assets/Editor/NetDev`: batch-mode scenario runner used to verify all of the above.

## Not networked yet

Gameplay objects still simulate locally on each machine: loot, guards, the elevator and phase changes,
backpack contents, the vendor and cash. Players, the seed and the floor layout are shared. That is the
next phase (host-authoritative guards and loot, replicated run state).