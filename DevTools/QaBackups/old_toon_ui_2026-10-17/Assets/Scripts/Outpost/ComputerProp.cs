using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The computer on the last monitoring desk of a guard outpost (it replaces the paper note, the owner, 2026-10-17). Point the crosshair at it and press E to use it
    /// (<see cref="ComputerScreen"/>): a log-in whose user name and password are on a post-it stuck to the monitor, then a page with a Safes tab that lists the room and the code of the
    /// floor's safe. What the computer knows is what the note used to say (<see cref="SafeRules.NoteFor"/>): which safe, and whether its code is on record. Purely local, like the note was:
    /// every player logs in and reads for himself, and what he learns goes into his own journal (<see cref="SafeIntel"/>).
    /// </summary>
    public sealed class ComputerProp : Interactable
    {
        /// <summary>The floor of the outpost the computer stands in, the floor of the safe it has a record of (0 = none), the run's seed for the safes, and whether the code is on record.</summary>
        public int outpostFloor, safeFloor, intelSeed;
        public bool givesCode;

        /// <summary>What the post-it on the monitor says.</summary>
        public ComputerRules.Login Credentials { get; private set; }

        public override string Prompt => "[E] Use the computer";

        public void Init(Bounds box, int outpost, int safe, int seed, bool code)
        {
            AimBox = box;
            outpostFloor = outpost; safeFloor = safe; intelSeed = seed; givesCode = code;
            Credentials = ComputerRules.CredentialsFor(seed, outpost);
            reach = 2.6f;
        }

        public override void Use(WobblyPlayer who) => ComputerScreen.Open(this, who);
    }
}
