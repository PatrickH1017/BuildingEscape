# Round 12, batch G40 (the owner, 2026-10-20): "The colors for the scanning haven't changed, safes and valuables should be gold when scanning, weapons should be red, and cash should be green" and "Please add building assets to the black
# squares in the city nearby the building (reference image attached)".
#  (1) the scan's colours (`ChecklistRules.Rgb`, which the glow, the diamond, the label and the edge arrows all use): safes and valuables gold, unique items (weapons, armour) red, cash green;
#  (2) the city outside the windows (`ExteriorCity`): the black squares were the plots left empty on purpose (one in eight everywhere, and a ring of 38 m round the building): none of the near band is empty any more and the ring is 14 m
#      (a street's width), so every plot near the building gets one of the owner's buildings (or a box tower where none fits), and nine in ten get a building instead of three in four.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$A = 'C:\bqm\Assets\Scripts'
$E = 'C:\bqm\Assets\Editor\NetDev'

Open-Src "$A\FloorChecklist.cs"
Rep '/// <summary>What the scan and the journal''s checklist tell apart (the owner, 2026-10-19): valuables glow gold, unique items such as weapons purple, safes red and cash green.</summary>' '/// <summary>What the scan and the journal''s checklist tell apart (the owner, 2026-10-20, over his 2026-10-19 colours): safes and valuables glow gold, unique items such as weapons red and cash green.</summary>'
Rep '/// <summary>The glow of a kind as red, green and blue (0 to 1): safes red, unique items purple, cash green, valuables gold.</summary>' '/// <summary>The glow of a kind as red, green and blue (0 to 1): safes and valuables gold, unique items (weapons) red, cash green (the owner, 2026-10-20: "safes and valuables should be gold when scanning, weapons should be red, and cash should be green").</summary>'
Rep @'
                case ScanKind.Safe: r = 0.95f; g = 0.22f; b = 0.22f; break;
                case ScanKind.Unique: r = 0.70f; g = 0.36f; b = 1.00f; break;
'@ @'
                case ScanKind.Safe: r = 1.00f; g = 0.80f; b = 0.18f; break;
                case ScanKind.Unique: r = 0.95f; g = 0.22f; b = 0.22f; break;
'@
Save-Src

Open-Src "$A\LootScanner.cs"
Rep '/// <summary>The glow of a kind: safes red, unique items purple, cash green, valuables gold (the owner, 2026-10-19).</summary>' '/// <summary>The glow of a kind: safes and valuables gold, unique items (weapons) red, cash green (the owner, 2026-10-20).</summary>'
Rep '// the glow in the colour of its kind (seen through walls, like the diamond): red safes, purple unique items, green cash, gold valuables' '// the glow in the colour of its kind (seen through walls, like the diamond): gold safes, red unique items, green cash, gold valuables'
Save-Src

Open-Src "$E\NetDevChecklistTest.cs"
Rep @'
            Check("scan: safes glow red, unique items purple, cash green and valuables gold",
                sr > 0.8f && sg < 0.4f && sb < 0.4f && ur > 0.5f && ub > 0.9f && ug < 0.5f && cg > 0.8f && cr < 0.4f && cb < 0.6f && vr > 0.9f && vg > 0.7f && vb < 0.3f,
'@ @'
            Check("scan: safes and valuables glow gold, unique items (weapons) red and cash green (the owner, 2026-10-20)",
                sr > 0.9f && sg > 0.7f && sb < 0.3f && vr > 0.9f && vg > 0.7f && vb < 0.3f && ur > 0.8f && ug < 0.4f && ub < 0.4f && cg > 0.8f && cr < 0.4f && cb < 0.6f,
'@
Save-Src

Open-Src "$E\NetDevChecklist.cs"
Rep 'as unique items (purple)"' 'as unique items (red)"'
Rep 'as a safe (red)"' 'as a safe (gold)"'
Rep '// ---- the scan: unique items (the weapons on the outpost''s desk) are purple-kind, a shut safe is red-kind' '// ---- the scan: unique items (the weapons on the outpost''s desk) are red-kind, a shut safe is gold-kind'
Save-Src

Open-Src "$A\Interior\ExteriorCity.cs"
Rep 'public const float KeepClear = 38f;' 'public const float KeepClear = 14f;'
Rep '/// <summary>No tower is closer to the building than this (a street''s width and more).</summary>' '/// <summary>No tower is closer to the building than this (a street''s width; it was 38 m until the owner, 2026-10-20, asked for buildings on the black squares near the building).</summary>'
Rep 'if (Hash(hx, hz) < 0.12f) continue;' 'if (band > 0 && Hash(hx, hz) < 0.12f) continue;          // (some plots of the middle and far bands are empty: streets, parks; none of the near band is: the owner, 2026-10-20, "add building assets to the black squares")'
Rep 'Hash(hx + 233, hz + 239) < 0.75f)' 'Hash(hx + 233, hz + 239) < 0.9f)'
Rep '// the owner''s buildings stand in most plots of the near ring; where none fits (and in one plot in four) the plot gets a box tower, with its lit windows' '// the owner''s buildings stand in nine plots in ten of the near ring; where none fits (and in the tenth) the plot gets a box tower, with its lit windows: no plot of it is left empty'
Save-Src

Open-Src 'C:\bqs\README_SEND.txt'
Rep 'safes red, unique items such as weapons purple, cash green, valuables gold' 'safes and valuables gold, weapons red, cash green'
Save-Src
Write-Output 'round 12 batch G40 patched'
