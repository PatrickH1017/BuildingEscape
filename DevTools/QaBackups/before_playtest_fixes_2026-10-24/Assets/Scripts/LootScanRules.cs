using System;

namespace BuildingEscape
{
    /// <summary>
    /// The colour a marked thing glows on the scan (the owner, 2026-10-21: "I only want the high valuables to glow gold including the safes. Make medium tier loot blue and the lowest tier loot glows green" and "keep the weapons glowing red"):
    /// green for little, blue for the middle, gold for a lot and for every safe, red for the unique items (weapons, armour). See <see cref="LootScanRules.GlowOf"/>.
    /// </summary>
    public enum ScanGlow { Green = 0, Blue = 1, Gold = 2, Red = 3 }

    /// <summary>The room a point is in (any kind: an office, an open-plan area, a hallway, a lobby), as the loot scanner and the floor builder share it.</summary>
    public struct RoomSpot
    {
        /// <summary>The room's number in <see cref="Layout.rooms"/>; -1 when the point is in none.</summary>
        public int index;
        /// <summary>The room's world rectangle.</summary>
        public Rc bounds;
        /// <summary>The number on its door ("114", "101A"), or null for a room without one (a hallway, an open area).</summary>
        public string label;
        /// <summary>The planner's theme ("office", "conference", "hall", "lobby"...) and kind (0 room, 1 open area, 2 hallway or lobby).</summary>
        public string theme;
        public int kind;

        public bool Valid => index >= 0;
    }

    /// <summary>
    /// The numbers of the loot scanner (<see cref="LootScanner"/>), without Unity types so they are tested outside Unity. The owner, 2026-10-17: "Players should be able
    /// to hold a button to scan rooms for loot" and "Scanning should only work within the room and show items in that room". Holding <see cref="LootScanner.ScanKey"/>
    /// sends out a pulse from the player that races outwards at <see cref="PulseSpeed"/> through the room they are standing in and marks every valuable lying in
    /// THAT room (not the rooms next to it, however close: a player has to go in to scan a room); a mark stays for <see cref="MarkSeconds"/> and fades
    /// during the last <see cref="FadeSeconds"/>; while the key is held another pulse leaves every <see cref="PulseGap"/> seconds.
    /// </summary>
    public static class LootScanRules
    {
        public const float PulseSpeed = 28f, PulseGap = 2.4f;
        public const float MarkSeconds = 6f, FadeSeconds = 1.4f;
        /// <summary>No pulse goes further than this (m), whatever the room (the biggest room of a floor is 17.6 m long).</summary>
        public const float MaxReach = 30f;
        /// <summary>Things worth less than this are not marked (the mugs that can be thrown count $25 each).</summary>
        public const int MinValue = 60;

        /// <summary>How far out the pulse has got <paramref name="seconds"/> after it left.</summary>
        public static float RadiusAt(float seconds) => Math.Max(0f, seconds) * PulseSpeed;

        /// <summary>How far a pulse from (x, z) has to go to cover the whole of a room: to its farthest corner (and no further than <see cref="MaxReach"/>).</summary>
        public static float Reach(float x, float z, Rc room)
        {
            float dx = Math.Max(Math.Abs(x - room.x0), Math.Abs(x - room.x1)), dz = Math.Max(Math.Abs(z - room.z0), Math.Abs(z - room.z1));
            return Math.Min(MaxReach, (float)Math.Sqrt(dx * dx + dz * dz));
        }

        /// <summary>Has the pulse reached a thing <paramref name="distance"/> metres away?</summary>
        public static bool Reached(float distance, float radius) => distance <= radius;

        /// <summary>Is a thing of this value worth a mark?</summary>
        public static bool Worth(int value) => value >= MinValue;

        /// <summary>Does a scan from room <paramref name="mine"/> show a thing lying in room <paramref name="theirs"/>? Only the room itself (and never a point that is in no room).</summary>
        public static bool SameRoom(int mine, int theirs) => mine >= 0 && mine == theirs;

        /// <summary>
        /// A little more than the room (the owner, 2026-10-22: "increase the radius of the scanner by just a little bit to make it a tad bit easier to find loot"): a thing in ANOTHER room (or in none) is marked too when it lies
        /// this close (m) to the player, a little way through the walls. 0 gives the old rule back: the room only.
        /// </summary>
        public const float ExtraReach = 3f;

        /// <summary>Does a scan from the player mark a thing <paramref name="distance"/> metres away, lying in the player's room (<paramref name="sameRoom"/>) or not? The room, and anything within <see cref="ExtraReach"/>.</summary>
        public static bool InScan(bool sameRoom, float distance) => sameRoom || (ExtraReach > 0f && distance >= 0f && distance <= ExtraReach);

