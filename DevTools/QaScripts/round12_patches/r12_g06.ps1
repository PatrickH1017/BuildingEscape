# Round 12, batch G06 (the owner, 2026-10-19: "You can keep some of the tall non asset buildings sporadically as they look pretty good for the skyscrapers in the distance"):
# the Intro's pack towers only stand in the nearer rows (seven rows, from z 288 m to 736 m) and get fewer with every row (62 % of the plots of the nearest row, 8 % of the seventh), so the old glass towers
# keep the distance and stand sporadically between the pack's towers; the CityNew scenario checks that the old skyscrapers are still there.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

Open-Src 'C:\bqm\Assets\Scripts\Intro\BossOffice.cs'
Rep 'for (int jp = 0; jp < 10; jp++)' 'for (int jp = 0; jp < 7; jp++)'
Rep 'if (!PackCone(x, z) || Hash(ip + 700, jp + 700) >= 0.62f) continue;' 'if (!PackCone(x, z) || Hash(ip + 700, jp + 700) >= 0.62f - 0.085f * jp) continue;                       // (fewer the farther: the old glass skyscrapers keep the distance)'
Rep @'
/// <summary>Stands the pack's towers (on about 60 % of the plots in the cone) four times their size, with the aerial perspective of the other towers; returns the plots that got one (the box towers there stay out).</summary>
'@ @'
/// <summary>Stands the pack's towers four times their size, with the aerial perspective of the other towers, on the plots of the cone: 62 % of the nearest row, 8 % of the seventh, none farther (the owner, 2026-10-19: keep some of the old tall towers sporadically, they make good skyscrapers in the distance); returns the plots that got one (the box towers there stay out).</summary>
'@
Save-Src

Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevCityNew.cs'
Rep 'towers >= 30 && towersOff == 0 && towerColliders == 0' 'towers >= 12 && towersOff == 0 && towerColliders == 0'
Rep @'
            // the boss from the front (the Medium shot: the skyline is his background), then the window shot with the plane at three places
'@ @'
            // the old glass skyscrapers still stand in the distance (the farthest band's mesh reaches 100 m or more over the street) and between the pack's towers (the middle band's too)
            foreach (string bandName in new[] { "City1", "City2" })
            {
                var bandT = office.root.Find(bandName);
                var bandMesh = bandT ? bandT.GetComponent<MeshFilter>().sharedMesh : null;
                CityNewCheck($"the old skyscrapers still stand in {bandName}", bandMesh != null && bandMesh.bounds.max.y + 96f >= 100f && bandMesh.vertexCount > 3000, $"(tallest {(bandMesh ? bandMesh.bounds.max.y + 96f : 0f):0} m, {(bandMesh ? bandMesh.vertexCount : 0)} vertices)");
            }
            // the boss from the front (the Medium shot: the skyline is his background), then the window shot with the plane at three places
'@
Save-Src
