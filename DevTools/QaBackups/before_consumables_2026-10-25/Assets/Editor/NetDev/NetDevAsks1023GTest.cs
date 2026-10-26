using System.Linq;
using BuildingEscape;
using BuildingEscape.Net;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch D, T5: the walkie talkie the Vendor sells and the player holds out to use the radio. Called from <c>NetDevTest.PolicyTest</c>; the Play Mode half is <c>NetDevAsks1023G.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023GTest()
        {
            Check("walkie (T5): it has a price, a weight that fits the pack, a size and a name (placeholders for the owner, except the name: his own words)",
                WalkieRules.Price > 0 && WalkieRules.Weight > 0f && WalkieRules.Weight < 3f && WalkieRules.ModelHeight > 0.05f && WalkieRules.ModelHeight < 0.5f && WalkieRules.Name == "Walkie Talkie");
            int idx = Vendor.WalkieIndex;
            Check("walkie (T5): the Vendor lists it at its price, it is always on offer, and the entries before it kept their places",
                idx == Vendor.Catalog.Length - 1 && Vendor.Catalog[idx].isWalkie && Vendor.Catalog[idx].price == WalkieRules.Price && Vendor.OnOffer(idx, 1) && Vendor.Catalog[Vendor.PewIndex].isGun && Vendor.Catalog[Vendor.AmmoIndex].isAmmo);
            Check("walkie (T5): two players who both hold a walkie talkie out hear each other at any distance, one who does not hears nobody from afar (the voice rules it plugs into)",
                VoiceRules.RouteFor(500f, true, true) == VoiceRules.Route.Radio && VoiceRules.RouteFor(500f, true, false) == VoiceRules.Route.None && VoiceRules.RouteFor(500f, false, true) == VoiceRules.Route.None && VoiceRules.RouteFor(2f, false, false) == VoiceRules.Route.Near);
        }
    }
}
