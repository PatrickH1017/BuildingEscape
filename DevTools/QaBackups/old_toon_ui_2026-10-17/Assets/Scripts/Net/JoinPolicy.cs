namespace BuildingEscape.Net
{
    public enum JoinVerdict { Allow, RunStarted, Full, WrongVersion, NotInLobby }

    /// <summary>The host's join rules as one pure function, so the player limit (NetConst.MaxPlayers) is unit-testable.</summary>
    public static class JoinPolicy
    {
        public static JoinVerdict Evaluate(bool runStarted, int playersConnected, string clientVersion,
                                           string hostVersion, bool isLobbyMember)
        {
            if (runStarted) return JoinVerdict.RunStarted;
            if (playersConnected >= NetConst.MaxPlayers) return JoinVerdict.Full;
            if (clientVersion != hostVersion) return JoinVerdict.WrongVersion;
            if (!isLobbyMember) return JoinVerdict.NotInLobby;
            return JoinVerdict.Allow;
        }

        public static string Message(JoinVerdict v) => v switch
        {
            JoinVerdict.RunStarted => "The run has already started.",
            JoinVerdict.Full => $"The lobby is full ({NetConst.MaxPlayers}/{NetConst.MaxPlayers}).",
            JoinVerdict.WrongVersion => "Game version mismatch. Update the game.",
            JoinVerdict.NotInLobby => "You are not in this lobby.",
            _ => ""
        };
    }
}