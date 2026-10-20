# Round 8, batch F19: a pure test for the two looks of the UI (the owner, 2026-10-18): Classic is exactly the old palette, Blue is blue (no purple left in the palette), and a preview does not change the player's choice.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevR7Test.cs'
Rep @'
            // ---- the terminal: OUTPOST LOCK / OUTPOST UNLOCK work the door of the outpost itself (the owner, 2026-10-18)
'@ @'
            // ---- the two looks of the UI (the owner, 2026-10-18: "make the UI main color blue instead of purple ... a separate test UI, don't fully implement it yet")
            {
                float HueOf(Color c) { Color.RGBToHSV(c, out float h, out _, out _); return h * 360f; }
                bool InBlue(Color c) { float h = HueOf(c); return h >= 195f && h <= 245f; }
                bool Is(Color c, int rgb) { var k = (Color32)c; return k.r == ((rgb >> 16) & 255) && k.g == ((rgb >> 8) & 255) && k.b == (rgb & 255); }
                bool choiceBefore = HudTheme.Blue;
                HudTheme.Preview(0);
                bool classicSame = !HudTheme.Blue && HudTheme.Style == UiStyle.Classic && Is(HudTheme.Card, 0x4C3CA8) && Is(HudTheme.Ink, 0x1B1236) && Is(HudTheme.Muted, 0xD6CDFA)
                    && Is(HudTheme.Dim, 0x9C94C8) && Is(HudTheme.Track, 0x2B2068) && Is(HudTheme.Violet, 0x8A5CFF);
                HudTheme.Preview(1);
                bool blueIsBlue = HudTheme.Blue && HudTheme.Style == UiStyle.Blue && InBlue(HudTheme.Card) && InBlue(HudTheme.Ink) && InBlue(HudTheme.Muted) && InBlue(HudTheme.Dim) && InBlue(HudTheme.Track) && InBlue(HudTheme.Tag) && InBlue(HudTheme.Violet);
                bool meaningsKept = Is(HudTheme.Cash, 0x45E07C) && Is(HudTheme.Danger, 0xFF4046) && Is(HudTheme.Quota, 0xFFC61F);       // (money stays green, danger red, the quota gold in both looks)
                HudTheme.Preview(-1);
                Check("ui: the Classic look is exactly the old palette, the Blue look (on trial) has no purple in its palette, money / danger / the quota keep their colours, and a preview leaves the player's choice alone",
                    classicSame && blueIsBlue && meaningsKept && HudTheme.Blue == choiceBefore, $"(classic {classicSame}, blue {blueIsBlue}, meanings {meaningsKept}, choice kept {HudTheme.Blue == choiceBefore})");
            }

            // ---- the terminal: OUTPOST LOCK / OUTPOST UNLOCK work the door of the outpost itself (the owner, 2026-10-18)
'@
Save-Src
Write-Output 'round 8 batch F19 (UI looks test) patched'
