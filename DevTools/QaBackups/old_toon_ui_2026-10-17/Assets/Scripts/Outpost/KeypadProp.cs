using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The keypad on the wall beside the guard outpost's door. Point the crosshair at it and press E to start the lock hack (the same as E
    /// on the door itself). Its lit parts show the state of the lock (<see cref="OutpostController"/>). Once the lock is open it does nothing.
    /// </summary>
    public sealed class KeypadProp : Interactable
    {
        OutpostController controller;

        public void Init(OutpostController c, Bounds box)
        {
            controller = c;
            AimBox = box;
            reach = 2.8f;
        }

        public override bool Available => controller != null && !controller.Unlocked;

        public override string Prompt => controller != null ? (controller.LockPrompt ?? "") : "";

        public override void Use(WobblyPlayer who)
        {
            if (controller != null) controller.UseLockedDoor(who);
        }
    }
}
