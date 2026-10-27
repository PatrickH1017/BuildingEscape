using System;
using System.Linq;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch C part 2: the vending machines that sell the consumables (T6) and the two black machines of the main room (T23). Called from <c>NetDevTest.PolicyTest</c>;
    /// the Play Mode half is <c>NetDevAsks1023V.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023VTest()
        {
            // ---- T6: the four consumables are the vending machines', and nothing else moved in the dealer's list
            var cat = Vendor.Catalog;
            var sold = Vendor.MachineProducts;
            Check("vending (T6): the four throwables keep their places and prices in the dealer's list (coffee mug $60, heavy stapler $250, squeaky toy $150, fire extinguisher $600)",
                cat[0].name == "Coffee Mug" && cat[0].price == 60 && cat[1].name == "Heavy Stapler" && cat[1].price == 250 && cat[2].name == "Squeaky Toy" && cat[2].price == 150 && cat[3].name == "Fire Extinguisher" && cat[3].price == 600);
            Check("vending (2026-10-27): the machines sell the consumables and nothing else, in the list's order; the four throwables are sold neither there nor by the dealer; the dealer still offers the three melee weapons",
                sold.SequenceEqual(Enumerable.Range(0, cat.Length).Where(i => cat[i].isConsumable)) && sold.Length == ConsumableRules.Count && sold.All(i => cat[i].machine && Vendor.OnMachine(i))
                && Enumerable.Range(0, 4).All(i => !Vendor.OnOffer(i, 1) && !Vendor.OnOffer(i, 13) && !Vendor.OnMachine(i)) && Enumerable.Range(4, 3).All(i => Vendor.OnOffer(i, 1) && !Vendor.OnMachine(i)));
            Check("vending (T6): the dealer's other entries keep their places (the pew 7, its bullets 8, 17 entries up to the walkie talkie, the five consumables of 2026-10-25 come after it)", Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8 && cat.Length == 17 + ConsumableRules.Count && Vendor.WalkieIndex == 16 && cat[Vendor.PewIndex].isGun && cat[Vendor.AmmoIndex].isAmmo && !Vendor.OnMachine(-1) && !Vendor.OnMachine(99));

            // ---- T23: both machines of the main room are the black one: two machines of one size stand side by side, centred on the vendor, against the wall, not overlapping
            var black = PropTable.Get("vending");
            DockVendingRules.MainRoom(40f, 0.2f, black.W, black.D, black.W, black.D, out var first, out var second);
            Check("vending (T23): two black machines stand on the main room's south wall: the same size, side by side by the gap, centred on the vendor, not overlapping, both facing north",
                Math.Abs(first.width - second.width) < 1e-4f && Math.Abs(first.depth - second.depth) < 1e-4f && !DockVendingRules.Overlap(first, second) && Math.Abs((first.XMin + second.XMax) * 0.5f - 40f) < 1e-3f
                && Math.Abs((second.XMin - first.XMax) - DockVendingRules.SideGap) < 1e-3f && first.yaw == 0f && second.yaw == 0f && Math.Abs(first.ZMin - second.ZMin) < 1e-4f);
        }
    }
}
