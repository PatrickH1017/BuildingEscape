using UnityEngine;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner's request of 2026-10-16: "after the 4th floor players cannot continue to the 5th floor, basically making this a demo up to the
        /// 4th floor (the full game will remove this limitation)" and "after completion of the 4th floor the vendor should now sell the pew pistol
        /// for free". Pure checks of the elevator rules (nothing above floor 4 is ever a destination, from any floor in any state; lifting the
        /// limit brings the old rules back), of what the panel says, and of the vendor's list.
        /// </summary>
        public static void DemoRulesTest()
        {
            Check("demo: the tower ends at floor 4 for now", TowerRules.DemoLastFloor == 4);
            Check("demo: floors 1 and 2 still lead up once their quota is met, not before",
                TowerRules.UpFrom(1, true, 1) == 2 && TowerRules.UpFrom(2, true, 2) == 3 && TowerRules.UpFrom(1, false, 1) == -1 && TowerRules.UpFrom(2, false, 2) == -1);
            Check("demo: from the docks the elevator resumes at floors 1 to 4", TowerRules.UpFrom(0, false, 1) == 1 && TowerRules.UpFrom(0, false, 2) == 2
                && TowerRules.UpFrom(0, false, 3) == 3 && TowerRules.UpFrom(0, false, 4) == 4);
            Check("demo: from floor 4 there is no way up, finished or not", TowerRules.UpFrom(4, true, 5) == -1 && TowerRules.UpFrom(4, false, 4) == -1);
            Check("demo: with all four floors done the docks have nothing to go up to", TowerRules.UpFrom(0, false, 5) == -1 && TowerRules.UpFrom(0, true, 5) == -1
                && TowerRules.UpFrom(0, false, 13) == -1);

            bool never = true;
            for (int level = 0; level <= 13; level++)
                foreach (bool done in new[] { false, true })
                    for (int resume = 1; resume <= 14; resume++)
                    {
                        int up = TowerRules.UpFrom(level, done, resume);
                        never &= up == -1 || (up >= 1 && up <= TowerRules.DemoLastFloor);
                    }
            Check("demo: nothing above floor 4 is ever a destination, from any floor in any state", never);

            Check("demo: lifting the limit (a limit of 13) brings the old rules back",
                TowerRules.UpFrom(4, true, 5, 13) == 5 && TowerRules.UpFrom(0, false, 5, 13) == 5 && TowerRules.UpFrom(12, true, 13, 13) == -1 && TowerRules.UpFrom(3, true, 4, 13) == -1
                && !TowerRules.Beyond(13, 13) && !TowerRules.IsDemoEnd(4, 13) && !TowerRules.DemoOver(5, 13));
            Check("demo: floor 4 is the end of the demo and nothing else is", TowerRules.IsDemoEnd(4) && !TowerRules.IsDemoEnd(3) && !TowerRules.IsDemoEnd(5) && !TowerRules.IsDemoEnd(0));
            Check("demo: floors 5 and up are beyond it, the docks and floors 1-4 are not", TowerRules.Beyond(5) && TowerRules.Beyond(13) && !TowerRules.Beyond(4) && !TowerRules.Beyond(0) && !TowerRules.Beyond(1));
            Check("demo: it is over once the floor to climb to is 5", TowerRules.DemoOver(5) && !TowerRules.DemoOver(4) && !TowerRules.DemoOver(1));
            Check("demo: the other rules did not change (group ends, down, the boss floor)", TowerRules.IsGroupEnd(3) && TowerRules.IsGroupEnd(6) && TowerRules.DownFrom(4, true) == 0
                && TowerRules.UpFrom(13, true, 13) == -1 && TowerRules.ResumeAfterLeaving(4, true) == 5);

            Check("demo: the HUD counts the docks and floors 1-4 only (5 stops), or all 14 without the limit", TowerRules.DemoStops(14) == 5 && TowerRules.DemoStops(14, 13) == 14 && TowerRules.DemoStops(3) == 3);
            string above = ElevatorExtraction.WhyNot(5, 4, -1, 0), fromDocks = ElevatorExtraction.WhyNot(1, 0, -1, -1), high = ElevatorExtraction.WhyNot(9, 0, -1, -1);
            Check("demo: the panel says why floor 5 is closed (from floor 4 and from the docks)", above.Contains("demo") && ElevatorExtraction.WhyNot(5, 0, -1, -1).Contains("demo") && high.Contains("demo"));
            Check("demo: ... and that a finished floor is closed once the demo is over", fromDocks.Contains("closed"));
            Check("demo: ... and the old reasons are still given below the limit", ElevatorExtraction.WhyNot(3, 1, -1, 0).Contains("quota") && ElevatorExtraction.WhyNot(2, 4, -1, 0).Contains("Docks"));

            // ---- the vendor
            var c = Vendor.Catalog;
            Check("vendor: the pew is the eighth entry, for money (not free any more), a gun; a clip of ammunition follows it, then the ammunition of the other guns; the seven others are where they were", c.Length == 16 && Vendor.PewIndex == 7 && Vendor.AmmoIndex == 8 && c[7].name == "Pew Pistol" && c[7].price == Vendor.PewPrice && Vendor.PewPrice > 0 && c[7].isGun
                && c[8].isAmmo && c[8].price == Vendor.AmmoClipPrice && Vendor.AmmoClipPrice > 0 && c[8].rounds == RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].magazine && !c[8].isGun
                && c[0].name == "Coffee Mug" && c[0].price == 60 && c[4].name == "Crowbar" && c[4].price == 400 && c[6].name == "Hatchet" && c[6].price == 550 && !c[0].isGun && !c[4].isGun);
            Check("vendor: the pew is for sale once floor 3 is finished (the next floor is 4), not before", !Vendor.PewForSale(1) && !Vendor.PewForSale(3) && Vendor.PewForSale(4) && Vendor.PewForSale(5) && Vendor.PewForSale(13)
                && Vendor.PewAfterFloor == 3);
            Check("vendor: its weight is the pew's (1.3 lbs, half of what it was: the owner, 2026-10-23) and its name is the one the pickup has", Mathf.Approximately(RangedWeapon.Specs[(int)c[7].gun].weight, 0.6f)
                && RangedWeapon.Specs[(int)c[7].gun].name == "Pew");
        }
    }
}
