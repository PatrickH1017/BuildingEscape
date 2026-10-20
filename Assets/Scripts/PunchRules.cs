namespace BuildingEscape
{
    /// <summary>
    /// Punching (the owner's task list of 2026-10-18: "Add punching"): with empty hands the left mouse button throws a punch, a short quick blow that stuns a guard for a moment - enough to free a friend he is
    /// holding down, to cut his radio call and to get away - but weaker than any weapon and never counting towards knocking him out. Plain numbers and one rule, no Unity types, so they are tested outside Unity;
    /// <see cref="MeleeController"/> does the punching and <see cref="Net.RunSync"/> tells the teammates (they see it, and hear it when they are near).
    /// </summary>
    public static class PunchRules
    {
        /// <summary>The hit zone: a sphere of <see cref="Radius"/> metres this far in front of the player (a weapon's is 1.4 m round a point 1.2 m ahead).</summary>
        public const float Reach = 1.0f, Radius = 1.0f;
        /// <summary>Seconds from the start of the blow to the moment it lands, the time between two punches, and how much faster than a slash the arm goes.</summary>
        public const float HitDelay = 0.15f, Cooldown = 0.55f, AnimSpeed = 1.6f;
        /// <summary>What a punch does: the stun of a guard (a weapon stuns 3-6 s), the shove, the noise guards hear (a hit, a miss; a weapon makes 14-18) and the health it takes off the training dummy.</summary>
        public const float StunSeconds = 1.4f, Knock = 3f, Noise = 9f, MissNoise = 3f, Damage = 6f;

        /// <summary>
        /// May a punch be thrown now? Only with nothing in the hands (no weapon, no gun, nothing to throw equipped, nothing carried), with no menu up, not pinned or busted, not in the middle of another blow
        /// and once the cooldown is over.
        /// </summary>
        public static bool CanPunch(bool nothingEquipped, bool handsFree, bool menuOpen, bool inCustody, bool busy, float now, float nextAt) =>
            nothingEquipped && handsFree && !menuOpen && !inCustody && !busy && now >= nextAt;
    }
}
