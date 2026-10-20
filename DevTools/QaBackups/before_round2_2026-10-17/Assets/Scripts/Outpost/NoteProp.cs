using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A note about a floor safe, lying on a desk in a guard outpost. Point the crosshair at it and press E to read it; it stays on the desk
    /// (everyone can read it) and what it says goes into the reader's journal (<see cref="SafeIntel"/>). The note says which room the safe
    /// is in - worked out when it is read, because the safe may be on a floor that has not been built yet - and gives the code or, when
    /// nobody knows it, says that a crowbar is needed.
    /// </summary>
    public sealed class NoteProp : Interactable
    {
        /// <summary>The floor of the outpost the note lies in, the floor of the safe it is about, the run's seed for the safes, and whether it gives the code.</summary>
        public int outpostFloor, safeFloor, intelSeed;
        public bool givesCode;

        public override string Prompt => "[E] Read the note";

        public void Init(Bounds box, int outpost, int safe, int seed, bool code)
        {
            AimBox = box;
            outpostFloor = outpost; safeFloor = safe; intelSeed = seed; givesCode = code;
            reach = 2.6f;
        }

        public override void Use(WobblyPlayer who)
        {
            var gen = TowerManager.Instance != null ? TowerManager.Instance.generator : null;
            string room = gen != null ? gen.SafeRoomLabel(safeFloor) : null;
            int code = givesCode ? SafeRules.Code(intelSeed, safeFloor) : 0;
            string body = SafeNotes.Body(intelSeed, outpostFloor, safeFloor, room, code);
            bool learned = SafeIntel.Add(safeFloor, room, code);
            GameAudio.PlaySound(SoundId.Paper, transform.position, 0.7f, 1f, true, 12f, 2f);
            IntelScreens.ShowNote(SafeNotes.Title(code), body, learned, safeFloor);
        }
    }
}
