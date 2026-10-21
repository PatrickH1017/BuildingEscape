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
        public void Locomote(bool moving, float speed = 1f)
        {
            if (!anim || pose == Pose.Down || pose == Pose.Dazed) return;
            if (clipMode) { LocomoteClips(moving, speed); pose = moving ? Pose.Run : Pose.Idle; return; }
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
        public bool Slashing => slashing;

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
            var output = AnimationPlayableOutput.Create(graph, "Toon", anim);
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

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>For tests: how strongly each clip slot is currently shown (idle, walk, run, down, attack, jump, dazed).</summary>
        public float[] DebugWeights() => (float[])weight.Clone();
    }
}
