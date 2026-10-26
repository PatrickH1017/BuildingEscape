using System;

namespace BuildingEscape
{
    /// <summary>
    /// What an eliminated guard leaves behind (the owner, 2026-10-23: "Eliminated guards should drop 0-3 bullets with 0 being more like then 1 then 2 then 3 being the least likely" and "A player should have to run over the eliminated guard to get the dropped bullets").
    /// Pure numbers, no Unity types, so the odds and the distance are tested outside the editor: the chance of each count, the roll that picks one, and how close a player has to come to take the bullets.
    /// </summary>
    public static class GuardDropRules
    {
        /// <summary>The most bullets a guard leaves.</summary>
        public const int MaxBullets = 3;

        /// <summary>The chance, in percent, of 0, 1, 2 and 3 bullets: none is the most likely and three the least likely (they add up to 100). One place to change the odds.</summary>
        public static readonly int[] Percent = { 40, 30, 20, 10 };

        /// <summary>The number of bullets for a roll of 0 to 99 (anything outside is clamped): with the odds above 0-39 leaves none, 40-69 one, 70-89 two and 90-99 three.</summary>
        public static int Bullets(int roll)
        {
            int r = Math.Max(0, Math.Min(99, roll));
            int edge = 0;
            for (int n = 0; n < Percent.Length; n++)
            {
                edge += Percent[n];
                if (r < edge) return n;
            }
            return MaxBullets;
        }

        /// <summary>How close, flat on the floor, a player has to come to the bullets to take them: running over the fallen guard (metres).</summary>
        public const float RunOverRadius = 0.9f;

        /// <summary>How far above or below the bullets the player's middle may be (metres; the middle of a standing player is about a metre above the floor).</summary>
        public const float RunOverHeight = 1.6f;

        /// <summary>Is a player at this flat distance and this height difference from the bullets running over them?</summary>
        public static bool RunsOver(float flatDistance, float heightDifference) => flatDistance <= RunOverRadius && Math.Abs(heightDifference) <= RunOverHeight;
    }
}
