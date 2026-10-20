# Round 2, batch 10: glass fronts on some offices (the owner: "see through ... glass windows"). Applies to BE_qa only.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'patchlib.ps1')
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
$Dir = Join-Path $PSScriptRoot 'BE_qa\Assets'

# the new file (kept in stage_glass until the QA tree is idle)
$src = Join-Path $PSScriptRoot 'stage_glass\FloorGenerator.Glass.cs'
$dst = Join-Path $Dir 'Scripts\FloorGenerator.Glass.cs'
if (-not $env:R2_DRY) { Copy-Item $src $dst -Force; Write-Output "copied $dst" }

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Interior.cs')
Rep @'
                             mine ? opWalls : pm ? pm : wallMat, walls);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
'@ @'
                             mine ? opWalls : pm ? pm : wallMat, walls);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                wallObjects[w] = go;
'@
Rep '            var walls = new GameObject("Partitions").transform;' @'
            wallObjects.Clear();
            windowedWalls.Clear();
            var walls = new GameObject("Partitions").transform;
'@
Rep '            try { BuildAccentWalls(interior, L); }' @'
            try { BuildOfficeWindows(interior, L); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Glass] The glass fronts could not be built, so the offices keep their plain walls: " + e);
                RestoreWindowedWalls();
                var half = interior.Find("OfficeWindows");
                if (half) { half.gameObject.SetActive(false); Kill(half.gameObject); }
            }
            try { BuildAccentWalls(interior, L); }
'@
Save-Src

Open-Src (Join-Path $Dir 'Scripts\FloorGenerator.Surfaces.cs')
Rep 'if (w.kind != "wall" || w.y1 - w.y0 < wallHeight * 0.8f) continue;' 'if (w.kind != "wall" || w.y1 - w.y0 < wallHeight * 0.8f || windowedWalls.Contains(w)) continue;'
Save-Src

Open-Src (Join-Path $Dir 'Editor\NetDev\NetDevRound2Look.cs')
Rep '                LookCheck($"{f} the city is there"' @'
                LookCheck($"{f} some offices have a glass front", gen.GlassOffices >= 3 && gen.GlassPanes >= 6 && gen.GlassFronts.Count >= 3, $"({gen.GlassOffices} offices, {gen.GlassPanes} panes, {gen.CornerOffices} in a corner of the building)");
                int glassWalls = 0, glassSolid = 0;
                foreach (var t in gen.Current.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Partition") continue;
                    var pr = t.GetComponent<MeshRenderer>();
                    var pc = t.GetComponent<BoxCollider>();
                    if (pr && !pr.enabled) { glassWalls++; if (pc && pc.enabled) glassSolid++; }
                }
                LookCheck($"{f} a wall with a glass front keeps its collider (nothing about walking or sight changes)", glassWalls == gen.GlassWallCount && glassSolid == glassWalls && glassWalls > 0, $"({glassWalls} walls, {glassSolid} with a collider)");
                LookCheck($"{f} the city is there"
'@
Rep '                int shots = 0;' @'
                foreach (var gf in gen.GlassFronts.Take(2)) wanted.Add(($"glass{n++}", gf.centre + gf.outward * 2.8f + Vector3.up * 0.05f, gf.centre + Vector3.down * 0.1f));
                int shots = 0;
'@
Save-Src

Write-Output 'batch 10 patched'
