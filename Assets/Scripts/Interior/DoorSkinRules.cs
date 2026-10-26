using System;

namespace BuildingEscape
{
    /// <summary>What a floor 7 door is drawn as: one of the pack's solid doors (white, or the wooden one in the rooms with wooden furniture), or the pack's glass double door.</summary>
    public enum DoorLook { White, Wood, Glass }

    /// <summary>
    /// The Floor 7 doors (the owner, 2026-10-23: "update the doors on the 7th floor to match the new assets used around the floor such as the walls"): the leaves of the sliding doors, which were plain light-blue slabs, are drawn as the doors of
    /// the POLYGON Office pack. Pure code (no Unity types) so that it can be tested outside the editor: which floor, which door looks like what, how many of the pack's panels stand side by side in a leaf, and where each panel goes.
    /// A leaf is a unit cube that <c>SlidingDoor</c> scales to (width, height, <c>SlidingDoor.LeafThickness</c>) every frame, so a panel is fitted to that CUBE (centred, -0.5 to +0.5 on every axis): the door slides, swings
    /// and hides exactly as the old one did, its collider and its clearances are untouched, and the new look fills exactly the box the old leaf filled.
    /// </summary>
    public static class DoorSkinRules
    {
        /// <summary>The same floors as the walls and the ceiling (floors 1 to 7: <see cref="FloorArtRules"/>): the doors follow <see cref="WallSkinRules"/> (when the walls are shown with the pack's pieces, so are the doors).</summary>
        public static bool AppliesTo(int floor) => WallSkinRules.AppliesTo(floor);

        // the pack's door pieces (PropTable keys) and what they measure (from the prefabs, see PropTable; the floor builder checks the real ones against these)
        public const string KeyWhite = "skin_door_white", KeyWood = "skin_door_wood", KeyGlassL = "skin_door_glass_l", KeyGlassR = "skin_door_glass_r";
        /// <summary>SM_Bld_Door_02 (white, with a kick plate) and SM_Bld_Door_05 (wood): one model, 1.029 x 1.967 x 0.247 m (the handles stand out on both faces).</summary>
        public const float SingleW = 1.029f, SingleH = 1.967f, SingleD = 0.247f;
        /// <summary>SM_Bld_Door_Large_01_L / _R, the two leaves of the pack's double glass door: 0.974 x 2.452 x 0.172 m each (our doorways are 2 m wide and 2.45 m high).</summary>
        public const float LargeW = 0.974f, LargeH = 2.452f, LargeD = 0.172f;

        /// <summary>Rooms with a wooden door: the ones that are furnished like an office (wooden desks and tables). Every other room, restroom and store room has the white one.</summary>
        public static bool WoodenDoorTheme(string theme) => theme == "office" || theme == "conference" || theme == "meeting" || theme == "training";

        /// <summary>
        /// The look of the door of a doorway: the wide hall doors and the doors of the offices with a glass front are glass (the owner wanted those to be see-through); the others are solid, so what is behind a shut door stays hidden
        /// (white, or wood for the office-like rooms). <paramref name="roomTheme"/> is the theme of the room the doorway belongs to (null when it is not known).
        /// </summary>
        public static DoorLook LookFor(string roomTheme, bool glassOffice, bool wide)
        {
            if (wide || glassOffice) return DoorLook.Glass;
            return WoodenDoorTheme(roomTheme) ? DoorLook.Wood : DoorLook.White;
        }

        /// <summary>The width of one panel of a look.</summary>
        public static float PanelWidth(DoorLook look) => look == DoorLook.Glass ? LargeW : SingleW;

        /// <summary>How many of the pack's panels stand side by side in a leaf that is <paramref name="leafWidth"/> metres wide (at least one): a 2 m doorway has a panel in each leaf, the 4.8 m hall doorway two.</summary>
        public static int Panels(float leafWidth, float panelWidth) => Math.Max(1, (int)Math.Round(leafWidth / panelWidth, MidpointRounding.AwayFromZero));

        /// <summary>
        /// Is this panel of the glass double door the pack's RIGHT leaf (else the left one)? Counted across the whole door from its left end (the low leaf holds panels 0 .. n-1, the high leaf n .. 2n-1, n = panels per leaf) the
        /// panels alternate left, right, left, right: a doorway with a panel in each leaf gets the pack's own pair, a wide one two pairs.
        /// </summary>
        public static bool UseRight(int side, int panelIndex, int panelsPerLeaf) => (((side > 0 ? panelsPerLeaf : 0) + panelIndex) & 1) == 1;

        /// <summary>
        /// Where a solid panel stands in the leaf: turned round across the leaf (mirrored) for the LOW leaf (side -1), so that the handles of a pair meet in the middle of the doorway and the hinges are at the jambs.
        /// The pack's door has its hinges on one edge and its handle on the other, the same on both faces.
        /// </summary>
        public static bool Mirrored(DoorLook look, int side) => look != DoorLook.Glass && side < 0;

        /// <summary>The fit of one panel in a leaf's unit cube: the scale of the model (negative x when mirrored) and where its pivot goes, both in the leaf's own units.</summary>
        public struct Fit { public float sx, sy, sz, px, py, pz; }

        /// <summary>
        /// Panel <paramref name="index"/> of <paramref name="panels"/> in a leaf, for a model that measures <paramref name="w"/> x <paramref name="h"/> x <paramref name="d"/> metres with the middle of its bounds
        /// (<paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/>) away from its pivot. The panels fill the leaf's width side by side, the model fills its height, and its thickness is the leaf's (so the handles that
        /// stand out of the door fold into the box the old leaf filled). <paramref name="mirror"/> turns the model round across the leaf (x negative).
        /// </summary>
        public static Fit PanelFit(int index, int panels, bool mirror, float w, float h, float d, float cx, float cy, float cz)
        {
            float sx = 1f / panels / w, sy = 1f / h, sz = 1f / d;
            float slot = -0.5f + (index + 0.5f) / panels;
            float mx = mirror ? -sx : sx;
            return new Fit { sx = mx, sy = sy, sz = sz, px = slot - mx * cx, py = -sy * cy, pz = -sz * cz };
        }
    }
}
