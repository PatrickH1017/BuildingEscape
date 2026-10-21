using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Creates Assets/Resources/CityCatalog.asset: the white plane (ComPlaneV1), the cars and the buildings of the packs the owner added on 2026-10-19 (Alstra Infinite planes, Awbmecreations cars,
    /// SimplePoly City). The scenery asks the catalog for them, so the prefabs ship with the game even though nothing else references them. Runs by itself whenever the catalog is missing
    /// something that is in the project.
    /// </summary>
    [InitializeOnLoad]
    public static class CityCatalogBuilder
    {
        const string AssetPath = "Assets/Resources/CityCatalog.asset";
        const string PlanePath = "Assets/Alstra Infinite/Planes LowPoly/Prefabs/ComPlaneV1.prefab";
        const string CarsFolder = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs";
        const string BuildingsFolder = "Assets/SimplePoly City - Low Poly Assets/Prefab/Buildings";

        static CityCatalogBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                var cat = AssetDatabase.LoadAssetAtPath<CityCatalog>(AssetPath);
                if (!cat && System.IO.File.Exists(AssetPath)) return;      // (a catalog that exists but does not load: its script is not imported yet - rebuilding now would overwrite it with a blank one)
                if (NeedsBuild(cat)) Build(false);
            };
        }

        [MenuItem("Employees Take Revenge/Setup City Pack", priority = 31)]
        static void Menu() => Build(true);

        /// <summary>Batch entry point: <c>-executeMethod BuildingEscape.EditorTools.CityCatalogBuilder.BuildBatch</c> (the catalog is rebuilt, the Editor quits).</summary>
        public static void BuildBatch() { AssetDatabase.Refresh(); Build(true); EditorApplication.Exit(0); }

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        /// <summary>The prefabs of a folder (and its folders), in the order of their paths; the paths holding <paramref name="without"/> are left out.</summary>
        static GameObject[] Prefabs(string folder, string without = null)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new GameObject[0];
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => without == null || !p.Contains(without)).OrderBy(p => p, System.StringComparer.Ordinal).Select(Load).Where(g => g != null).ToArray();
        }

        static bool Same(GameObject[] a, GameObject[] b) => a != null && a.Length == b.Length && a.SequenceEqual(b);

        static bool NeedsBuild(CityCatalog cat)
        {
            if (!cat) return true;
            return (!cat.plane && Load(PlanePath) != null)
                || ((cat.cars == null || cat.cars.Length == 0) && Prefabs(CarsFolder, "Air Plane").Length > 0)
                || ((cat.buildings == null || cat.buildings.Length == 0) && Prefabs(BuildingsFolder).Length > 0);
        }

        static void Build(bool log)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var cat = AssetDatabase.LoadAssetAtPath<CityCatalog>(AssetPath);
            bool created = false;
            if (!cat) { cat = ScriptableObject.CreateInstance<CityCatalog>(); AssetDatabase.CreateAsset(cat, AssetPath); created = true; }

            var plane = Load(PlanePath);
            var cars = Prefabs(CarsFolder, "Air Plane");            // (the nine cars; the pack's Air Plane_1 is not a car)
            var buildings = Prefabs(BuildingsFolder);
            bool changed = created || cat.plane != plane || !Same(cat.cars, cars) || !Same(cat.buildings, buildings);
            if (changed)
            {
                cat.plane = plane; cat.cars = cars; cat.buildings = buildings;
                EditorUtility.SetDirty(cat);
                AssetDatabase.SaveAssets();
            }
            if (log) Debug.Log($"City catalog ready: plane {(plane ? plane.name : "MISSING")}, {cars.Length} cars, {buildings.Length} buildings" + (changed ? "." : " (nothing changed)."));
        }
    }
}
