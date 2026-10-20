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

        // ------------------------------------------------------------------ the gun safe's hack

        /// <summary>A player finished the hack of the gun safe (won or lost; backing out is not reported).</summary>
        public void RequestGunSafe(bool success)
        {
            if (IsServer) HostGunSafe(Me, success, Epoch); else GunSafeRequestRpc(success, Epoch);
        }

        [Rpc(SendTo.Server)]
        void GunSafeRequestRpc(bool success, byte epoch, RpcParams rp = default) => HostGunSafe(rp.Receive.SenderClientId, success, epoch);

        void HostGunSafe(ulong who, bool success, byte epoch)
        {
            if (epoch != Epoch) return;                                  // about the floor before this one
            var np = PlayerOf(who);
            var c = OutpostController.Active;
            if (np == null || c == null || c.GunSafe == null) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!c.GunSafe.InReach(np.transform.position)) return;     // not even near the safe
            c.GunSafe.HostResult(success);                               // the open safe (or the alarm) reaches everybody with the run state
        }

        // ------------------------------------------------------------------ the alarm panel

        /// <summary>A player pressed the button on the alarm panel inside the outpost: the siren stops and the backup is called off.</summary>
        public void RequestStandDown()
        {
            if (IsServer) HostStandDown(Me, Epoch); else StandDownRequestRpc(Epoch);
        }

        [Rpc(SendTo.Server)]
        void StandDownRequestRpc(byte epoch, RpcParams rp = default) => HostStandDown(rp.Receive.SenderClientId, epoch);

        void HostStandDown(ulong who, byte epoch)
        {
            if (epoch != Epoch) return;                                  // about the floor before this one
            var np = PlayerOf(who);
            var c = OutpostController.Active;
            if (np == null || c == null) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!c.InPanelReach(np.transform.position)) return;        // not even near the panel
            if (c.HostStandDown()) StandDownRpc(Epoch);                  // (the new state reaches everybody with the run state)
        }

        /// <summary>Host -> everyone: the alarm panel was used (the relay clack and the lamps).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void StandDownRpc(byte epoch)
        {
            if (epoch != Epoch) return;
            OutpostController.Active?.PlayStandDown();
        }

        // ------------------------------------------------------------------ the safe

        /// <summary>A player tries safe number <paramref name="index"/> of the floor (0 first): with a code (<paramref name="pry"/> false) or with a crowbar from his backpack.</summary>
        public void RequestSafe(int index, bool pry, int code)
        {
            code = Mathf.Clamp(code, 0, 9999);
            index = Mathf.Clamp(index, 0, SafeRules.MaxSafes - 1);
            if (IsServer) HostSafe(Me, index, pry, code, Epoch); else SafeRequestRpc((byte)index, pry, (ushort)code, Epoch);
        }

        [Rpc(SendTo.Server)]
        void SafeRequestRpc(byte index, bool pry, ushort code, byte epoch, RpcParams rp = default) => HostSafe(rp.Receive.SenderClientId, index, pry, code, epoch);

        void HostSafe(ulong who, int index, bool pry, int code, byte epoch)
        {
            if (epoch != Epoch) return;
            var np = PlayerOf(who);
            var safe = FloorSafe.ByIndex(index);
            if (np == null || safe == null || safe.IsOpen) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!safe.InReach(np.transform.position, SafeReachSlack)) return;

            var reply = safe.HostAttempt(pry, code, np.GetComponent<Backpack>());
            switch (reply)
            {
                case SafeReply.Opened:
                    SafeOpenedRpc(Epoch, (byte)index, false);
                    break;
                case SafeReply.OpenedCrowbarBroke:
                    SafeOpenedRpc(Epoch, (byte)index, true);
                    Notice(who, "The crowbar snapped!");
                    break;
                case SafeReply.Ignored:
                    break;
                default:
                    SafeReplyRpc((byte)reply, (byte)index, RpcTarget.Single(who, RpcTargetUse.Temp));
                    break;
            }
        }

        /// <summary>Host -> everyone: safe number <paramref name="index"/> is open (and the crowbar that did it snapped, if it did).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void SafeOpenedRpc(byte epoch, byte index, bool crowbarBroke)
        {
            if (epoch != Epoch) return;
            FloorSafe.ByIndex(index)?.ApplyOpen(false, crowbarBroke);
        }

        /// <summary>Host -> the player who tried: the safe said no.</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void SafeReplyRpc(byte reply, byte index, RpcParams rp = default) => FloorSafe.ByIndex(index)?.NetReply((SafeReply)reply);
    }
}
