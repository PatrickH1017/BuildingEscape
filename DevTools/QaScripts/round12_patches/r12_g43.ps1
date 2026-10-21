# Round 12, batch G43 (the owner, 2026-10-20): "I added Blink animations, please use the animation in the attached image as the new stunned animations for officers rather than falling down"
# (the picture: two characters standing hunched, head down, arms hanging = the Blink pack's StunnedLoop).
#  (1) the clip set gets a `stunned` clip and the character catalog builder fills it (one Humanoid clip for every rig) from Assets/Blink/.../Combat/StunnedLoop.fbx;
#  (2) CharacterAnimator (clip mode) gets a dazed slot and Stun(): the loop plays, standing; Recover() snaps out of it (a rig without the clip falls down as before);
#  (3) GuardVisual: a stunned officer plays the dazed loop (puppets too: the state is streamed); players still fall (PlayerVisual is untouched);
#  (4) tests: the Toon line-up checks the loop (weights + picture), GuardCall checks a real knocked-over officer.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor'

Open-Src "$A\CharacterCatalog.cs"
Rep '        public AnimationClip pistolIdle, pistolRun, pistolShoot;' @'
        public AnimationClip pistolIdle, pistolRun, pistolShoot;
        /// <summary>The dazed loop (Blink's StunnedLoop: standing hunched, head down, arms hanging): what a stunned officer plays instead of falling down (the owner, 2026-10-20). Empty = he falls down as before.</summary>
        public AnimationClip stunned;
'@
Save-Src

Open-Src "$E\CharacterCatalogBuilder.cs"
Rep 'if (!cat || cat.characters.Length == 0 || !cat.atlas || !cat.male.idle || HasFileNameLabels(cat)) Build(false);' 'if (!cat || cat.characters.Length == 0 || !cat.atlas || !cat.male.idle || HasFileNameLabels(cat) || StunnedMissing(cat)) Build(false);'
Rep '        [MenuItem("Employees Take Revenge/Setup Characters", priority = 32)]' @'
        const string StunnedFbx = "Assets/Blink/Art/Animations/Animations_Starter_Pack/Combat/StunnedLoop.fbx";

        /// <summary>The Blink dazed loop (the owner, 2026-10-20: stunned officers use it instead of falling down): its clip in the FBX (the file also holds a one-frame T pose), or null while the pack is absent.</summary>
        static AnimationClip ReadStunned()
        {
            if (!File.Exists(StunnedFbx)) return null;
            return AssetDatabase.LoadAllAssetsAtPath(StunnedFbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == "StunnedLoop");
        }

        /// <summary>The Blink pack is in and the catalog has no dazed loop yet: it is rebuilt on the next editor start.</summary>
        static bool StunnedMissing(CharacterCatalog cat) => File.Exists(StunnedFbx) && cat.humanMale != null && !cat.humanMale.stunned;

        [MenuItem("Employees Take Revenge/Setup Characters", priority = 32)]
'@
Rep '            cat.humanFemale = ReadHuman("F");' @'
            cat.humanFemale = ReadHuman("F");
            var stunned = ReadStunned();
            foreach (var set in new[] { cat.male, cat.female, cat.zombie, cat.humanMale, cat.humanFemale }) set.stunned = stunned;          // (one clip for every rig: it is Humanoid)
'@
Save-Src

Open-Src "$A\CharacterAnimator.cs"
Rep 'enum Pose { Idle, Run, Down }' 'enum Pose { Idle, Run, Down, Dazed }'
Rep 'if (!anim || pose == Pose.Down) return;' 'if (!anim || pose == Pose.Down || pose == Pose.Dazed) return;' 2
Rep '        /// <summary>Back on their feet (does nothing unless they were down).</summary>' @'
        /// <summary>Stunned the way officers are (the owner, 2026-10-20: "use the animation ... as the new stunned animations for officers rather than falling down"): a rig that has the dazed loop stands hunched with its head down and its arms hanging; any other rig falls down as <see cref="Fall"/> does.</summary>
        public void Stun()
        {
            if (!anim) return;
            if (!clipMode || !hasStun) { Fall(); return; }
            if (pose == Pose.Dazed) return;
            pose = Pose.Dazed;
            gettingUp = false;
            play[(int)Slot.Down].SetSpeed(0);
            var loop = play[(int)Slot.Stun];
            loop.SetTime(0); loop.SetSpeed(1);
        }

        /// <summary>Back on their feet (does nothing unless they were down).</summary>
'@
Rep 'enum Slot { Idle, Walk, Run, Down, Attack, Jump }' 'enum Slot { Idle, Walk, Run, Down, Attack, Jump, Stun }'
Rep 'const int SlotCount = 6;' 'const int SlotCount = 7;'
Rep 'bool hasJump, jumping;' 'bool hasJump, hasStun, jumping;'
Rep '        public bool ClipMode => clipMode;' @'
        public bool ClipMode => clipMode;
        /// <summary>Does this rig have the dazed loop <see cref="Stun"/> plays (clip mode only)?</summary>
        public bool HasStun => clipMode && hasStun;
