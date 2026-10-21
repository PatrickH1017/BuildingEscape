# Round 12, batch G03 (the owner, 2026-10-19: "use the white plane as the plane in the cinematic now and use the car assets as the cars driving around the city"):
# a city catalog (the plane, the nine cars, the buildings of the packs), the Intro's jet is the white plane, the night city's cars are the pack's cars, and the CityNew scenario that looks at both.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }

# the new files are written with CRLF like the rest of the project
foreach ($f in @('C:\bqm\Assets\Scripts\Interior\CityCatalog.cs', 'C:\bqm\Assets\Editor\CityCatalogBuilder.cs', 'C:\bqm\Assets\Editor\NetDev\NetDevCityNew.cs')) {
  if (-not $env:R2_DRY) { $t = [IO.File]::ReadAllText($f); [IO.File]::WriteAllText($f, ($t -replace "`r?`n", "`r`n"), (New-Object Text.UTF8Encoding($false))) }
}

# ---- the Intro's jet: the white plane (the pack's nose is +z; the jet flies toward -x), the plain jet stays as the fallback
Open-Src 'C:\bqm\Assets\Scripts\Intro\BossOffice.cs'
Rep 'const float GroundY = -96f;' @'
const float PlaneScale = 4.0f;                            // the white plane (3.1 m long as the pack made it) flies about as big as the old plain jet: 12.4 m
        const float GroundY = -96f;
'@
RepRegion 'jet.localScale = Vector3.one * 1.7f;' 'Part(PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.05f, 0f), new Vector3(8.6f, 0.2f, 1.53f), gold, Vector3.zero);' @'
if (DressPlane(jet)) jet.localScale = Vector3.one * PlaneScale;
            else
            {
                jet.localScale = Vector3.one * 1.7f;
                var white = Mat(new Color(0.94f, 0.95f, 0.97f), 0.6f);
                var gold = Mat(new Color(0.85f, 0.65f, 0.18f), 0.6f, 0.5f);
                Part(PrimitiveType.Capsule, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(1.5f, 5.0f, 1.5f), white, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cube, "Wings", new Vector3(0.6f, -0.2f, 0f), new Vector3(2.3f, 0.14f, 11f), white, new Vector3(0f, 14f, 0f));
                Part(PrimitiveType.Cube, "Fin", new Vector3(4.3f, 1.35f, 0f), new Vector3(1.9f, 2.3f, 0.14f), gold, new Vector3(0f, 0f, -12f));
                Part(PrimitiveType.Cube, "Tailplane", new Vector3(4.4f, 0.55f, 0f), new Vector3(1.2f, 0.1f, 4.2f), white, Vector3.zero);
                Part(PrimitiveType.Cylinder, "EngineL", new Vector3(3.5f, 0.45f, -1.1f), new Vector3(0.8f, 1.2f, 0.8f), steel, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cylinder, "EngineR", new Vector3(3.5f, 0.45f, 1.1f), new Vector3(0.8f, 1.2f, 0.8f), steel, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cube, "Stripe", new Vector3(0f, 0.05f, 0f), new Vector3(8.6f, 0.2f, 1.53f), gold, Vector3.zero);
            }
'@
Rep '/// <summary>Where the jet is: <paramref name="progress"/>' @'
/// <summary>The owner's white plane (2026-10-19: the pack's ComPlaneV1; its nose is +z) as the jet: turned to fly toward -x, its landing gear up, centred on the jet's origin. False (nothing made) when the catalog has no plane.</summary>
        bool DressPlane(Transform parent)
        {
            var catalog = CityCatalog.Instance;
            var prefab = catalog ? catalog.plane : null;
            if (!prefab) return false;
            var model = Object.Instantiate(prefab, parent);
            model.name = "Plane";
            CityProps.Strip(model);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t != model.transform && t.name.Contains("LandingGear")) t.gameObject.SetActive(false);            // (it is in the air)
            model.transform.localScale = Vector3.one;
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            model.transform.localPosition = Vector3.zero;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.Destroy(model); return false; }
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            model.transform.localPosition = -parent.InverseTransformPoint(b.center);
            return true;
        }

        /// <summary>Where the jet is: <paramref name="progress"/>
'@
Save-Src

