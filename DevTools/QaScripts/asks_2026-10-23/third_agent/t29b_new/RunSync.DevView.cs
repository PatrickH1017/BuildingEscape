using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// What the host tells a developer who watches, and nobody else (the owner's notes of 2026-10-23 for T29: a live overlay of what the guards are doing): about four times a second, for every guard, how much health he has left and whom he is after
    /// (<see cref="DevOverlayRules"/>). What a guard is DOING (his state, the radio call, whether he is armed) already reaches every machine with the snapshots (RunSync.Snapshots.cs); health and the target do not, and only a spectator's overlay wants them.
    /// Nothing is sent while nobody watches, and a player's machine never registers the handler. Compiled into the Editor and the friends' builds only; a release build has empty stand-ins for the three calls RunSync makes.
    /// </summary>
    public partial class RunSync
    {
#if ETR_DEV_TOOLS || UNITY_EDITOR
        const string DevViewMessage = "be.dbg";
        float nextDevView;
        byte devViewEpoch; bool devViewHasEpoch, devViewHooked;
        readonly List<ulong> devWatchers = new();
        readonly List<GuardAI> devGuards = new();

        /// <summary>A spectator's machine listens for the host's debug record.</summary>
        void SpawnDevView()
        {
            if (IsServer || NetSession.Instance == null || !NetSession.Instance.IsSpectator) return;
            NetworkManager.CustomMessagingManager.RegisterNamedMessageHandler(DevViewMessage, OnDevView);
            devViewHooked = true; devViewHasEpoch = false;
        }

        void DespawnDevView()
        {
            if (!devViewHooked) return;
            devViewHooked = false;
            if (NetworkManager != null && NetworkManager.CustomMessagingManager != null) NetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(DevViewMessage);
            DevSpectatorView.ClearRecords();
        }

        /// <summary>Host: the debug record for the developers who watch (about four times a second, only while somebody does).</summary>
        void HostSendDevView()
        {
            var net = NetSession.Instance;
            if (net == null || net.SpectatorCount == 0) return;
            float now = Time.unscaledTime;
            if (now < nextDevView) return;
            nextDevView = now + DevOverlayRules.RecordInterval;
            var tm = TowerManager.Instance;
            if (tm != null && tm.Loading) return;
            devWatchers.Clear();
            foreach (var id in NetworkManager.ConnectedClientsIds) if (net.IsSpectatorClient(id)) devWatchers.Add(id);
            if (devWatchers.Count == 0) return;
            devGuards.Clear();
            foreach (var g in GuardAI.All) if (g && g.NetId >= 0 && g.NetId <= ushort.MaxValue && !g.IsPuppet && devGuards.Count < 200) devGuards.Add(g);
            using var w = new FastBufferWriter(2 + devGuards.Count * 4, Allocator.Temp);
            w.WriteValueSafe(Epoch);
            w.WriteValueSafe((byte)devGuards.Count);
            foreach (var g in devGuards)
            {
                sbyte slot = -1;
                var target = g.Target;
                if (target && target.TryGetComponent(out NetPlayer np) && np.IsSpawned) slot = (sbyte)np.Slot;
                w.WriteValueSafe((ushort)g.NetId);
                w.WriteValueSafe(DevOverlayRules.HealthByte(g.Health, g.MaxHealth, g.Dead));
                w.WriteValueSafe(slot);
            }
            foreach (var id in devWatchers) NetworkManager.CustomMessagingManager.SendNamedMessage(DevViewMessage, id, w, NetworkDelivery.UnreliableSequenced);
        }

        /// <summary>Spectator's machine: the host's record arrived (a record of another floor, from before this machine finished building the new one, is dropped).</summary>
        void OnDevView(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out byte epoch);
            r.ReadValueSafe(out byte n);
            if (!devViewHasEpoch || devViewEpoch != epoch) { DevSpectatorView.ClearRecords(); devViewEpoch = epoch; devViewHasEpoch = true; }
            if (epoch != Epoch) return;
            float now = Time.unscaledTime;
            for (int i = 0; i < n; i++)
            {
                r.ReadValueSafe(out ushort id); r.ReadValueSafe(out byte health); r.ReadValueSafe(out sbyte slot);
                DevSpectatorView.Record(id, health, slot, now);
            }
        }
#else
        void SpawnDevView() { }
        void DespawnDevView() { }
        void HostSendDevView() { }
#endif
    }
}
