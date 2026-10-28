#if ETR_DEV_TOOLS || UNITY_EDITOR
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Host: runs a command of the spectator's dev menu on the match (the owner, 2026-10-23: "dev menu works on that match, host checks sender"). Only <c>RunSync.HostDevCommand</c> calls it, after <see cref="DevCommandRules.Accept"/> let the command in; every command is
    /// one the host's own state already publishes to the party (cash, the level, the quota, the guards, the backup call), so nothing here makes an object the network does not know about. Compiled into the Editor and the friends' builds only (<see cref="DevAccess.Compiled"/>).
    /// </summary>
    public static class DevCommands
    {
        /// <summary>Runs the command; returns a few words on what it did (the tests read them), "" when there was nothing to do it on.</summary>
        public static string Run(DevCommand cmd, int arg)
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            if (hm == null || tm == null) return "";
            switch (cmd)
            {
                case DevCommand.Cash: hm.AddCash(arg); return "cash +" + arg;
                case DevCommand.GoToLevel: tm.GoToLevel(arg); return "level " + arg;
                case DevCommand.CompleteQuota: hm.DebugCompleteFloor(); return "quota";
                case DevCommand.StunGuards:
                    foreach (var g in Object.FindObjectsByType<GuardAI>(FindObjectsSortMode.None)) if (g && !g.IsPuppet) g.Stun(DevCommandRules.StunSeconds, null);
                    return "stun";
                case DevCommand.CallBackup: hm.CallBackup(null); return "backup";
                case DevCommand.CancelBackup: hm.StandDown(); return "stand down";
                case DevCommand.GodMode: hm.godMode = !hm.godMode; return "god " + (hm.godMode ? "on" : "off");
                case DevCommand.FreeTeam:
                    foreach (var p in WobblyPlayerRegistry.All) if (p) { p.PinImmuneUntil = 0f; hm.FreePinned(p); hm.ReleaseBusted(p); }          // (as the dev menu's "Free me", for everybody)
                    return "freed";
                default: return "";
            }
        }
    }
}
#endif
