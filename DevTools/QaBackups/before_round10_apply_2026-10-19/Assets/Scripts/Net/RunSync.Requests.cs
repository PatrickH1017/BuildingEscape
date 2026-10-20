using UnityEngine;
using Unity.Netcode;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Client -> host. Every player action that changes the shared world goes through here: the gameplay script
    /// calls a Request* method; on the host that runs the check directly, on a client it sends an RPC. The host
    /// validates (is the player close enough, does it fit, does it exist) and answers with an event (RunSync.Events).
    /// </summary>
    public partial class RunSync
    {
        const float ReachCheck = 4.5f;     // generous: the requester's view of the world is a moment old
        const float GuardHitReach = 3.6f;

        // A client may ask for at most a burst of 60 things, then 40 a second (a player acts a few times a second at most). Faster
        // than that is a modified client or a bug: the requests are dropped, so nobody can flood the host or, through it, the others.
        static readonly RateLimiter requestBudget = new RateLimiter(60f, 40f);
        float nextFloodLog;

        /// <summary>The player a request came from, or null if there is none or the sender is asking too fast.</summary>
        NetPlayer PlayerOf(ulong clientId)
        {
            if (IsServer && clientId != Me && !requestBudget.Allow(clientId, Time.unscaledTime))
            {
                if (Time.unscaledTime >= nextFloodLog)
                {
                    nextFloodLog = Time.unscaledTime + 5f;
                    Debug.LogWarning($"[NetSync] client {clientId} is sending requests too fast; ignoring the excess ({requestBudget.Refused} so far).");
                }
                return null;
            }
            foreach (var p in NetPlayer.All) if (p.OwnerClientId == clientId) return p;
            return null;
        }

        static bool Near(Transform a, Transform b, float d) => (a.position - b.position).sqrMagnitude <= d * d;

        static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        ulong Me => NetworkManager.LocalClientId;

        void Notice(ulong who, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            NoticeRpc(text, RpcTarget.Single(who, RpcTargetUse.Temp));
        }

        // ------------------------------------------------------------------ backpack

        public void RequestStore(Pickup p)
        {
            if (p == null || p.NetId < 0) return;
            if (IsServer) HostStore(Me, p.NetId); else StoreRequestRpc((ushort)p.NetId);
        }

        [Rpc(SendTo.Server)]
        void StoreRequestRpc(ushort id, RpcParams rp = default) => HostStore(rp.Receive.SenderClientId, id);

        void HostStore(ulong who, int id)
        {
            var np = PlayerOf(who);
            var p = NetIds.Get<Pickup>(id);
            if (np == null || p == null || !p.isActiveAndEnabled || p.IsHeld) return;
            var loot = p.Loot;
            if (loot != null && loot.Holders > 0) return;
            if (!Near(np.transform, p.transform, ReachCheck)) return;
            var bp = np.GetComponent<Backpack>();
            if (bp == null) return;
            if (p.TryGetComponent(out AmmoClip clip))
            {
                // a spare clip is used up on the spot (the owner, 2026-10-18): its rounds go into the pew this player carries - every machine adds them, as for the dealer's clip - and the clip is gone everywhere; no pew, no clip
                var clipGun = Vendor.GunOf(bp, clip.kind);
                if (clipGun == null) { Notice(who, Backpack.NoGunForClip(clip.kind)); return; }
                if (clipGun.Magazine.Room < clip.rounds) { Notice(who, Backpack.TooManyClips); return; }      // (three clips at most: the clip stays where it lies)
                int rounds = clip.rounds;
                AnnounceAmmo(np.Slot, rounds, clip.kind);
                Notice(who, Backpack.ClipTaken(rounds, clip.kind));
                BroadcastConsumed(p.NetId);
                Destroy(p.gameObject);
                return;
            }
            if (!bp.CanFit(p, out string why)) { Notice(who, why); return; }
            StoredRpc((ushort)id, (byte)np.Slot);
        }

        public void RequestDrop(Pickup p)
        {
            if (p == null || p.NetId < 0) return;
            if (IsServer) HostDrop(Me, p.NetId); else DropRequestRpc((ushort)p.NetId);
        }

        [Rpc(SendTo.Server)]
        void DropRequestRpc(ushort id, RpcParams rp = default) => HostDrop(rp.Receive.SenderClientId, id);

        void HostDrop(ulong who, int id)
        {
            var np = PlayerOf(who);
            var p = NetIds.Get<Pickup>(id);
            if (np == null || p == null) return;
            var bp = np.GetComponent<Backpack>();
            if (bp == null || !bp.Contains(p)) return;
            DroppedRpc((ushort)id, (byte)np.Slot, bp.DropPosition());
        }

        public void RequestDropAll()
        {
            if (IsServer) HostDropAll(Me); else DropAllRequestRpc();
        }

        [Rpc(SendTo.Server)]
        void DropAllRequestRpc(RpcParams rp = default) => HostDropAll(rp.Receive.SenderClientId);

        void HostDropAll(ulong who)
        {
            var np = PlayerOf(who);
            var bp = np ? np.GetComponent<Backpack>() : null;
            if (bp == null) return;
            foreach (var p in bp.Snapshot())
                if (p && p.NetId >= 0) DroppedRpc((ushort)p.NetId, (byte)np.Slot, bp.DropPosition());
        }

        /// <summary>Equip a weapon from the pack, or pass null to put it away.</summary>
        public void RequestEquip(Pickup p)
        {
            int id = p ? p.NetId : -1;
            if (IsServer) HostEquip(Me, id); else EquipRequestRpc((short)id);
        }

        [Rpc(SendTo.Server)]
        void EquipRequestRpc(short id, RpcParams rp = default) => HostEquip(rp.Receive.SenderClientId, id);

        void HostEquip(ulong who, int id)
        {
            var np = PlayerOf(who);
            var bp = np ? np.GetComponent<Backpack>() : null;
            if (bp == null) return;
            if (id >= 0)
            {
                var p = NetIds.Get<Pickup>(id);
                if (p == null || !bp.Contains(p) || !RangedWeapon.IsWeapon(p)) return;
            }
            EquippedRpc((short)id, (byte)np.Slot);
        }

        // ------------------------------------------------------------------ carrying (Q)

        public void RequestGrab(Pickup p)
        {
            if (p == null || p.NetId < 0) return;
            if (IsServer) HostGrab(Me, p.NetId); else GrabRequestRpc((ushort)p.NetId);
        }

        [Rpc(SendTo.Server)]
        void GrabRequestRpc(ushort id, RpcParams rp = default) => HostGrab(rp.Receive.SenderClientId, id);

        void HostGrab(ulong who, int id)
        {
            var np = PlayerOf(who);
            var p = NetIds.Get<Pickup>(id);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            if (pg == null || p == null || pg.Held || !p.isActiveAndEnabled) return;
            if (!Near(np.transform, p.transform, pg.reach + 2.5f)) return;
            var loot = p.Loot;
            if (p.IsHeld && (loot == null || loot.Holders == 0)) return; // already in someone's hands
            GrabbedRpc((ushort)id, (byte)np.Slot);
        }

        public void RequestRelease(bool thrown)
        {
            if (IsServer) HostRelease(Me, thrown); else ReleaseRequestRpc(thrown);
        }

        [Rpc(SendTo.Server)]
        void ReleaseRequestRpc(bool thrown, RpcParams rp = default) => HostRelease(rp.Receive.SenderClientId, thrown);

        void HostRelease(ulong who, bool thrown)
        {
            var np = PlayerOf(who);
            var pg = np ? np.GetComponent<PlayerGrab>() : null;
            if (pg == null || !pg.Held) return;
            ReleasedRpc((byte)np.Slot, thrown, thrown ? pg.ThrowImpulse() : Vector3.zero);
        }

        // ------------------------------------------------------------------ combat, noise, elevator, shop

        public void RequestGuardHit(GuardAI g)
        {
            if (g == null || g.NetId < 0) return;
            if (IsServer) HostGuardHit(Me, g.NetId); else GuardHitRequestRpc((ushort)g.NetId);
        }

        [Rpc(SendTo.Server)]
        void GuardHitRequestRpc(ushort id, RpcParams rp = default) => HostGuardHit(rp.Receive.SenderClientId, id);

        readonly System.Collections.Generic.Dictionary<ulong, float> lastHit = new();

        void HostGuardHit(ulong who, int id)
        {
            var np = PlayerOf(who);
            var g = NetIds.Get<GuardAI>(id);
            if (np == null || g == null) return;
            var bp = np.GetComponent<Backpack>();
            var weapon = bp && bp.Equipped ? bp.Equipped.GetComponent<MeleeWeapon>() : null;
            bool punch = weapon == null && !(bp && bp.Equipped);              // (empty hands: a punch)
            if (weapon == null && !punch) return;
            if (!Near(np.transform, g.transform, GuardHitReach)) return;
            float now = Time.unscaledTime;
            if (lastHit.TryGetValue(who, out float t) && now - t < 0.35f) return; // faster than any swing
            lastHit[who] = now;
            if (punch) g.PunchHost(PunchRules.StunSeconds, np.gameObject);
            else g.WeaponHitHost(weapon.Data.stun, np.gameObject);
        }

        /// <summary>This player swung a weapon (<paramref name="kind"/> 0-2) or threw a punch (<see cref="MeleeWeapon.PunchKind"/>): the teammates see it and hear it when they are near.</summary>
        public void RequestSwing(byte kind)
        {
            if (IsServer) HostSwing(Me, kind); else SwingRequestRpc(kind);
        }

        [Rpc(SendTo.Server)]
        void SwingRequestRpc(byte kind, RpcParams rp = default) => HostSwing(rp.Receive.SenderClientId, kind);

        static bool MeleeKindOk(byte kind) => kind <= 2 || kind == MeleeWeapon.PunchKind;

        void HostSwing(ulong who, byte kind)
        {
            var np = PlayerOf(who);
            if (np != null && MeleeKindOk(kind)) SwungRpc((byte)np.Slot, kind);
        }

        /// <summary>A blow landed (a weapon or a fist, on a guard or the training dummy) at <paramref name="at"/>: the teammates hear it from there, when they are near.</summary>
        public void RequestMeleeHit(byte kind, bool killed, Vector3 at)
        {
            if (IsServer) HostMeleeHit(Me, kind, killed, at); else MeleeHitRequestRpc(kind, killed, at);
        }

        [Rpc(SendTo.Server)]
        void MeleeHitRequestRpc(byte kind, bool killed, Vector3 at, RpcParams rp = default) => HostMeleeHit(rp.Receive.SenderClientId, kind, killed, at);

        readonly System.Collections.Generic.Dictionary<ulong, float> lastMeleeSound = new();

        void HostMeleeHit(ulong who, byte kind, bool killed, Vector3 at)
        {
            var np = PlayerOf(who);
            if (np == null || !MeleeKindOk(kind) || !(at.sqrMagnitude < 1e8f)) return;                 // (NaN and absurd places are refused)
            if ((np.transform.position - at).sqrMagnitude > 10f * 10f) return;                         // (the blow was where its player is)
            float now = Time.unscaledTime;
            if (lastMeleeSound.TryGetValue(who, out float t) && now - t < 0.12f) return;               // (faster than any swing)
            lastMeleeSound[who] = now;
            MeleeHitRpc((byte)np.Slot, kind, killed, at);
        }

        public void RequestNoise(Vector3 pos, float loudness)
        {
            if (IsServer) { NoiseBus.EmitLocal(pos, loudness); return; }
            NoiseRequestRpc(pos, loudness);
        }

        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        void NoiseRequestRpc(Vector3 pos, float loudness, RpcParams rp = default)
        {
            var np = PlayerOf(rp.Receive.SenderClientId);
            if (np == null || !(loudness > 0f) || float.IsInfinity(loudness)) return;
            // (NaN and infinity would make a guard walk to nowhere; the comparisons below are false for NaN, so it is refused here)
            if (!IsFinite(pos)) return;
            // a player cannot make noise far from where they are, and cannot be louder than the loudest real sound
            if ((np.transform.position - pos).sqrMagnitude > 8f * 8f) return;
            NoiseBus.EmitLocal(pos, Mathf.Min(loudness, 40f));
        }

        /// <summary>A panel button in an elevator: 0 = direction (or the key at the back door), 1 = go.</summary>
        public void RequestPanel(int action)
        {
            if (IsServer) HostPanel(Me, action); else PanelRequestRpc((byte)action);
        }

        [Rpc(SendTo.Server)]
        void PanelRequestRpc(byte action, RpcParams rp = default) => HostPanel(rp.Receive.SenderClientId, action);

        void HostPanel(ulong who, int action)
        {
            var np = PlayerOf(who);
            if (np == null) return;
            var wob = np.GetComponent<WobblyPlayer>();
            if (wob && wob.InCustody) return; // pinned or busted players cannot work the panel
            var pack = np.GetComponent<Backpack>();
            ElevatorExtraction.Current?.HostPress(np.transform, pack, action);  // each one checks the player is next to it
            BossElevator.Current?.HostPress(np.transform, action);
        }
        public void RequestBuy(int index)
        {
            if (IsServer) HostBuy(Me, index); else BuyRequestRpc((byte)index);
        }

        [Rpc(SendTo.Server)]
        void BuyRequestRpc(byte index, RpcParams rp = default) => HostBuy(rp.Receive.SenderClientId, index);

        void HostBuy(ulong who, int index)
        {
            var np = PlayerOf(who);
            var v = Vendor.Current;
            if (np == null || v == null) return;
            string msg = v.HostBuy(np, index);
            if (!string.IsNullOrEmpty(msg)) Notice(who, msg);
        }

        public void RequestSell()
        {
            if (IsServer) HostSell(Me); else SellRequestRpc();
        }

        [Rpc(SendTo.Server)]
        void SellRequestRpc(RpcParams rp = default) => HostSell(rp.Receive.SenderClientId);

        void HostSell(ulong who)
        {
            var np = PlayerOf(who);
            var v = Vendor.Current;
            if (np == null || v == null) return;
            string msg = v.HostSell(np);
            if (!string.IsNullOrEmpty(msg)) Notice(who, msg);
        }

        /// <summary>Vendor: pay to get a busted teammate out (their network slot).</summary>
        public void RequestBuyBack(int slot)
        {
            if (IsServer) HostBuyBack(Me, slot); else BuyBackRequestRpc((byte)slot);
        }

        [Rpc(SendTo.Server)]
        void BuyBackRequestRpc(byte slot, RpcParams rp = default) => HostBuyBack(rp.Receive.SenderClientId, slot);

        void HostBuyBack(ulong who, int slot)
        {
            var np = PlayerOf(who);
            var v = Vendor.Current;
            if (np == null || v == null) return;
            string msg = v.HostBuyBack(np, slot);
            if (!string.IsNullOrEmpty(msg)) Notice(who, msg);
        }

        // ------------------------------------------------------------------ doors

        const float DoorReachSlack = 1.3f;   // the asking player's position here is a moment old

        /// <summary>
        /// A player pressed E at a door: ask for it to be opened (<paramref name="open"/>) or shut. The player says what they want, not
        /// "flip it", so two players pressing at the same moment cannot undo each other. The door is addressed by its place in
        /// DoorManager.Doors (every machine builds the floor the same way).
        /// </summary>
        public void RequestDoor(int door, bool open)
        {
            if (door < 0 || door > ushort.MaxValue) return;
            if (IsServer) HostDoor(Me, door, open, Epoch); else DoorRequestRpc((ushort)door, open, Epoch);
        }

        [Rpc(SendTo.Server)]
        void DoorRequestRpc(ushort door, bool open, byte epoch, RpcParams rp = default) => HostDoor(rp.Receive.SenderClientId, door, open, epoch);

        void HostDoor(ulong who, int door, bool open, byte epoch)
        {
            if (epoch != Epoch) return;                              // asked about the floor before this one
            var np = PlayerOf(who);
            var doors = DoorManager.Active;
            if (np == null || doors == null) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (!doors.InUseReach(door, np.transform.position, DoorReachSlack)) return;
            if (open && doors.IsLocked(door) && !doors.OutpostInside(doors.Get(door), np.transform.position)) return;     // (the outpost's door opens when its lock is hacked, not before; from inside it just unlocks)
            doors.ApplyOutpostInsideRule(door, open, np.transform.position);
            DoorSetRpc((ushort)door, open, Epoch);
        }

        /// <summary>The player at the security computer locks / unlocks the doors of a room by its number.</summary>
        public void RequestComputerLock(int floor, string room, bool locked)
        {
            if (string.IsNullOrEmpty(room) || room.Length > 8) return;
            if (IsServer) HostComputerLock(Me, floor, room, locked, Epoch); else ComputerLockRequestRpc(floor, room, locked, Epoch);
        }

        [Rpc(SendTo.Server)]
        void ComputerLockRequestRpc(int floor, string room, bool locked, byte epoch, RpcParams rp = default) => HostComputerLock(rp.Receive.SenderClientId, floor, room, locked, epoch);

        void HostComputerLock(ulong who, int floor, string room, bool locked, byte epoch)
        {
            if (epoch != Epoch) return;
            var np = PlayerOf(who);
            var oc = OutpostController.Active;
            if (np == null || oc == null) return;
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;
            if (Vector3.Distance(np.transform.position, oc.KeypadPosition) > 30f) return;          // (the computer is in the outpost room)
            if (FloorDirectory.IsOutpostName(room))
            {
                if (!oc.IsInsideNear(np.transform.position)) return;                                // (the door of the room is locked from inside, at the computer)
                oc.HostLockByCode(locked);                                                         // (the new state reaches everybody with the run state)
                return;
            }
            var ids = FloorDirectory.DoorsOf(floor, room);
            if (ids == null) return;
            ComputerLockRpc(ids, locked, floor, Epoch);
        }

        /// <summary>Client: this machine has just built the floor; ask which doors the host's players have opened since it was built.</summary>
        public void RequestDoorStates()
        {
            if (!IsServer && IsSpawned) DoorStatesRequestRpc(Epoch);
        }

        [Rpc(SendTo.Server)]
        void DoorStatesRequestRpc(byte epoch, RpcParams rp = default)
        {
            if (epoch != Epoch) return;
            var doors = DoorManager.Active;
            if (doors == null) return;
            var open = doors.OpenByPlayers();
            if (open.Length > 0) DoorStatesRpc(open, Epoch, RpcTarget.Single(rp.Receive.SenderClientId, RpcTargetUse.Temp));
            var locked = doors.LockedByComputer();
            if (locked.Length > 0) ComputerLockStatesRpc(locked, HeistManager.Instance != null ? HeistManager.Instance.Floor : 0, Epoch, RpcTarget.Single(rp.Receive.SenderClientId, RpcTargetUse.Temp));
        }
    }
}