# ---- the night city's cars: the pack's cars (the box car stays as the fallback)
Open-Src 'C:\bqm\Assets\Scripts\Interior\CityStreets.cs'
Rep 'static Texture2D beamTexture;' @'
/// <summary>The pack's cars (2026-10-19) have their noses along +z, like the box car.</summary>
        const float CarYaw = 0f;
        /// <summary>A car's length (the pack's cars are 4.4 to 6.5 m long: all are fitted to about this) and where the bottom of its wheels is against its origin (the origin is 0.55 m above the ground, the road's top 0.15 m).</summary>
        const float CarLength = 4.7f, CarBottom = -0.40f;

        static Texture2D beamTexture;
'@
Rep 'var bodyMats = new Material[BodyColours.Length];' @'
var catalog = CityCatalog.Instance;
            var models = catalog && catalog.cars != null ? System.Array.FindAll(catalog.cars, g => g != null) : new GameObject[0];       // (the owner's cars; with none the cars are the plain boxes)
            bool useModels = models.Length > 0;
            var night = new Dictionary<Material, Material>();
            var bodyMats = new Material[BodyColours.Length];
'@
Rep 'for (int i = 0; i < BodyColours.Length; i++)' 'for (int i = 0; i < BodyColours.Length && !useModels; i++)'
Rep @'
                    int variant = (int)(Hash(seed, l * 7 + c, 7) * BodyColours.Length) % BodyColours.Length;
                    var go = new GameObject("Car");
                    go.transform.SetParent(holder.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = carMeshes[variant];
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = new[] { bodyMats[variant], darkMat, headMat, tailMat };
                    ConfigureRenderer(mr);
'@ @'
                    float pick = Hash(seed, l * 7 + c, 7);
                    var go = new GameObject("Car");
                    go.transform.SetParent(holder.transform, false);
                    if (useModels) DressCar(go.transform, models[(int)(pick * models.Length) % models.Length], night, owned);
                    else
                    {
                        int variant = (int)(pick * BodyColours.Length) % BodyColours.Length;
                        go.AddComponent<MeshFilter>().sharedMesh = carMeshes[variant];
                        var mr = go.AddComponent<MeshRenderer>();
                        mr.sharedMaterials = new[] { bodyMats[variant], darkMat, headMat, tailMat };
                        ConfigureRenderer(mr);
                    }
'@
Rep 'static void Glow(Transform car, Mesh mesh, Material mat, Vector3 at, float size)' @'
/// <summary>One of the owner's cars as a child of a car: turned to the car's frame, fitted to about 4.7 m, its wheels' bottom at the road, its materials made to glow by themselves (like the towers).</summary>
        static void DressCar(Transform car, GameObject prefab, Dictionary<Material, Material> night, OwnedObjects owned)
        {
            var model = Object.Instantiate(prefab, car);
            model.name = "Model " + prefab.name;
            CityProps.Strip(model);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.Euler(0f, CarYaw, 0f); model.transform.localScale = Vector3.one;
            var rs = model.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            float s = Mathf.Clamp(CarLength / Mathf.Max(0.5f, b.size.z), 0.7f, 1.15f);
            var at = car.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));            // (the car has just been made: it lies at its parent's frame, unscaled)
            model.transform.localScale = Vector3.one * s;
            model.transform.localPosition = new Vector3(-at.x * s, CarBottom - at.y * s, -at.z * s);
            foreach (var r in rs)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = NightOf(mats[i], night, owned);
                r.sharedMaterials = mats;
                ConfigureRenderer(r);
            }
        }

        /// <summary>The glowing copy of a pack material (one per material for the whole street; its lamps glow brighter than its paint).</summary>
        static Material NightOf(Material src, Dictionary<Material, Material> night, OwnedObjects owned)
        {
            if (!src) return null;
            if (!night.TryGetValue(src, out var m))
            {
                m = CityProps.NightCopy(src, CityProps.Glows(src) ? 1.6f : 0.55f);
                owned.items.Add(m);
                night[src] = m;
            }
            return m;
        }

        static void Glow(Transform car, Mesh mesh, Material mat, Vector3 at, float size)
'@
Save-Src

# ---- the CityNew scenario is registered
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevHarness.cs'
Rep 'mode != "assetpreview" && mode != "checklist"' 'mode != "assetpreview" && mode != "citynew" && mode != "checklist"'
Rep @'
        public static void AssetPreview()
'@ @'
        public static void CityNew()
        {
            SessionState.SetString("netdev.mode", "citynew");
            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");
            EditorApplication.isPlaying = true;
        }
        public static void AssetPreview()
'@
Rep @'
                case "assetpreview": yield return AssetPreviewScenario(); break;
'@ @'
                case "assetpreview": yield return AssetPreviewScenario(); break;
                case "citynew": yield return CityNewScenario(); break;
'@
Save-Src
