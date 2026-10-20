# Round 4, batch 20: the emergency axes (and the extinguishers and pull stations beside them) hang where they can be reached (the owner, 2026-10-17 night: "make sure the emergency axes are in places that look natural"):
# a wall item of safety gear needs the floor 0.9 m out from it, and 0.35 m to either side, free of anything solid (Mount's new "clear" argument, used by the corridor kit and the lobby kit); the stairwells' kit keeps
# the floor in front of the landing's face clear (the service room reserves it before it puts its crates down). BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeLayout.cs')
Rep 'Skips doorways and tall furniture.</summary>' 'Skips doorways and tall furniture. With <paramref name="clear"/> above 0 (safety gear: extinguisher, axe case, pull station) the floor in front of it, 0.9 m out and that far to either side, must be free of anything solid too, so that it can be reached.</summary>'
Rep 'public Item Mount(char side, string key, float along, float height, float gap = 0.0f)' 'public Item Mount(char side, string key, float along, float height, float gap = 0.0f, float clear = 0f)'
Rep '            for (int i = 0; i < floor.Count; i++)
                if (floor[i].h > height - 0.05f && floor[i].rc.Overlaps(deep, 0.02f)) return null;
' '            for (int i = 0; i < floor.Count; i++)
                if (floor[i].h > height - 0.05f && floor[i].rc.Overlaps(deep, 0.02f)) return null;
            if (clear > 0f)
            {
                // safety gear stays reachable: nothing solid in the strip in front of it (0.9 m out, a little wider than the item itself)
                Rc zone = rc;
                const float reach = 0.9f;
                switch (side)
                {
                    case ''S'': zone.z1 = rc.z0 + reach; zone.x0 -= clear; zone.x1 += clear; break;
                    case ''N'': zone.z0 = rc.z1 - reach; zone.x0 -= clear; zone.x1 += clear; break;
                    case ''W'': zone.x1 = rc.x0 + reach; zone.z0 -= clear; zone.z1 += clear; break;
                    default: zone.x0 = rc.x1 - reach; zone.z0 -= clear; zone.z1 += clear; break;
                }
                for (int i = 0; i < floor.Count; i++)
                    if (floor[i].solid && floor[i].h > 0.3f && floor[i].rc.Overlaps(zone, 0.02f)) return null;
            }
'
Save-Src

Open-Src (Join-Path $Dir 'Scripts\Interior\OfficeThemes.cs')
Rep '                    if (r.Mount(side, "extinguisher", a, 0.95f) == null) continue;
                    if (Chance(0.45)) r.Mount(side, "fire_alarm", a + 0.5f, 1.0f);
                    else if (Chance(0.25)) r.Mount(side, "fire_alarm", a - 0.5f, 1.0f);
                    if (Chance(0.14)) r.Mount(side, "emergency_axe", a - 1.0f, 0.95f);' '                    if (r.Mount(side, "extinguisher", a, 0.95f, 0f, KitClear) == null) continue;
                    if (Chance(0.45)) r.Mount(side, "fire_alarm", a + 0.5f, 1.0f, 0f, KitClear);
                    else if (Chance(0.25)) r.Mount(side, "fire_alarm", a - 0.5f, 1.0f, 0f, KitClear);
                    if (Chance(0.14)) r.Mount(side, "emergency_axe", a - 1.0f, 0.95f, 0f, KitClear);'
