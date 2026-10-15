namespace BuildingEscape
{
    /// <summary>
    /// Everything the How to Play page shows, in one place. The controls list mirrors the real key
    /// bindings in the scripts (keep it in step if a binding changes). The explanatory sections are
    /// placeholders for the game's own wording.
    /// </summary>
    public static class HowToPlayContent
    {
        /// <summary>Key, what it does.</summary>
        public static readonly (string key, string action)[] Controls =
        {
            ("W A S D", "Move"),
            ("Mouse", "Look / turn"),
            ("Alt (or V) hold", "Look around your character"),
            ("Shift", "Sprint"),
            ("Space", "Jump"),
            ("E", "Pick up into your backpack / talk to the vendor"),
            ("Tab", "Open your inventory"),
            ("Q", "Grab or drop heavy loot (more people carry heavier things)"),
            ("Left mouse", "Throw what you hold / swing your weapon"),
            ("H", "Hide at a desk"),
            ("F", "Use the elevator panel (click a floor, then GO) / sell to the vendor"),
            ("G", "Hidden elevator to the boss: go (everyone must be inside)"),
            ("M", "Mute your microphone"),
            ("T", "Push to talk (when set in Settings)"),
            ("1 - 7", "Buy from the vendor's shop"),
            ("Esc", "Menu"),
        };

        /// <summary>Title, body. Replace the bodies with the real text.</summary>
        public static readonly (string title, string body)[] Sections =
        {
            ("The goal", "[Write the goal of the game here.]"),
            ("Working together", "[Write how teammates cooperate here.]"),
            ("The guards", "[Write how guards notice you and what to do about it here.]"),
            ("The elevator and the Loading Docks", "[Write how floors, the elevator and the Loading Docks work here.]"),
        };
    }
}