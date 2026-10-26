using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// What Unity made of the SpawnCampGames walkie talkie (T5, the owner's list of 2026-10-23): the model is a Blender file, which the Editor can only import when it finds Blender. Prints how the .blend imported (the importer, the assets inside it)
    /// and what the prefab holds (renderers and mesh filters), then exits: <c>run_method_bd.ps1 -Method BuildingEscape.EditorTools.NetDevWalkieProbe.Run -Log probe -Pattern WalkieProbe</c>.
    /// </summary>
    public static class NetDevWalkieProbe
    {
        public static void Run()
        {
            const string blend = "Assets/SpawnCampGames/WalkieTalkie/Models/WalkieTalkie.blend", prefab = "Assets/SpawnCampGames/WalkieTalkie/Prefabs/Walkie.prefab";
            try
            {
                var imp = AssetImporter.GetAtPath(blend);
                Debug.Log($"[WalkieProbe] importer of the .blend: {(imp ? imp.GetType().Name : "none")}");
                var subs = AssetDatabase.LoadAllAssetsAtPath(blend);
                Debug.Log($"[WalkieProbe] assets inside the .blend: {subs.Length}");
                foreach (var s in subs) if (s) Debug.Log($"[WalkieProbe]   {s.GetType().Name} '{s.name}'");
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                Debug.Log($"[WalkieProbe] prefab loaded {go != null}; renderers {(go ? go.GetComponentsInChildren<Renderer>(true).Length : 0)}; mesh filters {(go ? go.GetComponentsInChildren<MeshFilter>(true).Length : 0)}");
                if (go)
                {
                    var probe = Object.Instantiate(go);
                    var rs = probe.GetComponentsInChildren<Renderer>(true);
                    Bounds b = default; bool any = false;
                    foreach (var r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                    Debug.Log($"[WalkieProbe] instance: renderers {rs.Length}, bounds {(any ? b.size.ToString("F3") : "none")}");
                    Object.DestroyImmediate(probe);
                }
            }
            finally { EditorApplication.Exit(0); }
        }
    }
}
