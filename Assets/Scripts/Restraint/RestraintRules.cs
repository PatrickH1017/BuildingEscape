using System;

namespace BuildingEscape
{
    /// <summary>
    /// The restraint, drag, struggle and rescue animations of the owner (2026-10-28: Restraint_Drag_Animations, Free_Hogtied, Guard_DragHead, Guard_DragHead_Struggle, Guard_Shot_DropHead), as plain numbers and pure functions (no Unity types), so they are tested outside
    /// Unity: when the clips show their zip ties, how fast a dragger's walk clip must play to keep step with the ground, and the struggle's numbers (the owner's design text: the guard's drag slows 40-60 % while the captive struggles, a stamina that
    /// empties in 3-4 s and comes back, and a guard who shoves a captive who keeps it up). The numbers that are mine, not the owner's, are marked PLACEHOLDER.
    /// </summary>
    public static class RestraintRules
    {
        /// <summary>The Blender files run at 30 frames a second; a clip of <paramref name="frames"/> frames (1 .. frames) lasts one frame less as Unity cuts it.</summary>
        public const float Fps = 30f;
        public static float Seconds(int frames) => (frames - 1) / Fps;

        public const int TieUpFrames = 76, HogtiedFrames = 90, DragFrames = 36, GuardShotFrames = 75, FreeUpFrames = 180;

        // ---- the zip ties (three black meshes in the Blender files): the tie-up puts the wrists' on at frame 31, the ankles' at 51 and the link through both at 63; the free-up takes the link off at frame 41, the ankles' at 66 and the wrists' at 91
        public static bool TieUpWrists(float t) => t >= 30f / Fps;
        public static bool TieUpAnkles(float t) => t >= 50f / Fps;
        public static bool TieUpLink(float t) => t >= 62f / Fps;
        public static bool FreeUpWrists(float t) => t < 90f / Fps;
        public static bool FreeUpAnkles(float t) => t < 65f / Fps;
        public static bool FreeUpLink(float t) => t < 40f / Fps;

        // ---- a dragger's walk: the clips walk on the spot, their feet moving back at 0.54 m/s on the ground (measured in the Blender files, all three), so a dragger who goes faster plays them faster
        public const float DragStepMs = 0.54f, DragRateMin = 0.5f, DragRateMax = 2.6f, DragStandStill = 0.15f;

        /// <summary>How fast the dragger's walk clip plays at this ground speed (m/s): 0 when he stands still (the clip is held), else the speed over the clip's own, within <see cref="DragRateMin"/> .. <see cref="DragRateMax"/>.</summary>
        public static float DragRate(float metersPerSecond)
        {
            if (metersPerSecond < DragStandStill) return 0f;
            return Math.Max(DragRateMin, Math.Min(DragRateMax, metersPerSecond / DragStepMs));
        }

        /// <summary>The time into a looping clip of <paramref name="length"/> seconds after <paramref name="elapsed"/> seconds.</summary>
        public static float LoopTime(float elapsed, float length) => length <= 1e-4f ? 0f : (elapsed % length + length) % length;

        // ---- the officer who drags a captive by the head (PLACEHOLDERS: the owner gave none but the ratios below)
        /// <summary>The officer's drag speed (m/s) and what is left of it while the captive struggles (the owner's text: his walk speed drops 40-60 %).</summary>
        public const float GuardDragSpeed = 1.2f, StruggleSlow = 0.5f;

        /// <summary>The speed of the officer's drag.</summary>
        public static float GuardSpeed(bool struggling) => GuardDragSpeed * (struggling ? StruggleSlow : 1f);

        /// <summary>The officer lets the captive go when he is this close (m) to the middle of the outpost, and the drop (Guard_Shot_DropHead) lasts as long as its clip.</summary>
        public const float DragArrive = 1.8f;
        public static float DropSeconds => Seconds(GuardShotFrames);

        // ---- the struggle's stamina (the owner's text: it empties in 3-4 s, the captive then walks at the normal pace for a few seconds while it comes back; the guard who is dragged against for too long shoves him)
        public const float StruggleSeconds = 3.5f, StruggleRecoverSeconds = 3f;
        /// <summary>The officer shoves a captive who has struggled this long without a break, and stops for <see cref="ShoveStopSeconds"/> to do it (the owner's text: 1.5 s).</summary>
        public const float ShoveAfterSeconds = 2.5f, ShoveStopSeconds = 1.5f;

