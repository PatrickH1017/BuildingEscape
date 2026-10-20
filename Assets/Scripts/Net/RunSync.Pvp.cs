using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>The PVP room's hits over the network (<see cref="PvpRules"/>): the attacker asks, the host decides and tells everybody the victim's health.</summary>
    public partial class RunSync
    {
        readonly Dictionary<ulong, float> lastPvpHit = new Dictionary<ulong, float>();

        /// <summary>The local player's blow or bullet landed on a rival in the PVP room: ask the host to count it (the host's own blows go straight in).</summary>
        public void RequestPvpHit(byte victimSlot, float damage)
        {
            if (IsServer) HostPvpHit(Me, victimSlot, damage); else PvpHitRequestRpc(victimSlot, damage);
        }

        [Rpc(SendTo.Server)]
        void PvpHitRequestRpc(byte victimSlot, float damage, RpcParams rp = default) => HostPvpHit(rp.Receive.SenderClientId, victimSlot, damage);

        void HostPvpHit(ulong who, byte victimSlot, float damage)
        {
            var attacker = PlayerOf(who);
            var victim = NetPlayer.BySlot(victimSlot);
            if (!attacker || !victim || attacker == victim) return;
            if (!(damage > 0f) || damage > PvpRules.MaxClaim) return;                                            // (a client that claims too much is ignored)
            var a = attacker.GetComponent<PvpFighter>(); var v = victim.GetComponent<PvpFighter>();
            if (!a || !v || !PvpRules.CanHurt(a.Inside, v.Inside, a.Down, v.Down, false)) return;
            if ((attacker.transform.position - victim.transform.position).sqrMagnitude > PvpRules.MaxReach * PvpRules.MaxReach) return;
            float now = Time.unscaledTime;
            if (lastPvpHit.TryGetValue(who, out float t) && now - t < 0.25f) return;                             // (no faster than a blow can be thrown)
            lastPvpHit[who] = now;
            v.Hurt(damage);
            PvpStateRpc(victimSlot, v.Health, v.Down);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void PvpStateRpc(byte slot, float health, bool down)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && np.TryGetComponent(out PvpFighter f)) f.SetState(health, down);
        }
    }
}
