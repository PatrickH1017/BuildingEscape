using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The room numbers over the doors (101, 102 ... on floor 1; 201 ... on floor 2). The whole floor's plates are ONE mesh with two
    /// materials (the dark plate and the light lettering), so they cost two draw calls however many rooms there are. The numbers
    /// themselves come from <see cref="RoomNumbers"/>.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Room numbers")]
        [Tooltip("A number plate over the door of every room: 101, 102... on floor 1, 201... on floor 2.")]
        public bool roomNumbers = true;

        int layoutFloor;                      // the floor the current layout was planned for

        const float DotSize = 0.06f;          // one dot of the lettering, in metres (a digit is 3 x 5 dots: 18 x 30 cm)
        const float DigitPitch = DotSize * (RoomNumbers.GlyphColumns + 1);
        const float PlateMargin = 0.12f, PlateGap = 0.012f, LetterGap = 0.012f;   // plate and letters stand off the wall by a hair

        static readonly Color PlateColour = new Color(0.10f, 0.13f, 0.18f), LetterColour = new Color(0.95f, 0.93f, 0.84f);

        /// <summary>Hangs a number plate over every numbered room's doorway (on the corridor side of the wall).</summary>
        void BuildRoomNumbers(Transform parent)
        {
            if (!roomNumbers || layout == null) return;
            var plates = RoomNumbers.Plates(layout, layoutFloor);
            if (plates.Count == 0) return;

            var verts = new List<Vector3>(plates.Count * 96);
            var normals = new List<Vector3>(plates.Count * 96);
            var plateTris = new List<int>(plates.Count * 6);
            var letterTris = new List<int>(plates.Count * 120);
            foreach (var p in plates) AddPlate(p, verts, normals, plateTris, letterTris);

            var mesh = new Mesh { name = "RoomNumbers" };
            if (verts.Count > 60000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(plateTris, 0);
            mesh.SetTriangles(letterTris, 1);
            mesh.RecalculateBounds();

            var go = new GameObject("RoomNumbers");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // the vertices are in world space, like every other box of the floor
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { Flat(PlateColour, 0.35f), Flat(LetterColour, 0.2f) };
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<OwnedMesh>().mesh = mesh;                                  // freed with the floor
        }

        void AddPlate(RoomNumbers.Plate p, List<Vector3> verts, List<Vector3> normals, List<int> plateTris, List<int> letterTris)
        {
            string text = p.label;
            float letters = text.Length * DigitPitch - DotSize;                         // no gap after the last digit
            float width = letters + PlateMargin * 2f;
            float height = Mathf.Min(RoomNumbers.GlyphRows * DotSize + PlateMargin * 2f, Mathf.Max(0.2f, p.y1 - p.y0 - 0.1f));

            var normal = new Vector3(p.nx, 0f, p.nz);
            var right = new Vector3(-p.nz, 0f, p.nx);      // to the right of somebody facing the plate (they look along -normal)
            var up = Vector3.up;
            var centre = new Vector3(p.x, (p.y0 + p.y1) * 0.5f, p.z) + normal * PlateGap;   // the middle of the beam over the door

            AddQuad(centre, right, up, width * 0.5f, height * 0.5f, normal, verts, normals, plateTris);

            // letters: runs of lit dots in each row become one quad each
            var face = centre + normal * LetterGap;
            float left = -letters * 0.5f, top = RoomNumbers.GlyphRows * DotSize * 0.5f;
            for (int i = 0; i < text.Length; i++)
            {
                float x0 = left + i * DigitPitch;
                for (int row = 0; row < RoomNumbers.GlyphRows; row++)
                {
                    int col = 0;
                    while (col < RoomNumbers.GlyphColumns)
                    {
                        if (!RoomNumbers.Lit(text[i], col, row)) { col++; continue; }
                        int start = col;
                        while (col < RoomNumbers.GlyphColumns && RoomNumbers.Lit(text[i], col, row)) col++;
                        float a = x0 + start * DotSize, b = x0 + col * DotSize;          // run from a to b
                        float t = top - row * DotSize;                                    // its top edge
                        var c = face + right * ((a + b) * 0.5f) + up * (t - DotSize * 0.5f);
                        AddQuad(c, right, up, (b - a) * 0.5f, DotSize * 0.5f, normal, verts, normals, letterTris);
                    }
                }
            }
        }

        /// <summary>A flat rectangle facing <paramref name="normal"/> (clockwise from the front, which is what Unity draws).</summary>
        static void AddQuad(Vector3 c, Vector3 right, Vector3 up, float halfW, float halfH, Vector3 normal,
                            List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            int i = verts.Count;
            verts.Add(c - right * halfW - up * halfH);   // bottom left
            verts.Add(c - right * halfW + up * halfH);   // top left
            verts.Add(c + right * halfW + up * halfH);   // top right
            verts.Add(c + right * halfW - up * halfH);   // bottom right
            for (int k = 0; k < 4; k++) normals.Add(normal);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }
    }
}
