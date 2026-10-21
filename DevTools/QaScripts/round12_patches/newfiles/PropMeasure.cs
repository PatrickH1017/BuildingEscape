using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>Logs the size of a few prefabs (their renderers' bounds at the origin): the numbers PropTable wants. Run: Unity -batchmode -nographics -projectPath C:\bqm -executeMethod BuildingEscape.EditorTools.PropMeasure.Log</summary>
    public static class PropMeasure
    {
        static readonly string[] Paths =
        {
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Container_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Container_Small_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Crate_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Crate_Large_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Crate_Small_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Pallet_Loaded_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Pallet_Loaded_02.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_PalletStack_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Barrel_01.prefab",
            "Assets/Synty/PolygonBattleRoyale/Prefabs/Props/SM_Prop_Barrel_02.prefab",
        };

        public static void Log()
        {
            foreach (var p in Paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (!prefab) { Debug.Log("[PropMeasure] missing " + p); continue; }
                var go = Object.Instantiate(prefab);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) { Debug.Log("[PropMeasure] no renderer " + p); Object.DestroyImmediate(go); continue; }
                var b = rs[0].bounds; for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                Debug.Log($"[PropMeasure] {prefab.name}: size x {b.size.x:0.00} y {b.size.y:0.00} z {b.size.z:0.00}, centre {b.center.x:0.00} {b.center.y:0.00} {b.center.z:0.00}, min y {b.min.y:0.00}, {rs.Length} renderer(s)");
                Object.DestroyImmediate(go);
            }
            EditorApplication.Exit(0);
        }
    }
}
