# Round 12, batch G02: the asset preview scenario now makes ONE picture of each plane in two views (side, top) and logs the average colour of each: to pick the white plane and see which way its nose points.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevAssetPreview.cs'
Rep @'
        IEnumerator AssetPreviewScenario()
'@ @'
        /// <summary>Each plane (the first prefab of each name) in two views, side (the camera on +x looking -x) above, top (screen up = +z, screen right = +x) below, in one picture; the average colour of each is logged.</summary>
        IEnumerator PlaneGrid(Transform root, Camera cam)
        {
            var chosen = new List<string>();
            var seen = new HashSet<string>();
            foreach (var p in PreviewPaths("Assets/Alstra Infinite/Planes LowPoly/Prefabs"))
            {
                string n = System.IO.Path.GetFileNameWithoutExtension(p);
                if (!n.StartsWith("Helicopter") && seen.Add(n)) chosen.Add(p);
            }
            const int T = 256;
            var sheet = new Texture2D(T * chosen.Count, T * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(T, T, 24);
            cam.orthographic = true;
            var bg = cam.backgroundColor;
            for (int i = 0; i < chosen.Count; i++)
            {
                var go = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(chosen[i]), root);
                go.transform.position = root.position; go.transform.rotation = Quaternion.identity;
                var rs = go.GetComponentsInChildren<Renderer>();
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                cam.orthographicSize = b.extents.magnitude * 0.9f;
                float sumR = 0f, sumG = 0f, sumB = 0f; int count = 0;
                for (int view = 0; view < 2; view++)
                {
                    if (view == 0) { cam.transform.position = b.center + Vector3.right * 60f; cam.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up); }
                    else { cam.transform.position = b.center + Vector3.up * 60f; cam.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward); }
                    yield return null;
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt;
                    var tile = new Texture2D(T, T, TextureFormat.RGB24, false);
                    tile.ReadPixels(new Rect(0, 0, T, T), 0, 0); tile.Apply();
                    RenderTexture.active = null; cam.targetTexture = null;
                    var px = tile.GetPixels();
                    if (view == 0) foreach (var c in px) if (Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) > 0.12f) { sumR += c.r; sumG += c.g; sumB += c.b; count++; }
                    sheet.SetPixels(i * T, (1 - view) * T, T, T, px);
                    Destroy(tile);
                }
                Log($"[Preview] plane {i}: {go.name.Replace("(Clone)", "")}  length {Mathf.Max(b.size.x, b.size.z):0.0} m, size {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0}, average colour {(count > 0 ? sumR / count : 0f):0.00} / {(count > 0 ? sumG / count : 0f):0.00} / {(count > 0 ? sumB / count : 0f):0.00}");
                Destroy(go);
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_preview_planegrid.png"), sheet.EncodeToPNG());
            cam.orthographic = false;
        }

        IEnumerator AssetPreviewScenario()
'@
Rep @'
            var planes = PreviewPaths("Assets/Alstra Infinite/Planes LowPoly/Prefabs");
            planes.AddRange(PreviewPaths("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs", 0, 1));          // (Air Plane_1, the first by name)
            yield return PreviewSheet(root, cam, "planes", planes);
            var cars = PreviewPaths("Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs", 1);
            yield return PreviewSheet(root, cam, "cars_a", cars.Take(5).ToList());
            yield return PreviewSheet(root, cam, "cars_b", cars.Skip(5).ToList());
            var bld = PreviewPaths("Assets/SimplePoly City - Low Poly Assets/Prefab/Buildings");
            yield return PreviewSheet(root, cam, "buildings_a", bld.Where(p => p.Contains("Sky_")).ToList());
            yield return PreviewSheet(root, cam, "buildings_b", bld.Where(p => p.Contains("Residential") || p.Contains("House_01_color01") || p.Contains("Factory") || p.Contains("Stadium") || p.Contains("Super Market") || p.Contains("Bakery") || p.Contains("Bar.prefab")).ToList());
'@ @'
            yield return PlaneGrid(root, cam);
'@
Save-Src
