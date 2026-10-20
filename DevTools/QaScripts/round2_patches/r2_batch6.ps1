# Round 2, batch 6: tests and tools for the one-carpet-per-floor palette and the night city; BossOffice back to its old self. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# the city no longer uses the cinematic's day pictures: BossOffice is as it was
Open-Src (Join-Path $Dir 'Scripts\Intro\BossOffice.cs')
Rep 'internal static Texture2D SkyTexture()' 'static Texture2D SkyTexture()'
Rep 'internal static Texture2D WindowTexture(int level)' 'static Texture2D WindowTexture(int level)'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Look.cs')
Rep 'LookCheck($"{f} many different carpets", gen.CarpetKinds >= 4, $"({gen.CarpetKinds})");' 'LookCheck($"{f} one carpet colour for the whole floor", gen.CarpetTintCount == 1, $"({gen.CarpetTintCount} colour, the floor''s own is {FloorGenerator.CarpetColourOf(floor)})");'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevFloatQa.cs')
Rep @'
                var interior = gen.Current.Find("Interior");
'@ @'
                var interior = gen.Current.Find("Interior");
                if (interior)
                {
                    var culler = interior.GetComponent<PropCuller>();
                    if (culler) culler.enabled = false;                                          // (the far rooms' furniture is hidden by distance: show all of it for the test)
                    foreach (var rr in interior.GetComponentsInChildren<Renderer>(true)) if (rr) rr.enabled = true;
                }
'@
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Test.cs')
Rep @'
            Object.DestroyImmediate(leaf);
        }
'@ @'
            Object.DestroyImmediate(leaf);

            // ---- "the carpets are a bit too colorful. Each floor should be a set color and floors can have the same colors as others"
            var seenCarpets = new System.Collections.Generic.HashSet<Color>();
            bool nextDiffers = true;
            for (int fl = 1; fl <= 13; fl++)
            {
                seenCarpets.Add(FloorGenerator.CarpetColourOf(fl));
                nextDiffers &= FloorGenerator.CarpetColourOf(fl) != FloorGenerator.CarpetColourOf(fl + 1);
            }
            Check("carpets: a floor has one set colour (the same on every call), no two floors in a row alike, and the palette is reused (floor 1 = floor 7)",
                nextDiffers && seenCarpets.Count == FloorGenerator.CarpetPaletteSize && FloorGenerator.CarpetColourOf(1) == FloorGenerator.CarpetColourOf(7) && FloorGenerator.CarpetColourOf(3) == FloorGenerator.CarpetColourOf(3));
            bool quietColours = true;
            foreach (var quietC in seenCarpets) { Color.RGBToHSV(quietC, out _, out float sat, out float val); quietColours &= sat < 0.36f && val < 0.55f; }
            Check("carpets: the colours are quiet ones (low saturation, not bright)", quietColours);

            // ---- "it should be nighttime outside": a dark sky with stars and a moon, windows lit in some of the towers
            var skyPx = ExteriorCity.NightSky().GetPixels32();
            int starPx = 0, moonPx = 0, counted = 0; double lum = 0;
            for (int sy = 77; sy < 512; sy += 3)
                for (int sx = 0; sx < 2048; sx += 3)
                {
                    var sc = skyPx[sy * 2048 + sx];
                    float sl = (sc.r + sc.g + sc.b) / (3f * 255f);
                    lum += sl; counted++;
                    if (sl > 0.6f && !(Mathf.Abs(sx - 1446) < 60 && Mathf.Abs(sy - 283) < 60)) starPx++;
                }
            for (int my = 270; my < 296; my++)
                for (int mx = 1433; mx < 1460; mx++)
                {
                    var mc = skyPx[my * 2048 + mx];
                    if ((mc.r + mc.g + mc.b) / (3f * 255f) > 0.8f) moonPx++;
                }
            Check("night: the sky is dark (average brightness under 0.12), with stars and a bright moon", lum / counted < 0.12 && starPx >= 40 && moonPx >= 300, $"(average {lum / counted:0.000}, stars {starPx}, moon pixels {moonPx})");
            bool windowsOk = true; string windowNote = "";
            for (int lv = 0; lv < 3; lv++)
            {
                var wp = ExteriorCity.NightWindows(lv).GetPixels32();
                int lit = 0;
                for (int wy = 0; wy < 8; wy++)
                    for (int wx = 0; wx < 8; wx++)
                    {
                        var wc = wp[(wy * 16 + 7) * 128 + wx * 16 + 8];
                        if ((wc.r + wc.g + wc.b) / (3f * 255f) > 0.18f) lit++;
                    }
                windowNote += $" level {lv}: {lit}/64";
                windowsOk &= lit >= 6 && lit <= 44;
            }
            Check("night: the towers have lit windows, some of the panes of every tile at every distance", windowsOk, "(" + windowNote.Trim() + ")");
        }
'@
Save-Src
Write-Output 'batch 6 patched'
