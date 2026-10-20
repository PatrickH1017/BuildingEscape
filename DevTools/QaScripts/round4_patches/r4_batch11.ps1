# Round 4, batch 11 (A7): what the adapted themes need besides themselves: a "fits here" test for a rectangle (RoomCtx.CanPlaceRect), the half cubicle wall, the outpost's desks for the new sizes. BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep '        /// <summary>Reserves floor space (keeps other furniture away) for a treasure.</summary>' '        /// <summary>Can solid furniture stand on this local rectangle: inside the room, clear of the keep-outs, the blocks and the aisles, and of the solid things already placed? (A whole cluster is tested before it is built.)</summary>
        public bool CanPlaceRect(Rc rc)
        {
            const float e = 0.005f;
            if (rc.x0 < -w / 2 - e || rc.x1 > w / 2 + e || rc.z0 < -d / 2 - e || rc.z1 > d / 2 + e) return false;
            for (int i = 0; i < keep.Count; i++) if (keep[i].Overlaps(rc)) return false;
            for (int i = 0; i < blocks.Count; i++) if (blocks[i].Overlaps(rc)) return false;
            for (int i = 0; i < lanes.Count; i++) if (lanes[i].Overlaps(rc)) return false;
            for (int i = 0; i < floor.Count; i++) if (floor[i].solid && floor[i].rc.Overlaps(rc, 0.02f)) return false;
            return true;
        }

        /// <summary>Reserves floor space (keeps other furniture away) for a treasure.</summary>'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\PropTable.cs')
Rep '            Add("divider", ' '            Add("cube_wall_half", Syn + "Props/Furniture/SM_Prop_CubicalWall_01_Half", 1.0f, 1.33f, .07f);
            Add("divider", '
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficePlanner.Outpost.cs')
Rep '                r.Put("chair_office", table.lx - table.def.D / 2 - 0.35f, table.lz, 90f);' '                r.Put("chair_office", table.lx - table.def.D / 2 - Tuck("chair_office"), table.lz, 90f);'
Rep '            for (int i = 0; i < 3; i++)
            {
                float x = -3.5f + 1.5f * i;
                var desk = r.Wall(''S'', "desk_1", x);
                if (desk == null) continue;
                desks.Add(desk);
                float dd = desk.def.D;
                r.Put("chair_office", x, desk.lz + dd / 2 + 0.30f, 180f);
                r.Top(desk, "monitor_new", -0.45f, -dd / 2 + 0.2f);
                r.Top(desk, i == 1 ? "monitor_old" : "monitor_new", 0.02f, -dd / 2 + 0.2f);
                r.Top(desk, "keyboard", -0.22f, 0.12f);
                r.Top(desk, "mouse", 0.2f, 0.12f);
                if (i == 0) r.Top(desk, "phone", 0.52f, -0.02f);
                if (i == 1) r.Top(desk, "papers", 0.55f, 0.0f, 90f);
            }' '            for (int i = 0; i < 3; i++)
            {
                float x = -3.4f + 2.1f * i;                                 // (the pack''s desks are 2 m wide)
                var desk = r.Wall(''S'', "desk_1", x);
                if (desk == null) continue;
                desks.Add(desk);
                float dd = desk.def.D;
                r.Put("chair_office", x, desk.lz + dd / 2 + Tuck("chair_office"), 180f);
                r.Top(desk, "monitor_new", -0.5f, -dd / 2 + 0.2f);
                r.Top(desk, i == 1 ? "monitor_old" : "monitor_new", 0.3f, -dd / 2 + 0.2f);
                r.Top(desk, "keyboard", -0.25f, 0.10f);
                r.Top(desk, "mouse", 0.2f, 0.10f);
                if (i == 0) r.Top(desk, "phone", 0.75f, 0.22f);
                if (i == 1) r.Top(desk, "papers", 0.7f, 0.0f, 90f);
            }'
Rep '            for (int i = 0; i < 7; i++) r.Mount(''N'', "wall_tv", -3.9f + 1.2f * i, 2.2f);
            r.Mount(''W'', "wall_tv", -1.5f, 2.2f);' '            for (int i = 0; i < 7; i += 2) r.Mount(''N'', "wall_tv", -3.9f + 1.2f * i, 2.2f);       // (the pack''s wall screens are 1.5 m wide: one above every other locker)
            r.Mount(''W'', "wall_tv", -1.5f, 2.2f);'
Save-Src

Write-Output 'round 4 batch 11 patched'
