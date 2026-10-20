# Round 2, batch 7: a quieter carpet palette; the float test also looks for items that hang half over a table's edge. Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Surfaces.cs')
Rep 'Rgb(0.30f, 0.38f, 0.50f),   // slate blue' 'Rgb(0.33f, 0.39f, 0.48f),   // slate blue'
Rep 'Rgb(0.30f, 0.43f, 0.37f),   // sage green' 'Rgb(0.32f, 0.42f, 0.36f),   // sage green'
Rep 'Rgb(0.47f, 0.30f, 0.33f),   // wine' 'Rgb(0.45f, 0.32f, 0.34f),   // wine'
Rep 'Rgb(0.40f, 0.41f, 0.44f),   // office grey' 'Rgb(0.40f, 0.41f, 0.43f),   // office grey'
Rep 'Rgb(0.45f, 0.40f, 0.32f),   // taupe' 'Rgb(0.45f, 0.41f, 0.35f),   // taupe'
Rep 'Rgb(0.27f, 0.41f, 0.45f),   // teal grey' 'Rgb(0.29f, 0.40f, 0.43f),   // teal grey'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevFloatQa.cs')
Rep 'int checkedCount = 0, floating = 0, nothing = 0, sunk = 0;' 'int checkedCount = 0, floating = 0, nothing = 0, sunk = 0, hanging = 0;'
Rep @'
                    float gap = b.min.y - hits[0].point.y;
                    if (gap > 0.03f) { floating++; worst.Add((gap, what, b.center)); }
                    else if (gap < -0.06f) sunk++;
'@ @'
                    float gap = b.min.y - hits[0].point.y;
                    if (gap > 0.03f) { floating++; worst.Add((gap, what, b.center)); }
                    else if (gap < -0.06f) sunk++;
                    else if (b.size.x > 0.06f && b.size.z > 0.06f)
                    {
                        // a 3 x 3 grid over the footprint: an item with half of it out in the air hangs over the edge
                        int unsupported = 0;
                        for (int gx = -1; gx <= 1; gx++)
                            for (int gz = -1; gz <= 1; gz++)
                            {
                                var o2 = new Vector3(b.center.x + gx * 0.4f * b.size.x, b.min.y + 0.06f, b.center.z + gz * 0.4f * b.size.z);
                                var h2 = Physics.RaycastAll(o2, Vector3.down, 0.5f, ~0, QueryTriggerInteraction.Ignore).Where(h => !h.collider.transform.IsChildOf(go.transform)).OrderBy(h => h.distance).ToArray();
                                if (h2.Length == 0 || b.min.y - h2[0].point.y > 0.05f) unsupported++;
                            }
                        if (unsupported >= 5) { hanging++; worst.Add((0.5f + unsupported * 0.01f, what + " (hangs over an edge)", b.center)); }
                    }
'@
Rep 'floating {floating}, over nothing {nothing}, sunk in {sunk}");' 'floating {floating}, over nothing {nothing}, sunk in {sunk}, hanging over an edge {hanging}");'
Rep 'floating == 0 && nothing == 0, $"(floating {floating}, over nothing {nothing} of {checkedCount})");' 'floating == 0 && nothing == 0 && hanging == 0, $"(floating {floating}, over nothing {nothing}, hanging {hanging} of {checkedCount})");'
Save-Src
Write-Output 'batch 7 patched'
