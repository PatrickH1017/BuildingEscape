# The owner, 2026-10-24: "make sure there aren't too many objects outside offices with glass walls, in the real world people with glass office walls usually leave them clear of view such as filing cabinets outside glass windows" (with two pictures of real glass offices) and
# "make sure there aren't clocks and other items on the windows to the outside, that doesn't make sense" (with a picture of posters, a clock and a drawing hung over the outside windows). ViewClearRules (pure) + FloorGenerator.ClearViews (see its summary) + the scan.
# Patches the scratch copy C:\bd; exact matches only (patchlib); new files are written with CRLF.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
Put-Crlf (Join-Path $here 'view_new\ViewClearRules.cs') ($A + 'Scripts\ViewClearRules.cs')
Put-Crlf (Join-Path $here 'view_new\FloorGenerator.ViewClear.cs') ($A + 'Scripts\FloorGenerator.ViewClear.cs')
Put-Crlf (Join-Path $here 'view_new\NetDevViewClear.cs') ($A + 'Editor\NetDev\NetDevViewClear.cs')

# ---- the glass fronts remember where they are
Open-Src ($A + 'Scripts\FloorGenerator.Glass.cs')
Rep '            GlassFronts.Add((new Vector3(r.Cx, 1.6f, r.Cz), outward));' @'
            GlassFronts.Add((new Vector3(r.Cx, 1.6f, r.Cz), outward));
            glassSpans.Add(new WindowSpan { alongX = alongX, line = centre, half = half, lo = lo, hi = hi, a = a, b = b, sill = sill, head = head, outwardSign = alongX ? outward.z : outward.x });          // (ClearViews keeps these clear)
'@
Rep '            GlassFronts.Clear();' @'
            GlassFronts.Clear();
            glassSpans.Clear();
'@
Rep @'
                if (wallObjects.TryGetValue(w, out var go) && go) { var rr = go.GetComponent<Renderer>(); if (rr) rr.enabled = true; }
            windowedWalls.Clear();
'@ @'
                if (wallObjects.TryGetValue(w, out var go) && go) { var rr = go.GetComponent<Renderer>(); if (rr) rr.enabled = true; }
            windowedWalls.Clear();
            glassSpans.Clear();
'@
Save-Src

# ---- the layout is cleared once the windows and the glass are known, before anything is built from it
Open-Src ($A + 'Scripts\FloorGenerator.Interior.cs')
Rep '// floor 7: the pack''s wall pieces and ceiling tiles (FloorGenerator.WallSkin.cs;' @'
try { ClearViews(L); }          // (the owner, 2026-10-24: nothing hangs on a window, nothing blocks the view outside a glass front: FloorGenerator.ViewClear.cs)
            catch (System.Exception e) { Debug.LogWarning("[Views] The windows could not be cleared: " + e); }
            // floor 7: the pack's wall pieces and ceiling tiles (FloorGenerator.WallSkin.cs;
'@
Save-Src

# ---- the tests: the pure ones, and the scan of the finished floors
Open-Src ($A + 'Editor\NetDev\NetDevTest.cs')
Rep '            PlaytestFixesTest();' @'
            PlaytestFixesTest();
            ViewClearTest();
'@
Save-Src
Open-Src ($A + 'Editor\NetDev\NetDevPropScan.cs')
Rep 'int fails = 0, plants = 0, midRoom = 0, mounts = 0, floatingMounts = 0, floorProps = 0, floatingFloor = 0;' 'int fails = 0, plants = 0, midRoom = 0, mounts = 0, floatingMounts = 0, floorProps = 0, floatingFloor = 0, windowMounts = 0, viewBlockers = 0, cleared = 0;'
Rep '                var offenders = new List<string>();' @'
                var offenders = new List<string>();
                cleared += gen.ViewCleared;
                if (gen.ExteriorWindows.Count != gen.WindowCount) { fails++; offenders.Add($"the {gen.ExteriorWindows.Count} outside windows the clearing worked out are not the {gen.WindowCount} that were built"); }
'@
Rep '                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);' @'
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    // the owner, 2026-10-24: nothing hangs on a window (outside windows, glass fronts) and nothing blocks the view in the strip outside a glass front
                    var rcb = new Rc(b.min.x, b.min.z, b.max.x, b.max.z);
                    if (def.cat == PropCat.Mount)
                    {
                        bool onWindow = false, onGlass = false;
                        foreach (var w in gen.ExteriorWindows) if (ViewClearRules.MountOnWall(w.alongX, w.line, w.a, w.b, w.sill, w.head, rcb, b.min.y, b.size.y)) { onWindow = true; break; }
                        foreach (var g in gen.GlassSpans) if (ViewClearRules.MountOnWall(g.alongX, g.line, g.a, g.b, g.sill, g.head, rcb, b.min.y, b.size.y)) { onGlass = true; break; }
                        if (onWindow || onGlass) { windowMounts++; offenders.Add($"wall prop '{def.key}' hangs on {(onWindow ? "an outside window" : "a glass front")} at ({b.center.x:0.0}, {b.center.y:0.0}, {b.center.z:0.0})"); }
                    }
                    else if (ViewClearRules.BlocksView(false, b.min.y, b.size.y))
                    {
                        foreach (var g in gen.GlassSpans)
                            if (rcb.Overlaps(ViewClearRules.Strip(g.alongX, g.line, g.half, g.lo, g.hi, g.outwardSign, ViewClearRules.ClearDepth))) { viewBlockers++; offenders.Add($"'{def.key}' ({b.size.y:0.0} m high) stands in the view outside a glass front at ({b.center.x:0.0}, {b.center.z:0.0})"); break; }
                    }
'@
Rep '            fails = midRoom + floatingMounts + floatingFloor;' '            fails += midRoom + floatingMounts + floatingFloor + windowMounts + viewBlockers;'
Rep '            Log($"[PropScan] plants {plants} (in the middle of a room: {midRoom}); wall props {mounts} (hanging in the air: {floatingMounts}); floor props {floorProps} (floating: {floatingFloor})");' '            Log($"[PropScan] plants {plants} (in the middle of a room: {midRoom}); wall props {mounts} (hanging in the air: {floatingMounts}; on a window: {windowMounts}); floor props {floorProps} (floating: {floatingFloor}; blocking the view outside glass: {viewBlockers}); taken out for the windows: {cleared}");'
Save-Src
"view clearing patched"
