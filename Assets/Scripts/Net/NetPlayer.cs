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

        // Hiding at a desk, written by the owner: the host's guards must see a hidden teammate as hidden.
        readonly NetworkVariable<bool> hidden = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        int pendingSlot = -1;
        Rigidbody rb;
        WobblyPlayer wobbly;
        NetworkTransform netTransform;
        Vector3 lastPos, smoothVelocity;

        /// <summary>Join order (0..3); also picks the character look so it matches on every machine.</summary>
        public int Slot => slot.Value;

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
            if (IsOwner) look.Value = PlayerAppearance.LoadOrDefault().Serialize();
            else ApplyLook(look.Value.ToString());

            bool mine = IsOwner;
            rb.isKinematic = !mine;
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
            All.Remove(this);
            if (wobbly) wobbly.VelocityOverride = null;
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

        void Update()
        {
            if (!IsSpawned || wobbly == null) return;
            if (IsOwner) { hidden.Value = wobbly.IsHidden; return; }
            wobbly.IsHidden = hidden.Value;
            // Puppets are kinematic, so derive their speed from how far they moved for the animator.
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var v = (transform.position - lastPos) / dt;
            smoothVelocity = Vector3.Lerp(smoothVelocity, v, 10f * Time.deltaTime);
            lastPos = transform.position;
            wobbly.VelocityOverride = smoothVelocity;
        }

        /// <summary>Moves the player without it sliding across the map on other machines (teleport).</summary>
        public void PlaceAt(Vector3 pos, Quaternion rot)
        {
            if (IsSpawned && IsOwner && netTransform) netTransform.Teleport(pos, rot, transform.localScale);
            else if (!IsSpawned) { transform.position = pos; transform.rotation = rot; }
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
        public static bool IsLocalPlayer(WobblyPlayer p)
        {
            var np = p ? p.GetComponent<NetPlayer>() : null;
            return np == null || !np.IsSpawned || np.IsOwner;
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
            var np = p ? p.GetComponent<NetPlayer>() : null;
            return np && np.IsSpawned ? np.Slot : int.MaxValue;
        }
    }
}