        // ---- the rescue (PLACEHOLDERS): the free-up clip is 6 s long; played 1.5 times as fast it takes 4 s and is the time a teammate holds the button
        public const float FreeSeconds = 4f, FreeReach = 1.8f;
        public static float FreeRate => Seconds(FreeUpFrames) / FreeSeconds;

        /// <summary>The free-up is shown from this share of the hold (a captive whose progress is under it is only lying), and the scuffs, strains and ratchets are heard within this many metres (teammates in the next rooms; the officers hear none of it).</summary>
        public const float FreeShownFrom = 0.005f, HearRange = 20f;

        /// <summary>One step of a captive's freeing: it gains <paramref name="dt"/> / <see cref="FreeSeconds"/> while a teammate holds the button beside him, and loses the same when nobody does (the clip winds back). 0 .. 1.</summary>
        public static float FreeStep(float progress, float dt, bool held)
        {
            float d = Math.Max(0f, dt) / FreeSeconds;
            return Math.Min(1f, Math.Max(0f, progress + (held ? d : -d)));
        }

        /// <summary>Where the rescuer kneels from the freed body's middle, in the clip (metres to the right of a body that faces forward, and behind its hips): the Blender file has him 0.62 m beside it, 0.12 m back.</summary>
        public const float RescuerSide = 0.62f, RescuerBack = 0.12f;

        /// <summary>Where the rescuer's BODY stands beside the lying captive (metres to his right): the two capsules are about a metre and a quarter wide together, so any nearer and one pushes the other away; the clip draws the rescuer kneeling closer (<see cref="RescuerSide"/>).</summary>
        public const float RescuerStandOff = 1.4f;
    }

    /// <summary>
    /// A captive's struggle stamina, host side (pure). While the captive holds the struggle button and has stamina, he struggles: it drains in <see cref="RestraintRules.StruggleSeconds"/>; at zero he cannot struggle until it has come back
    /// (<see cref="RestraintRules.StruggleRecoverSeconds"/> to full from empty); and a continuous struggle of <see cref="RestraintRules.ShoveAfterSeconds"/> makes the officer shove him (<see cref="ShoveDue"/>), which fills the stamina again.
    /// </summary>
    public sealed class StruggleMeter
    {
        /// <summary>0 (empty) .. 1 (full).</summary>
        public float Stamina { get; private set; } = 1f;
        /// <summary>Out of stamina: he cannot struggle until it is back to <see cref="RecoveredAt"/>.</summary>
        public bool Exhausted { get; private set; }
        /// <summary>How long he has struggled without a break (s).</summary>
        public float Streak { get; private set; }
        /// <summary>He is struggling this step (holding the button with stamina, not exhausted).</summary>
        public bool Active { get; private set; }

        /// <summary>The share of the stamina he must have back after being exhausted before he can struggle again.</summary>
        public const float RecoveredAt = 0.35f;

        /// <summary>Advances by <paramref name="dt"/> seconds with the button held or not. True while he struggles this step.</summary>
        public bool Step(float dt, bool holding)
        {
            dt = Math.Max(0f, dt);
            if (Exhausted && Stamina >= RecoveredAt) Exhausted = false;
            Active = holding && !Exhausted && Stamina > 0f;
            if (Active)
            {
                Stamina = Math.Max(0f, Stamina - dt / RestraintRules.StruggleSeconds);
                Streak += dt;
                if (Stamina <= 0f) { Exhausted = true; Active = false; }
            }
            else
            {
                Streak = 0f;
                Stamina = Math.Min(1f, Stamina + dt / RestraintRules.StruggleRecoverSeconds);
            }
            return Active;
        }

        /// <summary>He has struggled long enough for the officer to shove him.</summary>
        public bool ShoveDue => Active && Streak >= RestraintRules.ShoveAfterSeconds;

        /// <summary>The officer shoved him: the struggle stops and the stamina is full again.</summary>
        public void Shoved() { Stamina = 1f; Streak = 0f; Exhausted = false; Active = false; }

        public void Reset() { Stamina = 1f; Streak = 0f; Exhausted = false; Active = false; }
    }
}
