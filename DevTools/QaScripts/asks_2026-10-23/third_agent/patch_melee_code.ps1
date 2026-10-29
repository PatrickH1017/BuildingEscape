# The owner's melee holds (2026-10-28, Melee_Hold_Animations: "The melee version of Gun_Aim_Animations: Idle is the melee hip fire, Ready is the melee aimed"): for the crowbar, the bat and the hatchet four loops each - Idle / Ready, standing / running -
# made on the swing files (the weapon in the right hand exactly as the swing files hold it: measured, no hold table changed). The same machinery as the gun stances (2.73): the weapon layer mixes them by the right mouse button (ready) and by the
# speed (running, the legs' step); a swing blends in over the stance and back out; in first person the idle stays the code's hold (the pack's own first-person picture of Idle is "about where FirstPersonMelee holds it") and the ready is the clip's.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

# ---------------------------------------------------------------- WeaponAnimation: the melee clips and their loaders
Open-Src ($A + 'Scripts\Guns\WeaponAnimation.cs')
Rep '            public bool Valid => ready && swing && ready.isHumanMotion && swing.isHumanMotion;' @'
            public bool Valid => ready && swing && ready.isHumanMotion && swing.isHumanMotion;
            /// <summary>The melee holds (the owner, 2026-10-28): the weapon held idle and ready, standing and running, as loops (the swing starts from them).</summary>
            public AnimationClip idleStand, idleRun, readyStand, readyRun;
            /// <summary>The four loops are in the build and <see cref="StancesEnabled"/>: the weapon is held with them (the swing file's own ready pose is not shown).</summary>
            public bool Stances => StancesEnabled && idleStand && idleRun && readyStand && readyRun && idleStand.isHumanMotion && idleRun.isHumanMotion && readyStand.isHumanMotion && readyRun.isHumanMotion;
'@
Rep '        static GunClips[] guns;' @'
        /// <summary>The file (and the clip in it) of a melee hold: "Bat_Idle_Stand", "Bat_Ready_Run" ... (<paramref name="weapon"/>: <see cref="MeleeName"/>).</summary>
        public static string MeleeStanceFile(string weapon, bool ready, bool run) => weapon + (ready ? "_Ready" : "_Idle") + (run ? "_Run" : "_Stand");

        /// <summary>Loads a melee weapon's four hold loops into <paramref name="c"/> (they stay unset when a file is missing: the weapon is then held the way it was before).</summary>
        public static void LoadMeleeStances(MeleeClips c, string weapon)
        {
            AnimationClip One(bool ready, bool run) { string f = MeleeStanceFile(weapon, ready, run); return Find(Resources.LoadAll<AnimationClip>(Folder + "/" + f), f); }
            c.idleStand = One(false, false); c.idleRun = One(false, true); c.readyStand = One(true, false); c.readyRun = One(true, true);
            if (!(c.idleStand && c.idleRun && c.readyStand && c.readyRun)) Debug.LogWarning($"[WeaponAnimation] the {weapon} hold clips are missing from Resources/{Folder}: it is held the old way");
        }

        static GunClips[] guns;
'@
Rep '            if (c.Valid) melee[i] = c;' '            if (c.Valid) { LoadMeleeStances(c, n); melee[i] = c; }'
Save-Src

# ---------------------------------------------------------------- the setup: 12 more humanoid loops
Open-Src ($A + 'Editor\WeaponAnimationSetup.cs')
Rep '        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]' @'
        // the melee hold files (the owner, 2026-10-28): for each weapon Idle and Ready, standing (60 frames) and running (20 / 24 frames), each ONE loop of the whole take, named like its file
        static IEnumerable<KeyValuePair<string, Cut[]>> MeleeStanceCuts()
        {
            foreach (var weapon in new[] { "Crowbar", "Bat", "Hatchet" })
                foreach (var ready in new[] { false, true })
                    foreach (var run in new[] { false, true })
                    {
                        string f = WeaponAnimation.MeleeStanceFile(weapon, ready, run);
                        yield return new KeyValuePair<string, Cut[]>(f, new[] { new Cut(f, 1, run ? (ready ? 24 : 20) : 60, true) });
                    }
        }

        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]
'@
Rep '            foreach (var pair in Cuts.Concat(StanceCuts()))' '            foreach (var pair in Cuts.Concat(StanceCuts()).Concat(MeleeStanceCuts()))'
Save-Src

