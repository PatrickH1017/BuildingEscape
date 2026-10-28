# The owner's gun stance animations (2026-10-27): "I will now provide plenty of animations for hip-fire while standing/moving and aiming while standing/moving. Please implement them properly to the matching weapon".
# Four loops for each gun (hip fire standing / running, aimed standing / running; Blender 5.2 files exported with DevTools\PistolAnim_2026-10-27\export_fbx.py to Assets\Resources\Animations\<Gun>_HipFire_Stand.fbx ...):
#   WeaponAnimation  - the clips of a gun (GunClips.Stances), their names, StancesEnabled
#   WeaponAnimationSetup - the 20 files as humanoid loops
#   CharacterAnimator - the gun layer mixes low ready / hip stand / hip run / aimed stand / aimed run by how far the gun is raised and how fast the body moves; the run loops keep the legs' step
#   HeldGun - the stance clips hold the gun: the barrel as the clip has it is turned to the crosshair, the shot's kick is made here, the pistol is held in both hands (the left hand stays on the gun)
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

# ---------------------------------------------------------------- AimRules: where the raise is between the low ready, the hip and the sights
Open-Src ($A + 'Scripts\Guns\AimRules.cs')
Rep '        public static float Smooth(float share) { float t = Math.Max(0f, Math.Min(1f, share)); return t * t * (3f - 2f * t); }' @'
        public static float Smooth(float share) { float t = Math.Max(0f, Math.Min(1f, share)); return t * t * (3f - 2f * t); }

        /// <summary>How far a gun is from the low ready (0, lowered) to the hip (1, <see cref="HipRaise"/> and above), eased: what the stance clips are shown by.</summary>
        public static float HipShare(float raise) => Smooth(raise / HipRaise);

        /// <summary>How far a gun is from the hip (0) to the sights (1), eased.</summary>
        public static float AimShare(float raise) => Smooth((raise - HipRaise) / (1f - HipRaise));
'@
Save-Src

# ---------------------------------------------------------------- WeaponAnimation: the stance clips of a gun
Open-Src ($A + 'Scripts\Guns\WeaponAnimation.cs')
Rep '            public bool Valid => raise && aim && fire && raise.isHumanMotion && aim.isHumanMotion && fire.isHumanMotion && (!reload || reload.isHumanMotion);' @'
            public bool Valid => raise && aim && fire && raise.isHumanMotion && aim.isHumanMotion && fire.isHumanMotion && (!reload || reload.isHumanMotion);
            /// <summary>The stances (the owner, 2026-10-27): the gun held at the hip and aimed, standing and running, in both hands, as loops (the pistol too, now in both hands).</summary>
            public AnimationClip hipStand, hipRun, aimStand, aimRun;
            /// <summary>The four stance loops are in the build and <see cref="StancesEnabled"/>: the gun is held with them (the raise clip is the low ready only).</summary>
            public bool Stances => StancesEnabled && hipStand && hipRun && aimStand && aimRun && hipStand.isHumanMotion && hipRun.isHumanMotion && aimStand.isHumanMotion && aimRun.isHumanMotion;
'@
Rep '        public const string Raise = "_Raise", Aim = "_Aim", Fire = "_Fire", Reload = "_Reload", Ready = "_Ready", Swing = "_Swing";' @'
        public const string Raise = "_Raise", Aim = "_Aim", Fire = "_Fire", Reload = "_Reload", Ready = "_Ready", Swing = "_Swing";

        /// <summary>The guns are held with the owner's stance loops of 2026-10-27 (false: the raise / aim / fire clips of before are used alone).</summary>
        public static bool StancesEnabled = true;

        /// <summary>The file (and the clip in it) of a stance: "Rifle_HipFire_Stand", "Rifle_Aimed_Run" ... (<paramref name="gun"/>: <see cref="GunName"/>).</summary>
        public static string StanceFile(string gun, bool aimed, bool run) => gun + (aimed ? "_Aimed" : "_HipFire") + (run ? "_Run" : "_Stand");

        /// <summary>Loads a gun's four stance loops into <paramref name="c"/> (they stay unset when a file is missing: the gun is then held the way it was before the stances).</summary>
        public static void LoadStances(GunClips c, string gun)
        {
            AnimationClip One(bool aimed, bool run) { string f = StanceFile(gun, aimed, run); return Find(Resources.LoadAll<AnimationClip>(Folder + "/" + f), f); }
            c.hipStand = One(false, false); c.hipRun = One(false, true); c.aimStand = One(true, false); c.aimRun = One(true, true);
            if (!(c.hipStand && c.hipRun && c.aimStand && c.aimRun)) Debug.LogWarning($"[WeaponAnimation] the {gun} stance clips are missing from Resources/{Folder}: the gun is held the old way");
        }
