using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// Makes a player capsule network-aware. The owner runs the normal single-player scripts and
    /// their transform is synced to everyone; on every other machine the same capsule is a puppet:
    /// kinematic, input scripts off, animation fed from the synced movement.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new();

        readonly NetworkVariable<int> slot = new(-1);
        // The owner's chosen PlayerAppearance string; the owner writes it, everyone applies it.
        readonly NetworkVariable<FixedString64Bytes> look = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // The name over his head (the owner, 2026-10-23: "Players need nametags above their heads"), written by the owner of the body (his Steam name; empty without Steam), read cleaned (NameRules) by everybody.
        readonly NetworkVariable<FixedString64Bytes> displayName = new(default,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Hiding at a desk, written by the owner: the host's guards must see a hidden teammate as hidden.
        readonly NetworkVariable<bool> hidden = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Aiming (the gun is up at the sights), written by the owner: teammates see the gun at the hip or at the sights (the owner, 2026-10-27).
        readonly NetworkVariable<bool> aiming = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Crouched, written by the owner: teammates see the crouch (the puppet's own script is off).
        readonly NetworkVariable<bool> crouched = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Custody (pinned / busted), written by the host and applied on every machine so teammates see who is down.
        readonly NetworkVariable<byte> custody = new(0);
        readonly NetworkVariable<byte> pinSecondsLeft = new(0);
        readonly NetworkVariable<byte> bustedLevel = new(0);

        int pendingSlot = -1;
        bool held;
        Rigidbody rb;
        WobblyPlayer wobbly;
        NetworkTransform netTransform;
        Vector3 lastPos, smoothVelocity;

        /// <summary>Join order (0 .. MaxPlayers - 1); also picks the character look so it matches on every machine.</summary>
        public int Slot => slot.Value;

        /// <summary>The name this player's own machine wrote for him, cleaned (<see cref="NameRules.Clean"/>); "" when he has none (<see cref="NameRules.Tag"/> then says "Player n").</summary>
        public string DisplayName => NameRules.Clean(displayName.Value.ToString());

        /// <summary>The owner of this body writes his name (cleaned first).</summary>
        public void SetDisplayName(string raw) { if (IsOwner) displayName.Value = NameRules.Clean(raw); }

        /// <summary>Host only, before spawning.</summary>
        public void AssignSlot(int s) { pendingSlot = s; }

        public override void OnNetworkSpawn()
        {
            rb = GetComponent<Rigidbody>();
            wobbly = GetComponent<WobblyPlayer>();
            netTransform = GetComponent<NetworkTransform>();
            if (IsServer && pendingSlot >= 0) slot.Value = pendingSlot;
            All.Add(this);
            lastPos = transform.position;

            var visual = GetComponent<PlayerVisual>();
            if (visual && slot.Value >= 0) visual.lookIndex = slot.Value;
            look.OnValueChanged += (_, v) => ApplyLook(v.ToString());
            if (IsOwner) look.Value = (PlayerAppearance.TryLoadSaved(out var chosen) ? chosen : PlayerAppearance.ForSlot(Mathf.Max(0, slot.Value))).Serialize();       // (a player who never chose is a toon person by slot: every friend looks different)
            else ApplyLook(look.Value.ToString());
            if (IsOwner) { var steam = SteamService.Instance; SetDisplayName(steam != null && steam.Ready ? steam.LocalName : ""); }

            bool mine = IsOwner;
            rb.isKinematic = !mine;
            // Until the floor is built and this player has been put in the elevator, the body is held still: it would only fall into
            // the void (and the capsules, all spawned at one spot, would fling each other about).
            var tower = TowerManager.Instance;
            held = mine && !(tower != null && tower.HasFloor && !tower.Loading);
            if (held) wobbly.HoldStill(true);
            // Only the movement and voice scripts are owner-only. Backpack, PlayerGrab and MeleeController keep
            // running on puppets because they hold the shared state (what is in a pack, what is carried); they
            // read input only for the player the camera follows (Viewed / IsLocalPlayer).
            Enable<WobblyPlayer>(mine);
            Enable<VoiceNoise>(mine);

            if (mine)
            {
                var cam = FindFirstObjectByType<CameraFollow>();
                if (cam) cam.target = transform;
            }
        }

        public override void OnNetworkDespawn()
        {
            DropPackWhereTheyStood();
            All.Remove(this);
            if (wobbly) wobbly.VelocityOverride = null;
        }

        /// <summary>
        /// A player who drops out mid-run (a disconnect) puts their pack down where they stood, so the team keeps the loot and the
        /// weapons instead of losing them with the player. Every machine does the same thing; the host's physics settles it.
        /// </summary>
        void DropPackWhereTheyStood()
        {
            var nm = NetworkManager;
            if (nm == null || nm.ShutdownInProgress) return;           // the whole session is ending: nothing to save
            var tower = TowerManager.Instance;
            if (tower == null || !tower.HasFloor || tower.Loading) return;
            if (!TryGetComponent(out Backpack pack)) return;
            int i = 0;
            foreach (var item in pack.Snapshot())
            {
                if (!item) continue;
                var spot = transform.position + new Vector3((i % 3 - 1) * 0.35f, 0.6f + (i / 9) * 0.3f, (i / 3 % 3 - 1) * 0.35f);
                pack.ApplyDropped(item, spot);
                i++;
            }
        }

        void ApplyLook(string s)
        {
            var visual = GetComponent<PlayerVisual>();
            if (!visual || !PlayerAppearance.TryParse(s, out var a)) return; // bad / empty: keep the default look
            visual.Apply(a.ToLook());
        }

        void Enable<T>(bool on) where T : Behaviour
        {
            var c = GetComponent<T>();
            if (c) c.enabled = on;
        }

        /// <summary>Host: publish a player's custody state (the host's own copy was already applied by HeistManager).</summary>
        public void HostSetCustody(CustodyState state, int pinSeconds, int bustedOnLevel)
        {
            if (!IsServer) return;
            custody.Value = (byte)state;
            pinSecondsLeft.Value = (byte)Mathf.Clamp(pinSeconds, 0, 255);
            bustedLevel.Value = (byte)Mathf.Clamp(bustedOnLevel, 0, 255);
        }

        void Update()
        {
            if (!IsSpawned || wobbly == null) return;
            if (!IsServer) wobbly.ApplyCustody((CustodyState)custody.Value, pinSecondsLeft.Value, bustedLevel.Value);
            if (IsOwner) { hidden.Value = wobbly.IsHidden; crouched.Value = wobbly.IsCrouching; aiming.Value = wobbly.IsAiming; return; }
            wobbly.IsHidden = hidden.Value;
            wobbly.SetPuppetCrouch(crouched.Value);
            wobbly.IsAiming = aiming.Value;
            // Puppets are kinematic, so derive their speed from how far they moved for the animator.
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var v = (transform.position - lastPos) / dt;
            smoothVelocity = Vector3.Lerp(smoothVelocity, v, 10f * Time.deltaTime);
            lastPos = transform.position;
            wobbly.VelocityOverride = smoothVelocity;
        }

        /// <summary>True while the owner's body is held still, waiting to be put in the elevator.</summary>
        public bool IsHeld => held;

        /// <summary>Hold this body still (only the machine that drives it): the floor under it is about to be rebuilt.</summary>
        public void Hold()
        {
            if (!IsOwner || !wobbly) return;
            held = true;
            wobbly.HoldStill(true);
        }

        /// <summary>Let a held body go (it was placed, or the floor could not be built).</summary>
        public void Release()
        {
            if (!held) return;
            held = false;
            if (wobbly) wobbly.HoldStill(false);
        }

        /// <summary>Moves the player without it sliding across the map on other machines (teleport).</summary>
        public void PlaceAt(Vector3 pos, Quaternion rot)
        {
            if (IsSpawned && IsOwner && netTransform)
            {
                netTransform.Teleport(pos, rot, transform.localScale);   // tells the other machines it is a jump, not a run
                wobbly.TeleportTo(pos, rot);                              // and moves the rigidbody, which interpolation would otherwise put back
                Release();
            }
            else if (!IsSpawned) { if (wobbly) wobbly.TeleportTo(pos, rot); else transform.SetPositionAndRotation(pos, rot); }
            // Non-owners: the owner's own teleport arrives over the network.
        }

        public static NetPlayer BySlot(int slot)
        {
            foreach (var p in All) if (p.Slot == slot) return p;
            return null;
        }

        /// <summary>The player this machine controls, or null offline.</summary>
        public static NetPlayer Local
        {
            get
            {
                foreach (var p in All) if (p.IsOwner) return p;
                return null;
            }
        }

        /// <summary>True for the player this machine controls (offline: every player, there is only one).</summary>
        public new static bool IsLocalPlayer(WobblyPlayer p)   // (hides NetworkBehaviour.IsLocalPlayer on purpose: that one is about the NGO player object)
        {
            return !p || !p.TryGetComponent(out NetPlayer np) || !np.IsSpawned || np.IsOwner;
        }

        /// <summary>Registry players ordered by network slot, so every machine arranges the team identically.</summary>
        public static List<WobblyPlayer> Ordered()
        {
            var list = new List<WobblyPlayer>(WobblyPlayerRegistry.All);
            list.Sort((a, b) => SlotOf(a).CompareTo(SlotOf(b)));
            return list;
        }

        static int SlotOf(WobblyPlayer p)
        {
            return p && p.TryGetComponent(out NetPlayer np) && np.IsSpawned ? np.Slot : int.MaxValue;
        }
    }
}
