namespace BuildingEscape
{
    /// <summary>
    /// The safety net under a player's body (the friends' playtest, 2026-10-23: "a player was pushed to the roof of the building, out of the map, because too many guards pushed him into a corner"). Officers' colliders squeezing the body from all sides make the
    /// physics throw it out along the one free way, up, through the thin ceiling. Three rules, pure numbers: the body may never be pushed out of an overlap faster than <see cref="MaxDepenetrationSpeed"/>, may never move up faster than <see cref="MaxUpSpeed"/>
    /// (a jump is 5.5), and a body found outside the building (<see cref="Outside"/>) is put back at the last good spot it stood on (<see cref="WorthRemembering"/>) or at the floor's elevator.
    /// </summary>
    public static class PlayerSafetyRules
    {
        /// <summary>The fastest the physics may push the body out of something it overlaps (m/s; the engine's default is 10).</summary>
        public const float MaxDepenetrationSpeed = 1.5f;
        /// <summary>The fastest the body may go up (m/s): a jump is 5.5 (<c>WobblyPlayer.jumpImpulse</c>).</summary>
        public const float MaxUpSpeed = 6f;
        /// <summary>The body's middle is never higher than this above the floor in the building: a floor's walls are 3.5 m high, a standing body's middle is about 1 m up and a jump takes it to about 2.5.</summary>
        public const float MaxHeight = 3.1f;
        /// <summary>... and never lower than this (fallen through the floor).</summary>
        public const float MinHeight = -2.5f;
        /// <summary>How often a good spot is remembered while the body stands (s).</summary>
        public const float RememberEvery = 0.4f;

        /// <summary>Is the body's middle at this height outside the building (or not a number)?</summary>
        public static bool Outside(float y) => float.IsNaN(y) || y > MaxHeight || y < MinHeight;

        /// <summary>Is this a spot to come back to: standing on the ground at a normal height and not being thrown about?</summary>
        public static bool WorthRemembering(bool grounded, float y, float speed) => grounded && y > -0.2f && y < 1.6f && speed < 7f;
    }
}
