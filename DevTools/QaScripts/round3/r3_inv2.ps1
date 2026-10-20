# R0 (2nd step): the inventory card stays under the HUD banner and is as tall as its content (eased), like the lobby card.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\Backpack.cs"
if ($s.Contains('invCardH')) { "already patched"; return }
Rep @'
            float pw = Mathf.Min(840f * s, Screen.width - 40f * s), ph = Mathf.Min(790f * s, Screen.height - 64f * s);
            var panel = new Rect((Screen.width - pw) * 0.5f, (Screen.height - ph) * 0.5f + 16f * s, pw, ph);
'@ @'
            float pw = Mathf.Min(840f * s, Screen.width - 40f * s);
            float py = 126f * s;                                           // (the card stays under the banner at the top of the screen)
            float maxH = Mathf.Max(260f * s, Screen.height - py - 24f * s);
            float want = Mathf.Clamp(172f * s + invScroll.ContentHeight + 10f * s, 300f * s, Mathf.Min(maxH, 790f * s));    // (as tall as the lists were on the last frame)
            if (invCardH <= 0f) invCardH = want;
            else if (Event.current.type == EventType.Repaint) invCardH = Mathf.Lerp(invCardH, want, 1f - Mathf.Exp(-14f * Mathf.Min(0.1f, Time.unscaledDeltaTime)));
            float ph = Mathf.Min(invCardH, maxH);
            var panel = new Rect((Screen.width - pw) * 0.5f, Mathf.Max(py, (Screen.height - ph) * 0.5f + 16f * s), pw, ph);
'@
Rep '        readonly MenuKit.Scroll invScroll = new MenuKit.Scroll();' '        readonly MenuKit.Scroll invScroll = new MenuKit.Scroll();
        float invCardH;'
Rep @'
            if (!Viewed) return;
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
'@ @'
            if (!Viewed) return;
            if (!UiOpen) invCardH = 0f;
            float w = Weight;
            // the weight bar and the prompts only draw on Repaint; the inventory window (buttons) needs every event
'@
Save-Src
