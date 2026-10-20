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

        readonly Dictionary<ulong, float> lastGunShot = new();

        /// <summary>Shots the host has accepted from players, itself included (for the tests).</summary>
        public int GunShotsAccepted { get; private set; }

        /// <summary>The local player fired the pew: the host (or this machine, if it is the host) decides what the bullet did.</summary>
        public void RequestGunShot(Vector3 muzzle, Vector3 dir, int guardId)
        {
            if (IsServer) HostGunShot(Me, muzzle, dir, guardId);
            else GunShotRequestRpc(muzzle, dir, (short)guardId);
        }

        [Rpc(SendTo.Server)]
        void GunShotRequestRpc(Vector3 muzzle, Vector3 dir, short guardId, RpcParams rp = default) =>
            HostGunShot(rp.Receive.SenderClientId, muzzle, dir, guardId);

        void HostGunShot(ulong who, Vector3 muzzle, Vector3 dir, int guardId)
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
            if (lastGunShot.TryGetValue(who, out float t) && now - t < spec.fireInterval * 0.7f) return;    // faster than the gun can fire
            lastGunShot[who] = now;
            GunShotsAccepted++;

            dir.Normalize();
            var imp = Ballistics.Trace(muzzle, dir, spec.range, np.transform, false);
            if (imp.guard == null) imp = Forgiven(imp, np.transform, muzzle, dir, spec.range, guardId);
            byte result = 0;                                                           // 0 flew off, 1 a wall or a desk, 2 a guard, 3 a guard down for good, 4 the dummy
            if (imp.hit)
            {
                result = 1;
                if (imp.guard != null)
                {
                    bool wasDown = imp.guard.Dead;
                    imp.guard.WeaponHitHost(spec.stun, np.gameObject, spec.guardHits);
                    if (!wasDown) result = (byte)(imp.guard.Dead ? 3 : 2);
                    if (imp.guard.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
                }
                else if (imp.dummy != null)
                {
                    if (!imp.dummy.Down) result = 4;
                    imp.dummy.ApplyDamage(spec.damage, np.transform.position);
                }
                else if (imp.body != null)
                {
                    imp.body.AddForceAtPosition(dir * Mathf.Min(spec.knock * 0.4f, 8f), imp.point, ForceMode.VelocityChange);
                }
            }
            NoiseBus.Emit(muzzle, spec.noise, np.gameObject);                          // every guard within earshot comes to look
            if (NetworkManager.ConnectedClientsIds.Count > 1) PlayerShotRpc((byte)np.Slot, imp.point, result);
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
        void PlayerShotRpc(byte slot, Vector3 end, byte result)
        {
            if (!IsFinite(end)) return;
            var np = NetPlayer.BySlot(slot);
            if (!np || np.IsOwner) return;                                             // the shooter showed his own shot at once
            if (np.TryGetComponent(out GunController gc)) gc.ShowRemoteShot(end, result);
        }
    }
}