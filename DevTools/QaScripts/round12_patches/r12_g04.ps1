# Round 12, batch G04 (the owner, 2026-10-19: "use [the city assets] to make the city outside the windows look more realistic" / "have the city building assets in the background of the cinematic intro" /
# "the city doesn't need to be massive, just surrounding the building and big enough where a player feels like the building is really in a big city"):
# the night city's near ring is the pack's buildings (lit by a moon that lights their layer only), the Intro's skyline has the pack's towers (four times their size, with the aerial perspective of the box towers),
# the plane flies a little bigger, and the CityNew scenario looks at all of it.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

foreach ($f in @('C:\bqm\Assets\Scripts\Interior\CityBuildings.cs', 'C:\bqm\Assets\Editor\NetDev\NetDevCityNew.cs')) {
  if (-not $env:R2_DRY) { $t = [IO.File]::ReadAllText($f); [IO.File]::WriteAllText($f, ($t -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false))) }
}

# ---- the Intro: the plane a little bigger, the pack's towers as the skyline
Open-Src 'C:\bqm\Assets\Scripts\Intro\BossOffice.cs'
RepRx 'const float PlaneScale = 4\.0f;[^\r\n]*' 'const float PlaneScale = 5.0f;                            // the white plane (3.1 m long as the pack made it) flies a little bigger than the old plain jet (12.6 m): 15.5 m, so that it reads on the small TV'
Rep 'float[] zNear = { 14f, 260f, 640f }, zFar = { 260f, 640f, 1500f }, cell = { 24f, 32f, 46f };' @'
var packed = PackTowers();                                  // (the owner's towers first: the box towers of their plots stay out)
            float[] zNear = { 14f, 260f, 640f }, zFar = { 260f, 640f, 1500f }, cell = { 24f, 32f, 46f };
'@
Rep 'float roll = Hash(hx + 31, hz + 41);' @'
if (band >= 1 && packed.Contains(PackIndex(cx, cz))) continue;                                              // (one of the owner's towers stands there)
                        float roll = Hash(hx + 31, hz + 41);
