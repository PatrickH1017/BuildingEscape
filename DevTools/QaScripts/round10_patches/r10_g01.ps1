# Round 10, batch G01: the combat model. Bullets damage a guard by the gun and his shield and never knock him over; a melee blow knocks him over for the weapon's tier and does not count towards knocking him out;
# a shot guard lets go of the player he pins; guards are made with a loadout (gun + shield tier) by floor; armed guards use every gun kind; a shotgun or sniper hit blasts the player out for the floor; an armed guard drops a clip.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Src = 'C:\bqm\Assets\Scripts'

function NewFile([string]$target, [string]$draft) {
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n").Replace("`n", "`r`n") + "`r`n"
  if ($env:R2_DRY) { Write-Output "dry run: would write $target"; return }
  [IO.File]::WriteAllText($target, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "written $target"
}
function FromDraft([string]$target, [string]$draft) {
  Open-Src $target
  $t = [IO.File]::ReadAllText($draft).Replace("`r`n", "`n").TrimEnd("`n")
  $script:s = $t.Replace("`n", $script:eol) + $script:eol
  Save-Src
}

# ---- the new files and the two rewritten ones
NewFile "$Src\Guns\GuardLoadouts.cs" 'C:\bqs\r10_draft\GuardLoadouts.cs'
NewFile "$Src\Guns\GuardDrops.cs" 'C:\bqs\r10_draft\GuardDrops.cs'
NewFile "$Src\Guns\BlastFx.cs" 'C:\bqs\r10_draft\BlastFx.cs'
FromDraft "$Src\Guns\GuardGun.cs" 'C:\bqs\r10_draft\GuardGun.cs'
FromDraft "$Src\GuardArmour.cs" 'C:\bqs\r10_draft\GuardArmour.cs'

# ---- GuardAI: health, loadout, the three kinds of hit
Open-Src "$Src\GuardAI.cs"
Rep @'
        [Tooltip("Melee hits it takes to knock a guard out for good.")]
        public int hitsToKill = 10;

        /// <summary>Wears body armour (<see cref="GuardRules.Armoured"/>): a bullet counts for half the hits against him; a melee blow is still one hit.</summary>
        public bool Armoured { get; set; }

        /// <summary>Melee hits taken so far.</summary>
        public int Hits { get; private set; }
'@ @'
        [Tooltip("The damage it takes to knock a guard out for good, in hits: a bullet does its gun's guardHits (less through a shield), a melee blow none (it knocks him over).")]
        public int hitsToKill = 10;

        /// <summary>What he carries and wears (<see cref="GuardLoadout"/>): set when he is made (the host) or from the host's snapshot (a puppet); GuardVisual puts the vest and the gun on him.</summary>
        public GuardLoadout Loadout { get; private set; } = GuardLoadout.Unarmed;
        /// <summary>The shield he wears (none, light, medium, heavy): it takes a share of every bullet (<see cref="GuardLoadouts.BulletShare"/>).</summary>
        public ShieldTier Shield => Loadout.Shield;
        /// <summary>Wears a shield of any tier.</summary>
        public bool Armoured => Loadout.Shield != ShieldTier.None;

        /// <summary>Damage taken so far, in hits (a shield takes a share of each bullet, so it need not be a whole number).</summary>
        public float Damage { get; private set; }
        /// <summary>The damage taken so far, in whole hits.</summary>
        public int Hits => Mathf.RoundToInt(Damage);
'@
Rep @'
        /// <summary>Client: take the state the host sent.</summary>
        public void NetApply(GuardState state, bool dead, bool isCalling, float progress, float speed, bool armed = false, bool eatingNow = false)
        {
            State = state; Dead = dead; calling = isCalling; netProgress = progress; netSpeed = speed; eating = eatingNow;
            // the host says whether he carries a gun: this machine shows it (and plays the shots the host announces)
            if (armed && gun == null) gameObject.AddComponent<GuardGun>();
            else if (!armed && gun != null) gun.Remove();
        }
