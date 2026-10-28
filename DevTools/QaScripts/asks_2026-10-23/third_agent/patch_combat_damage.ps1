# The owner's task list of 2026-10-27, part 1: set bullet damage by ammunition (light 25 / 40 head, medium 40 / 60, heavy 100, shotgun 75), headshots, the hit marker and its three sounds (hit, headshot, elimination), and the aiming flag (hip fire by default, right mouse button = aiming) with the hip's wider spread.
# New files (sources in combat2\): Scripts\Guns\BulletDamageRules.cs, HitFeedback.cs, AimRules.cs. The rest are edits. The poses (HeldGun) are part 2.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

foreach ($pair in @(@('BulletDamageRules.cs', 'Scripts\Guns\BulletDamageRules.cs'), @('HitFeedback.cs', 'Scripts\Guns\HitFeedback.cs'), @('AimRules.cs', 'Scripts\Guns\AimRules.cs'))) {
  $src = Join-Path $here ('combat2\' + $pair[0]); $dst = $A + $pair[1]
  if ($Dry) { if (-not (Test-Path $src)) { throw "missing $src" }; Write-Output "dry ok would copy $($pair[0])" } else { Copy-Item $src $dst -Force; Write-Output "copied $($pair[0])" }
}

# ---- the three sounds of the hit marker (generated placeholders; the owner supplies the real ones)
Open-Src ($A + 'Scripts\Audio\OutpostSound.cs')
Rep '        public const int PingVariants = 1;' @'
        public const int HitVariants = 1;

        /// <summary>The hit marker's sound for a plain hit (the owner, 2026-10-27): a short dry tick, 1.85 kHz with a click on it. A placeholder.</summary>
        public static float[] HitTick()
        {
            var o = new float[Dsp.Samples(0.1f)];
            float ph = 0f, dec = Dsp.DecayPerSample(0.02f), env = 1f;
            for (int i = 0; i < o.Length; i++) { ph += 1850f / SR; ph -= (int)ph; o[i] = Dsp.Sine(ph) * env; env *= dec; }
            int click = Dsp.Samples(0.0025f);
            for (int i = 0; i < click && i < o.Length; i++) o[i] += (i % 2 == 0 ? 0.7f : -0.7f) * (1f - i / (float)click);
            Dsp.FadeEnds(o, 0.002f);
            Dsp.Normalize(o, 0.5f);
            return o;
        }

        /// <summary>The hit marker's sound for a headshot: a bright metallic ding, 2.64 and 3.96 kHz ringing for a fifth of a second. A placeholder.</summary>
        public static float[] HitHead()
        {
            var o = new float[Dsp.Samples(0.3f)];
            float p1 = 0f, p2 = 0f, dec = Dsp.DecayPerSample(0.07f), env = 1f;
            for (int i = 0; i < o.Length; i++)
            {
                p1 += 2640f / SR; p1 -= (int)p1; p2 += 3960f / SR; p2 -= (int)p2;
                o[i] = (Dsp.Sine(p1) + 0.55f * Dsp.Sine(p2)) * env;
                env *= dec;
            }
            int click = Dsp.Samples(0.002f);
            for (int i = 0; i < click && i < o.Length; i++) o[i] += (i % 2 == 0 ? 0.8f : -0.8f) * (1f - i / (float)click);
            Dsp.FadeEnds(o, 0.003f);
            Dsp.Normalize(o, 0.6f);
            return o;
        }

        /// <summary>The hit marker's sound for an elimination: a heavy falling thump with a noise burst and a short high ping on top, nearly half a second. A placeholder.</summary>
        public static float[] HitKill()
        {
            var o = new float[Dsp.Samples(0.4f)];
            float ph = 0f, env = 1f, dec = Dsp.DecayPerSample(0.11f);
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)o.Length;
                float hz = Dsp.Lerp(200f, 62f, System.Math.Min(1f, t * 3f));
                ph += hz / SR; ph -= (int)ph;
                o[i] = Dsp.Sine(ph) * env * 0.9f;
                env *= dec;
            }
            int at = Dsp.Samples(0.06f);
            float pp = 0f, pe = 1f, pd = Dsp.DecayPerSample(0.06f);
            for (int i = 0; at + i < o.Length; i++) { pp += 1180f / SR; pp -= (int)pp; o[at + i] += Dsp.Sine(pp) * pe * 0.45f; pe *= pd; }
            uint seed = 12345u; int burst = Dsp.Samples(0.04f);
            for (int i = 0; i < burst && i < o.Length; i++) { seed = seed * 1664525u + 1013904223u; float nz = ((seed >> 8) & 0xFFFF) / 32768f - 1f; o[i] += nz * (1f - i / (float)burst) * 0.5f; }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.7f);
            return o;
        }

        public const int PingVariants = 1;
