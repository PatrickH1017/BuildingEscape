using System.Linq;
using BuildingEscape;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Pure checks (no scene) of the owner's list of 2026-10-23 (DEVELOPER_NOTES 2.63), batch D, T1: floors 1 to 7 have the Floor 7 art (walls and ceiling, doors, dressing), the other floors do not. Called from <c>NetDevTest.PolicyTest</c>;
    /// the dressing of floors 1, 3 and 6 is also audited like floor 7's in <c>FloorDressTest</c>, and the Play Mode half is <c>NetDevAsks1023E.cs</c>.
    /// </summary>
    public static partial class NetDevTest
    {
        public static void Asks1023ETest()
        {
            Check("floor art (T1): floors 1 to 7 have the Floor 7 art, the docks and floors 8 to 13 do not",
                Enumerable.Range(1, 7).All(FloorArtRules.AppliesTo) && !FloorArtRules.AppliesTo(0) && Enumerable.Range(8, 6).All(f => !FloorArtRules.AppliesTo(f)) && FloorArtRules.FirstFloor == 1 && FloorArtRules.LastFloor == 7);
            Check("floor art (T1): the walls and the ceiling, the doors and the dressing all read that one rule",
                Enumerable.Range(0, 14).All(f => WallSkinRules.AppliesTo(f) == FloorArtRules.AppliesTo(f) && DoorSkinRules.AppliesTo(f) == FloorArtRules.AppliesTo(f)) && OfficePlanner.DressedFloor == FloorArtRules.LastFloor);
        }
    }
}
