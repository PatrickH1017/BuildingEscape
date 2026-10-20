using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Creates Assets/Resources/OfficeCatalog.asset: one prefab for every prop in PropTable (the VNB Low-Poly Office
    /// Set, the Office Essentials pack, the Pandazole kitchen props and the coffee shop pack). The room generator asks
    /// the catalog for a prop by key, so the prefabs ship with the game even though nothing else references them.
    /// Runs by itself whenever the catalog is missing props that are in the project.
    /// </summary>
    [InitializeOnLoad]
    public static class OfficeCatalogBuilder
    {
        const string Nap = "Assets/nappin/OfficeEssentialsPack/Prefabs/(Prb)";
        const string AssetPath = "Assets/Resources/OfficeCatalog.asset";

        static OfficeCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                var cat = AssetDatabase.LoadAssetAtPath<OfficeCatalog>(AssetPath);
                if (NeedsBuild(cat)) Build(false);
            };
        }

        [MenuItem("Employees Take Revenge/Setup Office Pack", priority = 30)]
        static void Menu() => Build(true);

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");

        /// <summary>True if the catalog does not hold a prefab that exists in the project.</summary>
        static bool NeedsBuild(OfficeCatalog cat)
        {
            if (!cat || !cat.HasProps) return true;
            foreach (var d in PropTable.All)
                if (d.path != null && !cat.Get(d.key) && Load(d.path)) return true;
            return false;
        }

        static void Build(bool log)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<OfficeCatalog>(AssetPath);
            bool created = false;
            if (!cat) { cat = ScriptableObject.CreateInstance<OfficeCatalog>(); AssetDatabase.CreateAsset(cat, AssetPath); created = true; }

            var props = new List<PropEntry>();
            var missing = new List<string>();
            foreach (var d in PropTable.All)
            {
                if (d.path == null) continue;
                var g = Load(d.path);
                if (g) props.Add(new PropEntry { key = d.key, prefab = g });
                else missing.Add(d.key + "  (" + d.path + ")");
            }

            bool changed = created || cat.props == null || cat.props.Count != props.Count;
            if (!changed)
                for (int i = 0; i < props.Count; i++)
                    if (cat.props[i].key != props[i].key || cat.props[i].prefab != props[i].prefab) { changed = true; break; }

            var desk1 = AssetDatabase.LoadAssetAtPath<GameObject>(Nap + "Desk1.prefab");
            var desk2 = AssetDatabase.LoadAssetAtPath<GameObject>(Nap + "Desk2.prefab");
            var pc = AssetDatabase.LoadAssetAtPath<GameObject>(Nap + "PC.prefab");
            var printer = AssetDatabase.LoadAssetAtPath<GameObject>(Nap + "Printer.prefab");
            if (cat.desk1 != desk1 || cat.desk2 != desk2 || cat.pc != pc || cat.printer != printer) changed = true;

            if (changed)
            {
                cat.props = props;
                cat.desk1 = desk1; cat.desk2 = desk2; cat.pc = pc; cat.printer = printer;
                EditorUtility.SetDirty(cat);
                AssetDatabase.SaveAssets();
            }

            if (log)
            {
                Debug.Log($"Office catalog ready: {props.Count} props" + (changed ? "." : " (nothing changed)."));
                if (missing.Count > 0)
                    Debug.LogWarning("These props are not in the project, so the generator will use plain boxes for them:\n" + string.Join("\n", missing));
            }
        }
    }
}