'@
Rep @'
                Pick(set.jump, fallback.jump),
            };
            hasJump = set.jump || fallback.jump;
'@ @'
                Pick(set.jump, fallback.jump),
                Pick(set.stunned, fallback.stunned),
            };
            hasJump = set.jump || fallback.jump;
            hasStun = set.stunned || fallback.stunned;
'@
Rep '            play[(int)Slot.Jump].SetSpeed(0);   // only runs during a jump' @'
            play[(int)Slot.Jump].SetSpeed(0);   // only runs during a jump
            play[(int)Slot.Stun].SetSpeed(0);   // only runs while dazed
'@
Rep '            if (pose != Pose.Down || gettingUp) return;' @'
            if (pose == Pose.Dazed) { pose = Pose.Idle; return; }     // (the dazed loop fades out in Update)
            if (pose != Pose.Down || gettingUp) return;
'@
Rep @'
            for (int i = (int)Slot.Idle; i <= (int)Slot.Run; i++)
            {
'@ @'
            for (int i = (int)Slot.Idle; i < SlotCount; i++)
            {
                if (i == (int)Slot.Down || i == (int)Slot.Attack || i == (int)Slot.Jump) continue;      // (these play once)
'@
Rep '            var want = pose == Pose.Down ? Slot.Down : slashing ? Slot.Attack : jumping ? Slot.Jump : baseSlot;' @'
            var want = pose == Pose.Down ? Slot.Down : pose == Pose.Dazed ? Slot.Stun : slashing ? Slot.Attack : jumping ? Slot.Jump : baseSlot;
            if (pose != Pose.Dazed && weight[(int)Slot.Stun] < 0.02f) play[(int)Slot.Stun].SetSpeed(0);      // (faded out: the loop rests until the next daze)
'@
Rep '/// <summary>For tests: how strongly each clip slot is currently shown (idle, walk, run, down, attack).</summary>' '/// <summary>For tests: how strongly each clip slot is currently shown (idle, walk, run, down, attack, jump, dazed).</summary>'
Save-Src

Open-Src "$A\GuardVisual.cs"
Rep '/// and animates it: runs while walking / investigating / chasing, falls down while stunned.' '/// and animates it: runs while walking / investigating / chasing, stands dazed (head down, arms hanging) while stunned (the owner, 2026-10-20; it used to fall down).'
Rep 'if (ai.State == GuardState.Stunned) { anim.Fall(); return; }' 'if (ai.State == GuardState.Stunned) { anim.Stun(); return; }'
Save-Src

Open-Src "$E\NetDev\NetDevToon.cs"
Rep '            ToonCheck("gets back up to idle", w[0] > 0.8f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");' @'
            ToonCheck("gets back up to idle", w[0] > 0.8f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");

            // the officers' stun (the owner, 2026-10-20): the Blink dazed loop, standing, instead of lying on the floor
            ToonCheck("the officer has the dazed (stunned) loop", cop.HasStun);
            for (float t = 0; t < 1.0f; t += Time.deltaTime) { cop.Stun(); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("stunned: the dazed loop shows and he does not fall down", w[6] > 0.8f && w[3] < 0.1f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
            ShotCam(cam, "toon_lineup_stunned");
            for (float t = 0; t < 3.0f; t += Time.deltaTime) { cop.Recover(); cop.Locomote(false); yield return null; }
            w = cop.DebugWeights();
            ToonCheck("snaps out of the daze to idle", w[0] > 0.8f && w[6] < 0.1f, $"(weights {string.Join(",", w.Select(x => x.ToString("0.00")))})");
'@
Save-Src

Open-Src "$E\NetDev\NetDevGuardCall.cs"
Rep @'
            GuardCallCheck("a melee blow knocks the guard over: he is stunned and not calling yet", melee.State == GuardState.Stunned && !melee.IsCalling);
            yield return new WaitForSeconds(1.5f);
'@ @'
            GuardCallCheck("a melee blow knocks the guard over: he is stunned and not calling yet", melee.State == GuardState.Stunned && !melee.IsCalling);
            yield return new WaitForSeconds(0.5f);
            var meleeVisual = melee.GetComponent<GuardVisual>();
            var meleeWeights = meleeVisual && meleeVisual.Anim ? meleeVisual.Anim.DebugWeights() : null;
            GuardCallCheck("a knocked-over officer stands dazed (the Blink stunned loop), he does not lie down (the owner, 2026-10-20)", meleeWeights != null && meleeVisual.Anim.HasStun && meleeWeights[6] > 0.5f && meleeWeights[3] < 0.2f,
                meleeWeights != null ? $"(weights {string.Join(",", meleeWeights.Select(x => x.ToString("0.00")))}, state {melee.State})" : "(no animator)");
            yield return new WaitForSeconds(1.0f);
'@
Save-Src
Write-Output 'round 12 batch G43 patched'
