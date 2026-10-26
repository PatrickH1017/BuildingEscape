using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The run-start cinematic over the network (<see cref="IntroCinematic"/>, rules in <see cref="IntroRules"/>). The host decides when it starts
    /// (a server time a moment ahead, so every machine shows the same picture at the same time, however long its docks took to build) and counts
    /// the votes: a player who has held Space for a second asks the host to count them in (<see cref="RequestIntroHold"/>); as soon as at least
    /// half of the party is counted in, the host sets <c>introSkipped</c> and every machine ends the Intro. Everything is a NetworkVariable the
    /// host writes, so a machine that is slow to arrive finds the Intro already over and plays nothing.
    /// </summary>
    public partial class RunSync
    {
        // host-written; -1 = the Intro has not been started
        readonly NetworkVariable<double> introStart = new(-1.0);
        readonly NetworkVariable<bool> introSkipped = new();
        readonly NetworkVariable<ushort> introTally = new();          // holders * 256 + party
        readonly HashSet<ulong> introHolders = new();

        /// <summary>The server time the Intro begins at (-1 = not started yet).</summary>
        public double IntroStart => introStart.Value;
        public bool IntroSkipped => introSkipped.Value;
        /// <summary>Skipped, or played to the end (a machine that arrives now has nothing to watch).</summary>
        public bool IntroOver => introSkipped.Value || (introStart.Value >= 0.0 && NetworkManager != null && NetworkManager.ServerTime.Time - introStart.Value >= IntroRules.Total);
        /// <summary>How many players are holding Space (counted in by the host) and how many are in the party, for the readout.</summary>
        public int IntroHolders => introTally.Value >> 8;
        public int IntroParty => introTally.Value & 255;

        /// <summary>Host: the Intro starts (once) <see cref="IntroRules.LeadIn"/> seconds from now.</summary>
        public void HostStartIntro()
        {
            if (!IsServer || !IsSpawned || introStart.Value >= 0.0) return;
            introStart.Value = NetworkManager.ServerTime.Time + IntroRules.LeadIn;
            HostIntroTally();
        }

        /// <summary>Host, for tests: there is no Intro in this run.</summary>
        public void HostSkipIntroNow()
        {
            if (!IsServer || !IsSpawned) return;
            if (introStart.Value < 0.0) introStart.Value = NetworkManager.ServerTime.Time;
            introSkipped.Value = true;
        }

        /// <summary>This player's hold of Space counts (or no longer counts) towards the skip.</summary>
        public void RequestIntroHold(bool holding)
        {
            if (IsServer) HostIntroHold(Me, holding); else IntroHoldRequestRpc(holding);
        }

        [Rpc(SendTo.Server)]
        void IntroHoldRequestRpc(bool holding, RpcParams rp = default) => HostIntroHold(rp.Receive.SenderClientId, holding);

        void HostIntroHold(ulong who, bool holding)
        {
            if (introSkipped.Value || introStart.Value < 0.0) return;           // nothing to vote on
            if (holding) introHolders.Add(who); else introHolders.Remove(who);
            HostIntroTally();
        }

        /// <summary>Host: count the votes (players who left no longer count and no longer make up the party) and skip when half of the party is in.</summary>
        void HostIntroTally()
        {
            introHolders.RemoveWhere(id => !NetworkManager.ConnectedClients.ContainsKey(id));
            int party = NetworkManager.ConnectedClientsIds.Count, holders = introHolders.Count;
            ushort tally = (ushort)((Mathf.Min(holders, 255) << 8) | Mathf.Min(party, 255));
            if (introTally.Value != tally) introTally.Value = tally;
            if (IntroRules.SkipPassed(holders, party)) introSkipped.Value = true;
        }

        /// <summary>Host, every frame: somebody who leaves while the Intro runs changes the party, and so what half of it is.</summary>
        void IntroServerTick()
        {
            if (introStart.Value < 0.0 || IntroOver) return;          // (not started, or over: skipped or played out)
            HostIntroTally();
        }
    }
}
