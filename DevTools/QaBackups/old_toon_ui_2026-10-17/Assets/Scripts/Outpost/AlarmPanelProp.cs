using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// The alarm panel inside a guard outpost, on the wall beside the door: a steel box with a big red button. Point the crosshair at it and
    /// press E to switch the siren off and call the backup off, whether it was the outpost's lock that started it or an officer who spotted
    /// somebody (<see cref="OutpostController.HostStandDown"/>). You have to be inside the outpost to reach it, so the door has to be open.
    /// </summary>
    public sealed class AlarmPanelProp : Interactable
    {
        OutpostController controller;

        public void Init(OutpostController c, Bounds box)
        {
            controller = c;
            AimBox = box;
            reach = 2.8f;
        }

        public override bool Available => controller != null;

        public override string Prompt => controller != null ? controller.PanelPrompt : "";

        public override void Use(WobblyPlayer who)
        {
            if (controller != null) controller.UsePanel(who);
        }
    }
}
