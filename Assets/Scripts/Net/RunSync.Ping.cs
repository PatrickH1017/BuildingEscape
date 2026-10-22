using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Pings over the network (the owner, 2026-10-21: "Add a ping system"): a player marks what the crosshair is on (<see cref="PingSystem"/> shows the marker on the pinger's own machine at once); the host checks the message and tells everybody,
    /// and every other machine puts the marker up. The marker is the item's network id (or the door's number) and the point the pinger marked; nothing is simulated, so a ping changes nothing in the world.
    /// </summary>
    public partial class RunSync
    {
        // A player may ping a burst of 3, then one and a half a second; the host drops the rest (a modified client cannot flood the team with markers).
        static readonly RateLimiter pingBudget = new RateLimiter(3f, 1.5f);

        /// <summary>The local player pinged: the host decides what the team sees.</summary>
        public void RequestPing(byte kind, ushort id, Vector3 pos)
        {
            if (IsServer) HostPing(Me, kind, id, pos); else PingRequestRpc(kind, id, pos);
        }

        [Rpc(SendTo.Server)]
        void PingRequestRpc(byte kind, ushort id, Vector3 pos, RpcParams rp = default) => HostPing(rp.Receive.SenderClientId, kind, id, pos);

        void HostPing(ulong who, byte kind, ushort id, Vector3 pos)
        {
            var np = PlayerOf(who);
            if (np == null || kind > (byte)PingTargetKind.Door || !IsFinite(pos)) return;
            if (!pingBudget.Allow(who, Time.unscaledTime)) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;               // (pinned or busted: no pings)
            if (!PingRules.HostAccepts(np.transform.position, pos)) return;                      // (a real place, within the range: the sender's view is a moment old)
            PingedRpc((byte)np.Slot, kind, id, pos);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void PingedRpc(byte slot, byte kind, ushort id, Vector3 pos) => PingSystem.OnPinged(slot, kind, id, pos);
    }
}
