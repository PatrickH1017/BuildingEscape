using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace BuildingEscape.Net
{
    public enum LobbyVisibility
    {
        /// <summary>Shown in the public browser; joinable by code and invite.</summary>
        Public,
        /// <summary>Not in the browser; joinable by code and invite.</summary>
        CodeOnly,
        /// <summary>Friends can Join Game from their list; joinable by invite.</summary>
        FriendsOnly
    }

    public struct LobbyListing
    {
        public CSteamID Id;
        public string Name, Version, Code;
        /// <summary>The Steam ID of the lobby's owner (the host); 0 for a lobby that does not name it (an older build).</summary>
        public ulong Host;
        public int Members, Max;
        public bool VersionOk;
    }

    public struct LobbyMember
    {
        public CSteamID Id;
        public string Name;
        public bool Ready, IsOwner, IsLocal;
        /// <summary>The member's PlayerAppearance string (empty until they set one).</summary>
        public string Look;
    }

    /// <summary>
    /// Thin wrapper over Steam Matchmaking for ONE lobby: create / find / join / leave, member data,
    /// invites. It knows nothing about Netcode; NetSession connects the two.
    /// </summary>
    public sealed class LobbyService : IDisposable
    {
        public CSteamID Current { get; private set; }
        public bool InLobby => Current.IsValid();
        public CSteamID Owner => InLobby ? SteamMatchmaking.GetLobbyOwner(Current) : CSteamID.Nil;
        public bool IsOwner => InLobby && Owner == SteamUser.GetSteamID();
        public string Code => InLobby ? SteamMatchmaking.GetLobbyData(Current, NetConst.KeyCode) : "";
        public string State => InLobby ? SteamMatchmaking.GetLobbyData(Current, NetConst.KeyState) : "";

        /// <summary>Members joined/left or lobby data changed.</summary>
        public event Action Changed;
        /// <summary>The player accepted an invite / Join Game from the Steam overlay or friends list.</summary>
        public event Action<CSteamID> JoinRequested;

        readonly Callback<LobbyChatUpdate_t> chatUpdate;
        readonly Callback<LobbyDataUpdate_t> dataUpdate;
        readonly Callback<GameLobbyJoinRequested_t> joinRequested;
        readonly CallResult<LobbyCreated_t> createResult = CallResult<LobbyCreated_t>.Create();
        readonly CallResult<LobbyEnter_t> enterResult = CallResult<LobbyEnter_t>.Create();
        readonly CallResult<LobbyMatchList_t> listResult = CallResult<LobbyMatchList_t>.Create();

        public LobbyService()
        {
            chatUpdate = Callback<LobbyChatUpdate_t>.Create(d => { if (d.m_ulSteamIDLobby == Current.m_SteamID) Changed?.Invoke(); });
            dataUpdate = Callback<LobbyDataUpdate_t>.Create(d => { if (d.m_ulSteamIDLobby == Current.m_SteamID) Changed?.Invoke(); });
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(d => JoinRequested?.Invoke(d.m_steamIDLobby));
        }

        // ------------------------------------------------------------------ create / join / leave

        public void Create(LobbyVisibility vis, string name, Action<bool, string> done)
        {
            var type = vis switch
            {
                LobbyVisibility.Public => ELobbyType.k_ELobbyTypePublic,
                LobbyVisibility.CodeOnly => ELobbyType.k_ELobbyTypeInvisible, // searchable by code, hidden from friends
                _ => ELobbyType.k_ELobbyTypeFriendsOnly,
            };
            createResult.Set(SteamMatchmaking.CreateLobby(type, NetConst.MaxPlayers), (r, ioFail) =>
            {
                if (ioFail || r.m_eResult != EResult.k_EResultOK) { done(false, $"Could not create a lobby ({(ioFail ? "no connection to Steam" : r.m_eResult.ToString())})."); return; }
                Current = new CSteamID(r.m_ulSteamIDLobby);
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyGame, NetConst.GameTag);
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyVersion, NetConst.BuildVersion);
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyCode, NewCode());
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyName, name);
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyListed, vis == LobbyVisibility.Public ? "1" : "0");
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyState, NetConst.StateOpen);
                SteamMatchmaking.SetLobbyData(Current, NetConst.KeyHost, SteamUser.GetSteamID().m_SteamID.ToString());          // (a developer who watches connects to the host without joining the lobby)
                SteamMatchmaking.SetLobbyMemberData(Current, NetConst.MemberReady, "0");
                done(true, null);
            });
        }

        public void Join(CSteamID id, Action<bool, string> done)
        {
            enterResult.Set(SteamMatchmaking.JoinLobby(id), (r, ioFail) =>
            {
                if (ioFail || r.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                {
                    done(false, ioFail ? "No connection to Steam." : EnterMessage((EChatRoomEnterResponse)r.m_EChatRoomEnterResponse));
                    return;
                }
                Current = new CSteamID(r.m_ulSteamIDLobby);
                if (SteamMatchmaking.GetLobbyData(Current, NetConst.KeyGame) != NetConst.GameTag)
                {
                    Leave();
                    done(false, "That lobby is not an Employees Take Revenge lobby.");
                    return;
                }
                SteamMatchmaking.SetLobbyMemberData(Current, NetConst.MemberReady, "0");
                done(true, null);
            });
        }

        public void Leave()
        {
            if (!InLobby) return;
            SteamMatchmaking.LeaveLobby(Current);
            Current = CSteamID.Nil;
        }

        // ------------------------------------------------------------------ search

        /// <summary>Lists open lobbies. With a code, returns only the lobby with that code (listed or not).</summary>
        public void Search(string code, Action<List<LobbyListing>> done, bool includeFull = false)
        {
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            if (!includeFull) SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);          // (a developer who watches takes no seat: he also finds a lobby that is full)
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(50);
            SteamMatchmaking.AddRequestLobbyListStringFilter(NetConst.KeyGame, NetConst.GameTag, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(NetConst.KeyState, NetConst.StateOpen, ELobbyComparison.k_ELobbyComparisonEqual);
            if (!string.IsNullOrEmpty(code))
                SteamMatchmaking.AddRequestLobbyListStringFilter(NetConst.KeyCode, code.Trim().ToUpperInvariant(), ELobbyComparison.k_ELobbyComparisonEqual);
            else
                SteamMatchmaking.AddRequestLobbyListStringFilter(NetConst.KeyListed, "1", ELobbyComparison.k_ELobbyComparisonEqual);

            listResult.Set(SteamMatchmaking.RequestLobbyList(), (r, ioFail) =>
            {
                var list = new List<LobbyListing>();
                if (!ioFail)
                    for (int i = 0; i < r.m_nLobbiesMatching; i++)
                    {
                        var id = SteamMatchmaking.GetLobbyByIndex(i);
                        string ver = SteamMatchmaking.GetLobbyData(id, NetConst.KeyVersion);
                        list.Add(new LobbyListing
                        {
                            Id = id,
                            Name = SteamMatchmaking.GetLobbyData(id, NetConst.KeyName),
                            Code = SteamMatchmaking.GetLobbyData(id, NetConst.KeyCode),
                            Version = ver,
                            VersionOk = ver == NetConst.BuildVersion,
                            Members = SteamMatchmaking.GetNumLobbyMembers(id),
                            Max = SteamMatchmaking.GetLobbyMemberLimit(id),
                            Host = ulong.TryParse(SteamMatchmaking.GetLobbyData(id, NetConst.KeyHost), out var hostId) ? hostId : 0UL,
                        });
                    }
                done(list);
            });
        }

        // ------------------------------------------------------------------ members / state

        public void GetMembers(List<LobbyMember> into)
        {
            into.Clear();
            if (!InLobby) return;
            var me = SteamUser.GetSteamID();
            var owner = Owner;
            int n = SteamMatchmaking.GetNumLobbyMembers(Current);
            for (int i = 0; i < n; i++)
            {
                var id = SteamMatchmaking.GetLobbyMemberByIndex(Current, i);
                var m = new LobbyMember
                {
                    Id = id,
                    Name = SteamFriends.GetFriendPersonaName(id),
                    Ready = SteamMatchmaking.GetLobbyMemberData(Current, id, NetConst.MemberReady) == "1",
                    Look = SteamMatchmaking.GetLobbyMemberData(Current, id, NetConst.MemberLook) ?? "",
                    IsOwner = id == owner,
                    IsLocal = id == me,
                };
                if (m.IsOwner) into.Insert(0, m); else into.Add(m);
            }
        }

        public bool IsMember(CSteamID id)
        {
            if (!InLobby) return false;
            int n = SteamMatchmaking.GetNumLobbyMembers(Current);
            for (int i = 0; i < n; i++)
                if (SteamMatchmaking.GetLobbyMemberByIndex(Current, i) == id) return true;
            return false;
        }

        public void SetReady(bool ready)
        {
            if (InLobby) SteamMatchmaking.SetLobbyMemberData(Current, NetConst.MemberReady, ready ? "1" : "0");
        }

        /// <summary>Shares this player's character look with the lobby (everyone sees it on the stage).</summary>
        public void SetLook(string look)
        {
            if (InLobby) SteamMatchmaking.SetLobbyMemberData(Current, NetConst.MemberLook, look ?? "");
        }

        /// <summary>Host: closes the lobby to new members and to the browser once the run starts.</summary>
        public void Lock()
        {
            if (!IsOwner) return;
            SteamMatchmaking.SetLobbyData(Current, NetConst.KeyState, NetConst.StateInRun);
            SteamMatchmaking.SetLobbyJoinable(Current, false);
        }

        /// <summary>The run is over and the party is back in the lobby: the owner opens it again (friends may join, the state says the lobby is open).</summary>
        public void Unlock()
        {
            if (!IsOwner) return;
            SteamMatchmaking.SetLobbyData(Current, NetConst.KeyState, NetConst.StateOpen);
            SteamMatchmaking.SetLobbyJoinable(Current, true);
        }

        /// <summary>Sends this lobby to one friend (they get an in-game pop-up, or a Steam notification if not playing).</summary>
        public bool Invite(CSteamID friend) => InLobby && SteamMatchmaking.InviteUserToLobby(Current, friend);

        public void ShowInviteDialog()
        {
            if (InLobby) SteamFriends.ActivateGameOverlayInviteDialog(Current);
        }

        /// <summary>Lets friends press Join Game in their Steam friends list.</summary>
        public void PublishPresence()
        {
            if (!InLobby) return;
            SteamFriends.SetRichPresence("connect", "+connect_lobby " + Current.m_SteamID);
            SteamFriends.SetRichPresence("steam_display", "#StatusInLobby");
        }

        public void ClearPresence() { SteamFriends.ClearRichPresence(); }

        // ------------------------------------------------------------------ helpers

        static string NewCode()
        {
            var chars = new char[NetConst.CodeLength];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = NetConst.CodeAlphabet[UnityEngine.Random.Range(0, NetConst.CodeAlphabet.Length)];
            return new string(chars);
        }

        static string EnterMessage(EChatRoomEnterResponse r) => r switch
        {
            EChatRoomEnterResponse.k_EChatRoomEnterResponseFull => "The lobby is full.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist => "That lobby no longer exists.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed => "You are not allowed to join that lobby.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned => "You are banned from that lobby.",
            EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited => "Your Steam account is limited.",
            _ => "Could not join the lobby (" + r + ")."
        };

        public void Dispose()
        {
            Leave();
            chatUpdate?.Dispose(); dataUpdate?.Dispose(); joinRequested?.Dispose();
            createResult?.Dispose(); enterResult?.Dispose(); listResult?.Dispose();
        }
    }
}