'@ @'
        /// <summary>Client: take the state the host sent (<paramref name="loadoutPacked"/>: his gun's kind and his shield, see <see cref="GuardLoadout.Pack"/>; 0 = not told).</summary>
        public void NetApply(GuardState state, bool dead, bool isCalling, float progress, float speed, bool armed = false, bool eatingNow = false, byte loadoutPacked = 0)
        {
            State = state; Dead = dead; calling = isCalling; netProgress = progress; netSpeed = speed; eating = eatingNow;
            // the host says what he carries and wears: this machine shows it (and plays the shots the host announces)
            var told = loadoutPacked != 0 ? GuardLoadout.Unpack(loadoutPacked, armed) : new GuardLoadout(armed, Loadout.Kind, Loadout.Shield);
            if (told != Loadout) Loadout = told;
            SyncGun();
        }

        /// <summary>The loadout he is made with (host / offline); GuardVisual notices the change and builds the vest and the gun.</summary>
        public void SetLoadout(GuardLoadout loadout) { Loadout = loadout; }

        /// <summary>Makes his gun match his loadout: the right kind in his hand when he is armed, none when he is not (a gun of another kind is taken away and the right one given).</summary>
        public void SyncGun()
        {
            if (Loadout.Armed)
            {
                if (gun != null && gun.kind != Loadout.Kind) gun.Remove();
                if (gun == null || !gun.Armed)
                {
                    var g = gameObject.AddComponent<GuardGun>();
                    g.Configure(Loadout.Kind);
                }
            }
            else if (gun != null) gun.Remove();
        }
'@
Rep @'
        /// <summary>The real effect of a weapon hit: offline, or on the host (after it checked a client's request).</summary>
        public void WeaponHitHost(float stunSeconds, GameObject by = null, int weight = 1)
        {
            if (Dead) return;
            calling = false; callTimer = 0f; // a weapon hit cuts the radio call
            ReleaseHold(true);               // hit while holding someone down: they get free
            int hitsNow = Mathf.Max(1, weight);
            if (Armoured && hitsNow > 1) hitsNow = GuardRules.ArmouredHits(hitsNow);      // body armour: a bullet counts for half (a melee blow is one hit either way)
            Hits += hitsNow;
            if (Hits >= hitsToKill)
            {
                Dead = true;
                State = GuardState.Stunned;
                stunUntil = float.MaxValue;
                agent.isStopped = true;
                Debug.Log($"{name} is down for good after {Hits} hits.");
                if (carriesKey)
                {
                    carriesKey = false;
                    var gen = FindFirstObjectByType<FloorGenerator>();
                    if (gen) gen.DropKey(transform.position + Vector3.up * 0.5f);
                }
                if (IsBoss) HeistManager.Instance?.BossDefeated();
                return;
            }
            Stun(stunSeconds, by);
        }
