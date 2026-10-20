# R10: streets and a few driving low-poly cars with headlights in the night city outside the windows.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"

$dst = "$Tree\Assets\Scripts\Interior\CityStreets.cs"
$src = [IO.File]::ReadAllText("$PSScriptRoot\CityStreets.cs.txt").Replace("`r`n", "`n").Replace("`n", "`r`n")
[IO.File]::WriteAllText($dst, $src, (New-Object Text.UTF8Encoding($false)))
"wrote CityStreets.cs"

Open-Src "$Tree\Assets\Scripts\Interior\ExteriorCity.cs"
if (-not $s.Contains('CityStreets.Build')) {
Rep '        public sealed class Stats { public int towers, meshes, quads; public float maxReach; }' '        public sealed class Stats { public int towers, meshes, quads, cars, roads; public float maxReach; }'
Rep @'
                        float w = step * (0.50f + 0.35f * Hash(hx + 11, hz)), d = step * (0.50f + 0.35f * Hash(hx, hz + 23));
                        float tx = centre.x + cx + (Hash(hx + 3, hz + 5) - 0.5f) * (step - w), tz = centre.z + cz + (Hash(hx + 7, hz + 9) - 0.5f) * (step - d);
'@ @'
                        // a tower stays inside its cell with a street's width to spare, so there are streets between the towers (CityStreets drives cars on them)
                        float room = step * (1f - CityStreets.StreetShare);
                        float w = room * (0.62f + 0.38f * Hash(hx + 11, hz)), d = room * (0.62f + 0.38f * Hash(hx, hz + 23));
                        float tx = centre.x + cx + (Hash(hx + 3, hz + 5) - 0.5f) * (room - w), tz = centre.z + cz + (Hash(hx + 7, hz + 9) - 0.5f) * (room - d);
'@
Rep @'
            AddMesh(root.transform, owned, gmesh, gm, "Streets");
'@ @'
            AddMesh(root.transform, owned, gmesh, gm, "Streets");

            // ---- the streets between the towers (wide dark roads, dashed centre lines) and the few cars that drive round the blocks with their headlights on
            try { stats.cars = CityStreets.Build(root.transform, owned, centre, halfX, halfZ, groundY, seed, cell[0], bandEnd[0], out stats.roads); }
            catch (System.Exception e) { Debug.LogWarning("[Scenery] The streets and the cars could not be built, so the city is towers only: " + e.Message); }
'@
Save-Src
}

Open-Src "$Tree\Assets\Scripts\FloorGenerator.Surfaces.cs"
if (-not $s.Contains('CityCars')) {
Rep '        public int CityTowers { get; private set; }' '        public int CityTowers { get; private set; }
        /// <summary>How many cars drive in the streets of the city outside, and how many roads there are (see <see cref="CityStreets"/>).</summary>
        public int CityCars { get; private set; }
        public int CityRoads { get; private set; }'
Rep '            WindowCount = 0; CityTowers = 0; CityMeshes = 0; CityReach = 0f; SceneryMs = 0;' '            WindowCount = 0; CityTowers = 0; CityMeshes = 0; CityReach = 0f; SceneryMs = 0; CityCars = 0; CityRoads = 0;'
Rep '                CityTowers = stats.towers; CityMeshes = stats.meshes; CityReach = stats.maxReach;' '                CityTowers = stats.towers; CityMeshes = stats.meshes; CityReach = stats.maxReach; CityCars = stats.cars; CityRoads = stats.roads;'
Save-Src
}