        /// <summary>What the scan calls the room: "ROOM 114" for a numbered one, "THE OPEN AREA", "THE LOBBY" or "THE HALLWAY" for the others.</summary>
        public static string RoomTitle(RoomSpot room)
        {
            if (!room.Valid) return "NO ROOM";
            if (!string.IsNullOrEmpty(room.label)) return "ROOM " + room.label;
            if (room.kind == 1) return "THE OPEN AREA";
            if (room.theme == "lobby" || room.theme == "service") return "THE LOBBY";
            return "THE HALLWAY";
        }

        /// <summary>How visible a mark that is <paramref name="age"/> seconds old is: 0 (none yet, or gone), 1 (fully), fading out at the end.</summary>
        public static float Alpha(float age)
        {
            if (age <= 0f || age >= MarkSeconds) return 0f;
            return age > MarkSeconds - FadeSeconds ? (MarkSeconds - age) / FadeSeconds : 1f;
        }

        /// <summary>How big a mark is, 0 to a little over 1 in the first moments (it pops up).</summary>
        public static float Pop(float age)
        {
            if (age <= 0f) return 0f;
            if (age >= 0.22f) return 1f;
            float t = age / 0.22f - 1f;                                    // (easeOutBack: 0 at the start, a little over 1 on the way, 1 at the end)
            return 1f + 2.70158f * t * t * t + 1.70158f * t * t;
        }

        /// <summary>
        /// How good a find is, from the floor's quota: 0 small, 1 good, 2 great, 3 jackpot (a typical valuable is worth about 3.5% of the quota; the
        /// tiers start at twice, four times and eight times that).
        /// </summary>
        public static int Tier(int value, int quota)
        {
            double unit = Math.Max(25.0, quota * 0.035);
            double r = value / unit;
            return r < 2.0 ? 0 : r < 4.0 ? 1 : r < 8.0 ? 2 : 3;
        }

        /// <summary>
        /// From which <see cref="Tier"/> a thing glows blue and from which gold (tiers: 0 small, 1 good, 2 great, 3 jackpot; a typical valuable is about 3.5% of the quota, so on floor 1 tier 0 is under $302, tier 1 is up to $602 and tier 2 up to $1,204).
        /// The one place to change if gold should be rarer: set <see cref="GoldFromTier"/> to 3 (and <see cref="BlueFromTier"/> to 2) and only the jackpots are gold.
        /// </summary>
        public const int BlueFromTier = 1, GoldFromTier = 2;

        /// <summary>
        /// The glow of a mark: a unique item (a weapon, armour) is red whatever it is worth, a safe is gold, and everything else by what it is worth now (<see cref="Tier"/> of <paramref name="value"/> on the floor's
        /// <paramref name="quota"/>): green under <see cref="BlueFromTier"/>, blue under <see cref="GoldFromTier"/>, gold from there up. Cash, the big treasures and the small valuables all go by this.
        /// </summary>
        public static ScanGlow GlowOf(bool uniqueItem, bool safe, int value, int quota)
        {
            if (uniqueItem) return ScanGlow.Red;
            if (safe) return ScanGlow.Gold;
            int tier = Tier(value, quota);
            return tier >= GoldFromTier ? ScanGlow.Gold : tier >= BlueFromTier ? ScanGlow.Blue : ScanGlow.Green;
        }

        /// <summary>A glow as red, green and blue (0 to 1): gold (1, .8, .18), blue (.3, .62, 1), green (.28, .92, .42), red (.95, .22, .22; the unique items' red of <see cref="ChecklistRules.Rgb"/>).</summary>
        public static void GlowRgb(ScanGlow glow, out float red, out float green, out float blue)
        {
            switch (glow)
            {
                case ScanGlow.Gold: red = 1.00f; green = 0.80f; blue = 0.18f; break;
                case ScanGlow.Blue: red = 0.30f; green = 0.62f; blue = 1.00f; break;
                case ScanGlow.Red: red = 0.95f; green = 0.22f; blue = 0.22f; break;
                default: red = 0.28f; green = 0.92f; blue = 0.42f; break;
            }
        }

        /// <summary>When can a pulse leave? Not at the docks (nothing to steal there), not with a screen open.</summary>
        public static bool CanScan(int floor, bool screenOpen, bool inCustody, bool cinematic) => floor >= 1 && !screenOpen && !inCustody && !cinematic;

        /// <summary>Is it time for another pulse (the key is held, the last one left <paramref name="sinceLast"/> seconds ago; negative: there was none)?</summary>
        public static bool NextPulse(bool holding, float sinceLast) => holding && (sinceLast < 0f || sinceLast >= PulseGap);

        /// <summary>The scan goes off only after the key has been held this long (the owner, 2026-10-23: "Scanning should have a 3 second delay"); letting go starts the wait over.</summary>
        public const float ChargeSeconds = 3f;
        public static bool Charged(float heldSeconds) => heldSeconds >= ChargeSeconds;

        /// <summary>How far the wait has got, 0 to 1 (the bar over the scan's status line).</summary>
        public static float ChargeFraction(float heldSeconds) => ChargeSeconds <= 0f ? 1f : Math.Min(1f, Math.Max(0f, heldSeconds / ChargeSeconds));
    }
}
