using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BuildingEscape
{
    /// <summary>
    /// Drives a character's animation (idle, run, jump, fall, get up, swing) from simple game states.
    /// The Party Characters' controller is trigger based, so this fires a trigger only when the pose changes.
    /// The Tiny Toon people have no controller: <see cref="UseClips"/> switches this to "clip mode", where the
    /// idle / walk / run / knocked-down / swing clips are blended by a small playable graph instead.
    /// Everything else in the game talks to the same few methods and does not care which rig it is.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        enum Pose { Idle, Run, Down, Dazed }

        static readonly int HIdle = Animator.StringToHash("idle");
        static readonly int HRun = Animator.StringToHash("run");
        static readonly int HFall = Animator.StringToHash("fall");
        static readonly int HJump = Animator.StringToHash("jump");
        static readonly int HGetUp = Animator.StringToHash("getup");
        static readonly int[] AllTriggers = { HIdle, HRun, HFall, HJump, HGetUp };

        Animator anim;
        Pose pose = Pose.Idle;
        float lockUntil;

        void Awake() { anim = GetComponent<Animator>(); }

        /// <summary>The right hand bone of a humanoid rig (what a held weapon is parented to), or null for the Party rig.</summary>
        public Transform RightHand => clipMode && anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        /// <summary>The left hand bone of a humanoid rig (the hand that holds the front of a long gun), or null for the Party rig.</summary>
        public Transform LeftHand => clipMode && anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftHand) : null;

        /// <summary>Call every frame. Idle or run, with the run animation sped up or slowed to match the movement.</summary>
        public void Locomote(bool moving, float speed = 1f, float metersPerSecond = -1f)
        {
            if (!anim || pose == Pose.Down || pose == Pose.Dazed) return;
            if (clipMode) { moveMs = !moving ? 0f : metersPerSecond >= 0f ? metersPerSecond : speed * 3.5f; LocomoteClips(moving, speed); pose = moving ? Pose.Run : Pose.Idle; return; }
            anim.speed = moving ? speed : 1f;
            if (Time.time < lockUntil) return; // let a jump / get-up finish first
            var next = moving ? Pose.Run : Pose.Idle;
            if (next == pose) return;
            pose = next;
            Fire(moving ? HRun : HIdle);
        }

        /// <summary>Knocked over or stunned.</summary>
        public void Fall()
        {
            if (!anim) return;
            if (clipMode) { FallClips(); return; }
            if (pose == Pose.Down) return;
            pose = Pose.Down;
            anim.speed = 1f;
            Fire(HFall);
        }

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
        public void Recover()
        {
            if (!anim) return;
            if (clipMode) { RecoverClips(); return; }
            if (pose != Pose.Down) return;
            pose = Pose.Run; // the get-up leads into the run state; Locomote settles it to idle if standing still
            lockUntil = Time.time + 0.3f;
            Fire(HGetUp);
        }

        public void Jump()
        {
            if (!anim || pose == Pose.Down || pose == Pose.Dazed) return;
            if (clipMode)
            {
                if (!hasJump) return;                    // (the Tiny Toon pack has no jump clip; the Human Animations one does)
                var j = play[(int)Slot.Jump];
                j.SetTime(0); j.SetSpeed(1);
                jumping = true; jumpUntil = Time.time + slotClip[(int)Slot.Jump].length;
                return;
            }
            pose = Pose.Run;
            lockUntil = Time.time + 0.6f;
            Fire(HJump);
        }

        AnimationClip slash;
        float slashT, slashSpeed = 1f;
        bool slashing;
        /// <summary>An attack is being played: the slash clip, or a melee weapon's Blender swing (<see cref="MeleePose"/>).</summary>
        public bool Slashing => slashing || meleeSwinging;

        /// <summary>
        /// Plays an attack over whatever the character is doing. The Party rig samples the given (retargeted) clip;
        /// a Tiny Toon rig ignores it and plays its own melee clip.
        /// </summary>
        public void PlaySlash(AnimationClip clip, float speed = 1f)
        {
            if (clipMode) { SlashClips(speed); return; }
            if (!clip) return;
            slash = clip; slashT = 0f; slashSpeed = Mathf.Max(0.1f, speed); slashing = true;
        }

        void LateUpdate()
        {
            if (clipMode || !slashing) return;
            slashT += Time.deltaTime * slashSpeed;
            if (!slash || slashT >= slash.length) { slashing = false; return; }
            slash.SampleAnimation(gameObject, slashT);
        }

        void Fire(int trigger)
        {
            foreach (var t in AllTriggers) anim.ResetTrigger(t);
            anim.SetTrigger(trigger);
        }

        // ------------------------------------------------------------------ clip mode (Tiny Toon people)

        enum Slot { Idle, Walk, Run, Down, Attack, Jump, Stun }
        const int SlotCount = 7;
        bool hasJump, hasStun, jumping;
        float jumpUntil;
        /// <summary>Relative speed (see Locomote) from which the run clip is used instead of the walk clip.</summary>
        const float RunThreshold = 0.9f;

        bool clipMode;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] play;
        AnimationClip[] slotClip;
        readonly float[] weight = new float[SlotCount];
        Slot baseSlot = Slot.Idle;
        bool gettingUp;
        float attackUntil;

        public bool ClipMode => clipMode;
        /// <summary>Does this rig have the dazed loop <see cref="Stun"/> plays (clip mode only)?</summary>
        public bool HasStun => clipMode && hasStun;

        /// <summary>
        /// Switch to clip mode: drop the animator controller and mix these clips with a playable graph.
        /// <paramref name="fallback"/> fills any clip the set lacks. Returns false (and stays as it was) if there is no idle clip.
        /// </summary>
        public bool UseClips(ToonClipSet set, ToonClipSet fallback)
        {
            if (!anim) anim = GetComponent<Animator>();
            if (!anim || set == null) return false;
            fallback ??= new ToonClipSet();
            var idle = set.idle ? set.idle : fallback.idle;
            if (!idle) return false;
            AnimationClip Pick(AnimationClip a, AnimationClip b) => a ? a : b ? b : idle;
            slotClip = new[]
            {
                idle,
                Pick(set.walk, fallback.walk),
                Pick(set.run, fallback.run),
                Pick(set.death, fallback.death),
                Pick(set.attack, fallback.attack),
                Pick(set.jump, fallback.jump),
                Pick(set.stunned, fallback.stunned),
            };
            hasJump = set.jump || fallback.jump;
            hasStun = set.stunned || fallback.stunned;

            anim.runtimeAnimatorController = null;
            anim.applyRootMotion = false; // the capsule moves, not the animation
            graph = PlayableGraph.Create("Toon " + name);
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, "Toon", anim);
            mixer = AnimationMixerPlayable.Create(graph, SlotCount);
            play = new AnimationClipPlayable[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                play[i] = AnimationClipPlayable.Create(graph, slotClip[i]);
                graph.Connect(play[i], 0, mixer, i);
                mixer.SetInputWeight(i, i == (int)Slot.Idle ? 1f : 0f);
                weight[i] = i == (int)Slot.Idle ? 1f : 0f;
            }
            play[(int)Slot.Down].SetSpeed(0);   // only runs while knocked over
            play[(int)Slot.Attack].SetSpeed(0); // only runs during a swing
            play[(int)Slot.Jump].SetSpeed(0);   // only runs during a jump
            play[(int)Slot.Stun].SetSpeed(0);   // only runs while dazed
            output.SetSourcePlayable(mixer);
            graph.Play();
            clipMode = true;
            return true;
        }

        void LocomoteClips(bool moving, float speed)
        {
            baseSlot = !moving ? Slot.Idle : speed >= RunThreshold ? Slot.Run : Slot.Walk;
            play[(int)Slot.Walk].SetSpeed(Mathf.Clamp(speed / 0.75f, 0.7f, 1.3f));
            play[(int)Slot.Run].SetSpeed(Mathf.Clamp(speed, 0.8f, 1.6f));
        }

        void FallClips()
        {
            if (pose == Pose.Down && !gettingUp) return;
            if (!gettingUp) play[(int)Slot.Down].SetTime(0);
            gettingUp = false;
            play[(int)Slot.Down].SetSpeed(1);
            pose = Pose.Down;
        }

        void RecoverClips()
        {
            if (pose == Pose.Dazed) { pose = Pose.Idle; return; }     // (the dazed loop fades out in Update)
            if (pose != Pose.Down || gettingUp) return;
            // the pack has no get-up clip, so the knock-down is played backwards
            var d = play[(int)Slot.Down];
            d.SetTime(Mathf.Min((float)d.GetTime(), slotClip[(int)Slot.Down].length));
            d.SetSpeed(-1);
            gettingUp = true;
        }

        void SlashClips(float speed)
        {
            speed = Mathf.Max(0.1f, speed);
            var a = play[(int)Slot.Attack];
            a.SetTime(0);
            a.SetSpeed(speed);
            attackUntil = Time.time + slotClip[(int)Slot.Attack].length / speed;
            slashing = true;
        }

        void Update()
        {
            if (!clipMode || !graph.IsValid()) return;

            if (gettingUp && play[(int)Slot.Down].GetTime() <= 0.02)
            {
                gettingUp = false;
                pose = Pose.Idle;
                play[(int)Slot.Down].SetSpeed(0);
            }
            if (slashing && Time.time >= attackUntil)
            {
                slashing = false;
                play[(int)Slot.Attack].SetSpeed(0);
            }

            // keep the looping clips looping (does not depend on the clip's import settings)
            for (int i = (int)Slot.Idle; i < SlotCount; i++)
            {
                if (i == (int)Slot.Down || i == (int)Slot.Attack || i == (int)Slot.Jump) continue;      // (these play once)
                float len = slotClip[i].length;
                double t = play[i].GetTime();
                if (len > 0.01f && t >= len) play[i].SetTime(t % len);
            }

            if (layersOn) FadeWeapons();                                           // (nobody holds the gun or the weapon any more: it was put away)
            if (jumping && Time.time >= jumpUntil) { jumping = false; play[(int)Slot.Jump].SetSpeed(0); }
            var want = pose == Pose.Down ? Slot.Down : pose == Pose.Dazed ? Slot.Stun : slashing ? Slot.Attack : jumping ? Slot.Jump : baseSlot;
            if (pose != Pose.Dazed && weight[(int)Slot.Stun] < 0.02f) play[(int)Slot.Stun].SetSpeed(0);      // (faded out: the loop rests until the next daze)
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            float sum = 0f;
            for (int i = 0; i < SlotCount; i++)
            {
                weight[i] = Mathf.Lerp(weight[i], i == (int)want ? 1f : 0f, k);
                sum += weight[i];
            }
            for (int i = 0; i < SlotCount; i++)
                mixer.SetInputWeight(i, sum > 0.001f ? weight[i] / sum : (i == (int)Slot.Idle ? 1f : 0f));
        }

        // ------------------------------------------------------------------ the weapons (clip mode on a humanoid rig: the Blender animations, see PistolAnimation and WeaponAnimation)

        // the clips above go into a layer mixer: 0 = them, 1 = a gun held (the raise, the aim, a shot) over the upper body and the right arm (both arms for a long gun), 2 = a pistol's reload over both arms, 3 = a melee weapon (held ready,
        // a swing) over the upper body and both arms. A gun's or a weapon's clips are put in when it is first held and changed when another one is.
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable gunMix, meleeMix;
        AnimationClipPlayable pRaise, pAim, pShot, pReload, mReady, mSwing;
        AnimationClipPlayable pHipStand, pHipRun, pAimStand, pAimRun;          // (the gun's stances, inputs 3 to 6 of gunMix; see GunPose)
        AnimationClipPlayable mIdleStand, mIdleRun, mReadyStand, mReadyRun;    // (the melee holds, inputs 2 to 5 of meleeMix; see MeleePose)
        float meleeStandT, meleeReadyShare, swingBlend;
        int runShareFrame = -1;
        float moveMs, runShare, standT;                                          // (how fast the body moves, m/s; how much of the upper body's run the stance shows; the standing loops' clock)
        WeaponAnimation.GunClips gunClips;
        WeaponAnimation.MeleeClips meleeClips;
        float raiseLength, shotLength, reloadLength;
        bool layersOn, meleeSwinging;
        int gunAsked = -10, meleeAsked = -10;    // the frames GunPose and MeleePose were last called in
        float gunWeight, reloadWeight, meleeWeight;

        /// <summary>Can this rig play the Blender weapon animations: clip mode and a humanoid avatar.</summary>
        public bool CanPlayWeapons => clipMode && anim && anim.isHuman && graph.IsValid();

        /// <summary>Can this rig play the pistol animation: the weapon layers and the clips in the build.</summary>
        public bool CanPlayPistol => CanPlayWeapons && PistolAnimation.Load() != null;

        /// <summary>The metres a second from which the stance loops show the run (below the first: standing), and from which they show all of it. A creeping player (2.8 m/s) shows most of it.</summary>
        public const float StanceWalkMs = 1f, StanceRunMs = 3.5f;
        /// <summary>Where in its cycle the legs' run (or walk) clip is when the stances' run loops start (a share of the cycle; tuned by eye).</summary>
        public const float StanceRunPhase = 0f;
        /// <summary>How much of the run the gun's stance shows this frame (0 standing .. 1 running; for the tests).</summary>
        public float RunShare => runShare;
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
        public bool StanceActive => layersOn && gunClips != null && gunClips.Stances;

        /// <summary>How much of the upper body the gun's animation has this frame (0..1; for the tests).</summary>
        public float GunWeight => layersOn && gunClips != null ? gunWeight : 0f;

        /// <summary>How far the reload clip is shown this frame (0..1, eased in and out): the HeldGun lets go of the aim meanwhile.</summary>
        public float GunReloadWeight => layersOn && gunClips != null ? reloadWeight * gunWeight : 0f;

        /// <summary>How much of the upper body the melee weapon's animation has this frame (0..1; for the tests).</summary>
        public float MeleeWeight => layersOn && meleeClips != null ? meleeWeight : 0f;

        /// <summary>The clips of the gun / the melee weapon in the layers now (null: none yet).</summary>
        public WeaponAnimation.GunClips GunClipsNow => gunClips;
        public WeaponAnimation.MeleeClips MeleeClipsNow => meleeClips;

        /// <summary>The weapons' layers, added to the graph the first time a weapon is held.</summary>
        bool EnsureLayers()
        {
            if (layersOn) return true;
            if (!CanPlayWeapons) return false;
            layers = AnimationLayerMixerPlayable.Create(graph, 4);
            output.SetSourcePlayable(layers);
            graph.Connect(mixer, 0, layers, 0);
            layers.SetInputWeight(0, 1f);
            gunMix = AnimationMixerPlayable.Create(graph, 7);
            meleeMix = AnimationMixerPlayable.Create(graph, 6);
            graph.Connect(gunMix, 0, layers, 1);
            graph.Connect(meleeMix, 0, layers, 3);
            layers.SetLayerMaskFromAvatarMask(2, PistolAnimation.ReloadMask);
            layers.SetLayerMaskFromAvatarMask(3, WeaponAnimation.UpperBody);
            for (int i = 1; i < 4; i++) layers.SetInputWeight(i, 0f);
            gunWeight = reloadWeight = meleeWeight = 0f;
            layersOn = true;
            return true;
        }

        /// <summary>A clip that does not run by itself: its time is set every frame.</summary>
        AnimationClipPlayable Still(AnimationClip clip)
        {
            var p = AnimationClipPlayable.Create(graph, clip);
            p.SetApplyFootIK(false);
            p.SetSpeed(0);
            return p;
        }

        /// <summary>Puts <paramref name="clip"/> into input <paramref name="input"/> of <paramref name="into"/> in place of <paramref name="old"/> (which goes; no clip leaves the input empty).</summary>
        AnimationClipPlayable Swap<T>(AnimationClipPlayable old, AnimationClip clip, T into, int input) where T : struct, IPlayable
        {
            if (old.IsValid()) { graph.Disconnect(into, input); old.Destroy(); }
            if (!clip) return default;
            var p = Still(clip);
            graph.Connect(p, 0, into, input);
            return p;
        }

        void UseGunClips(WeaponAnimation.GunClips c)
        {
            if (c == gunClips) return;
            pRaise = Swap(pRaise, c.raise, gunMix, 0);
            pAim = Swap(pAim, c.aim, gunMix, 1);
            pShot = Swap(pShot, c.fire, gunMix, 2);
            bool st = c.Stances;
            pHipStand = Swap(pHipStand, st ? c.hipStand : null, gunMix, 3);
            pHipRun = Swap(pHipRun, st ? c.hipRun : null, gunMix, 4);
            pAimStand = Swap(pAimStand, st ? c.aimStand : null, gunMix, 5);
            pAimRun = Swap(pAimRun, st ? c.aimRun : null, gunMix, 6);
            pReload = Swap(pReload, c.reload, layers, 2);
            layers.SetLayerMaskFromAvatarMask(1, c.twoHand || c.Stances ? WeaponAnimation.UpperBody : PistolAnimation.AimMask);
            raiseLength = c.raise.length; shotLength = c.fire.length; reloadLength = c.reload ? c.reload.length : 0f;
            gunClips = c;
            gunWeight = reloadWeight = 0f;
        }

        void UseMeleeClips(WeaponAnimation.MeleeClips c)
        {
            if (c == meleeClips) return;
            mReady = Swap(mReady, c.ready, meleeMix, 0);
            mSwing = Swap(mSwing, c.swing, meleeMix, 1);
            bool st = c.Stances;
            mIdleStand = Swap(mIdleStand, st ? c.idleStand : null, meleeMix, 2);
            mIdleRun = Swap(mIdleRun, st ? c.idleRun : null, meleeMix, 3);
            mReadyStand = Swap(mReadyStand, st ? c.readyStand : null, meleeMix, 4);
            mReadyRun = Swap(mReadyRun, st ? c.readyRun : null, meleeMix, 5);
            meleeClips = c;
            meleeWeight = 0f;
        }

        /// <summary>
        /// A gun's pose this frame (the HeldGun calls it every frame, before the animation runs): <paramref name="raise"/> 0 = lowered (a pistol at the side, a long gun at the low ready) .. 1 = aimed (the raise clip is shown at that point
        /// of it, so lowering plays it backwards), <paramref name="sinceShot"/> the seconds since the last shot (the shot clip plays for as long as it lasts), <paramref name="reload"/> how far a pistol's reload has got (0 = none). A
        /// pistol's upper body goes back to the walk and the run in the last quarter of the way down (a long gun stays in both hands); both at once while the body is knocked over or dazed. False when this rig cannot play it.
        /// </summary>
        public bool GunPose(WeaponAnimation.GunClips c, float raise, float sinceShot, float reload)
        {
            if (c == null || !EnsureLayers()) return false;
            UseGunClips(c);
            gunAsked = Time.frameCount;
            bool down = pose == Pose.Down || pose == Pose.Dazed;
            raise = Mathf.Clamp01(raise);
            gunWeight = down ? Mathf.MoveTowards(gunWeight, 0f, Time.deltaTime * 10f) : c.twoHand ? Mathf.MoveTowards(gunWeight, 1f, Time.deltaTime * 8f) : Mathf.Clamp01(raise * 4f);
            bool reloading = pReload.IsValid() && reload > 0f && reload < 1f;
            reloadWeight = Mathf.MoveTowards(reloadWeight, reloading ? 1f : 0f, Time.deltaTime * 8f);
            if (c.Stances)
            {
                // the stances (the owner, 2026-10-27): the gun is held with the loops, mixed by how far it is raised (the low ready of the raise clip, the hip, the sights) and how fast the body moves (standing, running). The shot has no
                // clip of its own here: its kick is made by the HeldGun. The standing loops run on a clock of their own; the running loops keep the step of the legs' own run (or walk) clip.
                float hip = AimRules.HipShare(raise), aimed = AimRules.AimShare(raise);
                StepRunShare();
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
                float phase = LegPhase();
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
            if (reloading) pReload.SetTime(reload * reloadLength);
            layers.SetInputWeight(1, gunWeight);
            layers.SetInputWeight(2, reloadWeight * gunWeight);
            return true;
        }

        /// <summary>The dock worker's pistol (<see cref="PistolAnimation"/>): <see cref="GunPose"/> with its clips.</summary>
        public bool PistolPose(float raise, float sinceShot, float reload) => GunPose(PistolAnimation.Load(), raise, sinceShot, reload);

        /// <summary>
        /// A melee weapon's pose this frame (its owner calls it every frame while it is in the hand, before the animation runs): held ready, or the swing <paramref name="swingTime"/> seconds into its clip (negative: no swing). The upper
        /// body goes back to the walk and the run while the body is knocked over or dazed. False when this rig cannot play it.
        /// </summary>
        public bool MeleePose(WeaponAnimation.MeleeClips c, float swingTime, float ready = 0f)
        {
            if (c == null || !EnsureLayers()) return false;
            UseMeleeClips(c);
            meleeAsked = Time.frameCount;
            bool down = pose == Pose.Down || pose == Pose.Dazed;
            meleeSwinging = !down && swingTime >= 0f && swingTime < c.swing.length;
            meleeWeight = Mathf.MoveTowards(meleeWeight, down ? 0f : 1f, Time.deltaTime * (down ? 10f : 8f));
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
            layers.SetInputWeight(3, meleeWeight);
            return true;
        }

        /// <summary>Nobody asked for a gun's or a weapon's pose last frame (it was put away): its layers fade out.</summary>
        void FadeWeapons()
        {
            if (gunAsked < Time.frameCount - 1)
            {
                gunWeight = Mathf.MoveTowards(gunWeight, 0f, Time.deltaTime * 8f);
                reloadWeight = Mathf.MoveTowards(reloadWeight, 0f, Time.deltaTime * 8f);
                layers.SetInputWeight(1, gunWeight);
                layers.SetInputWeight(2, reloadWeight * gunWeight);
            }
            if (meleeAsked < Time.frameCount - 1)
            {
                meleeSwinging = false;
                meleeWeight = Mathf.MoveTowards(meleeWeight, 0f, Time.deltaTime * 8f);
                layers.SetInputWeight(3, meleeWeight);
            }
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>For tests: how strongly each clip slot is currently shown (idle, walk, run, down, attack, jump, dazed).</summary>
        public float[] DebugWeights() => (float[])weight.Clone();
    }
}