'@
Rep 'Material Mat(Color color, float smoothness = 0.3f, float metallic = 0f)' @'
// ---- the owner's towers (2026-10-19): the pack's tall buildings, made big enough to be seen from far, as the skyline the windows look at

        const float PackCell = 64f, PackZ0 = 288f;                // the plots: 64 m squares in rows from z = 288 m outward

        /// <summary>The cone of the view the windows give: the plots outside it would never be seen.</summary>
        static bool PackCone(float x, float z) => Mathf.Abs(x) < 0.65f * z + 40f;

        /// <summary>The plot of the pack's grid a spot lies in (column, row).</summary>
        static Vector2Int PackIndex(float x, float z) => new Vector2Int(Mathf.FloorToInt((x + PackCell * 0.5f) / PackCell), Mathf.FloorToInt((z - PackZ0) / PackCell));

        /// <summary>Stands the pack's towers (on about 60 % of the plots in the cone) four times their size, with the aerial perspective of the other towers; returns the plots that got one (the box towers there stay out).</summary>
        HashSet<Vector2Int> PackTowers()
        {
            var placed = new HashSet<Vector2Int>();
            var pool = CityBuildings.Towers(CityCatalog.Instance);
            if (pool.Length == 0) return placed;
            for (int jp = 0; jp < 10; jp++)
                for (int ip = -14; ip <= 14; ip++)
                {
                    float x = ip * PackCell, z = PackZ0 + (jp + 0.5f) * PackCell;
                    if (!PackCone(x, z) || Hash(ip + 700, jp + 700) >= 0.62f) continue;
                    float haze = 0.7f * Mathf.Min(0.97f, 1f - Mathf.Exp(-Mathf.Sqrt(x * x + z * z) / 560f));
                    var go = CityBuildings.Place(root, pool[(int)(Hash(ip + 911, jp + 913) * pool.Length) % pool.Length], root.TransformPoint(new Vector3(x, GroundY, z)),
                        90f * Mathf.Floor(Hash(ip + 917, jp + 919) * 4f), PackCell - 12f, PackCell - 12f, 150f, 4f, src => Hazed(src, haze), -1);
                    if (go != null) placed.Add(new Vector2Int(ip, jp));
                }
            return placed;
        }

        /// <summary>A copy of a pack material for the daytime city: its picture dimmed by <paramref name="haze"/> with the haze's colour added (the aerial perspective the other towers have in their vertex colours).</summary>
        Material Hazed(Material src, float haze)
        {
            var m = new Material(Shader.Find("Standard")) { color = new Color(1f - haze, 1f - haze, 1f - haze, 1f), mainTexture = src ? src.mainTexture : null };
            m.SetFloat("_Glossiness", 0f); m.SetFloat("_Metallic", 0f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.SetColor("_EmissionColor", Haze * haze);
            owned.Add(m);
            return m;
        }

        Material Mat(Color color, float smoothness = 0.3f, float metallic = 0f)
'@
Save-Src

# ---- the night city: the pack's buildings in the near ring, and the moon that lights them
Open-Src 'C:\bqm\Assets\Scripts\Interior\ExteriorCity.cs'
Rep 'public sealed class Stats { public int towers, meshes, quads, cars, roads; public float maxReach; }' 'public sealed class Stats { public int towers, meshes, quads, cars, roads, buildings; public float maxReach; }'
Rep 'public const float KeepClear = 38f;' @'
public const float KeepClear = 38f;
        /// <summary>The owner's buildings (2026-10-19) stand in most plots of the near band out to this far from the building's middle; farther out are the box towers.</summary>
        public const float BuildingReach = 160f;
        /// <summary>How bright the moon is on the owner's buildings (a directional light that lights their layer only).</summary>
        public const float MoonIntensity = 0.28f;
'@
Rep 'var groups = new Dictionary<int, MeshBuilder>();' @'
var groups = new Dictionary<int, MeshBuilder>();
            var pool = CityBuildings.Pool(CityCatalog.Instance);              // (the owner's buildings for the near ring; empty with no catalog)
'@
Rep 'h = Mathf.Min(h, floorAbove + 6f + 0.35f * edge + 40f * Hash(hx + 103, hz + 107));' @'
float cap = floorAbove + 6f + 0.35f * edge + 40f * Hash(hx + 103, hz + 107);
                        h = Mathf.Min(h, cap);
'@
Rep 'AddTower(groups, band, tx, tz, w, d, groundY, groundY + h, Hash(hx + 79, hz + 83), Hash(hx + 43, hz + 47), stats);' @'
// the owner's buildings stand in most plots of the near ring; where none fits (and in one plot in four) the plot gets a box tower, with its lit windows
                        bool built = false;
                        if (band == 0 && pool.Length > 0 && r < BuildingReach && Hash(hx + 233, hz + 239) < 0.75f)
                            for (int attempt = 0; attempt < 3 && !built; attempt++)
                                built = CityBuildings.Place(root.transform, pool[(int)(Hash(hx + 211 + attempt * 17, hz + 223) * pool.Length) % pool.Length], new Vector3(centre.x + cx, groundY, centre.z + cz),
                                    90f * Mathf.Floor(Hash(hx + 227, hz + 229) * 4f), room, room, cap, 1f, null, CityBuildings.Layer) != null;
                        if (built) stats.buildings++;
                        else AddTower(groups, band, tx, tz, w, d, groundY, groundY + h, Hash(hx + 79, hz + 83), Hash(hx + 43, hz + 47), stats);
'@
Rep 'var roof = new Color(0.016f, 0.019f, 0.030f);' @'
if (stats.buildings > 0) Moon(root.transform);

            var roof = new Color(0.016f, 0.019f, 0.030f);
'@
Rep 'static void AddMesh(Transform parent, OwnedObjects owned, Mesh mesh, Material mat, string name)' @'
/// <summary>The moon's light for the owner's buildings: a dim bluish directional light from where the sky's moon is (azimuth 254 degrees, 43 above the horizon), on the buildings' layer only, so the rooms stay as they are.</summary>
        static void Moon(Transform parent)
        {
            var go = new GameObject("CityMoon");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(0.2024f, -0.6777f, 0.7059f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional; l.color = new Color(0.66f, 0.76f, 1f); l.intensity = MoonIntensity; l.shadows = LightShadows.None; l.cullingMask = 1 << CityBuildings.Layer;
        }

        static void AddMesh(Transform parent, OwnedObjects owned, Mesh mesh, Material mat, string name)
'@
Save-Src
