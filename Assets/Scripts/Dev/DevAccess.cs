namespace BuildingEscape
{
    /// <summary>
    /// Whether the developer tools are on offer in this build, and whether they are unlocked on this machine in this session (the owner, 2026-10-23: "To access the dev-menu you should need to type @Catdog813 into the terminal that is opened from clicking
    /// CTRL + F1. The dev-menu should no longer be accessible by just clicking F1"). The tools - the terminal (<c>DevTerminal</c>), the dev menu (<see cref="TestMode"/>) and spectating a match - are compiled into the Editor and into the friends' builds
    /// (the scripting define <c>ETR_DEV_TOOLS</c> that <c>DemoBuild</c> sets) and into no release build. The unlocked state lives in memory only: it is never saved, so every start of the game begins locked.
    /// </summary>
    public static class DevAccess
    {
        /// <summary>Are the developer tools part of this build at all?</summary>
#if ETR_DEV_TOOLS || UNITY_EDITOR
        public const bool Compiled = true;
#else
        public const bool Compiled = false;
#endif

        /// <summary>Has the code been typed in the terminal in this session?</summary>
        public static bool Unlocked { get; private set; }

        /// <summary>The code was typed (the terminal calls this). Does nothing in a build without the tools.</summary>
        public static void Unlock() { if (Compiled) Unlocked = true; }

        /// <summary>Locked again: F1 does nothing, the menu closes.</summary>
        public static void Lock() { Unlocked = false; }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Unlocked = false; }
    }
}
