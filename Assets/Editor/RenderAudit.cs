using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Finds everything in the project that would draw pink (magenta) in this built-in render pipeline project, and
    /// converts the materials of packs made for URP / shader graph to the Standard shader.
    ///
    /// Four independent checks:
    ///   1. every material asset: is its shader missing, hidden (the engine's error shader) or unsupported?
    ///   2. every prefab and model: a renderer slot with no material, or with a broken one;
    ///   3. every scene: the same for what is placed in it, plus the skybox;
    ///   4. a picture: each prefab is rendered on its own and its pixels are searched for magenta,
    ///      so a problem the name checks do not understand still shows up.
    ///
    /// Employees Take Revenge menu: "Audit Rendering (find pink)" only reports; "Fix Pink Materials" converts the broken
    /// materials in place (colour texture, tint, normal and emission maps, transparency are carried over).
    /// A report is written to Logs/RenderAudit.txt.
    /// </summary>
    public static class RenderAudit
    {
        [MenuItem("Employees Take Revenge/Audit Rendering (find pink)", priority = 40)]
        static void MenuAudit() => Run(fix: false, picture: true);

        [MenuItem("Employees Take Revenge/Fix Pink Materials (built-in pipeline)", priority = 41)]
        static void MenuFix() => Run(fix: true, picture: true);

        // batch-mode entry points
        public static void AuditBatch() => Run(false, true);
        public static void FixBatch() => Run(true, true);
        public static void QuickBatch() => Run(false, false);

        // ------------------------------------------------------------------ what counts as broken

        /// <summary>The shader is missing, the engine's error shader, or cannot run here (URP / HDRP / shader graph).</summary>
        public static bool IsBroken(Material m)
        {
            if (!m) return false;
            var sh = m.shader;
            if (!sh) return true;
            string n = sh.name;
            if (n == "Hidden/InternalErrorShader" || n.StartsWith("Hidden/InternalError")) return true;
            if (!sh.isSupported) return true;
            // shaders that exist in the project but belong to another pipeline never pass the support check above
            // on every platform, so also name the common ones
            return n.StartsWith("Universal Render Pipeline/") || n.StartsWith("HDRP/") || n.StartsWith("Shader Graphs/");
        }

        // ------------------------------------------------------------------ conversion

        static Texture FindTex(SerializedObject so, params string[] names)
        {
            var envs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            if (envs == null) return null;
            foreach (var wanted in names)
                for (int i = 0; i < envs.arraySize; i++)
                {
                    var e = envs.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("first").stringValue != wanted) continue;
                    var t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                    if (t) return t;
                }
            return null;
        }

        static Color? FindColor(SerializedObject so, params string[] names)
        {
            var cols = so.FindProperty("m_SavedProperties.m_Colors");
            if (cols == null) return null;
            foreach (var wanted in names)
                for (int i = 0; i < cols.arraySize; i++)
                {
                    var e = cols.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("first").stringValue == wanted) return e.FindPropertyRelative("second").colorValue;
                }
            return null;
        }

        static float? FindFloat(SerializedObject so, params string[] names)
        {
            var fl = so.FindProperty("m_SavedProperties.m_Floats");
            if (fl == null) return null;
            foreach (var wanted in names)
                for (int i = 0; i < fl.arraySize; i++)
                {
                    var e = fl.GetArrayElementAtIndex(i);
                    if (e.FindPropertyRelative("first").stringValue == wanted) return e.FindPropertyRelative("second").floatValue;
                }
            return null;
        }

        /// <summary>Switches a broken material to the Standard shader, keeping what it was showing.</summary>
        public static bool ConvertToStandard(Material m)
        {
            var std = Shader.Find("Standard");
            if (!m || !std) return false;
            var so = new SerializedObject(m);
            var tex = FindTex(so, "_BaseMap", "_MainTex", "_BaseColor", "_Albedo", "_ColorMap", "_BaseColorMap", "_Diffuse");
            var bump = FindTex(so, "_BumpMap", "_NormalMap", "_Normal", "_NormalTex");
            var metal = FindTex(so, "_MetallicGlossMap", "_MetallicMap", "_MetallicSmoothnessMap");
            var emis = FindTex(so, "_EmissionMap", "_Emission");
            var tint = FindColor(so, "_BaseColor", "_Color", "_Tint") ?? Color.white;
            var emisCol = FindColor(so, "_EmissionColor");
            float surface = FindFloat(so, "_Surface") ?? 0f;
            float clip = FindFloat(so, "_AlphaClip") ?? 0f;
            float cutoff = FindFloat(so, "_Cutoff") ?? 0.5f;
            float smooth = FindFloat(so, "_Smoothness", "_Glossiness") ?? 0.3f;
            float metallic = FindFloat(so, "_Metallic") ?? 0f;
            bool transparentName = m.name.ToLowerInvariant().Contains("transparent") || m.name.ToLowerInvariant().Contains("glass");

            m.shader = std;
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", tint);
            if (bump) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
            if (metal) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICGLOSSMAP"); }
            m.SetFloat("_Glossiness", Mathf.Clamp01(smooth));
            m.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (emis || (emisCol.HasValue && emisCol.Value.maxColorComponent > 0.01f))
            {
                if (emis) m.SetTexture("_EmissionMap", emis);
                m.SetColor("_EmissionColor", emis ? Color.white : emisCol.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            if (surface > 0.5f || transparentName)
            {
                // fade: a glass-like surface
                if (m.color.a > 0.95f && transparentName) { var c = m.color; c.a = 0.45f; m.color = c; }
                m.SetFloat("_Mode", 2f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = 3000;
            }
            else if (clip > 0.5f)
            {
                m.SetFloat("_Mode", 1f);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetFloat("_Cutoff", cutoff);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.EnableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 2450;
            }
            EditorUtility.SetDirty(m);
            return true;
        }

        // ------------------------------------------------------------------ the audit

        sealed class Report
        {
            public readonly StringBuilder text = new StringBuilder();
            public int materials, brokenMaterials, fixedMaterials, prefabs, prefabProblems, models, scenes, sceneProblems, rendered, magenta, skipped;
            public readonly List<string> unfixed = new List<string>();
            public void Line(string s) { text.AppendLine(s); Debug.Log("[Audit] " + s); }
        }

        static bool InProject(string path) => path.StartsWith("Assets/") && !path.Contains("/Editor/");

        static void Run(bool fix, bool picture)
        {
            var r = new Report();
            AssetDatabase.Refresh();

            // ---- 1. materials
            var matPaths = AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            var broken = new List<(string path, Material m)>();
            foreach (var p in matPaths)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (!m) continue;
                r.materials++;
                if (IsBroken(m)) { r.brokenMaterials++; broken.Add((p, m)); }
            }
            r.Line($"Materials: {r.materials} checked, {r.brokenMaterials} cannot be drawn by this pipeline.");
            foreach (var (p, m) in broken)
                r.Line($"  broken material: {p}  (shader '{(m.shader ? m.shader.name : "missing")}')");

            if (fix && broken.Count > 0)
            {
                foreach (var (p, m) in broken)
                {
                    if (ConvertToStandard(m)) { r.fixedMaterials++; r.Line($"  converted to Standard: {p}"); }
                    else r.unfixed.Add(p);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                int still = matPaths.Select(AssetDatabase.LoadAssetAtPath<Material>).Count(IsBroken);
                r.Line($"After the fix: {still} materials still broken.");
                r.brokenMaterials = still;
            }

            // ---- 2. prefabs and models: slots that would draw pink
            var assetPaths = AssetDatabase.FindAssets("t:Prefab t:Model", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().Where(InProject).ToList();
            var emptySlotAssets = new List<string>();
            foreach (var p in assetPaths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!go) continue;
                bool isModel = p.ToLowerInvariant().EndsWith(".fbx") || p.ToLowerInvariant().EndsWith(".obj");
                if (isModel) r.models++; else r.prefabs++;
                var problems = RendererProblems(go);
                if (problems.Any(x => x.EndsWith("is empty"))) emptySlotAssets.Add(p);
                if (problems.Count > 0)
                {
                    r.prefabProblems++;
                    r.Line($"  pink risk in {p}: {string.Join("; ", problems.Take(4))}{(problems.Count > 4 ? $" (+{problems.Count - 4} more)" : "")}");
                }
            }
            r.Line($"Prefabs: {r.prefabs} and models: {r.models} checked, {r.prefabProblems} would draw pink.");

            // ---- a model whose material was lost (an OBJ with no .mtl): let Unity give it its default material
            if (fix && emptySlotAssets.Count > 0)
            {
                foreach (var p in emptySlotAssets.Where(x => AssetImporter.GetAtPath(x) is ModelImporter).ToList())
                {
                    var imp = (ModelImporter)AssetImporter.GetAtPath(p);
                    if (imp.materialImportMode == ModelImporterMaterialImportMode.None) continue;
                    imp.materialImportMode = ModelImporterMaterialImportMode.None;
                    imp.SaveAndReimport();
                    r.fixedMaterials++;
                    r.Line($"  model with no material now uses the default one: {p}");
                }
                // prefabs that still have an empty slot get a plain grey material
                var grey = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/DefaultProp.mat");
                if (!grey)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
                    grey = new Material(Shader.Find("Standard")) { name = "DefaultProp", color = new Color(0.6f, 0.62f, 0.66f) };
                    AssetDatabase.CreateAsset(grey, "Assets/Materials/DefaultProp.mat");
                }
                foreach (var p in emptySlotAssets.Where(x => x.EndsWith(".prefab")))
                {
                    var contents = PrefabUtility.LoadPrefabContents(p);
                    bool changed = false;
                    foreach (var rend in contents.GetComponentsInChildren<Renderer>(true))
                    {
                        var ms = rend.sharedMaterials;
                        for (int i = 0; i < ms.Length; i++) if (ms[i] == null && !(rend is ParticleSystemRenderer)) { ms[i] = grey; changed = true; }
                        if (changed) rend.sharedMaterials = ms;
                    }
                    if (changed) { PrefabUtility.SaveAsPrefabAsset(contents, p); r.fixedMaterials++; r.Line($"  gave empty slots a plain material in prefab: {p}"); }
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                // look again
                int left = 0;
                foreach (var p in assetPaths)
                {
                    var go2 = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    if (go2 && RendererProblems(go2).Count > 0) left++;
                }
                r.prefabProblems = left;
                r.Line($"After the fix: {left} prefabs / models still have a slot that would draw pink.");
            }

            // ---- 3. scenes
            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).ToList();
            string startScene = SceneManagerActivePath();
            foreach (var sp in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                r.scenes++;
                var problems = new List<string>();
                foreach (var root in scene.GetRootGameObjects()) problems.AddRange(RendererProblems(root));
                var sky = RenderSettings.skybox;
                if (sky && IsBroken(sky)) problems.Add("skybox material is broken");
                if (problems.Count > 0)
                {
                    r.sceneProblems++;
                    r.Line($"  pink risk in scene {sp}: {string.Join("; ", problems.Take(5))}{(problems.Count > 5 ? $" (+{problems.Count - 5} more)" : "")}");
                }
            }
            r.Line($"Scenes: {r.scenes} checked, {r.sceneProblems} with something that would draw pink.");

            // ---- 4. pictures
            if (picture) RenderCheck(assetPaths.Where(p => !p.ToLowerInvariant().EndsWith(".fbx") || true).ToList(), r);

            if (!string.IsNullOrEmpty(startScene) && File.Exists(startScene)) EditorSceneManager.OpenScene(startScene, OpenSceneMode.Single);

            r.Line($"SUMMARY: broken materials {r.brokenMaterials}, prefab/model slots {r.prefabProblems}, scenes {r.sceneProblems}, " +
                   $"rendered {r.rendered} (skipped {r.skipped}), magenta pictures {r.magenta}, converted {r.fixedMaterials}.");
            try
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/RenderAudit.txt", r.text.ToString());
            }
            catch { /* the Console has it anyway */ }
        }

        static string SceneManagerActivePath()
        {
            var s = EditorSceneManager.GetActiveScene();
            return s.IsValid() ? s.path : null;
        }

        static List<string> RendererProblems(GameObject root)
        {
            var list = new List<string>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!r) continue;
                // a particle system's trail module is optional; everything else needs a material in every slot
                var mats = r.sharedMaterials;
                if (mats.Length == 0 && !(r is ParticleSystemRenderer)) { list.Add($"{PathOf(r)} has no material slot"); continue; }
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        // a mesh with more slots than materials, or an empty slot: both draw as the error shader
                        if (r is ParticleSystemRenderer psr && psr.renderMode == ParticleSystemRenderMode.None) continue;
                        list.Add($"{PathOf(r)} slot {i} is empty");
                    }
                    else if (IsBroken(mats[i])) list.Add($"{PathOf(r)} slot {i} uses broken material '{mats[i].name}'");
                }
            }
            return list;
        }

        static string PathOf(Component c)
        {
            var sb = new StringBuilder(c.name);
            for (var t = c.transform.parent; t != null && sb.Length < 80; t = t.parent) sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the picture check

        static void RenderCheck(List<string> assetPaths, Report r)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("AuditLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var camGo = new GameObject("AuditCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f);
            cam.fieldOfView = 40f;
            cam.enabled = false;
            const int size = 192;
            var rt = new RenderTexture(size, size, 24);
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var bad = new List<string>();

            foreach (var p in assetPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!prefab) continue;
                // things that are not a visible object on their own
                if (prefab.GetComponentInChildren<Canvas>(true) || prefab.GetComponentInChildren<Terrain>(true)) { r.skipped++; continue; }
                GameObject inst = null;
                try
                {
                    inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var rs = inst.GetComponentsInChildren<Renderer>(false).Where(x => x && x.enabled && !(x is ParticleSystemRenderer)).ToArray();
                    if (rs.Length == 0) { r.skipped++; continue; }
                    var b = rs[0].bounds;
                    foreach (var x in rs) b.Encapsulate(x.bounds);
                    float radius = Mathf.Max(0.05f, b.extents.magnitude);
                    var dir = new Vector3(0.6f, 0.4f, -1f).normalized;
                    cam.transform.position = b.center + dir * (radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f);
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.01f, radius * 0.05f);
                    cam.farClipPlane = radius * 8f + 10f;
                    cam.targetTexture = rt;
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    cam.targetTexture = null;
                    r.rendered++;
                    int pink = CountMagenta(tex.GetPixels32());
                    if (pink >= 25)
                    {
                        r.magenta++;
                        bad.Add(p);
                        r.Line($"  MAGENTA ({pink} px) when rendered: {p}");
                    }
                }
                catch (System.Exception e) { r.skipped++; Debug.LogWarning("[Audit] could not render " + p + ": " + e.Message); }
                finally { if (inst) Object.DestroyImmediate(inst); }
            }
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            r.Line($"Pictures: {r.rendered} objects rendered, {r.magenta} show magenta.");
        }

        /// <summary>Counts pixels close to pure magenta (the error shader's colour).</summary>
        public static int CountMagenta(Color32[] px)
        {
            int n = 0;
            foreach (var c in px) if (c.r > 225 && c.b > 225 && c.g < 45) n++;
            return n;
        }

        // ------------------------------------------------------------------ a contact sheet (to look at converted materials)

        /// <summary>Renders a fixed set of props side by side into a PNG in the temp folder (batch mode).</summary>
        public static void SheetBatch()
        {
            var paths = new[]
            {
                "Assets/GunsAssetPack/guns/Prefabs/ak47.prefab", "Assets/GunsAssetPack/guns/Prefabs/mac10.prefab", "Assets/GunsAssetPack/guns/Prefabs/pew.prefab",
                "Assets/GunsAssetPack/guns/Prefabs/shotgun.prefab", "Assets/GunsAssetPack/guns/Prefabs/rocketlaucher.prefab", "Assets/GunsAssetPack/guns/Prefabs/nade_low.prefab",
                "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/Display_Counter.prefab", "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/Coffee_Machine.prefab",
                "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/Chair.prefab", "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/Donut_1.prefab",
                "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/Cake_2.prefab", "Assets/Stylized Low Poly Coffee Shop Props Pack \u2013 Free/Prefabs/Menu_Board.prefab",
                "Assets/FREE/Pack_FREE_PartyCharacters/Resources/Prefabs/character_default.prefab", "Assets/FREE/Pack_FREE_PartyCharacters/Prefabs/Character_4c4e64.prefab",
                "Assets/FREE/Pack_FREE_PartyCharacters/Resources/Prefabs/Hats/party hat.prefab", "Assets/FREE/Pack_FREE_PartyCharacters/Resources/Prefabs/Hats/chef hat.prefab",
                "Assets/ToonyTinyPeople/TT_demo/prefabs/TT_demo_police.prefab", "Assets/ToonyTinyPeople/TT_demo/prefabs/TT_demo_female.prefab",
            };
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lightGo = new GameObject("AuditLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
            var cam = new GameObject("AuditCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.30f, 0.32f, 0.36f); cam.fieldOfView = 40f; cam.enabled = false;
            const int tile = 256, cols = 6;
            int rows = (paths.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * tile, rows * tile, TextureFormat.RGB24, false);
            var rt = new RenderTexture(tile, tile, 24);
            var one = new Texture2D(tile, tile, TextureFormat.RGB24, false);
            for (int i = 0; i < paths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (!prefab) { Debug.Log("[Audit] sheet: missing " + paths[i]); continue; }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 160f, 0f));
                var rs = inst.GetComponentsInChildren<Renderer>(false).Where(x => x && x.enabled).ToArray();
                if (rs.Length > 0)
                {
                    var b = rs[0].bounds; foreach (var x in rs) b.Encapsulate(x.bounds);
                    float radius = Mathf.Max(0.05f, b.extents.magnitude);
                    cam.transform.position = b.center + new Vector3(0.6f, 0.4f, -1f).normalized * (radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f);
                    cam.transform.LookAt(b.center);
                    cam.nearClipPlane = Mathf.Max(0.01f, radius * 0.05f); cam.farClipPlane = radius * 8f + 10f;
                    cam.targetTexture = rt; cam.Render();
                    RenderTexture.active = rt; one.ReadPixels(new Rect(0, 0, tile, tile), 0, 0); one.Apply(); RenderTexture.active = null; cam.targetTexture = null;
                    int cx = (i % cols) * tile, cy = (rows - 1 - i / cols) * tile;
                    sheet.SetPixels(cx, cy, tile, tile, one.GetPixels());
                }
                Object.DestroyImmediate(inst);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "be_audit_sheet.png"), sheet.EncodeToPNG());
            Debug.Log("[Audit] sheet written");
        }
    }
}
