using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Contact sheets of the plane, car and city prefabs the owner added (2026-10-19), to choose from: each sheet is a row of prefabs, left to right in the order of the log (<c>[Preview]</c> lines give the names and sizes).
    /// <c>qa_regress.ps1 -Methods AssetPreview:gfx</c>; pictures %TEMP%\be_shot_preview_*.png.
    /// </summary>
    public partial class NetDevRunner
    {
        static List<string> PreviewPaths(string folder, int skip = 0, int take = 99, string without = null)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            return guids.Select(AssetDatabase.GUIDToAssetPath).Where(p => without == null || !p.Contains(without)).OrderBy(p => p).Skip(skip).Take(take).ToList();
        }

        IEnumerator PreviewSheet(Transform root, Camera cam, string name, List<string> paths)
        {
            var objs = new List<GameObject>();
            var sizes = new List<Vector3>();
            float x = 0f;
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) { Log($"[Preview] {name}: cannot load {path}"); continue; }
                var go = Instantiate(prefab, root);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Log($"[Preview] {name}: {prefab.name} has no renderer"); Destroy(go); continue; }
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float w = Mathf.Max(b.size.x, b.size.z);
                x += w * 0.5f;
                go.transform.position += new Vector3(x - b.center.x, -b.min.y, root.position.z - b.center.z);
                objs.Add(go); sizes.Add(b.size);
                Log($"[Preview] {name} #{objs.Count}: {prefab.name}  size {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0} m, {rs.Length} renderers");
                x += w * 0.5f + 0.35f * w;
            }
            if (objs.Count == 0) yield break;
            float maxH = sizes.Max(s => s.y);
            cam.fieldOfView = 36f;
            cam.transform.position = new Vector3(x * 0.5f, maxH * 0.8f + x * 0.12f, root.position.z - x * 0.7f);
            cam.transform.LookAt(new Vector3(x * 0.5f, maxH * 0.35f, root.position.z));
            yield return null;
            ShotCam(cam, "preview_" + name);
            foreach (var g in objs) Destroy(g);
            yield return null;
        }

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
        {
            var hm = HeistManager.Instance; var tm = TowerManager.Instance;
            yield return WaitFor(() => hm != null && tm != null && !tm.Loading && hm.Phase == HeistPhase.Hub, 90, "basement");
            var root = new GameObject("AssetPreview").transform;
            root.position = new Vector3(0f, 0f, 800f);
            var sun = new GameObject("PreviewSun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            var cam = new GameObject("PreviewCam").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.55f, 0.66f, 0.82f); cam.nearClipPlane = 0.2f; cam.farClipPlane = 3000f; cam.enabled = false;

            yield return DartPreview(root, cam);
            Log("[Preview] DONE");
            Quit(0);
        }
    }
}
