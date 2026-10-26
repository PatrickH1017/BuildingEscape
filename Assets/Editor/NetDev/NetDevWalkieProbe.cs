using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// What Unity made of the SpawnCampGames walkie talkie (T5, the owner's list of 2026-10-23): the model is a Blender file, which the Editor can only import when it finds Blender. <c>Run</c> prints how the .blend imported (the importer, the assets
    /// inside it) and what the prefab holds (renderers and mesh filters), then exits: <c>run_method_bd.ps1 -Method BuildingEscape.EditorTools.NetDevWalkieProbe.Run -Log probe -Pattern WalkieProbe</c>. <c>Reimport</c> forces the import again first and
    /// prints the same before and after (a Library that remembers a failed import from a time when Blender was not there keeps it until the file is imported again).
    /// </summary>
    public static class NetDevWalkieProbe
    {
        const string Blend = "Assets/SpawnCampGames/WalkieTalkie/Models/WalkieTalkie.blend", Prefab = "Assets/SpawnCampGames/WalkieTalkie/Prefabs/Walkie.prefab";

        static void Print(string when)
        {
            var imp = AssetImporter.GetAtPath(Blend);
            Debug.Log($"[WalkieProbe] {when}: importer of the .blend: {(imp ? imp.GetType().Name : "none")}");
            var subs = AssetDatabase.LoadAllAssetsAtPath(Blend);
            Debug.Log($"[WalkieProbe] {when}: assets inside the .blend: {subs.Length}");
            foreach (var s in subs) if (s) Debug.Log($"[WalkieProbe] {when}:   {s.GetType().Name} '{s.name}'");
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            Debug.Log($"[WalkieProbe] {when}: prefab loaded {go != null}; renderers {(go ? go.GetComponentsInChildren<Renderer>(true).Length : 0)}; mesh filters {(go ? go.GetComponentsInChildren<MeshFilter>(true).Length : 0)}");
            if (!go) return;
            var probe = Object.Instantiate(go);
            var rs = probe.GetComponentsInChildren<Renderer>(true);
            Bounds b = default; bool any = false;
            foreach (var r in rs) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            Debug.Log($"[WalkieProbe] {when}: instance: renderers {rs.Length}, bounds {(any ? b.size.ToString("F3") : "none")}");
            Object.DestroyImmediate(probe);
        }

        public static void Run()
        {
            try { Print("now"); }
            finally { EditorApplication.Exit(0); }
        }

        public static void Reimport()
        {
            try
            {
                Print("before");
                AssetDatabase.ImportAsset(Blend, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(Prefab, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Print("after");
            }
            finally { EditorApplication.Exit(0); }
        }
    }
}
