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
            ("E", "Aim the crosshair, then press E: pick an item up into your backpack / open and close a door / talk to the vendor / hack a guard outpost's lock / use a guard outpost's computer / work a safe"),
            ("W A S D at a lock", "While hacking an outpost's keypad: press the key that appears within one second - four in a row opens the door, a miss sets off the alarm. Esc gives up"),
            ("J", "Your journal: the safes of the floor you are on that you have learned about from the outpost's computer (their room and, if the computer had it, their code)"),
            ("Z hold", "Scan for loot: a pulse sweeps the room you are standing in and marks the valuables lying in it (you have to go in to scan a room) with what they are, what they are worth and how far; the top of the screen says what the room holds. Not at the Loading Docks or in the elevator"),
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
                "Inside are desks with a computer, weapons and valuables - different every run, and better the higher you climb. " +
                "Every floor hides a safe: on floors 1 and 2 it is full of cash worth half of that floor's quota, from floor 3 up there are several (up to four), each with a pistol inside, and sometimes a little cash and a spare clip (each is picked up on its own). The computer in an outpost (its log-in is on a post-it on the monitor; click its Safes tab) lists this floor's safes - each one's room, and sometimes its code " +
                "(use it with E; J opens your journal). Without a code you can pry the safe open with a crowbar, but the crowbar snaps 35% of the time. " +
                "Carry the cash bundles to the elevator to bank them. " +
                "Shutting the outpost's door locks nothing - the officers open it like any other door - but a door you lock with the code stays shut for them too: at the computer's terminal (under the Nav map, or on the Hacked tab) type SECURITY to lock yourself in. Somebody outside has to hack the keypad again to open it (typing SECURITY at the computer again, or E on the door from inside, undoes it yourself). " +
                "On the computer, the Nav tab is a map of the floor (a ring spreads from every player and officer who moves, a padlock marks a locked room, the wheel zooms it, dragging moves it, pointing at a room names it and a click opens its card), and its terminal (under the map; the Hacked tab is the same terminal, bigger) locks and unlocks the doors of any room: type a room number and its doors lock, or unlock if they are locked."),
            ("Finding a room", "Every door has a room number over it (101, 102 ... on floor 1; 201 ... on floor 2). Signs on the corridor walls say which rooms lie that way - " +
                "\"ROOMS 111-120\" and an arrow. The arrow points along the corridor you are in, towards the doorway that starts the shortest walk to the nearest of those rooms. " +
                "Dark red signs that say \"SECURITY OFFICE\" with an arrow lead the way to the guard outpost the same way."),
        };
    }
}
