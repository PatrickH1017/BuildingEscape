using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// Drives the pack's animator (idle, run, jump, fall, get up) from simple game states.
    /// The pack's controller is trigger based, so this fires a trigger only when the pose changes.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        enum Pose { Idle, Run, Down }

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

        /// <summary>Call every frame. Idle or run, with the run animation sped up or slowed to match the movement.</summary>
        public void Locomote(bool moving, float speed = 1f)
        {
            if (!anim || pose == Pose.Down) return;
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
            if (!anim || pose == Pose.Down) return;
            pose = Pose.Down;
            anim.speed = 1f;
            Fire(HFall);
        }

        /// <summary>Back on their feet (does nothing unless they were down).</summary>
        public void Recover()
        {
            if (!anim || pose != Pose.Down) return;
            pose = Pose.Run; // the get-up leads into the run state; Locomote settles it to idle if standing still
            lockUntil = Time.time + 0.3f;
            Fire(HGetUp);
        }

        public void Jump()
        {
            if (!anim || pose == Pose.Down) return;
            pose = Pose.Run;
            lockUntil = Time.time + 0.6f;
            Fire(HJump);
        }

        AnimationClip slash;
        float slashT, slashSpeed = 1f;
        bool slashing;
        public bool Slashing => slashing;

        /// <summary>Plays a (retargeted) attack clip over whatever the animator is doing.</summary>
        public void PlaySlash(AnimationClip clip, float speed = 1f)
        {
            if (!clip) return;
            slash = clip; slashT = 0f; slashSpeed = Mathf.Max(0.1f, speed); slashing = true;
        }

        void LateUpdate()
        {
            if (!slashing) return;
            slashT += Time.deltaTime * slashSpeed;
            if (!slash || slashT >= slash.length) { slashing = false; return; }
            slash.SampleAnimation(gameObject, slashT);
        }

        void Fire(int trigger)
        {
            foreach (var t in AllTriggers) anim.ResetTrigger(t);
            anim.SetTrigger(trigger);
        }
    }
}