Rep '                    if (r.Mount(side, "extinguisher", a, 0.95f) != null)
                    {
                        r.Mount(side, "fire_alarm", a + 0.5f, 1.0f);
                        if (side == ''N'') r.Mount(side, "emergency_axe", a - 1.0f, 0.95f);' '                    if (r.Mount(side, "extinguisher", a, 0.95f, 0f, KitClear) != null)
                    {
                        r.Mount(side, "fire_alarm", a + 0.5f, 1.0f, 0f, KitClear);
                        if (side == ''N'') r.Mount(side, "emergency_axe", a - 1.0f, 0.95f, 0f, KitClear);'
Rep '            // around the guest elevator: a loading area, shelves and crates along the outer walls
            float h = Inner;
' '            // around the guest elevator: a loading area, shelves and crates along the outer walls
            float h = Inner;
            int stair = StairIndex(r.info.cellX, r.info.cellY);
            if (stair >= 0) StairFront(r, stair);                           // (the floor in front of the stairwell door and its safety kit stays clear: reserved before the crates go down)
'
Rep '            r.Mount(''N'', "ac_unit", 0f, 2.7f);
            int stair = StairIndex(r.info.cellX, r.info.cellY);
            if (stair >= 0) StairKit(r, stair);' '            r.Mount(''N'', "ac_unit", 0f, 2.7f);
            if (stair >= 0) StairKit(r, stair);'
Rep 'the STAIRS plate the generator hangs, under it the emergency axe in its case; to the right the green EXIT sign, the STAIRS sign of the pack, the fire alarm pull' 'the pack STAIRS sign (twice its size), under it the emergency axe in its case; to the right the green EXIT sign, the fire alarm pull'
Rep '            Put("extinguisher", 1.63f, 0.55f);
        }
' '            Put("extinguisher", 1.63f, 0.55f);
        }

        /// <summary>Safety gear stays reachable: the floor 0.9 m out from it, and this far (m) to either side of it, is free of anything solid (<see cref="RoomCtx.Mount"/>).</summary>
        const float KitClear = 0.35f;

        /// <summary>The floor in front of a stairwell door and its safety kit (the axe case, the extinguisher, the pull station) stays clear: nothing is put within 0.9 m of the landing face, along its whole width.</summary>
        void StairFront(RoomCtx r, int stair)
        {
            if (r.turns != 0 || inp.stairDirX == null) return;
            int dx = inp.stairDirX[stair], dz = inp.stairDirY[stair];
            if (dx == 0 && dz == 0) return;
            float mid = StairHalf + 0.45f;
            if (dx != 0) r.Reserve(dx * mid, 0f, 0.9f, 4.7f); else r.Reserve(0f, dz * mid, 4.7f, 0.9f);
        }
'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevR4Test.cs')
Rep '            bool pairs = true, doorsOk = true, empty = true, signsOk = true, stops = true, unnumbered = true, menWomen = true, hasBreak = true, nearLobby = true;' '            bool pairs = true, doorsOk = true, empty = true, signsOk = true, stops = true, unnumbered = true, menWomen = true, hasBreak = true, nearLobby = true, kitClear = true;
            int kitCount = 0, kitDiag = 0;'
Rep '                        hasBreak &= lay.rooms.Any(r => r.theme == "break" && r.kind == 0 && Mathf.Min(r.bounds.W, r.bounds.D) >= 4f);' '                        hasBreak &= lay.rooms.Any(r => r.theme == "break" && r.kind == 0 && Mathf.Min(r.bounds.W, r.bounds.D) >= 4f);
                        // the safety gear can be reached: the floor 0.9 m in front of every extinguisher, axe case and pull station, and 0.35 m to either side of it, has nothing solid standing on it
                        var solids = lay.pieces.Where(q => q.solid && !q.mount && !q.onTop && q.h > 0.3f).ToList();
                        foreach (var g in lay.pieces)
                        {
                            if (!g.mount || (g.key != "extinguisher" && g.key != "emergency_axe" && g.key != "fire_alarm")) continue;
                            kitCount++;
                            float fx = Mathf.Sin(g.yaw * Mathf.Deg2Rad), fz = Mathf.Cos(g.yaw * Mathf.Deg2Rad);
                            Rc zone = g.rc;
                            if (Mathf.Abs(fx) > Mathf.Abs(fz)) { if (fx > 0f) zone.x1 = g.rc.x0 + 0.9f; else zone.x0 = g.rc.x1 - 0.9f; zone.z0 -= 0.35f; zone.z1 += 0.35f; }
                            else { if (fz > 0f) zone.z1 = g.rc.z0 + 0.9f; else zone.z0 = g.rc.z1 - 0.9f; zone.x0 -= 0.35f; zone.x1 += 0.35f; }
                            foreach (var q in solids)
                                if (q.rc.Overlaps(zone, 0.02f))
                                {
                                    kitClear = false;
                                    if (kitDiag++ < 6) Debug.Log($"[NetTest] core diag: {g.key} at ({g.x:0.0}, {g.z:0.0}) facing {g.yaw:0} has {q.key} in front of it ({cols}x{rows} seed {sd} floor {floor})");
                                    break;
                                }
                        }'
Rep '            Check("core: every floor has a break room with a door of its own, at least 4 m across", hasBreak);' '            Check("core: every floor has a break room with a door of its own, at least 4 m across", hasBreak);
            Check($"core: the safety gear can be reached: nothing solid within 0.9 m in front of any of the {kitCount} extinguishers, axe cases and pull stations, or 0.35 m beside them", kitClear && kitCount > 100);'
Save-Src
Write-Output 'round 4 batch 20 patched'
