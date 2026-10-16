using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The screens of the guard outpost: the lock hack, the safe's keypad, a note on a desk and the journal of notes. While one of them
    /// is up the player stands still and nothing else answers E, Tab or the mouse buttons (the other screens - the shop, the elevator
    /// panel, the backpack - ask <see cref="Open"/> the same way they ask each other), and Esc closes the screen instead of opening the menu.
    /// Purely local: nothing here is sent over the network.
    /// </summary>
    public static class OutpostUi
    {
        public enum Kind { None, Hack, Safe, Note, Journal }

        /// <summary>The screen that is up (None while there is none).</summary>
        public static Kind Current { get; private set; }

        /// <summary>Is one of the outpost's screens up?</summary>
        public static bool Open => Current != Kind.None;

        /// <summary>The screens that are worked with the mouse: the cursor is free while one is up. (The hack only needs the keyboard.)</summary>
        public static bool FreeCursor => Current == Kind.Safe || Current == Kind.Note || Current == Kind.Journal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Current = Kind.None; }

        /// <summary>A screen came up. Only one at a time: the one that was up is replaced (its owner finds out through <see cref="Current"/>).</summary>
        public static void Show(Kind kind) { Current = kind; }

        /// <summary>A screen went away (ignored if another one has taken its place).</summary>
        public static void Hide(Kind kind)
        {
            if (Current == kind) Current = Kind.None;
        }
    }
}
