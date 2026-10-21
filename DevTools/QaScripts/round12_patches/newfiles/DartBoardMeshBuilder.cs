using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BuildingEscape.EditorTools
{
    /// <summary>
    /// The dart board's clean mesh (the owner, 2026-10-19 night: "take the picture off the center of the dart board" and "the dart that is already on it"). The Synty board (SM_Prop_Darkboard_01) has a portrait pinned over its
    /// middle and a dart stuck in the portrait, all in ONE mesh. <see cref="Analyse"/> logs the separate pieces of that mesh (pieces = triangles that share corners); <see cref="BuildBatch"/> leaves the pieces out that are not the
    /// board itself and saves the rest as <see cref="AssetPath"/>, which <c>FloorGenerator.BuildDartBoard</c> puts on the board's MeshFilter. The pack's own files are not touched.
    /// Run: Unity -batchmode -nographics -projectPath C:\bqm -executeMethod BuildingEscape.EditorTools.DartBoardMeshBuilder.Analyse (or BuildBatch).
    /// </summary>
    public static class DartBoardMeshBuilder
    {
        const string PrefabPath = "Assets/Synty/PolygonOffice/Prefabs/Props/Wall Props/SM_Prop_Darkboard_01.prefab";
        public const string AssetPath = "Assets/Resources/DartBoardClean.asset";

        sealed class Piece
        {
            public int id;
            public readonly List<int> triangles = new List<int>();      // first corner index of each triangle in the source index list
            public Bounds bounds;
            public bool any;
        }

        static Mesh SourceMesh()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var mf = prefab ? prefab.GetComponentInChildren<MeshFilter>() : null;
            return mf ? mf.sharedMesh : null;
        }

        static int Find(int[] parent, int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }

        /// <summary>The separate pieces of the mesh: corners at the same place (to a tenth of a millimetre) are the same corner, triangles that share a corner are one piece.</summary>
        static List<Piece> Pieces(Mesh m, out int[] tris, out Vector3[] verts)
        {
            verts = m.vertices;
            tris = m.triangles;
            var cell = new Dictionary<Vector3Int, int>();
            var klass = new int[verts.Length];
            int classes = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                var q = new Vector3Int(Mathf.RoundToInt(verts[i].x * 10000f), Mathf.RoundToInt(verts[i].y * 10000f), Mathf.RoundToInt(verts[i].z * 10000f));
                if (!cell.TryGetValue(q, out int c)) { c = classes++; cell[q] = c; }
                klass[i] = c;
            }
            var parent = Enumerable.Range(0, classes).ToArray();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = Find(parent, klass[tris[t]]), b = Find(parent, klass[tris[t + 1]]), c = Find(parent, klass[tris[t + 2]]);
                parent[b] = a; parent[Find(parent, c)] = a;
            }
            var byRoot = new Dictionary<int, Piece>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int root = Find(parent, klass[tris[t]]);
                if (!byRoot.TryGetValue(root, out var p)) byRoot[root] = p = new Piece { id = byRoot.Count };
                p.triangles.Add(t);
                for (int k = 0; k < 3; k++)
                {
                    var v = verts[tris[t + k]];
                    if (!p.any) { p.bounds = new Bounds(v, Vector3.zero); p.any = true; } else p.bounds.Encapsulate(v);
                }
            }
            return byRoot.Values.OrderByDescending(p => p.bounds.size.x * p.bounds.size.y).ToList();
        }

        /// <summary>Makes <see cref="AssetPath"/>: the mesh of the biggest piece only (the board itself, a flat disc under 5 cm deep); the portrait (one piece) and the dart (three pieces: the shaft with the tip, two fins) are left out.</summary>
        public static void BuildBatch()
        {
            var m = SourceMesh();
            if (!m) { Debug.Log("[DartBoardMesh] no mesh found at " + PrefabPath); EditorApplication.Exit(1); return; }
            var pieces = Pieces(m, out var tris, out var verts);
            var board = pieces[0];
            if (pieces.Count < 2 || board.bounds.size.z > 0.05f || board.bounds.size.x < 0.6f)
            { Debug.Log("[DartBoardMesh] FAIL the biggest piece is not the flat board (" + board.bounds.size + ", " + pieces.Count + " pieces)"); EditorApplication.Exit(1); return; }
            var normals = m.normals; var uv = m.uv; var uv2 = m.uv2; var colors = m.colors;
            var remap = new Dictionary<int, int>();
            var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>(); var nuv2 = new List<Vector2>(); var nc = new List<Color>(); var nt = new List<int>();
            foreach (int t in board.triangles)
                for (int k = 0; k < 3; k++)
                {
                    int i = tris[t + k];
                    if (!remap.TryGetValue(i, out int j))
                    {
                        j = nv.Count; remap[i] = j; nv.Add(verts[i]);
                        if (normals.Length == verts.Length) nn.Add(normals[i]);
                        if (uv.Length == verts.Length) nuv.Add(uv[i]);
                        if (uv2.Length == verts.Length) nuv2.Add(uv2[i]);
                        if (colors.Length == verts.Length) nc.Add(colors[i]);
                    }
                    nt.Add(j);
                }
            var clean = new Mesh { name = "DartBoardClean" };
            clean.SetVertices(nv);
            if (nn.Count == nv.Count) clean.SetNormals(nn);
            if (nuv.Count == nv.Count) clean.SetUVs(0, nuv);
            if (nuv2.Count == nv.Count) clean.SetUVs(1, nuv2);
            if (nc.Count == nv.Count) clean.SetColors(nc);
            clean.SetTriangles(nt, 0);
            clean.RecalculateBounds();
            if (nn.Count != nv.Count) clean.RecalculateNormals();
            System.IO.Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.DeleteAsset(AssetPath);
            AssetDatabase.CreateAsset(clean, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DartBoardMesh] saved {AssetPath}: {nv.Count} vertices, {nt.Count / 3} triangles, bounds {clean.bounds.size} (from {verts.Length} vertices, {tris.Length / 3} triangles in {pieces.Count} pieces)");
            EditorApplication.Exit(0);
        }

        /// <summary>Logs the pieces: how many triangles, where they are, how big.</summary>
        public static void Analyse()
        {
            var m = SourceMesh();
            if (!m) { Debug.Log("[DartBoardMesh] no mesh found at " + PrefabPath); EditorApplication.Exit(1); return; }
            var pieces = Pieces(m, out var tris, out var verts);
            Debug.Log($"[DartBoardMesh] mesh {m.name}: {verts.Length} vertices, {tris.Length / 3} triangles, {m.subMeshCount} submesh(es), bounds {m.bounds.center} size {m.bounds.size}, {pieces.Count} piece(s)");
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                Debug.Log($"[DartBoardMesh] piece {i}: {p.triangles.Count} triangles, centre {p.bounds.center}, size {p.bounds.size}, z {p.bounds.min.z:0.000} .. {p.bounds.max.z:0.000}");
            }
            EditorApplication.Exit(0);
        }
    }
}