'@
Save-Src
Open-Src ($A + 'Scripts\Audio\SoundBank.cs')
Rep 'Punch, Munch, Ping }' 'Punch, Munch, Ping, HitMarker, HitHead, HitKill }'
Rep 'OutpostSound.PunchVariants, OutpostSound.MunchVariants, OutpostSound.PingVariants,' 'OutpostSound.PunchVariants, OutpostSound.MunchVariants, OutpostSound.PingVariants, OutpostSound.HitVariants, OutpostSound.HitVariants, OutpostSound.HitVariants,'
Rep '                case SoundId.Ping: return OutpostSound.Ping(variant);' @'
                case SoundId.Ping: return OutpostSound.Ping(variant);
                case SoundId.HitMarker: return OutpostSound.HitTick();
                case SoundId.HitHead: return OutpostSound.HitHead();
                case SoundId.HitKill: return OutpostSound.HitKill();
'@
Save-Src

# ---- the gun table: the damage of every gun is its ammunition's (guardHits is a number of hits of 10 HP now: 2.5, 4, 7.5, 10)
Open-Src ($A + 'Scripts\Guns\RangedWeapon.cs')
Rep '            /// <summary>Health a bullet takes off a training dummy (a melee hit takes 11 to 22); for a shotgun, what each pellet takes.</summary>' '            /// <summary>Health a bullet takes off a training dummy (a melee hit takes 11 to 22): the gun''s ammunition''s body-shot damage (BulletDamageRules), for a shotgun what a whole shot takes.</summary>'
Rep '            /// <summary>How many of a guard''s hits to knock-out one bullet counts as (a melee hit counts as one); for a shotgun, what each pellet counts as.</summary>' '            /// <summary>How many of a guard''s hits (10 HP each) a body shot with this gun counts as (a melee hit counts as one): its ammunition''s damage (the owner, 2026-10-27: light 25, medium 40, heavy 100, shotgun 75 a shot; BulletDamageRules). A headshot does more: RangedWeapon.HitsFor.</summary>'
Rep '            public int guardHits;' '            public float guardHits;'
Rep 'damage = 60f, guardHits = 4,' 'damage = BulletDamageRules.LightBody, guardHits = BulletDamageRules.LightBody / GuardHealthRules.HpPerHit,'
Rep 'damage = 24f, guardHits = 2,' 'damage = BulletDamageRules.LightBody, guardHits = BulletDamageRules.LightBody / GuardHealthRules.HpPerHit,'
Rep 'damage = 42f, guardHits = 4,' 'damage = BulletDamageRules.MediumBody, guardHits = BulletDamageRules.MediumBody / GuardHealthRules.HpPerHit,'
Rep 'damage = 14f, guardHits = 1,' 'damage = BulletDamageRules.ShotgunBody, guardHits = BulletDamageRules.ShotgunBody / GuardHealthRules.HpPerHit,'
Rep 'damage = 150f, guardHits = 10,' 'damage = BulletDamageRules.HeavyBody, guardHits = BulletDamageRules.HeavyBody / GuardHealthRules.HpPerHit,'
Rep '        public Spec Data => Specs[(int)kind];' @'
        public Spec Data => Specs[(int)kind];

        /// <summary>How many of an officer's hits (10 HP each) one bullet of this gun counts as: a body shot, or a headshot (the owner, 2026-10-27).</summary>
        public float HitsFor(bool head) => BulletDamageRules.Hits(kind, head);
