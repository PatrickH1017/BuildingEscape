# Round 9, batch G07 (the four guns): firing, sounds and effects per kind.
#  - Spec gets ammoPlural and pitch; RangedWeapon.CasingOf(kind); GunRules.PelletSeed
#  - GunSounds / GunFx: sets (handgun, rifle, shotgun), a pool of cases per kind of case, pellets draw tracers only
#  - GunController: pellets (a shotgun fires eight, each with its own spread from the shot's seed), per-kind sounds / flash / case / kick, the out-of-ammo notice names the gun's own ammunition
#  - HeldGun: the barrel kicks as much as the spec says
#  - RunSync.Guns: the shot carries its seed, the host traces every pellet itself, fast guns are allowed their burst of bunched messages
#  - TestMode: a button for every gun
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}
FromDraft "$Src\Guns\GunSounds.cs" 'C:\bqs\gun_draft\GunSounds.cs'
FromDraft "$Src\Guns\GunFx.cs" 'C:\bqs\gun_draft\GunFx.cs'

# ---- RangedWeapon: ammunition names, the pitch of the shot, the case each gun throws
Open-Src "$Src\Guns\RangedWeapon.cs"
Rep @'
            public string ammoName;
            public int clipPrice;
        }
'@ @'
            public string ammoName;
            public int clipPrice;
            /// <summary>The same in the plural and in lower case, for a sentence ("the dealer sells pistol clips"), and the pitch of the gun's shot (a sniper rifle's is deeper, a sub machine gun's lighter; zero counts as 1).</summary>
            public string ammoPlural;
            public float pitch;
            public float Pitch => pitch > 0f ? pitch : 1f;
        }
'@
Rep 'unlockFloor = 3, ammoName = "Pistol Clip", clipPrice = 240,' 'unlockFloor = 3, ammoName = "Pistol Clip", clipPrice = 240, ammoPlural = "pistol clips", pitch = 1f,'
Rep 'unlockFloor = 5, ammoName = "SMG Magazine", clipPrice = 480,' 'unlockFloor = 5, ammoName = "SMG Magazine", clipPrice = 480, ammoPlural = "SMG magazines", pitch = 1.08f,'
Rep 'unlockFloor = 8, ammoName = "Rifle Magazine", clipPrice = 750,' 'unlockFloor = 8, ammoName = "Rifle Magazine", clipPrice = 750, ammoPlural = "rifle magazines", pitch = 1f,'
Rep 'unlockFloor = 6, ammoName = "Shotgun Shells", clipPrice = 420,' 'unlockFloor = 6, ammoName = "Shotgun Shells", clipPrice = 420, ammoPlural = "shotgun shells", pitch = 1f,'
Rep 'unlockFloor = 10, ammoName = "Sniper Rounds", clipPrice = 600,' 'unlockFloor = 10, ammoName = "Sniper Rounds", clipPrice = 600, ammoPlural = "sniper rounds", pitch = 0.78f,'
Rep @'
        public static GameObject PrefabOf(Kind k) => ModelOf(k).prefab;
'@ @'
        public static GameObject PrefabOf(Kind k) => ModelOf(k).prefab;
        /// <summary>The empty case this kind of gun throws out: its own cartridge (a rifle's, a shotgun's shell), else the pistol's.</summary>
        public static GameObject CasingOf(Kind k) { var m = ModelOf(k); return m != null && m.casing ? m.casing : (Catalog ? Catalog.pewBullet : null); }
'@
Save-Src

# ---- GunRules (pure): the seed of a pellet
Open-Src "$Src\Guns\GunRules.cs"
Rep @'
        /// <summary>How many spare clips these spare rounds make (a part of a clip counts as one): the "2 clips" on the screen.</summary>
