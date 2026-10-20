using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The gun safe on the guard outpost's desk (the owner, 2026-10-18: "The pistol should be in a gun safe in the Outpost that you need to hack to get into"). Point at it and press E: the hack screen comes up
    /// (<see cref="GunSafeHackPanel"/>); a clean hack swings the door open and the pew inside can be taken, a hit on a wall sets off the siren and calls backup like a failed lock hack. The pistol inside is made
    /// with the floor (it has its network id like everything else) but stays out of the world until the safe is open. Open or shut is one bit of the outpost's state (<see cref="OutpostController.Bits"/>), so
    /// every machine and a late joiner agree.
    /// </summary>
    public sealed class GunSafeProp : Interactable
    {
        /// <summary>A player farther from the safe than this loses the hack in progress; the host accepts a result from one this close (the position is a moment old).</summary>
        public const float HackReach = 3.6f, HostReach = 6.0f;
        const float OpenSeconds = 0.6f, OpenDegrees = 105f;

        public static GunSafeProp Active { get; private set; }

        OutpostController controller;
        Transform hinge;
        Pickup pew;
        bool open;
        float openedAt = -99f;

        /// <summary>The puzzle's seed: the same safe, the same corridor.</summary>
        public int Seed { get; private set; }
        public bool IsOpen => open;
        public Pickup Pew => pew;
        /// <summary>The siren is going: the safe is jammed like the lock.</summary>
        public bool Jammed => controller != null && controller.AlarmOn;

        public void Init(OutpostController c, Transform hingeTransform, Pickup gun, int seed)
        {
            controller = c; hinge = hingeTransform; pew = gun; Seed = seed;
            reach = 2.6f;
            FitAimBoxToChildren();
            Active = this;
        }

        protected override void OnDisable() { base.OnDisable(); if (Active == this) Active = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Active = null; }

        public override bool Available => !open;

        public override string Prompt => Jammed ? "The gun safe is jammed - the alarm is going off" : "[E] Hack the gun safe";

        public override void Use(WobblyPlayer who)
        {
            if (open || !who) return;
            if (Jammed) { Backpack.NoticeToLocal("The gun safe is jammed - wait for the alarm to stop"); return; }
            GunSafeHackPanel.Begin(this, who);
        }

        public bool InReach(Vector3 playerPosition) => (playerPosition - transform.position).sqrMagnitude <= HostReach * HostReach;

        /// <summary>The local player's hack ended: offline it is settled here, in a networked run the host settles it.</summary>
        public void SubmitResult(bool success)
        {
            if (Net.NetRole.Synced) Net.RunSync.Instance.RequestGunSafe(success);
            else HostResult(success);
        }

        /// <summary>Host (or offline): a hack finished. A won one opens the safe; a lost one sets off the siren and calls backup.</summary>
        public void HostResult(bool success)
        {
            if (!Net.NetRole.Simulates || open) return;
            if (success) { ApplyOpen(false); return; }
            if (controller != null) controller.HostAlarmAt(transform.position);
        }

        /// <summary>The safe opens (every machine): the door swings, the pistol comes into the world.</summary>
        public void ApplyOpen(bool quiet)
        {
            if (open) return;
            open = true;
            openedAt = Time.time;
            if (pew) pew.gameObject.SetActive(true);
            if (!quiet) GameAudio.PlaySound(SoundId.SafeOpen, transform.position, 0.8f, 1f, true, 16f, 2f, 0);
        }

        void Update()
        {
            if (!hinge) return;
            float t = open ? Mathf.Clamp01((Time.time - openedAt) / OpenSeconds) : 0f;
            t = t * t * (3f - 2f * t);
            hinge.localRotation = Quaternion.Euler(0f, -OpenDegrees * t, 0f);
        }
    }
}
