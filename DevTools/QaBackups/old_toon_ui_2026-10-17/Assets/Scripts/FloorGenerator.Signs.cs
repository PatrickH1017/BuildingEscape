using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BuildingEscape
{
    /// <summary>
    /// The direction signs on the corridor walls ("ROOMS 111-120" and an arrow, and "SECURITY OFFICE" and an arrow towards the guard outpost): see
    /// <see cref="DirectionSigns"/> for where they go and what they say. All the signs of a floor are ONE mesh with six materials (frame, plate,
    /// lettering, arrow, and the security signs' own plate and lettering), so they cost six draw calls however many there are. They are flat
    /// pictures hung on the wall, with no collider.
    /// </summary>
    public partial class FloorGenerator
    {
        [Header("Direction signs")]
        [Tooltip("A few signs on the corridor walls that point the way to the numbered rooms: \"ROOMS 111-120\" and an arrow.")]
        public bool directionSigns = true;

        int layoutSeed;                       // the seed the current layout was planned with

        static readonly Color SignFrameColour = new Color(0.52f, 0.56f, 0.62f), SignPlateColour = new Color(0.06f, 0.09f, 0.14f),
                              SignTextColour = new Color(0.96f, 0.94f, 0.86f), SignArrowColour = new Color(1.00f, 0.72f, 0.18f);
        // the security office's signs: a dark red plate with salmon lettering, so they stand out from the room-number signs
        static readonly Color SignSecurityPlate = new Color(0.22f, 0.04f, 0.05f), SignSecurityText = new Color(1.00f, 0.68f, 0.55f);
        const float SignFrameWidth = 0.014f, SignFrameStandoff = 0.006f, SignPlateStandoff = 0.012f, SignLetterStandoff = 0.018f;

        Layout signsOf;                       // the layout the cached plan belongs to
        List<DirSign> signPlan;

        /// <summary>The signs the current layout has (for tests and tools; worked out once per layout).</summary>
        public List<DirSign> PlanSigns()
        {
            if (layout == null) return new List<DirSign>();
            if (signsOf != layout || signPlan == null)
            {
                signPlan = DirectionSigns.Plan(layout, layoutFloor, cols, rows, openE, openN, cellSize, wallHeight, layoutSeed);
                signsOf = layout;
            }
            return signPlan;
        }

        /// <summary>
        /// The room a world point is in (the smallest of any kind that holds it, see <see cref="LootPlan.RoomAround"/>) with its rectangle, its number and its theme; its
        /// index is -1 when there is none. The loot scanner shows the loot of the room the player stands in and no other.
        /// </summary>
        public RoomSpot RoomSpotAt(Vector3 p)
        {
            var none = new RoomSpot { index = -1 };
            if (layout == null) return none;
            int i = LootPlan.RoomAround(layout, p.x, p.z);
            if (i < 0) return none;
            var room = layout.rooms[i];
            return new RoomSpot
            {
                index = i, bounds = room.bounds, theme = room.theme, kind = room.kind,
                label = RoomNumbers.IsNumbered(room) ? RoomNumbers.LabelOf(layout, room, layoutFloor) : null
            };
        }

        void BuildDirectionSigns(Transform parent)
        {
            if (!directionSigns || layout == null) return;
            var signs = PlanSigns();
            if (signs.Count == 0) return;

            var verts = new List<Vector3>(signs.Count * 400);
            var normals = new List<Vector3>(signs.Count * 400);
            var frameTris = new List<int>();
            var plateTris = new List<int>();
            var securityPlateTris = new List<int>();
            var securityTextTris = new List<int>();
            var textTris = new List<int>(signs.Count * 400);
            var arrowTris = new List<int>();
            var text = new List<DotText.Run>();
            var arrow = new List<DotText.Run>();

            foreach (var s in signs)
            {
                var normal = new Vector3(s.nx, 0f, s.nz);
                var right = new Vector3(-s.nz, 0f, s.nx);
                var up = Vector3.up;
                var centre = new Vector3(s.x, s.y, s.z);
                float halfW = s.width * 0.5f, halfH = s.height * 0.5f;

                AddQuad(centre + normal * SignFrameStandoff, right, up, halfW + SignFrameWidth, halfH + SignFrameWidth, normal, verts, normals, frameTris);
                AddQuad(centre + normal * SignPlateStandoff, right, up, halfW, halfH, normal, verts, normals, s.security ? securityPlateTris : plateTris);

                text.Clear(); arrow.Clear();
                DirectionSigns.Compose(s, text, arrow);
                var face = centre + normal * SignLetterStandoff;
                float left = -s.Columns * s.dot * 0.5f, top = DirectionSigns.TextRows * s.dot * 0.5f;
                foreach (var run in text) AddRun(run, s.dot, face, right, up, left, top, normal, verts, normals, s.security ? securityTextTris : textTris);
                foreach (var run in arrow) AddRun(run, s.dot, face, right, up, left, top, normal, verts, normals, arrowTris);
            }

            var mesh = new Mesh { name = "DirectionSigns" };
            if (verts.Count > 60000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.subMeshCount = 6;
            mesh.SetTriangles(frameTris, 0);
            mesh.SetTriangles(plateTris, 1);
            mesh.SetTriangles(textTris, 2);
            mesh.SetTriangles(arrowTris, 3);
            mesh.SetTriangles(securityPlateTris, 4);
            mesh.SetTriangles(securityTextTris, 5);
            mesh.RecalculateBounds();

            var go = new GameObject("DirectionSigns");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);   // the vertices are in world space, like every other box of the floor
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = new[] { Flat(SignFrameColour, 0.45f), Flat(SignPlateColour, 0.3f), Flat(SignTextColour, 0.2f), Flat(SignArrowColour, 0.2f), Flat(SignSecurityPlate, 0.3f), Flat(SignSecurityText, 0.2f) };
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<OwnedMesh>().mesh = mesh;                                  // freed with the floor
        }

        /// <summary>One run of lit dots of a sign as a rectangle on its face.</summary>
        static void AddRun(DotText.Run run, float dot, Vector3 face, Vector3 right, Vector3 up, float left, float top, Vector3 normal,
                           List<Vector3> verts, List<Vector3> normals, List<int> tris)
        {
            float a = left + run.col * dot, b = left + (run.col + run.len) * dot;       // from a to b along the sign
            float t = top - run.row * dot;                                              // its top edge
            var c = face + right * ((a + b) * 0.5f) + up * (t - dot * 0.5f);
            AddQuad(c, right, up, (b - a) * 0.5f, dot * 0.5f, normal, verts, normals, tris);
        }
    }
}