'@
Save-Src
Open-Src ($A + 'Scripts\Hud\WeaponCardRules.cs')
Rep 'public static float GunDamage(int guardHits) => GuardHealthRules.HitsToHp(Math.Max(0, guardHits));' 'public static float GunDamage(float guardHits) => GuardHealthRules.HitsToHp(Math.Max(0f, guardHits));'
Save-Src
Open-Src ($A + 'Scripts\Guns\GunRules.cs')
Rep '        public static int BulletsToKill(int hitsToKill, int hitsPerBullet) =>
            (Math.Max(1, hitsToKill) + Math.Max(1, hitsPerBullet) - 1) / Math.Max(1, hitsPerBullet);' @'
        public static int BulletsToKill(float hitsToKill, float hitsPerBullet) =>
            Math.Max(1, (int)Math.Ceiling(Math.Max(1f, hitsToKill) / (hitsPerBullet <= 0f ? 1f : hitsPerBullet) - 1e-4));
'@
Save-Src

# ---- the officer: where his head is
Open-Src ($A + 'Scripts\GuardAI.cs')
Rep '        void CutRadioAndRelease()' @'
        /// <summary>Did a bullet that hit him at this height (world y) hit his head (the owner, 2026-10-27: "headshots should deal more damage than body shots")? The top fifth of his body, from his feet (<see cref="BulletDamageRules.HeadFrom"/>), times his scale.</summary>
        public bool IsHeadHit(float hitY)
        {
            float s = Mathf.Abs(transform.lossyScale.y);
            return BulletDamageRules.IsHeadshot(hitY, transform.position.y - GuardRules.FeetBelowMiddle * s, GuardRules.BodyHeight * s);
        }

        void CutRadioAndRelease()
'@
Save-Src

# ---- the player: aiming (replicated for the teammates' view of the gun)
Open-Src ($A + 'Scripts\WobblyPlayer.cs')
Rep '        public bool IsCrouching { get; private set; }' @'
        public bool IsCrouching { get; private set; }
        /// <summary>The gun is up at the sights (the right mouse button; the owner, 2026-10-27): set by the player's own <see cref="GunController"/>, and on a teammate's puppet by <c>NetPlayer</c>.</summary>
        public bool IsAiming { get; set; }
