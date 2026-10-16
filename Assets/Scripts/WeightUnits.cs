using System;

namespace BuildingEscape
{
    /// <summary>
    /// Weights are stored in kilograms (every balance number in the game - item weights, the pack's capacity - is in kg and stays that way);
    /// everything the player READS is shown in pounds, written "lbs" (the owner asked for that, 2026-10-16). Pure C#, no Unity types, tested
    /// in NetDevTest (<c>HudUnitsTest</c>). Show a weight only through this class, so the unit cannot differ from one screen to the next.
    /// </summary>
    public static class WeightUnits
    {
        public const float PoundsPerKg = 2.20462262f;

        /// <summary>A weight in kilograms, in pounds.</summary>
        public static float ToLb(float kg) => kg * PoundsPerKg;

        /// <summary>The number of pounds for a weight in kilograms: one decimal and no ".0" ("8.8", "44"), whole pounds from 100 up.</summary>
        public static string Num(float kg)
        {
            double lb = kg * (double)PoundsPerKg;
            return lb >= 99.95
                ? Math.Round(lb, MidpointRounding.AwayFromZero).ToString("0")
                : (Math.Round(lb * 10.0, MidpointRounding.AwayFromZero) / 10.0).ToString("0.#");
        }

        /// <summary>"8.8 lbs" for a weight in kilograms.</summary>
        public static string Lbs(float kg) => Num(kg) + " lbs";

        /// <summary>Money per pound ("$/lb") for a value and a weight in kilograms; 0 for no weight.</summary>
        public static float PerLb(float value, float kg) => kg > 0f ? value / ToLb(kg) : 0f;
    }
}
