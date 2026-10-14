using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Creates Assets/Resources/OfficeCatalog.asset from the Office Essentials pack prefabs.</summary>
    [InitializeOnLoad]
    public static class OfficeCatalogBuilder
    {
        const string Root = "Assets/nappin/OfficeEssentialsPack/Prefabs/";
        const string AssetPath = "Assets/Resources/OfficeCatalog.asset";

        static OfficeCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (!AssetDatabase.LoadAssetAtPath<OfficeCatalog>(AssetPath)) Build(false);
            };
        }

        [MenuItem("Overtime Escape/Setup Office Pack", priority = 30)]
        static void Menu() => Build(true);

        static GameObject P(string n) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "(Prb)" + n + ".prefab");

        static void Build(bool log)
        {
            var desk1 = P("Desk1");
            if (!desk1) { if (log) Debug.LogWarning("Office pack prefabs not found under " + Root); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<OfficeCatalog>(AssetPath);
            if (!cat) { cat = ScriptableObject.CreateInstance<OfficeCatalog>(); AssetDatabase.CreateAsset(cat, AssetPath); }
            cat.desk1 = desk1; cat.desk2 = P("Desk2"); cat.pc = P("PC"); cat.printer = P("Printer");
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log("Office catalog ready: " + AssetPath);
        }
    }
}
