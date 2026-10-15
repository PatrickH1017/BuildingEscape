namespace BuildingEscape.Net
{
    /// <summary>Every shared network constant in one place.</summary>
    public static class NetConst
    {
        public const int MaxPlayers = 4;

        /// <summary>Lobby filter tag. AppID 480 (Spacewar) is shared with other games, so we tag our own lobbies.</summary>
        public const string GameTag = "overtime-escape";

        /// <summary>Bump when the wire format or gameplay rules change. Players on different versions cannot join each other.</summary>
        public const string ProtocolVersion = "2";   // 2: custody (pinned / busted), repeating backup and the vendor buy-back

        public const string BootScene = "Boot";
        public const string MenuScene = "Menu";
        public const string RunScene = "Greybox";

        public const int CodeLength = 5;
        public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I

        // Steam lobby data keys
        public const string KeyGame = "game";
        public const string KeyVersion = "ver";
        public const string KeyCode = "code";
        public const string KeyName = "name";
        public const string KeyListed = "listed";   // "1" = shown in the public browser
        public const string KeyState = "state";     // StateOpen / StateInRun
        public const string StateOpen = "open";
        public const string StateInRun = "run";
        public const string MemberReady = "ready";  // per-member data, "1" when ready
        public const string MemberLook = "look";    // per-member data, PlayerAppearance string

        public static string BuildVersion => UnityEngine.Application.version + "/" + ProtocolVersion;
    }
}
