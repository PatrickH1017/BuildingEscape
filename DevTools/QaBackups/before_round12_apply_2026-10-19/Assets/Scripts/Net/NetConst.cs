namespace BuildingEscape.Net
{
    /// <summary>Every shared network constant in one place.</summary>
    public static class NetConst
    {
        public const int MaxPlayers = 5;

        /// <summary>Lobby filter tag. AppID 480 (Spacewar) is shared with other games, so we tag our own lobbies.</summary>
        public const string GameTag = "employees-take-revenge";

        /// <summary>Bump when the wire format or gameplay rules change. Players on different versions cannot join each other.</summary>
        public const string ProtocolVersion = "23";   // 23: armour (a safe may hold a piece of it instead of a gun, the dealer sells three more things, the unlock mask has more bits); 22: officers carry guns of every kind and wear shields (the host streams both with each guard), bullets damage and never knock a guard over, a guard drops a clip (a pickup with a network id) and a shotgun or sniper hit blows a player up (announced); 21: the guns of the pack (a shot carries its seed and the host traces every pellet; ammunition is announced with its gun; a safe holds the gun of its floor and its spare clip is for that gun, so old and new cannot share a run); 20: a safe may also hold a gold watch (one more item with a network id: a floor holds other things, so old and new cannot share a run); 19: one pistol at most in a safe (a floor holds fewer things, so old and new cannot share a run); 18: several safes on floor 3 and up with pistols in them (the safe messages say which safe, the outpost state has a bit for each); 17: the PVP room's hits (new messages) and the donut break (bit 8 of a guard's flags in the snapshot); 16: punching, and a teammate's swings, punches and hits are heard when he is near (new messages); 15: the outpost door is locked with a code at the security terminal (SECURITY, then LOCK) and shutting it locks nothing, the host ignores presses on the alarm panel that is no longer built; 14: the security computer locks and unlocks doors (new network messages), closing the outpost door from inside locks it for the officers too; 13: the pew lies in the outpost of floor 3 (not 4) and the vendor sells it after floor 3, the outposts have a computer instead of a note, the floors have carpet, windows, a city and glass fronts (a floor is built differently, so old and new cannot share a run); 12: Employees Take Revenge (the rename, a new lobby tag), the floors hold small valuables (watches, phones...) instead of stealable furniture and fewer, roomier treasures (a floor is built differently, so old and new cannot share a run), the loot scanner, the security-office signs, the hallway doors' windows, the TV cutscene (a TV in the docks). 11: the run-start cinematic and proximity voice. 10: the player's pew works in a networked run (a request to the host and an announcement of the shot) and lies in the outpost of floor 4, and guards are armed from floor 4 (a floor holds one more item). 9: two stairwell doors that the backup also comes out of (the doors the backup uses are one byte of three bits now). 8: the guards' own elevator in the corner opposite the players' (the backup arrives in it, not in the outpost) and one guard on duty in every outpost - a floor is built differently, so old and new cannot share a run. 7: armed guards (a flag in the guard snapshot and a shot message), the pew pistol in the pack. 6: five players, players start in the elevator and are held until the floor is built, request / voice rate limits. 5: the alarm panel in the outpost, direction signs and more loot on the floors (a floor is built differently, so old and new cannot share a run). 4: the guard outpost's lock hack, alarm and the floor safes. 3: players open and close doors with E. 2: custody (pinned / busted), repeating backup and the vendor buy-back

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
