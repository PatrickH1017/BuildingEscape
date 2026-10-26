using System;
using System.Collections.Generic;
using System.Globalization;

namespace BuildingEscape
{
    /// <summary>Why a developer's spectator connection was refused (or not).</summary>
    public enum SpectateVerdict { Allow, NotInThisBuild, NotADeveloper, RunStarted, TooMany, WrongVersion }

    /// <summary>
    /// The pure rules of the developer spectator mode (the owner, 2026-10-23: a developer joins somebody else's match as an invisible spectator to watch how it goes, and the dev menu works on that match; his four answers: before Start only, recognised by his STEAM
    /// ACCOUNT, in the friends' builds and the Editor only, fully invisible). No Unity types, so every rule is tested outside the editor: the list of developer accounts, the connection payload, and the host's verdict. The accounts are Steam IDs in a text
    /// list (<c>Resources/DevAccounts.txt</c>, empty until the owner adds the developers); Steam itself proves which account is connecting (<c>SteamNetworkTransport.TryGetRemoteSteamId</c>), so there is no password on the network.
    /// </summary>
    public static class DevAccessRules
    {
        /// <summary>The tag a spectator's connection payload ends with.</summary>
        public const string SpectatorTag = "spec";

        /// <summary>How many developers may watch one match at once.</summary>
        public const int MaxSpectators = 2;

        /// <summary>
        /// The Steam IDs of a developer list: one number per line; a # starts a comment (a whole line or the end of one); blank lines and anything that is not a positive number are ignored (a typo can only ever admit nobody).
        /// </summary>
        public static HashSet<ulong> ParseList(string text)
        {
            var set = new HashSet<ulong>();
            if (string.IsNullOrEmpty(text)) return set;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;
                if (ulong.TryParse(line, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id != 0UL) set.Add(id);
            }
            return set;
        }

        /// <summary>Is this account on the list? (Account 0 - Steam not running - never is.)</summary>
        public static bool IsDeveloper(HashSet<ulong> list, ulong steamId) => steamId != 0UL && list != null && list.Contains(steamId);

        /// <summary>The text a client sends when it connects: "version|steamid", with "|spec" at the end for a spectator.</summary>
        public static string Payload(string version, ulong steamId, bool spectator) => version + "|" + steamId.ToString(CultureInfo.InvariantCulture) + (spectator ? "|" + SpectatorTag : "");

        /// <summary>Reads a connection payload (the host does this for every client): the version, the Steam ID the client claims (the host never trusts it: Steam says who it is) and whether it asks to spectate.</summary>
        public static void ParsePayload(string text, out string version, out ulong claimed, out bool spectator)
        {
            version = ""; claimed = 0UL; spectator = false;
            if (string.IsNullOrEmpty(text)) return;
            var parts = text.Split('|');
            version = parts[0];
            if (parts.Length > 1) ulong.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out claimed);
            spectator = parts.Length > 2 && parts[2] == SpectatorTag;
        }

        /// <summary>The host's verdict on a spectator: the tools must be in this build, the run must not have started (the owner: before Start only), there must be room, the version must match and the account Steam proved must be a developer's.</summary>
        public static SpectateVerdict Judge(bool toolsInBuild, bool runStarted, int spectatorsNow, bool versionOk, bool accountIsDeveloper)
        {
            if (!toolsInBuild) return SpectateVerdict.NotInThisBuild;
            if (!accountIsDeveloper) return SpectateVerdict.NotADeveloper;          // (before anything else is said: a stranger learns nothing about the state of the match)
            if (runStarted) return SpectateVerdict.RunStarted;
            if (spectatorsNow >= MaxSpectators) return SpectateVerdict.TooMany;
            if (!versionOk) return SpectateVerdict.WrongVersion;
            return SpectateVerdict.Allow;
        }

        /// <summary>The reason a refused spectator is shown (plain tool words, no game text).</summary>
        public static string Message(SpectateVerdict v) => v switch
        {
            SpectateVerdict.NotInThisBuild => "Spectating is not in this build.",
            SpectateVerdict.NotADeveloper => "This Steam account is not on the developer list.",
            SpectateVerdict.RunStarted => "The run has already started: a spectator joins before Start.",
            SpectateVerdict.TooMany => "There are already " + MaxSpectators + " spectators.",
            SpectateVerdict.WrongVersion => "Game version mismatch.",
            _ => "",
        };

        /// <summary>The players a count of connections stands for: everybody connected except the spectators (the party size, the player limit, the roster, the intro's vote and the spawns all count players only).</summary>
        public static int PlayersAmong(int connections, int spectators) => Math.Max(0, connections - Math.Max(0, spectators));
    }
}