'@ @'
        /// <summary>
        /// The real effect of a legacy weapon hit: offline, or on the host. It stuns him and counts <paramref name="weight"/> hits (a shield takes no share of it): the tests use it to put a guard down. In the game a melee blow is
        /// <see cref="MeleeHitHost"/> and a bullet <see cref="BulletHitHost"/> (the owner, 2026-10-19: bullets damage, melee knocks over).
        /// </summary>
        public void WeaponHitHost(float stunSeconds, GameObject by = null, int weight = 1)
        {
            if (Dead) return;
            CutRadioAndRelease();
            Damage += Mathf.Max(1, weight);
            if (CheckDown()) return;
            Stun(stunSeconds, by);
        }

        /// <summary>
        /// A melee blow (the owner, 2026-10-19: "Melee weapons knock over a guard one shot (depending on the tier of the weapon determines on how long guard are knocked down)"): he is knocked over for <paramref name="stunSeconds"/>
        /// (the weapon's: bat 3 s, crowbar 4 s, hatchet 6 s), it cuts his radio call and frees a player he holds down, and it does not count towards knocking him out - bullets do that. (Only the boss still takes it as one of his thirty
        /// hits, until his floor is redone.) A client's blow is a request: the host decides.
        /// </summary>
        public void MeleeHit(float stunSeconds, GameObject by = null)
        {
            if (IsPuppet) { Net.RunSync.Instance?.RequestGuardHit(this); return; }
            MeleeHitHost(stunSeconds, by);
        }

        /// <summary>The real effect of a melee blow: offline, or on the host (after it checked a client's request).</summary>
        public void MeleeHitHost(float stunSeconds, GameObject by = null)
        {
            if (Dead) return;
            CutRadioAndRelease();
            if (IsBoss) { Damage += 1f; if (CheckDown()) return; }
            Stun(stunSeconds, by);
        }

        /// <summary>
        /// A bullet (the owner, 2026-10-19: "Bullets should not knock down, bullets should eliminate and depending on the bullet determines how much health a guard loses"): it takes <paramref name="bulletHits"/> hits of his health
        /// (<see cref="hitsToKill"/>; the gun's <c>guardHits</c>) less what his shield takes (<see cref="GuardLoadouts.DamageTaken"/>; a piercing round takes it all), and it never knocks him over. It cuts his radio call and frees a player
        /// he holds down, and a guard who was not after anybody goes to where it came from. Host / offline only (a client's shot is decided by the host).
        /// </summary>
        public void BulletHitHost(float bulletHits, bool piercing = false, GameObject by = null)
        {
            if (Dead) return;
            CutRadioAndRelease();
            Damage += GuardLoadouts.DamageTaken(bulletHits, Shield, piercing);
            if (CheckDown()) return;
            TurnToShooter(by);
        }

        void CutRadioAndRelease()
        {
            calling = false; callTimer = 0f; // a weapon hit cuts the radio call
            ReleaseHold(true);               // hit while holding someone down: they get free
        }

        /// <summary>Has his damage reached his health? Then he is down for good: he drops the key if he has it and the clip of his gun, and the boss's floor is won.</summary>
        bool CheckDown()
        {
            if (Damage < hitsToKill - 0.001f) return false;
            Dead = true;
            State = GuardState.Stunned;
            stunUntil = float.MaxValue;
            agent.isStopped = true;
            Debug.Log($"{name} is down for good after {Damage:0.#} hits.");
            if (carriesKey)
            {
                carriesKey = false;
                var gen = FindFirstObjectByType<FloorGenerator>();
                if (gen) gen.DropKey(transform.position + Vector3.up * 0.5f);
            }
            GuardDrops.OnDown(this);         // (the owner, 2026-10-19: "guards should drop a fair amount of bullets per guard")
            if (IsBoss) HeistManager.Instance?.BossDefeated();
            return true;
        }

        /// <summary>A bullet that did not put him down: when he is not busy with a player (a chase, a knock-over) he goes to where it came from, to look.</summary>
        void TurnToShooter(GameObject by)
        {
            if (!by || IsPuppet || Dead) return;
            if (State == GuardState.Chase || State == GuardState.Stunned) return;
            noiseTarget = by.transform.position;
            State = GuardState.Investigate;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(noiseTarget);
            waitUntil = 0f;
        }
'@
Save-Src

# ---- GuardVisual: the vest of his tier and the gun of his kind
Open-Src "$Src\GuardVisual.cs"
Rep @'
            var hm = HeistManager.Instance;
            // body armour from floor 4 on (the owner, 2026-10-18): a vest on his chest; the host's rules count a bullet for half the hits (GuardAI.Armoured)
            if (!ai.IsBoss && GuardRules.Armoured(hm ? hm.Floor : 0)) { ai.Armoured = true; GuardArmour.AddVest(anim, transform); }
            donut = GuardDonut.Attach(anim);                   // (hidden until he stops for one: GuardAI.Eating)
            // armed guards (the floor rule, or the test menu): the pew in his hand
            if (!GetComponent<GuardGun>() && ArmedGuards.ShouldArm(hm ? hm.Floor : 0)) gameObject.AddComponent<GuardGun>();
        }
'@ @'
            donut = GuardDonut.Attach(anim);                   // (hidden until he stops for one: GuardAI.Eating)
            ApplyLoadout();                                    // the shield on his chest and the gun in his hand: what the host decided (GuardAI.Loadout)
        }

        GuardLoadout applied = GuardLoadout.Unarmed;
        Transform vest;

        /// <summary>Puts on him what his loadout says (the owner, 2026-10-19: guards by floor with a gun and a light / medium / heavy shield or none): the vest of his tier and, when he is armed, the gun of his kind. A puppet gets its loadout from the host's snapshot.</summary>
        void ApplyLoadout()
        {
            var l = ai.Loadout;
            applied = l;
            if (vest) { Destroy(vest.gameObject); vest = null; }
            if (!ai.IsBoss && l.Shield != ShieldTier.None) vest = GuardArmour.AddVest(anim, transform, l.Shield);
            ai.SyncGun();
        }
