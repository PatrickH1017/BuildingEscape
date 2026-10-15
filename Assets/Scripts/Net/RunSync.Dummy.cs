using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The loading dock's training dummy in a networked run. The host owns its health (it applies the damage of the
    /// weapon a player really holds and publishes the result), clients only ask to hit it and mirror what they read.
    /// </summary>
    public partial class RunSync
    {
        const float DummyReach = 4.5f;

        // host-written; -1 = there is no dummy on this level
        readonly NetworkVariable<float> dummyHealth = new(-1f);
        readonly Dictionary<ulong, float> lastDummyHit = new();

        /// <summary>The dummy's health as the host last published it (-1 when there is none).</summary>
        public float DummyHealth => dummyHealth.Value;

        /// <summary>Host: publish the dummy's health (only writes when it changed).</summary>
        public void HostSetDummyHealth(float health)
        {
            if (!IsServer || !IsSpawned) return;
            if (!Mathf.Approximately(dummyHealth.Value, health)) dummyHealth.Value = health;
        }

        /// <summary>A swing connected with the dummy: the host (or this machine, if it is the host) applies it.</summary>
        public void RequestDummyHit(float damage)
        {
            if (IsServer) HostDummyHit(Me, damage); else DummyHitRequestRpc(damage);
        }

        [Rpc(SendTo.Server)]
        void DummyHitRequestRpc(float claimed, RpcParams rp = default) => HostDummyHit(rp.Receive.SenderClientId, claimed);

        void HostDummyHit(ulong who, float claimed)
        {
            var dummy = TrainingDummy.Current;
            var np = PlayerOf(who);
            if (dummy == null || np == null) return;
            var bp = np.GetComponent<Backpack>();
            var weapon = bp && bp.Equipped ? bp.Equipped.GetComponent<MeleeWeapon>() : null;
            if (weapon == null) return;                                   // no weapon, no hit
            if (!Near(np.transform, dummy.transform, DummyReach)) return; // not close enough
            float now = Time.unscaledTime;
            if (lastDummyHit.TryGetValue(who, out float t) && now - t < 0.3f) return; // faster than any swing
            lastDummyHit[who] = now;
            // the damage is the weapon's, whatever the client claimed
            dummy.ApplyDamage(weapon.Data.damage, np.transform.position);
        }
    }
}
