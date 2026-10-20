using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Employees Take Revenge > Setup Floor Surfaces: collects the carpets the owner added (every material or texture in the project with "carpet" in its name) into
    /// Assets/Resources/FloorSurfaceCatalog.asset, which the floor builder reads. Run it again after adding or removing carpets. It only reads the project's art: a material is
    /// read for its picture (Standard's _MainTex or URP's _BaseMap) and its colour, never used itself (see <see cref="FloorSurfaceCatalog"/>).
    /// </summary>
    public static class FloorSurfaceCatalogBuilder
    {
        const string AssetPath = "Assets/Resources/FloorSurfaceCatalog.asset";

        [MenuItem("Employees Take Revenge/Setup Floor Surfaces", priority = 60)]
        public static void Build() => Run();

        /// <summary>Batch entry point: <c>-executeMethod BuildingEscape.EditorTools.FloorSurfaceCatalogBuilder.BuildBatch</c>.</summary>
        public static void BuildBatch()
        {
            int n = Run();
            EditorApplication.Exit(n >= 0 ? 0 : 1);
        }

        /// <summary>Returns how many carpets the catalog holds.</summary>
        public static int Run()
        {
            var found = new List<FloorSurfaceCatalog.Carpet>();
            var usedTextures = new HashSet<Texture2D>();
            foreach (var guid in AssetDatabase.FindAssets("carpet t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") || !Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains("carpet")) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) continue;
                Texture2D tex = null;
                foreach (var prop in new[] { "_MainTex", "_BaseMap", "_BaseColorMap", "_AlbedoMap" }) if (!tex && m.HasProperty(prop)) tex = m.GetTexture(prop) as Texture2D;
                Color tint = m.HasProperty("_Color") ? m.GetColor("_Color") : m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                if (tex) usedTextures.Add(tex);
                found.Add(new FloorSurfaceCatalog.Carpet { label = m.name, albedo = tex, tint = tint, tileMeters = 2f });
            }
            foreach (var guid in AssetDatabase.FindAssets("carpet t:Texture2D"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/") || !Path.GetFileNameWithoutExtension(path).ToLowerInvariant().Contains("carpet")) continue;
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (!t || usedTextures.Contains(t) || t.name.ToLowerInvariant().Contains("normal")) continue;
                found.Add(new FloorSurfaceCatalog.Carpet { label = t.name, albedo = t, tint = Color.white, tileMeters = 2f });
            }
            found.RemoveAll(c => !c.albedo);
            found.Sort((a, b) => string.CompareOrdinal(a.label, b.label));

            Directory.CreateDirectory("Assets/Resources");
            var cat = AssetDatabase.LoadAssetAtPath<FloorSurfaceCatalog>(AssetPath);
            if (!cat) { cat = ScriptableObject.CreateInstance<FloorSurfaceCatalog>(); AssetDatabase.CreateAsset(cat, AssetPath); }
            // keep the tile size somebody set by hand for a carpet of the same name
            var old = new Dictionary<string, float>();
            foreach (var c in cat.carpets) if (c != null && !string.IsNullOrEmpty(c.label)) old[c.label] = c.tileMeters;
            foreach (var c in found) if (old.TryGetValue(c.label, out float tile) && tile > 0.05f) c.tileMeters = tile;
            cat.carpets = found.ToArray();
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FloorSurfaces] {found.Count} carpet(s) in the catalog: {string.Join(", ", found.ConvertAll(c => c.label))}");
            return found.Count;
        }
    }
}
