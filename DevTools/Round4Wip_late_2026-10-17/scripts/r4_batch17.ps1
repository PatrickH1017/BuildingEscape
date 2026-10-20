# Round 4, batch 17: "make sure the emergency axes are in places that look natural": nothing is hung where something already hangs (RoomCtx.Mount refuses a spot that overlaps another wall piece). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'
Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep '            for (int i = 0; i < floor.Count; i++)
                if (floor[i].h > height - 0.05f && floor[i].rc.Overlaps(deep, 0.02f)) return null;
            return PutDef(def, lx, lz, ly, height, false, true, 0);' '            for (int i = 0; i < floor.Count; i++)
                if (floor[i].h > height - 0.05f && floor[i].rc.Overlaps(deep, 0.02f)) return null;
            // nothing hangs there already (a poster, a clock, an extinguisher, an axe case): two things never overlap on a wall, so an axe always has a clear bit of wall of its own
            var wrc = ToWorldRect(rc);
            float top = height + def.H;
            var all = pl.layout.pieces;
            for (int i = 0; i < all.Count; i++)
            {
                var p = all[i];
                if (p.mount && p.y < top - 0.02f && p.y + p.h > height + 0.02f && p.rc.Overlaps(wrc, 0.02f)) return null;
            }
            return PutDef(def, lx, lz, ly, height, false, true, 0);'
Save-Src
Write-Output 'round 4 batch 17 patched'