namespace BuildingEscape.Net
{
    /// <summary>Every shared network constant in one place.</summary>
    public static class NetConst
    {
        public const int MaxPlayers = 5;

        /// <summary>Lobby filter tag. AppID 480 (Spacewar) is shared with other games, so we tag our own lobbies.</summary>
        public const string GameTag = "overtime-escape";

        /// <summary>Bump when the wire format or gameplay rules change. Players on different versions cannot join each other.</summary>
        public const string ProtocolVersion = "7";   // 7: armed guards (a flag in the guard snapshot and a shot message), the pew pistol in the pack. 6: five players, players start in the elevator and are held until the floor is built, request / voice rate limits. 5: the alarm panel in the outpost, direction signs and more loot on the floors (a floor is built differently, so old and new cannot share a run). 4: the guard outpost's lock hack, alarm and the floor safes. 3: players open and close doors with E. 2: custody (pinned / busted), repeating backup and the vendor buy-back

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
