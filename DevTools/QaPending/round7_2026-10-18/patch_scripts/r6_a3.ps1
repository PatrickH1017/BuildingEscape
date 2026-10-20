# Round 6, batch A3 (the owner's task list of 2026-10-18): (2) punching with empty hands, (6) teammates hear each other's melee swings, punches and hits when they are near. New files: PunchRules.cs, the rewritten
# MeleeController.cs (copy_new.ps1). Needs batch A2 applied first (the checks go before the intro block of Round6Test and before the last line of the R6 scenario). BE_qa (= C:\bq) only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# ---------------------------------------------------------------- the fist's kind, the guard's reaction
Open-Src (Join-Path $Dir 'Scripts\MeleeWeapon.cs')
Rep '        public enum Kind { Crowbar, Bat, Hatchet }' '        public enum Kind { Crowbar, Bat, Hatchet }

        /// <summary>What the network calls a punch (a weapon is its <see cref="Kind"/>, 0-2).</summary>
        public const byte PunchKind = 255;'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\GuardAI.cs')
Rep '        public void Stun(float seconds, GameObject by = null)' '        /// <summary>A punch landed (<see cref="PunchRules"/>): the host decides what it does (a client''s guard is a puppet and asks it to).</summary>
        public void PunchHit(float stunSeconds, GameObject by = null)
        {
            if (IsPuppet) { Net.RunSync.Instance?.RequestGuardHit(this); return; }
            PunchHost(stunSeconds, by);
        }

        /// <summary>
        /// The real effect of a punch (offline, or on the host after it checked a client''s request): the radio call is cut, a player he holds down gets free and he is stunned for a moment, but the blow does not count towards
        /// knocking him out (only weapons and bullets do).
        /// </summary>
        public void PunchHost(float stunSeconds, GameObject by = null)
        {
            if (Dead) return;
            calling = false; callTimer = 0f;
            ReleaseHold(true);
            Stun(stunSeconds, by);
        }

        public void Stun(float seconds, GameObject by = null)'
Save-Src

# ---------------------------------------------------------------- the sounds
Open-Src (Join-Path $Dir 'Scripts\GameAudio.cs')
Rep '        public static void Whoosh(Vector3 pos)
        {
            var c = Catalog; if (c == null) return;
            Play(Pick(c.whoosh), pos, 0.6f, Random.Range(0.95f, 1.1f), false);
        }' '        public static void Whoosh(Vector3 pos)
        {
            var c = Catalog; if (c == null) return;
            Play(Pick(c.whoosh), pos, 0.6f, Random.Range(0.95f, 1.1f), false);
        }

        /// <summary>The arm of a punch going through the air: a quicker, quieter whoosh.</summary>
        public static void PunchWhoosh(Vector3 pos)
        {
            var c = Catalog; if (c == null) return;
            Play(Pick(c.whoosh), pos, 0.4f, Random.Range(1.2f, 1.35f), false);
        }

        /// <summary>A teammate''s swing or punch, heard from where he stands: a spatial sound, so only a teammate who is near is heard (the owner, 2026-10-18: "Players should hear teammates hits including melee if nearby").</summary>
        public static void RemoteWhoosh(Vector3 pos, bool punch)
        {
            var c = Catalog; if (c == null) return;
            Play(Pick(c.whoosh), pos, punch ? 0.4f : 0.6f, punch ? Random.Range(1.2f, 1.35f) : Random.Range(0.95f, 1.1f), true, 22f, 3f);
        }

        /// <summary>A fist landing: a dull thump.</summary>
        public static void PunchHit(Vector3 pos) => PlaySound(SoundId.Punch, pos, 1f, Random.Range(0.92f, 1.08f), true, 24f, 3f);

        /// <summary>The sound of a blow by what threw it: <paramref name="kind"/> 0-2 is a weapon (<see cref="MeleeWeapon.Kind"/>), <see cref="MeleeWeapon.PunchKind"/> a fist.</summary>
        public static void MeleeHitAt(byte kind, bool killed, Vector3 pos)
        {
            if (kind == MeleeWeapon.PunchKind) PunchHit(pos);
            else if (kind <= 2) MeleeHit((MeleeWeapon.Kind)kind, killed, pos);
        }'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Audio\SoundBank.cs')
Rep 'AlarmOff, MetalLock }' 'AlarmOff, MetalLock, Punch }'
Rep 'OutpostSound.KeypadVariants, 1, 1, 1, 1, 1, OutpostSound.PryVariants, 1, 1, 1, 1,' 'OutpostSound.KeypadVariants, 1, 1, 1, 1, 1, OutpostSound.PryVariants, 1, 1, 1, 1, OutpostSound.PunchVariants,'
Rep '                case SoundId.MetalLock: return OutpostSound.MetalLock();' '                case SoundId.MetalLock: return OutpostSound.MetalLock();
                case SoundId.Punch: return OutpostSound.Punch(variant);'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Audio\OutpostSound.cs')
