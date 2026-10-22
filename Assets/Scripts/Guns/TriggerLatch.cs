namespace BuildingEscape
{
    /// <summary>
    /// A click that begins while the gun cannot be used never becomes a shot (the owner, 2026-10-21: "When you click go on the elevator menu you fire a shot from your gun that should not be happening"). The elevator panel (like the shop, the pack
    /// or a menu) closes on the click while the mouse button is still held down; the next frame the gun is usable again, sees the button held, and fires. With the latch the button has to be let go of, and pressed again, before the gun works:
    /// a button that is down while the gun is not usable latches, and the latch opens only when the button is up. Pure: the controller asks it once a frame.
    /// </summary>
    public struct TriggerLatch
    {
        bool latched;

        /// <summary>Is the click that is still held a click that began while the gun could not be used?</summary>
        public bool Latched => latched;

        /// <summary>
        /// Once a frame: whether the gun may be used right now (no menu, the cursor in the game, the hands free) and whether the fire button is held down. True when a shot may be fired this frame (a held button repeats, as before).
        /// </summary>
        public bool Pull(bool usable, bool held)
        {
            if (!held) { latched = false; return false; }
            if (!usable) { latched = true; return false; }
            return !latched;
        }

        /// <summary>Forget the latch (a new gun, a new floor).</summary>
        public void Reset() { latched = false; }
    }
}