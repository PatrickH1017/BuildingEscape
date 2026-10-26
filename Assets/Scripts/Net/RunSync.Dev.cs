#if ETR_DEV_TOOLS || UNITY_EDITOR
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The spectator's dev menu over the network (the owner, 2026-10-23: a developer who watches a match can use the dev menu on it, and "host checks sender"): the spectator's machine sends the host one command (<see cref="DevCommand"/> and an argument), the host runs it
    /// only if the sender is a developer who was let in as a spectator (<see cref="NetSession.IsSpectatorClient"/>: the host remembers who, nobody else is ever told), the command is a valid one (<see cref="DevCommandRules"/>) and the sender is not flooding.
    /// Compiled into the Editor and the friends' builds only; the host's answer is the effect itself (cash, a new level, ...) - nothing is announced.
    /// </summary>
    public partial class RunSync
    {
        /// <summary>A spectator's machine: ask the host to run one command of the dev menu on the match.</summary>
        public void RequestDevCommand(DevCommand cmd, int arg)
        {
            if (IsServer || !IsSpawned) return;                    // (the host has its own dev menu; only a spectator's machine sends this)
            DevCommandRpc((byte)cmd, arg);
        }

        [Rpc(SendTo.Server)]
        void DevCommandRpc(byte cmd, int arg, RpcParams rp = default) => HostDevCommand(rp.Receive.SenderClientId, (DevCommand)cmd, arg);

        /// <summary>Host: runs the command when it comes from a spectator and is valid. Returns whether it ran (the tests call it with another sender to see it refused).</summary>
        public bool HostDevCommand(ulong who, DevCommand cmd, int arg)
        {
            if (!IsServer) return false;
            var net = NetSession.Instance; var tm = TowerManager.Instance;
            bool fromSpectator = net != null && net.IsSpectatorClient(who);
            if (!DevCommandRules.Accept(DevAccess.Compiled, fromSpectator, cmd, arg, tm != null ? tm.totalFloors : 0)) return false;
            if (!requestBudget.Allow(who, Time.unscaledTime)) return false;
            return DevCommands.Run(cmd, arg).Length > 0;
        }
    }
}
#endif
