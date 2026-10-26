namespace BuildingEscape
{
    /// <summary>
    /// Which floors have the new art (the owner, 2026-10-23: "Please update floors 1-6 to use the assets the 7th floor is using, the style is much cleaner"): the POLYGON Office pack's wall pieces and ceiling tiles
    /// (<see cref="WallSkinRules"/>), the pack's doors (<see cref="DoorSkinRules"/>) and the environmental dressing (<see cref="OfficePlanner"/>, OfficePlanner.Dressing.cs). Floor 7 was the test floor; since the owner's go
    /// it is floors 1 to 7, and floors 8 and up keep the old look (not asked). One place to change: all three read this rule.
    /// </summary>
    public static class FloorArtRules
    {
        /// <summary>The first and the last floor that has the new art.</summary>
        public const int FirstFloor = 1, LastFloor = 7;

        /// <summary>Does this floor get the new art?</summary>
        public static bool AppliesTo(int floor) => floor >= FirstFloor && floor <= LastFloor;
    }
}
