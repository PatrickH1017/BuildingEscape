namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner, 2026-10-22: "Make all guards vision cones off by default to players but still work the same way. Put a check box in the test menu that lets us toggle it on and off so we can see their vision as we dev". The pure rule of whether the
        /// cone's picture is drawn (GuardRules.ConeDrawn), on its own: never while the switch is off (whatever the camera sees), when it is on only for a guard who is up, near and in view, and a test can force it. The Play-mode half, with real guards and the
        /// real flashlight, is the scenario <c>Cones</c> (NetDevCones.cs).
        /// </summary>
        static void GuardConesTest()
        {
            // (awake, near, shown, showCones, forced)
            bool offWhateverTheCameraSees = !GuardRules.ConeDrawn(true, true, 1f, false, false) && !GuardRules.ConeDrawn(true, true, 0.5f, false, false) && !GuardRules.ConeDrawn(true, true, 0.02f, false, false);
            Check("guard cones: with the switch off (the default) the cone is not drawn, even for a guard who is up, near and fully in view", offWhateverTheCameraSees);
            Check("guard cones: with the switch on the cone is drawn for a guard who is up, near and in view",
                GuardRules.ConeDrawn(true, true, 1f, true, false) && GuardRules.ConeDrawn(true, true, 0.02f, true, false));
            Check("guard cones: ... and not for one who is down or stunned, far from the camera or out of view (a wall, behind the camera: nothing shown)",
                !GuardRules.ConeDrawn(false, true, 1f, true, false) && !GuardRules.ConeDrawn(true, false, 1f, true, false) && !GuardRules.ConeDrawn(true, true, 0f, true, false) && !GuardRules.ConeDrawn(true, true, 0.01f, true, false));
            Check("guard cones: the tests' hook forces it on with the switch off (they read the cone whatever the camera sees), but still not for a guard who is down, far or out of view",
                GuardRules.ConeDrawn(true, true, 1f, false, true) && !GuardRules.ConeDrawn(false, true, 1f, false, true) && !GuardRules.ConeDrawn(true, false, 1f, false, true) && !GuardRules.ConeDrawn(true, true, 0f, false, true));
        }
    }
}