'@ @'
        /// <summary>
        /// The seed of pellet number <paramref name="index"/> of a shot: the shot's own seed spread out (a prime apart), so that no two pellets of a shot share an angle. The shooter's machine and the host both work it out from the
        /// seed the shot carries, so a shotgun's pellets land in the same places for both.
        /// </summary>
        public static int PelletSeed(int shotSeed, int index) => unchecked(shotSeed + index * 7919);

        /// <summary>How many spare clips these spare rounds make (a part of a clip counts as one): the "2 clips" on the screen.</summary>
'@
Save-Src

# ---- GunController
Open-Src "$Src\Guns\GunController.cs"
Rep @'
            held = HeldGun.Attach(anim, next.kind, next.silenced);
            if (!held) return false;
            gun = next;
            reloadStep = next.Magazine.Reloading ? 0 : 99;
            GunSounds.Prewarm();
            GunSounds.Draw(transform.position, pack.Viewed);
            return true;
'@ @'
            held = HeldGun.Attach(anim, next.kind, next.silenced);
            if (!held) return false;
            gun = next;
            reloadStep = next.Magazine.Reloading ? 0 : 99;
            GunSounds.Prewarm();
            GunSounds.Draw(transform.position, pack.Viewed, next.Data.sounds);
            return true;
'@
Rep @'
        public void ShowRemoteShot(Vector3 end, byte result)
        {
            if (!gun) return;                                            // (his gun is not drawn on this machine yet)
            ShotsSeen++;
            var muzzle = held ? held.Muzzle.position : transform.position + Vector3.up * 0.3f + transform.forward * 0.6f;
            GunFx.Get().Shot(muzzle, end, result != 0, held ? held.Grip.right : transform.right, gun.silenced, held ? held.PortPosition : null);
            GunSounds.Shot(muzzle, false, gun.silenced);
'@ @'
        public void ShowRemoteShot(Vector3 end, byte result, int seed = 0)
        {
            if (!gun) return;                                            // (his gun is not drawn on this machine yet)
            ShotsSeen++;
            var spec = gun.Data;
            var muzzle = held ? held.Muzzle.position : transform.position + Vector3.up * 0.3f + transform.forward * 0.6f;
            var fx = GunFx.Get();
            fx.Shot(muzzle, end, result != 0, held ? held.Grip.right : transform.right, gun.silenced, held ? held.PortPosition : null, spec.FlashScale, RangedWeapon.CasingOf(gun.kind));
            for (int i = 1; i < spec.Pellets; i++)
            {
                // the other pellets of a shotgun shot: his aim with their own spread (the seed is the same on every machine), drawn where each would land here
                var aim = end - muzzle;
                if (aim.sqrMagnitude < 0.01f) break;
                GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread, out float yaw, out float pitch);
                var d = Quaternion.LookRotation(aim.normalized) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(muzzle, d, spec.range, transform, false);
                fx.Pellet(muzzle, imp.point, imp.hit);
            }
            GunSounds.Shot(muzzle, false, gun.silenced, spec.sounds, spec.Pitch);
'@
Rep @'
        void Shoot(RangedWeapon.Spec spec, Vector3 muzzle)
        {
            ShotsFired++;
            var aimPoint = AimPoint(out _);
            var dir = aimPoint - muzzle;
            var cam = Camera.main;
            // (a wall right in front of the gun puts the aim point behind or beside the muzzle: shoot along the view then)
            if (dir.sqrMagnitude < 0.04f || (cam && Vector3.Dot(dir, cam.transform.forward) < 0f)) dir = cam ? cam.transform.forward : transform.forward;
            dir.Normalize();
            GunRules.Spread(Random.Range(1, int.MaxValue), spec.spread, out float yaw, out float pitch);
            dir = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;

            var imp = Ballistics.Trace(muzzle, dir, spec.range, transform, false);
            LastMuzzle = muzzle; LastDirection = dir; LastImpact = imp;
            bool synced = Net.NetRole.Synced;
            if (!synced) Land(imp, dir, spec);                           // (in a networked run the host decides what the bullet does)

            GunFx.Get().Shot(muzzle, imp.point, imp.hit, held ? held.Grip.right : transform.right, gun.silenced, held ? held.PortPosition : null);
            GunSounds.Shot(muzzle, true, gun.silenced);
            if (held) held.Kick();
            if (synced)
            {
                // the same hit sound at once (a guess: the host may disagree), then the host does the rest and tells everybody else
                if (imp.hit && ((imp.guard != null && !imp.guard.Dead) || (imp.dummy != null && !imp.dummy.Down))) GameAudio.BulletHit(false, imp.point);
                Net.RunSync.Instance.RequestGunShot(muzzle, dir, imp.guard != null ? imp.guard.NetId : -1);
            }
            else NoiseBus.Emit(muzzle, gun.NoiseRadius, gameObject);          // every guard within earshot comes to look
        }

        /// <summary>What the bullet does where it lands.</summary>
        void Land(Impact imp, Vector3 dir, RangedWeapon.Spec spec)
        {
            if (!imp.hit) return;
            if (imp.guard != null)
            {
                bool wasDown = imp.guard.Dead;
                imp.guard.WeaponHit(spec.stun, gameObject, spec.guardHits);
                if (!wasDown) GameAudio.BulletHit(imp.guard.Dead, imp.point);
                if (imp.guard.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down) GameAudio.BulletHit(false, imp.point);
                imp.dummy.Hit(spec.damage, transform.position);
            }
'@ @'
        void Shoot(RangedWeapon.Spec spec, Vector3 muzzle)
        {
            ShotsFired++;
            var aimPoint = AimPoint(out _);
            var dir = aimPoint - muzzle;
            var cam = Camera.main;
            // (a wall right in front of the gun puts the aim point behind or beside the muzzle: shoot along the view then)
            if (dir.sqrMagnitude < 0.04f || (cam && Vector3.Dot(dir, cam.transform.forward) < 0f)) dir = cam ? cam.transform.forward : transform.forward;
            dir.Normalize();
            int seed = Random.Range(1, int.MaxValue);                    // (the shot's own: every pellet's spread comes from it, here and on the host)
            bool synced = Net.NetRole.Synced;
            var fx = GunFx.Get();
            var rightSide = held ? held.Grip.right : transform.right;
            var port = held ? held.PortPosition : null;
            var casing = RangedWeapon.CasingOf(gun.kind);
            bool hitSounded = false, foeSeen = false;
            for (int i = 0; i < spec.Pellets; i++)
            {
                GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread, out float yaw, out float pitch);
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(muzzle, d, spec.range, transform, false);
                if (i == 0) { LastMuzzle = muzzle; LastDirection = d; LastImpact = imp; }
                if (!synced) Land(imp, d, spec, ref hitSounded);         // (in a networked run the host decides what the bullets do)
                else if (imp.hit && ((imp.guard != null && !imp.guard.Dead) || (imp.dummy != null && !imp.dummy.Down))) foeSeen = true;
                if (i == 0) fx.Shot(muzzle, imp.point, imp.hit, rightSide, gun.silenced, port, spec.FlashScale, casing);
                else fx.Pellet(muzzle, imp.point, imp.hit);
            }
            GunSounds.Shot(muzzle, true, gun.silenced, spec.sounds, spec.Pitch);
            if (held) held.Kick();
            if (synced)
            {
                // the same hit sound at once (a guess: the host may disagree), then the host does the rest and tells everybody else
                if (foeSeen) GameAudio.BulletHit(false, LastImpact.point);
                Net.RunSync.Instance.RequestGunShot(muzzle, dir, LastImpact.guard != null ? LastImpact.guard.NetId : -1, seed);
            }
            else NoiseBus.Emit(muzzle, gun.NoiseRadius, gameObject);          // every guard within earshot comes to look
        }

        /// <summary>What a bullet (a pellet) does where it lands. <paramref name="soundPlayed"/>: the hit sound of this shot has been played (a shotgun's eight pellets make one).</summary>
        void Land(Impact imp, Vector3 dir, RangedWeapon.Spec spec, ref bool soundPlayed)
        {
            if (!imp.hit) return;
            if (imp.guard != null)
            {
                bool wasDown = imp.guard.Dead;
                imp.guard.WeaponHit(spec.stun, gameObject, spec.guardHits);
                if (!wasDown && !soundPlayed) { GameAudio.BulletHit(imp.guard.Dead, imp.point); soundPlayed = true; }
                if (imp.guard.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down && !soundPlayed) { GameAudio.BulletHit(false, imp.point); soundPlayed = true; }
                imp.dummy.Hit(spec.damage, transform.position);
            }
'@
Rep @'
        void PlayReloadSounds(GunMagazine mag, float now)
        {
            if (!mag.Reloading || reloadStep >= GunSounds.ReloadStepTimes.Length) return;
            float scale = mag.ReloadSeconds / 2.2f;
            while (reloadStep < GunSounds.ReloadStepTimes.Length && now - mag.ReloadStartedAt >= GunSounds.ReloadStepTimes[reloadStep] * scale)
            {
                GunSounds.ReloadStep(reloadStep, transform.position, true);
                reloadStep++;
            }
        }
'@ @'
        void PlayReloadSounds(GunMagazine mag, float now)
        {
            var times = GunSounds.ReloadTimes(gun.Data.sounds);
            if (!mag.Reloading || reloadStep >= times.Length) return;
            float scale = mag.ReloadSeconds / 2.2f;
            while (reloadStep < times.Length && now - mag.ReloadStartedAt >= times[reloadStep] * scale)
            {
                GunSounds.ReloadStep(reloadStep, transform.position, true, gun.Data.sounds);
                reloadStep++;
            }
        }
'@
Rep 'Backpack.NoticeToLocal("Out of ammo - the dealer in the Loading Docks sells pistol clips");' 'Backpack.NoticeToLocal("Out of ammo - the dealer in the Loading Docks sells " + (gun ? gun.Data.ammoPlural : "pistol clips"));'
Save-Src

# ---- HeldGun: the kick is the gun's own
Open-Src "$Src\Guns\HeldGun.cs"
Rep @'
        /// <summary>Degrees the barrel kicks up when a shot is fired.</summary>
        const float KickDegrees = 9f;

'@ ''
Rep 'aim = Quaternion.AngleAxis(-kick * KickDegrees, right.normalized) * aim;' 'aim = Quaternion.AngleAxis(-kick * RangedWeapon.Specs[(int)Kind].KickDegrees, right.normalized) * aim;'
Save-Src

# ---- RunSync.Guns: the seed, every pellet traced by the host, a burst allowance for bunched messages
Open-Src "$Src\Net\RunSync.Guns.cs"
Rep @'
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
'@ @'
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
'@
Rep @'
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
                else if (imp.body != null && imp.body.TryGetComponent(out WobblyPlayer rival) && rival.gameObject != np.gameObject)
                {
                    // the PVP room: the host decides, and tells everybody the rival's health
                    var rivalNp = rival.GetComponent<NetPlayer>();
                    var af = np.GetComponent<PvpFighter>(); var vf = rival.GetComponent<PvpFighter>();
                    if (af && vf && rivalNp && PvpRules.CanHurt(af.Inside, vf.Inside, af.Down, vf.Down, false))
                    {
                        vf.Hurt(PvpRules.BulletDamage);
                        PvpStateRpc((byte)rivalNp.Slot, vf.Health, vf.Down);
                    }
                }
                else if (imp.body != null)
                {
                    imp.body.AddForceAtPosition(dir * Mathf.Min(spec.knock * 0.4f, 8f), imp.point, ForceMode.VelocityChange);
                }
            }
            NoiseBus.Emit(muzzle, gun.NoiseRadius, np.gameObject);                          // every guard within earshot comes to look
            if (NetworkManager.ConnectedClientsIds.Count > 1) PlayerShotRpc((byte)np.Slot, imp.point, result);
        }
