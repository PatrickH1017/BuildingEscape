using System;

namespace BuildingEscape
{
    /// <summary>
    /// Hip fire and aiming (the owner, 2026-10-27: "There should now be hip fire and aiming in first and in third person. A weapon should be at hip-fire unless a player is aiming which is the right mouse button"), without Unity types so the numbers are tested outside Unity.
    /// The gun is at the HIP by default; the right mouse button brings it up to the sights (first person: the aligned sight / the sniper's scope, which need the gun fully up; third person: the arm along the crosshair) and a shot from the hip scatters wider. The gun's raise
    /// (<c>HeldGun.Raise</c>, 0 down .. 1 at the sights, eased by the gun itself) is <see cref="HipRaise"/> at the hip. The numbers are PLACEHOLDERS (the owner gave none).
    /// </summary>
    public static class AimRules
    {
        /// <summary>A shot from the hip is fired into a cone this many times the gun's own (aiming: 1).</summary>
        public const float HipSpread = 1.5f;

        /// <summary>How far up the gun is at the hip (0 down at the side, 1 at the sights): the animated pistol's raise clip is shown at this point, a long gun's arm is blended this far from its carried pose to the aim.</summary>
        public const float HipRaise = 0.5f;

        /// <summary>How long the gun takes to come up to the sights, and to go back to the hip (s): the raise runs at six a second (<c>HeldGun</c>), a full raise takes a sixth of a second.</summary>
        public const float AimSeconds = 0.17f;

        public static float SpreadFactor(bool aiming) => aiming ? 1f : HipSpread;

        /// <summary>The raise the gun is asked for: <see cref="HipRaise"/> at the hip, 1 at the sights.</summary>
        public static float RaiseFor(bool aiming) => aiming ? 1f : HipRaise;

        /// <summary>The aim share (0 hip .. 1 sights) a frame later: it moves at 1 / <see cref="AimSeconds"/> a second towards where it should be.</summary>
        public static float Ease(float share, bool aiming, float dt) => MoveTowards(share, aiming ? 1f : 0f, Math.Max(0f, dt) / AimSeconds);

        static float MoveTowards(float a, float b, float max) => Math.Abs(b - a) <= max ? b : a + Math.Sign(b - a) * max;

        /// <summary>The way a pose is blended: 0 at the hip, 1 at the sights, eased at both ends.</summary>
        public static float Smooth(float share) { float t = Math.Max(0f, Math.Min(1f, share)); return t * t * (3f - 2f * t); }
    }
}