# ---------------------------------------------------------------- CharacterAnimator: the melee layer mixes the holds
Open-Src ($A + 'Scripts\CharacterAnimator.cs')
Rep '            meleeMix = AnimationMixerPlayable.Create(graph, 2);' '            meleeMix = AnimationMixerPlayable.Create(graph, 6);'
Rep '        AnimationClipPlayable pHipStand, pHipRun, pAimStand, pAimRun;          // (the gun''s stances, inputs 3 to 6 of gunMix; see GunPose)' @'
        AnimationClipPlayable pHipStand, pHipRun, pAimStand, pAimRun;          // (the gun's stances, inputs 3 to 6 of gunMix; see GunPose)
        AnimationClipPlayable mIdleStand, mIdleRun, mReadyStand, mReadyRun;    // (the melee holds, inputs 2 to 5 of meleeMix; see MeleePose)
        float meleeStandT, meleeReadyShare, swingBlend;
        int runShareFrame = -1;
'@
Rep '            mSwing = Swap(mSwing, c.swing, meleeMix, 1);' @'
            mSwing = Swap(mSwing, c.swing, meleeMix, 1);
            bool st = c.Stances;
            mIdleStand = Swap(mIdleStand, st ? c.idleStand : null, meleeMix, 2);
            mIdleRun = Swap(mIdleRun, st ? c.idleRun : null, meleeMix, 3);
            mReadyStand = Swap(mReadyStand, st ? c.readyStand : null, meleeMix, 4);
            mReadyRun = Swap(mReadyRun, st ? c.readyRun : null, meleeMix, 5);
'@
# the run share and the legs' phase are shared by the guns' stances and the melee holds
Rep '                float runWant = moveMs <= 0f ? 0f : Mathf.InverseLerp(StanceWalkMs, StanceRunMs, moveMs);
                runShare = Mathf.MoveTowards(runShare, runWant, Time.deltaTime * 6f);' '                StepRunShare();'
Rep '                var leg = baseSlot == Slot.Walk ? Slot.Walk : Slot.Run;
                float legLen = slotClip[(int)leg].length;
                float phase = legLen > 0.01f ? Mathf.Repeat((float)play[(int)leg].GetTime() / legLen + StanceRunPhase, 1f) : 0f;' '                float phase = LegPhase();'
Rep '        /// <summary>The gun in the layers is held with the stance loops (for the tests).</summary>' @'
        /// <summary>How much of the run the stances show: smoothed once a frame (0 standing .. 1 running).</summary>
        float StepRunShare()
        {
            if (runShareFrame != Time.frameCount)
            {
                runShareFrame = Time.frameCount;
                float want = moveMs <= 0f ? 0f : Mathf.InverseLerp(StanceWalkMs, StanceRunMs, moveMs);
                runShare = Mathf.MoveTowards(runShare, want, Time.deltaTime * 6f);
            }
            return runShare;
        }

        /// <summary>Where the legs' own run (or walk) clip is in its cycle (0..1), plus <see cref="StanceRunPhase"/>: the running loops of the guns and the melee weapons take their time from it.</summary>
        float LegPhase()
        {
            var leg = baseSlot == Slot.Walk ? Slot.Walk : Slot.Run;
            float legLen = slotClip[(int)leg].length;
            return legLen > 0.01f ? Mathf.Repeat((float)play[(int)leg].GetTime() / legLen + StanceRunPhase, 1f) : 0f;
        }

        /// <summary>How far the melee weapon is up at the ready this frame (0 idle .. 1 ready, eased; for the first person view and the tests).</summary>
        public float MeleeReadyShare => layersOn && meleeClips != null && meleeClips.Stances ? meleeReadyShare : 0f;
        /// <summary>The melee weapon in the layers is held with the hold loops (for the tests).</summary>
        public bool MeleeStanceActive => layersOn && meleeClips != null && meleeClips.Stances;
        /// <summary>The gun in the layers is held with the stance loops (for the tests).</summary>