'@
Rep '            if (c.Valid) guns[i] = c;' '            if (c.Valid) { LoadStances(c, n); guns[i] = c; }'
Save-Src

# ---------------------------------------------------------------- PistolAnimation: the player's pistol has its stances too
Open-Src ($A + 'Scripts\Guns\PistolAnimation.cs')
Rep '            if (c.Valid && c.reload) clips = c;' '            if (c.Valid && c.reload) { WeaponAnimation.LoadStances(c, "Pistol"); clips = c; }'
Save-Src

# ---------------------------------------------------------------- the setup: the 20 files are humanoid loops named like their files
Open-Src ($A + 'Editor\WeaponAnimationSetup.cs')
Rep '        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]' @'
        // the stance files (the owner, 2026-10-27): for each gun the hip-fire and the aimed pose, standing (60 frames) and running (20 / 24 frames), each ONE loop of the whole take, named like its file
        static IEnumerable<KeyValuePair<string, Cut[]>> StanceCuts()
        {
            foreach (var gun in new[] { "Pistol", "Smg", "Rifle", "Shotgun", "Sniper" })
                foreach (var aimed in new[] { false, true })
                    foreach (var run in new[] { false, true })
                    {
                        string f = WeaponAnimation.StanceFile(gun, aimed, run);
                        yield return new KeyValuePair<string, Cut[]>(f, new[] { new Cut(f, 1, run ? (aimed ? 24 : 20) : 60, true) });
                    }
        }

        [MenuItem("Employees Take Revenge/Setup Weapon Animations")]
'@
Rep '            foreach (var pair in Cuts)' '            foreach (var pair in Cuts.Concat(StanceCuts()))'
Save-Src

# ---------------------------------------------------------------- the player's speed reaches the animator in metres a second
Open-Src ($A + 'Scripts\PlayerVisual.cs')
Rep '            anim.Locomote(v.magnitude > 0.5f, Mathf.Clamp(v.magnitude / 3.5f, 0.6f, 1.6f));' '            anim.Locomote(v.magnitude > 0.5f, Mathf.Clamp(v.magnitude / 3.5f, 0.6f, 1.6f), v.magnitude);'
Save-Src

# ---------------------------------------------------------------- CharacterAnimator: the gun layer mixes the stances
Open-Src ($A + 'Scripts\CharacterAnimator.cs')
Rep '        public void Locomote(bool moving, float speed = 1f)' '        public void Locomote(bool moving, float speed = 1f, float metersPerSecond = -1f)'
Rep '            if (clipMode) { LocomoteClips(moving, speed); pose = moving ? Pose.Run : Pose.Idle; return; }' @'
            if (clipMode) { moveMs = !moving ? 0f : metersPerSecond >= 0f ? metersPerSecond : speed * 3.5f; LocomoteClips(moving, speed); pose = moving ? Pose.Run : Pose.Idle; return; }
'@
Rep '        AnimationClipPlayable pRaise, pAim, pShot, pReload, mReady, mSwing;' @'
        AnimationClipPlayable pRaise, pAim, pShot, pReload, mReady, mSwing;
        AnimationClipPlayable pHipStand, pHipRun, pAimStand, pAimRun;          // (the gun's stances, inputs 3 to 6 of gunMix; see GunPose)
        float moveMs, runShare, standT;                                          // (how fast the body moves, m/s; how much of the upper body's run the stance shows; the standing loops' clock)
'@
Rep '            gunMix = AnimationMixerPlayable.Create(graph, 3);' '            gunMix = AnimationMixerPlayable.Create(graph, 7);'
Rep '            pShot = Swap(pShot, c.fire, gunMix, 2);' @'
            pShot = Swap(pShot, c.fire, gunMix, 2);
            bool st = c.Stances;
            pHipStand = Swap(pHipStand, st ? c.hipStand : null, gunMix, 3);
            pHipRun = Swap(pHipRun, st ? c.hipRun : null, gunMix, 4);
            pAimStand = Swap(pAimStand, st ? c.aimStand : null, gunMix, 5);
            pAimRun = Swap(pAimRun, st ? c.aimRun : null, gunMix, 6);