'@
Save-Src
Open-Src ($A + 'Scripts\Net\NetPlayer.cs')
Rep '        // Crouched, written by the owner: teammates see the crouch (the puppet''s own script is off).' @'
        // Aiming (the gun is up at the sights), written by the owner: teammates see the gun at the hip or at the sights (the owner, 2026-10-27).
        readonly NetworkVariable<bool> aiming = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Crouched, written by the owner: teammates see the crouch (the puppet's own script is off).
'@
Rep '            if (IsOwner) { hidden.Value = wobbly.IsHidden; crouched.Value = wobbly.IsCrouching; return; }' '            if (IsOwner) { hidden.Value = wobbly.IsHidden; crouched.Value = wobbly.IsCrouching; aiming.Value = wobbly.IsAiming; return; }'
Rep '            wobbly.SetPuppetCrouch(crouched.Value);' @'
            wobbly.SetPuppetCrouch(crouched.Value);
            wobbly.IsAiming = aiming.Value;
'@
Save-Src

# ---- the shooter's machine (offline: it decides what its bullets do)
Open-Src ($A + 'Scripts\Guns\GunController.cs')
Rep '        float nextAmmoNotice;' @'
        float nextAmmoNotice;

        // ---- hip fire and aiming (the owner, 2026-10-27; AimRules, HitFeedback, BulletDamageRules)
        /// <summary>The gun is up at the sights (the right mouse button held); otherwise it is fired from the hip, into a wider cone. For a teammate: what his machine says (a networked run).</summary>
        public bool Aiming { get; private set; }
        /// <summary>A test's own aiming button (null: the real right mouse button).</summary>
        public static bool? TestAim;
        /// <summary>A test's own point to aim the shots at (null: where the crosshair points).</summary>
        public static Vector3? TestAimPoint;
        readonly Impact[] pelletImpacts = new Impact[16];
        readonly Vector3[] pelletDirs = new Vector3[16];
        readonly ShotTally tally = new ShotTally();
'@
Rep 'static void ResetTestTrigger() { TestTrigger = null; TestCursorLocked = null; }' 'static void ResetTestTrigger() { TestTrigger = null; TestCursorLocked = null; TestAim = null; TestAimPoint = null; }'
Rep '            bool usable = viewed && CanUse();' @'
            bool usable = viewed && CanUse();
            Aiming = viewed ? (TestAim ?? (usable && Input.GetMouseButton(1))) : wobbly && wobbly.IsAiming;
            if (viewed && wobbly) wobbly.IsAiming = Aiming;
'@
Rep '            foe = false;
            float range = gun.Data.range;' @'
            foe = false;
            if (TestAimPoint.HasValue) return TestAimPoint.Value;
            float range = gun.Data.range;
'@
RepRegion '            bool hitSounded = false, foeSeen = false;' '            GunSounds.Shot(muzzle, true, gun.silenced, spec.sounds, spec.Pitch);' @'
            bool hitSounded = false, foeSeen = false;
            int pellets = spec.Pellets;
            float cone = spec.spread * PlayerBuffs.SpreadFactor(gameObject) * AimRules.SpreadFactor(Aiming);          // (from the hip a shot scatters wider)
            tally.Reset();
            for (int i = 0; i < pellets; i++)
            {
                GunRules.Spread(GunRules.PelletSeed(seed, i), cone, out float yaw, out float pitch);
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(origin, d, spec.range, transform, false);
                pelletImpacts[i] = imp; pelletDirs[i] = d;
                if (imp.guard != null && !imp.guard.Dead) tally.Note(imp.guard, imp.guard.IsHeadHit(imp.point.y));
                if (i == 0) { LastMuzzle = muzzle; LastDirection = d; LastImpact = imp; }
                if (synced && imp.hit && ((imp.guard != null && !imp.guard.Dead) || (imp.dummy != null && !imp.dummy.Down))) foeSeen = true;
                if (i == 0) fx.Shot(muzzle, imp.point, imp.hit, rightSide, gun.silenced, port, spec.FlashScale, casing);
                else fx.Pellet(muzzle, imp.point, imp.hit);
            }
            if (!synced)                                                      // (in a networked run the host decides what the bullets do)
            {
                var feedback = HitKind.None;
                for (int i = 0; i < pellets; i++) Land(pelletImpacts[i], pelletDirs[i], spec, ref hitSounded, ref feedback);
                HitFeedback.Play(feedback);                                   // (the hit marker and its sound: a hit, a headshot, an elimination; the owner, 2026-10-27)
            }
            GunSounds.Shot(muzzle, true, gun.silenced, spec.sounds, spec.Pitch);
'@
Rep '                if (foeSeen) GameAudio.BulletHit(false, LastImpact.point);' @'
                if (foeSeen) { GameAudio.BulletHit(false, LastImpact.point); HitFeedback.Play(HitKind.Hit); }          // (the host's answer adds the headshot or the elimination: RunSync.ShotFeedbackRpc)
'@
Rep 'Net.RunSync.Instance.RequestGunShot(origin, dir, LastImpact.guard != null ? LastImpact.guard.NetId : -1, seed);' 'Net.RunSync.Instance.RequestGunShot(origin, dir, LastImpact.guard != null ? LastImpact.guard.NetId : -1, seed, Aiming);'
Rep 'public void ShowRemoteShot(Vector3 end, byte result, int seed = 0)' 'public void ShowRemoteShot(Vector3 end, byte result, int seed = 0, bool aiming = true)'
Rep 'GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread * PlayerBuffs.SpreadFactor(gameObject), out float yaw, out float pitch);' 'GunRules.Spread(GunRules.PelletSeed(seed, i), spec.spread * PlayerBuffs.SpreadFactor(gameObject) * AimRules.SpreadFactor(aiming), out float yaw, out float pitch);'
RepRegion '        void Land(Impact imp, Vector3 dir, RangedWeapon.Spec spec, ref bool soundPlayed)' '                imp.dummy.Hit(spec.damage, transform.position);' @'
        void Land(Impact imp, Vector3 dir, RangedWeapon.Spec spec, ref bool soundPlayed, ref HitKind feedback)
        {
            if (!imp.hit) return;
            if (imp.guard != null)
            {
                var g = imp.guard;
                bool wasDown = g.Dead;
                // a set amount for each kind of ammunition, more for a headshot, and a shotgun's shot is dealt once to each officer however many pellets hit him (the owner, 2026-10-27)
                if (!wasDown && tally.Take(g, out bool head))
                {
                    g.BulletHitHost(gun.HitsFor(head), false, gameObject);          // (a bullet damages, it never knocks over: the owner, 2026-10-19)
                    feedback = BulletDamageRules.Stronger(feedback, BulletDamageRules.Feedback(g.Dead, head));
                    if (!soundPlayed) { GameAudio.BulletHit(g.Dead, imp.point); soundPlayed = true; }
                }
                if (g.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down)
                {
                    feedback = BulletDamageRules.Stronger(feedback, HitKind.Hit);
                    if (!soundPlayed) { GameAudio.BulletHit(false, imp.point); soundPlayed = true; }
                }
                if (tally.Take(imp.dummy, out _)) imp.dummy.Hit(BulletDamageRules.Damage(gun.kind, false), transform.position);
'@
Save-Src

# ---- the host (a client's shot): the same damage, headshots, the answer to the shooter, the aiming flag
Open-Src ($A + 'Scripts\Net\RunSync.Guns.cs')
Rep '        readonly Dictionary<ulong, float> nextGunShot = new();' @'
        readonly Dictionary<ulong, float> nextGunShot = new();
        readonly Impact[] pelletImpacts = new Impact[16];
        readonly Vector3[] pelletDirs = new Vector3[16];
        readonly ShotTally tally = new ShotTally();
'@
Rep 'public void RequestGunShot(Vector3 muzzle, Vector3 dir, int guardId, int seed)' 'public void RequestGunShot(Vector3 muzzle, Vector3 dir, int guardId, int seed, bool aiming = true)'
Rep 'if (IsServer) HostGunShot(Me, muzzle, dir, guardId, seed);' 'if (IsServer) HostGunShot(Me, muzzle, dir, guardId, seed, aiming);'
Rep 'else GunShotRequestRpc(muzzle, dir, (short)guardId, seed);' 'else GunShotRequestRpc(muzzle, dir, (short)guardId, seed, aiming);'
Rep 'void GunShotRequestRpc(Vector3 muzzle, Vector3 dir, short guardId, int seed, RpcParams rp = default) =>' 'void GunShotRequestRpc(Vector3 muzzle, Vector3 dir, short guardId, int seed, bool aiming, RpcParams rp = default) =>'
Rep 'HostGunShot(rp.Receive.SenderClientId, muzzle, dir, guardId, seed);' 'HostGunShot(rp.Receive.SenderClientId, muzzle, dir, guardId, seed, aiming);'
Rep 'void HostGunShot(ulong who, Vector3 muzzle, Vector3 dir, int guardId, int seed)' 'void HostGunShot(ulong who, Vector3 muzzle, Vector3 dir, int guardId, int seed, bool aiming = true)'
RepRegion '            byte best = 0; int bestRank = -1; Vector3 firstEnd = default;' '            if (NetworkManager.ConnectedClientsIds.Count > 1) PlayerShotRpc((byte)np.Slot, firstEnd, best, seed);' @'
            byte best = 0; int bestRank = -1; Vector3 firstEnd = default;
            int pellets = spec.Pellets;
            float cone = spec.spread * PlayerBuffs.SpreadFactor(np.gameObject) * AimRules.SpreadFactor(aiming);          // (from the hip a shot scatters wider)
            tally.Reset();
            for (int i = 0; i < pellets; i++)                                          // (a shotgun's pellets: each its own spread, from the shot's seed)
            {
                GunRules.Spread(GunRules.PelletSeed(seed, i), cone, out float yaw, out float pitch);
                var d = Quaternion.LookRotation(dir) * Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                var imp = Ballistics.Trace(muzzle, d, spec.range, np.transform, false);
                if (imp.guard == null && i == 0) imp = Forgiven(imp, np.transform, muzzle, d, spec.range, guardId);
                pelletImpacts[i] = imp; pelletDirs[i] = d;
                if (imp.guard != null && !imp.guard.Dead) tally.Note(imp.guard, imp.guard.IsHeadHit(imp.point.y));
                if (i == 0) firstEnd = imp.point;
            }
            var feedback = HitKind.None;
            for (int i = 0; i < pellets; i++)
            {
                byte result = HostPellet(np, pelletImpacts[i], pelletDirs[i], gun.kind);
                int rank = result == 0 ? 0 : result == 1 ? 1 : result == 4 ? 2 : result == 2 ? 3 : result == 5 ? 4 : 5;      // (what the others are shown: a guard down for good beats a headshot beats a guard hit beats the dummy beats a wall)
                if (rank > bestRank) { bestRank = rank; best = result; }
                feedback = BulletDamageRules.Stronger(feedback, result == 3 ? HitKind.Kill : result == 5 ? HitKind.Head : (result == 2 || result == 4) ? HitKind.Hit : HitKind.None);
            }
            NoiseBus.Emit(muzzle, gun.NoiseRadius, np.gameObject);                          // every guard within earshot comes to look
            if (feedback != HitKind.None)                                                   // (the hit marker and its sound go to the shooter alone: the owner, 2026-10-27)
            {
                if (who == Me) HitFeedback.Play(feedback); else ShotFeedbackRpc((byte)feedback, RpcTarget.Single(who, RpcTargetUse.Temp));
            }
            if (NetworkManager.ConnectedClientsIds.Count > 1) PlayerShotRpc((byte)np.Slot, firstEnd, best, seed, aiming);
'@
RepRegion '        byte HostPellet(NetPlayer np, Impact imp, Vector3 dir, RangedWeapon.Spec spec)' '                imp.dummy.ApplyDamage(spec.damage, np.transform.position);' @'
        byte HostPellet(NetPlayer np, Impact imp, Vector3 dir, RangedWeapon.Kind kind)
        {
            var spec = RangedWeapon.Specs[(int)kind];
            byte result = 0;
            if (!imp.hit) return result;
            result = 1;
            if (imp.guard != null)
            {
                bool wasDown = imp.guard.Dead;
                bool dealt = false, head = false;
                if (!wasDown) dealt = tally.Take(imp.guard, out head);
                // a set amount for each kind of ammunition, more for a headshot, a shotgun's shot dealt once to each officer (the owner, 2026-10-27)
                if (dealt) imp.guard.BulletHitHost(BulletDamageRules.Hits(kind, head), false, np.gameObject);        // (a bullet damages, it never knocks over: the owner, 2026-10-19)
                if (!wasDown) result = (byte)(imp.guard.Dead ? 3 : tally.HeadOf(imp.guard) ? 5 : 2);
                if (imp.guard.TryGetComponent(out Rigidbody grb) && !grb.isKinematic) grb.AddForce(dir * spec.knock, ForceMode.VelocityChange);
            }
            else if (imp.dummy != null)
            {
                if (!imp.dummy.Down) result = 4;
                if (tally.Take(imp.dummy, out _)) imp.dummy.ApplyDamage(BulletDamageRules.Damage(kind, false), np.transform.position);
'@
Rep '        void PlayerShotRpc(byte slot, Vector3 end, byte result, int seed)' @'
        void PlayerShotRpc(byte slot, Vector3 end, byte result, int seed, bool aiming)
'@
Rep 'if (np.TryGetComponent(out GunController gc)) gc.ShowRemoteShot(end, result, seed);' 'if (np.TryGetComponent(out GunController gc)) gc.ShowRemoteShot(end, result, seed, aiming);'
Rep '        /// <summary>Host -> everybody but the shooter: his bullet ended here (see <see cref="GunController.ShowRemoteShot"/> for what the number means).</summary>' @'
        /// <summary>Host -> the shooter alone: what his bullets did to officers (1 a hit, 2 a headshot, 3 an elimination): the hit marker and its sound (<see cref="HitFeedback"/>; the owner, 2026-10-27).</summary>
        [Rpc(SendTo.SpecifiedInParams)]
        void ShotFeedbackRpc(byte kind, RpcParams rp = default)
        {
            if (kind >= 1 && kind <= 3) HitFeedback.Play((HitKind)kind);
        }

        /// <summary>Host -> everybody but the shooter: his bullet ended here (see <see cref="GunController.ShowRemoteShot"/> for what the number means: 5 is a headshot that did not eliminate; <paramref name="aiming"/>: he fired with the gun up at the sights).</summary>
'@
Save-Src
"combat damage patched (sources)"