Rep '        static void Clunk(float[] o, int at, float gain, float hz, SynthRandom r)' '        public const int PunchVariants = 3;

        /// <summary>A fist landing on a body: a low thump with a short dull slap on top; three variants, a little different in pitch.</summary>
        public static float[] Punch(int variant)
        {
            var r = new SynthRandom(2301 + variant * 17);
            var o = new float[Dsp.Samples(0.28f)];
            Clunk(o, 0, 0.95f, 72f + 14f * variant, r);
            var lp = new Svf();
            lp.Set(1800f - 250f * variant, 0.8f);
            float dec = Dsp.DecayPerSample(0.03f), env = 1f;
            for (int i = 0; i < Dsp.Samples(0.06f) && i < o.Length; i++) { o[i] += lp.Low(r.Signed()) * env * 0.8f; env *= dec; }
            Dsp.FadeEnds(o, 0.004f);
            Dsp.Normalize(o, 0.9f);
            return o;
        }

        static void Clunk(float[] o, int at, float gain, float hz, SynthRandom r)'
Save-Src

# ---------------------------------------------------------------- the network
Open-Src (Join-Path $Dir 'Scripts\Net\RunSync.Requests.cs')
Rep '            var weapon = bp && bp.Equipped ? bp.Equipped.GetComponent<MeleeWeapon>() : null;
            if (weapon == null) return;
            if (!Near(np.transform, g.transform, GuardHitReach)) return;
            float now = Time.unscaledTime;
            if (lastHit.TryGetValue(who, out float t) && now - t < 0.35f) return; // faster than any swing
            lastHit[who] = now;
            g.WeaponHitHost(weapon.Data.stun, np.gameObject);' '            var weapon = bp && bp.Equipped ? bp.Equipped.GetComponent<MeleeWeapon>() : null;
            bool punch = weapon == null && !(bp && bp.Equipped);              // (empty hands: a punch)
            if (weapon == null && !punch) return;
            if (!Near(np.transform, g.transform, GuardHitReach)) return;
            float now = Time.unscaledTime;
            if (lastHit.TryGetValue(who, out float t) && now - t < 0.35f) return; // faster than any swing
            lastHit[who] = now;
            if (punch) g.PunchHost(PunchRules.StunSeconds, np.gameObject);
            else g.WeaponHitHost(weapon.Data.stun, np.gameObject);'
Rep '        public void RequestSwing()
        {
            if (IsServer) HostSwing(Me); else SwingRequestRpc();
        }

        [Rpc(SendTo.Server)]
        void SwingRequestRpc(RpcParams rp = default) => HostSwing(rp.Receive.SenderClientId);

        void HostSwing(ulong who)
        {
            var np = PlayerOf(who);
            if (np != null) SwungRpc((byte)np.Slot);
        }' '        /// <summary>This player swung a weapon (<paramref name="kind"/> 0-2) or threw a punch (<see cref="MeleeWeapon.PunchKind"/>): the teammates see it and hear it when they are near.</summary>
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
        }'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Net\RunSync.Events.cs')
Rep '        [Rpc(SendTo.ClientsAndHost)]
        void SwungRpc(byte slot)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && !np.IsOwner && np.TryGetComponent(out MeleeController melee)) melee.PlayRemoteSwing();
        }' '        [Rpc(SendTo.ClientsAndHost)]
        void SwungRpc(byte slot, byte kind)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && !np.IsOwner && np.TryGetComponent(out MeleeController melee)) melee.PlayRemoteSwing(kind);
        }

        [Rpc(SendTo.ClientsAndHost)]
        void MeleeHitRpc(byte slot, byte kind, bool killed, Vector3 at)
        {
            var np = NetPlayer.BySlot(slot);
            if (np && !np.IsOwner && np.TryGetComponent(out MeleeController melee)) melee.PlayRemoteHit(kind, killed, at);
        }'
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Net\NetConst.cs')
Rep 'ProtocolVersion = "15";   // 15: ' 'ProtocolVersion = "16";   // 16: punching, and a teammate''s swings, punches and hits are heard when he is near (new messages); 15: '
Save-Src
Open-Src (Join-Path $Dir 'Scripts\Hud\KeybindScreen.cs')
Rep 'new Entry("LMB", "throw / swing weapon / fire")' 'new Entry("LMB", "throw / swing weapon / fire / punch")'
Save-Src

