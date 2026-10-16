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
            ("Crosshair", "The dot in the middle of the screen is what you act on and where you swing: aim it at an item, a door or the dealer. It turns yellow when E works what it is on, and red over a guard you can hit"),
            ("Alt (or V) hold", "Look around your character"),
            ("Shift", "Sprint"),
            ("Ctrl (or C) hold", "Crouch: slower, but your footsteps are much quieter"),
            ("Space", "Jump"),
            ("E", "Aim the crosshair, then press E: pick an item up into your backpack / open and close a door / talk to the vendor / hack a guard outpost's lock / read a note / work a safe"),
            ("W A S D at a lock", "While hacking an outpost's keypad: press the key that appears within one second - four in a row opens the door, a miss sets off the alarm. Esc gives up"),
            ("J", "Your journal: the floor safes you have learned about from notes (their room and, if a note gave it, their code)"),
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
            ("Guard outposts and safes", "Every floor has a guard outpost: the dark room with a long one-way window (a mirror from the corridor) and a keypad beside its steel door. " +
                "Aim at the keypad and press E to hack the lock: a key (W, A, S or D) appears and you have one second to press it, four times in a row. " +
                "A clean hack opens the door for good; a mistake sets off the alarm for a few seconds and calls backup, exactly as if a guard had spotted you. " +
                "Inside are desks with notes, weapons and valuables - different every run, and better the higher you climb. " +
                "About one floor in five hides a safe full of cash worth half of that floor's quota. A note in an outpost names the safe's room and sometimes its code " +
                "(read it with E; J opens your journal). Without a code you can pry the safe open with a crowbar, but the crowbar snaps 35% of the time. " +
                "Carry the cash bundles to the elevator to bank them. " +
                "On the wall inside every outpost, beside the door, is an alarm panel with a big red button: press E on it to shut the siren off and call the backup off, " +
                "whether the alarm was yours or an officer spotted somebody. No more officers come, and the radios stay jammed for ten seconds so they cannot call again at once - " +
                "but the officers who already arrived stay, and a later sighting calls backup again. You have to be inside, so a failed hack (which jams the lock) has to be retried first."),
            ("Finding a room", "Every door has a room number over it (101, 102 ... on floor 1; 201 ... on floor 2). Signs on the corridor walls say which rooms lie that way - " +
                "\"ROOMS 111-120\" and an arrow. The arrow points along the corridor you are in, towards the doorway that starts the shortest walk to the nearest of those rooms."),
        };
    }
}