'@ @'
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
                GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread, out float yaw, out float pitch);
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
                imp.guard.WeaponHitHost(spec.stun, np.gameObject, spec.guardHits);
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
                    vf.Hurt(PvpRules.BulletDamage);
                    PvpStateRpc((byte)rivalNp.Slot, vf.Health, vf.Down);
                }
            }
            else if (imp.body != null)
            {
                imp.body.AddForceAtPosition(dir * Mathf.Min(spec.knock * 0.4f, 8f), imp.point, ForceMode.VelocityChange);
            }
            return result;
        }
'@
Rep @'
        void PlayerShotRpc(byte slot, Vector3 end, byte result)
        {
            if (!IsFinite(end)) return;
            var np = NetPlayer.BySlot(slot);
            if (!np || np.IsOwner) return;                                             // the shooter showed his own shot at once
            if (np.TryGetComponent(out GunController gc)) gc.ShowRemoteShot(end, result);
        }
'@ @'
        void PlayerShotRpc(byte slot, Vector3 end, byte result, int seed)
        {
            if (!IsFinite(end)) return;
            var np = NetPlayer.BySlot(slot);
            if (!np || np.IsOwner) return;                                             // the shooter showed his own shot at once
            if (np.TryGetComponent(out GunController gc)) gc.ShowRemoteShot(end, result, seed);
        }
