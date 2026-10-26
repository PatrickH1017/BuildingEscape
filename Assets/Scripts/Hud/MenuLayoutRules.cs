using System;

namespace BuildingEscape
{
    /// <summary>
    /// Where the Esc menu's card may stand (the owner, 2026-10-24: "scale down the size of the Escape menu UI so that it doesn't overlay with the other UIs"), without Unity types so it is tested outside Unity. The run's HUD keeps the middle of the screen clear
    /// except at its top (the floor's title and the line under it) and at its bottom (the hotbar and the stamina bar): the card lives in the band between them, and a card that is taller than the band gets the band's height and scrolls (the Settings page).
    /// All lengths are reference pixels (times <c>HudTheme.S</c> on the screen) unless it says screen pixels.
    /// </summary>
    public static class MenuLayoutRules
    {
        /// <summary>Kept clear at the top: the floor banner and the line under it (about 100), and the ribbon that sticks out above a card (22).</summary>
        public const float TopReserve = 130f;
        /// <summary>Kept clear at the bottom: the hotbar and the stamina bar under it.</summary>
        public const float BottomReserve = 132f;
        /// <summary>The Settings card is never taller than this (it was 880).</summary>
        public const float SettingsMaxHeight = 780f;
        /// <summary>The band is never thinner than this (a window too short for the HUD uses what there is).</summary>
        public const float MinBand = 120f;

        /// <summary>The band for a screen <paramref name="screenHeight"/> pixels tall with the HUD scale <paramref name="s"/>: where it starts and how tall it is (screen pixels).</summary>
        public static void Band(float screenHeight, float s, out float top, out float height)
        {
            top = TopReserve * s;
            height = Math.Max(MinBand, screenHeight - (TopReserve + BottomReserve) * s);
        }

        /// <summary>The top of a card <paramref name="cardHeight"/> tall (screen pixels): in the middle of the band; one too tall for it is centred on the screen as it always was.</summary>
        public static float CardY(float bandTop, float bandHeight, float cardHeight, float screenHeight) =>
            cardHeight <= bandHeight ? bandTop + (bandHeight - cardHeight) * 0.5f : (screenHeight - cardHeight) * 0.5f;

        /// <summary>The Settings card's height (screen pixels): <see cref="SettingsMaxHeight"/> at most, and no taller than the band.</summary>
        public static float SettingsHeight(float s, float bandHeight) => Math.Min(SettingsMaxHeight * s, bandHeight);
    }
}
