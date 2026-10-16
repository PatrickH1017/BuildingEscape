using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The guard outpost's lock and the floor safe in a networked run. The state (lock open, alarm going, safe open) is three bits that the
    /// host publishes with the rest of the run state and every machine applies to its own copy of the floor (<see cref="OutpostController"/>);
    /// a player's attempts - a finished hack, a code typed at the safe, a crowbar - are requests that the host judges.
    /// </summary>
    public partial class RunSync
    {
        // The floor the bits belong to (its epoch) in the high byte and the bits in the low one; all ones: there is nothing to say yet.
        // A machine that is still on the previous floor, or already on the next one, ignores a value that is not for its floor.
        readonly NetworkVariable<ushort> vOutpost = new(0xFFFF);

        const float SafeReachSlack = 2.0f;      // the asking player's position here is a moment old

        void HostPublishOutpost()
        {
            var c = OutpostController.Active;
            vOutpost.Value = c != null && c.BuiltEpoch == Epoch ? (ushort)((Epoch << 8) | c.Bits) : (ushort)0xFFFF;
        }

        void ClientApplyOutpost()
        {
            ushort v = vOutpost.Value;
            if (v == 0xFFFF || (v >> 8) != Epoch) return;
            var c = OutpostController.Active;
            if (c != null && c.BuiltEpoch == Epoch) c.NetApply((byte)(v & 0xFF));
        }

        // ------------------------------------------------------------------ the lock hack

        /// <summary>A player finished the hack on the outpost's lock (won or lost; backing out is not reported).</summary>
        public void RequestHack(bool success)
        {
            if (IsServer) HostHack(Me, success, Epoch); else HackRequestRpc(success, Epoch);
        }

        [Rpc(SendTo.Server)]
        void HackRequestRpc(bool success, byte epoch, RpcParams rp = default) => HostHack(rp.Receive.SenderClientId, success, epoch);

        void HostHack(ulong who, bool success, byte epoch)
        {
            if (epoch != Epoch) return;                                  // about the floor before this one
            var np = PlayerOf(who);
            var c = OutpostController.Active;
            if (np == null || c == null) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!c.InHackReach(np.transform.position)) return;          // not even near the keypad
            c.HostHackResult(success);                                  // the new state reaches everybody with the run state
        }

        // ------------------------------------------------------------------ the safe

        /// <summary>A player tries the floor safe: with a code (<paramref name="pry"/> false) or with a crowbar from his backpack.</summary>
        public void RequestSafe(bool pry, int code)
        {
            code = Mathf.Clamp(code, 0, 9999);
            if (IsServer) HostSafe(Me, pry, code, Epoch); else SafeRequestRpc(pry, (ushort)code, Epoch);
        }

        [Rpc(SendTo.Server)]
        void SafeRequestRpc(bool pry, ushort code, byte epoch, RpcParams rp = default) => HostSafe(rp.Receive.SenderClientId, pry, code, epoch);

        void HostSafe(ulong who, bool pry, int code, byte epoch)
        {
            if (epoch != Epoch) return;
            var np = PlayerOf(who);
            var safe = FloorSafe.Active;
            if (np == null || safe == null || safe.IsOpen) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!safe.InReach(np.transform.position, SafeReachSlack)) return;

            var reply = safe.HostAttempt(pry, code, np.GetComponent<Backpack>());
            switch (reply)
            {
                case SafeReply.Opened:
                    SafeOpenedRpc(Epoch, false);
                    break;
                case SafeReply.OpenedCrowbarBroke:
                    SafeOpenedRpc(Epoch, true);
                    Notice(who, "The crowbar snapped!");
                    break;
                case SafeReply.Ignored:
                    break;
                default:
                    SafeReplyRpc((byte)reply, RpcTarget.Single(who, RpcTargetUse.Temp));
                    break;
            }
        }

        /// <summary>Host -> everyone: the safe is open (and the crowbar that did it snapped, if it did).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void SafeOpenedRpc(byte epoch, bool crowbarBroke)
        {
            if (epoch != Epoch) return;
            FloorSafe.Active?.ApplyOpen(false, crowbarBroke);
        }

        /// <summary>Host -> the player who tried: the safe said no.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void SafeReplyRpc(byte reply, RpcParams rp = default) => FloorSafe.Active?.NetReply((SafeReply)reply);
    }
}