'@
Save-Src

# ---- TestMode: a button for every gun
Open-Src "$Src\TestMode.cs"
Rep @'
            if (GUILayout.Button("Pew (gun)"))
            {
                if (pack && RangedWeapon.Available) GivePew(pack);
                else Say("The Guns pack is not set up: run Employees Take Revenge > Setup Weapons");
            }
            GUILayout.EndHorizontal();
'@ @'
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (RangedWeapon.Kind gk in System.Enum.GetValues(typeof(RangedWeapon.Kind)))
            {
                if (!GUILayout.Button(RangedWeapon.Specs[(int)gk].name)) continue;
                if (pack && RangedWeapon.Available && RangedWeapon.Has(gk)) GiveGun(pack, gk);
                else Say("The Guns pack is not set up: run Employees Take Revenge > Setup Weapons");
            }
            GUILayout.EndHorizontal();
'@
Rep @'
        void GivePew(Backpack pack)
        {
            var go = RangedWeapon.Create(RangedWeapon.Kind.Pew, SpawnPoint(pack), null);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            var p = go.GetComponent<Pickup>();
            if (!pack.TryStore(p)) { Say("Pack too full - dropped it in front of you (raise capacity)"); return; }
            pack.SetEquipped(p);
            Say("Pew in your hand: LMB fires, R reloads");
        }
