using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Using a consumable in a networked run (the owner, 2026-10-25; <see cref="ConsumableRules"/>): the player asks, the host decides - the can is in that player's pack, he is free (not pinned or busted), the kind is not cooling down and (invisible) not used this round -
    /// and then the can is gone everywhere and every machine starts the effect for that player (<see cref="PlayerBuffs.StartLocal"/>), so the host's guards see (or do not see) him as he is and the other players hear (or do not hear) his steps.
    /// </summary>
    public partial class RunSync
    {
        /// <summary>The local player uses this consumable of his pack: the host (or this machine, if it is the host) decides.</summary>
        public void RequestUseConsumable(Pickup p)
        {
            if (p == null || p.NetId < 0) return;
            if (IsServer) HostUseConsumable(Me, p.NetId); else UseConsumableRequestRpc((ushort)p.NetId);
        }

        [Rpc(SendTo.Server)]
        void UseConsumableRequestRpc(ushort id, RpcParams rp = default) => HostUseConsumable(rp.Receive.SenderClientId, id);

        void HostUseConsumable(ulong who, int id)
        {
            var np = PlayerOf(who);
            var p = NetIds.Get<Pickup>(id);
            if (np == null || p == null) return;
            var bp = np.GetComponent<Backpack>();
            var buffs = np.GetComponent<PlayerBuffs>();
            if (bp == null || buffs == null || !bp.Contains(p) || !p.TryGetComponent(out ConsumableItem item)) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;               // (pinned or busted: no hands free)
            var check = buffs.Check(item.kind);
            if (check != UseCheck.Ok) { Notice(who, ConsumableRules.Refusal(item.kind, check, buffs.Set.CooldownLeft(item.kind, Time.time))); return; }
            var kind = item.kind;
            bp.RemoveUsed(p);
            BroadcastConsumed(p.NetId);                                                          // (the other machines destroy their copy)
            Destroy(p.gameObject);
            ConsumableStartedRpc((byte)np.Slot, (byte)kind);
            ConsumablesUsed++;
        }

        /// <summary>Consumables the host has accepted (for the tests).</summary>
        public int ConsumablesUsed { get; private set; }

        [Rpc(SendTo.ClientsAndHost)]
        void ConsumableStartedRpc(byte slot, byte kind)
        {
            if (kind >= ConsumableRules.Count) return;
            var np = NetPlayer.BySlot(slot);
            var buffs = np ? np.GetComponent<PlayerBuffs>() : null;
            if (buffs) buffs.StartLocal((ConsumableKind)kind);
        }
    }
}
