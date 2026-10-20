# Round 4, batch 22: the strip of floor in front of a wall-mounted piece of safety gear is remembered (RoomCtx.reachZones), so that nothing solid is put there afterwards either (a corner plant after the extinguisher,
# a cabinet the hiding-place pass adds); the extinguishers of the break room, the copy room and the server room use the same clearance. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep '        public readonly List<Rc> blocks = new List<Rc>();

        public System.Random rng => pl.rng;' '        public readonly List<Rc> blocks = new List<Rc>();
        /// <summary>The strips of floor in front of wall-mounted safety gear (extinguisher, axe case, pull station): nothing solid and taller than 0.3 m is put on them (local rectangles; <see cref="Mount"/> with clear above 0 adds one).</summary>
        public readonly List<Rc> reachZones = new List<Rc>();

        public System.Random rng => pl.rng;'
# PutDef: solid floor furniture keeps off the strips
Rep '                    for (int i = 0; i < floor.Count; i++) if (Conflict(floor[i], rc, solid)) return null;
                }
            }
            var it = new Item' '                    for (int i = 0; i < floor.Count; i++) if (Conflict(floor[i], rc, solid)) return null;
                    if (solid && ph > 0.3f) for (int i = 0; i < reachZones.Count; i++) if (reachZones[i].Overlaps(rc, 0.02f)) return null;
                }
            }
            var it = new Item'
# Mount: declare the strip outside the check, remember it once the piece is placed
Rep '            if (clear > 0f)
            {
                // safety gear stays reachable: nothing solid in the strip in front of it (0.9 m out, a little wider than the item itself)
                Rc zone = rc;
                const float reach = 0.9f;' '            Rc zone = rc;
            if (clear > 0f)
            {
                // safety gear stays reachable: nothing solid in the strip in front of it (0.9 m out, a little wider than the item itself)
                const float reach = 0.9f;'
Rep '            return PutDef(def, lx, lz, ly, height, false, true, 0);
        }' '            if (clear > 0f) reachZones.Add(zone);
            return PutDef(def, lx, lz, ly, height, false, true, 0);
        }'
# Panel (a free-standing screen) and CanPlaceRect (a whole cluster) keep off the strips too
Rep '            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return null;
            var def = new PropDef("panel"' '            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return null;
            for (int i = 0; i < reachZones.Count; i++) if (reachZones[i].Overlaps(rc, 0.02f)) return null;
            var def = new PropDef("panel"'
Rep '            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return false;
            return true;' '            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return false;
            for (int i = 0; i < reachZones.Count; i++) if (reachZones[i].Overlaps(rc, 0.02f)) return false;
            return true;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeThemes.cs')
Rep '            for (float x = -hx + 0.5f; x <= hx - 0.5f; x += 0.5f) if (r.Mount(''S'', "extinguisher", x, 0.95f) != null) break;' '            for (float x = -hx + 0.5f; x <= hx - 0.5f; x += 0.5f) if (r.Mount(''S'', "extinguisher", x, 0.95f, 0f, KitClear) != null) break;'
Rep '            for (float x = hx - 0.5f; x >= -hx + 0.5f; x -= 0.5f) if (r.Mount(''S'', "extinguisher", x, 0.95f) != null) break;      // (copiers get hot)' '            for (float x = hx - 0.5f; x >= -hx + 0.5f; x -= 0.5f) if (r.Mount(''S'', "extinguisher", x, 0.95f, 0f, KitClear) != null) break;      // (copiers get hot)'
Rep '            for (float x = hx - 0.5f; x >= -hx + 0.5f; x -= 0.5f) if (r.Mount(''S'', "extinguisher", x, 0.95f) != null) break;
        }' '            for (float x = hx - 0.5f; x >= -hx + 0.5f; x -= 0.5f) if (r.Mount(''S'', "extinguisher", x, 0.95f, 0f, KitClear) != null) break;
        }'
Save-Src
Write-Output 'round 4 batch 22 patched'
