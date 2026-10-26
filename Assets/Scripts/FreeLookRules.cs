namespace BuildingEscape
{
    /// <summary>
    /// When the free look (Alt, or V where Alt is taken by the system) unlocks the camera from the way the body faces (the owner, 2026-10-23: "Clicking Alt should not unlock the camera view only when holding it"):
    /// the key has to be HELD for <see cref="HoldSeconds"/> before the view unlocks, so a tap on it changes nothing, and it locks again the moment the key is let go.
    /// Pure numbers, no Unity types, so the rule is tested outside the editor; <c>CameraFollow</c> keeps the time and asks.
    /// </summary>
    public static class FreeLookRules
    {
        /// <summary>How long the key must be held before the view unlocks (a tap is shorter than this).</summary>
        public const float HoldSeconds = 0.25f;

        /// <summary>The time the key has been held after <paramref name="dt"/> more seconds: counted up while it is down, back to nothing the moment it is up.</summary>
        public static float Held(float held, bool keyDown, float dt) => keyDown ? held + (dt > 0f ? dt : 0f) : 0f;

        /// <summary>Is the view unlocked after the key has been held this long?</summary>
        public static bool Unlocked(float held) => held >= HoldSeconds;
    }
}