'@
Rep '            layers.SetLayerMaskFromAvatarMask(1, c.twoHand ? WeaponAnimation.UpperBody : PistolAnimation.AimMask);' '            layers.SetLayerMaskFromAvatarMask(1, c.twoHand || c.Stances ? WeaponAnimation.UpperBody : PistolAnimation.AimMask);'
RepRegion '            bool shot = raise >= 0.999f && sinceShot >= 0f && sinceShot < shotLength;' '            pShot.SetTime(shot ? sinceShot : 0f);' @'
            if (c.Stances)
            {
                // the stances (the owner, 2026-10-27): the gun is held with the loops, mixed by how far it is raised (the low ready of the raise clip, the hip, the sights) and how fast the body moves (standing, running). The shot has no
                // clip of its own here: its kick is made by the HeldGun. The standing loops run on a clock of their own; the running loops keep the step of the legs' own run (or walk) clip.
                float hip = AimRules.HipShare(raise), aimed = AimRules.AimShare(raise);
                float runWant = moveMs <= 0f ? 0f : Mathf.InverseLerp(StanceWalkMs, StanceRunMs, moveMs);
                runShare = Mathf.MoveTowards(runShare, runWant, Time.deltaTime * 6f);
                float h = hip * (1f - aimed), a = hip * aimed;
                gunMix.SetInputWeight(0, 1f - hip);
                gunMix.SetInputWeight(1, 0f);
                gunMix.SetInputWeight(2, 0f);
                gunMix.SetInputWeight(3, h * (1f - runShare));
                gunMix.SetInputWeight(4, h * runShare);
                gunMix.SetInputWeight(5, a * (1f - runShare));
                gunMix.SetInputWeight(6, a * runShare);
                pRaise.SetTime(0f);
                standT += Time.deltaTime;
                pHipStand.SetTime(Mathf.Repeat(standT, c.hipStand.length));
                pAimStand.SetTime(Mathf.Repeat(standT, c.aimStand.length));
                var leg = baseSlot == Slot.Walk ? Slot.Walk : Slot.Run;
                float legLen = slotClip[(int)leg].length;
                float phase = legLen > 0.01f ? Mathf.Repeat((float)play[(int)leg].GetTime() / legLen + StanceRunPhase, 1f) : 0f;
                pHipRun.SetTime(phase * c.hipRun.length);
                pAimRun.SetTime(phase * c.aimRun.length);
            }
            else
            {
                bool shot = raise >= 0.999f && sinceShot >= 0f && sinceShot < shotLength;
                int slot = shot ? 2 : raise < 0.999f ? 0 : 1;
                for (int i = 0; i < 3; i++) gunMix.SetInputWeight(i, i == slot ? 1f : 0f);
                for (int i = 3; i < 7; i++) gunMix.SetInputWeight(i, 0f);
                pRaise.SetTime(raise * raiseLength);
                pShot.SetTime(shot ? sinceShot : 0f);
            }
'@
Rep '        /// <summary>How much of the upper body the gun''s animation has this frame (0..1; for the tests).</summary>' @'
        /// <summary>The metres a second from which the stance loops show the run (below the first: standing), and from which they show all of it. A creeping player (2.8 m/s) shows most of it.</summary>
        public const float StanceWalkMs = 1f, StanceRunMs = 3.5f;
        /// <summary>Where in its cycle the legs' run (or walk) clip is when the stances' run loops start (a share of the cycle; tuned by eye).</summary>
        public const float StanceRunPhase = 0f;
        /// <summary>How much of the run the gun's stance shows this frame (0 standing .. 1 running; for the tests).</summary>
        public float RunShare => runShare;
        /// <summary>The gun in the layers is held with the stance loops (for the tests).</summary>
        public bool StanceActive => layersOn && gunClips != null && gunClips.Stances;

        /// <summary>How much of the upper body the gun's animation has this frame (0..1; for the tests).</summary>
'@
Save-Src
"stance code patched (CharacterAnimator, WeaponAnimation, setup); HeldGun next"