'@
Rep @'
            if (!anim) return;
            bool eat = ai.Eating && !ai.Dead;
'@ @'
            if (!anim) return;
            if (ai.Loadout != applied) ApplyLoadout();         // (the host told a puppet what he carries, or the test menu armed him)
            bool eat = ai.Eating && !ai.Dead;
'@
Save-Src

# ---- ArmedGuards: the floor ladder
Open-Src "$Src\Guns\ArmedGuards.cs"
Rep @'
        /// <summary>The floor from which guards are armed in the game: 4.</summary>
        public const int DefaultFirstArmedFloor = 4;
'@ @'
        /// <summary>The floor from which guards are armed in the game: 3 (the owner, 2026-10-19: "The first level(3rd floor) of armed guards should have pistols with light shield"; it was 4 until then).</summary>
        public const int DefaultFirstArmedFloor = GuardLoadouts.FirstArmedFloor;
'@
Rep @'
            return ArmedRules.IsArmed(floor, FirstArmedFloor, everywhere);
        }
'@ @'
            return ArmedRules.IsArmed(floor, FirstArmedFloor, everywhere);
        }

        /// <summary>
        /// Is the old rule in force instead of the floors' loadouts (<see cref="GuardLoadouts"/>)? Yes when a test or the test menu changed the first armed floor or armed everybody: then every guard of an armed floor carries a pew with
        /// a light shield, and nothing else.
        /// </summary>
        static bool LegacyRule()
        {
            bool legacy = (Everywhere && !Net.NetSession.Online) || FirstArmedFloor != DefaultFirstArmedFloor;
#if UNITY_EDITOR
            legacy |= ForceForTests;
#endif
            return legacy;
        }

        /// <summary>
        /// The loadouts of the <paramref name="guards"/> patrolling officers of a floor, in the order they are made (host / offline: a remote client's guards are puppets and get theirs from the host). Give it an rng of its own, so
        /// that the maze and the loot stay what they were; the same rng gives the same floor.
        /// </summary>
        public static GuardLoadout[] PlanFor(int floor, int guards, System.Random rng)
        {
            var plan = new GuardLoadout[Mathf.Max(0, guards)];
            if (plan.Length == 0 || Net.NetRole.IsRemoteClient) return plan;               // (all unarmed: the default)
            if (LegacyRule())
            {
                var l = ShouldArm(floor) ? GuardLoadout.Of(RangedWeapon.Kind.Pew) : GuardLoadout.Unarmed;
                for (int i = 0; i < plan.Length; i++) plan[i] = l;
                return plan;
            }
            return GuardLoadouts.Plan(floor, guards, rng);
        }

        /// <summary>One officer outside the plan - backup, or the guard on duty in an outpost: armed with the floor's share and a gun by the floor's weights. <paramref name="rng"/>: use one of its own while the floor is built (every machine builds it), none for a call at run time.</summary>
        public static GuardLoadout RollFor(int floor, System.Random rng = null)
        {
            if (Net.NetRole.IsRemoteClient) return GuardLoadout.Unarmed;
            if (LegacyRule()) return ShouldArm(floor) ? GuardLoadout.Of(RangedWeapon.Kind.Pew) : GuardLoadout.Unarmed;
            return rng != null ? GuardLoadouts.Roll(floor, rng.NextDouble(), rng.NextDouble()) : GuardLoadouts.Roll(floor, Random.value, Random.value);
        }
'@
Rep @'
                if (!g || g.Dead || g.IsPuppet || g.Armed) continue;
                g.gameObject.AddComponent<GuardGun>();
                n++;
'@ @'
                if (!g || g.Dead || g.IsPuppet || g.Armed) continue;
                g.SetLoadout(new GuardLoadout(true, RangedWeapon.Kind.Pew, g.Shield));      // (a pew, and the shield he already wears)
                g.SyncGun();
                n++;
