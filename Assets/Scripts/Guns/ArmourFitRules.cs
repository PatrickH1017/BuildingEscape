using System;

namespace BuildingEscape
{
    /// <summary>
    /// How the Synty Battle Royale body armour is fitted (the owner, 2026-10-23: "replace all body armors in our current game with the body armor from the asset packs ... the asset pack comes with wearable armor in different tiers already built in").
    /// Pure rules, free of Unity types so they can be tested outside it. The pack has three tiers of armour worn on the chest (SM_Chr_Attach_Male_Armor_01 to 03: a vest, a vest with plates, a heavy rig with shoulder pads) and three of the same
    /// armour lying flat to be picked up (SM_PickUp_Armor_01 to 03); they are the game's Light, Medium and Heavy (<see cref="ShieldTier"/>).
    /// A worn piece is modelled for a Synty body standing at the origin - the vest runs from the hips (y 0.94 m) to the base of the neck (1.50 m) and is 0.44 m across - and the game's people are not that size (the Tiny Toon people are not
    /// even that shape), so it is scaled to the person: as tall as the distance from his hips to his neck, as wide as the distance between his shoulders. The numbers marked "measured" are the model's vertex bounds read from the pack's FBX files;
    /// the reference body's (<see cref="RefHipsY"/>, <see cref="RefNeckY"/>, <see cref="RefShoulderWidth"/>) are the armour's own extent and an estimate of the Synty skeleton, not read from a rig.
    /// </summary>
    public static class ArmourFitRules
    {
        /// <summary>The reference body the worn models are made for: height of the hips and of the neck's base above the feet, and the width between the shoulder joints (metres).</summary>
        public const float RefHipsY = 0.94f, RefNeckY = 1.50f, RefShoulderWidth = 0.40f;

        /// <summary>A little wider and deeper than the body it covers, so the body does not poke through the armour; the least and most a model is ever scaled by (a rig that gives nonsense is held to these).</summary>
        public const float Margin = 1.04f, MinScale = 0.4f, MaxScale = 2.5f;

        /// <summary>The least distance (m) between a person's hips and neck that is taken as a body at all (below it the fit is refused and the old boxes are used).</summary>
        public const float MinTorso = 0.05f;

        static float Clamp(float v) => Math.Max(MinScale, Math.Min(MaxScale, v));

        /// <summary>
        /// The scale of a worn model for a person whose hips are <paramref name="hipsY"/> and neck <paramref name="neckY"/> high (world metres, he stands upright) and whose shoulder joints are <paramref name="shoulderWidth"/> apart (0 or less = unknown:
        /// the width follows the height). False when the body is no body (the neck not above the hips).
        /// </summary>
        public static bool WornScale(float hipsY, float neckY, float shoulderWidth, out float sx, out float sy, out float sz)
        {
            sx = sy = sz = 1f;
            float torso = neckY - hipsY;
            if (!(torso >= MinTorso)) return false;
            sy = Clamp(torso / (RefNeckY - RefHipsY));
            sx = shoulderWidth > 0.01f ? Clamp(Margin * shoulderWidth / RefShoulderWidth) : Clamp(Margin * sy);
            sz = sx;
            return true;
        }

        /// <summary>Where the model's origin (the reference body's feet) goes below the hips, in the person's own frame, once the model is scaled by <paramref name="sy"/>: so the model's lower edge lies on the hips.</summary>
        public static float WornDrop(float sy) => RefHipsY * sy;

        // ------------------------------------------------------------------ the armour lying about

        /// <summary>How big a piece lying about is next to the pack's model: the pieces are 0.44 to 0.60 m across, too big for the safes' 0.51 m deep hollow (<c>FloorSafe</c>), so they are shown at this share of their size.</summary>
        public const float PickupScale = 0.8f;

        /// <summary>The bounds of a pick-up model (SM_PickUp_Armor_01 to 03, measured: x across, y thick, z deep; the model lies flat), as a minimum corner and a maximum corner, before <see cref="PickupScale"/>.</summary>
        public static void PickupBounds(ShieldTier tier, out float minX, out float minY, out float minZ, out float maxX, out float maxY, out float maxZ)
        {
            switch (tier)
            {
                case ShieldTier.Heavy: minX = -0.30f; minY = -0.03f; minZ = -0.32f; maxX = 0.30f; maxY = 0.14f; maxZ = 0.34f; break;
                case ShieldTier.Medium: minX = -0.23f; minY = -0.02f; minZ = -0.29f; maxX = 0.23f; maxY = 0.09f; maxZ = 0.25f; break;
                default: minX = -0.22f; minY = -0.03f; minZ = -0.29f; maxX = 0.22f; maxY = 0.09f; maxZ = 0.29f; break;
            }
        }

        /// <summary>The size of a piece lying about (across, thick, deep) at <see cref="PickupScale"/>.</summary>
        public static void PickupSize(ShieldTier tier, out float across, out float thick, out float deep)
        {
            PickupBounds(tier, out float x0, out float y0, out float z0, out float x1, out float y1, out float z1);
            across = (x1 - x0) * PickupScale; thick = (y1 - y0) * PickupScale; deep = (z1 - z0) * PickupScale;
        }

        /// <summary>The middle of a pick-up model's bounds (before scaling): where the model must be moved from so that it sits in the middle of the piece's collider.</summary>
        public static void PickupCenter(ShieldTier tier, out float cx, out float cy, out float cz)
        {
            PickupBounds(tier, out float x0, out float y0, out float z0, out float x1, out float y1, out float z1);
            cx = (x0 + x1) * 0.5f; cy = (y0 + y1) * 0.5f; cz = (z0 + z1) * 0.5f;
        }

        /// <summary>How many parts a worn vest has under its "BodyArmour" object: one (the pack's model) when the pack's armour is in the weapon catalog, else the boxes of the old vest (1, 3 and 5).</summary>
        public static int VestParts(ShieldTier tier, bool packModels) => tier == ShieldTier.None ? 0 : packModels ? 1 : tier == ShieldTier.Light ? 1 : tier == ShieldTier.Medium ? 3 : 5;
    }
}
