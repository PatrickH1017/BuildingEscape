using System;

namespace BuildingEscape.EditorTools
{
    public static partial class NetDevTest
    {
        /// <summary>
        /// The owner, 2026-10-23: "replace all body armors in our current game with the body armor from the asset packs". The pure rules of how the Synty Battle Royale armour is fitted (ArmourFitRules), on their own: a worn vest is scaled to the
        /// wearer's torso and shoulders, and the pieces lying about are shown at a size that fits the safes. The Play-mode half, with the real models, is the scenario <c>Armour</c> (NetDevArmour.cs).
        /// </summary>
        static void ArmourFitTest()
        {
            const float Eps = 1e-3f;
            bool ok = ArmourFitRules.WornScale(ArmourFitRules.RefHipsY, ArmourFitRules.RefNeckY, ArmourFitRules.RefShoulderWidth, out float sx, out float sy, out float sz);
            Check($"armour fit: on the reference body the vest is as tall as modelled and a little wider and deeper (x {sx:0.000}, y {sy:0.000}, z {sz:0.000})",
                ok && Math.Abs(sy - 1f) < Eps && Math.Abs(sx - ArmourFitRules.Margin) < Eps && Math.Abs(sz - sx) < Eps && ArmourFitRules.Margin > 1f && ArmourFitRules.Margin < 1.2f);

            ArmourFitRules.WornScale(0.47f, 0.75f, ArmourFitRules.RefShoulderWidth, out float hx, out float hy, out _);
            Check($"armour fit: a person half as tall at the torso gets a vest half as tall (y {hy:0.000}), the width following his shoulders alone (x {hx:0.000})", Math.Abs(hy - 0.5f) < Eps && Math.Abs(hx - ArmourFitRules.Margin) < Eps);

            ArmourFitRules.WornScale(0.94f, 1.50f, ArmourFitRules.RefShoulderWidth * 2f, out float wx, out float wy, out float wz);
            Check($"armour fit: shoulders twice as far apart make the vest twice as wide and as deep, the height unchanged (x {wx:0.000}, y {wy:0.000}, z {wz:0.000})",
                Math.Abs(wx - 2f * ArmourFitRules.Margin) < Eps && Math.Abs(wy - 1f) < Eps && Math.Abs(wz - wx) < Eps);

            ArmourFitRules.WornScale(0.94f, 1.50f, 0f, out float ux, out float uy, out _);
            ArmourFitRules.WornScale(0.47f, 0.75f, -1f, out float vx, out float vy, out _);
            Check("armour fit: with no shoulder width the width follows the height", Math.Abs(ux - ArmourFitRules.Margin * uy) < Eps && Math.Abs(vx - ArmourFitRules.Margin * vy) < Eps);

            bool huge = ArmourFitRules.WornScale(0f, 100f, 50f, out float bx, out float by, out float bz);
            bool tiny = ArmourFitRules.WornScale(0f, 0.06f, 0.02f, out float tx, out float ty, out float tz);
            Check("armour fit: nonsense is held to the limits (never beyond " + ArmourFitRules.MinScale + " to " + ArmourFitRules.MaxScale + ")",
                huge && Math.Abs(bx - ArmourFitRules.MaxScale) < Eps && Math.Abs(by - ArmourFitRules.MaxScale) < Eps && Math.Abs(bz - ArmourFitRules.MaxScale) < Eps && tiny && Math.Abs(ty - ArmourFitRules.MinScale) < Eps && tx >= ArmourFitRules.MinScale);

            Check("armour fit: a neck that is not above the hips, or a body shorter than " + ArmourFitRules.MinTorso + " m, is refused (the old boxes are built)",
                !ArmourFitRules.WornScale(1f, 1f, 0.4f, out _, out _, out _) && !ArmourFitRules.WornScale(1.2f, 1.0f, 0.4f, out _, out _, out _) && !ArmourFitRules.WornScale(1f, 1.02f, 0.4f, out _, out _, out _) && ArmourFitRules.WornScale(1f, 1.06f, 0.4f, out _, out _, out _));

            Check($"armour fit: the vest's lower edge goes on the hips: its origin is {ArmourFitRules.WornDrop(1f):0.00} m below them at full size and in proportion when scaled",
                Math.Abs(ArmourFitRules.WornDrop(1f) - ArmourFitRules.RefHipsY) < Eps && Math.Abs(ArmourFitRules.WornDrop(0.5f) - ArmourFitRules.RefHipsY * 0.5f) < Eps && ArmourFitRules.WornDrop(2f) > ArmourFitRules.WornDrop(1f));

            // the pieces lying about: the pack's measured bounds, shown at a share of their size; each must fit the weapon safe's hollow (0.51 m deep, 0.78 m wide: FloorSafe) turned the way FloorSafe turns it (across along the depth)
            bool sizes = true, fits = true, grows = true, centred = true; string why = "";
            float lastAcross = 0f;
            foreach (var tier in new[] { ShieldTier.Light, ShieldTier.Medium, ShieldTier.Heavy })
            {
                ArmourFitRules.PickupBounds(tier, out float x0, out float y0, out float z0, out float x1, out float y1, out float z1);
                ArmourFitRules.PickupSize(tier, out float across, out float thick, out float deep);
                ArmourFitRules.PickupCenter(tier, out float cx, out float cy, out float cz);
                sizes &= Math.Abs(across - (x1 - x0) * ArmourFitRules.PickupScale) < Eps && Math.Abs(thick - (y1 - y0) * ArmourFitRules.PickupScale) < Eps && Math.Abs(deep - (z1 - z0) * ArmourFitRules.PickupScale) < Eps && across > 0f && thick > 0f && deep > 0f;
                centred &= Math.Abs(cx - (x0 + x1) * 0.5f) < Eps && Math.Abs(cy - (y0 + y1) * 0.5f) < Eps && Math.Abs(cz - (z0 + z1) * 0.5f) < Eps;
                if (!(across <= 0.51f - 0.02f && deep <= 0.78f - 0.02f)) { fits = false; why += $" {tier}: {across:0.00} x {deep:0.00}"; }
                if (across < lastAcross - Eps) grows = false;
                lastAcross = across;
            }
            Check("armour fit: a piece lying about is the pack's measured size at the pickup scale (" + ArmourFitRules.PickupScale + "), its middle is the middle of its bounds", sizes && centred && ArmourFitRules.PickupScale > 0.3f && ArmourFitRules.PickupScale <= 1f);
            Check("armour fit: every tier fits the weapon safe's hollow (0.51 m deep, 0.78 m wide) and each tier is at least as wide as the one before" + (why.Length > 0 ? " (too big:" + why + ")" : ""), fits && grows);

            Check("armour fit: the reference body is the one the pack's vests were measured on (hips 0.94 m, neck 1.50 m: the vest's own extent; shoulders 0.40 m), and the limits are sane (0.3 to 0.5 at least, 3 at most)",
                Math.Abs(ArmourFitRules.RefHipsY - 0.94f) < Eps && Math.Abs(ArmourFitRules.RefNeckY - 1.50f) < Eps && Math.Abs(ArmourFitRules.RefShoulderWidth - 0.40f) < Eps && ArmourFitRules.MinScale >= 0.3f && ArmourFitRules.MinScale <= 0.5f && ArmourFitRules.MaxScale >= 2f && ArmourFitRules.MaxScale <= 3f);

            Check("armour fit: a vest has no parts with no armour, one part when the pack's models are used, the old boxes' 1, 3 and 5 when not",
                ArmourFitRules.VestParts(ShieldTier.None, true) == 0 && ArmourFitRules.VestParts(ShieldTier.None, false) == 0 &&
                ArmourFitRules.VestParts(ShieldTier.Light, true) == 1 && ArmourFitRules.VestParts(ShieldTier.Medium, true) == 1 && ArmourFitRules.VestParts(ShieldTier.Heavy, true) == 1 &&
                ArmourFitRules.VestParts(ShieldTier.Light, false) == 1 && ArmourFitRules.VestParts(ShieldTier.Medium, false) == 3 && ArmourFitRules.VestParts(ShieldTier.Heavy, false) == 5);
        }
    }
}
