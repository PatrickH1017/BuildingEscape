using System.Collections.Generic;
using UnityEngine;

namespace BuildingEscape
{
    /// <summary>
    /// A vending machine of the Loading Docks (the owner, 2026-10-23: "Vending machines should be used to buy consumables"): E with the crosshair on it opens its screen (<see cref="VendingScreen"/>), where the four things the dealer used to sell - the coffee
    /// mug, the heavy stapler, the squeaky toy and the fire extinguisher, the game's only consumables - are bought; the dealer no longer sells them (<see cref="Vendor.OnOffer"/>). The machine is an <see cref="Interactable"/>, worked like the outpost's
    /// keypad: the crosshair on it, close enough. The purchase itself is the dealer's (<see cref="Vendor.BuyAtMachine"/>: the host decides in a networked run) and what is bought goes into the buyer's pack, or lies at the machine's tray when the pack has no room.
    /// </summary>
    public sealed class VendingMachine : Interactable
    {
        static readonly List<VendingMachine> machines = new List<VendingMachine>();

        /// <summary>The machines of the docks, in the order they were made (the docks are built the same way on every machine of a session, so a number means the same machine everywhere).</summary>
        public static IReadOnlyList<VendingMachine> Machines => machines;

        /// <summary>The machine with this number (its place in <see cref="Machines"/>), or null.</summary>
        public static VendingMachine ByNumber(int number) => number >= 0 && number < machines.Count ? machines[number] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { machines.Clear(); }

        /// <summary>Its number: its place in <see cref="Machines"/>.</summary>
        public int Number => machines.IndexOf(this);

        /// <summary>Where a bought item lies when the buyer's pack has no room: in front of the machine, low down, where its tray is.</summary>
        public Vector3 TrayPoint { get; private set; }

        /// <summary>Set up by the floor generator once the prop stands: its footprint says which way its front is; the aim box is the box of what is seen.</summary>
        public void Setup(DockVendingRules.Spot spot, float floorY)
        {
            spot.Front(out int fx, out int fz);
            TrayPoint = new Vector3(spot.x + fx * (spot.depth * 0.5f + 0.4f), floorY + 0.5f, spot.z + fz * (spot.depth * 0.5f + 0.4f));
            FitAimBoxToChildren();
        }

        protected override void OnEnable() { base.OnEnable(); if (!machines.Contains(this)) machines.Add(this); }
        protected override void OnDisable() { base.OnDisable(); machines.Remove(this); }

        /// <summary>A placeholder: the owner writes the words.</summary>
        public override string Prompt => "[E] Use the vending machine";

        public override void Use(WobblyPlayer who) { VendingScreen.Show(this); }
    }
}
