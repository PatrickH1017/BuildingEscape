using System.Collections.Generic;
using Steamworks;

namespace BuildingEscape.Net
{
    public struct FriendEntry
    {
        public CSteamID Id;
        public string Name;
        /// <summary>Signed in to Steam (online, away, busy...). These are listed first.</summary>
        public bool Online;
        /// <summary>Currently running this game (same AppID).</summary>
        public bool InThisGame;
        public string Status;
    }

    /// <summary>Reads the Steam friends list for the invite screen: online friends first, then by name.</summary>
    public static class FriendsService
    {
        public static void Refresh(List<FriendEntry> into)
        {
            into.Clear();
            var steam = SteamService.Instance;
            if (steam == null || !steam.Ready) return;

            uint app = SteamUtils.GetAppID().m_AppId;
            int n = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);
            for (int i = 0; i < n; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                var state = SteamFriends.GetFriendPersonaState(id);
                bool online = state != EPersonaState.k_EPersonaStateOffline;
                bool inGame = SteamFriends.GetFriendGamePlayed(id, out FriendGameInfo_t info) && info.m_gameID.AppID().m_AppId == app;
                into.Add(new FriendEntry
                {
                    Id = id,
                    Name = SteamFriends.GetFriendPersonaName(id),
                    Online = online,
                    InThisGame = inGame,
                    Status = inGame ? "playing this game" : StateLabel(state),
                });
            }

            into.Sort((a, b) =>
            {
                if (a.Online != b.Online) return a.Online ? -1 : 1;           // online friends on top
                if (a.InThisGame != b.InThisGame) return a.InThisGame ? -1 : 1; // then those already in the game
                return string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase);
            });
        }

        static string StateLabel(EPersonaState s) => s switch
        {
            EPersonaState.k_EPersonaStateOffline => "offline",
            EPersonaState.k_EPersonaStateBusy => "busy",
            EPersonaState.k_EPersonaStateAway => "away",
            EPersonaState.k_EPersonaStateSnooze => "away",
            _ => "online",
        };
    }
}