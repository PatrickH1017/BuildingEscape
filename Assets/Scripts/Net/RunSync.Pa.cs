using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The hallway loudspeakers over the network (<see cref="PaState"/>): which speakers are switched on and who is speaking through them. The host publishes both with the run state (for the floor that is
    /// on show: its epoch); a player at the outpost's computer asks the host to change them, and the host only listens to somebody who is inside the outpost.
    /// </summary>
    public partial class RunSync
    {
        readonly NetworkVariable<ulong> vSpeakerMask = new(ulong.MaxValue);
        readonly NetworkVariable<sbyte> vPaLive = new(-1);
        readonly NetworkVariable<byte> vPaEpoch = new(0);

        void HostPublishPa()
        {
            vPaEpoch.Value = Epoch;
            vSpeakerMask.Value = PaState.Mask;
            vPaLive.Value = (sbyte)PaState.LiveBy;
        }

        void ClientApplyPa()
        {
            if (vPaEpoch.Value != Epoch) return;
            PaState.Mask = vSpeakerMask.Value;
            PaState.LiveBy = vPaLive.Value;
        }

        bool InOutpost(ulong who)
        {
            var np = PlayerOf(who);
            var c = OutpostController.Active;
            return np != null && c != null && c.IsInside(np.transform.position);
        }

        /// <summary>The player at the computer switched the speakers (a click on one, Select all, Deselect all).</summary>
        public void RequestSpeakers(ulong mask)
        {
            if (IsServer) HostSpeakers(Me, mask, Epoch); else SpeakersRequestRpc(mask, Epoch);
        }

        [Rpc(SendTo.Server)]
        void SpeakersRequestRpc(ulong mask, byte epoch, RpcParams rp = default) => HostSpeakers(rp.Receive.SenderClientId, mask, epoch);

        void HostSpeakers(ulong who, ulong mask, byte epoch)
        {
            if (epoch != Epoch || !InOutpost(who)) return;
            PaState.Mask = mask & PaRules.MaskOf(PaState.Speakers.Count);
        }

        /// <summary>The player at the computer holds / lets go of V.</summary>
        public void RequestPa(bool on)
        {
            if (IsServer) HostPa(Me, on, Epoch); else PaRequestRpc(on, Epoch);
        }

        [Rpc(SendTo.Server)]
        void PaRequestRpc(bool on, byte epoch, RpcParams rp = default) => HostPa(rp.Receive.SenderClientId, on, epoch);

        void HostPa(ulong who, bool on, byte epoch)
        {
            if (epoch != Epoch) return;
            var np = PlayerOf(who);
            if (np == null) return;
            if (!on) { if (PaState.LiveBy == np.Slot) PaState.LiveBy = -1; return; }
            if (!InOutpost(who)) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            PaState.LiveBy = np.Slot;
        }
    }
}