'@
Rep @'
                var gun = g.GetComponent<GuardGun>();
                if (!gun) continue;
                gun.Remove();
                n++;
'@ @'
                var gun = g.GetComponent<GuardGun>();
                if (!gun) continue;
                g.SetLoadout(new GuardLoadout(false, RangedWeapon.Kind.Pew, g.Shield));
                g.SyncGun();                                           // (takes the gun away)
                n++;
'@
Save-Src

# ---- GuardRules: the vest rule of 2026-10-18 is replaced by the shield tiers
Open-Src "$Src\GuardRules.cs"
Rep @'
        /// <summary>The first floor whose guards wear body armour (the owner, 2026-10-18: "Wave 4 guards should have body armour"): a bullet counts for half the hits against them (the pew's three bullets become five).</summary>
        public const int ArmourFirstFloor = 4;

        /// <summary>Do the guards of this floor wear body armour? (Not the boss: he has his own toughness.)</summary>
        public static bool Armoured(int floor) => floor >= ArmourFirstFloor && !TowerRules.IsBoss(floor);

        /// <summary>The hits a bullet of <paramref name="bulletHits"/> hits counts for against a guard in body armour.</summary>
        public static int ArmouredHits(int bulletHits) => System.Math.Max(1, bulletHits / 2);

'@ @'
'@
Save-Src

# ---- FloorGenerator: who carries what
Open-Src "$Src\FloorGenerator.cs"
Rep @'
            NoteBaseSeed(floor, seed);
'@ @'
            NoteBaseSeed(floor, seed);
            planSeed = seed;
'@
Rep @'
            int count = Mathf.Min(maxGuards, GuardRules.PatrolGuards(floor, baseGuards));
            int placed = 0;
'@ @'
            int count = Mathf.Min(maxGuards, GuardRules.PatrolGuards(floor, baseGuards));
            int placed = 0;
            var plan = ArmedGuards.PlanFor(floor, count, new System.Random(unchecked(planSeed * 7919 + floor * 31 + 5)));      // (who carries what: its own random numbers, so the maze and the loot stay what they were)
'@
Rep @'
                var made = SpawnGuard($"Guard_{g}", route, g + floor); // a different guard look for each guard (and mix per floor)
'@ @'
                var made = SpawnGuard($"Guard_{g}", route, g + floor, plan[g]); // a different guard look for each guard (and mix per floor)
'@
Rep @'
        GuardAI SpawnGuard(string name, List<Vector3> route, int look)
        {
'@ @'
        int planSeed;
        int FloorNow => HeistManager.Instance ? HeistManager.Instance.Floor : 0;

        /// <param name="loadout">What he carries and wears (<see cref="GuardLoadouts"/>); the default is nothing (a remote client's guard is told by the host).</param>
        GuardAI SpawnGuard(string name, List<Vector3> route, int look, GuardLoadout loadout = default)
        {
'@
Rep @'
            ai.chaseSpeed = guardChaseSpeed;
            go.AddComponent<GuardVisual>().lookIndex = look;
'@ @'
            ai.chaseSpeed = guardChaseSpeed;
            ai.SetLoadout(loadout);
            go.AddComponent<GuardVisual>().lookIndex = look;
'@
Rep @'
                var ai = SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { p }, look);
'@ @'
                var ai = SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { p }, look, ArmedGuards.RollFor(FloorNow));
'@
Rep @'
                var ai = SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { best }, look);
'@ @'
                var ai = SpawnGuard($"Backup_{reinforcementCount++}", new List<Vector3> { best }, look, ArmedGuards.RollFor(FloorNow));
'@
Save-Src

Open-Src "$Src\FloorGenerator.Outpost.cs"
Rep @'
            var ai = SpawnGuard("Guard_Outpost", new List<Vector3> { hit.position }, floor * 5 + 2);
'@ @'
            var ai = SpawnGuard("Guard_Outpost", new List<Vector3> { hit.position }, floor * 5 + 2, ArmedGuards.RollFor(floor, new System.Random(unchecked(planSeed * 131 + floor * 17 + 3))));
'@
Save-Src