'@ @'
        void GiveGun(Backpack pack, RangedWeapon.Kind kind)
        {
            var go = RangedWeapon.Create(kind, SpawnPoint(pack), null);
            var floor = FindFirstObjectByType<FloorGenerator>();
            if (floor && floor.Current) go.transform.SetParent(floor.Current, true);
            var p = go.GetComponent<Pickup>();
            if (!pack.TryStore(p)) { Say("Pack too full - dropped it in front of you (raise capacity)"); return; }
            pack.SetEquipped(p);
            Say(RangedWeapon.Specs[(int)kind].name + " in your hand: LMB fires, R reloads");
        }

        void GivePew(Backpack pack) => GiveGun(pack, RangedWeapon.Kind.Pew);          // (the tests call it by this name)
'@
Rep @'
                GUILayout.Label(m.Reloading ? "Pew: reloading ..." : $"Pew: {m.Rounds} / {m.Capacity} rounds, {(m.InfiniteReserve ? "endless" : m.Reserve.ToString())} spare (LMB fires, R reloads)");
'@ @'
                GUILayout.Label(m.Reloading ? gun.Data.name + ": reloading ..." : $"{gun.Data.name}: {m.Rounds} / {m.Capacity} rounds, {(m.InfiniteReserve ? "endless" : m.Reserve.ToString())} spare (LMB fires, R reloads)");
'@
Save-Src
