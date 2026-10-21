# Round 12, batch G07: two things found by the full suite.
# 1) The moon light of the night city's buildings (a second directional light in the scene) is taken out again: the buildings are lit by the night's own sun (FloorGenerator.FindSun takes the first directional light it
#    finds when the scene has no sun assigned, so a second one could be taken for it), and the picture showed the moon changing little. The buildings stay on the default layer.
# 2) NetDevCombat: the guard duels are timing-fragile. A patrolling guard walks on for the 0.7 s his gun is made in, and the spot for the player, worked out before, can then be behind a partition from where he
#    stands (the SMG duel found exactly that once the floors took a little longer to build). The spot is worked out again from where he stands when the player is put there.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

# ---- the Combat test: the temporary debug lines go (the file before them is restored), then the robust spot
if (-not $env:R2_DRY) { Copy-Item 'C:\bqs\bak_NetDevCombat_before_debug.cs' 'C:\bqm\Assets\Editor\NetDev\NetDevCombat.cs' -Force }
else { Write-Output 'dry run: would restore NetDevCombat.cs from the backup before the debug lines' }
Open-Src $(if ($env:R2_DRY) { 'C:\bqs\bak_NetDevCombat_before_debug.cs' } else { 'C:\bqm\Assets\Editor\NetDev\NetDevCombat.cs' })
Rep @'
            yield return new WaitForSeconds(0.7f);
            var gg = shooter.Gun;
'@ @'
            yield return new WaitForSeconds(0.7f);
            // (he walked on while his gun was made: the spot is worked out again from where he stands now, a partition may be between him and the first one)
            Vector3 spotNow = spot; bool clearNow = false;
            for (int retry = 0; retry < 6 && !clearNow; retry++)
            {
                clearNow = OpenSpot(shooter.transform.position, dist, out spotNow, false);
                if (!clearNow) yield return new WaitForSeconds(0.3f);
            }
            if (clearNow) spot = spotNow; else Log($"[Combat] {label}: {shooter.name} has no clear line at {dist:0} m from where he stands now: the first spot is used");
            var gg = shooter.Gun;
'@
if ($env:R2_DRY) { Write-Output 'dry run: ok' } else { Save-Src }

# ---- the night city: no moon light, no layer
Open-Src 'C:\bqm\Assets\Scripts\Interior\ExteriorCity.cs'
RepRx '\r?\n[ ]*/// <summary>How bright the moon is on the owner.s buildings[^\r\n]*\r?\n[ ]*public const float MoonIntensity = 0\.28f;' ''
RepRx 'if \(stats\.buildings > 0\) Moon\(root\.transform\);\r?\n\r?\n[ ]*var roof = ' 'var roof = '
RepRegion "/// <summary>The moon's light for the owner's buildings" 'static void AddMesh(Transform parent, OwnedObjects owned, Mesh mesh, Material mat, string name)' 'static void AddMesh(Transform parent, OwnedObjects owned, Mesh mesh, Material mat, string name)'
Rep 'room, room, cap, 1f, null, CityBuildings.Layer)' 'room, room, cap, 1f, null, -1)'
Save-Src

Open-Src 'C:\bqm\Assets\Scripts\Interior\CityBuildings.cs'
RepRx '[ ]*/// <summary>The user layer of the night city.s buildings[^\r\n]*\r?\n[ ]*public const int Layer = 30;\r?\n\r?\n' ''
RepRx ' The night city.s buildings are on <see cref="Layer"/>, so that the moon \(a light that lights only that layer\) leaves the rooms alone\.' ''
Save-Src

# ---- the CityNew test follows
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevCityNew.cs'
Rep '// ---- the buildings round the building, and the moon that lights them' '// ---- the buildings round the building'
RepRx '\r?\n[ ]*Light moon = null;' ''
Rep 'int offLayer = 0, withColliders = 0, tooBig = 0, floating = 0;' 'int withColliders = 0, tooBig = 0, floating = 0;'
RepRx '\r?\n[ ]*if \(bt\.GetComponentsInChildren<Transform>\(true\)\.Any\(x => x\.gameObject\.layer != CityBuildings\.Layer\)\) offLayer\+\+;' ''
Rep 'CityNewCheck("the buildings are on their own layer, with no colliders", offLayer == 0 && withColliders == 0, $"({offLayer} off the layer, {withColliders} colliders)");' 'CityNewCheck("the buildings have no colliders", withColliders == 0, $"({withColliders} colliders)");'
RepRx '\r?\n[ ]*var moonT = city\.Find\("CityMoon"\);\r?\n[ ]*moon = moonT \? moonT\.GetComponent<Light>\(\) : null;\r?\n[ ]*CityNewCheck\("the moon lights the buildings[^\r\n]*' ''
Rep 'the street from a window (three strengths of moon), the east side' 'the street from a window, the east side'
RepRegion 'string[] names = { "a", "b", "c" };' 'if (moon != null) moon.intensity = ExteriorCity.MoonIntensity;' @'
cam.transform.position = new Vector3(wx, 1.6f, 0.9f); cam.transform.rotation = Quaternion.Euler(24f, 180f, 0f);
            ShotCam(cam, $"citynew_floor{floor}_south");
'@
Save-Src
