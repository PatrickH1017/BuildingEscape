using System;

namespace BuildingEscape
{
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

        /// <summary>When can a pulse leave? Not at the docks (nothing to steal there), not with a screen open.</summary>
        public static bool CanScan(int floor, bool screenOpen, bool inCustody, bool cinematic) => floor >= 1 && !screenOpen && !inCustody && !cinematic;

        /// <summary>Is it time for another pulse (the key is held, the last one left <paramref name="sinceLast"/> seconds ago; negative: there was none)?</summary>
        public static bool NextPulse(bool holding, float sinceLast) => holding && (sinceLast < 0f || sinceLast >= PulseGap);
    }
}
