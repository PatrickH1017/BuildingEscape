using UnityEngine;
using Unity.Netcode;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Host -> everyone: things that happened. The host decides (RunSync.Requests) and then announces; every
    /// machine, the host included, applies the event through the same method, so there is one code path.
    /// </summary>
    public partial class RunSync
    {
        // ------------------------------------------------------------------ backpack

        [Rpc(SendTo.ClientsAndHost)]
        void StoredRpc(ushort id, byte slot)
        {
            var np = NetPlayer.BySlot(slot);
            var p = NetIds.Get<Pickup>(id);
            if (np && p) np.GetComponent<Backpack>().ApplyStored(p);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void DroppedRpc(ushort id, byte slot, Vector3 pos)
        {
            var np = NetPlayer.BySlot(slot);
            var p = NetIds.Get<Pickup>(id);
            if (np && p) np.GetComponent<Backpack>().ApplyDropped(p, pos);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void EquippedRpc(short id, byte slot)
        {
            var np = NetPlayer.BySlot(slot);
            if (!np) return;
            np.GetComponent<Backpack>().ApplyEquipped(id >= 0 ? NetIds.Get<Pickup>(id) : null);
        }

        // ------------------------------------------------------------------ carrying

        [Rpc(SendTo.ClientsAndHost)]
        void GrabbedRpc(ushort id, byte slot)
        {
            var np = NetPlayer.BySlot(slot);
            var p = NetIds.Get<Pickup>(id);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            if (pg && p) pg.ApplyGrab(p);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ReleasedRpc(byte slot, bool thrown, Vector3 impulse)
        {
            var np = NetPlayer.BySlot(slot);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            if (pg) pg.ApplyRelease(thrown, impulse);
        }

        // ------------------------------------------------------------------ world changes

        /// <summary>Host: something was banked / sold / used up and has been destroyed here; clients follow.</summary>
        public void BroadcastConsumed(int id)
        {
            if (IsServer && id >= 0) ConsumedRpc((ushort)id);
        }

        [Rpc(SendTo.NotServer)]
        void ConsumedRpc(ushort id)
        {
            var p = NetIds.Get<Pickup>(id);
            if (p) Destroy(p.gameObject);
        }

        public void BroadcastBroken(int id)
        {
            if (IsServer && id >= 0) BrokenRpc((ushort)id);
        }

        [Rpc(SendTo.NotServer)]
        void BrokenRpc(ushort id)
        {
            var p = NetIds.Get<Pickup>(id);
            if (p && p.Loot) p.Loot.NetShatter();
        }

        public void BroadcastSpawnItem(int id, int product, Vector3 pos, float yaw)
        {
            if (IsServer) SpawnItemRpc((ushort)id, (byte)product, pos, yaw);
        }

        [Rpc(SendTo.NotServer)]
        void SpawnItemRpc(ushort id, byte product, Vector3 pos, float yaw)
        {
            if (Vendor.Current) Vendor.Current.SpawnFromNet(id, product, pos, yaw);
        }

        public void BroadcastSpawnGuard(int id, Vector3 pos, int look, Vector3 dispatch)
        {
            if (IsServer) SpawnGuardRpc((ushort)id, pos, look, dispatch);
        }

        [Rpc(SendTo.NotServer)]
        void SpawnGuardRpc(ushort id, Vector3 pos, int look, Vector3 dispatch)
        {
            var tm = TowerManager.Instance;
            if (tm && tm.generator) tm.generator.SpawnReinforcementFromNet(id, pos, look);
        }

        public void BroadcastSpawnKey(int id, Vector3 pos)
        {
            if (IsServer) SpawnKeyRpc((ushort)id, pos);
        }

        [Rpc(SendTo.NotServer)]
        void SpawnKeyRpc(ushort id, Vector3 pos)
        {
            var tm = TowerManager.Instance;
            if (tm && tm.generator) tm.generator.SpawnKeyFromNet(id, pos);
        }

        public void BroadcastToast(string text)
        {
            if (IsServer && !string.IsNullOrEmpty(text)) ToastRpc(text);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void ToastRpc(string text) => ElevatorExtraction.ShowToast(text);

        public void BroadcastBackupMessage(string text)
        {
            if (IsServer && !string.IsNullOrEmpty(text)) BackupMessageRpc(text);
        }

        [Rpc(SendTo.NotServer)]
        void BackupMessageRpc(string text) => HeistManager.Instance?.NetBackupMessage(text);

        /// <summary>Host: a shop item went straight into a player's pack.</summary>
        public void AnnounceStored(int id, int slot)
        {
            if (IsServer && id >= 0) StoredRpc((ushort)id, (byte)slot);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void NoticeRpc(string text, RpcParams rp = default) => Backpack.NoticeToLocal(text);

        // ------------------------------------------------------------------ players

        /// <summary>Host: knock a player over. Only the player's own machine moves them, so it is routed there.</summary>
        public void BroadcastStumble(int slot, Vector3 impulse, float seconds)
        {
            if (IsServer) StumbleRpc((byte)slot, impulse, seconds);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void StumbleRpc(byte slot, Vector3 impulse, float seconds)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && np.IsOwner) np.GetComponent<WobblyPlayer>().StumbleLocal(impulse, seconds);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void SwungRpc(byte slot)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && !np.IsOwner) np.GetComponent<MeleeController>()?.PlayRemoteSwing();
        }

        // ------------------------------------------------------------------ floors

        /// <summary>Host: everyone builds the next level and rides the cargo along.</summary>
        public void BroadcastLoadLevel(int level, ushort[] cargo, int resume)
        {
            if (!IsServer) return;
            LoadLevelRpc(level, cargo, resume, (byte)(Epoch + 1));
        }

        [Rpc(SendTo.ClientsAndHost)]
        void LoadLevelRpc(int level, ushort[] cargo, int resume, byte epoch)
        {
            Epoch = epoch;
            OnFloorChanged();
            var tm = TowerManager.Instance;
            if (tm) tm.NetLoadLevel(level, cargo, resume);
        }

        /// <summary>Host: after the floor is built, tell clients what it contains so a desync is caught immediately.</summary>
        public void PublishChecksum()
        {
            if (IsServer) ChecksumRpc(NetIds.Checksum(), NetIds.Count, Epoch);
        }

        [Rpc(SendTo.NotServer)]
        void ChecksumRpc(int sum, int count, byte epoch)
        {
            // the client may still be building the floor; wait for it, then compare
            StartCoroutine(CompareChecksum(sum, count, epoch));
        }

        System.Collections.IEnumerator CompareChecksum(int sum, int count, byte epoch)
        {
            var tm = TowerManager.Instance;
            for (float t = 0; t < 10f && tm != null && tm.Loading; t += Time.unscaledDeltaTime) yield return null;
            if (epoch != Epoch) yield break;
            if (NetIds.Checksum() != sum || NetIds.Count != count)
                Debug.LogError($"[NetSync] DESYNC: this floor has {NetIds.Count} objects (checksum {NetIds.Checksum()}), the host has {count} ({sum}).");
            else
                Debug.Log($"[NetSync] floor matches the host: {count} objects.");
            SyncChecked = true;
        }

        /// <summary>True once this machine verified its floor against the host (used by tests).</summary>
        public static bool SyncChecked;
    }
}