using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The dock radio over the network (<see cref="RadioState"/>): the host publishes the station, when the radio's clock started (the session's clock), the playlists' seed, whether it is switched on and its volume with the run state, so every machine plays the same song at the same
    /// place; a client's click on a button (a station, the power, the volume) is a request, and the host only listens to a player who stands at the radio.
    /// </summary>
    public partial class RunSync
    {
        readonly NetworkVariable<sbyte> vRadioStation = new(-1);
        readonly NetworkVariable<double> vRadioEpoch = new();
        readonly NetworkVariable<int> vRadioSeed = new();
        readonly NetworkVariable<bool> vRadioOn = new(true);
        readonly NetworkVariable<byte> vRadioVolume = new(RadioRules.DefaultVolume);

        /// <summary>How close (m) a player who asks for another station must stand to the radio (its reach and a margin for the moment the request takes).</summary>
        const float RadioReachForRequests = 5f;

        void HostPublishRadio()
        {
            _ = RadioState.Running;                                           // (a radio started before the session began starts again on the session's clock)
            vRadioStation.Value = (sbyte)RadioState.Station;
            vRadioEpoch.Value = RadioState.Epoch;
            vRadioSeed.Value = RadioState.Seed;
            vRadioOn.Value = RadioState.On;
            vRadioVolume.Value = (byte)RadioState.Volume;
        }

        void ClientApplyRadio() => RadioState.NetApply(vRadioStation.Value, vRadioEpoch.Value, vRadioSeed.Value, vRadioOn.Value, vRadioVolume.Value);

        /// <summary>The player clicked a button on the radio's screen (<see cref="RadioRules.ActionOf"/>: a station, the power, the volume).</summary>
        public void RequestRadio(int action)
        {
            if (IsServer) HostRadio(Me, action); else RadioRequestRpc((byte)action);
        }

        [Rpc(SendTo.Server)]
        void RadioRequestRpc(byte action, RpcParams rp = default) => HostRadio(rp.Receive.SenderClientId, action);

        void HostRadio(ulong who, int action)
        {
            if (!RadioRules.ValidAction(action)) return;
            var np = PlayerOf(who);
            var radio = DockRadio.Current;
            if (np == null || radio == null || radio.DistanceFrom(np.transform.position) > RadioReachForRequests) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            RadioState.Apply(action);
        }
    }
}
