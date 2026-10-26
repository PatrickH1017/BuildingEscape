using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// Makes <c>Assets/Resources/AlarmBeacon.prefab</c> (the owner, 2026-10-24: the alarm beacons use the small roof lamp of the PBR Lamps Pack he added): a prefab of the pack's <c>Small_roof_lamp</c> prefab, in Resources so that the built game carries it (and the pack's mesh and material
    /// with it; the pack's own prefab is not touched). Run once: <c>Employees Take Revenge &gt; Make the alarm beacon prefab</c>, or <c>-executeMethod BuildingEscape.EditorTools.BeaconPrefabBuilder.Build</c>. Without it (or without the pack) the beacons are plain red domes.
    /// </summary>
    public static class BeaconPrefabBuilder
    {
        const string Pack = "Assets/New Solution Studio/PBR Lamps Pack/Prefabs/Small_roof_lamp.prefab";
        const string Out = "Assets/Resources/AlarmBeacon.prefab";

        [MenuItem("Employees Take Revenge/Make the alarm beacon prefab")]
        public static void Build()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Pack);
            if (!src) { Debug.LogError("[Beacon] The pack's lamp is not at " + Pack); return; }
            var root = new GameObject("AlarmBeacon");
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
            lamp.name = "Lamp";
            lamp.transform.localPosition = Vector3.zero; lamp.transform.localRotation = Quaternion.identity; lamp.transform.localScale = Vector3.one;
            var saved = PrefabUtility.SaveAsPrefabAsset(root, Out);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Beacon] " + (saved ? "saved " + Out : "COULD NOT SAVE " + Out));
        }
    }
}