# ---- the host's snapshot carries the loadout (the kind byte was never read)
Open-Src "$Src\Net\RunSync.Snapshots.cs"
Rep @'
            public Vector3 pos; public Quaternion rot;
            public byte state, call, flags;
            public float time;
'@ @'
            public Vector3 pos; public Quaternion rot;
            public byte kind, state, call, flags;
            public float time;
'@
Rep @'
                    kind = 1;
                    state = (byte)g.State;
'@ @'
                    kind = g.Loadout.Pack();                                    // (a guard: bit 0 set; his shield and the kind of gun he carries ride in the other bits)
                    state = (byte)g.State;
'@
Rep @'
                                   || state != s.state || flags != s.flags || call != s.call;
'@ @'
                                   || state != s.state || flags != s.flags || call != s.call || kind != s.kind;
'@
Rep @'
                sent[kv.Key] = new Sent { pos = pos, rot = rot, state = state, call = call, flags = flags, time = now };
'@ @'
                sent[kv.Key] = new Sent { pos = pos, rot = rot, kind = kind, state = state, call = call, flags = flags, time = now };
'@
Rep @'
                if (c is GuardAI g) g.NetApply((GuardState)state, (flags & 1) != 0, (flags & 2) != 0, call / 255f, speed / 20f, (flags & 4) != 0, (flags & 8) != 0);
'@ @'
                if (c is GuardAI g) g.NetApply((GuardState)state, (flags & 1) != 0, (flags & 2) != 0, call / 255f, speed / 20f, (flags & 4) != 0, (flags & 8) != 0, kind);
'@
Save-Src

# ---- two more things the host tells the clients: a guard's clip, a blast
Open-Src "$Src\Net\RunSync.Events.cs"
Rep @'
        public void BroadcastToast(string text)
'@ @'
        /// <summary>Host: an armed guard went down and dropped a clip of his gun's ammunition (<see cref="GuardDrops"/>); the clients make the same one, with this network id.</summary>
        public void BroadcastSpawnClip(int id, Vector3 pos, byte kind, byte rounds)
        {
            if (IsServer && NetworkManager.ConnectedClientsIds.Count > 1) SpawnClipRpc((ushort)id, pos, kind, rounds);
        }

        [Rpc(SendTo.NotServer)]
        void SpawnClipRpc(ushort id, Vector3 pos, byte kind, byte rounds)
        {
            if (!IsFinite(pos) || kind >= RangedWeapon.Specs.Length) return;
            GuardDrops.SpawnFromNet(id, pos, (RangedWeapon.Kind)kind, rounds);
        }

        /// <summary>Host: a shotgun or a sniper rifle blew this player up (<see cref="HeistManager.Blast"/>); every other machine plays the blast. <paramref name="slot"/> -1: the offline player.</summary>
        public void BroadcastBlast(int slot, Vector3 pos)
        {
            if (IsServer && NetworkManager.ConnectedClientsIds.Count > 1) BlastRpc((sbyte)slot, pos);
        }

        [Rpc(SendTo.NotServer)]
        void BlastRpc(sbyte slot, Vector3 pos)
        {
            if (!IsFinite(pos)) return;
            BlastFx.Play(pos, HudTheme.PlayerColour(slot + 1));
        }

        public void BroadcastToast(string text)
'@
Save-Src

Open-Src "$Src\Net\RunSync.Requests.cs"
Rep @'
            else g.WeaponHitHost(weapon.Data.stun, np.gameObject);
'@ @'
            else g.MeleeHitHost(weapon.Data.stun, np.gameObject);
'@
Save-Src

Open-Src "$Src\Net\RunSync.Guns.cs"
Rep @'
                imp.guard.WeaponHitHost(spec.stun, np.gameObject, spec.guardHits);
'@ @'
                imp.guard.BulletHitHost(spec.guardHits, false, np.gameObject);        // (a bullet damages, it never knocks over: the owner, 2026-10-19)
'@
Save-Src

Open-Src "$Src\Guns\GunController.cs"
Rep @'
                imp.guard.WeaponHit(spec.stun, gameObject, spec.guardHits);
'@ @'
                imp.guard.BulletHitHost(spec.guardHits, false, gameObject);          // (a bullet damages, it never knocks over: the owner, 2026-10-19)