# ---------------------------------------------------------------- the checks
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR6Test.cs')
Rep '            // ---- the intro: the filled bar saves the vote' '            // ---- punching: weaker than any weapon, only with empty hands
            float minStun = 99f, minCool = 99f, minDamage = 99f, minNoise = 99f;
            foreach (var w in MeleeWeapon.Specs) { minStun = Mathf.Min(minStun, w.stun); minCool = Mathf.Min(minCool, w.cooldown); minDamage = Mathf.Min(minDamage, w.damage); minNoise = Mathf.Min(minNoise, w.noise); }
            Check("punch: it stuns less than any weapon, hits for less, makes less noise and comes quicker than the quickest weapon", PunchRules.StunSeconds < minStun && PunchRules.Damage < minDamage && PunchRules.Noise < minNoise && PunchRules.Cooldown < minCool);
            Check("punch: a miss is quieter than a hit, the zone is smaller than a weapon''s (1.4 m)", PunchRules.MissNoise < PunchRules.Noise && PunchRules.Radius < 1.4f && PunchRules.HitDelay < 0.22f);
            Check("punch: allowed with empty hands, no menu, free, not busy and the cooldown over", PunchRules.CanPunch(true, true, false, false, false, 5f, 5f));
            Check("punch: not with something in the hands (a weapon, a gun, a thing to throw) or carrying a heavy thing", !PunchRules.CanPunch(false, true, false, false, false, 5f, 0f) && !PunchRules.CanPunch(true, false, false, false, false, 5f, 0f));
            Check("punch: not with a menu up, not pinned or busted, not in the middle of a blow, not before the cooldown is over", !PunchRules.CanPunch(true, true, true, false, false, 5f, 0f) && !PunchRules.CanPunch(true, true, false, true, false, 5f, 0f) && !PunchRules.CanPunch(true, true, false, false, true, 5f, 0f) && !PunchRules.CanPunch(true, true, false, false, false, 4.9f, 5f));
            Check("punch: it has its own sound (three variants, short, audible)", SoundBank.Variants(SoundId.Punch) == 3 && PunchSoundOk());
            Check("punch: a fist has its own kind on the network and no weapon uses it", MeleeWeapon.PunchKind == 255 && System.Enum.GetValues(typeof(MeleeWeapon.Kind)).Length <= 3);

            // ---- the intro: the filled bar saves the vote'
Rep '        public static void Round6Test()
        {' '        static bool PunchSoundOk()
        {
            for (int v = 0; v < 3; v++)
            {
                var data = OutpostSound.Punch(v);
                float peak = 0f; foreach (var x in data) peak = Mathf.Max(peak, Mathf.Abs(x));
                if (data.Length < Dsp.Samples(0.1f) || data.Length > Dsp.Samples(0.5f) || peak < 0.3f || peak > 1.001f) return false;
            }
            return true;
        }

        public static void Round6Test()
        {'
Save-Src
Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR6.cs')
Rep '            Log($"[R6] DONE fails={r6Fails}");' '            // ---- (2) punching: a punch stuns an officer for a moment but does not count towards knocking him out
            {
                var melee = player.GetComponent<MeleeController>();
                var pack = player.GetComponent<Backpack>();
                R6Check("the player can punch: a melee controller and nothing equipped", melee != null && pack != null && !pack.Equipped);
                var agent = guard.GetComponent<UnityEngine.AI.NavMeshAgent>();
                bool placed = false;
                var home = guard.transform.position;
                for (int k = 0; k < 8 && !placed && melee != null && agent != null; k++)
                {
                    player.TeleportTo(home + Vector3.up * 0.1f, Quaternion.Euler(0f, k * 45f, 0f));
                    yield return new WaitForSeconds(0.2f);
                    var ahead = Crosshair.SwingForward(player.transform); ahead.y = 0f;
                    var spot = player.transform.position + ahead.normalized * PunchRules.Reach; spot.y = home.y;
                    placed = UnityEngine.AI.NavMesh.SamplePosition(spot, out var navHit, 0.4f, UnityEngine.AI.NavMesh.AllAreas) && agent.Warp(navHit.position);
                }
                R6Check("an officer was put right in front of the fists", placed);
                if (placed)
                {
                    yield return new WaitForSeconds(0.3f);
                    int before = guard.Hits;
                    bool thrown = melee.TryPunch();
                    R6Check("a punch is thrown with empty hands", thrown && melee.PunchesThrown == 1);
                    R6Check("...and a second one at once is not (the blow is not over, the cooldown)", !melee.TryPunch());
                    yield return new WaitForSeconds(0.7f);
                    R6Check("it landed: the officer is stunned, and the blow did not count towards knocking him out", melee.PunchesLanded >= 1 && guard.State == GuardState.Stunned && guard.Hits == before && !guard.Dead, $"(state {guard.State}, hits {before} -> {guard.Hits}, landed {melee.PunchesLanded})");
                    yield return new WaitForSeconds(PunchRules.StunSeconds + 0.8f);
                    R6Check("a moment later he is up again", guard.State != GuardState.Stunned && !guard.Dead, $"({guard.State})");
                    R6Check("another punch can be thrown after the cooldown", melee.TryPunch());
                    yield return new WaitForSeconds(0.8f);
                }
                // what teammates hear: the remote calls count (the sound itself falls silent with the distance)
                int swings0 = melee.RemoteSwingsHeard, hits0 = melee.RemoteHitsHeard;
                melee.PlayRemoteSwing(MeleeWeapon.PunchKind); melee.PlayRemoteSwing(0);
                melee.PlayRemoteHit(MeleeWeapon.PunchKind, false, player.transform.position); melee.PlayRemoteHit(1, true, player.transform.position);
                R6Check("a teammate''s swings and hits are played here (the remote calls ran: 2 swings, 2 hits)", melee.RemoteSwingsHeard == swings0 + 2 && melee.RemoteHitsHeard == hits0 + 2);
            }

            Log($"[R6] DONE fails={r6Fails}");'
Save-Src
Write-Output 'round 6 batch A3 patched'
