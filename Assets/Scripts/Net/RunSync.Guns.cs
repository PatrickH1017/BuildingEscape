using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace BuildingEscape.Net
{
    /// <summary>
    /// The player's pew in a networked run (the owner's request, 2026-10-16: the pistol lies in the outpost of floor 3 (floor 4 until 2026-10-17), so it has to work for a
    /// whole team). The shooter's machine shows its own flash, tracer and sound at once (GunController) and tells the host what it fired: the
    /// muzzle, the direction and which guard it saw in its sights. The HOST traces the bullet itself, decides what it hit (a guard goes down by
    /// the same rule as an offline hit, the training dummy loses health, a loose item is shoved), makes the noise the guards hear, and announces
    /// where the bullet ended; every other machine shows the shot in the shooter's hand. Like a melee hit, it is checked against what the
    /// player really holds, how close the muzzle is to them and how fast they fire.
    /// </summary>
    public partial class RunSync
    {
        const float GunMuzzleSlack = 4f;         // m: the muzzle is where the shooter is (their position here is a moment old)
        const float GunAimForgiveness = 1.0f;    // m: the host's guard has moved on since the shooter's picture of him was drawn

        const float GunBurstSlack = 0.15f;       // s: messages of a fast gun arrive bunched when the network hiccups; the average rate is still the gun's

        readonly Dictionary<ulong, float> nextGunShot = new();

        /// <summary>Shots the host has accepted from players, itself included (for the tests).</summary>
        public int GunShotsAccepted { get; private set; }

        /// <summary>
        /// The local player fired a gun: the host (or this machine, if it is the host) decides what the bullets did. <paramref name="dir"/> is the aim; the spread of every pellet comes from <paramref name="seed"/>
        /// (<see cref="GunRules.PelletSeed"/>), the same on every machine.
        /// </summary>
        public void RequestGunShot(Vector3 muzzle, Vector3 dir, int guardId, int seed)
        {
            if (IsServer) HostGunShot(Me, muzzle, dir, guardId, seed);
            else GunShotRequestRpc(muzzle, dir, (short)guardId, seed);
        }

        [Rpc(SendTo.Server)]
        void GunShotRequestRpc(Vector3 muzzle, Vector3 dir, short guardId, int seed, RpcParams rp = default) =>
            HostGunShot(rp.Receive.SenderClientId, muzzle, dir, guardId, seed);

        void HostGunShot(ulong who, Vector3 muzzle, Vector3 dir, int guardId, int seed)
        {
            var np = PlayerOf(who);
            if (np == null || !IsFinite(muzzle) || !IsFinite(dir) || dir.sqrMagnitude < 0.25f) return;
            var bp = np.GetComponent<Backpack>();
            var gun = bp && bp.Equipped ? bp.Equipped.GetComponent<RangedWeapon>() : null;
            if (gun == null) return;                                                   // no gun in hand, no shot
            if (np.TryGetComponent(out WobblyPlayer wp) && wp.InCustody) return;      // pinned or busted
            if ((np.transform.position - muzzle).sqrMagnitude > GunMuzzleSlack * GunMuzzleSlack) return;
            var spec = gun.Data;
            float now = Time.unscaledTime;
            float next = nextGunShot.TryGetValue(who, out float due) ? due : 0f;
            if (now < next - GunBurstSlack) return;                                    // faster than the gun can fire
            nextGunShot[who] = Mathf.Max(next, now) + spec.fireInterval * 0.8f;
            GunShotsAccepted++;

            dir.Normalize();
            byte best = 0; int bestRank = -1; Vector3 firstEnd = default;
            for (int i = 0; i < spec.Pellets; i++)                                     // (a shotgun's pellets: each its own spread, from the shot's seed)
            {
                GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread * PlayerBuffs.SpreadFactor(np.gameObject), out float yaw, out float pitch);
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(muzzle, d, spec.range, np.transform, false);
                if (imp.guard == null && i == 0) imp = Forgiven(imp, np.transform, muzzle, d, spec.range, guardId);
                byte result = HostPellet(np, imp, d, spec);
                if (i == 0) firstEnd = imp.point;
                int rank = result == 0 ? 0 : result == 1 ? 1 : result == 4 ? 2 : result == 2 ? 3 : 4;      // (what the others are shown: a guard down for good beats a guard hit beats the dummy beats a wall)
                if (rank > bestRank) { bestRank = rank; best = result; }
            }
            NoiseBus.Emit(muzzle, gun.NoiseRadius, np.gameObject);                          // every guard within earshot comes to look
            if (NetworkManager.ConnectedClientsIds.Count > 1) PlayerShotRpc((byte)np.Slot, firstEnd, best, seed);
        }

        /// <summary>What one bullet (pellet) of a player's shot does where it lands, decided by the host. 0 flew off, 1 a wall or a desk, 2 a guard, 3 a guard down for good, 4 the dummy.</summary>
        byte HostPellet(NetPlayer np, Impact imp, Vector3 dir, RangedWeapon.Spec spec)
        {
            byte result = 0;
            if (!imp.hit) return result;
            result = 1;
            if (imp.guard != null)
            {
                bool wasDown = imp.guard.Dead;
                imp.guard.BulletHitHost(spec.guardHits, false, np.gameObject);        // (a bullet damages, it never knocks over: the owner, 2026-10-19)
                if (!wasDown) result = (byte)(imp.guard.Dead ? 3 : 2);
                if (imp.guard.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down) result = 4;
                imp.dummy.ApplyDamage(spec.damage, np.transform.position);
            }
            else if (imp.body != null && imp.body.TryGetComponent(out WobblyPlayer rival) && rival.gameObject != np.gameObject)
            {
                // the PVP room: the host decides, and tells everybody the rival's health
                var rivalNp = rival.GetComponent<NetPlayer>();
                var af = np.GetComponent<PvpFighter>(); var vf = rival.GetComponent<PvpFighter>();
                if (af && vf && rivalNp && PvpRules.CanHurt(af.Inside, vf.Inside, af.Down, vf.Down, false))
                {
                    vf.Hurt(PvpRules.BulletDamage, np.Slot);
                    PvpStateRpc((byte)rivalNp.Slot, vf.Health, vf.Down);
                }
            }
            else if (imp.body != null)
            {
                imp.body.AddForceAtPosition(dir * Mathf.Min(spec.knock * 0.4f, 8f), imp.point, ForceMode.VelocityChange);
            }
            return result;
        }

        /// <summary>
        /// The shooter saw a guard in his sights that the host's own trace missed (the guard has moved a little since): when that guard is
        /// close to the shooter's line and nothing solid is between the two, he is hit all the same.
        /// </summary>
        static Impact Forgiven(Impact imp, Transform shooter, Vector3 muzzle, Vector3 dir, float range, int guardId)
        {
            if (guardId < 0) return imp;
            var g = NetIds.Get<GuardAI>(guardId);
            if (g == null || g.Dead) return imp;
            var chest = g.transform.position + Vector3.up * 0.3f;                      // (a body's pivot is its middle)
            var to = chest - muzzle;
            float along = Vector3.Dot(to, dir);
            if (along < 0.3f || along > range) return imp;
            if ((to - dir * along).magnitude > GunAimForgiveness) return imp;         // nowhere near where the shooter aimed
            var clear = Ballistics.Trace(muzzle, to, to.magnitude + 0.3f, shooter, false);
            return clear.guard == g ? clear : imp;                                     // a wall or a desk is in the way
        }

        /// <summary>Host -> everybody but the shooter: his bullet ended here (see <see cref="GunController.ShowRemoteShot"/> for what the number means).</summary>
        [Rpc(SendTo.ClientsAndHost)]
        void PlayerShotRpc(byte slot, Vector3 end, byte result, int seed)
        {
            if (!IsFinite(end)) return;
            var np = NetPlayer.BySlot(slot);
            if (!np || np.IsOwner) return;                                             // the shooter showed his own shot at once
            if (np.TryGetComponent(out GunController gc)) gc.ShowRemoteShot(end, result, seed);
        }
    }
}