'@
Save-Src

Open-Src "$Src\MeleeController.cs"
Rep @'
                    guard.WeaponHit(spec.stun, gameObject);
'@ @'
                    guard.MeleeHit(spec.stun, gameObject);                           // (a blow knocks over for the weapon's tier and does not count towards knocking him out: the owner, 2026-10-19)
'@
Save-Src

# ---- HeistManager: the blast
Open-Src "$Src\HeistManager.cs"
Rep @'
        /// <summary>The pin ran out: the player is arrested. They stay busted until the vendor gets them out.</summary>
'@ @'
        /// <summary>
        /// A shotgun or a sniper rifle hit this player (<see cref="GuardLoadouts.Eliminates"/>; the owner, 2026-10-19: "they should explode in a manner with reference to R.E.P.O"): their floor is over. They are busted at once - knocked
        /// out for the floor, they spectate their teammates, and the vendor buys them back at the Loading Docks - and the blast is shown on every machine (<see cref="BlastFx"/>). Host / offline. False when it could not happen (not in a run,
        /// god mode, already out): the hit is then only a knock-down.
        /// </summary>
        public bool Blast(WobblyPlayer p, GuardAI by)
        {
            if (Phase != HeistPhase.Heist || godMode || !p || !Net.NetRole.Simulates) return false;
            if (p.Custody == CustodyState.Busted) return false;
            var holder = p.PinnedBy;
            SetCustody(p, CustodyState.Busted, 0f, Floor);
            if (holder) holder.ReleaseHold();
            var np = p.GetComponent<Net.NetPlayer>();
            int slot = np && np.IsSpawned ? np.Slot : -1;
            var at = p.transform.position + Vector3.up * 0.8f;
            BlastFx.Play(at, HudTheme.PlayerColour(slot + 1));
            Net.RunSync.Instance?.BroadcastBlast(slot, at);
            var away = by ? p.transform.position - by.transform.position : Vector3.back;
            away.y = 0f;
            p.Stumble((away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back) * 6f + Vector3.up * 5f, 2f);       // (the body is thrown clear)
            Announce($"{Who(p, "was", "were")} blown up - buy them back from the vendor at the Loading Docks");
            CheckTeamDown();
            return true;
        }

        /// <summary>The pin ran out: the player is arrested. They stay busted until the vendor gets them out.</summary>
'@
Save-Src

# ---- the pew found in a safe holds one clip, like every gun found (guards drop bullets now)
Open-Src "$Src\Outpost\FloorSafe.cs"
Rep @'
kind == RangedWeapon.Kind.Pew ? -1 : 0);      // (the barrel is the gun's +X: turned by -90 degrees it points out of the door; a gun found here holds its one clip and has no spare - the pew keeps its two for now)
'@ @'
0);      // (the barrel is the gun's +X: turned by -90 degrees it points out of the door; a gun found here holds its one clip and has no spare, the pew too since the guards drop bullets)
'@
Save-Src

# ---- the protocol: officers with every kind of gun and shield, a clip drop, a blast
Open-Src "$Src\Net\NetConst.cs"
Rep @'
public const string ProtocolVersion = "21";   // 21: the guns of the pack
'@ @'
public const string ProtocolVersion = "22";   // 22: officers carry guns of every kind and wear shields (the host streams both with each guard), bullets damage and never knock a guard over, a guard drops a clip (a pickup with a network id) and a shotgun or sniper hit blows a player up (announced); 21: the guns of the pack
'@
Save-Src

# ---- the built-player autopilot: floor 3 has armed guards now, but not all of them
Open-Src "$Src\QaAutopilot.cs"
Rep @'
                    Check($"the guards of floor {floor} are armed", GuardAI.All.Count(g => g && g.Armed) >= 3, $"({GuardAI.All.Count(g => g && g.Armed)} armed)");
'@ @'
                    Check($"some guards of floor {floor} are armed (and not all of them)", GuardAI.All.Count(g => g && g.Armed) >= 1 && GuardAI.All.Count(g => g && !g.Armed) >= 1, $"({GuardAI.All.Count(g => g && g.Armed)} armed of {GuardAI.All.Count(g => g)})");
'@
Save-Src
