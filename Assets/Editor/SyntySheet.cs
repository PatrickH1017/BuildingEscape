using System.Collections.Generic;
using System.IO;
using System.Linq;
using BuildingEscape.Net;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Round 4 (A6 / A7): contact sheets of the Synty office pack, drawn with the pack's own (Shader Graph) materials and with the one Standard atlas material the game puts on its people.
    /// Run in batch mode WITH graphics: <c>-executeMethod BuildingEscape.EditorTools.SyntySheet.Batch</c>; the pictures go to the folder in the environment variable SYNTY_SHEET_DIR (default: the temp folder).
    /// </summary>
    public static class SyntySheet
    {
        const string Root = "Assets/Synty/PolygonOffice/Prefabs";

        public static void Batch()
        {
            var dir = System.Environment.GetEnvironmentVariable("SYNTY_SHEET_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
            var all = AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            var chars = all.Where(p => p.Contains("/Characters/") && !p.Contains("Attachment")).ToList();
            var cat = Resources.Load<CharacterCatalog>("CharacterCatalog");
            var atlas = cat ? cat.characters.Where(c => c.atlas).Select(c => c.atlas).FirstOrDefault() : null;
            Debug.Log($"[Synty] prefabs {all.Count}, characters {chars.Count}, atlas {(atlas ? atlas.name : "none")}");

            if (System.Environment.GetEnvironmentVariable("SYNTY_REIMPORT") == "1") ReimportShaderGraphs();
            Sheet(chars, Path.Combine(dir, "synty_chars_native.png"), null, 6, 256);
            if (atlas) Sheet(chars, Path.Combine(dir, "synty_chars_atlas.png"), atlas, 6, 256);

            var wanted = new[]
            {
                "Desk_01", "Desk_03", "Desk_05", "Chair_01", "Chair_05", "Couch_01", "Shelf_01", "Cabinets_01", "Table_Conference_01", "CubicalWall_01", "Divider_01", "Table_Round_01",
                "Monitor", "Computer", "Laptop", "Keyboard", "Phone", "Plant", "Water", "Printer", "Copier", "Fridge", "Vending", "Clock", "Darkboard", "Whiteboard", "Bin_", "Lamp",
                "Door_01", "Elevator_01", "Wall_Glass_01", "Floor_Carpet_01", "Ceiling_Panel_Light_01", "Pillar_Interior_01", "Stairs_Carpet_01", "Wall_Blank_Window_01",
            };
            var props = new List<string>();
            foreach (var w in wanted)
            {
                var hit = all.FirstOrDefault(p => !p.Contains("/Characters/") && Path.GetFileNameWithoutExtension(p).Contains(w));
                Debug.Log($"[Synty] wanted '{w}' -> {(hit != null ? hit.Substring(Root.Length) : "NOTHING")}");
                if (hit != null && !props.Contains(hit)) props.Add(hit);
            }
            Sheet(props, Path.Combine(dir, "synty_props_native.png"), null, 8, 224);
            Debug.Log("[Synty] sheets written to " + dir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Measures every prefab under the pack's Props / Buildings / Generic folders (size, offset of the bounds' middle from the pivot, lowest point, renderers) into the tab separated file
        /// synty_measure.tsv in SYNTY_SHEET_DIR: the numbers PropTable needs (batch mode, no graphics needed).
        /// </summary>
        public static void Measure()
        {
            var dir = System.Environment.GetEnvironmentVariable("SYNTY_SHEET_DIR");
            if (string.IsNullOrEmpty(dir)) dir = Path.GetTempPath();
            var all = AssetDatabase.FindAssets("t:Prefab", new[] { Root }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sb = new System.Text.StringBuilder("path\tsx\tsy\tsz\tcx\tcz\tminY\trenderers\n");
            foreach (var p in all)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rs = inst.GetComponentsInChildren<Renderer>(true).Where(x => x && !(x is ParticleSystemRenderer)).ToArray();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                    sb.Append(p.Substring(Root.Length + 1)).Append("\t").Append(b.size.x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(b.size.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t")
                      .Append(b.size.z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(b.center.x.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(b.center.z.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t")
                      .Append(b.min.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append("\t").Append(rs.Length).Append("\n");
                }
                Object.DestroyImmediate(inst);
            }
            File.WriteAllText(Path.Combine(dir, "synty_measure.tsv"), sb.ToString());
            Debug.Log("[Synty] measured " + all.Count + " prefabs into " + Path.Combine(dir, "synty_measure.tsv"));
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// A contact sheet of the prefabs listed in the file SYNTY_LIST (one path per line, relative to the pack's Prefabs folder, with or without ".prefab"), written to SYNTY_OUT (png).
        /// SYNTY_TILE (pixels, default 192), SYNTY_COLS (default 8) and SYNTY_YAW (degrees the prop is turned, default 160; 180 = seen from +Z, i.e. its front when the front is +Z) are optional.
        /// </summary>
        public static void Pick()
        {
            string Env(string n, string d) { var v = System.Environment.GetEnvironmentVariable(n); return string.IsNullOrEmpty(v) ? d : v; }
            var lines = File.ReadAllLines(Env("SYNTY_LIST", "")).Select(l => l.Trim()).Where(l => l.Length > 0).Select(l => l.EndsWith(".prefab") ? l : l + ".prefab").Select(l => Root + "/" + l).ToList();
            Sheet(lines, Env("SYNTY_OUT", Path.Combine(Path.GetTempPath(), "synty_pick.png")), null, int.Parse(Env("SYNTY_COLS", "8")), int.Parse(Env("SYNTY_TILE", "192")), float.Parse(Env("SYNTY_YAW", "160"), System.Globalization.CultureInfo.InvariantCulture));
            EditorApplication.Exit(0);
        }

        /// <summary>Imports the pack's shader graphs again, sub graphs first, and says how each one came out.</summary>
        public static void ReimportShaderGraphs()
        {
            var graphs = AssetDatabase.FindAssets("", new[] { "Assets/Synty" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".shadergraph") || p.EndsWith(".shadersubgraph")).OrderBy(p => p.EndsWith(".shadersubgraph") ? 0 : 1).ToList();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var p in graphs) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();
            Debug.Log($"[Synty] re-imported {graphs.Count} shader graphs in {watch.Elapsed.TotalSeconds:0} s");
            foreach (var p in graphs.Where(g => g.EndsWith(".shadergraph")))
            {
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(p);
                Debug.Log($"[Synty] graph {Path.GetFileNameWithoutExtension(p)}: {(sh ? sh.name + (sh.isSupported ? " supported" : " NOT SUPPORTED") + (ShaderUtil.ShaderHasError(sh) ? " HAS ERRORS" : "") : "no shader")}");
            }
        }

        static void Sheet(List<string> paths, string file, Texture2D forceAtlas, int cols, int tile, float yaw = 160f)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var forceMaterial = forceAtlas ? ToonCharacterFactory.AtlasMaterialFor(forceAtlas) : null;
            var light = new GameObject("SheetLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var cam = new GameObject("SheetCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f); cam.fieldOfView = 40f; cam.enabled = false;
            int rows = (paths.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * tile, rows * tile, TextureFormat.RGB24, false);
            var rt = new RenderTexture(tile, tile, 24);
            var one = new Texture2D(tile, tile, TextureFormat.RGB24, false);
            int magentaTiles = 0, rendered = 0;
            var shaders = new HashSet<string>();
            for (int i = 0; i < paths.Count; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (!prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
                var rs = inst.GetComponentsInChildren<Renderer>(false).Where(x => x && x.enabled).ToArray();
                if (rs.Length > 0)
                {
                    foreach (var r in rs)
                    {
                        if (forceMaterial) { var m = new Material[r.sharedMaterials.Length]; for (int k = 0; k < m.Length; k++) m[k] = forceMaterial; r.sharedMaterials = m; }
                        foreach (var m in r.sharedMaterials) if (m && m.shader) shaders.Add(m.shader.name + (m.shader.isSupported ? "" : " (NOT SUPPORTED)"));
                    }
                    var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                    float radius = Mathf.Max(0.05f, b.extents.magnitude);
                    cam.transform.position = b.center + new Vector3(0.6f, 0.4f, -1f).normalized * (radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f);
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.01f, radius * 0.05f); cam.farClipPlane = radius * 8f + 10f;
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt; one.ReadPixels(new Rect(0, 0, tile, tile), 0, 0); one.Apply(); RenderTexture.active = null; cam.targetTexture = null;
                    rendered++;
                    int pink = RenderAudit.CountMagenta(one.GetPixels32());
                    if (pink >= 25) { magentaTiles++; Debug.Log($"[Synty] MAGENTA ({pink} px): {paths[i]}"); }
                    int cx = (i % cols) * tile, cy = (rows - 1 - i / cols) * tile;
                    sheet.SetPixels(cx, cy, tile, tile, one.GetPixels());
                }
                Object.DestroyImmediate(inst);
            }
            sheet.Apply();
            File.WriteAllBytes(file, sheet.EncodeToPNG());
            Debug.Log($"[Synty] {Path.GetFileName(file)}: {rendered} of {paths.Count} drawn, {magentaTiles} show magenta; shaders: {string.Join(" | ", shaders)}");
            Object.DestroyImmediate(rt); Object.DestroyImmediate(one); Object.DestroyImmediate(sheet);
        }
    }
}