'@
Rep '        public bool MeleePose(WeaponAnimation.MeleeClips c, float swingTime)' '        public bool MeleePose(WeaponAnimation.MeleeClips c, float swingTime, float ready = 0f)'
Rep '            meleeMix.SetInputWeight(0, meleeSwinging ? 0f : 1f);
            meleeMix.SetInputWeight(1, meleeSwinging ? 1f : 0f);
            mReady.SetTime(0);
            mSwing.SetTime(meleeSwinging ? swingTime : 0f);' @'
            if (c.Stances)
            {
                // the holds (the owner, 2026-10-28): idle or ready (the right mouse button), standing or running (the speed, the legs' step); a swing blends in over the hold in a tenth of a second and back out after it
                swingBlend = Mathf.MoveTowards(swingBlend, meleeSwinging ? 1f : 0f, Time.deltaTime * (meleeSwinging ? 14f : 9f));
                meleeReadyShare = AimRules.Smooth(ready);
                float run = StepRunShare(), hold = 1f - swingBlend;
                meleeMix.SetInputWeight(0, 0f);
                meleeMix.SetInputWeight(1, swingBlend);
                meleeMix.SetInputWeight(2, hold * (1f - meleeReadyShare) * (1f - run));
                meleeMix.SetInputWeight(3, hold * (1f - meleeReadyShare) * run);
                meleeMix.SetInputWeight(4, hold * meleeReadyShare * (1f - run));
                meleeMix.SetInputWeight(5, hold * meleeReadyShare * run);
                meleeStandT += Time.deltaTime;
                mIdleStand.SetTime(Mathf.Repeat(meleeStandT, c.idleStand.length));
                mReadyStand.SetTime(Mathf.Repeat(meleeStandT, c.readyStand.length));
                float phase = LegPhase();
                mIdleRun.SetTime(phase * c.idleRun.length);
                mReadyRun.SetTime(phase * c.readyRun.length);
                mReady.SetTime(0);
                mSwing.SetTime(meleeSwinging ? swingTime : swingBlend > 0.001f ? c.swing.length : 0f);
            }
            else
            {
                meleeMix.SetInputWeight(0, meleeSwinging ? 0f : 1f);
                meleeMix.SetInputWeight(1, meleeSwinging ? 1f : 0f);
                for (int i = 2; i < 6; i++) meleeMix.SetInputWeight(i, 0f);
                mReady.SetTime(0);
                mSwing.SetTime(meleeSwinging ? swingTime : 0f);
            }
'@
Save-Src

# ---------------------------------------------------------------- MeleeController: the right mouse button is the ready
Open-Src ($A + 'Scripts\MeleeController.cs')
Rep '            PoseMelee();

            if (held == null)' @'
            UpdateReady(eq, held);
            PoseMelee();

            if (held == null)
'@
Rep '            if (anim) anim.MeleePose(meleeClips, swinging ? (Time.time - swingStarted) * swingSpeed : -1f);' '            if (anim) anim.MeleePose(meleeClips, swinging ? (Time.time - swingStarted) * swingSpeed : -1f, readyNow);'
Rep '        static Transform FindDeep(Transform t, string n)' @'
        // ---- the ready (the owner, 2026-10-28: the melee holds, "Idle is the melee hip fire, Ready is the melee aimed"): the right mouse button held with a melee weapon in the hand raises it to the ready (the same flag the guns' aiming sends:
        // the teammates are told); it changes nothing but the pose - the swing, its timing and its damage are as they were
        float readyNow;
        /// <summary>The weapon is held at the ready (the right mouse button; a teammate's from the flag he sends).</summary>
        public bool Ready { get; private set; }
        /// <summary>How far up at the ready the weapon is (0 idle .. 1; for the tests).</summary>
        public float ReadyShare => readyNow;

        void UpdateReady(Pickup eq, Pickup held)
        {
            bool viewed = pack && pack.Viewed;
            bool want = false;
            if (eq && meleeClips != null)
            {
                if (viewed)
                {
                    bool menu = Crosshair.MenuOpen || Backpack.UiOpen || Vendor.ShopOpen || EscapeMenu.Open || TestMode.PanelOpen || ElevatorPanel.Open || OutpostUi.Open;
                    bool free = !(wobbly && wobbly.InCustody) && !(grab && grab.Held);
                    want = GunController.TestAim ?? (!menu && free && (GunController.TestCursorLocked ?? Cursor.lockState == CursorLockMode.Locked) && Input.GetMouseButton(1));
                    if (wobbly) wobbly.IsAiming = want;
                }
                else want = wobbly && wobbly.IsAiming && !wobbly.InCustody;
            }
            else if (viewed && wobbly && held == null) wobbly.IsAiming = false;               // (empty hands: nothing is raised; a gun's own controller sets the flag while one is in the hand)
            Ready = want;
            readyNow = AimRules.Ease(readyNow, want, Time.deltaTime);
        }

        static Transform FindDeep(Transform t, string n)
'@
Save-Src

# ---------------------------------------------------------------- FirstPersonMelee: the ready is the clip's, the idle is the code's hold
Open-Src ($A + 'Scripts\FirstPersonMelee.cs')
Rep '            float want = share * (swing ? 0f : 1f);' '            float want = share * (swing ? 0f : 1f) * (1f - (anim ? anim.MeleeReadyShare : 0f));          // (the ready of the owner''s melee holds is the clip''s own first-person picture: the fist and the handle at the right edge)'
Save-Src
"melee code patched"
