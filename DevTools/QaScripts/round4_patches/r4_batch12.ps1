# Round 4, batch 12: the suites keep their glass front (no direction sign on a suite's perimeter wall), natural walkways (pods of at most two desks across, the long open room and the training room less tight),
# A3 (the outpost guard can leave the outpost once the door is unlocked). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\DirectionSigns.cs')
Rep '            foreach (var r in layout.rooms)
                foreach (var d in r.doors)
                    if (d.Grow(0.1f).Overlaps(thin)) return true;' '            foreach (var r in layout.rooms)
                foreach (var d in r.doors)
                    if (d.Grow(0.1f).Overlaps(thin)) return true;

            // the front of an executive suite is glass (the owner: suites have glass walls and doors): no sign on a suite''s perimeter wall
            foreach (var r in layout.rooms)
                if (r.role == OfficePlanner.RoleSuite && r.bounds.Grow(0.45f).Overlaps(thin)) return true;'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeThemes.cs')
Rep '            var def = PropTable.Get(deskKey);
            float dw = def.W, dd = def.D;
            float pitch = Math.Max(dw, Dw("cube_wall")) + 0.02f;           // one cubicle wall of the spine per desk' '            nx = Math.Min(nx, 2);                                           // (a pod is at most two desks across: natural walkways round it, the owner: "some of the desks are too close together")
            var def = PropTable.Get(deskKey);
            float dw = def.W, dd = def.D;
            float pitch = Math.Max(dw, Dw("cube_wall")) + 0.02f;           // one cubicle wall of the spine per desk'
Rep '                int nx = Math.Min(4, (int)Math.Floor((a1 - a0) / pitch));
                if (nx < 1) continue;
                float len = nx * pitch;
                Island(r, s * (a0 + len / 2), 0.0f, nx);' '                int nx = Math.Min(2, (int)Math.Floor((a1 - a0) / pitch));
                if (nx < 1) continue;
                float len = nx * pitch;
                Island(r, s * (a0 + (a1 - a0) / 2f), 0.0f, nx);              // (centred between the cross aisle and the end wall: a walkway on both sides)'
Rep '            float pitchZ = dd + 0.72f;                                       // a desk and the chair behind it' '            float pitchZ = dd + 0.90f;                                       // a desk, the chair behind it and a way past'
Rep '            var xs = new List<float>();
            for (int k = 0; k < 8; k++)
            {
                float x = aisle + 0.1f + dw / 2 + k * (dw + 0.2f);
                if (x + dw / 2 > hx - 0.3f) break;
                xs.Add(x); xs.Add(-x);
            }' '            var xs = new List<float>();
            float xd = aisle + 0.1f + dw / 2;
            for (int k = 0; k < 8; k++)
            {
                if (xd + dw / 2 > hx - 0.3f) break;
                xs.Add(xd); xs.Add(-xd);
                xd += dw + (k % 2 == 0 ? 0.12f : 0.9f);                          // (two desks side by side, then a walkway)
            }'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Outpost.cs')
Rep '            ai.noticeGate = p => room.Contains(p.x, p.z);' '            // blind and deaf outside the room while the door is locked; once it is unlocked (hacked) he notices what a guard notices anywhere and can leave the outpost to chase it, then walks back to his post
            // (the owner, 2026-10-17 night: "The outpost guard should be able to leave the outpost ... players should try to lure the guard out of the outpost after hacking the door")
            ai.noticeGate = p => room.Contains(p.x, p.z) || (outpostCtl != null && outpostCtl.Unlocked);'
Save-Src
Write-Output 'round 4 batch 12 patched'
