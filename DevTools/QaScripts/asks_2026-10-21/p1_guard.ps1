# 2026-10-21, the owner's list: (1) an officer who is shot starts calling for backup, and killing him in time cancels the call; (3) a dead officer's body can be walked through.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\GuardAI.cs"
if ($s.Contains('BeginShotCall')) { "GuardAI already patched"; return }

Rep '        /// <summary>True while the guard is radioing for backup. Only a weapon hit stops it.</summary>' '        /// <summary>True while the guard is radioing for backup. A melee blow or a punch cuts it, so does being put down for good; a bullet does not (it starts one: <see cref="BeginShotCall"/>).</summary>'

Rep @'
        /// (<see cref="hitsToKill"/>; the gun's <c>guardHits</c>) less what his shield takes (<see cref="GuardLoadouts.DamageTaken"/>; a piercing round takes it all), and it never knocks him over. It cuts his radio call and frees a player
        /// he holds down, and a guard who was not after anybody goes to where it came from. Host / offline only (a client's shot is decided by the host).
'@ @'
        /// (<see cref="hitsToKill"/>; the gun's <c>guardHits</c>) less what his shield takes (<see cref="GuardLoadouts.DamageTaken"/>; a piercing round takes it all), and it never knocks him over. It frees a player he holds down. A shot officer who is still on his feet
        /// starts to radio for backup (the owner, 2026-10-21: "officers should start calling for backup after being shot and if a player eliminates them in time the call will be cancelled"): see <see cref="BeginShotCall"/>; the bullet that puts him down
        /// cancels the call (<see cref="CheckDown"/>). Host / offline only (a client's shot is decided by the host).
'@

Rep @'
            if (Dead) return;
            CutRadioAndRelease();
            Damage += GuardLoadouts.DamageTaken(bulletHits, Shield, piercing);
            if (CheckDown()) return;
            TurnToShooter(by);
'@ @'
            if (Dead) return;
            ReleaseHold(true);                       // (hit while holding somebody down: they get free; a radio call he has going is NOT cut by a bullet any more)
            Damage += GuardLoadouts.DamageTaken(bulletHits, Shield, piercing);
            if (CheckDown()) return;                 // (down for good: the call he had going is cancelled with him)
            if (BeginShotCall(by)) return;           // (he stands and radios for backup: no need to send him to look at where the shot came from)
            TurnToShooter(by);
'@

Rep @'
            if (Damage < hitsToKill - 0.001f) return false;
            Dead = true;
            State = GuardState.Stunned;
'@ @'
            if (Damage < hitsToKill - 0.001f) return false;
            Dead = true;
            calling = false; callTimer = 0f; callAnyway = false; callOnGetUp = false;      // (eliminated before his call was through: no backup, the ring over his head goes - the owner, 2026-10-21)
            State = GuardState.Stunned;
'@

Rep @'
        /// <summary>A punch landed (<see cref="PunchRules"/>): the host decides what it does (a client's guard is a puppet and asks it to).</summary>
'@ @'
        /// <summary>
        /// A bullet that did not put him down starts a radio call for backup: he stops where he is and radios for <see cref="callSeconds"/> (the same ring over his head as a sighting), whether or not he sees anybody; when the call is through the
        /// officers are sent and he goes after whoever shot him (<see cref="AfterGetUpCall"/>). Putting him down before that cancels the call (<see cref="CheckDown"/>); a melee blow or a punch cuts it. A bullet that hits a guard who is already radioing
        /// neither starts it over nor stops it. A guard who is after somebody already (a chase begins with a call, so his backup is on the way), one who is knocked over (he radios on getting up), the boss and one who is not on the floor start no call.
        /// True when he is radioing now (the caller then does not send him to look at where the shot came from).
        /// </summary>
        bool BeginShotCall(GameObject by)
        {
            if (Dead || IsBoss || IsPuppet) return false;
            var shooter = by ? by.GetComponentInParent<WobblyPlayer>() : null;
            if (calling)
            {
                callAnyway = true;                                       // (shot while he watches somebody: the call no longer needs them in view)
                if (shooter) { hitBy = shooter; target = shooter; lastSeenPos = shooter.transform.position; }
                if (by) noiseTarget = by.transform.position;
                return true;
            }
            if (State == GuardState.Chase || State == GuardState.Stunned) return false;
            if (!agent || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
            hitBy = shooter;
            target = shooter;
            noiseTarget = by ? by.transform.position : transform.position;
            if (shooter) lastSeenPos = shooter.transform.position;
            calling = true; callTimer = 0f; unseenFor = 0f; callAnyway = true;
            eating = false;
            State = GuardState.Calling;
            agent.isStopped = true;
            Debug.Log($"{name} was shot and is calling for backup");
            return true;
        }

        /// <summary>A punch landed (<see cref="PunchRules"/>): the host decides what it does (a client's guard is a puppet and asks it to).</summary>
'@

Rep @'
        void LateUpdate()
        {
            if (Dead && !IsBoss && corpse == null) corpse = gameObject.AddComponent<GuardCorpse>();
        }
'@ @'
        bool bodySettled;

        /// <summary>
        /// A guard who is down for good lies where he fell and nobody has to walk round him (the owner, 2026-10-21: "A eliminated officer should remain on the ground but can now be walked through"): every collider of his body goes off - the standing
        /// capsule would stay in the way as an invisible pillar, and soak up the bullets and blows meant for somebody behind him - and the body stays where it lies until <see cref="GuardCorpse"/> takes it away.
        /// Every machine does it by itself from the flag it has (the host's from the blow, a puppet's from the snapshot).
        /// </summary>
        void SettleBody()
        {
            bodySettled = true;
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c) c.enabled = false;
        }

        void LateUpdate()
        {
            if (!Dead) return;
            if (!bodySettled) SettleBody();
            if (!IsBoss && corpse == null) corpse = gameObject.AddComponent<GuardCorpse>();
        }
'@
Save-Src
"p1 done"
