using System;

namespace BuildingEscape.EditorTools
{
    /// <summary>The Esc menu's place on the screen (the owner, 2026-10-24: "scale down the size of the Escape menu UI so that it doesn't overlay with the other UIs"). Called from <c>NetDevTest.PolicyTest</c>; the pictures are <c>AudioDevicesUi</c>'s (%TEMP%\be_ui_audio_*.png, %TEMP%\be_ui_esc_*.png).</summary>
    public static partial class NetDevTest
    {
        public static void MenuLayoutTest()
        {
            bool settingsFit = true, mainFit = true;
            string worst = "";
            foreach (float h in new[] { 540f, 600f, 720f, 768f, 888f, 900f, 1080f, 1200f, 1440f, 2160f })
            {
                float s = Math.Min(2f, Math.Max(0.75f, h / 1080f));                           // (HudTheme.S)
                MenuLayoutRules.Band(h, s, out float top, out float band);
                float ph = MenuLayoutRules.SettingsHeight(s, band);
                float y = MenuLayoutRules.CardY(top, band, ph, h);
                bool ok = y - 22f * s >= 108f * s - 0.01f && y + ph <= h - MenuLayoutRules.BottomReserve * s + 0.01f && ph <= 780f * s + 0.01f;      // (the ribbon above the card stays under the floor's banner; the card ends over the hotbar)
                settingsFit &= ok;
                float need = 630f * s;                                                         // (the main page with LEAVE RUN: the tallest; the DEV MENU button stands beside the card)
                float my = MenuLayoutRules.CardY(top, band, need, h);
                bool mok = need > band || (my - 22f * s >= 108f * s - 0.01f && my + need <= h - MenuLayoutRules.BottomReserve * s + 0.01f);
                mainFit &= mok;
                if (!ok || !mok) worst += $" [{h}: y {y:0} ph {ph:0} / main {my:0}]";
            }
            Check("menu: on every window height from 540 to 2160 the Settings card (780 at most) stands in the band between the floor's banner and the hotbar, ribbon included", settingsFit, worst);
            Check("menu: the main Esc card (630 tall at most, with LEAVE RUN; DEV MENU stands beside it) stands in the same band wherever it fits in it", mainFit, worst);
            MenuLayoutRules.Band(1080f, 1f, out float top1, out float band1);
            Check("menu: at 1080 the band starts at 130 and is 818 tall; a card is centred in it; one taller than the band is centred on the screen as it always was",
                Math.Abs(top1 - 130f) < 1e-3f && Math.Abs(band1 - 818f) < 1e-3f && Math.Abs(MenuLayoutRules.CardY(130f, 818f, 630f, 1080f) - 224f) < 1e-3f && Math.Abs(MenuLayoutRules.CardY(130f, 818f, 900f, 1080f) - 90f) < 1e-3f
                && Math.Abs(MenuLayoutRules.SettingsHeight(1f, 818f) - 780f) < 1e-3f && Math.Abs(MenuLayoutRules.SettingsHeight(1f, 500f) - 500f) < 1e-3f);
        }
    }
}
