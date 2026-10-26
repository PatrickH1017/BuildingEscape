namespace BuildingEscape
{
    /// <summary>What a developer who watches a match may ask the host to do in it (the commands of the dev menu that are safe in a networked run).</summary>
    public enum DevCommand : byte { None = 0, Cash = 1, GoToLevel = 2, CompleteQuota = 3, StunGuards = 4, CallBackup = 5, CancelBackup = 6, GodMode = 7, FreeTeam = 8 }

    /// <summary>
    /// The pure rules of the spectator's dev menu (the owner, 2026-10-23: "dev menu works on that match, host checks sender"): the dev menu's commands that act on the match itself and run on the host (cash, a level, the quota, the guards, the backup call, god mode), with the
    /// arguments the menu offers, and the host's gate for a command message. The menu's other commands (an item in the pack, a teleport of the body) need a player and are not offered to a spectator. No Unity types, so every rule is tested outside the editor.
    /// </summary>
    public static class DevCommandRules
    {
        /// <summary>The amounts of cash the menu adds.</summary>
        public static readonly int[] CashAmounts = { 100, 1000, 10000 };

        /// <summary>How long the menu's "stun all guards" holds them (as the player's own dev menu does).</summary>
        public const float StunSeconds = 30f;

        /// <summary>Is this command, with this argument, one the host runs? A cash amount the menu offers, a level inside the tower; the others take no argument.</summary>
        public static bool Valid(DevCommand cmd, int arg, int totalFloors)
        {
            switch (cmd)
            {
                case DevCommand.Cash: return System.Array.IndexOf(CashAmounts, arg) >= 0;
                case DevCommand.GoToLevel: return arg >= 0 && arg < totalFloors;
                case DevCommand.CompleteQuota:
                case DevCommand.StunGuards:
                case DevCommand.CallBackup:
                case DevCommand.CancelBackup:
                case DevCommand.GodMode:
                case DevCommand.FreeTeam: return true;
                default: return false;
            }
        }

        /// <summary>The host's gate for a command message: the dev tools are in this build, the sender is a developer who was let in as a spectator (the host remembers who), and the command is a valid one.</summary>
        public static bool Accept(bool toolsInBuild, bool senderIsSpectator, DevCommand cmd, int arg, int totalFloors) => toolsInBuild && senderIsSpectator && Valid(cmd, arg, totalFloors);

        /// <summary>The words on the menu's button for a command (plain tool words).</summary>
        public static string Label(DevCommand cmd, int arg)
        {
            switch (cmd)
            {
                case DevCommand.Cash: return "+$" + arg.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
                case DevCommand.GoToLevel: return arg == 0 ? "Basement" : "F" + arg;
                case DevCommand.CompleteQuota: return "Complete quota (unlock elevator up)";
                case DevCommand.StunGuards: return "Stun all guards 30s";
                case DevCommand.CallBackup: return "Call backup now";
                case DevCommand.CancelBackup: return "Cancel backup call";
                case DevCommand.GodMode: return "God mode (guards can't catch the team)";
                case DevCommand.FreeTeam: return "Free every pinned or busted player";
                default: return "";
            }
        }
    }
}
