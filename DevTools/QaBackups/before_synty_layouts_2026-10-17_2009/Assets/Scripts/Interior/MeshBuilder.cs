using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// Collects boxes and quads (with normals and UVs) into ONE mesh, so a floor's tiled floors, window walls and door frames cost a draw call each however many pieces they are made of.
    /// A box's UVs run 0 to 1 over each face; <see cref="Quad"/> takes explicit UVs (the floors use metres, so a texture repeats every so many metres).
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> tri = new List<int>();

        public int VertexCount => v.Count;

        /// <summary>One quad, corners a, b, c, d in order round it (front side where the normal points); uv runs (u0, v0) at a to (u1, v1) at c.</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float u0 = 0f, float v0 = 0f, float u1 = 1f, float v1 = 1f)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(new Vector2(u0, v0)); uv.Add(new Vector2(u1, v0)); uv.Add(new Vector2(u1, v1)); uv.Add(new Vector2(u0, v1));
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>One quad with a UV for each of its corners.</summary>
        public void QuadUV(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(normal); n.Add(normal); n.Add(normal); n.Add(normal);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            tri.Add(i); tri.Add(i + 1); tri.Add(i + 2); tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
        }

        /// <summary>A box between two corners (all six faces; Unity's front faces are the clockwise ones seen from outside).</summary>
        public void Box(Vector3 min, Vector3 max)
        {
            Quad(new Vector3(min.x, min.y, min.z), new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, min.y, min.z), Vector3.back);     // -z
            Quad(new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, min.y, max.z), Vector3.forward);  // +z
            Quad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z), new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z), Vector3.left);     // -x
            Quad(new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z), Vector3.right);    // +x
            Quad(new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(max.x, max.y, min.z), Vector3.up);       // +y
            Quad(new Vector3(min.x, min.y, max.z), new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z), Vector3.down);     // -y
        }

        /// <summary>The mesh (null when nothing was added). More than 65535 vertices switch to 32-bit indices.</summary>
        public Mesh ToMesh(string name)
        {
            if (v.Count == 0) return null;
            var m = new Mesh { name = name };
            if (v.Count > 60000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
