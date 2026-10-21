# Round 12, batch G09: the asset preview scenario looks at the Synty dart board (SM_Prop_Darkboard_01 of the Office pack: its name has a typo) - its size, its renderers and six views of it - to learn which way it faces
# and how big it is, before the darts game is built round it (the owner, 2026-10-19 night).
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\NetDev\NetDevAssetPreview.cs'
Rep 'yield return PlaneGrid(root, cam);' 'yield return DartPreview(root, cam);'
Rep @'
        IEnumerator AssetPreviewScenario()
'@ @'
        /// <summary>The Synty dart board: its size, its renderers and six views of it in one picture (from +x, -x, +z, -z, above, below), to learn which way it faces.</summary>
        IEnumerator DartPreview(Transform root, Camera cam)
        {
            string path = "Assets/Synty/PolygonOffice/Prefabs/Props/Wall Props/SM_Prop_Darkboard_01.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) { Log("[Preview] no dart board prefab at " + path); yield break; }
            var go = Instantiate(prefab, root);
            go.transform.position = root.position; go.transform.rotation = Quaternion.identity;
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Log($"[Preview] dartboard: size {b.size.x:0.000} x {b.size.y:0.000} x {b.size.z:0.000} m, centre {(b.center - root.position)}, {rs.Length} renderers, the prefab's origin is {(root.position - b.center)} from the centre");
            foreach (var r in rs) Log($"[Preview]   renderer {r.name}: {r.sharedMaterials.Length} materials ({string.Join(", ", r.sharedMaterials.Select(m => m ? m.name : "null"))}), bounds {r.bounds.size.x:0.000} x {r.bounds.size.y:0.000} x {r.bounds.size.z:0.000}");
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Log($"[Preview]   collider {c.GetType().Name} on {c.name}");
            const int T = 256;
            var sheet = new Texture2D(T * 3, T * 2, TextureFormat.RGB24, false);
            var rt = new RenderTexture(T, T, 24);
            cam.orthographic = true; cam.orthographicSize = b.extents.magnitude * 1.05f;
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down };
            var ups = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.forward, Vector3.forward };
            for (int i = 0; i < 6; i++)
            {
                cam.transform.position = b.center + dirs[i] * 20f;
                cam.transform.rotation = Quaternion.LookRotation(-dirs[i], ups[i]);
                yield return null;
                cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt;
                var tile = new Texture2D(T, T, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, T, T), 0, 0); tile.Apply();
                RenderTexture.active = null; cam.targetTexture = null;
                sheet.SetPixels((i % 3) * T, (1 - i / 3) * T, T, T, tile.GetPixels());
                Destroy(tile);
            }
            sheet.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "be_shot_preview_dartboard.png"), sheet.EncodeToPNG());
            cam.orthographic = false;
        }

        IEnumerator AssetPreviewScenario()
'@
Save-Src
